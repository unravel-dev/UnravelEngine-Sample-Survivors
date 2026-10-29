using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unravel.Core;

namespace Survivors
{
    /// <summary>What damage did to an enemy.</summary>
    public enum DamageResult
    {
        /// <summary>The enemy cannot be hit now (dead, dying, rising).</summary>
        Missed,
        Hurt,
        Killed,
    }

    /// <summary>
    /// Every regular enemy of a run, of any type, as rows of arrays. One spatial grid serves separation and weapon
    /// queries; each frame the rows step on worker threads (movement, the ability state machine, status effects),
    /// ability effects run on the main thread through <see cref="commandSink"/>, and the rows draw with one
    /// Graphics.DrawMeshInstanced call per (type, look). Main thread only, except the step it runs on workers itself.
    /// The class is split by concern: this file holds the rows, registration, spawning, the frame step and deaths;
    /// the Queries, Actions, Status, Behaviour and Draw parts hold the rest.
    /// </summary>
    public sealed partial class EnemyWorld
    {
        public const int MAX_ABILITIES = 4;
        private const int STEP_ROWS_PER_TASK = 192;
        private const int INITIAL_COMMANDS_PER_TASK = 16;
        private const float DEGREES_TO_RADIANS = MathF.PI / 180.0f;
        /// <summary>The grid's starting reach; registering a bigger type grows it.</summary>
        private const float INITIAL_MAX_RADIUS = 0.5f;
        /// <summary>Elites deal this much more ability damage.</summary>
        private const float ELITE_DAMAGE = 1.5f;
        /// <summary>A new enemy's first cooldown is a random share of the cooldown from this up to 1 + jitter.</summary>
        private const float FIRST_COOLDOWN_MIN_SHARE = 0.3f;
        /// <summary>Seconds of the death clip shown before the body shrinks away.</summary>
        private const float DEATH_CLIP_SECONDS = 0.55f;
        private const float DEATH_SHRINK_SECONDS = 0.15f;
        /// <summary>Most bodies playing their death at once; later deaths pop at once.</summary>
        private const int DEATH_CLIP_BUDGET = 300;
        /// <summary>Samples per second of every baked enemy clip.</summary>
        private const float BAKE_FRAMES_PER_SECOND = 30.0f;
        /// <summary>Experience multiplier of an elite kill.</summary>
        private const float ELITE_EXPERIENCE = 25.0f;

        private readonly List<EnemyType> types = new List<EnemyType>();
        private readonly Dictionary<string, BakedAnimation> bakes = new Dictionary<string, BakedAnimation>();
        private readonly int capacity;
        private int rowCount;
        private int aliveCount;
        private int dyingCount;
        private int freeCount;
        private float maxRadius = INITIAL_MAX_RADIUS;
        private int[] typeCounts = Array.Empty<int>();
        private readonly int[] typeOf;
        private Vector3[] currentPositions;
        private Vector3[] nextPositions;
        private readonly Vector3[] velocities;
        private readonly Vector3[] knockbacks;
        private readonly float[] yaws;
        private readonly float[] health;
        private readonly float[] maxHealth;
        private readonly float[] damageScale;
        private readonly float[] scaleOf;
        private readonly float[] radiusOf;
        /// <summary>Scales the damage a row takes; its owner sets it (bosses guarding, channeling, dazed).</summary>
        private readonly float[] damageTakenScale;
        private readonly EnemyState[] states;
        private readonly float[] stateTime;
        private readonly int[] castAbility;
        private readonly bool[] castFired;
        private readonly Vector3[] castTarget;
        private readonly float[] cooldowns;
        private readonly int[] clips;
        private readonly float[] clipTimes;
        private readonly float[] clipRates;
        private readonly float[] marchTime;
        private readonly Vector3[] marchGoal;
        private readonly float[] marchSpeed;
        private readonly bool[] marchToPoint;
        private readonly bool[] rewarded;
        private readonly bool[] quickDeath;
        private readonly bool[] alive;
        private readonly bool[] collidable;
        private readonly bool[] elite;
        /// <summary>Set by the step when burn or venom took the last health; FinishStep starts the death.</summary>
        private readonly bool[] diedInStep;
        private readonly int[] lastSource;
        private readonly uint[] generations;
        private readonly int[] freeRows;
        private readonly int[] queryRows;
        /// <summary>The row moved at once (spawn, teleport): its next draw has no motion blur from the old place.</summary>
        private readonly bool[] jumped;
        private readonly List<EnemyCommand>[] taskCommands;
        private readonly Action<int> stepTask;
        private float stepDeltaTime;
        private Vector4[] obstacles = Array.Empty<Vector4>();
        private readonly SpatialHashGrid grid = new SpatialHashGrid(1.0f);
        private bool isGridDirty = true;

        /// <summary>Where the enemies go (usually the hero), set every frame by the game.</summary>
        public Vector3 targetPosition;
        /// <summary>The hero's ground radius; melee reach and ability ranges measure to its edge.</summary>
        public float targetRadius = 0.5f;
        /// <summary>False when the hero is dead: enemies stop and stop casting.</summary>
        public bool hasTarget = true;
        /// <summary>Enemies stay within this distance of <see cref="arenaCenter"/>.</summary>
        public float arenaRadius = 70.0f;
        public Vector3 arenaCenter = Vector3.zero;
        public bool stepInParallel = true;
        /// <summary>Runs the effects of abilities on the main thread.</summary>
        public IEnemyCommandSink commandSink;
        /// <summary>Called once per death, on the main thread.</summary>
        public Action<EnemyDeath> onDeath;

        /// <summary>Enemies alive, not counting the dying.</summary>
        public int count => aliveCount;

        /// <summary>The registered types, by index.</summary>
        public IReadOnlyList<EnemyType> registeredTypes => types;

        public EnemyWorld(int capacity)
        {
            this.capacity = Math.Max(capacity, 1);
            int size = this.capacity;
            typeOf = new int[size];
            currentPositions = new Vector3[size];
            nextPositions = new Vector3[size];
            velocities = new Vector3[size];
            knockbacks = new Vector3[size];
            yaws = new float[size];
            health = new float[size];
            maxHealth = new float[size];
            damageScale = new float[size];
            scaleOf = new float[size];
            radiusOf = new float[size];
            damageTakenScale = new float[size];
            states = new EnemyState[size];
            stateTime = new float[size];
            castAbility = new int[size];
            castFired = new bool[size];
            castTarget = new Vector3[size];
            cooldowns = new float[size * MAX_ABILITIES];
            clips = new int[size];
            clipTimes = new float[size];
            clipRates = new float[size];
            marchTime = new float[size];
            marchGoal = new Vector3[size];
            marchSpeed = new float[size];
            marchToPoint = new bool[size];
            rewarded = new bool[size];
            quickDeath = new bool[size];
            alive = new bool[size];
            collidable = new bool[size];
            elite = new bool[size];
            diedInStep = new bool[size];
            lastSource = new int[size];
            generations = new uint[size];
            freeRows = new int[size];
            queryRows = new int[size];
            jumped = new bool[size];
            statuses = new RowStatus[size];
            statusDealt = new float[size];
            rowTransforms = new Matrix4x4[size];
            rowStates = new BakedAnimationState[size];
            drawTransforms = new Matrix4x4[size];
            drawPrevious = new Matrix4x4[size];
            drawStates = new BakedAnimationState[size];
            drawPreviousStates = new BakedAnimationState[size];
            drawLook = new int[size];
            int taskCount = (size + STEP_ROWS_PER_TASK - 1) / STEP_ROWS_PER_TASK;
            taskCommands = new List<EnemyCommand>[taskCount];
            for (int i = 0; i < taskCount; i++)
            {
                taskCommands[i] = new List<EnemyCommand>(INITIAL_COMMANDS_PER_TASK);
            }
            stepTask = StepTask;
        }

        /// <summary>
        /// Adds a type: bakes its clips and creates its look material instances. Takes a moment; register when the run
        /// loads. Throws when the mesh has no bones or a clip is missing.
        /// </summary>
        /// <returns>The type's index for <see cref="Spawn(int, Vector3, in SpawnOptions)"/>.</returns>
        public int Register(EnemyType type)
        {
            if (type == null)
            {
                throw new ArgumentException("No enemy type.");
            }
            if (type.isProxy)
            {
                return AddType(type);
            }
            if (type.mesh == null)
            {
                throw new ArgumentException($"{type.name} needs a mesh.");
            }
            if (type.abilities.Length > MAX_ABILITIES)
            {
                throw new ArgumentException($"{type.name} has more than {MAX_ABILITIES} abilities.");
            }
            if (type.clips[(int)EnemyClip.Walk] == null)
            {
                throw new ArgumentException($"{type.name} needs a walk clip.");
            }
            type.baked = GetBake(type.mesh, IndexClips(type));
            for (int role = 0; role < (int)EnemyClip.Count; role++)
            {
                if (type.clipIndex[role] < 0)
                {
                    type.clipIndex[role] = type.clipIndex[(int)EnemyClip.Walk];
                }
                type.clipLength[role] = type.baked.GetLength(type.clipIndex[role]);
            }
            type.looks = CreateLooks(type);
            return AddType(type);
        }

        /// <summary>Lists the distinct clips of a type in bake order and points each role at its clip (-1 for none).</summary>
        private static List<AnimationClip> IndexClips(EnemyType type)
        {
            var bakedClips = new List<AnimationClip>();
            for (int role = 0; role < (int)EnemyClip.Count; role++)
            {
                AnimationClip clip = type.clips[role];
                if (clip == null)
                {
                    type.clipIndex[role] = -1;
                    continue;
                }
                int existing = bakedClips.IndexOf(clip);
                type.clipIndex[role] = existing >= 0 ? existing : bakedClips.Count;
                if (existing < 0)
                {
                    bakedClips.Add(clip);
                }
            }
            return bakedClips;
        }

        private int AddType(EnemyType type)
        {
            type.index = types.Count;
            types.Add(type);
            Array.Resize(ref typeCounts, types.Count);
            maxRadius = MathF.Max(maxRadius, type.radius * type.scale * type.eliteScale);
            Array.Resize(ref bucketCounts, types.Count * (int)EnemyLook.Count);
            Array.Resize(ref bucketStarts, types.Count * (int)EnemyLook.Count);
            return type.index;
        }

        /// <summary>One bake per mesh and clip list: tint variants of a model share it.</summary>
        private BakedAnimation GetBake(Mesh mesh, List<AnimationClip> clipList)
        {
            var key = new System.Text.StringBuilder(mesh.uid.ToString());
            foreach (AnimationClip clip in clipList)
            {
                key.Append('|').Append(clip.uid.ToString());
            }
            string id = key.ToString();
            if (!bakes.TryGetValue(id, out BakedAnimation bake) || !bake.isValid)
            {
                bake = BakedAnimation.Bake(mesh, clipList.ToArray(), BAKE_FRAMES_PER_SECOND);
                bakes[id] = bake;
            }
            return bake;
        }

        /// <summary>Releases the bakes and material instances. Call when the run ends.</summary>
        public void Dispose()
        {
            foreach (BakedAnimation bake in bakes.Values)
            {
                bake.Dispose();
            }
            bakes.Clear();
            foreach (EnemyType type in types)
            {
                type.baked = null;
                if (type.looks == null)
                {
                    continue;
                }
                foreach (MaterialInstance look in type.looks)
                {
                    look?.Dispose();
                }
                type.looks = null;
            }
            types.Clear();
        }

        /// <summary>Static circles enemies walk around, each as (x, z, radius).</summary>
        public void SetObstacles(ReadOnlySpan<Vector3> circles)
        {
            obstacles = new Vector4[circles.Length];
            for (int i = 0; i < circles.Length; i++)
            {
                obstacles[i] = new Vector4(circles[i].x, 0.0f, circles[i].y, circles[i].z);
            }
        }

        /// <summary>Adds an enemy.</summary>
        /// <param name="type">A registered type index.</param>
        /// <param name="position">Where it appears (on the ground).</param>
        /// <param name="options">Elite, health, damage, rise and reward.</param>
        /// <returns>The enemy, or <see cref="Enemy.None"/> when the world is full.</returns>
        public Enemy Spawn(int type, Vector3 position, in SpawnOptions options)
        {
            int row = AllocateRow();
            if (row < 0)
            {
                return Enemy.None;
            }
            EnemyType kind = types[type];
            bool isElite = options.isElite;
            float scale = kind.scale * (isElite ? kind.eliteScale : 1.0f);
            typeOf[row] = type;
            currentPositions[row] = position;
            nextPositions[row] = position;
            velocities[row] = Vector3.zero;
            knockbacks[row] = Vector3.zero;
            Vector3 toTarget = targetPosition - position;
            yaws[row] = MathF.Atan2(toTarget.x, toTarget.z);
            maxHealth[row] = options.health > 0.0f ? options.health : kind.health * options.healthScale * (isElite ? kind.eliteHealth : 1.0f);
            health[row] = maxHealth[row];
            damageScale[row] = options.damageScale * (isElite ? ELITE_DAMAGE : 1.0f);
            damageTakenScale[row] = 1.0f;
            scaleOf[row] = scale;
            radiusOf[row] = kind.radius * scale;
            bool hasRiseClip = options.rises && kind.HasClip(EnemyClip.Rise);
            states[row] = options.rises && !kind.isProxy ? EnemyState.Rising : EnemyState.Chase;
            stateTime[row] = 0.0f;
            castAbility[row] = -1;
            castFired[row] = false;
            ResetCooldowns(row, kind);
            clips[row] = hasRiseClip ? kind.GetClip(EnemyClip.Rise) : kind.GetClip(EnemyClip.Walk);
            clipTimes[row] = hasRiseClip ? 0.0f : Random.Range(0.0f, kind.GetClipLength(EnemyClip.Walk));
            clipRates[row] = 1.0f;
            statuses[row].Reset();
            marchTime[row] = 0.0f;
            rewarded[row] = options.isRewarded;
            quickDeath[row] = false;
            alive[row] = true;
            collidable[row] = true;
            elite[row] = isElite;
            diedInStep[row] = false;
            lastSource[row] = -1;
            jumped[row] = true;
            aliveCount++;
            typeCounts[type]++;
            isGridDirty = true;
            return MakeId(row);
        }

        /// <summary>A free row (reused first), or -1 when the world is full.</summary>
        private int AllocateRow()
        {
            if (freeCount > 0)
            {
                return freeRows[--freeCount];
            }
            return rowCount < capacity ? rowCount++ : -1;
        }

        /// <summary>Staggers the first cooldowns so a fresh crowd does not attack in step.</summary>
        private void ResetCooldowns(int row, EnemyType kind)
        {
            for (int i = 0; i < MAX_ABILITIES; i++)
            {
                EnemyAbility ability = i < kind.abilities.Length ? kind.abilities[i] : null;
                cooldowns[row * MAX_ABILITIES + i] = ability != null ? ability.cooldown * Random.Range(FIRST_COOLDOWN_MIN_SHARE, 1.0f + ability.cooldownJitter) : 0.0f;
            }
        }

        /// <summary>Steps every enemy by <paramref name="dt"/> seconds and runs the ability effects.</summary>
        public void Step(float dt)
        {
            if (rowCount == 0)
            {
                return;
            }
            EnsureGrid();
            int taskCount = (rowCount + STEP_ROWS_PER_TASK - 1) / STEP_ROWS_PER_TASK;
            stepDeltaTime = dt;
            if (stepInParallel && taskCount > 1)
            {
                Parallel.For(0, taskCount, stepTask);
            }
            else
            {
                for (int task = 0; task < taskCount; task++)
                {
                    StepTask(task);
                }
            }
            (currentPositions, nextPositions) = (nextPositions, currentPositions);
            isGridDirty = true;
            FinishStep();
            EnsureGrid();
            RunCommands(taskCount);
        }

        /// <summary>One worker's share of the step: a block of rows and its own command list.</summary>
        private void StepTask(int task)
        {
            int first = task * STEP_ROWS_PER_TASK;
            StepRows(first, Math.Min(first + STEP_ROWS_PER_TASK, rowCount), stepDeltaTime, taskCommands[task]);
        }

        /// <summary>Removes every enemy without deaths.</summary>
        public void Clear()
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (alive[row])
                {
                    generations[row]++;
                }
                alive[row] = false;
                collidable[row] = false;
            }
            rowCount = 0;
            freeCount = 0;
            aliveCount = 0;
            dyingCount = 0;
            Array.Clear(typeCounts, 0, typeCounts.Length);
            isGridDirty = true;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Main thread bookkeeping after the step
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Credits the step's burn and venom damage, starts the deaths it caused and frees finished bodies.</summary>
        private void FinishStep()
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (statusDealt[row] > 0.0f)
                {
                    int source = lastSource[row];
                    if (source >= 0 && source < STATUS_SOURCES)
                    {
                        statusDamageBySource[source] += statusDealt[row];
                    }
                    statusDealt[row] = 0.0f;
                }
                if (!alive[row])
                {
                    continue;
                }
                if (diedInStep[row])
                {
                    diedInStep[row] = false;
                    if (states[row] != EnemyState.Dying)
                    {
                        BeginDeath(row);
                    }
                    continue;
                }
                if (states[row] == EnemyState.Dying && stateTime[row] >= GetDeathLength(row) + DEATH_SHRINK_SECONDS)
                {
                    dyingCount--;
                    Release(row);
                }
            }
        }

        /// <summary>Hands the step's ability commands to the sink, skipping casters that are gone by now.</summary>
        private void RunCommands(int taskCount)
        {
            for (int task = 0; task < taskCount; task++)
            {
                List<EnemyCommand> commands = taskCommands[task];
                for (int i = 0; i < commands.Count && commandSink != null; i++)
                {
                    EnemyCommand command = commands[i];
                    // A caster that died after it queued the command (burned out in FinishStep, blown up by an
                    // earlier command) casts nothing.
                    if (GetRow(command.enemy) >= 0)
                    {
                        commandSink.Execute(this, in command);
                    }
                }
                commands.Clear();
            }
        }

        private void BeginDeath(int row)
        {
            EnemyType kind = types[typeOf[row]];
            ref RowStatus status = ref statuses[row];
            bool wasFrozen = status.freezeTime > 0.0f;
            bool wasMarked = status.markTime > 0.0f;
            bool wasBloodied = status.bloodTime > 0.0f;
            states[row] = EnemyState.Dying;
            stateTime[row] = 0.0f;
            collidable[row] = false;
            bool alternate = kind.HasClip(EnemyClip.DeathAlt) && (row & 1) == 1;
            clips[row] = kind.GetClip(alternate ? EnemyClip.DeathAlt : EnemyClip.Death);
            clipTimes[row] = 0.0f;
            quickDeath[row] = dyingCount >= DEATH_CLIP_BUDGET || kind.isProxy;
            dyingCount++;
            clipRates[row] = 1.0f;
            status.ClearOnDeath();
            aliveCount--;
            typeCounts[typeOf[row]]--;
            isGridDirty = true;
            onDeath?.Invoke(new EnemyDeath
            {
                enemy = MakeId(row),
                type = typeOf[row],
                position = currentPositions[row],
                isElite = elite[row],
                experience = rewarded[row] ? kind.experience * (elite[row] ? ELITE_EXPERIENCE : 1.0f) : 0.0f,
                source = lastSource[row],
                isRewarded = rewarded[row],
                wasFrozen = wasFrozen,
                wasMarked = wasMarked,
                wasBloodied = wasBloodied,
            });
        }

        /// <summary>Seconds of death clip a dying row shows before it shrinks away.</summary>
        private float GetDeathLength(int row)
        {
            EnemyType kind = types[typeOf[row]];
            if (quickDeath[row] || !kind.HasClip(EnemyClip.Death))
            {
                return 0.0f;
            }
            return MathF.Min(kind.baked.GetLength(clips[row]), DEATH_CLIP_SECONDS);
        }

        private void Release(int row)
        {
            alive[row] = false;
            collidable[row] = false;
            generations[row]++;
            freeRows[freeCount++] = row;
            isGridDirty = true;
        }

        private void EnsureGrid()
        {
            if (!isGridDirty)
            {
                return;
            }
            grid.cellSize = 2.0f * maxRadius;
            grid.Build(currentPositions.AsSpan(0, rowCount), collidable.AsSpan(0, rowCount));
            isGridDirty = false;
        }

        private Enemy MakeId(int row) => new Enemy { row = row, generation = generations[row] };
    }
}

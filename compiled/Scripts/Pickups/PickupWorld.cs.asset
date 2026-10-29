using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Things the hero collects.</summary>
    public enum PickupKind
    {
        /// <summary>Azure shard, 1-4 experience.</summary>
        GemSmall,
        /// <summary>Verdant shard, 5-19.</summary>
        GemMedium,
        /// <summary>Amber shard, 20-99.</summary>
        GemLarge,
        /// <summary>Violet shard, 100 and more.</summary>
        GemEpic,
        /// <summary>The Crimson Cluster stray gems merge into when too many lie around.</summary>
        GemHuge,
        Gold,
        /// <summary>Roast Haunch: heals.</summary>
        Food,
        /// <summary>Lodestone: every gem flies to the hero.</summary>
        Magnet,
        /// <summary>Holy Bell: clears the screen.</summary>
        Bomb,
        /// <summary>Frost Hourglass: freezes the screen.</summary>
        Freeze,
        Chest,
        Count,
    }

    /// <summary>How a pickup kind looks. The world owns <see cref="materialInstance"/> once the visual is set.</summary>
    public sealed class PickupVisual
    {
        public Mesh mesh;
        public Material material;
        public MaterialInstance materialInstance;
        public float scale = 1.0f;
        /// <summary>Degrees per second about the up axis.</summary>
        public float spin = 90.0f;
        /// <summary>Hover height and bob amplitude (m).</summary>
        public float height = 0.45f;
        public float bob = 0.12f;
        public Vector3 meshRotation = Vector3.zero;
        /// <summary>Emissive intensity of the material instance the glow pulses around.</summary>
        public float glow;
        /// <summary>Share of <see cref="glow"/> added at the top of each pulse; 0 keeps the glow steady.</summary>
        public float glowPulse;
    }

    /// <summary>
    /// Every pickup on the ground as rows: gems, gold, food, magnets, bombs and chests. Pickups within the hero's
    /// magnet radius fly to the hero, accelerating; collecting calls <see cref="onCollect"/>. Far gems merge per cell
    /// every few seconds; gems beyond the cap go into one huge gem worth their sum. Drawn instanced per kind with the
    /// looks it owns. Main thread.
    /// </summary>
    public sealed class PickupWorld
    {
        private const float COLLECT_DISTANCE = 0.7f;
        private const float ATTRACT_START_SPEED = -4.0f;
        private const float ATTRACT_ACCELERATION = 42.0f;
        private const float MAX_ATTRACT_SPEED = 30.0f;
        private const float MIN_ATTRACT_DISTANCE = 1e-4f;
        private const float SPAWN_POP_SPEED = 3.0f;
        private const float SPAWN_POP_TIME = 0.35f;
        private const float DESPAWN_DISTANCE = 120.0f;
        /// <summary>Merge pass: every 2 s, gems farther than 28 m from the hero merge per 4 m cell.</summary>
        private const float MERGE_INTERVAL = 2.0f;
        private const float MERGE_DISTANCE = 28.0f;
        private const float MERGE_CELL = 4.0f;
        /// <summary>Least experience of each gem tier above the small one.</summary>
        private const float MEDIUM_GEM_EXPERIENCE = 5.0f;
        private const float LARGE_GEM_EXPERIENCE = 20.0f;
        private const float EPIC_GEM_EXPERIENCE = 100.0f;
        /// <summary>Bob speed (radians per second); each row is this much out of step with the last, so a pile ripples.</summary>
        private const float BOB_SPEED = 3.0f;
        private const float BOB_PHASE_PER_ROW = 0.37f;
        /// <summary>The bob phase turned into a spin offset (about degrees per radian), so neighbours do not spin in step.</summary>
        private const float SPIN_PHASE_DEGREES = 57.0f;
        /// <summary>The huge gem grows by this share per tenfold of its value, up to the maximum.</summary>
        private const float HUGE_GEM_GROWTH_PER_DECADE = 0.25f;
        private const float HUGE_GEM_MAX_GROWTH = 0.8f;
        private const float GLOW_PULSE_HERTZ = 1.0f;

        private readonly int capacity;
        private readonly int mergeThreshold;
        private int rowCount;
        private int liveCount;
        private int gemCount;
        private readonly Stack<int> freeRows = new Stack<int>();
        private readonly PickupKind[] kinds;
        private readonly Vector3[] positions;
        private readonly Vector3[] previous;
        private readonly Vector3[] popVelocity;
        private readonly float[] values;
        private readonly float[] ages;
        private readonly float[] attractSpeed;
        private readonly bool[] attracted;
        private readonly bool[] alive;
        private readonly PickupVisual[] visuals = new PickupVisual[(int)PickupKind.Count];
        private readonly int[] kindCounts = new int[(int)PickupKind.Count];
        private readonly int[] kindStarts = new int[(int)PickupKind.Count];
        private readonly Matrix4x4[] drawTransforms;
        private readonly Matrix4x4[] drawPrevious;
        private readonly Dictionary<long, int> mergeCells = new Dictionary<long, int>();
        private int hugeGem = -1;
        private float mergeTimer = MERGE_INTERVAL;

        /// <summary>The hero's position and pickup radius, set every frame.</summary>
        public Vector3 heroPosition;
        public float magnetRadius = 2.5f;
        /// <summary>Called when the hero collects a pickup: its kind, value and where it was.</summary>
        public Action<PickupKind, float, Vector3> onCollect;
        /// <summary>False during the Hollow King's fall: gems still fly and chime, but give no experience.</summary>
        public bool gemsGiveExperience = true;

        public int count => liveCount;

        /// <summary>Gems on the ground (the lodestone pity rule).</summary>
        public int gemsOnGround => gemCount;

        public PickupWorld(int capacity, int mergeThreshold)
        {
            this.capacity = Math.Max(capacity, 1);
            this.mergeThreshold = mergeThreshold;
            kinds = new PickupKind[this.capacity];
            positions = new Vector3[this.capacity];
            previous = new Vector3[this.capacity];
            popVelocity = new Vector3[this.capacity];
            values = new float[this.capacity];
            ages = new float[this.capacity];
            attractSpeed = new float[this.capacity];
            attracted = new bool[this.capacity];
            alive = new bool[this.capacity];
            drawTransforms = new Matrix4x4[this.capacity];
            drawPrevious = new Matrix4x4[this.capacity];
        }

        /// <summary>Sets how a kind looks; the world owns the visual's material instance from now on.</summary>
        public void SetVisual(PickupKind kind, PickupVisual visual) => visuals[(int)kind] = visual;

        /// <summary>Pulses the glow of the looks that pulse; once per frame.</summary>
        public void PulseGlow(float time)
        {
            float wave = 0.5f + 0.5f * MathF.Sin(time * GLOW_PULSE_HERTZ * MathF.PI * 2.0f);
            foreach (PickupVisual visual in visuals)
            {
                if (visual?.materialInstance != null && visual.glowPulse > 0.0f)
                {
                    visual.materialInstance.emissiveIntensity = visual.glow * (1.0f + visual.glowPulse * wave);
                }
            }
        }

        /// <summary>Releases the looks' material instances (run teardown).</summary>
        public void DisposeLooks()
        {
            foreach (PickupVisual visual in visuals)
            {
                if (visual?.materialInstance == null)
                {
                    continue;
                }
                visual.materialInstance.Dispose();
                visual.materialInstance = null;
            }
        }

        public static bool IsGem(PickupKind kind) => kind <= PickupKind.GemHuge;

        /// <summary>The gem tier for an experience value.</summary>
        public static PickupKind GetGemKind(float experience)
        {
            if (experience >= EPIC_GEM_EXPERIENCE)
            {
                return PickupKind.GemEpic;
            }
            if (experience >= LARGE_GEM_EXPERIENCE)
            {
                return PickupKind.GemLarge;
            }
            return experience >= MEDIUM_GEM_EXPERIENCE ? PickupKind.GemMedium : PickupKind.GemSmall;
        }

        /// <summary>Pickups of a kind on the ground (drop caps).</summary>
        public int CountOf(PickupKind kind)
        {
            int total = 0;
            for (int row = 0; row < rowCount; row++)
            {
                if (alive[row] && kinds[row] == kind)
                {
                    total++;
                }
            }
            return total;
        }

        /// <summary>Drops a pickup; it pops out a little in a random direction.</summary>
        public int Spawn(PickupKind kind, Vector3 position, float value, bool pops = true)
        {
            // The huge gem itself is exempt: it is what overflow merges into.
            if (kind != PickupKind.GemHuge && IsGem(kind) && gemCount >= mergeThreshold && TryMerge(position, value))
            {
                return hugeGem;
            }
            int row;
            if (freeRows.Count > 0)
            {
                row = freeRows.Pop();
            }
            else if (rowCount < capacity)
            {
                row = rowCount++;
            }
            else
            {
                return -1;
            }
            kinds[row] = kind;
            position.y = 0.0f;
            positions[row] = position;
            previous[row] = position;
            Vector2 direction = Random.insideUnitCircle;
            popVelocity[row] = pops ? new Vector3(direction.x, 0.0f, direction.y) * SPAWN_POP_SPEED : Vector3.zero;
            values[row] = value;
            ages[row] = 0.0f;
            attractSpeed[row] = ATTRACT_START_SPEED;
            attracted[row] = false;
            alive[row] = true;
            liveCount++;
            if (IsGem(kind))
            {
                gemCount++;
            }
            return row;
        }

        /// <summary>Sends the gems and gold within a radius of a point to the hero (wisps carrying gems).</summary>
        public int AttractWithin(Vector3 center, float radius)
        {
            float limit = radius * radius;
            int attractedCount = 0;
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row] || attracted[row] || !IsAttractable(kinds[row]))
                {
                    continue;
                }
                if (Ground.DistanceSquared(positions[row], center) <= limit)
                {
                    attracted[row] = true;
                    attractedCount++;
                }
            }
            return attractedCount;
        }

        /// <summary>Sends every gem (and gold) on the ground to the hero.</summary>
        public void AttractAll()
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (alive[row] && IsAttractable(kinds[row]))
                {
                    attracted[row] = true;
                }
            }
        }

        /// <summary>Positions of the pickups of a kind (chest markers); returns how many were written.</summary>
        public int CollectPositions(PickupKind kind, Span<Vector3> buffer)
        {
            int written = 0;
            for (int row = 0; row < rowCount && written < buffer.Length; row++)
            {
                if (alive[row] && kinds[row] == kind)
                {
                    buffer[written++] = positions[row];
                }
            }
            return written;
        }

        /// <summary>Every chest on the ground with its item count (chest beacons); returns how many were written.</summary>
        public int CollectChests(Span<Vector3> positionsOut, Span<float> itemsOut)
        {
            int written = 0;
            int capacity = Math.Min(positionsOut.Length, itemsOut.Length);
            for (int row = 0; row < rowCount && written < capacity; row++)
            {
                if (alive[row] && kinds[row] == PickupKind.Chest)
                {
                    positionsOut[written] = positions[row];
                    itemsOut[written] = values[row];
                    written++;
                }
            }
            return written;
        }

        /// <summary>The nearest pickup to a point that is not already flying to the hero (the test autopilot).</summary>
        public bool FindNearest(Vector3 from, float maxDistance, out Vector3 position) => TryFindNearest(from, maxDistance, null, out position);

        /// <summary>The nearest pickup of one kind within a distance (the test autopilot's food and chest runs).</summary>
        public bool FindNearestOfKind(Vector3 from, float maxDistance, PickupKind kind, out Vector3 position) => TryFindNearest(from, maxDistance, kind, out position);

        /// <summary>Where the huge gem (overflow of the gem cap) lies, for its off-screen marker.</summary>
        public bool TryGetHugeGem(out Vector3 position)
        {
            bool exists = hugeGem >= 0 && alive[hugeGem];
            position = exists ? positions[hugeGem] : Vector3.zero;
            return exists;
        }

        public void Step(float dt)
        {
            mergeTimer -= dt;
            if (mergeTimer <= 0.0f)
            {
                mergeTimer = MERGE_INTERVAL;
                MergeFarGems();
            }
            float magnetSquared = magnetRadius * magnetRadius;
            for (int row = 0; row < rowCount; row++)
            {
                if (alive[row])
                {
                    StepRow(row, dt, magnetSquared);
                }
            }
        }

        public void Draw()
        {
            if (liveCount == 0)
            {
                return;
            }
            CountKinds();
            float time = Time.time;
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row])
                {
                    continue;
                }
                int kind = (int)kinds[row];
                PickupVisual visual = visuals[kind];
                if (visual == null)
                {
                    continue;
                }
                int slot = kindStarts[kind] + kindCounts[kind]++;
                drawTransforms[slot] = MakeTransform(visual, positions[row], time, row, values[row], kinds[row]);
                drawPrevious[slot] = MakeTransform(visual, previous[row], time - Time.deltaTime, row, values[row], kinds[row]);
            }
            for (int kind = 0; kind < visuals.Length; kind++)
            {
                PickupVisual visual = visuals[kind];
                if (visual == null || !AssetChecks.IsLoaded(visual.mesh) || kindCounts[kind] == 0)
                {
                    continue;
                }
                Graphics.DrawMeshInstanced(visual.mesh,
                                           visual.material,
                                           drawTransforms.AsSpan(kindStarts[kind], kindCounts[kind]),
                                           drawPrevious.AsSpan(kindStarts[kind], kindCounts[kind]),
                                           false,
                                           materialInstance: visual.materialInstance);
            }
        }

        public void Clear()
        {
            for (int row = 0; row < rowCount; row++)
            {
                alive[row] = false;
            }
            rowCount = 0;
            liveCount = 0;
            gemCount = 0;
            hugeGem = -1;
            mergeTimer = MERGE_INTERVAL;
            freeRows.Clear();
        }

        /// <summary>Gems and gold fly to the hero; food, the rare pickups and chests wait to be walked over.</summary>
        private static bool IsAttractable(PickupKind kind) => IsGem(kind) || kind == PickupKind.Gold;

        /// <summary>Pops a fresh pickup out, pulls it in once it is in reach and collects or forgets it.</summary>
        private void StepRow(int row, float dt, float magnetSquared)
        {
            previous[row] = positions[row];
            ages[row] += dt;
            if (ages[row] < SPAWN_POP_TIME)
            {
                positions[row] += popVelocity[row] * (dt * (1.0f - ages[row] / SPAWN_POP_TIME));
            }
            Vector3 toHero = heroPosition - positions[row];
            toHero.y = 0.0f;
            float distanceSquared = toHero.sqrMagnitude;
            if (!attracted[row] && distanceSquared <= magnetSquared && kinds[row] != PickupKind.Chest)
            {
                attracted[row] = true;
            }
            if (attracted[row])
            {
                // A small hop away first, then an accelerating rush: the classic vacuum.
                attractSpeed[row] = MathF.Min(attractSpeed[row] + ATTRACT_ACCELERATION * dt, MAX_ATTRACT_SPEED);
                float distance = MathF.Sqrt(distanceSquared);
                if (distance > MIN_ATTRACT_DISTANCE)
                {
                    positions[row] += toHero / distance * MathF.Min(attractSpeed[row] * dt, distance);
                }
                distanceSquared = (heroPosition - positions[row]).sqrMagnitude;
            }
            if (distanceSquared <= COLLECT_DISTANCE * COLLECT_DISTANCE)
            {
                Collect(row);
            }
            else if (!IsGem(kinds[row]) && kinds[row] != PickupKind.Chest && distanceSquared > DESPAWN_DISTANCE * DESPAWN_DISTANCE)
            {
                Remove(row);
            }
        }

        /// <summary>Sizes the draw slots of each kind.</summary>
        private void CountKinds()
        {
            Array.Clear(kindCounts, 0, kindCounts.Length);
            for (int row = 0; row < rowCount; row++)
            {
                if (alive[row])
                {
                    kindCounts[(int)kinds[row]]++;
                }
            }
            int start = 0;
            for (int i = 0; i < kindCounts.Length; i++)
            {
                kindStarts[i] = start;
                start += kindCounts[i];
                kindCounts[i] = 0;
            }
        }

        /// <param name="kind">Only pickups of this kind; null: any pickup not already flying to the hero.</param>
        private bool TryFindNearest(Vector3 from, float maxDistance, PickupKind? kind, out Vector3 position)
        {
            position = from;
            float best = maxDistance * maxDistance;
            bool found = false;
            for (int row = 0; row < rowCount; row++)
            {
                bool isCandidate = kind.HasValue ? kinds[row] == kind.Value : !attracted[row];
                if (!alive[row] || !isCandidate)
                {
                    continue;
                }
                float distance = Ground.DistanceSquared(positions[row], from);
                if (distance < best)
                {
                    best = distance;
                    position = positions[row];
                    found = true;
                }
            }
            return found;
        }

        private void Collect(int row)
        {
            PickupKind kind = kinds[row];
            float value = values[row];
            Vector3 position = positions[row];
            Remove(row);
            onCollect?.Invoke(kind, value, position);
        }

        private void Remove(int row)
        {
            alive[row] = false;
            liveCount--;
            if (IsGem(kinds[row]))
            {
                gemCount--;
            }
            if (row == hugeGem)
            {
                hugeGem = -1;
            }
            freeRows.Push(row);
        }

        /// <summary>
        /// Gems farther than 28 m from the hero collapse into one gem per 4 m cell (value summed, tier recomputed, at the
        /// richest gem's spot), so the ground stays under the cap while the gems near the hero stay exactly as dropped.
        /// </summary>
        private void MergeFarGems()
        {
            mergeCells.Clear();
            float limit = MERGE_DISTANCE * MERGE_DISTANCE;
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row] || attracted[row] || row == hugeGem || !IsGem(kinds[row]))
                {
                    continue;
                }
                if (Ground.DistanceSquared(positions[row], heroPosition) < limit)
                {
                    continue;
                }
                long cell = GroundCell.GetKey(positions[row], MERGE_CELL);
                if (!mergeCells.TryGetValue(cell, out int keeper))
                {
                    mergeCells[cell] = row;
                    continue;
                }
                int merged = row;
                if (values[row] > values[keeper])
                {
                    (keeper, merged) = (row, keeper);
                    mergeCells[cell] = keeper;
                }
                values[keeper] += values[merged];
                kinds[keeper] = GetGemKind(values[keeper]);
                Remove(merged);
            }
        }

        /// <summary>Over the gem cap, a new gem's value goes to the run's single huge gem, wherever it fell.</summary>
        private bool TryMerge(Vector3 position, float value)
        {
            if (hugeGem < 0 || !alive[hugeGem])
            {
                hugeGem = -1;
                int row = Spawn(PickupKind.GemHuge, position, 0.0f, false);
                if (row < 0)
                {
                    return false;
                }
                hugeGem = row;
            }
            values[hugeGem] += value;
            return true;
        }

        private static Matrix4x4 MakeTransform(PickupVisual visual, Vector3 position, float time, int row, float value, PickupKind kind)
        {
            float phase = row * BOB_PHASE_PER_ROW;
            position.y = visual.height + MathF.Sin(time * BOB_SPEED + phase) * visual.bob;
            float scale = visual.scale;
            if (kind == PickupKind.GemHuge)
            {
                scale *= 1.0f + MathF.Min(MathF.Log10(1.0f + value) * HUGE_GEM_GROWTH_PER_DECADE, HUGE_GEM_MAX_GROWTH);
            }
            Quaternion rotation = Quaternion.Euler(0.0f, time * visual.spin + phase * SPIN_PHASE_DEGREES, 0.0f) * Quaternion.Euler(visual.meshRotation);
            return Matrix4x4.TRS(position, rotation, new Vector3(scale, scale, scale));
        }
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Whom a projectile hurts.</summary>
    public enum Team : byte
    {
        Hero,
        Enemy,
    }

    /// <summary>How a projectile moves.</summary>
    public enum ProjectileMotion : byte
    {
        /// <summary>Straight along its velocity.</summary>
        Linear,
        /// <summary>Turns toward the nearest enemy (or the hero) at <see cref="ProjectileSpawn.homing"/> rad/s.</summary>
        Homing,
        /// <summary>Flies out, slows, comes back to the hero and ends there.</summary>
        Boomerang,
        /// <summary>Circles the hero at <see cref="ProjectileSpawn.orbitRadius"/>.</summary>
        Orbit,
        /// <summary>Lobbed: falls under gravity and ends when it lands.</summary>
        Arc,
        /// <summary>Stays where it is (mines, lingering blades).</summary>
        Static,
    }

    /// <summary>
    /// One particle layer drawn at a projectile every frame (a white-hot core, a coloured halo, licking flames): its
    /// short-lived particles fade behind the moving projectile into a comet tail. Sized by the projectile's scale.
    /// </summary>
    public sealed class ProjectileHead
    {
        public string effect;
        /// <summary>Particle size (m) at scale 1.</summary>
        public float size = 0.5f;
        public Color tint = Color.white;
        /// <summary>Takes the projectile's own tint when it has one (an Arcane Echo cast); white-hot cores do not.</summary>
        public bool takesSpawnTint = true;
        /// <summary>Drawn on the ground under the projectile: where it really flies, seen from the tilted camera.</summary>
        public bool onGround;
        internal int id = Effects.UNRESOLVED;

        public ProjectileHead(string effect, float size, Color tint, bool takesSpawnTint = true, bool onGround = false)
        {
            this.effect = effect;
            this.size = size;
            this.tint = tint;
            this.takesSpawnTint = takesSpawnTint;
            this.onGround = onGround;
        }
    }

    /// <summary>How a projectile looks: a mesh drawn instanced, oriented along its travel or spinning.</summary>
    public sealed class ProjectileVisual
    {
        public string name;
        public Mesh mesh;
        public Material material;
        public MaterialInstance materialInstance;
        public float scale = 1.0f;
        /// <summary>Spin (degrees per second) about the visual's up axis; 0 aligns the mesh's +Z with the travel.</summary>
        public float spin = 0.0f;
        /// <summary>Extra rotation (Euler degrees) applied in the mesh's own space first.</summary>
        public Vector3 meshRotation = Vector3.zero;
        /// <summary>Height above the ground a ground projectile flies at.</summary>
        public float height = 1.0f;
        /// <summary>Particle effect emitted along the path, or null.</summary>
        public string trail;
        /// <summary>Seconds between trail emissions.</summary>
        public float trailInterval = 0.03f;
        public bool castShadows = false;
        /// <summary>Particle layers drawn at the projectile every frame (see <see cref="ProjectileHead"/>).</summary>
        public ProjectileHead[] heads = Array.Empty<ProjectileHead>();
        /// <summary>Skips the mesh: the heads are the whole look (orbs, darts, wisps).</summary>
        public bool hideMesh = false;
        internal int trailId = Effects.UNRESOLVED;
    }

    /// <summary>What a weapon wants to happen when its projectile hits or ends. All calls run on the main thread.</summary>
    public interface IProjectileHandler
    {
        /// <summary>The projectile touched an enemy (hero team) it has not hit yet.</summary>
        void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow);

        /// <summary>The projectile ended: out of time, out of pierce, landed, or back at the hero.</summary>
        void OnExpire(ProjectileWorld world, int projectile, Vector3 position);
    }

    /// <summary>The fields of a new projectile.</summary>
    public struct ProjectileSpawn
    {
        public Team team;
        public int visual;
        public ProjectileMotion motion;
        public Vector3 position;
        public Vector3 velocity;
        public float lifetime;
        public float damage;
        /// <summary>Hit radius (m) on the ground plane.</summary>
        public float radius;
        /// <summary>
        /// Hero projectiles: enemies it passes through after the first (-1 never stops). Enemy projectiles end on the
        /// hero unless this is above 0.
        /// </summary>
        public int pierce;
        /// <summary>Knockback speed it gives.</summary>
        public float knockback;
        public float homing;
        public float orbitRadius;
        /// <summary>Orbit: radians per second around the hero.</summary>
        public float orbitSpeed;
        /// <summary>Orbit: the current angle (radians) around the hero.</summary>
        public float orbitAngle;
        /// <summary>Arc: downward acceleration (m/s^2).</summary>
        public float gravity;
        /// <summary>Seconds before the same enemy can be hit again (orbits, statics); 0 hits each enemy once.</summary>
        public float rehitInterval;
        public float scale;
        /// <summary>Weapon slot for kill credit, -1 for none.</summary>
        public int source;
        public IProjectileHandler handler;
        /// <summary>Any value the handler wants back.</summary>
        public int userValue;
        /// <summary>Enemy projectiles: who fired it, named on the death screen when it hurts the hero.</summary>
        public string sourceName;
        /// <summary>Visual only: grows the particle heads (rank and evolution), never the hit radius. 0 means 1.</summary>
        public float headScale;
        /// <summary>Visual only: replaces the heads' colour (alpha 0 keeps the look's own).</summary>
        public Color headTint;
    }

    /// <summary>
    /// Every projectile of a run, hero and enemy, as rows. Hero projectiles hit enemies through the
    /// <see cref="EnemyWorld"/> grid and hand each hit to their weapon's <see cref="IProjectileHandler"/>; enemy
    /// projectiles hit the hero through <see cref="heroTarget"/> and report it with <see cref="heroDamagedBy"/>.
    /// Main thread.
    /// </summary>
    public sealed class ProjectileWorld
    {
        private const int MAX_REMEMBERED_HITS = 8;
        private const int QUERY_CAPACITY = 64;
        /// <summary>Hero homing projectiles turn toward the nearest enemy within this radius (m).</summary>
        private const float HOMING_RANGE = 14.0f;
        private const float BOOMERANG_RETURN_DISTANCE = 0.8f;
        /// <summary>Outbound boomerangs start at this multiple of their speed and slow linearly to a stop, so they cover
        /// speed x half their life.</summary>
        private const float BOOMERANG_LAUNCH_BOOST = 2.0f;
        /// <summary>Returning boomerangs fly back this much faster than they were thrown.</summary>
        private const float BOOMERANG_RETURN_BOOST = 1.4f;
        /// <summary>Seconds of life a returning boomerang always keeps, so it reaches the hero before it expires.</summary>
        private const float BOOMERANG_RETURN_MARGIN = 0.5f;

        private readonly int capacity;
        private int rowCount;
        private int liveCount;
        private readonly Stack<int> freeRows = new Stack<int>();
        private readonly List<ProjectileVisual> visuals = new List<ProjectileVisual>();

        private readonly ProjectileSpawn[] data;
        private readonly bool[] alive;
        private readonly float[] age;
        private readonly float[] trailTimer;
        private readonly Enemy[] hits;
        private readonly float[] hitTimes;
        private readonly int[] hitCursor;
        private readonly bool[] returning;
        private readonly Vector3[] previousPositions;
        private readonly int[] queryRows = new int[QUERY_CAPACITY];
        private Matrix4x4[] drawTransforms;
        private Matrix4x4[] drawPrevious;
        private int[] visualCounts = Array.Empty<int>();
        private int[] visualStarts = Array.Empty<int>();

        public EnemyWorld enemies;
        /// <summary>Where Orbit and Boomerang projectiles anchor and where enemy projectiles aim.</summary>
        public Vector3 heroPosition;
        public IHeroTarget heroTarget;
        /// <summary>Raised with <see cref="ProjectileSpawn.sourceName"/> when an enemy projectile hurts the hero.</summary>
        public event Action<string> heroDamagedBy;
        /// <summary>Particle trails along paths.</summary>
        public Effects effects;

        public int count => liveCount;

        public ProjectileWorld(int capacity)
        {
            this.capacity = Math.Max(capacity, 1);
            data = new ProjectileSpawn[this.capacity];
            alive = new bool[this.capacity];
            age = new float[this.capacity];
            trailTimer = new float[this.capacity];
            hits = new Enemy[this.capacity * MAX_REMEMBERED_HITS];
            hitTimes = new float[this.capacity * MAX_REMEMBERED_HITS];
            hitCursor = new int[this.capacity];
            returning = new bool[this.capacity];
            previousPositions = new Vector3[this.capacity];
            drawTransforms = new Matrix4x4[this.capacity];
            drawPrevious = new Matrix4x4[this.capacity];
        }

        /// <summary>Adds a look; returns its index for <see cref="ProjectileSpawn.visual"/>.</summary>
        public int RegisterVisual(ProjectileVisual visual)
        {
            int index = visuals.Count;
            visuals.Add(visual);
            Array.Resize(ref visualCounts, visuals.Count);
            Array.Resize(ref visualStarts, visuals.Count);
            return index;
        }

        /// <summary>The index of a look by name, or -1.</summary>
        public int FindVisual(string name)
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i].name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        public ProjectileVisual GetVisual(int index) => visuals[index];

        /// <summary>Adds a projectile; returns its row, or -1 when full.</summary>
        public int Spawn(in ProjectileSpawn spawn)
        {
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
            data[row] = spawn;
            if (data[row].scale <= 0.0f)
            {
                data[row].scale = 1.0f;
            }
            alive[row] = true;
            age[row] = 0.0f;
            trailTimer[row] = 0.0f;
            hitCursor[row] = 0;
            returning[row] = false;
            previousPositions[row] = spawn.position;
            for (int i = 0; i < MAX_REMEMBERED_HITS; i++)
            {
                hits[row * MAX_REMEMBERED_HITS + i] = Enemy.None;
            }
            liveCount++;
            return row;
        }

        public ref ProjectileSpawn Get(int row) => ref data[row];

        public bool IsAlive(int row) => row >= 0 && row < rowCount && alive[row];

        public float GetAge(int row) => age[row];

        /// <summary>Live projectiles of a team within a ground radius (shields that block enemy shots).</summary>
        public int QueryCircle(Team team, Vector3 center, float radius, Span<int> rows)
        {
            int written = 0;
            for (int row = 0; row < rowCount && written < rows.Length; row++)
            {
                if (!alive[row] || data[row].team != team)
                {
                    continue;
                }
                float dx = data[row].position.x - center.x;
                float dz = data[row].position.z - center.z;
                float reach = radius + data[row].radius * data[row].scale;
                if (dx * dx + dz * dz <= reach * reach)
                {
                    rows[written++] = row;
                }
            }
            return written;
        }

        /// <summary>Ends a projectile without its handler's OnExpire (blocked or absorbed).</summary>
        public void Remove(int row)
        {
            if (row < 0 || row >= rowCount || !alive[row])
            {
                return;
            }
            alive[row] = false;
            liveCount--;
            freeRows.Push(row);
        }

        /// <summary>Ends a projectile and calls its handler's OnExpire.</summary>
        public void Expire(int row)
        {
            if (!alive[row])
            {
                return;
            }
            alive[row] = false;
            liveCount--;
            freeRows.Push(row);
            data[row].handler?.OnExpire(this, row, data[row].position);
        }

        /// <summary>Ends every projectile of a team without calling handlers.</summary>
        public void Clear(Team team)
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (alive[row] && data[row].team == team)
                {
                    alive[row] = false;
                    liveCount--;
                    freeRows.Push(row);
                }
            }
        }

        /// <summary>Moves every projectile and resolves its hits.</summary>
        public void Step(float dt)
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row])
                {
                    continue;
                }
                age[row] += dt;
                previousPositions[row] = data[row].position;
                if (!Move(row, dt) || age[row] >= data[row].lifetime)
                {
                    Expire(row);
                    continue;
                }
                EmitTrail(row, dt);
                EmitHeads(row, dt);
                if (data[row].team == Team.Hero)
                {
                    HitEnemies(row);
                }
                else
                {
                    HitHero(row);
                }
            }
        }

        /// <summary>Draws every projectile; call once per frame after the step.</summary>
        public void Draw()
        {
            if (liveCount == 0)
            {
                return;
            }
            Array.Clear(visualCounts, 0, visualCounts.Length);
            for (int row = 0; row < rowCount; row++)
            {
                if (alive[row])
                {
                    visualCounts[data[row].visual]++;
                }
            }
            int start = 0;
            for (int i = 0; i < visualCounts.Length; i++)
            {
                visualStarts[i] = start;
                start += visualCounts[i];
                visualCounts[i] = 0;
            }
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row])
                {
                    continue;
                }
                int visual = data[row].visual;
                int slot = visualStarts[visual] + visualCounts[visual]++;
                drawTransforms[slot] = MakeTransform(row, data[row].position);
                drawPrevious[slot] = MakeTransform(row, previousPositions[row]);
            }
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visualCounts[i] == 0 || visuals[i].mesh == null || visuals[i].hideMesh)
                {
                    continue;
                }
                ProjectileVisual visual = visuals[i];
                Graphics.DrawMeshInstanced(visual.mesh,
                                           visual.material,
                                           drawTransforms.AsSpan(visualStarts[i], visualCounts[i]),
                                           drawPrevious.AsSpan(visualStarts[i], visualCounts[i]),
                                           visual.castShadows,
                                           materialInstance: visual.materialInstance);
            }
        }

        private bool Move(int row, float dt)
        {
            ref ProjectileSpawn p = ref data[row];
            switch (p.motion)
            {
                case ProjectileMotion.Homing:
                    Steer(row, dt);
                    p.position += p.velocity * dt;
                    return true;
                case ProjectileMotion.Boomerang:
                    return MoveBoomerang(row, dt);
                case ProjectileMotion.Orbit:
                    p.orbitAngle += p.orbitSpeed * dt;
                    var offset = new Vector3(MathF.Sin(p.orbitAngle), 0.0f, MathF.Cos(p.orbitAngle)) * p.orbitRadius;
                    Vector3 next = heroPosition + offset;
                    next.y = heroPosition.y + visuals[p.visual].height;
                    p.velocity = dt > 0.0f ? (next - p.position) / dt : Vector3.zero;
                    p.position = next;
                    return true;
                case ProjectileMotion.Arc:
                    p.velocity.y -= p.gravity * dt;
                    p.position += p.velocity * dt;
                    return p.position.y > 0.0f || p.velocity.y > 0.0f;
                case ProjectileMotion.Static:
                    return true;
                default:
                    p.position += p.velocity * dt;
                    return true;
            }
        }

        private void Steer(int row, float dt)
        {
            ref ProjectileSpawn p = ref data[row];
            Vector3 target;
            if (p.team == Team.Hero)
            {
                if (enemies == null || !enemies.QueryNearest(p.position, HOMING_RANGE, out int enemyRow))
                {
                    return;
                }
                target = enemies.GetPosition(enemyRow);
            }
            else
            {
                target = heroPosition;
            }
            float speed = p.velocity.magnitude;
            Vector3 desired = target - p.position;
            desired.y = 0.0f;
            if (desired.sqrMagnitude < 1e-6f || speed < 1e-4f)
            {
                return;
            }
            Vector3 current = new Vector3(p.velocity.x, 0.0f, p.velocity.z).normalized;
            Vector3 wanted = desired.normalized;
            float angle = MathF.Acos(Math.Clamp(Vector3.Dot(current, wanted), -1.0f, 1.0f));
            float step = MathF.Min(1.0f, p.homing * dt / MathF.Max(angle, 1e-4f));
            Vector3 direction = Vector3.Lerp(current, wanted, step).normalized;
            p.velocity = new Vector3(direction.x * speed, p.velocity.y, direction.z * speed);
        }

        private bool MoveBoomerang(int row, float dt)
        {
            ref ProjectileSpawn p = ref data[row];
            float outbound = p.lifetime * 0.5f;
            if (!returning[row])
            {
                // Slows to a stop at half its life, then flies back faster.
                float t = age[row] / MathF.Max(outbound, 1e-3f);
                p.position += p.velocity * (dt * MathF.Max(1.0f - t, 0.0f) * BOOMERANG_LAUNCH_BOOST);
                if (t >= 1.0f)
                {
                    returning[row] = true;
                    ClearHits(row);
                }
                return true;
            }
            Vector3 back = heroPosition - p.position;
            back.y = 0.0f;
            float distance = back.magnitude;
            if (distance < BOOMERANG_RETURN_DISTANCE)
            {
                return false;
            }
            float speed = p.velocity.magnitude * BOOMERANG_RETURN_BOOST;
            p.position += back / distance * MathF.Min(speed * dt, distance);
            p.lifetime = MathF.Max(p.lifetime, age[row] + BOOMERANG_RETURN_MARGIN);
            return true;
        }

        private void HitEnemies(int row)
        {
            if (enemies == null)
            {
                return;
            }
            ref ProjectileSpawn p = ref data[row];
            int found = enemies.QueryCapsule(previousPositions[row], p.position, p.radius * p.scale, queryRows);
            for (int i = 0; i < found && alive[row]; i++)
            {
                int enemyRow = queryRows[i];
                Enemy enemy = enemies.GetEnemy(enemyRow);
                if (WasHit(row, enemy))
                {
                    continue;
                }
                RememberHit(row, enemy);
                if (p.handler != null)
                {
                    p.handler.OnHitEnemy(this, row, enemyRow);
                }
                else
                {
                    Vector3 push = new Vector3(p.velocity.x, 0.0f, p.velocity.z).normalized * p.knockback;
                    Combat.DamageEnemy(enemyRow, p.damage, push, p.source);
                }
                if (p.pierce == 0)
                {
                    Expire(row);
                    return;
                }
                if (p.pierce > 0)
                {
                    p.pierce--;
                }
            }
        }

        private void HitHero(int row)
        {
            if (heroTarget == null || !heroTarget.isTargetable)
            {
                return;
            }
            ref ProjectileSpawn p = ref data[row];
            Vector3 hero = heroTarget.position;
            float reach = p.radius * p.scale + heroTarget.radius;
            float dx = hero.x - p.position.x;
            float dz = hero.z - p.position.z;
            if (dx * dx + dz * dz > reach * reach)
            {
                return;
            }
            if (heroTarget.TakeDamage(p.damage, p.position) && p.sourceName != null)
            {
                heroDamagedBy?.Invoke(p.sourceName);
            }
            if (p.pierce <= 0)
            {
                Expire(row);
            }
        }

        private bool WasHit(int row, Enemy enemy)
        {
            int start = row * MAX_REMEMBERED_HITS;
            float interval = data[row].rehitInterval;
            for (int i = 0; i < MAX_REMEMBERED_HITS; i++)
            {
                if (!hits[start + i].Equals(enemy))
                {
                    continue;
                }
                return interval <= 0.0f || age[row] - hitTimes[start + i] < interval;
            }
            return false;
        }

        private void RememberHit(int row, Enemy enemy)
        {
            int slot = row * MAX_REMEMBERED_HITS + hitCursor[row];
            hits[slot] = enemy;
            hitTimes[slot] = age[row];
            hitCursor[row] = (hitCursor[row] + 1) % MAX_REMEMBERED_HITS;
        }

        private void ClearHits(int row)
        {
            for (int i = 0; i < MAX_REMEMBERED_HITS; i++)
            {
                hits[row * MAX_REMEMBERED_HITS + i] = Enemy.None;
            }
        }

        private void EmitTrail(int row, float dt)
        {
            ProjectileVisual visual = visuals[data[row].visual];
            if (visual.trail == null || effects == null)
            {
                return;
            }
            trailTimer[row] -= dt;
            if (trailTimer[row] > 0.0f)
            {
                return;
            }
            trailTimer[row] = visual.trailInterval;
            if (visual.trailId == Effects.UNRESOLVED)
            {
                visual.trailId = effects.Find(visual.trail);
            }
            effects.Emit(visual.trailId, data[row].position, 1);
        }

        private void EmitHeads(int row, float dt)
        {
            ProjectileVisual visual = visuals[data[row].visual];
            if (visual.heads.Length == 0 || effects == null || dt <= 0.0f)
            {
                return;
            }
            ref ProjectileSpawn p = ref data[row];
            foreach (ProjectileHead head in visual.heads)
            {
                if (head.id == Effects.UNRESOLVED)
                {
                    head.id = effects.Find(head.effect);
                }
                float headScale = p.headScale > 0.0f ? p.headScale : 1.0f;
                Color tint = head.takesSpawnTint && p.headTint.a > 0.0f ? p.headTint : head.tint;
                Vector3 at = head.onGround ? new Vector3(p.position.x, Tuning.TELEGRAPH_HEIGHT, p.position.z) : p.position;
                effects.EmitSized(head.id, at, 1, tint, head.size * p.scale * headScale);
            }
        }

        private Matrix4x4 MakeTransform(int row, Vector3 position)
        {
            ref ProjectileSpawn p = ref data[row];
            ProjectileVisual visual = visuals[p.visual];
            Quaternion rotation;
            if (visual.spin != 0.0f)
            {
                rotation = Quaternion.Euler(0.0f, age[row] * visual.spin, 0.0f);
            }
            else
            {
                Vector3 direction = p.velocity.sqrMagnitude > 1e-6f ? p.velocity.normalized : Vector3.forward;
                rotation = Quaternion.LookRotation(direction, MathF.Abs(direction.y) > 0.99f ? Vector3.right : Vector3.up);
            }
            rotation = rotation * Quaternion.Euler(visual.meshRotation);
            float scale = visual.scale * p.scale;
            return Matrix4x4.TRS(position, rotation, new Vector3(scale, scale, scale));
        }
    }

    /// <summary>The hero as something enemy attacks can hit.</summary>
    public interface IHeroTarget
    {
        Vector3 position { get; }
        float radius { get; }
        bool isTargetable { get; }
        /// <summary>Applies armor, dodge and invulnerability; returns whether it hurt.</summary>
        bool TakeDamage(float amount, Vector3 from);
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>What an enemy is doing; the world steps each state differently.</summary>
    public enum EnemyState : byte
    {
        /// <summary>Climbing out of the ground: no movement, not targetable.</summary>
        Rising,
        /// <summary>Moving by its type's movement rule and choosing abilities.</summary>
        Chase,
        /// <summary>Playing an ability: wind-up, the hit moment, then recovery.</summary>
        Casting,
        /// <summary>Knocked off balance by a heavy hit.</summary>
        Stagger,
        /// <summary>Playing its death; no collision, removed when the clip ends.</summary>
        Dying,
    }

    /// <summary>How an enemy moves while it is not casting.</summary>
    public enum EnemyMovement : byte
    {
        /// <summary>Straight at the hero.</summary>
        Chase,
        /// <summary>Holds a distance band around the hero and circles it.</summary>
        KeepRange,
    }

    /// <summary>What an ability does at its hit moment.</summary>
    public enum EnemyEffect : byte
    {
        /// <summary>Damages the hero when it is within reach in front of the caster.</summary>
        Melee,
        /// <summary>Fires projectiles at the hero (count spread over the spread angle).</summary>
        Projectile,
        /// <summary>Marks a circle at the hero's position at wind-up start; damages inside it at the hit moment.</summary>
        GroundBurst,
        /// <summary>Spawns enemies of another type around the caster.</summary>
        Summon,
        /// <summary>Dashes in a straight line toward where the hero was; hits around the caster when the dash ends.</summary>
        Charge,
        /// <summary>Explodes, damaging around itself, and dies.</summary>
        Explode,
        /// <summary>Wards allies within the ability radius: faster and tougher for <see cref="EnemyAbility.chargeTime"/> seconds.</summary>
        Ward,
    }

    /// <summary>Clip roles a type can bake; abilities and states name clips by role.</summary>
    public enum EnemyClip
    {
        Walk,
        Run,
        Idle,
        Attack,
        AttackHeavy,
        Cast,
        Shoot,
        Hit,
        Death,
        DeathAlt,
        Rise,
        Count,
    }

    /// <summary>Statuses an enemy type shrugs off; the world's apply methods honour them.</summary>
    [Flags]
    public enum StatusImmunity : byte
    {
        None = 0,
        /// <summary>A freeze slows it instead: it has no frozen pose and never stops dead.</summary>
        Freeze = 1 << 0,
        /// <summary>Wards (Hex Casters, pyres) do not reach it.</summary>
        Ward = 1 << 1,
        /// <summary>It never wanders off confused.</summary>
        Confuse = 1 << 2,
        /// <summary>It is never popped off the ground.</summary>
        Launch = 1 << 3,
    }

    /// <summary>
    /// One thing an enemy type can do. Times are seconds from the start of the cast; the cast clip plays from 0 and the
    /// effect lands at <see cref="hitTime"/>.
    /// </summary>
    public sealed class EnemyAbility
    {
        /// <summary>The effect at the hit moment.</summary>
        public EnemyEffect effect = EnemyEffect.Melee;
        /// <summary>The clip the cast plays.</summary>
        public EnemyClip clip = EnemyClip.Attack;
        /// <summary>Clip playback rate during the cast.</summary>
        public float clipSpeed = 1.0f;
        /// <summary>
        /// Seconds of chasing after a cast before the ability is ready again. It runs only while the enemy chases (not
        /// while it casts, is held or confused), so casts come at most every <see cref="duration"/> + cooldown seconds.
        /// </summary>
        public float cooldown = 2.0f;
        /// <summary>Random share added to the first cooldown so a crowd does not cast in step.</summary>
        public float cooldownJitter = 0.5f;
        /// <summary>The cast starts when the hero is between these distances (edge to edge).</summary>
        public float minRange = 0.0f;
        public float range = 1.2f;
        /// <summary>Seconds from cast start to the hit moment (the wind-up).</summary>
        public float hitTime = 0.45f;
        /// <summary>Seconds from cast start until the enemy moves again.</summary>
        public float duration = 1.0f;
        /// <summary>Damage of the hit (scaled by the enemy's damage scale).</summary>
        public float damage = 8.0f;
        /// <summary>Melee reach, burst and explosion radius.</summary>
        public float radius = 1.2f;
        /// <summary>Projectile speed, or charge speed.</summary>
        public float speed = 9.0f;
        /// <summary>Projectiles per cast, or enemies per summon.</summary>
        public int count = 1;
        /// <summary>Degrees the projectiles spread over.</summary>
        public float spread = 0.0f;
        /// <summary>Seconds a charge lasts, or seconds a ward holds.</summary>
        public float chargeTime = 0.6f;
        /// <summary>Type index a summon spawns.</summary>
        public int summonType = -1;
        /// <summary>Most enemies alive the world allows before a summon is skipped.</summary>
        public int summonLimit = 2500;
        /// <summary>The projectile look (a projectile catalog name) of a Projectile ability.</summary>
        public string visual = "";
        /// <summary>Whether the cast turns to face the hero at its start.</summary>
        public bool facesTarget = true;
        /// <summary>The cast starts only with this many other enemies within <see cref="allyRadius"/> (Ward wants a crowd).</summary>
        public int minAllies = 0;
        /// <summary>The cast starts only with at most this many other enemies near (Raise wants few).</summary>
        public int maxAllies = int.MaxValue;
        public float allyRadius = 6.0f;
        /// <summary>Shows a danger cone over the wind-up (heavy melee).</summary>
        public bool telegraph;
        /// <summary>Sound at cast start, or empty.</summary>
        public string castSound = "";
    }

    /// <summary>
    /// An enemy kind: its look, its numbers and its abilities. The world bakes <see cref="clips"/> once and draws
    /// every enemy of the type with one call per look.
    /// </summary>
    public sealed class EnemyType
    {
        public string name = "Enemy";
        /// <summary>The name the death screen shows ("Bone Archer").</summary>
        public string displayName = "Enemy";
        /// <summary>Bursts a moment after any death but its own fuse, hurting other enemies (Cinder Skull).</summary>
        public bool detonatesOnDeath;
        /// <summary>A row with no body of its own: its owner (a boss, a lantern, a pyre) drives its position and reads its health.</summary>
        public bool isProxy;
        /// <summary>A boss's row: weapons that aim at the nearest enemy or the densest crowd favour it.</summary>
        public bool isBoss;
        /// <summary>Statuses the type shrugs off.</summary>
        public StatusImmunity immunities = StatusImmunity.None;
        public Mesh mesh;
        public Material material;
        /// <summary>Clips by <see cref="EnemyClip"/> role; null roles fall back to Walk.</summary>
        public AnimationClip[] clips = new AnimationClip[(int)EnemyClip.Count];
        public EnemyMovement movement = EnemyMovement.Chase;
        public float scale = 1.0f;
        /// <summary>Ground radius (m) at scale 1: separation and hits.</summary>
        public float radius = 0.45f;
        public float speed = 2.2f;
        public float acceleration = 14.0f;
        public float turnRate = 600.0f;
        public float health = 10.0f;
        /// <summary>Knockback taken is divided by this.</summary>
        public float weight = 1.0f;
        /// <summary>Experience the gem it drops is worth.</summary>
        public float experience = 1.0f;
        /// <summary>For KeepRange: the distance band it holds.</summary>
        public float holdMin = 6.0f;
        public float holdMax = 9.0f;
        /// <summary>Walk clip playback per m/s of speed; 0 plays it at its own rate.</summary>
        public float walkClipPerSpeed = 0.0f;
        /// <summary>Turn (degrees) added to the heading for meshes that do not face +Z.</summary>
        public float yawOffset = 0.0f;
        /// <summary>Emissive the elite variant glows with.</summary>
        public Color eliteGlow = new Color(0.8f, 0.25f, 1.0f, 1.0f);
        /// <summary>
        /// Emissive map the glowing looks (hit flash, elite, burning, frozen) use so the whole body glows; null keeps
        /// the material's own map (which may light the eyes only).
        /// </summary>
        public Texture bodyGlowMap;
        public float eliteScale = 1.35f;
        public float eliteHealth = 8.0f;
        public EnemyAbility[] abilities = Array.Empty<EnemyAbility>();

        internal BakedAnimation baked;
        internal readonly int[] clipIndex = new int[(int)EnemyClip.Count];
        internal readonly float[] clipLength = new float[(int)EnemyClip.Count];
        internal MaterialInstance[] looks;
        internal int index;

        /// <summary>The baked clip index of a role, falling back to Walk.</summary>
        internal int GetClip(EnemyClip role) => clipIndex[(int)role];

        /// <summary>Seconds the clip of a role lasts.</summary>
        internal float GetClipLength(EnemyClip role) => clipLength[(int)role];

        /// <summary>Whether the role has a clip of its own (not the Walk fallback).</summary>
        internal bool HasClip(EnemyClip role) => clips[(int)role] != null;

        /// <summary>Whether the type shrugs off a status.</summary>
        internal bool IsImmune(StatusImmunity status) => (immunities & status) != 0;
    }

    /// <summary>The draw buckets of a type; a flashing enemy draws flashing whatever else it is.</summary>
    internal enum EnemyLook
    {
        Normal,
        Elite,
        Burning,
        Frozen,
        Flash,
        Count,
    }

    /// <summary>How an enemy enters the world (<see cref="EnemyWorld.Spawn(int, Vector3, in SpawnOptions)"/>).</summary>
    public struct SpawnOptions
    {
        /// <summary>Bigger, tougher, glowing; drops a chest.</summary>
        public bool isElite;
        /// <summary>Multiplies the type's health (time and Dread scaling); used when <see cref="health"/> is 0.</summary>
        public float healthScale;
        /// <summary>Health the owner decides (bosses, pyres, the robber): replaces the type's health and every scale.</summary>
        public float health;
        /// <summary>Multiplies ability damage.</summary>
        public float damageScale;
        /// <summary>Climbs out of the ground first (the rise clip, or a climb without one).</summary>
        public bool rises;
        /// <summary>Killing it gives experience and drops (summoned adds do not).</summary>
        public bool isRewarded;
    }

    /// <summary>An enemy: its row and the generation the row had when it spawned.</summary>
    public struct Enemy : IEquatable<Enemy>
    {
        public int row;
        public uint generation;

        public static Enemy None => new Enemy { row = -1 };

        public bool isValid => row >= 0;

        public bool Equals(Enemy other) => row == other.row && generation == other.generation;

        public override bool Equals(object obj) => obj is Enemy other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(row, generation);
    }

    /// <summary>A death the world reports once per enemy.</summary>
    public struct EnemyDeath
    {
        public Enemy enemy;
        public int type;
        public Vector3 position;
        public bool isElite;
        public float experience;
        /// <summary>The weapon slot that dealt the killing blow, -1 for none.</summary>
        public int source;
        /// <summary>False for summoned enemies: no experience, no drops.</summary>
        public bool isRewarded;
        /// <summary>The enemy was frozen when it died (shatter effects).</summary>
        public bool wasFrozen;
        /// <summary>The enemy was marked when it died.</summary>
        public bool wasMarked;
        /// <summary>The enemy was bloodied when it died (Butcher's Chains).</summary>
        public bool wasBloodied;
    }

    /// <summary>
    /// Work an ability hands to the main thread. Begin marks a cast start (telegraphs), Hit its effect.
    /// </summary>
    public struct EnemyCommand
    {
        public enum Phase : byte
        {
            Begin,
            Hit,
        }

        public Phase phase;
        public Enemy enemy;
        public int type;
        public int ability;
        public Vector3 position;
        public Vector3 target;
        public Vector3 direction;
        public float damageScale;
        public bool isElite;
        /// <summary>The caster's ground radius (m), elite size included.</summary>
        public float radius;
        /// <summary>The caster's model scale, elite size included (effect heights).</summary>
        public float scale;
        /// <summary>Seconds from cast start to the hit moment for this caster (hasted elites wind up faster).</summary>
        public float windup;
    }

    /// <summary>Runs ability commands on the main thread: projectiles, telegraphs, hits on the hero.</summary>
    public interface IEnemyCommandSink
    {
        void Execute(EnemyWorld world, in EnemyCommand command);
    }
}

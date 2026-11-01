using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>One firing of a weapon: full strength, or an Arcane Echo at reduced damage with a violet tint.</summary>
    public readonly struct CastInfo
    {
        public readonly float damageScale;
        public readonly bool isEcho;

        public CastInfo(float damageScale, bool isEcho)
        {
            this.damageScale = damageScale;
            this.isEcho = isEcho;
        }

        public static CastInfo normal => new CastInfo(1.0f, false);

        /// <summary>An Arcane Echo: <see cref="Weapon.ECHO_DAMAGE"/> of the damage, in violet.</summary>
        public static CastInfo echo => new CastInfo(Weapon.ECHO_DAMAGE, true);
    }

    /// <summary>
    /// A delayed step of a weapon (the 2nd swing, a sword's slam, a meteor's impact). <see cref="kind"/> tells which
    /// step it is; a weapon fills only the fields its step uses, best through a factory method of its own.
    /// </summary>
    public struct WeaponEvent
    {
        public int kind;
        /// <summary>Game time (s) the step is due; set by <see cref="Weapon.Schedule"/>.</summary>
        public float time;
        /// <summary>Where the step happens.</summary>
        public Vector3 position;
        /// <summary>Where the step comes from (the strike a bolt leaps from).</summary>
        public Vector3 origin;
        public Vector3 direction;
        /// <summary>A number the step carries (a damage, a burn, a spread angle).</summary>
        public float value;
        public float radius;
        /// <summary>How many more steps follow from this one (jumps or chain links left).</summary>
        public int count;
        /// <summary>Which of the weapon's own slots the step belongs to (a swing's order, a hit memory, a bolt).</summary>
        public int index;
        public Enemy target;
        public CastInfo cast;
    }

    /// <summary>
    /// A weapon in a loadout slot. The base runs the cooldown (scaled by the Cooldown stat), retries shortly when a cast
    /// finds nothing to aim at, runs the weapon's delayed events, lets the class kit change its own class weapons
    /// (echo casts, forced critical hits), and offers stat-scaled numbers (Might, Area, Amount, Projectile Speed,
    /// Duration, Knockback) and targeting helpers (nearest, facing, cluster, random in view). Subclasses implement
    /// <see cref="Cast"/> and, for lingering behaviour, <see cref="Update"/>. A weapon is the handler of the
    /// projectiles it spawns with handler = this (override <see cref="OnHitEnemy"/> and <see cref="OnExpire"/>); the
    /// ones it spawns with a <see cref="PERMANENT"/> lifetime leave with it in <see cref="Dispose"/>. Kills and hits of
    /// any source arrive through <see cref="OnEnemyKilled"/> and <see cref="OnEnemyDamaged"/>. Main thread; no
    /// allocations per frame.
    /// </summary>
    public abstract class Weapon : IProjectileHandler
    {
        /// <summary>Lifetime of projectiles that stay until the weapon removes them.</summary>
        protected const float PERMANENT = 1.0e6f;
        private const float RETRY_SECONDS = 0.1f;
        private const int EVENT_CAPACITY = 160;
        private const int PERMANENT_ROWS_START = 16;
        /// <summary>Seconds after its cast an echo fires.</summary>
        private const float ECHO_DELAY = 0.25f;
        private const float NO_ECHO = -1.0f;
        /// <summary>Burns never tick faster than this, whatever a weapon's BurnTick says.</summary>
        private const float MIN_BURN_TICK = 0.05f;
        /// <summary>Damage share of an echo cast.</summary>
        public const float ECHO_DAMAGE = 0.7f;
        /// <summary>Effects grow with rank (x1.35 at rank 8) and again when evolved, so upgrades read grander.</summary>
        private const float FX_SCALE_PER_RANK = 0.05f;
        private const float FX_EVOLVED_SCALE = 1.3f;

        protected static readonly Color WHITE = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        /// <summary>The violet of Arcane Echo casts.</summary>
        protected static readonly Color ECHO_TINT = new Color(0.72f, 0.45f, 1.0f, 1.0f);

        public readonly WeaponDef def;
        /// <summary>The loadout slot (0..5), also the source of the run record's damage table.</summary>
        public readonly int slot;
        protected readonly WeaponContext ctx;
        protected readonly PlayerStats stats;
        protected readonly EnemyWorld enemies;
        protected readonly ProjectileWorld projectiles;
        protected readonly Effects effects;
        protected readonly Sounds sounds;
        protected readonly HeroController hero;
        protected readonly Strikes strikes;
        /// <summary>A class weapon of the hero's class: the class kit's hooks apply to it.</summary>
        private readonly bool isKitWeapon;
        private readonly WeaponEvent[] events = new WeaponEvent[EVENT_CAPACITY];
        /// <summary>Rows this weapon spawned with a <see cref="PERMANENT"/> lifetime (some may have ended since).</summary>
        private int[] permanentRows = new int[PERMANENT_ROWS_START];
        private int permanentCount;
        private int eventCount;
        private float cooldownLeft;
        private float echoAt = NO_ECHO;
        /// <summary>Game time (s) of this weapon.</summary>
        protected float clock;

        protected Weapon(WeaponContext context, WeaponDef definition, int slotIndex)
        {
            ctx = context;
            def = definition;
            slot = slotIndex;
            stats = context.stats;
            enemies = context.enemies;
            projectiles = context.projectiles;
            effects = context.effects;
            sounds = context.sounds;
            hero = context.hero;
            strikes = context.strikes;
            isKitWeapon = context.kit != null && definition.IsClassWeaponOf(context.kit.heroClass);
            level = definition.GetLevel(1);
        }

        public int rank { get; private set; } = 1;
        public bool isEvolved { get; private set; }
        /// <summary>The numbers in use: the rank's, or the evolution's.</summary>
        public WeaponLevel level { get; private set; }
        public string displayName => isEvolved ? def.evolution.name : def.name;
        /// <summary>Item id shown in the HUD: the evolution's id once evolved.</summary>
        public string displayId => isEvolved ? def.evolution.id : def.id;
        public bool isMaxRank => rank >= WeaponDef.MAX_RANK;
        /// <summary>Seconds between casts after the Cooldown stat.</summary>
        public float cooldown => baseCooldown * stats[StatId.Cooldown];

        public void SetRank(int value)
        {
            rank = Math.Clamp(value, 1, WeaponDef.MAX_RANK);
            if (!isEvolved)
            {
                level = def.GetLevel(rank);
            }
            OnLevelChanged();
        }

        /// <summary>Switches to the evolved numbers and behaviour.</summary>
        public void Evolve()
        {
            if (def.evolution == null || isEvolved)
            {
                return;
            }
            isEvolved = true;
            rank = WeaponDef.MAX_RANK;
            level = def.evolution.level;
            OnLevelChanged();
        }

        /// <summary>
        /// The weapon leaves the loadout or the run ends: <see cref="OnDispose"/> lets go of what the weapon holds, then
        /// the projectiles it spawned with a <see cref="PERMANENT"/> lifetime are removed (no OnExpire).
        /// </summary>
        public void Dispose()
        {
            OnDispose();
            RemovePermanentRows();
        }

        /// <summary>Steps events, lingering behaviour and the cooldown; once per frame while playing.</summary>
        public void Tick(float dt)
        {
            clock += dt;
            RunEvents();
            RunEcho();
            Update(dt);
            if (!usesCooldown || !hero.isAlive)
            {
                return;
            }
            cooldownLeft -= dt;
            if (cooldownLeft > 0.0f)
            {
                return;
            }
            if (Cast(CastInfo.normal))
            {
                cooldownLeft = cooldown;
                RollEcho();
            }
            else
            {
                cooldownLeft = RETRY_SECONDS;
            }
        }

        /// <summary>Kill hook, for kills of any source (runs inside the damage call that killed: only schedule work here, never damage).</summary>
        public virtual void OnEnemyKilled(in EnemyDeath death)
        {
        }

        /// <summary>
        /// Hit hook, for every hero hit that damaged an enemy, of any source: the slot that dealt it, the damage after
        /// crits and whether it was critical. Runs inside the damage call: only change state or queue work here, never
        /// damage.
        /// </summary>
        public virtual void OnEnemyDamaged(int row, int source, float damage, bool isCritical)
        {
        }

        /// <summary>One of this weapon's projectiles (spawned with handler = this) touched an enemy it has not hit yet.</summary>
        public virtual void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
        }

        /// <summary>One of this weapon's projectiles (spawned with handler = this) ended: out of time or pierce, landed, or back.</summary>
        public virtual void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
        }

        // -------------------------------------------------------------------------------------------------------
        // What subclasses implement
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Fires once; returns false when there was nothing to aim at (the base retries in 0.1 s).</summary>
        protected abstract bool Cast(in CastInfo cast);

        /// <summary>Continuous behaviour (auras, orbits, summons); every frame.</summary>
        protected virtual void Update(float dt)
        {
        }

        /// <summary>The rank or evolution changed.</summary>
        protected virtual void OnLevelChanged()
        {
        }

        /// <summary>A delayed step scheduled with <see cref="Schedule"/> is due.</summary>
        protected virtual void OnEvent(in WeaponEvent e)
        {
        }

        /// <summary>The weapon leaves: let go of run events and undo what it added to the run (stat bonuses, sounds).</summary>
        protected virtual void OnDispose()
        {
        }

        /// <summary>False for weapons that act every frame instead of on a cooldown.</summary>
        protected virtual bool usesCooldown => true;

        protected virtual float baseCooldown => level[WeaponField.Cooldown];

        // -------------------------------------------------------------------------------------------------------
        // Stat-scaled numbers
        // -------------------------------------------------------------------------------------------------------

        /// <summary>A base damage after Might, the cast's scale (echo) and the bond.</summary>
        protected float MightOf(float baseDamage, in CastInfo cast) => ctx.MightOf(baseDamage) * cast.damageScale * bondFactor;

        /// <summary>A base damage after Might and the bond.</summary>
        protected float MightOf(float baseDamage) => ctx.MightOf(baseDamage) * bondFactor;

        /// <summary>
        /// The bond: an evolution deals more per rank of the passive that evolved it, so ranking the passive stays worth
        /// it; 1 before the evolution. The loadout sets it whenever its passives or this weapon change.
        /// </summary>
        protected float bondFactor { get; private set; } = 1.0f;

        /// <summary>Sets the bond from the rank of the passive this weapon's evolution pairs with.</summary>
        internal void SetBondRank(int passiveRank)
        {
            bondFactor = isEvolved ? 1.0f + PassiveTraits.BOND_PER_RANK * passiveRank : 1.0f;
        }

        /// <summary>A radius or length after Area.</summary>
        protected float AreaOf(float radius) => radius * stats[StatId.Area];

        /// <summary>How grand this weapon's effects read: 1 at rank 1, 1.35 at rank 8, x1.3 more when evolved.</summary>
        protected float fxScale => (1.0f + (rank - 1) * FX_SCALE_PER_RANK) * (isEvolved ? FX_EVOLVED_SCALE : 1.0f);

        /// <summary>A layered effect over this weapon's emitters (see <see cref="FxComposite"/>).</summary>
        protected FxComposite Composite(params FxLayer[] layers) => new FxComposite(effects, layers);

        /// <summary>Extra projectiles, swings or summons from the Amount stat.</summary>
        protected int amount => stats.GetCount(StatId.Amount);

        protected float SpeedOf(float speed) => speed * stats[StatId.ProjectileSpeed];

        /// <summary>A lingering time after Duration.</summary>
        protected float DurationOf(float seconds) => seconds * stats[StatId.Duration];

        /// <summary>A knockback distance (m) as push speed; Combat applies the Knockback stat on top.</summary>
        protected static float KnockbackOf(float metres) => Strikes.KnockbackOf(metres);

        /// <summary>Damage of this level's field after Might.</summary>
        protected float DamageOf(WeaponField field, in CastInfo cast) => MightOf(level[field], cast);

        /// <summary>The burn this level sets, per second: Burn (after Might and the cast) per BurnTick.</summary>
        protected float BurnPerSecondOf(in CastInfo cast) => MightOf(level[WeaponField.Burn], cast) / MathF.Max(level[WeaponField.BurnTick], MIN_BURN_TICK);

        // -------------------------------------------------------------------------------------------------------
        // Targeting
        // -------------------------------------------------------------------------------------------------------

        protected Vector3 heroPosition => hero.position;

        /// <summary>The last movement direction.</summary>
        protected Vector3 facing => hero.lastMoveDirection;

        /// <summary>The nearest targetable enemy within range.</summary>
        protected bool TryNearest(float range, out int row) => enemies.QueryNearest(hero.position, range, out row);

        /// <summary>Toward the nearest enemy within range, else the movement direction.</summary>
        protected Vector3 AimFacingNearest(float range)
        {
            return TryNearest(range, out int row) ? Ground.Direction(hero.position, enemies.GetPosition(row), facing) : facing;
        }

        /// <summary>The <paramref name="count"/> nearest enemies, nearest first.</summary>
        protected int FindNearest(float range, Span<int> rows, int count) => strikes.FindNearest(hero.position, range, rows, count);

        /// <summary>A random enemy within range of the hero.</summary>
        protected bool TryRandomInView(float range, out int row) => enemies.QueryRandom(hero.position, range, out row);

        /// <summary>The densest cells within range (read them from ctx.clusters).</summary>
        protected int FindClusters(float range) => ctx.clusters.Find(hero.position, range);

        // -------------------------------------------------------------------------------------------------------
        // Feel helpers
        // -------------------------------------------------------------------------------------------------------

        /// <summary>The attack clip priority of this weapon.</summary>
        protected int attackPriority => isEvolved ? WeaponFeel.PRIORITY_EVOLVED : def.isClassWeapon ? WeaponFeel.PRIORITY_CLASS : WeaponFeel.PRIORITY_SHARED;

        /// <summary>Asks for an upper-body attack clip (one of the controller's Upper states).</summary>
        protected void RequestAttack(string state) => ctx.feel.RequestAttack(state, attackPriority);

        /// <summary>A hit of this weapon after the class kit's say (a Rogue weapon spends Smoke Roll's guaranteed critical hits).</summary>
        protected HitEffect WithAutoCrit(HitEffect effect)
        {
            if (isKitWeapon)
            {
                ctx.kit.PrepareHit(ref effect);
            }
            return effect;
        }

        /// <summary>The colour of a cast: the echo violet for an echo, else <paramref name="color"/>.</summary>
        protected static Color Tint(in CastInfo cast, Color color) => cast.isEcho ? ECHO_TINT : color;

        /// <summary>A colour with its alpha scaled (overlapping layers that must not stack into white).</summary>
        protected static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);

        /// <summary>The damage number colour of a cast: violet for an echo, else <paramref name="kind"/>.</summary>
        protected static DamageKind NumberKind(in CastInfo cast, DamageKind kind = DamageKind.Normal) => cast.isEcho ? DamageKind.Echo : kind;

        protected void Emit(int effect, Vector3 position, int count, Color color) => effects.Emit(effect, position, count, color);

        /// <summary>One particle with a size of its own and the emitter's lifetime (muzzle flashes, sized layers).</summary>
        protected void EmitSized(int effect, Vector3 position, Color color, float size) => effects.EmitSized(effect, position, 1, color, size);

        /// <summary>One particle with a size and lifetime of its own (rings, decals, telegraphs).</summary>
        protected void EmitShape(int effect, Vector3 position, Color color, float size, float lifetime)
        {
            effects.Emit(effect, position, 1, color, size, lifetime);
        }

        protected void PlayAt(string sound, Vector3 position, float volume = 1.0f, float pitch = 1.0f)
        {
            sounds.PlayAt(sound, position, volume, pitch);
        }

        // -------------------------------------------------------------------------------------------------------
        // Projectiles
        // -------------------------------------------------------------------------------------------------------

        /// <summary>A hero projectile of this weapon's slot that pierces everything until the caller says otherwise.</summary>
        protected ProjectileSpawn NewProjectile(int visual, ProjectileMotion motion, Vector3 position, Vector3 velocity, float lifetime)
        {
            return new ProjectileSpawn
            {
                team = Team.Hero,
                visual = visual,
                motion = motion,
                position = position,
                velocity = velocity,
                lifetime = lifetime,
                pierce = -1,
                scale = 1.0f,
                headScale = fxScale,
                source = slot,
            };
        }

        /// <summary>
        /// Spawns a projectile of this weapon; returns its row, or -1 when the world is full. One with a
        /// <see cref="PERMANENT"/> lifetime is remembered, so it leaves with the weapon.
        /// </summary>
        protected int Spawn(in ProjectileSpawn spawn)
        {
            int row = projectiles.Spawn(spawn);
            if (row >= 0 && spawn.lifetime >= PERMANENT)
            {
                RememberPermanent(row);
            }
            return row;
        }

        /// <summary>A projectile that only shows something (swords, glyphs, rings, stuck knives).</summary>
        protected int SpawnVisual(int visual, ProjectileMotion motion, Vector3 position, Vector3 velocity, float lifetime, float scale = 1.0f)
        {
            ProjectileSpawn spawn = NewProjectile(visual, motion, position, velocity, lifetime);
            spawn.handler = ctx.visualOnly;
            spawn.scale = scale;
            return Spawn(spawn);
        }

        /// <summary>Where a projectile flies on the ground (the hero's movement direction while it stands still).</summary>
        protected Vector3 FlightOf(in ProjectileSpawn projectile) => Ground.Normalize(projectile.velocity, facing);

        /// <summary>Hits an enemy with a projectile's damage, pushed along its flight by its knockback.</summary>
        protected DamageResult HitAlongFlight(int enemyRow, in ProjectileSpawn projectile, in HitEffect effect = default)
        {
            return strikes.Hit(enemyRow, projectile.damage, FlightOf(projectile) * projectile.knockback, slot, effect);
        }

        /// <summary>Whether a row still holds the projectile this weapon spawned with <paramref name="handler"/> and look.</summary>
        protected bool IsOwned(int row, IProjectileHandler handler, int visual)
        {
            if (row < 0 || !projectiles.IsAlive(row))
            {
                return false;
            }
            ref ProjectileSpawn p = ref projectiles.Get(row);
            return ReferenceEquals(p.handler, handler) && p.visual == visual && p.source == slot;
        }

        /// <summary>Ends a projectile this weapon owns (its handler's OnExpire runs).</summary>
        protected void ExpireOwned(ref int row, IProjectileHandler handler, int visual)
        {
            if (IsOwned(row, handler, visual))
            {
                projectiles.Expire(row);
            }
            row = -1;
        }

        /// <summary>Whether a row still holds a projectile this weapon spawned with a <see cref="PERMANENT"/> lifetime.</summary>
        private bool IsPermanentOwned(int row)
        {
            if (!projectiles.IsAlive(row))
            {
                return false;
            }
            ref ProjectileSpawn p = ref projectiles.Get(row);
            return p.source == slot && p.lifetime >= PERMANENT;
        }

        /// <summary>Notes a new permanent row; entries whose projectile ended (or whose row is this one again) are dropped first.</summary>
        private void RememberPermanent(int row)
        {
            int i = 0;
            while (i < permanentCount)
            {
                if (permanentRows[i] == row || !IsPermanentOwned(permanentRows[i]))
                {
                    permanentRows[i] = permanentRows[--permanentCount];
                    continue;
                }
                i++;
            }
            if (permanentCount == permanentRows.Length)
            {
                Array.Resize(ref permanentRows, permanentCount * 2);
            }
            permanentRows[permanentCount++] = row;
        }

        private void RemovePermanentRows()
        {
            for (int i = 0; i < permanentCount; i++)
            {
                if (IsPermanentOwned(permanentRows[i]))
                {
                    projectiles.Remove(permanentRows[i]);
                }
            }
            permanentCount = 0;
        }

        // -------------------------------------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Runs <paramref name="e"/> through <see cref="OnEvent"/> after a delay; false when the queue is full.</summary>
        protected bool Schedule(float delay, WeaponEvent e)
        {
            if (eventCount >= EVENT_CAPACITY)
            {
                return false;
            }
            e.time = clock + MathF.Max(delay, 0.0f);
            events[eventCount++] = e;
            return true;
        }

        private void RunEvents()
        {
            int i = 0;
            while (i < eventCount)
            {
                if (events[i].time > clock)
                {
                    i++;
                    continue;
                }
                WeaponEvent due = events[i];
                events[i] = events[--eventCount];
                OnEvent(due);
            }
        }

        /// <summary>After a full cast, the class kit may ask for an echo of it (Arcane Echo for Mage weapons).</summary>
        private void RollEcho()
        {
            if (isKitWeapon && ctx.kit.RollEcho())
            {
                echoAt = clock + ECHO_DELAY;
            }
        }

        /// <summary>Fires a pending echo once it is due; a hero who died meanwhile casts nothing.</summary>
        private void RunEcho()
        {
            if (echoAt < 0.0f || clock < echoAt)
            {
                return;
            }
            echoAt = NO_ECHO;
            if (!hero.isAlive)
            {
                return;
            }
            ctx.kit.OnEcho();
            Cast(CastInfo.echo);
        }
    }
}

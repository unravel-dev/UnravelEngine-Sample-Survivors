using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Ember Orb (Mage, Common): fireballs (Count + Amount, Stagger apart) home on the nearest enemies within Range (one
    /// per enemy, then doubling up) at Speed (Projectile Speed), turning Turn degrees per second; each explodes on the
    /// first enemy it touches for Damage in Radius (Area), pushing it back (Knockback), and leaves a burn of Burn per
    /// BurnTick for SubDuration (Duration stat) that refreshes rather than stacks.
    /// Evolution Sunfire Orb (+ Quickening Hourglass): bigger, hotter blasts that throw SubCount embers SubRange outward
    /// (each landing for SubDamage in SubRadius), and a burn that would finish its target consumes it at once (bosses
    /// excepted).
    /// Look: a white-hot core in orange flame inside a halo (MageLooks), shedding embers, launched with a flare at the
    /// hand; its blast flashes, throws fireballs, a ground shock, sparks, embers and smoke and leaves a scorch, and an
    /// orb that hits nothing fizzles out in sparks. Sunfire Orbs burn sun-gold under a corona, add solar rays to their
    /// blast and throw sun-gold embers that land in small blasts. Echo casts burn violet.
    /// </summary>
    public sealed class EmberOrb : Weapon
    {
        private const int FIRE = 0;
        private const int MAX_TARGETS = 12;
        /// <summary>At most this many enemies one Sunfire blast consumes.</summary>
        private const int MAX_CONSUMED = 96;
        /// <summary>userValue flags of an orb or ember: an echo's, one that hit, an ember.</summary>
        private const int ECHO_FLAG = 1;
        private const int HIT_FLAG = 2;
        private const int EMBER_FLAG = 4;
        private const float LAUNCH_HEIGHT = 1.3f;
        private const float LAUNCH_FORWARD = 0.5f;
        private const float ORB_RADIUS = 0.35f;
        private const float LIFETIME_SHARE = 1.4f;
        private const float MUZZLE_SIZE = 0.9f;
        private const int FIZZLE_SPARKS = 6;
        private const float BLAST_HEIGHT = 1.1f;
        private const float EMBER_GRAVITY = 20.0f;
        private const float EMBER_FLIGHT = 0.4f;
        private const float EMBER_HEIGHT = 0.6f;
        private const float EMBER_LIFETIME = 1.5f;
        private const float EMBER_KNOCKBACK = 0.3f;

        private readonly int[] targets = new int[MAX_TARGETS];
        private readonly int[] consumed = new int[MAX_CONSUMED];
        private readonly int orbVisual;
        private readonly int sunVisual;
        private readonly int emberVisual;
        private readonly int fizzle;
        private readonly int muzzle;
        private readonly FxComposite fireImpact;
        private readonly FxComposite sunImpact;
        private readonly FxComposite emberLanding;

        public EmberOrb(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            orbVisual = context.FindVisual("EmberOrb");
            sunVisual = context.FindVisual("SunfireOrb");
            emberVisual = context.FindVisual("Ember");
            fizzle = effects.Find("EmberFizzle");
            muzzle = effects.Find("FireMuzzle");
            fireImpact = Composite(new FxLayer("FireFlash", 1, 0.9f), new FxLayer("FireBall", 4, 0.75f), FxLayer.Ground("FireShock", 2.0f),
                                   new FxLayer("FireSparks", 16), new FxLayer("FireEmbers", 8), new FxLayer("FireSmoke", 2, 0.8f, false),
                                   FxLayer.Ground("ScorchMark", 1.6f, false));
            sunImpact = Composite(new FxLayer("FireFlash", 1, 1.1f), new FxLayer("SolarRays", 1, 1.8f), new FxLayer("FireBall", 6, 0.75f),
                                  FxLayer.Ground("FireShock", 2.1f), new FxLayer("FireSparks", 26), new FxLayer("FireEmbers", 18),
                                  new FxLayer("FireSmoke", 3, 0.85f, false), FxLayer.Ground("ScorchMark", 1.8f, false));
            emberLanding = Composite(new FxLayer("FireFlash", 1, 2.0f), new FxLayer("FireBall", 1, 0.8f), new FxLayer("FireSparks", 8),
                                     FxLayer.Ground("ScorchMark", 1.4f, false));
        }

        private Color fireColor => isEvolved ? MageLooks.SUN : MageLooks.FIRE;

        protected override bool Cast(in CastInfo cast)
        {
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_TARGETS);
            int found = FindNearest(level[WeaponField.Range], targets, count);
            if (found == 0)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                int row = targets[i % found];
                Schedule(level[WeaponField.Stagger] * i, new WeaponEvent
                {
                    kind = FIRE,
                    target = enemies.GetEnemy(row),
                    position = enemies.GetPosition(row),
                    cast = cast,
                });
            }
            RequestAttack("CastShoot");
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == FIRE)
            {
                Fire(e);
            }
        }

        private void Fire(in WeaponEvent e)
        {
            Vector3 origin = heroPosition + facing * LAUNCH_FORWARD + Ground.Up(LAUNCH_HEIGHT);
            int row = enemies.GetRow(e.target);
            Vector3 aim = row >= 0 ? enemies.GetPosition(row) : e.position;
            Vector3 direction = Ground.Direction(origin, aim, facing);
            float speed = SpeedOf(level[WeaponField.Speed]);
            ProjectileSpawn spawn = NewProjectile(isEvolved ? sunVisual : orbVisual, ProjectileMotion.Homing, origin, direction * speed,
                                                  level[WeaponField.Range] * LIFETIME_SHARE / speed);
            spawn.homing = level[WeaponField.Turn] * Ground.DEGREES_TO_RADIANS;
            spawn.damage = DamageOf(WeaponField.Damage, e.cast);
            spawn.radius = ORB_RADIUS;
            spawn.pierce = 0;
            spawn.handler = this;
            spawn.userValue = e.cast.isEcho ? ECHO_FLAG : 0;
            spawn.headTint = e.cast.isEcho ? ECHO_TINT : default;
            Spawn(spawn);
            EmitSized(muzzle, origin, Tint(e.cast, fireColor), MUZZLE_SIZE * fxScale);
            PlayAt("ember_whoosh", origin);
        }

        /// <summary>The cast an orb or ember belongs to, from its flags (an echo's carries the echo's scale and violet).</summary>
        private static CastInfo CastOf(in ProjectileSpawn orb) => (orb.userValue & ECHO_FLAG) != 0 ? CastInfo.echo : CastInfo.normal;

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn orb = ref world.Get(projectile);
            if ((orb.userValue & EMBER_FLAG) != 0)
            {
                return;
            }
            orb.userValue |= HIT_FLAG;
            Explode(enemies.GetPosition(enemyRow), orb.damage, CastOf(orb));
        }

        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            ref ProjectileSpawn orb = ref world.Get(projectile);
            CastInfo cast = CastOf(orb);
            if ((orb.userValue & EMBER_FLAG) != 0)
            {
                LandEmber(position, orb.damage, cast);
            }
            else if ((orb.userValue & HIT_FLAG) == 0)
            {
                Emit(fizzle, position, FIZZLE_SPARKS, Tint(cast, MageLooks.FIRE));
            }
        }

        private void Explode(Vector3 at, float damage, in CastInfo cast)
        {
            Vector3 ground = Ground.Flat(at);
            float radius = AreaOf(level[WeaponField.Radius]);
            float burnPerSecond = BurnPerSecondOf(cast);
            float burnSeconds = DurationOf(level[WeaponField.SubDuration]);
            var effect = new HitEffect { burnPerSecond = burnPerSecond, burnSeconds = burnSeconds, numberKind = NumberKind(cast) };
            strikes.Circle(ground, radius, damage, KnockbackOf(level[WeaponField.Knockback]), slot, effect);
            if (isEvolved)
            {
                ConsumeBurning(burnPerSecond * burnSeconds);
            }
            (isEvolved ? sunImpact : fireImpact).Play(ground + Ground.Up(BLAST_HEIGHT), radius, fxScale, Tint(cast, fireColor));
            PlayAt("ember_boom", ground);
            if (isEvolved)
            {
                ThrowEmbers(ground, cast);
            }
        }

        /// <summary>Sunfire: enemies the new burn would finish anyway die at once (bosses excepted).</summary>
        private void ConsumeBurning(float burnTotal)
        {
            ReadOnlySpan<int> hits = strikes.lastHits;
            int count = 0;
            for (int i = 0; i < hits.Length && count < consumed.Length; i++)
            {
                int row = hits[i];
                if (enemies.IsTargetable(row) && !strikes.IsBoss(row) && enemies.GetHealth(row) <= burnTotal)
                {
                    consumed[count++] = row;
                }
            }
            for (int i = 0; i < count; i++)
            {
                int row = consumed[i];
                if (enemies.IsTargetable(row))
                {
                    enemies.Kill(enemies.GetEnemy(row), slot);
                }
            }
        }

        /// <summary>Sunfire: embers lobbed out in a ring, landing SubRange away (Area).</summary>
        private void ThrowEmbers(Vector3 ground, in CastInfo cast)
        {
            int count = level.GetCount(WeaponField.SubCount);
            float start = Ground.RandomYaw();
            float distance = AreaOf(level[WeaponField.SubRange]);
            float rise = (0.5f * EMBER_GRAVITY * EMBER_FLIGHT * EMBER_FLIGHT - EMBER_HEIGHT) / EMBER_FLIGHT;
            for (int k = 0; k < count; k++)
            {
                Vector3 direction = Ground.RingDirection(start, k, count);
                Vector3 velocity = direction * (distance / EMBER_FLIGHT) + Ground.Up(rise);
                ProjectileSpawn spawn = NewProjectile(emberVisual, ProjectileMotion.Arc, ground + Ground.Up(EMBER_HEIGHT), velocity, EMBER_LIFETIME);
                spawn.gravity = EMBER_GRAVITY;
                spawn.damage = MightOf(level[WeaponField.SubDamage], cast);
                spawn.handler = this;
                spawn.userValue = EMBER_FLAG | (cast.isEcho ? ECHO_FLAG : 0);
                Spawn(spawn);
            }
        }

        private void LandEmber(Vector3 position, float damage, in CastInfo cast)
        {
            Vector3 ground = Ground.Flat(position);
            float radius = AreaOf(level[WeaponField.SubRadius]);
            var effect = new HitEffect { numberKind = NumberKind(cast, DamageKind.Burn) };
            strikes.Circle(ground, radius, damage, KnockbackOf(EMBER_KNOCKBACK), slot, effect);
            emberLanding.Play(ground + Ground.Up(BLAST_HEIGHT), radius, fxScale, Tint(cast, MageLooks.SUN));
        }
    }
}

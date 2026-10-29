using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Spirit Darts (shared, Common): Count + Amount homing darts (Speed, Projectile Speed; turning Turn degrees per
    /// second), Stagger apart, launched toward the nearest enemy within Range with a little spread; each hits for Damage
    /// through Pierce enemies.
    /// Evolution Soul Hail (+ Tome of Insight): a continuous stream of single darts (each point of Amount speeds the
    /// stream up by AmountScale instead of adding a dart), and every rewarded kill by a dart gives ExperiencePerKill
    /// experience (Growth applies).
    /// Look: pale-blue particle darts (SharedLooks) with a glint at the hand and a pop of sparks, a ring and motes on
    /// each hit. Soul Hail turns them a deeper spectral blue, keeps a soul circle under the hero while it streams,
    /// bursts hits into soul shards and lets every rewarded kill release a rising soul.
    /// </summary>
    public sealed class SpiritDarts : Weapon
    {
        private const int DART = 0;
        private const int SOUL = 1;
        private const float DART_RADIUS = 0.25f;
        private const float LAUNCH_HEIGHT = 1.3f;
        private const float SPREAD_DEGREES = 25.0f;
        private const float STREAM_SPREAD_DEGREES = 8.0f;
        private const float DART_LIFETIME = 2.5f;
        private const float MUZZLE_SIZE = 0.4f;
        /// <summary>Hits pop at this share of the enemy's height.</summary>
        private const float POP_HEIGHT_SHARE = 0.7f;
        /// <summary>A dart ending within this of its lifetime ran out of time (a pierce-out already popped).</summary>
        private const float LIFETIME_END_TOLERANCE = 0.03f;
        private const float SIGIL_INTERVAL = 0.12f;
        private const float SIGIL_SECONDS = 0.4f;
        private const float SIGIL_DIAMETER = 1.8f;
        private const float SOUL_HEIGHT = 0.6f;

        /// <summary>Soul Hail's deeper spectral blue: heads (spawn tint), pops, sigil and souls.</summary>
        private static readonly Color SOUL_BLUE = new Color(0.45f, 0.62f, 1.0f, 1.0f);

        private readonly int dartVisual;
        private readonly int muzzle;
        private readonly int sigil;
        private readonly FxComposite spiritPop;
        private readonly FxComposite soulPop;
        private readonly FxComposite soulRelease;
        private readonly FxComposite dissolve;
        private Throttle sigilThrottle = new Throttle(SIGIL_INTERVAL);

        public SpiritDarts(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            dartVisual = context.FindVisual("SpiritDart");
            muzzle = effects.Find("SharedMuzzleStar");
            sigil = effects.Find("SharedSoulSigil");
            spiritPop = Composite(new FxLayer("SharedFlash", 1, 0.55f), new FxLayer("SharedPing", 1, 0.7f), new FxLayer("SharedSparks", 5),
                                  new FxLayer("SharedMotes", 2));
            soulPop = Composite(new FxLayer("SharedFlash", 1, 0.7f), new FxLayer("SharedPing", 1, 0.9f), new FxLayer("SharedSoulShards", 5),
                                new FxLayer("SharedSparks", 5), new FxLayer("SharedMotes", 3));
            soulRelease = Composite(new FxLayer("SharedRise", 1, 1.1f), new FxLayer("SharedPing", 1, 1.0f), new FxLayer("SharedMotes", 4));
            dissolve = Composite(new FxLayer("SharedPing", 1, 0.45f), new FxLayer("SharedMotes", 2));
        }

        protected override float baseCooldown => isEvolved
            ? level[WeaponField.Cooldown] / (1.0f + level[WeaponField.AmountScale] * amount)
            : level[WeaponField.Cooldown];

        private Color color => isEvolved ? SOUL_BLUE : SharedLooks.SPIRIT;

        protected override bool Cast(in CastInfo cast)
        {
            if (!TryNearest(level[WeaponField.Range], out _))
            {
                return false;
            }
            int darts = isEvolved ? 1 : level.GetCount(WeaponField.Count) + amount;
            for (int i = 0; i < darts; i++)
            {
                Schedule(level[WeaponField.Stagger] * i, new WeaponEvent { kind = DART, count = darts, cast = cast });
            }
            RequestAttack("CastShoot");
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == DART)
            {
                Fire(e);
            }
            else if (e.kind == SOUL)
            {
                soulRelease.Play(e.position + Ground.Up(SOUL_HEIGHT), fxScale, fxScale, SOUL_BLUE);
            }
        }

        private void Fire(in WeaponEvent e)
        {
            float spread = e.count > 1 ? SPREAD_DEGREES : STREAM_SPREAD_DEGREES;
            Vector3 direction = Ground.Rotate(AimFacingNearest(level[WeaponField.Range]), Random.Range(-spread, spread));
            Vector3 origin = heroPosition + Ground.Up(LAUNCH_HEIGHT);
            float speed = SpeedOf(level[WeaponField.Speed]);
            ProjectileSpawn spawn = NewProjectile(dartVisual, ProjectileMotion.Homing, origin, direction * speed, DART_LIFETIME);
            spawn.homing = level[WeaponField.Turn] * Ground.DEGREES_TO_RADIANS;
            spawn.damage = DamageOf(WeaponField.Damage, e.cast);
            spawn.radius = DART_RADIUS;
            spawn.pierce = level.GetCount(WeaponField.Pierce);
            spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
            spawn.handler = this;
            spawn.headTint = isEvolved ? SOUL_BLUE : default;
            Spawn(spawn);
            EmitSized(muzzle, origin, color, MUZZLE_SIZE * fxScale);
            if (isEvolved)
            {
                ShowSoulSigil();
            }
            PlayAt("spirit_dart", origin);
        }

        /// <summary>Soul Hail: the soul circle under the hero, re-emitted while the stream fires so it follows the hero.</summary>
        private void ShowSoulSigil()
        {
            if (!sigilThrottle.TryPass(clock))
            {
                return;
            }
            Vector3 ground = Ground.Flat(heroPosition) + Ground.Up(FxSizes.GROUND_LIFT);
            EmitShape(sigil, ground, SOUL_BLUE, SIGIL_DIAMETER * fxScale, SIGIL_SECONDS);
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn dart = ref world.Get(projectile);
            Vector3 position = enemies.GetPosition(enemyRow);
            float height = enemies.GetHeight(enemyRow);
            HitAlongFlight(enemyRow, dart);
            Vector3 at = position + Ground.Up(height * POP_HEIGHT_SHARE);
            (isEvolved ? soulPop : spiritPop).Play(at, fxScale, fxScale, color);
        }

        /// <summary>A dart that ran out of time fades into motes.</summary>
        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            if (world.GetAge(projectile) >= world.Get(projectile).lifetime - LIFETIME_END_TOLERANCE)
            {
                dissolve.Play(position, fxScale, 1.0f, color);
            }
        }

        /// <summary>Soul Hail: a dart kill gives its experience bonus straight to the hero and releases a soul.</summary>
        public override void OnEnemyKilled(in EnemyDeath death)
        {
            if (isEvolved && death.source == slot && death.isRewarded)
            {
                ctx.run.AddExperience(level[WeaponField.ExperiencePerKill] * stats[StatId.Growth]);
                Schedule(0.0f, new WeaponEvent { kind = SOUL, position = death.position });
            }
        }
    }
}

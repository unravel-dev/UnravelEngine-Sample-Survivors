using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Fire Flask (shared, Uncommon): Count + Amount flasks, Stagger apart, lobbed at random enemies within Range (a
    /// SubInterval flight) shatter into burning pools of Radius (Area) that deal Damage every Interval for Duration
    /// (Duration stat).
    /// Evolution Hellfire Cask (+ Wide Sigil): bigger pools in a ring SubRange around the hero.
    /// Look: a flask with a burning wick (SharedLooks) and a fiery streak along its arc; it shatters in a flash, a
    /// fireball, burning droplets, glass and smoke, and leaves fire on the ground (the pool's zone look): dark scorch,
    /// licking flames that light the moor, smouldering coals, rising embers and smoke. Hellfire casks roll black smoke
    /// in flight, erupt in a column of fire, burn a gold glyph under each pool and send black smoke up from it.
    /// </summary>
    public sealed class FireFlask : Weapon
    {
        private const int THROW = 0;
        private const int MAX_FLASKS = 12;
        /// <summary>Lob gravity (m/s^2); the SharedFlaskArc trail falls with the same, so it draws the flask's path.</summary>
        private const float GRAVITY = 25.0f;
        private const float LAUNCH_HEIGHT = 1.4f;
        private const float SPARE_SPREAD = 3.0f;
        private const float SAFETY_LIFETIME_SHARE = 3.0f;
        private const float SIGIL_HEIGHT = 0.03f;
        private const float SCORCH_HEIGHT = 0.02f;
        private const float SCORCH_SHARE = 1.15f;
        private const float SIGIL_SHARE = 0.9f;
        private const float SHATTER_HEIGHT = 0.4f;
        private const float ARC_WIDTH = 0.24f;
        private const float HELLFIRE_ARC_WIDTH = 0.36f;
        private const int TOSS_EMBERS = 3;
        /// <summary>The burning pool: licking flames, then coals, embers and smoke anywhere on it (Hellfire's smoke more often).</summary>
        private const float FLAME_INTERVAL = 0.08f;
        private const int FLAME_COUNT = 3;
        private const int HELLFIRE_FLAME_COUNT = 5;
        private const float POOL_FX_INTERVAL = 0.15f;
        private const float POOL_SPREAD = 1.0f;
        private const float POOL_EMBER_HEIGHT = 0.2f;
        private const float COAL_HEIGHT = 0.04f;
        private const int COAL_COUNT = 2;
        /// <summary>Smoke rises every this many coal sprinkles (Hellfire's black smoke more often).</summary>
        private const int SMOKE_EVERY = 3;
        private const int HELLFIRE_SMOKE_EVERY = 2;
        private const float SMOKE_INTERVAL = POOL_FX_INTERVAL * SMOKE_EVERY;
        private const float HELLFIRE_SMOKE_INTERVAL = POOL_FX_INTERVAL * HELLFIRE_SMOKE_EVERY;

        /// <summary>The Hellfire glyph burns gold so it never reads as an enemy's red-orange telegraph.</summary>
        private static readonly Color HELLFIRE_GOLD = new Color(1.0f, 0.7f, 0.3f, 1.0f);

        private readonly int flaskVisual;
        private readonly int caskVisual;
        private readonly int arc;
        private readonly int embers;
        private readonly int scorch;
        private readonly int sigil;
        private readonly ZoneLook poolLook;
        private readonly ZoneLook hellfireLook;
        private readonly FxComposite shatter;
        private readonly FxComposite caskShatter;

        public FireFlask(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            flaskVisual = context.FindVisual("FireFlask");
            caskVisual = context.FindVisual("HellfireCask");
            arc = effects.Find("SharedFlaskArc");
            embers = effects.Find("FireEmbers");
            scorch = effects.Find("ScorchMark");
            sigil = effects.Find("SharedHellfireSigil");
            int coals = effects.Find("SharedFlaskCoals");
            poolLook = BuildPoolLook(effects.Find("SharedFlaskFire"), FLAME_COUNT, coals, effects.Find("FireSmoke"), SMOKE_INTERVAL, SharedLooks.FIRE);
            hellfireLook = BuildPoolLook(effects.Find("SharedHellfireFire"), HELLFIRE_FLAME_COUNT, coals, effects.Find("SharedHellSmoke"),
                                         HELLFIRE_SMOKE_INTERVAL, SharedLooks.HELLFIRE);
            shatter = Composite(new FxLayer("FireFlash", 1, 0.9f), new FxLayer("FireBall", 5, 0.55f), new FxLayer("SharedFlaskSplash", 12),
                                new FxLayer("SharedFlaskGlass", 7, 0.0f, false), new FxLayer("FireSmoke", 2, 0.6f, false));
            caskShatter = Composite(new FxLayer("FireFlash", 1, 0.9f), new FxLayer("SharedHellfirePillar", 2, 1.5f, true, true),
                                    new FxLayer("FireBall", 7, 0.5f), new FxLayer("SharedFlaskSplash", 16), new FxLayer("SharedFlaskGlass", 8, 0.0f, false),
                                    new FxLayer("SharedHellSmoke", 3, 0.0f, false));
        }

        private Color color => isEvolved ? SharedLooks.HELLFIRE : SharedLooks.FIRE;

        /// <summary>Seconds a flask flies before it lands.</summary>
        private float flightSeconds => level[WeaponField.SubInterval];

        /// <summary>A pool's fire: licking flames that light the moor, smouldering coals, rising embers and smoke.</summary>
        private ZoneLook BuildPoolLook(int flames, int flameCount, int coals, int smoke, float smokeInterval, Color fire)
        {
            return new ZoneLook(
                new ZoneStream(flames, FLAME_INTERVAL, flameCount, fire),
                new ZoneStream(coals, POOL_FX_INTERVAL, COAL_COUNT, fire) { spread = POOL_SPREAD, height = COAL_HEIGHT },
                new ZoneStream(embers, POOL_FX_INTERVAL, 1, fire) { spread = POOL_SPREAD, height = POOL_EMBER_HEIGHT },
                new ZoneStream(smoke, smokeInterval, 1, WHITE) { spread = POOL_SPREAD, height = 0.0f });
        }

        protected override bool Cast(in CastInfo cast)
        {
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_FLASKS);
            if (isEvolved)
            {
                float start = Ground.RandomYaw();
                for (int k = 0; k < count; k++)
                {
                    Vector3 target = heroPosition + Ground.RingDirection(start, k, count) * level[WeaponField.SubRange];
                    Schedule(level[WeaponField.Stagger] * k, new WeaponEvent { kind = THROW, position = target, cast = cast });
                }
                RequestAttack("Throw");
                return true;
            }
            if (!TryRandomInView(level[WeaponField.Range], out int first))
            {
                return false;
            }
            Vector3 firstTarget = enemies.GetPosition(first);
            for (int k = 0; k < count; k++)
            {
                Vector3 target = k == 0 ? firstTarget
                    : TryRandomInView(level[WeaponField.Range], out int row) ? enemies.GetPosition(row) : firstTarget + Ground.RandomInCircle(SPARE_SPREAD);
                Schedule(level[WeaponField.Stagger] * k, new WeaponEvent { kind = THROW, position = target, cast = cast });
            }
            RequestAttack("Throw");
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == THROW)
            {
                Lob(Ground.Flat(e.position));
            }
        }

        /// <summary>Throws so the flask lands on <paramref name="target"/> after the flight time.</summary>
        private void Lob(Vector3 target)
        {
            float flight = flightSeconds;
            Vector3 start = heroPosition + Ground.Up(LAUNCH_HEIGHT);
            Vector3 across = Ground.Flat(target - start) * (1.0f / flight);
            float rise = (0.5f * GRAVITY * flight * flight - start.y) / flight;
            Vector3 velocity = across + Ground.Up(rise);
            ProjectileSpawn spawn = NewProjectile(isEvolved ? caskVisual : flaskVisual, ProjectileMotion.Arc, start, velocity,
                                                  flight * SAFETY_LIFETIME_SHARE);
            spawn.gravity = GRAVITY;
            spawn.handler = this;
            Spawn(spawn);
            effects.EmitMoving(arc, start, velocity, 1, color, (isEvolved ? HELLFIRE_ARC_WIDTH : ARC_WIDTH) * fxScale, flight);
            Emit(embers, start, TOSS_EMBERS, color);
            PlayAt("flask_throw", start);
        }

        /// <summary>The flask landed: a burning pool starts there (a flask passes through the enemies it flies over).</summary>
        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            Vector3 ground = Ground.Flat(position);
            float radius = AreaOf(level[WeaponField.Radius]);
            float duration = DurationOf(level[WeaponField.Duration]);
            ctx.zones.Add(new ZoneSpec
            {
                shape = ZoneShape.Circle,
                a = ground,
                b = ground,
                radius = radius,
                damage = DamageOf(WeaponField.Damage, CastInfo.normal),
                interval = level[WeaponField.Interval],
                duration = duration,
                source = slot,
                effect = new HitEffect { numberKind = DamageKind.Burn },
                look = isEvolved ? hellfireLook : poolLook,
            });
            ShowPool(ground, radius, duration);
            PlayAt("flask_shatter", ground);
        }

        /// <summary>The shatter burst and the pool's dark scorch (and the Hellfire glyph); the zone's look burns on it.</summary>
        private void ShowPool(Vector3 ground, float radius, float duration)
        {
            float diameter = radius * 2.0f;
            EmitShape(scorch, ground + Ground.Up(SCORCH_HEIGHT), WHITE, diameter * SCORCH_SHARE, duration);
            if (isEvolved)
            {
                EmitShape(sigil, ground + Ground.Up(SIGIL_HEIGHT), HELLFIRE_GOLD, diameter * SIGIL_SHARE, duration);
            }
            (isEvolved ? caskShatter : shatter).Play(ground + Ground.Up(SHATTER_HEIGHT), radius, fxScale, color);
        }
    }
}

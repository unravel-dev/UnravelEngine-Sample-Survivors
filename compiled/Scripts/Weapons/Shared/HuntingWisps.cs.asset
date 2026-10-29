using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Hunting Wisps (shared, Rare): Count + Amount spirit wisps circle the hero on two loose, counter-rotating rings
    /// whose radius breathes; each in turn (every Cooldown per wisp, Cooldown stat) dives at a random enemy within Range
    /// at Speed (Projectile Speed), splashes it for Damage in SubRadius (Area) with a little Knockback, and flies back
    /// to its place in the ring.
    /// Evolution Wisp Choir (+ Lodestone Charm): more wisps, faster dives, and the wisps carry gems within SubRange back
    /// to the hero.
    /// Look: mint spirit flames around a pale core (SharedLooks) that flare as they dive and splash in a flash, a
    /// swirl, glints, rising ghost flames, a ground ring and motes. Choir wisps burn warm white under golden rays; their
    /// splashes add a column of light and corona rays.
    /// </summary>
    public sealed class HuntingWisps : Weapon
    {
        private const int MAX_WISPS = 12;
        private const float ORBIT_RADIUS = 1.6f;
        private const float OUTER_RING_EXTRA = 0.6f;
        private const float ORBIT_SPEED = 1.7f;
        private const float BREATH_SHARE = 0.2f;
        private const float BREATH_RATE = 0.5f;
        /// <summary>The wisp look's orbit height (ProjectileCatalog "Wisp").</summary>
        private const float ORBIT_HEIGHT = 1.8f;
        private const float AIM_HEIGHT = 0.8f;
        private const float RETURN_SPEED = 14.0f;
        private const float ARRIVE_DISTANCE = 0.5f;
        /// <summary>A diving wisp has arrived when it is within this many frames' flight of its aim (it cannot overshoot).</summary>
        private const float ARRIVE_STEPS = 1.2f;
        private const float DIVE_TIMEOUT = 1.5f;
        private const float RETRY_SECONDS = 0.2f;
        /// <summary>Seconds between Wisp Choir's gem carries.</summary>
        private const float CARRY_INTERVAL = 0.5f;
        private const float SPLASH_HEIGHT = 0.4f;
        private const float DASH_SIZE = 0.7f;
        private const int DASH_GLINTS = 3;

        /// <summary>The Choir's warm white: a touch warmer than SharedLooks' choir wisp.</summary>
        private static readonly Color CHOIR = new Color(1.0f, 0.95f, 0.72f, 1.0f);

        private enum WispPhase : byte
        {
            Orbiting,
            Diving,
            Returning,
        }

        private struct Wisp
        {
            public int row;
            public WispPhase phase;
            public float timer;
            public float angle;
            public float speed;
            public float radius;
            public float diveTime;
            public Enemy target;
            public Vector3 aim;
        }

        private readonly Wisp[] wisps = new Wisp[MAX_WISPS];
        private readonly int wispVisual;
        private readonly int choirVisual;
        private readonly int dash;
        private readonly int glints;
        private readonly FxComposite wispSplash;
        private readonly FxComposite choirSplash;
        private int wispCount;
        private int builtVisual = -1;
        private float carryTimer;

        public HuntingWisps(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            wispVisual = context.FindVisual("Wisp");
            choirVisual = context.FindVisual("ChoirWisp");
            dash = effects.Find("SharedMuzzleStar");
            glints = effects.Find("SharedGlints");
            wispSplash = Composite(new FxLayer("SharedFlash", 1, 1.1f), new FxLayer("SharedTwirl", 1, 2.0f), new FxLayer("SharedGlints", 6),
                                   new FxLayer("SharedRise", 2, 1.0f), FxLayer.Ground("SharedGroundRing", 2.4f), new FxLayer("SharedMotes", 3));
            choirSplash = Composite(new FxLayer("SharedFlash", 1, 1.3f), new FxLayer("SharedHolyPillar", 1, 1.0f, true, true),
                                    new FxLayer("SolarRays", 1, 1.5f), new FxLayer("SharedTwirl", 1, 2.4f), new FxLayer("SharedGlints", 9),
                                    new FxLayer("SharedRise", 3, 1.1f), FxLayer.Ground("SharedGroundRing", 2.8f), new FxLayer("SharedMotes", 5));
        }

        protected override bool usesCooldown => false;

        protected override bool Cast(in CastInfo cast) => false;

        private int visual => isEvolved ? choirVisual : wispVisual;

        private float diveInterval => level[WeaponField.Cooldown] * stats[StatId.Cooldown];

        private Color color => isEvolved ? CHOIR : SharedLooks.WISP;

        protected override void Update(float dt)
        {
            int desired = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_WISPS);
            if (desired != wispCount || builtVisual != visual)
            {
                Rebuild(desired);
            }
            if (isEvolved)
            {
                CarryGems(dt);
            }
            float breath = (Mathf.PingPong(clock * BREATH_RATE, 1.0f) * 2.0f - 1.0f) * BREATH_SHARE;
            for (int i = 0; i < wispCount; i++)
            {
                ref Wisp wisp = ref wisps[i];
                if (!IsOwned(wisp.row, ctx.visualOnly, visual))
                {
                    wisp.row = SpawnWisp(wisp.angle, wisp.speed, wisp.radius);
                    wisp.phase = WispPhase.Orbiting;
                    if (wisp.row < 0)
                    {
                        continue;
                    }
                }
                ref ProjectileSpawn body = ref projectiles.Get(wisp.row);
                switch (wisp.phase)
                {
                    case WispPhase.Orbiting:
                        wisp.angle = body.orbitAngle;
                        body.orbitRadius = wisp.radius * (1.0f + (i % 2 == 0 ? breath : -breath));
                        wisp.timer -= dt;
                        if (wisp.timer <= 0.0f)
                        {
                            TryDive(ref wisp, ref body);
                        }
                        break;
                    case WispPhase.Diving:
                        wisp.angle += wisp.speed * dt;
                        Dive(ref wisp, ref body, dt);
                        break;
                    default:
                        wisp.angle += wisp.speed * dt;
                        Return(ref wisp, ref body);
                        break;
                }
            }
        }

        /// <summary>Wisp Choir: gems and gold within SubRange of the hero fly to the hero.</summary>
        private void CarryGems(float dt)
        {
            carryTimer -= dt;
            if (carryTimer > 0.0f)
            {
                return;
            }
            carryTimer = CARRY_INTERVAL;
            ctx.run.pickups.AttractWithin(heroPosition, level[WeaponField.SubRange]);
        }

        private void Rebuild(int desired)
        {
            for (int i = 0; i < wispCount; i++)
            {
                ExpireOwned(ref wisps[i].row, ctx.visualOnly, builtVisual);
            }
            builtVisual = visual;
            wispCount = desired;
            float interval = diveInterval;
            for (int i = 0; i < desired; i++)
            {
                bool isOuter = i % 2 == 1;
                float angle = MathF.PI * 2.0f * i / desired;
                float speed = isOuter ? -ORBIT_SPEED : ORBIT_SPEED;
                float radius = isOuter ? ORBIT_RADIUS + OUTER_RING_EXTRA : ORBIT_RADIUS;
                wisps[i] = new Wisp
                {
                    row = SpawnWisp(angle, speed, radius),
                    phase = WispPhase.Orbiting,
                    timer = interval * (i + 1) / desired,
                    angle = angle,
                    speed = speed,
                    radius = radius,
                };
            }
        }

        private int SpawnWisp(float angle, float speed, float radius)
        {
            ProjectileSpawn spawn = NewProjectile(visual, ProjectileMotion.Orbit, heroPosition, Vector3.zero, PERMANENT);
            spawn.orbitAngle = angle;
            spawn.orbitSpeed = speed;
            spawn.orbitRadius = radius;
            spawn.handler = ctx.visualOnly;
            return Spawn(spawn);
        }

        private void TryDive(ref Wisp wisp, ref ProjectileSpawn body)
        {
            if (!TryRandomInView(level[WeaponField.Range], out int row))
            {
                wisp.timer = RETRY_SECONDS;
                return;
            }
            wisp.target = enemies.GetEnemy(row);
            wisp.aim = enemies.GetPosition(row) + Ground.Up(AIM_HEIGHT);
            wisp.phase = WispPhase.Diving;
            wisp.diveTime = 0.0f;
            body.motion = ProjectileMotion.Linear;
            body.velocity = (wisp.aim - body.position).normalized * SpeedOf(level[WeaponField.Speed]);
            EmitSized(dash, body.position, color, DASH_SIZE * fxScale);
            Emit(glints, body.position, DASH_GLINTS, color);
            PlayAt("wisp_whistle", body.position);
        }

        /// <summary>Homes on the target (or where it was) and splashes on arrival.</summary>
        private void Dive(ref Wisp wisp, ref ProjectileSpawn body, float dt)
        {
            wisp.diveTime += dt;
            int row = enemies.GetRow(wisp.target);
            if (row >= 0)
            {
                wisp.aim = enemies.GetPosition(row) + Ground.Up(AIM_HEIGHT);
            }
            Vector3 to = wisp.aim - body.position;
            float distance = to.magnitude;
            float speed = SpeedOf(level[WeaponField.Speed]);
            if (distance <= MathF.Max(ARRIVE_DISTANCE, speed * dt * ARRIVE_STEPS) || wisp.diveTime > DIVE_TIMEOUT)
            {
                Splash(wisp.aim);
                wisp.phase = WispPhase.Returning;
                return;
            }
            body.velocity = to * (speed / distance);
        }

        private void Splash(Vector3 at)
        {
            Vector3 ground = Ground.Flat(at);
            float radius = AreaOf(level[WeaponField.SubRadius]);
            strikes.Circle(ground, radius, DamageOf(WeaponField.Damage, CastInfo.normal), KnockbackOf(level[WeaponField.Knockback]), slot);
            (isEvolved ? choirSplash : wispSplash).Play(ground + Ground.Up(SPLASH_HEIGHT), radius, fxScale, color);
            PlayAt("wisp_splash", ground);
        }

        /// <summary>Flies back to its moving place in the ring and rejoins the orbit there.</summary>
        private void Return(ref Wisp wisp, ref ProjectileSpawn body)
        {
            var offset = new Vector3(MathF.Sin(wisp.angle), 0.0f, MathF.Cos(wisp.angle)) * wisp.radius;
            Vector3 hero = heroPosition;
            Vector3 goal = hero + offset + Ground.Up(ORBIT_HEIGHT);
            Vector3 to = goal - body.position;
            float distance = to.magnitude;
            if (distance <= ARRIVE_DISTANCE)
            {
                body.motion = ProjectileMotion.Orbit;
                body.orbitAngle = wisp.angle;
                body.orbitRadius = wisp.radius;
                wisp.phase = WispPhase.Orbiting;
                wisp.timer = diveInterval;
                return;
            }
            body.velocity = to * (MathF.Max(RETURN_SPEED, SpeedOf(level[WeaponField.Speed])) / distance);
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Skewer Javelin (shared, Common): Count + Amount heavy javelins thrown along the walk direction, fanned Angle
    /// degrees apart, at Speed (Projectile Speed) out to Range; each pierces everything for Damage and drags enemies
    /// along with its Knockback. Size (times Area) scales the hitbox (Width wide) and the mesh.
    /// Evolution Godspear (+ Whetstone): more and heavier javelins, each detonating at the end of its range (SubDamage
    /// in SubRadius, Area).
    /// Look: each javelin wears a steel glint (SharedLooks), leaves a bright steel streak along its path and speed
    /// lines off the throw; every enemy it skewers flashes with a ring, a streak across it along the flight line,
    /// speed lines punching out behind it, sparks and dust. Godspears blaze gold (SharedLooks) under a wide gold ray, and detonate in a
    /// column of light with corona rays, a ground ring, glowing cracks, rubble and dust.
    /// </summary>
    public sealed class SkewerJavelin : Weapon
    {
        /// <summary>userValue flag of a javelin thrown as a Godspear (it detonates at max range).</summary>
        private const int GODSPEAR_FLAG = 1;
        private const float LAUNCH_HEIGHT = 1.2f;
        private const float LAUNCH_FORWARD = 0.6f;
        private const float RANGE_END_TOLERANCE = 0.03f;
        private const float BLAST_KNOCKBACK = 1.5f;
        private const float BLAST_TRAUMA = 0.12f;
        private const float BLAST_HEIGHT = 0.5f;
        private const float MUZZLE_SIZE = 0.6f;
        private const float GODSPEAR_MUZZLE_SIZE = 1.1f;
        /// <summary>Path streak widths (m) per unit of javelin scale.</summary>
        private const float STREAK_WIDTH = 0.32f;
        private const float RAY_WIDTH = 0.5f;
        private const int SPEED_LINES = 3;
        private const float SPEED_LINE_SPREAD = 0.35f;
        private const float SPEED_LINE_SHARE_MIN = 0.55f;
        private const float SPEED_LINE_SHARE_MAX = 0.85f;
        private const float SPEED_LINE_SIZE = 0.12f;
        private const float SPEED_LINE_SECONDS = 0.18f;
        /// <summary>Skewer hits show at this share of the enemy's height.</summary>
        private const float HIT_HEIGHT_SHARE = 0.6f;
        private const float SKEWER_FLARE_SIZE = 2.0f;
        private const float SKEWER_FLARE_SECONDS = 0.16f;
        /// <summary>A slow drift (m/s) along the flight line that turns the skewer streak to it (align_to_direction).</summary>
        private const float SKEWER_FLARE_DRIFT = 0.05f;
        private const int SKEWER_LINES = 2;
        private const float SKEWER_LINE_SHARE = 0.5f;

        private static readonly Color STEEL = new Color(0.82f, 0.88f, 0.95f, 1.0f);
        private static readonly Color GOLD = new Color(1.0f, 0.85f, 0.45f, 1.0f);
        private static readonly Color GOLD_WHITE = new Color(1.0f, 0.95f, 0.8f, 1.0f);

        private readonly int javelinVisual;
        private readonly int godspearVisual;
        private readonly int muzzle;
        private readonly int streak;
        private readonly int ray;
        private readonly int speedLines;
        private readonly int skewerFlare;
        private readonly FxComposite skewer;
        private readonly FxComposite godspearSkewer;
        private readonly FxComposite blast;

        public SkewerJavelin(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            javelinVisual = context.FindVisual("SkewerJavelin");
            godspearVisual = context.FindVisual("Godspear");
            muzzle = effects.Find("SharedMuzzleStar");
            streak = effects.Find("SharedJavelinStreak");
            ray = effects.Find("SharedGodspearRay");
            speedLines = effects.Find("SharedSpeedLines");
            skewerFlare = effects.Find("SharedSkewerFlare");
            skewer = Composite(new FxLayer("SharedFlash", 1, 0.9f), new FxLayer("SharedPing", 1, 1.0f), new FxLayer("SharedSparks", 10),
                               new FxLayer("SharedDust", 1, 0.0f, false));
            godspearSkewer = Composite(new FxLayer("SharedFlash", 1, 1.0f), new FxLayer("SharedPing", 1, 1.1f), new FxLayer("SharedSparks", 12),
                                       new FxLayer("SharedGlints", 4), new FxLayer("SharedDust", 1, 0.0f, false));
            blast = Composite(new FxLayer("SharedFlash", 1, 1.0f), new FxLayer("SharedHolyPillar", 1, 1.3f, true, true), new FxLayer("SolarRays", 1, 1.2f),
                              FxLayer.Ground("SharedGroundRing", 2.2f), FxLayer.Ground("SharedGroundCracks", 1.6f), new FxLayer("SharedSparks", 12),
                              new FxLayer("SharedGlints", 12), new FxLayer("MeteorRocks", 8, 0.0f, false), new FxLayer("MeteorDust", 4, 0.45f, false));
        }

        protected override bool Cast(in CastInfo cast)
        {
            Vector3 forward = facing;
            int count = level.GetCount(WeaponField.Count) + amount;
            float fan = level[WeaponField.Angle];
            float speed = SpeedOf(level[WeaponField.Speed]);
            float lifetime = level[WeaponField.Range] / speed;
            for (int k = 0; k < count; k++)
            {
                Vector3 direction = Ground.Rotate(forward, (k - (count - 1) * 0.5f) * fan);
                Vector3 origin = heroPosition + direction * LAUNCH_FORWARD + Ground.Up(LAUNCH_HEIGHT);
                ProjectileSpawn spawn = NewProjectile(isEvolved ? godspearVisual : javelinVisual, ProjectileMotion.Linear, origin, direction * speed, lifetime);
                spawn.damage = DamageOf(WeaponField.Damage, cast);
                spawn.radius = level[WeaponField.Width] * 0.5f;
                spawn.scale = level[WeaponField.Size] * stats[StatId.Area];
                spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
                spawn.handler = this;
                spawn.userValue = isEvolved ? GODSPEAR_FLAG : 0;
                Spawn(spawn);
                ShowFlight(origin, spawn.velocity, lifetime, spawn.scale);
            }
            Vector3 hand = heroPosition + Ground.Up(LAUNCH_HEIGHT);
            EmitSized(muzzle, hand, isEvolved ? GOLD : STEEL, (isEvolved ? GODSPEAR_MUZZLE_SIZE : MUZZLE_SIZE) * fxScale);
            RequestAttack("Throw");
            PlayAt("javelin_throw", heroPosition);
            return true;
        }

        /// <summary>
        /// The javelin's path streak (one trail particle flying with it for its whole life), a Godspear's gold ray,
        /// and speed lines peeling off the throw.
        /// </summary>
        private void ShowFlight(Vector3 origin, Vector3 velocity, float lifetime, float scale)
        {
            Color color = isEvolved ? GOLD : STEEL;
            effects.EmitMoving(streak, origin, velocity, 1, isEvolved ? GOLD_WHITE : STEEL, STREAK_WIDTH * scale, lifetime);
            if (isEvolved)
            {
                effects.EmitMoving(ray, origin, velocity, 1, GOLD, RAY_WIDTH * scale, lifetime);
            }
            Vector3 side = Ground.Perpendicular(Ground.Normalize(velocity, facing));
            for (int k = 0; k < SPEED_LINES; k++)
            {
                Vector3 offset = side * Random.Range(-SPEED_LINE_SPREAD, SPEED_LINE_SPREAD) + Ground.Up(Random.Range(-SPEED_LINE_SPREAD, SPEED_LINE_SPREAD));
                Vector3 drift = velocity * Random.Range(SPEED_LINE_SHARE_MIN, SPEED_LINE_SHARE_MAX);
                effects.EmitMoving(speedLines, origin + offset, drift, 1, color, SPEED_LINE_SIZE * fxScale, SPEED_LINE_SECONDS);
            }
        }

        /// <summary>A javelin or Godspear skewers an enemy: its damage, pushed along its flight, and a skewer hit.</summary>
        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn javelin = ref world.Get(projectile);
            HitAlongFlight(enemyRow, javelin);
            Vector3 at = enemies.GetPosition(enemyRow) + Ground.Up(enemies.GetHeight(enemyRow) * HIT_HEIGHT_SHARE);
            ShowSkewer(at, javelin.velocity, (javelin.userValue & GODSPEAR_FLAG) != 0);
        }

        /// <summary>A skewer hit: flash, ring, sparks and dust, a streak across the enemy and lines punching out behind it.</summary>
        private void ShowSkewer(Vector3 at, Vector3 velocity, bool isGodspear)
        {
            Color color = isGodspear ? GOLD : STEEL;
            (isGodspear ? godspearSkewer : skewer).Play(at, fxScale, fxScale, color);
            Vector3 direction = Ground.Normalize(velocity, facing);
            effects.EmitMoving(skewerFlare, at, direction * SKEWER_FLARE_DRIFT, 1, color, SKEWER_FLARE_SIZE * fxScale, SKEWER_FLARE_SECONDS);
            Vector3 punch = direction * (velocity.magnitude * SKEWER_LINE_SHARE);
            for (int k = 0; k < SKEWER_LINES; k++)
            {
                Vector3 offset = Ground.Up(Random.Range(-SPEED_LINE_SPREAD, SPEED_LINE_SPREAD));
                effects.EmitMoving(speedLines, at + offset, punch, 1, color, SPEED_LINE_SIZE * fxScale, SPEED_LINE_SECONDS);
            }
        }

        /// <summary>Godspear: a javelin that flew its whole range detonates.</summary>
        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            ref ProjectileSpawn javelin = ref world.Get(projectile);
            if ((javelin.userValue & GODSPEAR_FLAG) == 0 || world.GetAge(projectile) < javelin.lifetime - RANGE_END_TOLERANCE)
            {
                return;
            }
            Vector3 ground = Ground.Flat(position);
            float radius = AreaOf(level[WeaponField.SubRadius]);
            strikes.Circle(ground, radius, MightOf(level[WeaponField.SubDamage]), KnockbackOf(BLAST_KNOCKBACK), slot);
            blast.Play(ground + Ground.Up(BLAST_HEIGHT), radius, fxScale, GOLD);
            PlayAt("godspear_boom", ground);
            ctx.feel.Shake(BLAST_TRAUMA);
        }
    }
}

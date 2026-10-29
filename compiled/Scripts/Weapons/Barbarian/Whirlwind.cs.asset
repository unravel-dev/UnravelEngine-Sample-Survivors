using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Whirlwind (Barbarian, Uncommon): the barbarian spins (full-body Spin) for Duration (Duration stat, +AmountScale
    /// per point of Amount), hitting everything within Radius (Area) every Interval and swirling it around him
    /// (Knockback), MoveSpeed faster while spinning. The whoosh climbs four semitones over the spin.
    /// Evolution Maelstrom (+ Windstride Boots): longer, wider, back to back, and it pulls enemies within SubRange
    /// (Area) inward at Pull metres per second.
    /// Look: two blades draw a ring of chained steel arcs with streaks at their tips; each tick sweeps dust and stone
    /// sparks off the rim; the spin starts with a dust kick and ends with a shockwave. Full Fury turns the blades blood
    /// red. Maelstrom is a dark storm: cold steel blades over a dark vortex covering the pull reach, lightning crackling
    /// in it and running along the ground, slate dust and debris drawn inward, and a thunderclap when it ends.
    /// </summary>
    public sealed class Whirlwind : Weapon
    {
        private const float QUARTER_TURN = 90.0f;
        private const float BLADE_HEIGHT = 1.0f;
        private const float BLADE_RADIUS_SHARE = 0.85f;
        private const float BLADE_DEGREES_PER_SECOND = 900.0f;
        private const int BLADES = 2;
        /// <summary>Four semitones: 2^(4/12).</summary>
        private const float PITCH_RISE = 1.26f;
        private const float LOOP_FADE_SECONDS = 0.12f;
        /// <summary>The pitch rise divides by the spin's length, never by less than this.</summary>
        private const float MIN_SPIN_SECONDS = 1e-3f;
        private const int SCRATCH_CAPACITY = 1024;

        /// <summary>Radius of the arc in its texture per metre of quad (BarbarianWhirlArc: slash_04).</summary>
        private const float ARC_TEXTURE_RADIUS = 0.30f;
        private const float ARC_INTERVAL = 0.05f;
        /// <summary>The arc emitter's own lifetime: its spin is authored for it.</summary>
        private const float ARC_SECONDS = 0.16f;
        private const float STREAK_SPEED = 8.0f;
        private const float STREAK_SIZE = 0.6f;
        private const float STREAK_SECONDS = 0.14f;
        private const float RIM_DUST_HEIGHT = 0.4f;
        private const float RIM_DUST_SPEED = 4.0f;
        private const float RIM_DUST_OUTWARD = 1.2f;
        private const float RIM_DUST_SIZE = 0.7f;
        private const float RIM_DUST_SECONDS = 0.9f;
        private const int RIM_DUST_COUNT = 2;
        private const int RIM_GRIT_COUNT = 3;
        private const float BURST_HEIGHT = 0.8f;
        // Maelstrom.
        /// <summary>A new vortex every interval, each living the emitter's 1.4 s: three overlap, turning slowly.</summary>
        private const float VORTEX_INTERVAL = 0.45f;
        private const float VORTEX_SECONDS = 1.4f;
        /// <summary>The vortex quad per metre of pull reach (its arms fade out at ~0.42 x the quad: ~1.2 x the reach).</summary>
        private const float VORTEX_PER_REACH = 2.9f;
        private const float STORM_DRIFT_IN = 3.0f;
        private const float STORM_DRIFT_AROUND = 4.5f;
        private const float STORM_DUST_SIZE = 1.0f;
        private const float STORM_DUST_SECONDS = 1.1f;
        private const float DEBRIS_LIFT = 3.0f;
        private const float DEBRIS_SIZE = 0.2f;
        private const float DEBRIS_SECONDS = 0.8f;
        private const float BOLT_HEIGHT_MIN = 0.4f;
        private const float BOLT_HEIGHT_MAX = 1.6f;
        private const float BOLT_SIZE_MIN = 1.1f;
        private const float BOLT_SIZE_MAX = 1.8f;
        /// <summary>The ground lightning runs through the middle of the pull reach, as long as the reach.</summary>
        private const float STRAND_REACH_SHARE = 0.5f;
        private const float STRAND_SECONDS = 0.14f;

        /// <summary>Full Fury on the blades: a shade deeper than BarbarianLooks.FURY_RED.</summary>
        private static readonly Color FURY_BLADE = new Color(1.0f, 0.1f, 0.06f, 1.0f);
        private static readonly Color STORM_STEEL = new Color(0.6f, 0.72f, 0.95f, 1.0f);
        private static readonly Color LIGHTNING = new Color(0.72f, 0.88f, 1.0f, 1.0f);
        private static readonly Color STORM_RING = new Color(0.62f, 0.72f, 0.95f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.GROUND_LIFT);

        private readonly int[] scratch = new int[SCRATCH_CAPACITY];
        /// <summary>The Barbarian's kit (Fury), or null for another class.</summary>
        private readonly BarbarianKit barbarian;
        private readonly int whirlArc;
        private readonly int streak;
        private readonly int dust;
        private readonly int grit;
        private readonly int shockRing;
        private readonly int dustRing;
        private readonly int vortex;
        private readonly int stormBolt;
        private readonly int stormStrand;
        private readonly int stormDust;
        private readonly int debris;
        private readonly FxComposite spinKick;
        private readonly FxComposite whirlStop;
        private readonly FxComposite thunderclap;
        private float spinStart;
        private float spinEnd = -1.0f;
        private float tickTimer;
        private float bladeAngle;
        private float arcTimer;
        private float vortexTimer;
        private StatBonus speedBonus = new StatBonus(StatId.MoveSpeed, true);
        private AudioVoice loop;

        public Whirlwind(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            barbarian = context.KitAs<BarbarianKit>();
            whirlArc = effects.Find("BarbarianWhirlArc");
            streak = effects.Find("BarbarianSweepStreak");
            dust = effects.Find("BarbarianDust");
            grit = effects.Find("BarbarianGrit");
            shockRing = effects.Find("BarbarianShockRing");
            dustRing = effects.Find("BarbarianDustRing");
            vortex = effects.Find("BarbarianStormVortex");
            stormBolt = effects.Find("BarbarianStormBolt");
            stormStrand = effects.Find("BarbarianStormStrand");
            stormDust = effects.Find("BarbarianStormDust");
            debris = effects.Find("BarbarianRocks");
            // Size unit = spin radius.
            spinKick = Composite(FxLayer.Ground("BarbarianDustRing", 1.6f, false), new FxLayer("BarbarianDust", 4, 0.25f, false),
                                 new FxLayer("BarbarianGrit", 6));
            whirlStop = Composite(new FxLayer("BarbarianGrit", 14), new FxLayer("BarbarianDust", 6, 0.3f, false),
                                  FxLayer.Ground("BarbarianDustRing", 2.0f, false));
            thunderclap = Composite(new FxLayer("BarbarianFlash", 1, 0.7f), new FxLayer("BarbarianStormBolt", 3, 0.45f),
                                    new FxLayer("BarbarianGrit", 16), new FxLayer("BarbarianStormDust", 8, 0.3f, false));
        }

        private bool isSpinning => spinEnd >= 0.0f;

        protected override bool Cast(in CastInfo cast)
        {
            float duration = DurationOf(level[WeaponField.Duration]) * (1.0f + level[WeaponField.AmountScale] * amount);
            if (!isSpinning)
            {
                spinStart = clock;
                tickTimer = 0.0f;
                loop = sounds.Play("whirlwind_loop");
                KickOff(AreaOf(level[WeaponField.Radius]));
            }
            spinEnd = clock + duration;
            hero.PlayFullBody("Spin", duration);
            speedBonus.Set(stats, level[WeaponField.MoveSpeed]);
            return true;
        }

        protected override void OnLevelChanged()
        {
            if (isSpinning)
            {
                speedBonus.Set(stats, level[WeaponField.MoveSpeed]);
            }
        }

        /// <summary>The weapon leaves mid-spin: the speed bonus ends and the whoosh fades out.</summary>
        protected override void OnDispose()
        {
            spinEnd = -1.0f;
            speedBonus.Set(stats, 0.0f);
            FadeLoop();
        }

        protected override void Update(float dt)
        {
            if (!isSpinning)
            {
                return;
            }
            if (clock >= spinEnd || !hero.isAlive)
            {
                Stop();
                return;
            }
            float radius = AreaOf(level[WeaponField.Radius]);
            tickTimer -= dt;
            if (tickTimer <= 0.0f)
            {
                tickTimer += level[WeaponField.Interval];
                Strike(radius);
            }
            DrawBlades(radius, dt);
            if (isEvolved)
            {
                DrawStorm(dt);
            }
            if (!loop.isPlaying)
            {
                // A long Maelstrom outlasts the clip: start it again.
                loop = sounds.Play("whirlwind_loop");
            }
            if (loop.started)
            {
                float progress = Math.Clamp((clock - spinStart) / MathF.Max(spinEnd - spinStart, MIN_SPIN_SECONDS), 0.0f, 1.0f);
                loop.SetPitch(Mathf.Lerp(1.0f, PITCH_RISE, progress));
            }
        }

        private void Strike(float radius)
        {
            Vector3 center = heroPosition;
            strikes.Circle(center, radius, DamageOf(WeaponField.Damage, CastInfo.normal), KnockbackOf(level[WeaponField.Knockback]), slot,
                           default, PushMode.Swirl);
            if (level[WeaponField.Pull] > 0.0f)
            {
                Pull(center);
            }
            DrawRim(center, radius);
        }

        /// <summary>Maelstrom: an inward impulse per tick sized so enemies drift in at Pull metres per second.</summary>
        private void Pull(Vector3 center)
        {
            float reach = AreaOf(level[WeaponField.SubRange]);
            float impulse = level[WeaponField.Pull] * level[WeaponField.Interval] * Tuning.KNOCKBACK_SPEED_PER_METRE;
            enemies.Pull(center, reach, impulse, scratch);
            DrawPull(center, reach);
        }

        /// <summary>The blade colour: cold storm steel for Maelstrom, blood red at full Fury, else warm steel.</summary>
        private Color bladeColor => isEvolved ? STORM_STEEL : barbarian != null && barbarian.isFuryFull ? FURY_BLADE : BarbarianLooks.STEEL;

        /// <summary>
        /// Two blades at 900 deg/s: a steel streak at each tip every frame and, every 0.05 s, an arc trailing each blade
        /// (headed at the blade, so its leading end sits on it); the fading arcs chain into a spinning ring.
        /// </summary>
        private void DrawBlades(float radius, float dt)
        {
            bladeAngle += BLADE_DEGREES_PER_SECOND * dt;
            Vector3 center = heroPosition + Ground.Up(BLADE_HEIGHT);
            float reach = radius * BLADE_RADIUS_SHARE;
            Color color = bladeColor;
            arcTimer -= dt;
            bool drawsArc = arcTimer <= 0.0f;
            if (drawsArc)
            {
                arcTimer += ARC_INTERVAL;
            }
            for (int blade = 0; blade < BLADES; blade++)
            {
                float yaw = bladeAngle + Ground.FULL_TURN * blade / BLADES;
                Vector3 outward = Ground.FromYaw(yaw);
                Vector3 along = Ground.FromYaw(yaw + QUARTER_TURN);
                effects.EmitMoving(streak, center + outward * reach, along * STREAK_SPEED, 1, color, STREAK_SIZE, STREAK_SECONDS);
                if (drawsArc)
                {
                    effects.EmitMoving(whirlArc, center, outward * FxSizes.HEADING_SPEED, 1, color, reach / ARC_TEXTURE_RADIUS, ARC_SECONDS);
                }
            }
        }

        /// <summary>Each tick sweeps dust and stone sparks off a random point of the rim, along the spin.</summary>
        private void DrawRim(Vector3 center, float radius)
        {
            Vector3 outward = Ground.RandomDirection();
            Vector3 rim = center + outward * radius;
            Vector3 around = Ground.Perpendicular(outward);
            Vector3 drift = around * RIM_DUST_SPEED + outward * RIM_DUST_OUTWARD;
            effects.EmitMoving(isEvolved ? stormDust : dust, rim + Ground.Up(RIM_DUST_HEIGHT), drift, RIM_DUST_COUNT, RIM_DUST_SIZE, RIM_DUST_SECONDS);
            Emit(grit, rim + Ground.Up(BLADE_HEIGHT), RIM_GRIT_COUNT, bladeColor);
        }

        /// <summary>Maelstrom's tick: slate dust and debris drawn in from the reach, a bolt in the storm and one along the ground.</summary>
        private void DrawPull(Vector3 center, float reach)
        {
            Vector3 outward = Ground.RandomDirection();
            Vector3 rim = center + outward * reach;
            Vector3 inward = Ground.Perpendicular(outward) * STORM_DRIFT_AROUND - outward * STORM_DRIFT_IN;
            effects.EmitMoving(stormDust, rim + Ground.Up(RIM_DUST_HEIGHT), inward, RIM_DUST_COUNT, STORM_DUST_SIZE, STORM_DUST_SECONDS);
            effects.EmitMoving(debris, rim + LIFT, inward + Ground.Up(DEBRIS_LIFT), 1, DEBRIS_SIZE, DEBRIS_SECONDS);
            Vector3 boltAt = center + Ground.RandomInCircle(reach) + Ground.Up(Random.Range(BOLT_HEIGHT_MIN, BOLT_HEIGHT_MAX));
            EmitSized(stormBolt, boltAt, LIGHTNING, Random.Range(BOLT_SIZE_MIN, BOLT_SIZE_MAX));
            Vector3 strand = Ground.RandomDirection();
            effects.EmitMoving(stormStrand, center + strand * (reach * STRAND_REACH_SHARE) + LIFT, strand * FxSizes.HEADING_SPEED, 1, LIGHTNING, reach,
                               STRAND_SECONDS);
        }

        /// <summary>Maelstrom's dark eye over the pull reach, renewed so it follows the hero.</summary>
        private void DrawStorm(float dt)
        {
            vortexTimer -= dt;
            if (vortexTimer > 0.0f)
            {
                return;
            }
            vortexTimer += VORTEX_INTERVAL;
            float reach = AreaOf(level[WeaponField.SubRange]);
            EmitShape(vortex, heroPosition + LIFT, WHITE, reach * VORTEX_PER_REACH, VORTEX_SECONDS);
        }

        /// <summary>The spin starts: dust kicked out around the feet (Maelstrom: the first lightning and the storm's eye).</summary>
        private void KickOff(float radius)
        {
            Vector3 at = heroPosition + Ground.Up(BURST_HEIGHT);
            spinKick.Play(at, radius, fxScale, bladeColor);
            arcTimer = 0.0f;
            vortexTimer = 0.0f;
            if (isEvolved)
            {
                EmitSized(stormBolt, at, LIGHTNING, BOLT_SIZE_MAX);
            }
        }

        private void Stop()
        {
            spinEnd = -1.0f;
            speedBonus.Set(stats, 0.0f);
            FadeLoop();
            DrawRelease(AreaOf(level[WeaponField.Radius]));
            PlayAt("whirlwind_stop", heroPosition);
        }

        private void FadeLoop()
        {
            if (loop.started)
            {
                loop.FadeTo(0.0f, LOOP_FADE_SECONDS, true);
            }
            loop = default;
        }

        /// <summary>The spin ends with a shockwave at its radius (Maelstrom: a thunderclap with a cold flash and bolts).</summary>
        private void DrawRelease(float radius)
        {
            Vector3 at = heroPosition + Ground.Up(BURST_HEIGHT);
            Vector3 ground = Ground.Flat(heroPosition) + LIFT;
            if (isEvolved)
            {
                thunderclap.Play(at, radius, fxScale, LIGHTNING);
                EmitSized(shockRing, ground, STORM_RING, radius * FxSizes.SHOCK_RING_SIZE_PER_RADIUS);
                EmitSized(dustRing, ground, STORM_RING, radius * FxSizes.SHOCK_RING_SIZE_PER_RADIUS);
                return;
            }
            whirlStop.Play(at, radius, fxScale, bladeColor);
            EmitSized(shockRing, ground, WHITE, radius * FxSizes.SHOCK_RING_SIZE_PER_RADIUS);
        }
    }
}

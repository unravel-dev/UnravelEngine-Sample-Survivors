using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Oathblade Arc (Knight, Common): a wide slice (Angle, Radius after Area) toward the nearest enemy within Range
    /// (else the walk direction) that hits everything in it and throws it back (Knockback); extra swings (Count +
    /// Amount) alternate to the back, Stagger apart.
    /// Evolution Dawnbreaker (+ Iron Heart): a full sweep; every Every-th swing launches a golden crescent wave
    /// (SubDamage, Width wide, SubRange far at SubSpeed), and the swings heal Heal per enemy hit, at most HealCap per
    /// second.
    /// Look: each swing opens a steel-gold crescent over the arc (two side by side once the arc is wider than the
    /// texture's) with a white-hot core, wind lines sweeping the swing's way, sparks flung off the edge and a glint on
    /// the blade; every enemy hit flashes a star. Dawnbreaker sweeps a ring of dawn-gold crescents inside a spinning
    /// whirl, raises shafts of sunrise light around the knight and leaves a golden ring that blooms after the blade; its
    /// crescent waves leave golden afterimages along their flight and dissolve into motes, and heals lift motes.
    /// </summary>
    public sealed class OathbladeArc : Weapon
    {
        private const int SWING = 0;
        private const float SLASH_HEIGHT = 1.0f;
        private const float CRESCENT_HEIGHT = 1.0f;
        private const float CRESCENT_FORWARD = 0.6f;
        private const float CRESCENT_KNOCKBACK = 1.0f;
        /// <summary>Heals wait in a bank of at most this much health and are paid in whole points within the per-second cap.</summary>
        private const float HEAL_BANK_LIMIT = 6.0f;
        private const float MIN_HEAL = 1.0f;

        /// <summary>The arc one slash crescent covers; wider swings lay two side by side, a full sweep three.</summary>
        private const float ARC_TEXTURE_DEGREES = 136.0f;
        private const float ARC_OVERLAP_DEGREES = 10.0f;
        private const int WIDE_SWEEP_ARCS = 2;
        private const int FULL_SWEEP_ARCS = 3;
        /// <summary>Crescent size per hit radius: its band spans 0.55-0.94 of the radius around the knight.</summary>
        private const float ARC_SIZE_PER_RADIUS = 3.0f;
        private const float ARC_SECONDS = 0.2f;
        private const float ARC_CORE_SECONDS = 0.09f;
        private const float ARC_CORE_SHARE = 0.96f;
        /// <summary>The crescent drifts out slowly; the velocity also turns it along the swing.</summary>
        private const float ARC_DRIFT = 0.6f;
        /// <summary>Overlapping crescents of a wide or full sweep are dimmed so they never stack into white.</summary>
        private const float OVERLAP_ALPHA = 0.7f;
        private const float STREAK_DEGREES = 14.0f;
        private const int STREAK_MIN = 6;
        private const int STREAK_MAX = 24;
        private const float STREAK_OUTER = 0.95f;
        private const float STREAK_INNER = 0.68f;
        private const float STREAK_SPEED = 9.0f;
        private const float STREAK_WIDTH = 0.7f;
        private const float STREAK_SECONDS = 0.2f;
        /// <summary>The sweep's trailing streak fades at this share of the leading one's lifetime: the streaks read as a sweep.</summary>
        private const float STREAK_SWEEP_FADE = 0.5f;
        private const float QUARTER_TURN = 90.0f;
        private const int EDGE_SPARKS = 6;
        private const float EDGE_SPARK_SPEED = 9.0f;
        /// <summary>Edge sparks fly at this share of their speed plus up to the jitter share more.</summary>
        private const float EDGE_SPARK_SPEED_MIN = 0.7f;
        private const float EDGE_SPARK_SPEED_JITTER = 0.6f;
        private const float EDGE_SPARK_OUTWARD = 0.75f;
        private const float EDGE_SPARK_TANGENT = 0.55f;
        private const float EDGE_SPARK_LIFT = 2.0f;
        private const float SPARK_SIZE = 0.07f;
        private const float SPARK_SECONDS = 0.4f;
        /// <summary>Spark speeds vary by the golden-ratio sequence of their index (no draw from the game's random).</summary>
        private const float GOLDEN_FRACTION = 0.618034f;
        private const float BLADE_GLINT_REACH = 0.35f;
        private const float BLADE_GLINT_SIZE = 1.1f;
        private const int MAX_HIT_GLINTS = 6;
        private const float HIT_GLINT_SIZE = 0.9f;
        /// <summary>Dawnbreaker whirl size per radius: the swirl's ring lands at 0.8 of the radius.</summary>
        private const float WHIRL_SIZE_PER_RADIUS = 1.9f;
        /// <summary>Afterimage size per radius: its ring starts at 0.76 of the radius and widens 15%.</summary>
        private const float AFTERIMAGE_SIZE_PER_RADIUS = 2.1f;
        private const int SUN_RAYS = 8;
        private const float SUN_RAY_REACH = 0.85f;
        private const float SUN_RAY_HEIGHT = 3.2f;
        private const float SUN_RAY_SHORT_SHARE = 0.7f;
        private const float SUN_RAY_SECONDS = 0.45f;
        private const float SUN_RAY_DRIFT = 1.2f;
        /// <summary>Each swing turns the ray fan by this much, so consecutive sweeps never repeat the same rays.</summary>
        private const float SUN_RAY_TURN = 22.5f;
        private const int MAX_WAVES = 8;
        private const float WAVE_GHOST_INTERVAL = 0.025f;
        /// <summary>Afterimage size per Area: the crescent mesh's blade reaches 2 m from its centre at Area 1.</summary>
        private const float WAVE_GHOST_SIZE = 6.4f;
        private const float WAVE_GHOST_SECONDS = 0.22f;
        private const float WAVE_GHOST_DRIFT = 0.12f;
        /// <summary>Distance from the wave's centre of curvature to the middle of its blade, per Area.</summary>
        private const float WAVE_BLADE_REACH = 1.6f;
        private const float WAVE_GLOW_SIZE = 2.6f;
        private const int WAVE_LAUNCH_SPARKS = 10;
        private const float WAVE_LAUNCH_SPREAD = 70.0f;
        private const float WAVE_LAUNCH_SPARK_SPEED = 13.0f;
        private const float WAVE_LAUNCH_SPEED_MIN = 0.75f;
        private const float WAVE_LAUNCH_SPEED_JITTER = 0.5f;
        private const float WAVE_LAUNCH_LIFT = 1.5f;
        private const float WAVE_LAUNCH_FLASH = 1.6f;
        private const float WAVE_LAUNCH_GLINT = 2.2f;
        private const float WAVE_END_GLINT = 1.6f;

        private static readonly Color STEEL = new Color(1.0f, 0.93f, 0.72f, 1.0f);
        private static readonly Color DAWN = new Color(1.0f, 0.78f, 0.32f, 1.0f);
        private static readonly Color PALE_STEEL = new Color(0.86f, 0.92f, 1.0f, 1.0f);
        /// <summary>The slash's hit stars and edge sparks: a little richer than KnightLooks.GOLD.</summary>
        private static readonly Color BLADE_GOLD = new Color(1.0f, 0.82f, 0.42f, 1.0f);
        private static readonly Color DAWN_LIGHT = new Color(1.0f, 0.88f, 0.6f, 1.0f);
        private static readonly Color SUNRISE = new Color(1.0f, 0.7f, 0.36f, 1.0f);
        private static readonly Vector3 CHEST = Ground.Up(KnightLooks.CHEST_HEIGHT);

        private readonly int[] waves = new int[MAX_WAVES];
        private readonly Vector3[] waveBlades = new Vector3[MAX_WAVES];
        private readonly int crescentVisual;
        private readonly int slashArc;
        private readonly int arcGhost;
        private readonly int steelSparks;
        private readonly int glint;
        private readonly int flash;
        private readonly int whirl;
        private readonly int afterimage;
        private readonly int sunRays;
        private readonly int waveGlow;
        private readonly SweepStyle sweep;
        private readonly FxComposite waveEnd;
        private readonly FxComposite healMotes;
        private int swingCount;
        private float pendingHeal;
        private float healBudget;
        private float waveGhostTimer;
        private float sunRayTurn;

        public OathbladeArc(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            crescentVisual = context.FindVisual("OathCrescent");
            slashArc = effects.Find("KnightSlashArc");
            arcGhost = effects.Find("KnightArcGhost");
            steelSparks = effects.Find("KnightSteelSparks");
            glint = effects.Find("KnightGlint");
            flash = effects.Find("KnightFlash");
            whirl = effects.Find("KnightWhirl");
            afterimage = effects.Find("KnightAfterimage");
            sunRays = effects.Find("KnightSunRays");
            waveGlow = effects.Find("KnightWaveGlow");
            sweep = new SweepStyle
            {
                effect = effects.Find("KnightSweepStreak"),
                degreesPerStreak = STREAK_DEGREES,
                minStreaks = STREAK_MIN,
                maxStreaks = STREAK_MAX,
                outerReach = STREAK_OUTER,
                innerReach = STREAK_INNER,
                speed = STREAK_SPEED,
                seconds = STREAK_SECONDS,
                trailingShare = STREAK_SWEEP_FADE,
            };
            waveEnd = Composite(new FxLayer("KnightMotes", 10), new FxLayer("KnightSteelSparks", 5));
            healMotes = Composite(new FxLayer("KnightMotes", 6));
            Array.Fill(waves, -1);
        }

        protected override bool Cast(in CastInfo cast)
        {
            Vector3 direction = AimFacingNearest(level[WeaponField.Range]);
            int swings = level.GetCount(WeaponField.Count) + amount;
            for (int i = 0; i < swings; i++)
            {
                Schedule(level[WeaponField.Stagger] * i, new WeaponEvent
                {
                    kind = SWING,
                    direction = i % 2 == 0 ? direction : direction * -1.0f,
                    index = i,
                    cast = cast,
                });
            }
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == SWING)
            {
                Swing(e.direction, e.index, e.cast);
            }
        }

        protected override void Update(float dt)
        {
            if (!isEvolved)
            {
                return;
            }
            StepWaves(dt);
            PayHeal(dt);
        }

        private void Swing(Vector3 direction, int index, in CastInfo cast)
        {
            Vector3 origin = heroPosition;
            float radius = AreaOf(level[WeaponField.Radius]);
            float arc = level[WeaponField.Angle];
            float damage = DamageOf(WeaponField.Damage, cast);
            float knockback = KnockbackOf(level[WeaponField.Knockback]);
            int hits = arc >= SweepStreaks.FULL_CIRCLE
                ? strikes.Circle(origin, radius, damage, knockback, slot)
                : strikes.Cone(origin, direction, radius, arc * 0.5f, damage, knockback, slot);
            GlintHits(isEvolved ? DAWN : BLADE_GOLD);
            bool isBackhand = index % 2 == 1;
            RequestAttack(isBackhand ? "SlashDiagonal" : "Slash");
            PlaySwing(origin, direction, radius, arc, isBackhand ? 1.0f : -1.0f);
            PlayAt("oathblade_swing", origin);
            if (hits > 0)
            {
                PlayAt("oathblade_hit", origin);
            }
            if (isEvolved)
            {
                pendingHeal = MathF.Min(HEAL_BANK_LIMIT, pendingHeal + hits * level[WeaponField.Heal]);
                swingCount++;
                int every = Math.Max(1, level.GetCount(WeaponField.Every));
                if (swingCount % every == 0)
                {
                    LaunchCrescent(direction, cast);
                }
            }
        }

        /// <summary>Dawnbreaker: pays the banked heal in whole points, at most HealCap per second, with rising motes and the number.</summary>
        private void PayHeal(float dt)
        {
            float cap = level[WeaponField.HealCap];
            healBudget = MathF.Min(cap, healBudget + cap * dt);
            float heal = MathF.Floor(MathF.Min(pendingHeal, healBudget));
            if (heal < MIN_HEAL)
            {
                return;
            }
            ctx.HealHero(heal, HealSource.Weapon);
            healMotes.Play(heroPosition + CHEST, FxSizes.METRE_UNIT, 1.0f, DAWN);
            pendingHeal -= heal;
            healBudget -= heal;
        }

        /// <summary>A star on each enemy the last strike hit (the first few).</summary>
        private void GlintHits(Color color)
        {
            ReadOnlySpan<int> rows = strikes.lastHits;
            int count = Math.Min(rows.Length, MAX_HIT_GLINTS);
            for (int i = 0; i < count; i++)
            {
                EmitSized(glint, enemies.GetPosition(rows[i]) + CHEST, color, HIT_GLINT_SIZE * fxScale);
            }
        }

        /// <summary>
        /// One swing's look; <paramref name="sweepSign"/> -1 sweeps toward rising angles (left to right), +1 back.
        /// </summary>
        private void PlaySwing(Vector3 origin, Vector3 direction, float radius, float arc, float sweepSign)
        {
            Vector3 center = origin + Ground.Up(SLASH_HEIGHT);
            EmitSized(glint, center + direction * (radius * BLADE_GLINT_REACH), KnightLooks.HOLY_LIGHT, BLADE_GLINT_SIZE * fxScale);
            EmitCrescents(center, direction, radius, arc, isEvolved ? DAWN : STEEL);
            SweepStyle streaks = sweep;
            streaks.width = STREAK_WIDTH * fxScale;
            SweepStreaks.Draw(effects, center, direction, radius, arc, sweepSign, isEvolved ? DAWN_LIGHT : PALE_STEEL, streaks);
            EmitEdgeSparks(center, direction, radius, arc, sweepSign, isEvolved ? SUNRISE : BLADE_GOLD);
            if (isEvolved)
            {
                PlayDawn(origin, center, radius);
            }
        }

        /// <summary>The slash crescents over the arc, each with a short white-hot core.</summary>
        private void EmitCrescents(Vector3 center, Vector3 direction, float radius, float arc, Color color)
        {
            bool isFull = arc >= SweepStreaks.FULL_CIRCLE;
            int copies = isFull ? FULL_SWEEP_ARCS : arc > ARC_TEXTURE_DEGREES + ARC_OVERLAP_DEGREES ? WIDE_SWEEP_ARCS : 1;
            float spacing = isFull ? Ground.FULL_TURN / copies : arc - ARC_TEXTURE_DEGREES;
            float first = -spacing * (copies - 1) * 0.5f;
            float alpha = copies > 1 ? OVERLAP_ALPHA : 1.0f;
            Color body = WithAlpha(color, alpha);
            Color core = WithAlpha(KnightLooks.HOLY_LIGHT, alpha);
            float size = radius * ARC_SIZE_PER_RADIUS;
            for (int k = 0; k < copies; k++)
            {
                Vector3 drift = Ground.Rotate(direction, first + spacing * k) * ARC_DRIFT;
                effects.EmitMoving(slashArc, center, drift, 1, body, size, ARC_SECONDS);
                effects.EmitMoving(slashArc, center, drift, 1, core, size * ARC_CORE_SHARE, ARC_CORE_SECONDS);
            }
        }

        /// <summary>Sparks flung off the blade's edge, outward and along the swing.</summary>
        private void EmitEdgeSparks(Vector3 center, Vector3 direction, float radius, float arc, float sweepSign, Color color)
        {
            int count = (int)MathF.Round(EDGE_SPARKS * fxScale);
            Vector3 lift = Ground.Up(EDGE_SPARK_LIFT);
            for (int k = 0; k < count; k++)
            {
                Vector3 outward = Ground.Rotate(direction, arc * ((k + 0.5f) / count - 0.5f));
                Vector3 along = Ground.Rotate(outward, -QUARTER_TURN * sweepSign);
                float speed = EDGE_SPARK_SPEED * (EDGE_SPARK_SPEED_MIN + EDGE_SPARK_SPEED_JITTER * Jitter(k));
                Vector3 velocity = (outward * EDGE_SPARK_OUTWARD + along * EDGE_SPARK_TANGENT) * speed + lift;
                effects.EmitMoving(steelSparks, center + outward * radius, velocity, 1, color, SPARK_SIZE, SPARK_SECONDS);
            }
        }

        /// <summary>Dawnbreaker: the spinning whirl, the late golden afterimage and a fan of sunrise rays.</summary>
        private void PlayDawn(Vector3 origin, Vector3 center, float radius)
        {
            effects.EmitSized(whirl, center, 1, DAWN, radius * WHIRL_SIZE_PER_RADIUS);
            effects.EmitSized(afterimage, center, 1, DAWN, radius * AFTERIMAGE_SIZE_PER_RADIUS);
            sunRayTurn = (sunRayTurn + SUN_RAY_TURN) % Ground.FULL_TURN;
            Vector3 ground = origin + Ground.Up(FxSizes.GROUND_LIFT);
            for (int k = 0; k < SUN_RAYS; k++)
            {
                Vector3 outward = Ground.RingDirection(sunRayTurn, k, SUN_RAYS);
                float height = SUN_RAY_HEIGHT * (k % 2 == 0 ? 1.0f : SUN_RAY_SHORT_SHARE);
                effects.EmitMoving(sunRays, ground + outward * (radius * SUN_RAY_REACH), outward * SUN_RAY_DRIFT, 1, height, SUN_RAY_SECONDS);
            }
        }

        private void LaunchCrescent(Vector3 direction, in CastInfo cast)
        {
            float speed = SpeedOf(level[WeaponField.SubSpeed]);
            Vector3 origin = heroPosition + direction * CRESCENT_FORWARD + Ground.Up(CRESCENT_HEIGHT);
            ProjectileSpawn spawn = NewProjectile(crescentVisual, ProjectileMotion.Linear, origin, direction * speed, level[WeaponField.SubRange] / speed);
            spawn.damage = DamageOf(WeaponField.SubDamage, cast);
            spawn.radius = level[WeaponField.Width] * 0.5f;
            spawn.scale = stats[StatId.Area];
            spawn.knockback = KnockbackOf(CRESCENT_KNOCKBACK);
            TrackWave(Spawn(spawn));
            PlayWaveLaunch(origin, direction);
            PlayAt("dawn_crescent", origin);
        }

        /// <summary>The wave's launch: a flash and a glint at the blade, sparks fanned out ahead.</summary>
        private void PlayWaveLaunch(Vector3 origin, Vector3 direction)
        {
            EmitSized(flash, origin, DAWN, WAVE_LAUNCH_FLASH);
            EmitSized(glint, origin, KnightLooks.HOLY_LIGHT, WAVE_LAUNCH_GLINT);
            Vector3 lift = Ground.Up(WAVE_LAUNCH_LIFT);
            for (int k = 0; k < WAVE_LAUNCH_SPARKS; k++)
            {
                Vector3 along = Ground.Rotate(direction, WAVE_LAUNCH_SPREAD * ((k + 0.5f) / WAVE_LAUNCH_SPARKS - 0.5f));
                float speed = WAVE_LAUNCH_SPARK_SPEED * (WAVE_LAUNCH_SPEED_MIN + WAVE_LAUNCH_SPEED_JITTER * Jitter(k));
                effects.EmitMoving(steelSparks, origin, along * speed + lift, 1, SUNRISE, SPARK_SIZE, SPARK_SECONDS);
            }
        }

        /// <summary>Follows a new crescent wave for its afterimages; a reused row ends the wave it held first.</summary>
        private void TrackWave(int row)
        {
            if (row < 0)
            {
                return;
            }
            int free = -1;
            for (int i = 0; i < MAX_WAVES; i++)
            {
                if (waves[i] == row)
                {
                    DissolveWave(waveBlades[i]);
                    waves[i] = -1;
                }
                if (waves[i] < 0 && free < 0)
                {
                    free = i;
                }
            }
            if (free >= 0)
            {
                waves[free] = row;
                waveBlades[free] = projectiles.Get(row).position;
            }
        }

        /// <summary>Golden afterimages turned along each wave's flight and a light at its blade; ended waves dissolve.</summary>
        private void StepWaves(float dt)
        {
            waveGhostTimer -= dt;
            bool isGhostDue = waveGhostTimer <= 0.0f;
            if (isGhostDue)
            {
                waveGhostTimer = WAVE_GHOST_INTERVAL;
            }
            for (int i = 0; i < MAX_WAVES; i++)
            {
                if (waves[i] < 0)
                {
                    continue;
                }
                if (!IsOwned(waves[i], null, crescentVisual))
                {
                    DissolveWave(waveBlades[i]);
                    waves[i] = -1;
                    continue;
                }
                ref ProjectileSpawn wave = ref projectiles.Get(waves[i]);
                waveBlades[i] = wave.position + FlightOf(wave) * (WAVE_BLADE_REACH * wave.scale);
                if (isGhostDue)
                {
                    effects.EmitMoving(arcGhost, wave.position, wave.velocity * WAVE_GHOST_DRIFT, 1, DAWN, WAVE_GHOST_SIZE * wave.scale, WAVE_GHOST_SECONDS);
                    EmitSized(waveGlow, waveBlades[i], DAWN, WAVE_GLOW_SIZE * wave.scale);
                }
            }
        }

        private void DissolveWave(Vector3 blade)
        {
            waveEnd.Play(blade, FxSizes.METRE_UNIT, fxScale, DAWN);
            EmitSized(glint, blade, DAWN, WAVE_END_GLINT);
        }

        private static float Jitter(int index) => index * GOLDEN_FRACTION % 1.0f;
    }
}

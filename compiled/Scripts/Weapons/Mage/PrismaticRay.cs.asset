using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Prismatic Ray (Mage, Rare): Count + Amount beams of light, evenly spaced, sweep around the mage: one full circle
    /// over Duration (the sweep), further with the Duration stat. Every Interval each beam hits the enemies it swept
    /// over since the last tick (each once per pass, Range long after Area) for Damage, burning them (Burn per BurnTick
    /// for SubDuration) and chilling them (-Slow speed for SubDuration). The ray owns the Mage's thermal shock
    /// (<see cref="ThermalShock"/>): SubDamage in SubRadius, at most once per SubInterval per enemy.
    /// Evolution Aurora Lance (+ Tome of Insight): more beams in a double helix, two counter-turning sets, always on
    /// (one turn per <see cref="AURORA_TURN_SECONDS"/> s, faster with Cooldown); thermal shock deals double.
    /// Look: each beam is a white-hot core over a soft glow, with a warm fringe on its leading side and a cold one on its
    /// trailing side spreading toward the tip like light through a prism; glitter in every colour sheds along it, a
    /// flare at its tip lights the ground, a faint streak of light fades on the ground behind the sweep, and enemies it
    /// crosses glint. Aurora Lance turns green-teal and rose-violet, with two strands of motes twisting around every
    /// beam and aurora curtains standing at the tips. Echo casts sweep in violet.
    /// </summary>
    public sealed class PrismaticRay : Weapon
    {
        private const int MAX_SWEEPS = 4;
        private const int MAX_BEAMS = 12;
        private const int AURORA_SETS = 2;
        private const int QUERY_CAPACITY = 1024;
        private const int MAX_GLINTS_PER_TICK = 16;
        private const float SWEEP_FLOOR = 0.1f;
        private const float TICK_FLOOR = 0.05f;
        /// <summary>Seconds per turn of Aurora Lance's beams (before the Cooldown stat).</summary>
        private const float AURORA_TURN_SECONDS = 2.4f;
        // Beam look.
        /// <summary>The beam emitters' stretch_speed_scale: a beam's length (m) is its speed (m/s) x this.</summary>
        private const float STRETCH = 20.0f;
        private const float BEAM_HEIGHT = 1.1f;
        /// <summary>Beams leave the prism this far from the mage's centre, so her body stays clear.</summary>
        private const float BEAM_START = 0.45f;
        private const float BEAM_SECONDS = 0.035f;
        private const float BEAM_GROW_SECONDS = 0.15f;
        private const float BEAM_SHRINK_SECONDS = 0.12f;
        private const float CORE_WIDTH = 0.2f;
        private const float GLOW_SHARE = 0.6f;
        private const float FRINGE_WIDTH = 0.24f;
        /// <summary>How far (m) the fringes spread from the core at the beam's tip.</summary>
        private const float FRINGE_SPREAD = 0.8f;
        private const float TIP_SIZE = 0.9f;
        /// <summary>The prism at the mage's hands the beams leave from.</summary>
        private const float SOURCE_SIZE = 0.75f;
        private const float GLINT_SIZE = 0.55f;
        private const float GLINT_HEIGHT_SHARE = 0.7f;
        private const float TRACE_SECONDS = 0.15f;
        private const float TRACE_HEIGHT = 0.05f;
        private const float TRACE_DRIFT = 0.05f;
        private const float IGNITE_SIZE = 2.0f;
        private const float FADE_SIZE = 1.4f;
        private const float CAST_VOLUME = 0.8f;
        private const float BLINK_PITCH = 1.25f;
        private const float WHOOSH_PITCH = 0.9f;
        // Aurora Lance look.
        private const int HELIX_STEPS = 3;
        private const float HELIX_RADIUS = 0.32f;
        private const float HELIX_TURNS = 2.5f;
        private const float HELIX_SPEED = 0.9f;
        private const float HELIX_SPIN = 9.0f;
        private const float MOTE_SIZE = 0.2f;
        private const float VEIL_HEIGHT = 2.2f;

        private static readonly Color CORE = new Color(1.0f, 0.97f, 0.88f, 1.0f);
        private static readonly Color GLOW = new Color(1.0f, 0.82f, 0.55f, 1.0f);
        private static readonly Color WARM = new Color(1.0f, 0.42f, 0.14f, 1.0f);
        private static readonly Color COLD = new Color(0.35f, 0.8f, 1.0f, 1.0f);
        private static readonly Color ECHO_CORE = new Color(0.9f, 0.82f, 1.0f, 1.0f);
        private static readonly Color AURORA_GREEN = new Color(0.4f, 1.0f, 0.7f, 1.0f);
        private static readonly Color AURORA_TEAL = new Color(0.3f, 0.85f, 1.0f, 1.0f);
        private static readonly Color AURORA_ROSE = new Color(1.0f, 0.45f, 0.75f, 1.0f);
        private static readonly Color AURORA_VIOLET = new Color(0.7f, 0.45f, 1.0f, 1.0f);
        private static readonly Color[] SPECTRUM =
        {
            new Color(1.0f, 0.4f, 0.55f, 1.0f),
            new Color(1.0f, 0.6f, 0.25f, 1.0f),
            new Color(1.0f, 0.88f, 0.4f, 1.0f),
            new Color(0.5f, 1.0f, 0.6f, 1.0f),
            new Color(0.4f, 0.88f, 1.0f, 1.0f),
            new Color(0.7f, 0.5f, 1.0f, 1.0f),
        };

        /// <summary>Beams turning together: one cast's (or echo's) sweep, or one of Aurora Lance's two sets.</summary>
        private struct Sweep
        {
            public bool isActive;
            public bool isPermanent;
            public float startTime;
            public float startYaw;
            /// <summary>+1 turns clockwise seen from above, -1 the other way.</summary>
            public float turnSign;
            public int beams;
            /// <summary>Degrees turned so far, and up to the last damage tick.</summary>
            public float turned;
            public float ticked;
            /// <summary>Degrees the sweep turns in all (finite sweeps).</summary>
            public float total;
            public float endTime;
            public int set;
            public CastInfo cast;
        }

        private readonly Sweep[] sweeps = new Sweep[MAX_SWEEPS];
        private readonly int[] rows = new int[QUERY_CAPACITY];
        private readonly ThermalShock shock;
        private readonly int core;
        private readonly int glow;
        private readonly int fringe;
        private readonly int sparkle;
        private readonly int tip;
        private readonly int prism;
        private readonly int trace;
        private readonly int glint;
        private readonly int prismFlash;
        private readonly int mote;
        private readonly int veil;
        private float tickTimer;
        private int sparkleCursor;
        private int auroraBeams;

        public PrismaticRay(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            shock = new ThermalShock(context, slotIndex);
            core = effects.Find("MagePrismCore");
            glow = effects.Find("MagePrismGlow");
            fringe = effects.Find("MagePrismFringe");
            sparkle = effects.Find("MagePrismSparkle");
            tip = effects.Find("MagePrismTip");
            prism = effects.Find("MagePrismSource");
            trace = effects.Find("MagePrismTrace");
            glint = effects.Find("MagePrismGlint");
            prismFlash = effects.Find("MagePrismFlash");
            mote = effects.Find("MageAuroraMote");
            veil = effects.Find("MageAuroraVeil");
        }

        /// <summary>Aurora Lance never rests: its beams turn every frame instead of casting on a cooldown.</summary>
        protected override bool usesCooldown => !isEvolved;

        private int beamCount => level.GetCount(WeaponField.Count) + amount;

        public override void OnEnemyDamaged(int row, int source, float damage, bool isCritical) => shock.OnEnemyDamaged(row, source, clock);

        protected override bool Cast(in CastInfo cast)
        {
            if (!TryNearest(BeamLength(), out _))
            {
                return false;
            }
            int beams = Math.Min(beamCount, MAX_BEAMS);
            float baseSweep = MathF.Max(level[WeaponField.Duration], SWEEP_FLOOR);
            float seconds = DurationOf(baseSweep);
            float startYaw = Ground.ToYaw(AimFacingNearest(BeamLength()));
            StartSweep(new Sweep
            {
                startYaw = startYaw,
                turnSign = Random.Float() < 0.5f ? -1.0f : 1.0f,
                beams = beams,
                total = Ground.FULL_TURN * seconds / baseSweep,
                endTime = clock + seconds,
                cast = cast,
            });
            EmitSized(prismFlash, BeamOrigin(), Tint(cast, GLOW), IGNITE_SIZE * fxScale);
            PlayAt("blink", heroPosition, CAST_VOLUME, BLINK_PITCH);
            PlayAt("ember_whoosh", heroPosition, CAST_VOLUME, WHOOSH_PITCH);
            RequestAttack("CastLong");
            return true;
        }

        /// <summary>Aurora Lance: two counter-turning sets share the beams (Count + Amount); rebuilt when that changes.</summary>
        private void EnsureAurora()
        {
            int beams = Math.Min(beamCount, MAX_BEAMS);
            if (auroraBeams == beams)
            {
                return;
            }
            auroraBeams = beams;
            for (int i = 0; i < MAX_SWEEPS; i++)
            {
                sweeps[i].isActive = false;
            }
            for (int set = 0; set < AURORA_SETS; set++)
            {
                StartSweep(new Sweep
                {
                    isPermanent = true,
                    startYaw = set * Ground.FULL_TURN / beams,
                    turnSign = set == 0 ? 1.0f : -1.0f,
                    beams = (beams + 1 - set) / AURORA_SETS,
                    set = set,
                    cast = CastInfo.normal,
                });
            }
            EmitSized(prismFlash, BeamOrigin(), AURORA_GREEN, IGNITE_SIZE * fxScale);
        }

        protected override void Update(float dt)
        {
            if (!hero.isAlive)
            {
                return;
            }
            if (isEvolved)
            {
                EnsureAurora();
            }
            TurnSweeps(dt);
            tickTimer -= dt;
            if (tickTimer <= 0.0f)
            {
                tickTimer += MathF.Max(level[WeaponField.Interval], TICK_FLOOR);
                TickBeams();
            }
            shock.Update(dt, clock, heroPosition, GetShockSettings());
            DrawSweeps();
        }

        /// <summary>Thermal shock's numbers from this level: SubDamage (after Might) in SubRadius (Area), once per SubInterval.</summary>
        private ShockSettings GetShockSettings()
        {
            return new ShockSettings
            {
                damage = MightOf(level[WeaponField.SubDamage]),
                radius = AreaOf(level[WeaponField.SubRadius]),
                restSeconds = level[WeaponField.SubInterval],
                beams = beamCount,
                reach = BeamLength(),
                fxScale = fxScale,
                isAurora = isEvolved,
            };
        }

        // -------------------------------------------------------------------------------------------------------
        // Sweeps
        // -------------------------------------------------------------------------------------------------------

        private void StartSweep(Sweep sweep)
        {
            for (int i = 0; i < MAX_SWEEPS; i++)
            {
                if (!sweeps[i].isActive)
                {
                    sweep.isActive = true;
                    sweep.startTime = clock;
                    sweeps[i] = sweep;
                    return;
                }
            }
        }

        private void TurnSweeps(float dt)
        {
            for (int i = 0; i < MAX_SWEEPS; i++)
            {
                ref Sweep sweep = ref sweeps[i];
                if (!sweep.isActive)
                {
                    continue;
                }
                if (sweep.isPermanent)
                {
                    sweep.turned += Ground.FULL_TURN / (AURORA_TURN_SECONDS * stats[StatId.Cooldown]) * dt;
                    if (sweep.ticked >= Ground.FULL_TURN)
                    {
                        sweep.turned -= Ground.FULL_TURN;
                        sweep.ticked -= Ground.FULL_TURN;
                    }
                    continue;
                }
                sweep.turned = MathF.Min(sweep.total, sweep.turned + Ground.FULL_TURN / MathF.Max(level[WeaponField.Duration], SWEEP_FLOOR) * dt);
            }
        }

        /// <summary>
        /// Every enemy whose centre a beam swept over since the last tick is hit once; finished sweeps end here after
        /// their last stretch is hit.
        /// </summary>
        private void TickBeams()
        {
            if (!HasActiveSweep())
            {
                return;
            }
            Vector3 center = heroPosition;
            int found = enemies.QueryCircle(center, BeamLength(), rows);
            int glints = 0;
            for (int i = 0; i < found; i++)
            {
                int row = rows[i];
                float yaw = Ground.ToYaw(enemies.GetPosition(row) - center);
                for (int s = 0; s < MAX_SWEEPS; s++)
                {
                    ref Sweep sweep = ref sweeps[s];
                    if (sweep.isActive && IsSwept(sweep, yaw))
                    {
                        HitBeam(row, sweep, ref glints);
                    }
                }
            }
            for (int s = 0; s < MAX_SWEEPS; s++)
            {
                ref Sweep sweep = ref sweeps[s];
                if (!sweep.isActive)
                {
                    continue;
                }
                sweep.ticked = sweep.turned;
                if (!sweep.isPermanent && sweep.turned >= sweep.total)
                {
                    sweep.isActive = false;
                    EmitSized(prismFlash, BeamOrigin(), Tint(sweep.cast, GLOW), FADE_SIZE * fxScale);
                }
            }
        }

        /// <summary>Whether any beam of a sweep passed over <paramref name="yaw"/> since its last tick.</summary>
        private static bool IsSwept(in Sweep sweep, float yaw)
        {
            float swept = sweep.turned - sweep.ticked;
            if (swept <= 0.0f)
            {
                return false;
            }
            float spacing = Ground.FULL_TURN / sweep.beams;
            float from = sweep.startYaw + sweep.turnSign * sweep.ticked;
            float ahead = Wrap(sweep.turnSign * (yaw - from));
            return ahead % spacing < swept;
        }

        private void HitBeam(int row, in Sweep sweep, ref int glints)
        {
            Vector3 body = enemies.GetPosition(row) + Ground.Up(enemies.GetHeight(row) * GLINT_HEIGHT_SHARE);
            float seconds = DurationOf(level[WeaponField.SubDuration]);
            var effect = new HitEffect
            {
                burnPerSecond = BurnPerSecondOf(sweep.cast),
                burnSeconds = seconds,
                slowFactor = 1.0f - level[WeaponField.Slow],
                slowSeconds = seconds,
                numberKind = NumberKind(sweep.cast),
            };
            DamageResult result = strikes.Hit(row, DamageOf(WeaponField.Damage, sweep.cast), Vector3.zero, slot, effect);
            if (result == DamageResult.Missed)
            {
                return;
            }
            if (result == DamageResult.Hurt)
            {
                shock.NoteFresh(row);
            }
            if (glints < MAX_GLINTS_PER_TICK)
            {
                glints++;
                EmitSized(glint, body, BeamPalette(sweep, glints % 2 == 0), GLINT_SIZE * fxScale);
            }
        }

        private bool HasActiveSweep()
        {
            for (int i = 0; i < MAX_SWEEPS; i++)
            {
                if (sweeps[i].isActive)
                {
                    return true;
                }
            }
            return false;
        }

        // -------------------------------------------------------------------------------------------------------
        // Look
        // -------------------------------------------------------------------------------------------------------

        private void DrawSweeps()
        {
            bool isDrawn = false;
            for (int s = 0; s < MAX_SWEEPS; s++)
            {
                ref Sweep sweep = ref sweeps[s];
                if (!sweep.isActive)
                {
                    continue;
                }
                isDrawn = true;
                float reach = BeamLength() * BeamExtent(sweep);
                float yaw = sweep.startYaw + sweep.turnSign * sweep.turned;
                for (int b = 0; b < sweep.beams; b++)
                {
                    DrawBeam(Ground.RingDirection(yaw, b, sweep.beams), reach, sweep);
                }
            }
            if (isDrawn)
            {
                EmitSized(prism, BeamOrigin(), CORE, SOURCE_SIZE * fxScale);
            }
        }

        /// <summary>Beams grow out of the prism as a sweep starts and draw back into it as it ends.</summary>
        private float BeamExtent(in Sweep sweep)
        {
            float grow = Math.Clamp((clock - sweep.startTime) / BEAM_GROW_SECONDS, 0.0f, 1.0f);
            if (sweep.isPermanent)
            {
                return grow;
            }
            float shrink = Math.Clamp((sweep.endTime - clock) / BEAM_SHRINK_SECONDS, 0.0f, 1.0f);
            return MathF.Min(grow, shrink);
        }

        private void DrawBeam(Vector3 direction, float reach, in Sweep sweep)
        {
            if (reach <= BEAM_START)
            {
                return;
            }
            Vector3 origin = BeamOrigin();
            Vector3 start = origin + direction * BEAM_START;
            Vector3 end = origin + direction * reach;
            Vector3 lead = Ground.Perpendicular(direction) * sweep.turnSign;
            float spread = FRINGE_SPREAD * fxScale;
            Color glowColor = BeamPalette(sweep, true);
            EmitBeam(core, start, end, sweep.cast.isEcho ? ECHO_CORE : CORE, CORE_WIDTH * fxScale);
            EmitBeam(glow, start, end, glowColor, level[WeaponField.Width] * GLOW_SHARE * fxScale);
            EmitBeam(fringe, start, end + lead * spread, FringeColor(sweep, true), FRINGE_WIDTH * fxScale);
            EmitBeam(fringe, start, end - lead * spread, FringeColor(sweep, false), FRINGE_WIDTH * fxScale);
            EmitSized(tip, end, glowColor, TIP_SIZE * fxScale);
            sparkleCursor = (sparkleCursor + 1) % SPECTRUM.Length;
            Emit(sparkle, Vector3.Lerp(start, end, Random.Float()), 1, sweep.isPermanent ? BeamPalette(sweep, Random.Float() < 0.5f) : SPECTRUM[sparkleCursor]);
            Vector3 floor = Ground.Up(TRACE_HEIGHT - origin.y);
            effects.EmitMoving(trace, (start + end) * 0.5f + floor, direction * TRACE_DRIFT, 1, glowColor, reach - BEAM_START, TRACE_SECONDS);
            if (sweep.isPermanent)
            {
                DrawHelix(start, end, direction, sweep);
                EmitSized(veil, Ground.Flat(end), BeamPalette(sweep, false), VEIL_HEIGHT * fxScale);
            }
        }

        /// <summary>One stretched quad from a to b (its drift speed sets its length).</summary>
        private void EmitBeam(int effect, Vector3 a, Vector3 b, Color color, float width)
        {
            effects.EmitMoving(effect, (a + b) * 0.5f, (b - a) * (1.0f / STRETCH), 1, color, width, BEAM_SECONDS);
        }

        /// <summary>Aurora Lance: two strands of motes twisting around the beam, flowing outward.</summary>
        private void DrawHelix(Vector3 start, Vector3 end, Vector3 direction, in Sweep sweep)
        {
            Vector3 side = Ground.Perpendicular(direction);
            float flow = clock * HELIX_SPEED;
            for (int k = 0; k < HELIX_STEPS; k++)
            {
                float along = (k + flow - MathF.Floor(flow)) / HELIX_STEPS;
                float angle = along * HELIX_TURNS * MathF.PI * 2.0f + clock * HELIX_SPIN;
                Vector3 axis = Vector3.Lerp(start, end, along);
                Vector3 offset = (side * MathF.Cos(angle) + Vector3.up * MathF.Sin(angle)) * (HELIX_RADIUS * fxScale);
                EmitSized(mote, axis + offset, BeamPalette(sweep, true), MOTE_SIZE * fxScale);
                EmitSized(mote, axis - offset, BeamPalette(sweep, false), MOTE_SIZE * fxScale);
            }
        }

        /// <summary>The beam's glow colour: gold (violet for an echo); Aurora Lance's sets are green-teal and rose-violet.</summary>
        private static Color BeamPalette(in Sweep sweep, bool isFirst)
        {
            if (sweep.isPermanent)
            {
                return sweep.set == 0 ? (isFirst ? AURORA_GREEN : AURORA_TEAL) : (isFirst ? AURORA_ROSE : AURORA_VIOLET);
            }
            return sweep.cast.isEcho ? ECHO_TINT : isFirst ? GLOW : CORE;
        }

        /// <summary>The leading fringe burns warm, the trailing one chills cold (echo: violet and pale violet).</summary>
        private static Color FringeColor(in Sweep sweep, bool isLeading)
        {
            if (sweep.isPermanent)
            {
                return BeamPalette(sweep, isLeading);
            }
            if (sweep.cast.isEcho)
            {
                return isLeading ? ECHO_TINT : ECHO_CORE;
            }
            return isLeading ? WARM : COLD;
        }

        private float BeamLength() => AreaOf(level[WeaponField.Range]);

        private Vector3 BeamOrigin() => heroPosition + Ground.Up(BEAM_HEIGHT);

        /// <summary>An angle in degrees wrapped into [0, 360).</summary>
        private static float Wrap(float degrees)
        {
            float wrapped = degrees % Ground.FULL_TURN;
            return wrapped < 0.0f ? wrapped + Ground.FULL_TURN : wrapped;
        }
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Ravager Chop (Barbarian, Common): a two-handed chop in a cone (Angle, Radius long after Area) toward the nearest
    /// enemy within Range that hits everything in it with huge Knockback; extra chops (Count + Amount) follow Stagger
    /// apart; from rank 8 the hits stun (Stun).
    /// Evolution Skullsplitter (+ Whetstone): each chop also splits the ground in a fissure SubRange long (Area), Width
    /// wide, dealing SubDamage; its kills grant FuryPerKill Fury instead of a kill's one.
    /// Look: a heavy steel arc sweeping the cone (alternate chops sweep back) with streaks along the swing, and where
    /// the axe lands a flash, stone sparks, clods, rocks, dust, a shockwave and a crack. Full Fury turns it blood red.
    /// Skullsplitter doubles the arc with a crimson outer edge, cracks the ground in crimson and runs a glowing crimson
    /// fissure of rock spikes, rubble and blood spurts that bursts at its end.
    /// </summary>
    public sealed class RavagerChop : Weapon
    {
        private const int CHOP = 0;
        private const int FISSURE = 1;
        private const int FISSURE_END = 2;
        private const float CHOP_TRAUMA = 0.05f;
        private const float FISSURE_SEGMENT = 1.0f;
        private const float FISSURE_STEP_SECONDS = 0.02f;
        private const float FISSURE_KNOCKBACK = 0.6f;
        private const int FISSURE_SEGMENTS_MAX = 10;

        /// <summary>Waist height of the axe's arc.</summary>
        private const float ARC_HEIGHT = 0.9f;
        /// <summary>Radius of the arc in its texture per metre of quad (BarbarianChopArc*: slash_04).</summary>
        private const float ARC_TEXTURE_RADIUS = 0.30f;
        /// <summary>Radius of the crescent in its texture per metre of quad (BarbarianChopSmear: slash_02).</summary>
        private const float SMEAR_TEXTURE_RADIUS = 0.33f;
        private const float ARC_RADIUS_SHARE = 0.75f;
        /// <summary>Skullsplitter's crimson outer edge, just outside the bone-white arc.</summary>
        private const float EDGE_RADIUS_SHARE = 0.86f;
        private const float SMEAR_RADIUS_SHARE = 0.58f;
        /// <summary>The arc emitters' own lifetime: their spin is authored for it.</summary>
        private const float ARC_SECONDS = 0.2f;
        private const float SMEAR_SECONDS = 0.3f;
        private const float STREAK_DEGREES = 12.0f;
        private const int STREAK_MIN = 3;
        private const int STREAK_MAX = 14;
        /// <summary>The streaks run along the arc itself.</summary>
        private const float STREAK_REACH = 1.0f;
        private const float STREAK_SPEED = 7.0f;
        private const float STREAK_SIZE = 0.75f;
        private const float STREAK_SECONDS = 0.2f;
        /// <summary>The trailing streak of a sweep fades at this share of the leading one's lifetime.</summary>
        private const float STREAK_SWEEP_FADE = 0.55f;
        /// <summary>Where the axe lands, as a share of the cone length.</summary>
        private const float IMPACT_SHARE = 0.6f;
        private const float IMPACT_HEIGHT = 0.35f;
        private const float CRACK_SHARE = 0.55f;
        private const float CLEAVE_CRACK_SHARE = 0.8f;
        private const float CRACK_SECONDS = 1.6f;
        private const float CLEAVE_GLOW_SECONDS = 1.1f;
        /// <summary>Fissure lines are this much longer than their segment so neighbours overlap.</summary>
        private const float FISSURE_LINE_SHARE = 1.5f;
        private const float FISSURE_LINE_SECONDS = 1.6f;
        private const float FISSURE_GLOW_SECONDS = 0.9f;
        private const float RUBBLE_SHARE = 1.2f;
        /// <summary>Spike height per metre of fissure width, and the random spread of heights and sideways offsets.</summary>
        private const float SPIKE_HEIGHT_SHARE = 1.2f;
        private const float SPIKE_HEIGHT_MIN = 0.8f;
        private const float SPIKE_HEIGHT_MAX = 1.2f;
        private const float SPIKE_SIDE_SHARE = 0.6f;
        private const int END_SPIKES = 3;
        private const float END_SPIKE_SCALE = 1.3f;

        /// <summary>Full Fury on the axe: a shade deeper than BarbarianLooks.FURY_RED.</summary>
        private static readonly Color FURY_BLADE = new Color(1.0f, 0.1f, 0.06f, 1.0f);
        private static readonly Color BONE = new Color(1.0f, 0.93f, 0.82f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.LOW_GROUND_LIFT);

        /// <summary>One hit memory per chop of a cast (chop fissures overlap in time).</summary>
        private readonly HashSet<Enemy>[] fissureMemories = { new HashSet<Enemy>(), new HashSet<Enemy>() };
        /// <summary>The Barbarian's kit (Fury), or null for another class.</summary>
        private readonly BarbarianKit barbarian;
        private readonly int arcClockwise;
        private readonly int arcCounter;
        private readonly int smear;
        private readonly int crack;
        private readonly int crackGlow;
        private readonly int fissureLine;
        private readonly int fissureGlow;
        private readonly int rubble;
        private readonly int spike;
        private readonly SweepStyle sweep;
        private readonly FxComposite chopImpact;
        private readonly FxComposite cleaveImpact;
        private readonly FxComposite splitBurst;
        private readonly FxComposite splitEnd;
        /// <summary>True while a fissure segment deals its damage: the kills it makes (reported inside that call) are its own.</summary>
        private bool isSplitting;

        public RavagerChop(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            barbarian = context.KitAs<BarbarianKit>();
            arcClockwise = effects.Find("BarbarianChopArcCw");
            arcCounter = effects.Find("BarbarianChopArcCcw");
            smear = effects.Find("BarbarianChopSmear");
            crack = effects.Find("BarbarianCrack");
            crackGlow = effects.Find("BarbarianCrackGlow");
            fissureLine = effects.Find("BarbarianFissure");
            fissureGlow = effects.Find("BarbarianFissureGlow");
            rubble = effects.Find("BarbarianRubble");
            spike = effects.Find("BarbarianRockSpike");
            sweep = new SweepStyle
            {
                effect = effects.Find("BarbarianSweepStreak"),
                degreesPerStreak = STREAK_DEGREES,
                minStreaks = STREAK_MIN,
                maxStreaks = STREAK_MAX,
                outerReach = STREAK_REACH,
                innerReach = STREAK_REACH,
                speed = STREAK_SPEED,
                width = STREAK_SIZE,
                seconds = STREAK_SECONDS,
                trailingShare = STREAK_SWEEP_FADE,
            };
            // Size unit = cone length.
            chopImpact = Composite(new FxLayer("BarbarianFlash", 1, 0.5f), new FxLayer("BarbarianGrit", 10),
                                   new FxLayer("BarbarianDirtSpray", 4, 0.0f, false), new FxLayer("BarbarianRocks", 5, 0.0f, false),
                                   new FxLayer("BarbarianDust", 4, 0.28f, false), FxLayer.Ground("BarbarianShockRing", 0.85f, false),
                                   FxLayer.Ground("BarbarianDustRing", 0.9f, false));
            cleaveImpact = Composite(new FxLayer("BarbarianFlash", 1, 0.6f), new FxLayer("BarbarianGrit", 12), new FxLayer("BarbarianMagma", 6),
                                     new FxLayer("BarbarianDirtSpray", 4, 0.0f, false), new FxLayer("BarbarianRocks", 5, 0.0f, false),
                                     new FxLayer("BarbarianBoulders", 1, 0.0f, false), new FxLayer("BarbarianDust", 4, 0.3f, false),
                                     FxLayer.Ground("BarbarianShockRing", 1.0f, false), FxLayer.Ground("BarbarianDustRing", 1.1f, false));
            // Size unit = 1 m.
            splitBurst = Composite(new FxLayer("BarbarianMagma", 2), new FxLayer("BarbarianDirtSpray", 1, 0.0f, false),
                                   new FxLayer("BarbarianDust", 1, 0.55f, false));
            splitEnd = Composite(new FxLayer("BarbarianFlash", 1, 1.4f), new FxLayer("BarbarianMagma", 4), new FxLayer("BarbarianGrit", 6),
                                 new FxLayer("BarbarianRocks", 4, 0.0f, false), new FxLayer("BarbarianBoulders", 1, 0.0f, false),
                                 new FxLayer("BarbarianDust", 2, 0.6f, false), FxLayer.Ground("BarbarianShockRing", 2.4f, false));
        }

        private bool isFuryFull => barbarian != null && barbarian.isFuryFull;

        /// <summary>Skullsplitter: Fury one of its fissure kills grants.</summary>
        private float furyPerKill => level[WeaponField.FuryPerKill];

        protected override bool Cast(in CastInfo cast)
        {
            int chops = level.GetCount(WeaponField.Count) + amount;
            for (int i = 0; i < chops; i++)
            {
                Schedule(level[WeaponField.Stagger] * i, new WeaponEvent { kind = CHOP, index = i, cast = cast });
            }
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == CHOP)
            {
                Chop(e.index, e.cast);
            }
            else if (e.kind == FISSURE)
            {
                Split(e);
            }
            else if (e.kind == FISSURE_END)
            {
                BurstFissureEnd(e.position, e.direction, e.radius);
            }
        }

        /// <summary>Skullsplitter: a fissure kill grants its extra Fury on top of the kill's own.</summary>
        public override void OnEnemyKilled(in EnemyDeath death)
        {
            if (isSplitting && death.source == slot && barbarian != null)
            {
                barbarian.AddKillFury(furyPerKill - BarbarianKit.FURY_PER_KILL);
            }
        }

        private void Chop(int index, in CastInfo cast)
        {
            Vector3 origin = heroPosition;
            Vector3 direction = AimFacingNearest(level[WeaponField.Range]);
            float length = AreaOf(level[WeaponField.Radius]);
            float cone = level[WeaponField.Angle];
            var effect = new HitEffect { stunSeconds = level[WeaponField.Stun] };
            int hits = strikes.Cone(origin, direction, length, cone * 0.5f, DamageOf(WeaponField.Damage, cast), KnockbackOf(level[WeaponField.Knockback]), slot, effect);
            bool isSecond = index % 2 == 1;
            RequestAttack(isSecond ? "DualChop" : "HeavyChop");
            DrawSwing(origin, direction, length, cone, isSecond ? 1.0f : -1.0f);
            DrawLanding(origin + direction * (length * IMPACT_SHARE), direction, length);
            PlayAt("ravager_chop", origin);
            if (hits > 0)
            {
                PlayAt("ravager_hit", origin);
            }
            ctx.feel.Shake(CHOP_TRAUMA);
            if (isEvolved)
            {
                StartFissure(origin, direction, index % fissureMemories.Length, cast);
            }
        }

        /// <summary>
        /// The axe's arc over the cone: a sweeping arc (sweepSign -1 turns like Ground.Rotate, +1 back), a soft smear
        /// inside it and steel streaks along the swing. Skullsplitter adds a crimson outer edge.
        /// </summary>
        private void DrawSwing(Vector3 origin, Vector3 direction, float length, float cone, float sweepSign)
        {
            Vector3 center = origin + Ground.Up(ARC_HEIGHT);
            Vector3 heading = direction * FxSizes.HEADING_SPEED;
            int arc = sweepSign < 0.0f ? arcClockwise : arcCounter;
            bool isFury = isFuryFull;
            Color edge = isFury ? FURY_BLADE : isEvolved ? BONE : BarbarianLooks.STEEL;
            effects.EmitMoving(arc, center, heading, 1, edge, length * ARC_RADIUS_SHARE / ARC_TEXTURE_RADIUS, ARC_SECONDS);
            if (isEvolved)
            {
                effects.EmitMoving(arc, center, heading, 1, isFury ? FURY_BLADE : BarbarianLooks.CRIMSON, length * EDGE_RADIUS_SHARE / ARC_TEXTURE_RADIUS,
                                   ARC_SECONDS);
            }
            effects.EmitMoving(smear, center, heading, 1, isEvolved && !isFury ? BarbarianLooks.CRIMSON : edge,
                               length * SMEAR_RADIUS_SHARE / SMEAR_TEXTURE_RADIUS, SMEAR_SECONDS);
            SweepStreaks.Draw(effects, center, direction, length * ARC_RADIUS_SHARE, cone, sweepSign, edge, sweep);
        }

        /// <summary>Where the axe bites the ground: the impact burst and a crack (Skullsplitter's glows crimson).</summary>
        private void DrawLanding(Vector3 impact, Vector3 direction, float length)
        {
            Vector3 ground = Ground.Flat(impact) + LIFT;
            Vector3 heading = direction * FxSizes.HEADING_SPEED;
            Color tint = isFuryFull ? FURY_BLADE : isEvolved ? BarbarianLooks.CRIMSON : BarbarianLooks.STEEL;
            (isEvolved ? cleaveImpact : chopImpact).Play(ground + Ground.Up(IMPACT_HEIGHT), length, fxScale, tint);
            float crackSize = length * (isEvolved ? CLEAVE_CRACK_SHARE : CRACK_SHARE);
            effects.EmitMoving(crack, ground, heading, 1, WHITE, crackSize, CRACK_SECONDS);
            if (isEvolved)
            {
                effects.EmitMoving(crackGlow, ground, heading, 1, tint, crackSize, CLEAVE_GLOW_SECONDS);
            }
        }

        /// <summary>One segment of a chop's fissure: from <paramref name="start"/>, <paramref name="step"/> m along <paramref name="direction"/>.</summary>
        private static WeaponEvent FissureEvent(Vector3 start, Vector3 direction, float step, float halfWidth, int memory, in CastInfo cast)
        {
            return new WeaponEvent
            {
                kind = FISSURE,
                position = start,
                direction = direction,
                value = step,
                radius = halfWidth,
                index = memory,
                cast = cast,
            };
        }

        private void StartFissure(Vector3 origin, Vector3 direction, int memory, in CastInfo cast)
        {
            fissureMemories[memory].Clear();
            float length = AreaOf(level[WeaponField.SubRange]);
            int segments = Math.Clamp((int)MathF.Round(length / FISSURE_SEGMENT), 1, FISSURE_SEGMENTS_MAX);
            float step = length / segments;
            float halfWidth = AreaOf(level[WeaponField.Width]) * 0.5f;
            for (int j = 0; j < segments; j++)
            {
                Schedule(FISSURE_STEP_SECONDS * j, FissureEvent(origin + direction * (step * j), direction, step, halfWidth, memory, cast));
            }
            Schedule(FISSURE_STEP_SECONDS * segments, new WeaponEvent
            {
                kind = FISSURE_END,
                position = origin + direction * length,
                direction = direction,
                radius = halfWidth,
            });
            PlayAt("skullsplitter_crack", origin);
        }

        /// <summary>One segment of the fissure; its kills feed Fury (see <see cref="OnEnemyKilled"/>).</summary>
        private void Split(in WeaponEvent e)
        {
            Vector3 a = e.position;
            Vector3 b = a + e.direction * e.value;
            isSplitting = true;
            strikes.Capsule(a, b, e.radius, DamageOf(WeaponField.SubDamage, e.cast), KnockbackOf(FISSURE_KNOCKBACK), slot, default,
                            PushMode.Sideways, default, fissureMemories[e.index]);
            isSplitting = false;
            DrawSplit(a, b, e.direction, e.value, e.radius);
        }

        /// <summary>The fissure opening: a dark crack with a crimson seam, rubble, a rock spike and blood spurts.</summary>
        private void DrawSplit(Vector3 a, Vector3 b, Vector3 direction, float step, float halfWidth)
        {
            Vector3 middle = Ground.Flat((a + b) * 0.5f) + LIFT;
            Vector3 heading = direction * FxSizes.HEADING_SPEED;
            effects.EmitMoving(fissureLine, middle, heading, 1, WHITE, step * FISSURE_LINE_SHARE, FISSURE_LINE_SECONDS);
            effects.EmitMoving(fissureGlow, middle, heading, 1, BarbarianLooks.CRIMSON, step * FISSURE_LINE_SHARE, FISSURE_GLOW_SECONDS);
            effects.EmitMoving(rubble, middle, heading, 1, WHITE, step * RUBBLE_SHARE, FISSURE_LINE_SECONDS);
            Vector3 side = Ground.Perpendicular(direction) * (halfWidth * SPIKE_SIDE_SHARE * Random.Range(-1.0f, 1.0f));
            Vector3 root = Ground.Flat(Vector3.Lerp(a, b, Random.Float())) + side;
            float height = halfWidth * 2.0f * SPIKE_HEIGHT_SHARE * Random.Range(SPIKE_HEIGHT_MIN, SPIKE_HEIGHT_MAX);
            EmitSized(spike, root, WHITE, height);
            splitBurst.Play(middle, FxSizes.METRE_UNIT, 1.0f, BarbarianLooks.CRIMSON);
        }

        /// <summary>The fissure's far end bursts: crimson flash, blood spurts, a cluster of spikes, rocks and a shockwave.</summary>
        private void BurstFissureEnd(Vector3 end, Vector3 direction, float halfWidth)
        {
            Vector3 ground = Ground.Flat(end) + LIFT;
            splitEnd.Play(ground + Ground.Up(IMPACT_HEIGHT), FxSizes.METRE_UNIT, fxScale, BarbarianLooks.CRIMSON);
            Vector3 across = Ground.Perpendicular(direction);
            for (int k = 0; k < END_SPIKES; k++)
            {
                float offset = (k - (END_SPIKES - 1) * 0.5f) * halfWidth;
                float height = halfWidth * 2.0f * SPIKE_HEIGHT_SHARE * END_SPIKE_SCALE * Random.Range(SPIKE_HEIGHT_MIN, SPIKE_HEIGHT_MAX);
                EmitSized(spike, ground + across * offset, WHITE, height);
            }
        }
    }
}

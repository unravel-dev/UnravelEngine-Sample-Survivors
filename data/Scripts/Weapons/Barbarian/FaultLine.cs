using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Fault Line (Barbarian, Rare): a stomp sends one fissure (Count + Amount) toward each of the nearest enemies within
    /// Range (spares spread around); rock spikes erupt along each line, Radius long and Width wide (Area), over
    /// Interval, hitting each enemy once per stomp for Damage, popping it up and stunning it (Stun, Duration stat).
    /// Evolution Tectonic Rift (+ Plate Pauldron): the fissures form a star aimed at the nearest enemy and leave lava
    /// cracks that burn for SubDuration (Duration stat): SubDamage every SubInterval.
    /// Look: the stomp flashes and throws a shockwave, a dust ring, rocks and a crack around the feet; each fissure tears
    /// open as a dark zigzag groove lined with rubble while chunky rock spikes burst from it and a dust wave rolls out to
    /// both sides. Tectonic Rift stomps with a lava flash and a glowing crack web, spurts magma along every line and
    /// fills it with molten cracks (a dark crust around an orange seam with a white-hot core that cools) whose heat
    /// glows on the ground, bubbling droplets and sending up embers and smoke until the lava cools.
    /// </summary>
    public sealed class FaultLine : Weapon
    {
        private const int SEGMENT = 0;
        private const int LAVA = 1;
        private const int MAX_FISSURES = 10;
        private const float SEGMENT_LENGTH = 1.0f;
        private const int MIN_SEGMENTS = 3;
        /// <summary>Long (Area-scaled) fissures use longer segments instead of more events.</summary>
        private const int MAX_SEGMENTS = 12;
        private const float STOMP_TRAUMA = 0.10f;
        private const float POP_HEIGHT = 0.5f;

        private const float STOMP_HEIGHT = 0.3f;
        private const float STOMP_CRACK_SIZE = 3.0f;
        private const float RIFT_CRACK_SIZE = 4.6f;
        private const float STOMP_CRACK_SECONDS = 1.8f;
        private const float RIFT_GLOW_SECONDS = 2.2f;
        /// <summary>
        /// Crack strokes (BarbarianGroove, BarbarianLavaSeam, BarbarianLavaCore) are sized this many times their length:
        /// their streak covers 40% of the quad, and neighbours overlap by 25%.
        /// </summary>
        private const float STROKE_SIZE_PER_METRE = 3.125f;
        /// <summary>How far the middle of a crack's span is pushed sideways, as a share of the span (either side).</summary>
        private const float KINK_MIN = 0.07f;
        private const float KINK_MAX = 0.16f;
        private const float GROOVE_SECONDS = 2.0f;
        private const float RUBBLE_SHARE = 1.2f;
        /// <summary>Spike height per metre of fissure width, and the random spread of heights and sideways offsets.</summary>
        private const float SPIKE_HEIGHT_SHARE = 1.35f;
        private const float SPIKE_HEIGHT_MIN = 0.8f;
        private const float SPIKE_HEIGHT_MAX = 1.25f;
        private const float SPIKE_SIDE_SHARE = 0.7f;
        private const int SPIKES_PER_SEGMENT = 3;
        private const int RIFT_SPIKES_PER_SEGMENT = 1;
        private const float WAVE_HEIGHT = 0.35f;
        private const float WAVE_SPEED = 3.5f;
        private const float WAVE_ALONG = 1.0f;
        private const float WAVE_SIZE = 0.9f;
        private const float WAVE_SECONDS = 1.1f;
        /// <summary>Tectonic Rift draws its molten crack over spans of this many segments (longer, calmer strokes).</summary>
        private const int LAVA_SPAN_SEGMENTS = 2;
        /// <summary>The dark crust outlasts the molten seam a little, so the crack goes dark before it closes.</summary>
        private const float CRUST_EXTRA_SECONDS = 0.3f;
        /// <summary>The heat glow is this much longer than its span, so neighbouring glows merge into one band.</summary>
        private const float LAVA_GLOW_SHARE = 1.6f;
        /// <summary>The burning lava zone: bubbling spurts, embers and smoke (within this share of its half width).</summary>
        private const float LAVA_SPURT_INTERVAL = 0.1f;
        private const int LAVA_SPURT_COUNT = 2;
        private const float LAVA_EMBER_INTERVAL = 0.06f;
        private const float LAVA_SMOKE_INTERVAL = 0.35f;
        private const float LAVA_SIDE_SHARE = 0.5f;
        private const float SMOKE_SIZE = 0.8f;
        private const float SMOKE_SECONDS = 2.2f;

        private static readonly Color STONE_SPARK = new Color(1.0f, 0.88f, 0.7f, 1.0f);
        private static readonly Color LAVA_GLOW = new Color(1.0f, 0.4f, 0.08f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.LOW_GROUND_LIFT);

        private readonly HashSet<Enemy> memory = new HashSet<Enemy>();
        private readonly int[] targets = new int[MAX_FISSURES];
        private readonly int crack;
        private readonly int crackGlow;
        private readonly int groove;
        private readonly int rubble;
        private readonly int spike;
        private readonly int dustWave;
        private readonly int lavaSeam;
        private readonly int lavaCore;
        private readonly int lavaGlow;
        private readonly ZoneLook lavaLook;
        private readonly FxComposite stomp;
        private readonly FxComposite riftStomp;
        private readonly FxComposite eruption;
        private readonly FxComposite riftEruption;

        public FaultLine(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            crack = effects.Find("BarbarianCrack");
            crackGlow = effects.Find("BarbarianCrackGlow");
            groove = effects.Find("BarbarianGroove");
            rubble = effects.Find("BarbarianRubble");
            spike = effects.Find("BarbarianRockSpike");
            dustWave = effects.Find("BarbarianDustWave");
            lavaSeam = effects.Find("BarbarianLavaSeam");
            lavaCore = effects.Find("BarbarianLavaCore");
            lavaGlow = effects.Find("BarbarianLavaGlow");
            lavaLook = new ZoneLook(
                new ZoneStream(effects.Find("BarbarianLavaSpurt"), LAVA_SPURT_INTERVAL, LAVA_SPURT_COUNT, LAVA_GLOW),
                new ZoneStream(effects.Find("BarbarianLavaEmbers"), LAVA_EMBER_INTERVAL, 1, WHITE)
                {
                    spread = LAVA_SIDE_SHARE,
                    height = FxSizes.LOW_GROUND_LIFT,
                },
                new ZoneStream(effects.Find("BarbarianLavaSmoke"), LAVA_SMOKE_INTERVAL, 1, WHITE)
                {
                    spread = LAVA_SIDE_SHARE,
                    height = FxSizes.LOW_GROUND_LIFT,
                    size = SMOKE_SIZE,
                    lifetime = SMOKE_SECONDS,
                });
            // Size unit = 1 m.
            stomp = Composite(new FxLayer("BarbarianFlash", 1, 1.8f), FxLayer.Ground("BarbarianShockRing", 5.0f, false),
                              FxLayer.Ground("BarbarianDustRing", 5.5f, false), new FxLayer("BarbarianDust", 8, 0.8f, false),
                              new FxLayer("BarbarianRocks", 8, 0.0f, false), new FxLayer("BarbarianBoulders", 2, 0.0f, false),
                              new FxLayer("BarbarianDirtSpray", 6, 0.0f, false), new FxLayer("BarbarianGrit", 8));
            riftStomp = Composite(new FxLayer("BarbarianFlash", 1, 3.0f), FxLayer.Ground("BarbarianShockRing", 6.5f, false),
                                  FxLayer.Ground("BarbarianDustRing", 7.0f, false), new FxLayer("BarbarianDust", 5, 0.9f, false),
                                  new FxLayer("BarbarianRocks", 6, 0.0f, false), new FxLayer("BarbarianBoulders", 3, 0.0f, false),
                                  new FxLayer("BarbarianMagma", 10, 0.0f, false), new FxLayer("BarbarianGrit", 8));
            eruption = Composite(new FxLayer("BarbarianDirtSpray", 1, 0.0f, false), new FxLayer("BarbarianRocks", 1, 0.0f, false),
                                 new FxLayer("BarbarianGrit", 1));
            riftEruption = Composite(new FxLayer("BarbarianMagma", 2, 0.0f, false));
        }

        protected override bool Cast(in CastInfo cast)
        {
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_FISSURES);
            int found = FindNearest(level[WeaponField.Range], targets, isEvolved ? 1 : count);
            if (found == 0)
            {
                return false;
            }
            Vector3 origin = heroPosition;
            Vector3 first = Ground.Direction(origin, enemies.GetPosition(targets[0]), facing);
            float length = AreaOf(level[WeaponField.Radius]);
            float halfWidth = AreaOf(level[WeaponField.Width]) * 0.5f;
            memory.Clear();
            for (int i = 0; i < count; i++)
            {
                bool hasTarget = !isEvolved && i < found;
                Vector3 direction = hasTarget ? Ground.Direction(origin, enemies.GetPosition(targets[i]), first) : Ground.Rotate(first, Ground.FULL_TURN * i / count);
                SendFissure(origin, direction, length, halfWidth, cast);
            }
            DrawStomp(origin, first);
            PlayAt("fault_stomp", origin);
            PlayAt("fault_crack", origin + first * (length * 0.5f));
            RequestAttack("Chop");
            ctx.feel.Shake(STOMP_TRAUMA);
            return true;
        }

        /// <summary>Segments a fissure of this length is split into (one event each).</summary>
        private static int SegmentsOf(float length) => Math.Clamp((int)MathF.Round(length / SEGMENT_LENGTH), MIN_SEGMENTS, MAX_SEGMENTS);

        private void SendFissure(Vector3 origin, Vector3 direction, float length, float halfWidth, in CastInfo cast)
        {
            int segments = SegmentsOf(length);
            float step = length / segments;
            float travel = level[WeaponField.Interval];
            for (int j = 0; j < segments; j++)
            {
                Schedule(travel * j / segments, SegmentEvent(origin + direction * (step * j), direction, step, halfWidth, j, cast));
            }
            if (isEvolved)
            {
                Schedule(travel, new WeaponEvent { kind = LAVA, position = origin, direction = direction, value = length, radius = halfWidth, cast = cast });
            }
        }

        /// <summary>The <paramref name="segment"/>-th stretch of a fissure: <paramref name="step"/> m from <paramref name="start"/>.</summary>
        private static WeaponEvent SegmentEvent(Vector3 start, Vector3 direction, float step, float halfWidth, int segment, in CastInfo cast)
        {
            return new WeaponEvent
            {
                kind = SEGMENT,
                position = start,
                direction = direction,
                value = step,
                radius = halfWidth,
                index = segment,
                cast = cast,
            };
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == SEGMENT)
            {
                Erupt(e);
            }
            else if (e.kind == LAVA)
            {
                LeaveLava(e);
            }
        }

        private void Erupt(in WeaponEvent e)
        {
            Vector3 a = e.position;
            Vector3 b = a + e.direction * e.value;
            var effect = new HitEffect { stunSeconds = DurationOf(level[WeaponField.Stun]) };
            strikes.Capsule(a, b, e.radius, DamageOf(WeaponField.Damage, e.cast), KnockbackOf(level[WeaponField.Knockback]), slot, effect,
                            PushMode.Sideways, default, memory);
            ReadOnlySpan<int> popped = strikes.lastHits;
            for (int i = 0; i < popped.Length; i++)
            {
                enemies.Launch(popped[i], POP_HEIGHT);
            }
            DrawSegment(a, b, e);
        }

        /// <summary>
        /// One segment tearing open: chunky rock spikes and a dust wave rolling out. Fault Line opens a dark zigzag
        /// groove lined with rubble, clods and rocks; Tectonic Rift spurts magma and, every second segment, lays the
        /// molten crack over the next two.
        /// </summary>
        private void DrawSegment(Vector3 a, Vector3 b, in WeaponEvent e)
        {
            Vector3 middle = Ground.Flat((a + b) * 0.5f) + LIFT;
            int spikes = isEvolved ? RIFT_SPIKES_PER_SEGMENT : SPIKES_PER_SEGMENT;
            for (int k = 0; k < spikes; k++)
            {
                RaiseSpike(a, b, e.direction, e.radius);
            }
            if (isEvolved)
            {
                PushDustWave(middle, e.direction, e.index % 2 == 0 ? 1.0f : -1.0f);
                riftEruption.Play(middle, FxSizes.METRE_UNIT, 1.0f, LAVA_GLOW);
                if (e.index % LAVA_SPAN_SEGMENTS == 0)
                {
                    DrawLavaSpan(a, e);
                }
                return;
            }
            PushDustWave(middle, e.direction, 1.0f);
            PushDustWave(middle, e.direction, -1.0f);
            EmitCrack(groove, a, Kink(a, b, e.direction, e.value), b, GROOVE_SECONDS);
            effects.EmitMoving(rubble, middle, e.direction * FxSizes.HEADING_SPEED, 1, WHITE, e.value * RUBBLE_SHARE, GROOVE_SECONDS);
            eruption.Play(middle, FxSizes.METRE_UNIT, fxScale, STONE_SPARK);
        }

        /// <summary>A rock spike at a random point of the segment, a little off its middle line.</summary>
        private void RaiseSpike(Vector3 a, Vector3 b, Vector3 direction, float halfWidth)
        {
            Vector3 side = Ground.Perpendicular(direction) * (halfWidth * SPIKE_SIDE_SHARE * Random.Range(-1.0f, 1.0f));
            Vector3 root = Ground.Flat(Vector3.Lerp(a, b, Random.Float())) + side;
            EmitSized(spike, root, WHITE, halfWidth * 2.0f * SPIKE_HEIGHT_SHARE * Random.Range(SPIKE_HEIGHT_MIN, SPIKE_HEIGHT_MAX));
        }

        /// <summary>Dust rolling out to one side of the fissure (+1 right of its heading, -1 left) and a little along it.</summary>
        private void PushDustWave(Vector3 middle, Vector3 direction, float side)
        {
            Vector3 velocity = Ground.Perpendicular(direction) * (WAVE_SPEED * side) + direction * WAVE_ALONG;
            effects.EmitMoving(dustWave, middle + Ground.Up(WAVE_HEIGHT), velocity, 1, WAVE_SIZE, WAVE_SECONDS);
        }

        /// <summary>
        /// Tectonic Rift's molten crack over up to two segments from <paramref name="a"/>: a dark crust, an orange seam,
        /// a white-hot core and the heat's glow on the ground, all lasting until this fissure's lava ends.
        /// </summary>
        private void DrawLavaSpan(Vector3 a, in WeaponEvent e)
        {
            int segments = SegmentsOf(AreaOf(level[WeaponField.Radius]));
            int steps = Math.Clamp(segments - e.index, 1, LAVA_SPAN_SEGMENTS);
            float span = e.value * steps;
            Vector3 b = a + e.direction * span;
            float seconds = level[WeaponField.Interval] * (1.0f - (float)e.index / segments) + DurationOf(level[WeaponField.SubDuration]);
            Vector3 kink = Kink(a, b, e.direction, span);
            EmitCrack(groove, a, kink, b, seconds + CRUST_EXTRA_SECONDS);
            EmitCrack(lavaSeam, a, kink, b, seconds);
            EmitCrack(lavaCore, a, kink, b, seconds);
            Vector3 middle = Ground.Flat((a + b) * 0.5f) + LIFT;
            effects.EmitMoving(lavaGlow, middle, e.direction * FxSizes.HEADING_SPEED, 1, WHITE, span * LAVA_GLOW_SHARE, seconds);
        }

        /// <summary>A crack's zigzag over one span: its middle pushed to a random side, its ends on the line so spans join.</summary>
        private static Vector3 Kink(Vector3 a, Vector3 b, Vector3 direction, float span)
        {
            float side = span * Random.Range(KINK_MIN, KINK_MAX) * (Random.Float() < 0.5f ? -1.0f : 1.0f);
            return (a + b) * 0.5f + Ground.Perpendicular(direction) * side;
        }

        /// <summary>Two crack strokes, <paramref name="a"/> to the kink and the kink to <paramref name="b"/>.</summary>
        private void EmitCrack(int effect, Vector3 a, Vector3 kink, Vector3 b, float lifetime)
        {
            EmitStroke(effect, a, kink, lifetime);
            EmitStroke(effect, kink, b, lifetime);
        }

        private void EmitStroke(int effect, Vector3 from, Vector3 to, float lifetime)
        {
            Vector3 heading = Ground.Direction(from, to, Vector3.forward) * FxSizes.HEADING_SPEED;
            Vector3 middle = Ground.Flat((from + to) * 0.5f) + LIFT;
            effects.EmitMoving(effect, middle, heading, 1, WHITE, Ground.Distance(from, to) * STROKE_SIZE_PER_METRE, lifetime);
        }

        /// <summary>The stomp at the feet: flash, shockwave, dust and rocks over a crack (Tectonic Rift: lava flash and a glowing web).</summary>
        private void DrawStomp(Vector3 origin, Vector3 first)
        {
            Vector3 ground = Ground.Flat(origin) + LIFT;
            Vector3 at = ground + Ground.Up(STOMP_HEIGHT);
            Vector3 heading = first * FxSizes.HEADING_SPEED;
            if (isEvolved)
            {
                riftStomp.Play(at, FxSizes.METRE_UNIT, fxScale, LAVA_GLOW);
                effects.EmitMoving(crack, ground, heading, 1, WHITE, RIFT_CRACK_SIZE, STOMP_CRACK_SECONDS);
                effects.EmitMoving(crackGlow, ground, heading, 1, LAVA_GLOW, RIFT_CRACK_SIZE, RIFT_GLOW_SECONDS);
                return;
            }
            stomp.Play(at, FxSizes.METRE_UNIT, fxScale, BarbarianLooks.DUST_GOLD);
            effects.EmitMoving(crack, ground, heading, 1, WHITE, STOMP_CRACK_SIZE, STOMP_CRACK_SECONDS);
        }

        /// <summary>Tectonic Rift: the fissure's lava burns as a lingering zone that bubbles, spits embers and smokes until it cools.</summary>
        private void LeaveLava(in WeaponEvent e)
        {
            ctx.zones.Add(new ZoneSpec
            {
                shape = ZoneShape.Capsule,
                a = e.position,
                b = e.position + e.direction * e.value,
                radius = e.radius,
                damage = DamageOf(WeaponField.SubDamage, e.cast),
                interval = level[WeaponField.SubInterval],
                duration = DurationOf(level[WeaponField.SubDuration]),
                source = slot,
                effect = new HitEffect { numberKind = DamageKind.Burn },
                look = lavaLook,
            });
        }
    }
}

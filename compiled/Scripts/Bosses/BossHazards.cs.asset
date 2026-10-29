using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Boss attacks that land after the boss has moved on: filling danger circles (Bone Rain, Grave Field), expanding
    /// gapped rings (Soul Nova) and spike lines that erupt metre by metre (Grave Slam). Each hazard hits the hero at
    /// most once, at the moment and place its telegraph promised. A hazard keeps resolving whatever its boss does
    /// next (a phase change, its death): the decal on the ground stays a promise. Main thread, game time.
    /// </summary>
    public sealed class BossHazards
    {
        private const int INITIAL_CAPACITY = 64;
        /// <summary>Seconds before a circle's hit that its falling bone appears (Bone Rain).</summary>
        private const float FALL_LEAD_SECONDS = 0.28f;
        private const float FALL_HEIGHT = 9.0f;
        private const float FALL_SIZE = 1.4f;
        private const float IMPACT_HEIGHT = 0.1f;
        /// <summary>Metres between two spike bursts along an erupting line.</summary>
        private const float SPIKE_SPACING = 1.0f;
        /// <summary>Metres behind the eruption front that still hurt (the spikes stand for a moment).</summary>
        private const float SPIKE_TRAIL = 2.0f;
        private const float SPIKE_JITTER = 0.6f;
        private const float SPIKE_SIZE = 2.2f;
        private const float SPIKE_LIFETIME = 0.7f;
        private const int SPIKE_SHARDS = 3;
        private const int IMPACT_SHARDS = 5;
        private const float IMPACT_SIZE_PER_RADIUS = 2.2f;
        private const float IMPACT_LIFETIME = 0.6f;
        /// <summary>Length (m) of one segment of an expanding nova ring at its largest radius.</summary>
        private const float NOVA_SEGMENT_LENGTH = 2.0f;

        private struct CircleHazard
        {
            public Vector3 center;
            public float radius;
            public float hitAt;
            public float damage;
            public string source;
            public int impactEffect;
            public Color impactColor;
            public string impactSound;
            public bool falls;
            public bool fallShown;
        }

        private struct RingHazard
        {
            public Vector3 center;
            public float startAt;
            public float speed;
            public float halfThickness;
            public float maxRadius;
            public Vector3 gapDirection;
            public float gapHalfAngle;
            public float damage;
            public string source;
            public string startSound;
            public bool started;
            public bool hasHit;
        }

        private struct LineHazard
        {
            public Vector3 origin;
            public Vector3 direction;
            public float length;
            public float halfWidth;
            public float startAt;
            public float speed;
            public float damage;
            public string source;
            public float nextSpike;
            public bool hasHit;
        }

        private readonly Run run;
        private readonly BossFx fx;
        private readonly List<CircleHazard> circles = new List<CircleHazard>(INITIAL_CAPACITY);
        private readonly List<RingHazard> rings = new List<RingHazard>(8);
        private readonly List<LineHazard> lines = new List<LineHazard>(4);
        private float time;

        public BossHazards(Run run, BossFx fx)
        {
            this.run = run;
            this.fx = fx;
        }

        /// <summary>
        /// A circle that hits at <paramref name="delay"/> seconds from now; the caller draws its telegraph.
        /// <paramref name="falls"/> drops a bone streak onto it just before the hit.
        /// </summary>
        /// <param name="impactColor">Tint of the impact effect (white keeps the emitter's own colors).</param>
        public void AddCircle(Vector3 center, float radius, float delay, float damage, string source, int impactEffect, Color impactColor, string impactSound, bool falls)
        {
            circles.Add(new CircleHazard
            {
                center = BossGeometry.Flat(center),
                radius = radius,
                hitAt = time + delay,
                damage = damage,
                source = source,
                impactEffect = impactEffect,
                impactColor = impactColor,
                impactSound = impactSound,
                falls = falls,
            });
        }

        /// <summary>
        /// A ring band growing from <paramref name="center"/> at <paramref name="speed"/> m/s after
        /// <paramref name="delay"/> seconds, up to <paramref name="maxRadius"/>; the hero is safe in its two gaps.
        /// Shows its own wave when it starts.
        /// </summary>
        public void AddRing(Vector3 center, float delay, float speed, float halfThickness, float maxRadius, Vector3 gapDirection, float gapHalfAngle, float damage, string source, string startSound)
        {
            rings.Add(new RingHazard
            {
                center = BossGeometry.Flat(center),
                startAt = time + delay,
                speed = speed,
                halfThickness = halfThickness,
                maxRadius = maxRadius,
                gapDirection = gapDirection,
                gapHalfAngle = gapHalfAngle,
                damage = damage,
                source = source,
                startSound = startSound,
            });
        }

        /// <summary>A spike line erupting from <paramref name="origin"/> along a heading at <paramref name="speed"/> m/s.</summary>
        public void AddLine(Vector3 origin, Vector3 direction, float length, float halfWidth, float speed, float damage, string source)
        {
            lines.Add(new LineHazard
            {
                origin = BossGeometry.Flat(origin),
                direction = BossGeometry.Flat(direction).normalized,
                length = length,
                halfWidth = halfWidth,
                startAt = time,
                speed = speed,
                damage = damage,
                source = source,
                nextSpike = 0.0f,
            });
        }

        public void Tick(float dt)
        {
            time += dt;
            TickCircles();
            TickRings();
            TickLines();
        }

        /// <summary>Drops every pending hazard (new run, victory).</summary>
        public void Clear()
        {
            circles.Clear();
            rings.Clear();
            lines.Clear();
        }

        private float heroGrace => run.hero != null ? run.hero.radius * Boss.HERO_GRACE_SHARE : 0.0f;

        private void TickCircles()
        {
            for (int i = circles.Count - 1; i >= 0; i--)
            {
                CircleHazard circle = circles[i];
                if (circle.falls && !circle.fallShown && time >= circle.hitAt - FALL_LEAD_SECONDS)
                {
                    circle.fallShown = true;
                    circles[i] = circle;
                    float fallSpeed = FALL_HEIGHT / FALL_LEAD_SECONDS;
                    fx.Moving(fx.boneRainFall, circle.center + new Vector3(0.0f, FALL_HEIGHT, 0.0f), new Vector3(0.0f, -fallSpeed, 0.0f), 1, FALL_SIZE, FALL_LEAD_SECONDS);
                }
                if (time < circle.hitAt)
                {
                    continue;
                }
                circles.RemoveAt(i);
                fx.Burst(circle.impactEffect, circle.center + new Vector3(0.0f, IMPACT_HEIGHT, 0.0f), 1, circle.impactColor, circle.radius * IMPACT_SIZE_PER_RADIUS, IMPACT_LIFETIME);
                fx.Burst(fx.boneBurst, circle.center + new Vector3(0.0f, IMPACT_HEIGHT * 3.0f, 0.0f), IMPACT_SHARDS);
                if (!string.IsNullOrEmpty(circle.impactSound))
                {
                    run.sounds.PlayAt(circle.impactSound, circle.center);
                }
                if (run.hero != null && BossGeometry.InCircle(run.hero.position, circle.center, circle.radius, heroGrace))
                {
                    Boss.DamageHero(run, circle.damage, circle.center, circle.source);
                }
            }
        }

        private void TickRings()
        {
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                RingHazard ring = rings[i];
                if (time < ring.startAt)
                {
                    continue;
                }
                if (!ring.started)
                {
                    ring.started = true;
                    fx.NovaWave(ring.center, ring.gapDirection, ring.gapHalfAngle, ring.speed, ring.maxRadius, ring.halfThickness * 2.0f, NOVA_SEGMENT_LENGTH);
                    if (!string.IsNullOrEmpty(ring.startSound))
                    {
                        run.sounds.PlayAt(ring.startSound, ring.center);
                    }
                }
                float radius = (time - ring.startAt) * ring.speed;
                if (radius > ring.maxRadius + ring.halfThickness)
                {
                    rings.RemoveAt(i);
                    continue;
                }
                if (!ring.hasHit && run.hero != null
                    && BossGeometry.InGappedRing(run.hero.position, ring.center, radius, ring.halfThickness, ring.gapDirection, ring.gapHalfAngle, heroGrace)
                    && Boss.DamageHero(run, ring.damage, ring.center, ring.source))
                {
                    ring.hasHit = true;
                }
                rings[i] = ring;
            }
        }

        private void TickLines()
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                LineHazard line = lines[i];
                float front = MathF.Min((time - line.startAt) * line.speed, line.length);
                while (line.nextSpike <= front)
                {
                    // Cosmetic: the engine's random, so the run's seeded dice stay untouched.
                    float jitter = Random.Range(-SPIKE_JITTER, SPIKE_JITTER);
                    var side = new Vector3(line.direction.z, 0.0f, -line.direction.x);
                    Vector3 spike = line.origin + line.direction * line.nextSpike + side * jitter;
                    fx.Burst(fx.boneSpike, spike, 1, Color.white, SPIKE_SIZE, SPIKE_LIFETIME);
                    fx.Burst(fx.boneBurst, spike + new Vector3(0.0f, IMPACT_HEIGHT * 3.0f, 0.0f), SPIKE_SHARDS);
                    if (((int)line.nextSpike & 3) == 0)
                    {
                        run.sounds.PlayAt("bone_spike", spike);
                    }
                    line.nextSpike += SPIKE_SPACING;
                }
                if (!line.hasHit && run.hero != null)
                {
                    Vector3 hero = run.hero.position;
                    float along = BossGeometry.GetAlong(hero, line.origin, line.direction);
                    bool isReached = along <= front + heroGrace && along >= front - SPIKE_TRAIL;
                    if (isReached && BossGeometry.InLane(hero, line.origin, line.direction, line.length, line.halfWidth, heroGrace)
                        && Boss.DamageHero(run, line.damage, line.origin + line.direction * MathF.Max(along, 0.0f), line.source))
                    {
                        line.hasHit = true;
                    }
                }
                if (front >= line.length && time - line.startAt >= (line.length + SPIKE_TRAIL) / MathF.Max(line.speed, 0.1f))
                {
                    lines.RemoveAt(i);
                    continue;
                }
                lines[i] = line;
            }
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The shape of a lingering ground zone.</summary>
    public enum ZoneShape : byte
    {
        Circle,
        /// <summary>A segment a-b swept by the radius (fissures, lava cracks).</summary>
        Capsule,
    }

    /// <summary>A particle effect a lingering zone sprinkles at random points inside it while it lasts.</summary>
    public struct ZoneStream
    {
        /// <summary>The share of the radius particles start within, unless a stream names its own.</summary>
        public const float DEFAULT_SPREAD = 0.85f;

        public int effect;
        /// <summary>Seconds between two sprinkles, and the particles of each.</summary>
        public float interval;
        public int count;
        public Color color;
        /// <summary>Particles start within this share of the radius around the zone's center (or its segment).</summary>
        public float spread;
        /// <summary>Height (m) above the ground particles start at.</summary>
        public float height;
        /// <summary>A size and lifetime of their own; 0 keeps the emitter's.</summary>
        public float size;
        public float lifetime;

        /// <summary>A stream of the emitter's own particles a little above the ground, within most of the radius.</summary>
        public ZoneStream(int effect, float interval, int count, Color color)
        {
            this.effect = effect;
            this.interval = interval;
            this.count = count;
            this.color = color;
            spread = DEFAULT_SPREAD;
            height = FxSizes.GROUND_LIFT;
            size = 0.0f;
            lifetime = 0.0f;
        }
    }

    /// <summary>
    /// How a weapon's lingering zones look: the particle streams each of them sprinkles. Build it once (in the weapon's
    /// constructor) and hand it to every zone of that look.
    /// </summary>
    public sealed class ZoneLook
    {
        public readonly ZoneStream[] streams;

        public ZoneLook(params ZoneStream[] streams)
        {
            if (streams.Length > GroundZones.MAX_STREAMS)
            {
                throw new ArgumentException($"A zone look has at most {GroundZones.MAX_STREAMS} particle streams.");
            }
            this.streams = streams;
        }
    }

    /// <summary>What a new lingering zone does and how it looks.</summary>
    public struct ZoneSpec
    {
        public ZoneShape shape;
        public Vector3 a;
        public Vector3 b;
        public float radius;
        /// <summary>Damage per tick (already scaled by Might); 0 for status-only zones.</summary>
        public float damage;
        public float interval;
        public float duration;
        public int source;
        public HitEffect effect;
        /// <summary>Health the hero regains per tick that hits at least one enemy.</summary>
        public float healPerTick;
        /// <summary>Enemies inside lose the hero, wander and do not cast (smoke) for this many seconds per tick.</summary>
        public float confuseSeconds;
        /// <summary>The particles the zone sprinkles; null for none.</summary>
        public ZoneLook look;
    }

    /// <summary>
    /// Lingering ground effects simulated in C# (pools, craters, lava cracks, smoke): each zone damages what stands in it
    /// every tick through <see cref="Strikes"/> and sprinkles the particle streams of its look. Fixed capacity; a full
    /// list replaces the zone closest to ending. Main thread; no allocations.
    /// </summary>
    public sealed class GroundZones
    {
        /// <summary>Particle streams one zone look may have.</summary>
        public const int MAX_STREAMS = 4;
        private const int CAPACITY = 64;
        /// <summary>The shortest tick and particle intervals a zone runs at, whatever its spec asks.</summary>
        private const float MIN_TICK_INTERVAL = 0.05f;
        private const float MIN_PARTICLE_INTERVAL = 0.02f;

        private struct Zone
        {
            public ZoneSpec spec;
            public float life;
            public float tickTimer;
        }

        private readonly Zone[] zones = new Zone[CAPACITY];
        /// <summary>Each zone's stream timers, <see cref="MAX_STREAMS"/> per zone.</summary>
        private readonly float[] streamTimers = new float[CAPACITY * MAX_STREAMS];
        private readonly Strikes strikes;
        private readonly Effects effects;
        private readonly EnemyWorld enemies;
        private readonly HeroController hero;
        private int count;

        public GroundZones(Strikes strikes, Run run)
        {
            this.strikes = strikes;
            effects = run.effects;
            enemies = run.enemies;
            hero = run.hero;
        }

        /// <summary>Starts a zone; it ticks and sprinkles at once (the ignition hit).</summary>
        public void Add(in ZoneSpec spec)
        {
            int index = count < CAPACITY ? count++ : FindShortest();
            zones[index] = new Zone { spec = spec, life = spec.duration, tickTimer = 0.0f };
            Array.Clear(streamTimers, index * MAX_STREAMS, MAX_STREAMS);
        }

        public void Clear() => count = 0;

        public void Tick(float dt)
        {
            int i = 0;
            while (i < count)
            {
                ref Zone zone = ref zones[i];
                zone.life -= dt;
                if (zone.life <= 0.0f)
                {
                    RemoveAt(i);
                    continue;
                }
                zone.tickTimer -= dt;
                if (zone.tickTimer <= 0.0f)
                {
                    zone.tickTimer += MathF.Max(zone.spec.interval, MIN_TICK_INTERVAL);
                    Strike(ref zone);
                }
                if (zone.spec.look != null)
                {
                    Sprinkle(i, zone.spec, dt);
                }
                i++;
            }
        }

        private void Strike(ref Zone zone)
        {
            ref ZoneSpec spec = ref zone.spec;
            int hits = spec.shape == ZoneShape.Circle
                ? strikes.Circle(spec.a, spec.radius, spec.damage, 0.0f, spec.source, spec.effect, PushMode.None)
                : strikes.Capsule(spec.a, spec.b, spec.radius, spec.damage, 0.0f, spec.source, spec.effect, PushMode.None);
            if (hits > 0 && spec.healPerTick > 0.0f && hero != null)
            {
                hero.Heal(spec.healPerTick, HealSource.Weapon);
            }
            if (spec.confuseSeconds > 0.0f)
            {
                ReadOnlySpan<int> rows = strikes.lastHits;
                for (int k = 0; k < rows.Length; k++)
                {
                    enemies.Confuse(rows[k], spec.confuseSeconds);
                }
            }
        }

        /// <summary>Each stream of the zone's look whose timer ran out sprinkles at a random point inside the zone.</summary>
        private void Sprinkle(int index, in ZoneSpec spec, float dt)
        {
            ZoneStream[] streams = spec.look.streams;
            for (int s = 0; s < streams.Length; s++)
            {
                ref float timer = ref streamTimers[index * MAX_STREAMS + s];
                timer -= dt;
                if (timer > 0.0f)
                {
                    continue;
                }
                ref ZoneStream stream = ref streams[s];
                timer += MathF.Max(stream.interval, MIN_PARTICLE_INTERVAL);
                Vector3 point = GetRandomPoint(spec, stream);
                if (stream.size > 0.0f)
                {
                    effects.Emit(stream.effect, point, stream.count, stream.color, stream.size, stream.lifetime);
                }
                else
                {
                    effects.Emit(stream.effect, point, stream.count, stream.color);
                }
            }
        }

        private static Vector3 GetRandomPoint(in ZoneSpec spec, in ZoneStream stream)
        {
            Vector3 center = spec.shape == ZoneShape.Circle ? spec.a : Vector3.Lerp(spec.a, spec.b, Random.Float());
            return center + Ground.RandomInCircle(spec.radius * stream.spread) + Ground.Up(stream.height);
        }

        private void RemoveAt(int index)
        {
            int last = --count;
            zones[index] = zones[last];
            Array.Copy(streamTimers, last * MAX_STREAMS, streamTimers, index * MAX_STREAMS, MAX_STREAMS);
        }

        private int FindShortest()
        {
            int best = 0;
            for (int i = 1; i < count; i++)
            {
                if (zones[i].life < zones[best].life)
                {
                    best = i;
                }
            }
            return best;
        }
    }
}

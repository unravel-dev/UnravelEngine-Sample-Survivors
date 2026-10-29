using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Nightshade Plague's toxic clouds: every rupture leaves one (a rupture inside an existing cloud refreshes it
    /// instead), and every tick the owner's callback poisons and slows what stands in it. They are not GroundZones: a
    /// cloud merges with the one it lands in, its tick adds venom rather than a hit, and its fumes and stain scale with
    /// its radius. Look: thin green-black fumes drifting at knee to chest height over a mottled dark venom stain, faint
    /// green glow wisps inside and bubbles popping on the ground; nothing red-orange, and thin enough that the enemies
    /// inside stay readable. Fixed capacity; a full list replaces the cloud closest to ending. Main thread; no
    /// allocations per frame.
    /// </summary>
    internal sealed class ToxicClouds
    {
        private const int CAPACITY = 10;
        /// <summary>A rupture closer than this share of the radius to a cloud's center refreshes that cloud.</summary>
        private const float MERGE_SHARE = 0.6f;
        private const float TICK_SECONDS = 0.5f;
        /// <summary>Fumes: one per interval at a random point of this share of the radius, between the two heights.</summary>
        private const float PUFF_INTERVAL = 0.12f;
        private const float PUFF_SPREAD = 0.85f;
        private const float PUFF_LOW = 0.25f;
        private const float PUFF_HIGH = 0.9f;
        private const float PUFF_SIZE_MIN = 0.45f;
        private const float PUFF_SIZE_MAX = 0.6f;
        private const float GLOW_SIZE = 0.8f;
        private const float GLOW_HEIGHT = 0.6f;
        /// <summary>Bubbles popping on the stain.</summary>
        private const float BUBBLE_INTERVAL = 0.25f;
        private const float BUBBLE_HEIGHT = 0.1f;
        /// <summary>The stain is one decal refreshed this often, living a little longer so the next one meets it.</summary>
        private const float STAIN_REFRESH = 0.8f;
        private const float STAIN_OVERLAP = 1.35f;
        private const float STAIN_DIAMETER_SHARE = 2.1f;
        /// <summary>The stain is drawn only while the cloud has more than this share of a refresh left.</summary>
        private const float STAIN_END_SHARE = 0.5f;

        private struct Cloud
        {
            public Vector3 center;
            public float radius;
            public float life;
            public float tickTimer;
            public float puffTimer;
            public float bubbleTimer;
            public float stainTimer;
        }

        private readonly Cloud[] clouds = new Cloud[CAPACITY];
        private readonly Effects effects;
        private readonly Action<Vector3, float> poison;
        private readonly int fume;
        private readonly int glow;
        private readonly int bubble;
        private readonly int stain;
        private int count;

        /// <param name="poison">Called every tick with a cloud's center and radius (outside any damage call).</param>
        public ToxicClouds(Effects effects, Action<Vector3, float> poison)
        {
            this.effects = effects;
            this.poison = poison;
            fume = effects.Find("RogueToxicCloud");
            glow = effects.Find("RogueToxicGlow");
            bubble = effects.Find("RogueVenomBubble");
            stain = effects.Find("RogueToxicStain");
        }

        /// <summary>Leaves a cloud at a point for some seconds; it poisons at once.</summary>
        public void Add(Vector3 center, float radius, float seconds)
        {
            Vector3 ground = Ground.Flat(center);
            for (int i = 0; i < count; i++)
            {
                ref Cloud cloud = ref clouds[i];
                if (Ground.Distance(cloud.center, ground) < cloud.radius * MERGE_SHARE)
                {
                    cloud.life = MathF.Max(cloud.life, seconds);
                    cloud.radius = MathF.Max(cloud.radius, radius);
                    return;
                }
            }
            int index = count < CAPACITY ? count++ : FindShortest();
            clouds[index] = new Cloud { center = ground, radius = radius, life = seconds };
        }

        /// <summary>Steps every cloud: ticks the poison, sprinkles the fumes and bubbles, refreshes the stain.</summary>
        public void Tick(float dt)
        {
            int i = 0;
            while (i < count)
            {
                ref Cloud cloud = ref clouds[i];
                cloud.life -= dt;
                if (cloud.life <= 0.0f)
                {
                    clouds[i] = clouds[--count];
                    continue;
                }
                cloud.tickTimer -= dt;
                if (cloud.tickTimer <= 0.0f)
                {
                    cloud.tickTimer += TICK_SECONDS;
                    poison(cloud.center, cloud.radius);
                }
                Show(ref cloud, dt);
                i++;
            }
        }

        private void Show(ref Cloud cloud, float dt)
        {
            cloud.puffTimer -= dt;
            if (cloud.puffTimer <= 0.0f)
            {
                cloud.puffTimer += PUFF_INTERVAL;
                Vector3 at = cloud.center + Ground.RandomInCircle(cloud.radius * PUFF_SPREAD) + Ground.Up(Random.Range(PUFF_LOW, PUFF_HIGH));
                effects.EmitSized(fume, at, 1, Color.white, cloud.radius * Random.Range(PUFF_SIZE_MIN, PUFF_SIZE_MAX));
                Vector3 inside = cloud.center + Ground.RandomInCircle(cloud.radius * PUFF_SPREAD) + Ground.Up(GLOW_HEIGHT);
                effects.EmitSized(glow, inside, 1, RogueLooks.VENOM, GLOW_SIZE);
            }
            cloud.bubbleTimer -= dt;
            if (cloud.bubbleTimer <= 0.0f)
            {
                cloud.bubbleTimer += BUBBLE_INTERVAL;
                Vector3 at = cloud.center + Ground.RandomInCircle(cloud.radius * PUFF_SPREAD) + Ground.Up(BUBBLE_HEIGHT);
                effects.Emit(bubble, at, 1, RogueLooks.VENOM);
            }
            cloud.stainTimer -= dt;
            if (cloud.stainTimer <= 0.0f && cloud.life > STAIN_REFRESH * STAIN_END_SHARE)
            {
                cloud.stainTimer += STAIN_REFRESH;
                Vector3 floor = cloud.center + Ground.Up(FxSizes.LOW_GROUND_LIFT);
                effects.Emit(stain, floor, 1, Color.white, cloud.radius * STAIN_DIAMETER_SHARE, STAIN_REFRESH * STAIN_OVERLAP);
            }
        }

        private int FindShortest()
        {
            int best = 0;
            for (int i = 1; i < count; i++)
            {
                if (clouds[i].life < clouds[best].life)
                {
                    best = i;
                }
            }
            return best;
        }
    }
}

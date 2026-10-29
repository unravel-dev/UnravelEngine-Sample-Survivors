using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Moonwell (healing at a price): a 3.5 m pool of moonlight that holds 40% of the hero's max health. Standing in it
    /// heals 6% of max health per second until it runs dry, but the first sip calls the dead: while the hero drinks, a
    /// ring of them rises around the well every 3 s (8 + 2 per minute) and marches on it. The light fades 25 s after
    /// the first sip. Only healing that lands drains the well. Look: a pale blue pillar and pool of light that dim as
    /// the well empties, motes rising off the water and off the hero while it drinks.
    /// </summary>
    public sealed class MoonwellEvent : MapEvent
    {
        private const float RADIUS = 3.5f;
        private const float POOL_SHARE = 0.4f;
        private const float HEAL_SHARE_PER_SECOND = 0.06f;
        private const float LIMIT_SECONDS = 25.0f;
        private const float WAVE_INTERVAL = 3.0f;
        private const float WAVE_RADIUS = 12.0f;
        private const int WAVE_BASE = 8;
        private const float WAVE_PER_MINUTE = 2.0f;
        /// <summary>Seconds between the heal numbers while the hero drinks (one number, not a crumb per frame).</summary>
        private const float NUMBER_INTERVAL = 0.5f;
        private const float GLOW_MIN = 0.25f;
        private const float GLOW_MAX = 1.0f;
        private const int MOTES = 3;
        /// <summary>The pillar dims with the water left, down to this share when the well is dry.</summary>
        private const float BEAM_MIN_SHARE = 0.4f;
        private const float WATER_MOTES_HEIGHT = 0.2f;
        private const float HERO_MOTES_HEIGHT = 0.5f;
        private const float HEAL_NUMBER_HEIGHT = 2.2f;

        private static readonly Color MOON = new Color(0.58f, 0.82f, 1.0f, 1.0f);
        private static readonly SurgeRule SURGE = new SurgeRule(WAVE_INTERVAL, WAVE_BASE, WAVE_PER_MINUTE, WAVE_RADIUS);

        private readonly float pool;
        private float remaining;
        private float healedForNumber;
        private float numberTimer;

        public override MapEventKind kind => MapEventKind.Moonwell;
        public override string title => "Moonwell";
        public override string goal => state == MapEventState.Waiting ? "Step in while hurt to heal; the dead will come."
                                     : "Stand in the light to heal.";
        public override Color color => MOON;
        public override string icon => UIText.Icon("mending");
        public override MarkerKind markerKind => MarkerKind.Moonwell;

        public MoonwellEvent(Run run, EventFx fx, Vector3 center) : base(run, fx, center)
        {
            pool = run.hero.maxHealth * POOL_SHARE;
            remaining = pool;
            progress = 1.0f;
            Announce("A moonwell shines on the moor");
        }

        protected override void Tick(float dt, bool refresh)
        {
            bool inside = IsHeroWithin(RADIUS);
            float fullness = remaining / pool;
            if (refresh)
            {
                fx.Beacon(center, Dim(MOON, BEAM_MIN_SHARE + (1.0f - BEAM_MIN_SHARE) * fullness));
                fx.Zone(center, RADIUS, MOON, GLOW_MIN + (GLOW_MAX - GLOW_MIN) * fullness);
                fx.Emit(fx.motes, center + new Vector3(0.0f, WATER_MOTES_HEIGHT, 0.0f), Math.Max(1, (int)(MOTES * fullness)), MOON);
            }
            if (state == MapEventState.Waiting)
            {
                if (inside && run.hero.healthFraction < 1.0f)
                {
                    Activate(LIMIT_SECONDS, "The dead smell the moonlight");
                }
                return;
            }
            if (inside)
            {
                Drink(dt, refresh);
            }
            ShowHealing(dt);
            if (remaining <= 0.0f)
            {
                progress = 0.0f;
                Win("The moonwell runs dry");
            }
        }

        /// <summary>Heals from the pool and calls the dead while the hero drinks.</summary>
        private void Drink(float dt, bool refresh)
        {
            HeroController hero = run.hero;
            float healed = hero.Heal(MathF.Min(remaining, hero.maxHealth * HEAL_SHARE_PER_SECOND * dt), HealSource.Event);
            remaining = MathF.Max(0.0f, remaining - healed);
            healedForNumber += healed;
            progress = remaining / pool;
            TickSurge(dt, SURGE);
            if (refresh && healed > 0.0f)
            {
                fx.Emit(fx.motes, hero.position + new Vector3(0.0f, HERO_MOTES_HEIGHT, 0.0f), MOTES, MOON);
            }
        }

        /// <summary>The healing shows as one number every half second, not a crumb per frame.</summary>
        private void ShowHealing(float dt)
        {
            numberTimer -= dt;
            if (numberTimer > 0.0f || healedForNumber < 1.0f)
            {
                return;
            }
            numberTimer = NUMBER_INTERVAL;
            run.damageNumbers?.ShowHeal(run.hero.position + new Vector3(0.0f, HEAL_NUMBER_HEIGHT, 0.0f), healedForNumber);
            healedForNumber = 0.0f;
        }

        /// <summary>The light fades: whatever was drunk was the prize, so it ends without a loss.</summary>
        protected override void TimeOut()
        {
            Win("The moonlight fades");
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Why nobody tanks a boss. Every boss hit that lands marks the hero: each stack makes the next boss hit hurt more and
    /// cuts all healing; a stack fades after a few seconds without a hit, so dodging is what clears it. Boss hits also find
    /// only half the hero's armor and grow with health beyond what a hero has at that point of a run, so stacking stats
    /// buys a mistake or two, not a stand-up fight. In turn a first hit never takes a glass hero's whole health: it is
    /// capped at a share of max health that grows with the stacks. A red ring on the hero's feet and the boss bar show the
    /// stacks, and a hit gives a moment of grace, so hits never chain within one blink. Main thread; the boss manager owns
    /// it while bosses live.
    /// </summary>
    public sealed class DeathMark
    {
        private const int MAX_STACKS = 5;
        private const float DAMAGE_PER_STACK = 0.25f;
        private const float HEALING_CUT_PER_STACK = 0.18f;
        private const float STACK_SECONDS = 4.5f;
        /// <summary>After a boss hit the hero is untouchable this long: every blow gets its moment to react to.</summary>
        private const float HIT_GRACE_SECONDS = 0.7f;
        /// <summary>A boss hit takes at most this share of max health, and this much more per stack.</summary>
        private const float MAX_HIT_SHARE = 0.6f;
        private const float MAX_HIT_SHARE_PER_STACK = 0.2f;
        /// <summary>Boss hits scale up with health above this much, plus this per minute of the run.</summary>
        private const float REFERENCE_HEALTH = 130.0f;
        private const float REFERENCE_HEALTH_PER_MINUTE = 12.0f;
        private const float SECONDS_PER_MINUTE = 60.0f;
        private const float RING_INTERVAL = 0.15f;
        private const float RING_SECONDS = 0.3f;
        private const float RING_SIZE = 2.6f;
        private const float RING_SIZE_PER_STACK = 0.5f;
        private const float RING_ALPHA_PER_STACK = 0.2f;
        private const float GROUND_LIFT = 0.05f;
        private const float BURST_HEIGHT = 1.0f;
        private const float BURST_SIZE = 1.6f;
        private const float BANNER_SECONDS = 3.5f;

        private static readonly Color MARK_RED = new Color(1.0f, 0.12f, 0.1f, 1.0f);
        private static readonly string[] STACK_TITLES = BuildTitles();

        private readonly Run run;
        private readonly int ring;
        private readonly int burst;
        private int stacks;
        private float stackTimer;
        private float ringTimer;
        private bool isExplained;

        public DeathMark(Run run)
        {
            this.run = run;
            ring = run.effects.Find("BossMarkRing");
            burst = run.effects.Find("CoreFlash");
        }

        /// <summary>The line the boss bar shows under the boss's own title; empty without stacks.</summary>
        public string stackTitle => STACK_TITLES[stacks];

        /// <summary>A boss attack's damage against this hero now, before the hero's armor.</summary>
        public float Scale(float amount)
        {
            HeroController hero = run.hero;
            float reference = REFERENCE_HEALTH + REFERENCE_HEALTH_PER_MINUTE * run.runTime / SECONDS_PER_MINUTE;
            float healthScale = MathF.Max(1.0f, hero.maxHealth / reference);
            float cap = hero.maxHealth * (MAX_HIT_SHARE + stacks * MAX_HIT_SHARE_PER_STACK);
            return MathF.Min(amount * (1.0f + stacks * DAMAGE_PER_STACK) * healthScale, cap) * run.stats.halfArmorFactor;
        }

        /// <summary>A boss hit landed: one more stack.</summary>
        public void AddStack()
        {
            stacks = Math.Min(stacks + 1, MAX_STACKS);
            stackTimer = STACK_SECONDS;
            ApplyHealing();
            run.hero.SetInvulnerable(HIT_GRACE_SECONDS);
            run.effects.EmitSized(burst, run.hero.position + Ground.Up(BURST_HEIGHT), 1, MARK_RED, BURST_SIZE);
            if (!isExplained)
            {
                isExplained = true;
                run.ui.ShowBanner("Death's Mark: every boss hit makes the next one worse and cuts your healing.", BANNER_SECONDS);
            }
        }

        /// <summary>Stacks fade one by one without a hit; once per frame while a boss lives.</summary>
        public void Tick(float dt)
        {
            if (stacks == 0)
            {
                return;
            }
            stackTimer -= dt;
            if (stackTimer <= 0.0f)
            {
                stacks--;
                stackTimer = STACK_SECONDS;
                ApplyHealing();
            }
            ringTimer -= dt;
            if (ringTimer > 0.0f)
            {
                return;
            }
            ringTimer = RING_INTERVAL;
            Vector3 feet = Ground.Flat(run.hero.position) + Ground.Up(GROUND_LIFT);
            Color color = new Color(MARK_RED.r, MARK_RED.g, MARK_RED.b, MathF.Min(1.0f, stacks * RING_ALPHA_PER_STACK));
            run.effects.Emit(ring, feet, 1, color, RING_SIZE + stacks * RING_SIZE_PER_STACK, RING_SECONDS);
        }

        /// <summary>No stacks and whole healing (no boss left, a new run).</summary>
        public void Clear()
        {
            stacks = 0;
            ApplyHealing();
        }

        /// <summary>A new run explains the mark again.</summary>
        public void ResetRun()
        {
            Clear();
            isExplained = false;
        }

        private void ApplyHealing()
        {
            if (run.hero != null)
            {
                run.hero.healingScale = 1.0f - stacks * HEALING_CUT_PER_STACK;
            }
        }

        private static string[] BuildTitles()
        {
            var titles = new string[MAX_STACKS + 1];
            titles[0] = "";
            for (int i = 1; i <= MAX_STACKS; i++)
            {
                titles[i] = "Death's Mark x" + i;
            }
            return titles;
        }
    }
}

using System;

namespace Survivors
{
    /// <summary>
    /// A heal that gives at most a share of max health per second, however often it triggers (Bloodlust's full-Fury
    /// kills, Bloodletting's crits). The budget refills continuously up to one second's worth. Keep it in a non-readonly
    /// field: it is a mutable struct.
    /// </summary>
    public struct HealBudget
    {
        private readonly float sharePerSecond;
        private float left;

        public HealBudget(float sharePerSecond)
        {
            this.sharePerSecond = sharePerSecond;
            left = 0.0f;
        }

        /// <summary>Refills the budget for a frame; once per frame.</summary>
        public void Refill(float maxHealth, float dt)
        {
            float cap = maxHealth * sharePerSecond;
            left = MathF.Min(cap, left + cap * dt);
        }

        /// <summary>Heals up to <paramref name="amount"/>, within what the budget has left.</summary>
        public void Heal(HeroController hero, float amount, HealSource source)
        {
            left -= hero.Heal(MathF.Min(amount, left), source);
        }
    }
}

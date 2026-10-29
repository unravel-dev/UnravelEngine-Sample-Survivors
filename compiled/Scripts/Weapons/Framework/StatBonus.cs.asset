namespace Survivors
{
    /// <summary>
    /// A stat bonus that moves with some state (Fury, Momentum, a passive's rank, standing on a glyph): setting a new
    /// value adds only the difference to the stats, so the bonus can change every frame without drifting. Keep it in
    /// a non-readonly field: it is a mutable struct.
    /// </summary>
    public struct StatBonus
    {
        private readonly StatId stat;
        private readonly bool isPercent;
        private float applied;

        /// <param name="isPercent">Percent bonuses use AddPercent (0.1 = +10%); the others AddFlat.</param>
        public StatBonus(StatId stat, bool isPercent)
        {
            this.stat = stat;
            this.isPercent = isPercent;
            applied = 0.0f;
        }

        /// <summary>The bonus now in the stats.</summary>
        public float value => applied;

        /// <summary>Moves the bonus to <paramref name="target"/>; nothing changes when it is already there.</summary>
        public void Set(PlayerStats stats, float target)
        {
            if (target == applied)
            {
                return;
            }
            Add(stats, stat, isPercent, target - applied);
            applied = target;
        }

        /// <summary>Adds a bonus once: percent bonuses with AddPercent, the others with AddFlat.</summary>
        public static void Add(PlayerStats stats, StatId stat, bool isPercent, float amount)
        {
            if (isPercent)
            {
                stats.AddPercent(stat, amount);
            }
            else
            {
                stats.AddFlat(stat, amount);
            }
        }
    }
}

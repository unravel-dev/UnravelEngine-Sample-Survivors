using System;

namespace Survivors
{
    /// <summary>
    /// Picks one of several options with a chance proportional to its weight (archetype mixes, boss attacks, map
    /// events). Options with weight 0 are never picked. Callers fill a span of weights (stackalloc: no garbage).
    /// </summary>
    public static class WeightedPick
    {
        /// <summary>An index of <paramref name="weights"/>, drawn with one roll of <paramref name="rng"/>.</summary>
        /// <returns>The picked index, or -1 (and no roll) when every weight is 0.</returns>
        public static int Pick(System.Random rng, ReadOnlySpan<float> weights)
        {
            float total = 0.0f;
            int last = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0.0f)
                {
                    total += weights[i];
                    last = i;
                }
            }
            if (last < 0)
            {
                return -1;
            }
            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0.0f)
                {
                    continue;
                }
                roll -= weights[i];
                if (roll <= 0.0f)
                {
                    return i;
                }
            }
            // Rounding can leave a sliver of the roll: it belongs to the last option.
            return last;
        }
    }
}

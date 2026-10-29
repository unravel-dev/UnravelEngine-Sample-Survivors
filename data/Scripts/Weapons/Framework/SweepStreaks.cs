using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>How the wind streaks of a melee sweep look (see <see cref="SweepStreaks"/>).</summary>
    public struct SweepStyle
    {
        /// <summary>The streak emitter (a short stretched line).</summary>
        public int effect;
        /// <summary>One streak per this many degrees of the arc, at least <see cref="minStreaks"/> and at most <see cref="maxStreaks"/>.</summary>
        public float degreesPerStreak;
        public int minStreaks;
        public int maxStreaks;
        /// <summary>Streaks sit alternately at these shares of the radius (equal for one line).</summary>
        public float outerReach;
        public float innerReach;
        /// <summary>Speed (m/s) the streaks fly along the swing, their width (m) and the leading streak's life (s).</summary>
        public float speed;
        public float width;
        public float seconds;
        /// <summary>The trailing streak lives this share of the leading one's life, so the streaks read as a sweep.</summary>
        public float trailingShare;
    }

    /// <summary>
    /// Wind streaks along a melee swing's arc, flying the way the blade sweeps; the streak at the leading end lives
    /// longest, so the sweep reads. A full circle spreads them evenly around. Main thread; no allocations.
    /// </summary>
    public static class SweepStreaks
    {
        private const float QUARTER_TURN = 90.0f;
        /// <summary>An arc this wide (or wider) is a full circle: the data writes 360 and this margin absorbs rounding.</summary>
        public const float FULL_CIRCLE = 359.0f;

        /// <param name="sweepSign">-1 sweeps toward rising angles (like <see cref="Ground.Rotate"/>), +1 back.</param>
        public static void Draw(Effects effects, Vector3 center, Vector3 direction, float radius, float arc, float sweepSign, Color color, in SweepStyle style)
        {
            bool isFull = arc >= FULL_CIRCLE;
            int count = Math.Clamp((int)(arc / style.degreesPerStreak), style.minStreaks, style.maxStreaks);
            float step = isFull ? arc / count : arc / (count - 1);
            float start = isFull ? 0.0f : -arc * 0.5f;
            for (int k = 0; k < count; k++)
            {
                float order = (float)k / (count - 1);
                float lead = sweepSign < 0.0f ? order : 1.0f - order;
                Vector3 outward = Ground.Rotate(direction, start + step * k);
                Vector3 along = Ground.Rotate(outward, -QUARTER_TURN * sweepSign);
                float reach = k % 2 == 0 ? style.outerReach : style.innerReach;
                float lifetime = style.seconds * (style.trailingShare + (1.0f - style.trailingShare) * lead);
                effects.EmitMoving(style.effect, center + outward * (radius * reach), along * style.speed, 1, color, style.width, lifetime);
            }
        }
    }
}

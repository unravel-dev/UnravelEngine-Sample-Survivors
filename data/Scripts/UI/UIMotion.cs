using System;

namespace Survivors
{
    /// <summary>Easing curves for motion the screens compute in C# (RCSS animations name their own curves).</summary>
    internal static class UIMotion
    {
        /// <summary>Fast start, soft landing: 1 - (1 - t)^3 for t in 0..1 (clamped).</summary>
        public static float CubicOut(float t)
        {
            float remaining = 1.0f - Math.Clamp(t, 0.0f, 1.0f);
            return 1.0f - remaining * remaining * remaining;
        }
    }
}

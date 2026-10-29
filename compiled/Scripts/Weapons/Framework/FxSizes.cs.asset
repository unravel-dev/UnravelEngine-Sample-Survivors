namespace Survivors
{
    /// <summary>
    /// Sizes and offsets the weapon, kit and trait effects share, so the same texture reads the same everywhere.
    /// </summary>
    public static class FxSizes
    {
        /// <summary>Height ground decals (rings, sigils, cracks) float above the ground so they never sink into it.</summary>
        public const float GROUND_LIFT = 0.05f;
        /// <summary>A slightly lower lift: decals drawn with it sort under those at <see cref="GROUND_LIFT"/> where both overlap.</summary>
        public const float LOW_GROUND_LIFT = 0.04f;
        /// <summary>The ring texture (circle_02) draws its line at 0.29 of the particle size: this size per metre of radius puts the line on the radius.</summary>
        public const float RING_SIZE_PER_RADIUS = 3.4f;
        /// <summary>The Barbarian shock rings (shock_ring, circle_04) sit at about 0.45 of the particle size: this size per metre of radius puts them on the radius.</summary>
        public const float SHOCK_RING_SIZE_PER_RADIUS = 2.2f;
        /// <summary>The rune circle texture has its outer ring at 0.46 of the particle size: this size per metre of radius puts it on the radius.</summary>
        public const float SIGIL_SIZE_PER_RADIUS = 2.17f;
        /// <summary>EmitMoving speed of particles that only need a heading (cracks, crescents): they keep their place.</summary>
        public const float HEADING_SPEED = 0.01f;
        /// <summary>The size unit of a layered effect whose layer sizes are written in metres (FxComposite.Play).</summary>
        public const float METRE_UNIT = 1.0f;
    }
}

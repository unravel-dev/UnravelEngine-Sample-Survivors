using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Constants several systems share: enemy body height, knockback, telegraph height and colour. Balance numbers of
    /// weapons, enemies and drops live with their data; the enemy world keeps its own look and rules.
    /// </summary>
    public static class Tuning
    {
        /// <summary>Chest height of an enemy at scale 1 (m), for hit effects and damage numbers.</summary>
        public const float ENEMY_BODY_HEIGHT = 1.2f;
        /// <summary>Knockback speed (m/s) per metre a knockback should push; weapon data names distances.</summary>
        public const float KNOCKBACK_SPEED_PER_METRE = 10.0f;

        /// <summary>Height (m) ground decals and telegraphs float at, above the floor.</summary>
        public const float TELEGRAPH_HEIGHT = 0.06f;
        /// <summary>Enemy danger (telegraphs): red-orange; hero effects never use it.</summary>
        public static readonly Color DANGER_COLOR = new Color(1.0f, 0.29f, 0.16f, 1.0f);
    }
}

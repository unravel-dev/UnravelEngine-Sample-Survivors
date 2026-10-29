using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The Mage's palette and the particle heads of the Mage projectile looks (see ProjectileCatalog: one entry per look
    /// name). Every Mage projectile is particles only: Ember Orbs are a white-hot core in orange flame and a halo,
    /// Sunfire Orbs burn sun-gold under a corona, embers are small sun-gold flames, and meteors are a core in a huge
    /// flame and halo (the great meteor adds a sun-gold corona).
    /// </summary>
    internal static class MageLooks
    {
        /// <summary>Fire of the Mage's spells: muzzles, blasts, burns passed on, thermal shock's flames.</summary>
        internal static readonly Color FIRE = new Color(1.0f, 0.55f, 0.18f, 1.0f);
        /// <summary>The orange of the orbs' and meteors' flames.</summary>
        internal static readonly Color FLAME = new Color(1.0f, 0.5f, 0.15f, 1.0f);
        /// <summary>Sunfire: the evolved orb's muzzle, blasts and ember landings.</summary>
        internal static readonly Color SUN = new Color(1.0f, 0.82f, 0.45f, 1.0f);
        internal static readonly Color SUN_GOLD = new Color(1.0f, 0.8f, 0.42f, 1.0f);
        /// <summary>Lightning: a pale blue-white core over a blue glow.</summary>
        internal static readonly Color LIGHTNING_CORE = new Color(0.85f, 0.97f, 1.0f, 1.0f);
        internal static readonly Color LIGHTNING_GLOW = new Color(0.3f, 0.7f, 1.0f, 1.0f);

        internal static readonly (string look, bool particleOnly, ProjectileHead[] heads)[] heads =
        {
            ("EmberOrb", true, new[] { ProjectileCatalog.Core(0.38f), ProjectileCatalog.Flame(0.9f, FLAME), ProjectileCatalog.Halo(1.4f, FLAME) }),
            ("SunfireOrb", true, new[]
            {
                ProjectileCatalog.Core(0.45f), ProjectileCatalog.Flame(1.0f, SUN_GOLD), ProjectileCatalog.Corona(1.15f, SUN_GOLD),
                ProjectileCatalog.Halo(1.4f, SUN_GOLD),
            }),
            ("Ember", true, new[] { ProjectileCatalog.Core(0.2f), ProjectileCatalog.Flame(0.5f, SUN_GOLD), ProjectileCatalog.Halo(0.7f, SUN_GOLD) }),
            ("Meteor", true, new[] { ProjectileCatalog.Core(0.8f), ProjectileCatalog.Flame(2.0f, FLAME), ProjectileCatalog.Halo(3.0f, FLAME) }),
            ("GreatMeteor", true, new[]
            {
                ProjectileCatalog.Core(1.6f), ProjectileCatalog.Flame(4.0f, FLAME), ProjectileCatalog.Corona(4.5f, SUN_GOLD), ProjectileCatalog.Halo(6.0f, FLAME),
            }),
        };
    }
}

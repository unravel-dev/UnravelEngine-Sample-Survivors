using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The shared weapons' palette and the particle heads of their projectile looks (see ProjectileCatalog: one entry
    /// per look name). Spirit darts, wisps and the Eclipse Disc are particles only (a hot core, a coloured flame, wisp
    /// or ring, and a halo or sparkle); discs, javelins, flasks and the Thunderspire rod keep their mesh and wear a
    /// whirl, glint or flame. The bramble rings hide their mesh: BrambleHalo draws the bramble with particles around
    /// the (invisible) ring.
    /// </summary>
    internal static class SharedLooks
    {
        private static readonly Color WHITE = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        /// <summary>Spirit Darts' pale spirit blue.</summary>
        internal static readonly Color SPIRIT = new Color(0.65f, 0.85f, 1.0f, 1.0f);
        private static readonly Color SPIRIT_CORE = new Color(0.88f, 0.95f, 1.0f, 1.0f);
        /// <summary>Ricochet Disc's cyan and the Eclipse Disc's violet.</summary>
        internal static readonly Color DISC = new Color(0.55f, 0.95f, 1.0f, 1.0f);
        private static readonly Color DISC_GLINT = new Color(0.85f, 1.0f, 1.0f, 1.0f);
        internal static readonly Color ECLIPSE = new Color(0.62f, 0.4f, 1.0f, 1.0f);
        private static readonly Color ECLIPSE_RING = new Color(0.82f, 0.62f, 1.0f, 1.0f);
        private static readonly Color GOLD = new Color(1.0f, 0.8f, 0.4f, 1.0f);
        private static readonly Color GOLD_CORE = new Color(1.0f, 0.95f, 0.8f, 1.0f);
        private static readonly Color STEEL = new Color(0.75f, 0.85f, 1.0f, 1.0f);
        private static readonly Color STEEL_GLINT = new Color(0.92f, 0.96f, 1.0f, 1.0f);
        /// <summary>Fire Flask's flames and Hellfire Cask's deeper hellfire.</summary>
        internal static readonly Color FIRE = new Color(1.0f, 0.5f, 0.15f, 1.0f);
        internal static readonly Color HELLFIRE = new Color(1.0f, 0.32f, 0.08f, 1.0f);
        /// <summary>Lightning: Storm Totem's blue-white bolts over a blue glow, Thunderspire's violet-white over violet.</summary>
        internal static readonly Color LIGHTNING_CORE = new Color(0.85f, 0.97f, 1.0f, 1.0f);
        internal static readonly Color LIGHTNING_GLOW = new Color(0.3f, 0.7f, 1.0f, 1.0f);
        internal static readonly Color THUNDER = new Color(0.9f, 0.85f, 1.0f, 1.0f);
        internal static readonly Color THUNDER_GLOW = new Color(0.55f, 0.45f, 1.0f, 1.0f);
        /// <summary>Hunting Wisps' mint spirit flame.</summary>
        internal static readonly Color WISP = new Color(0.55f, 1.0f, 0.85f, 1.0f);
        private static readonly Color WISP_CORE = new Color(0.88f, 1.0f, 0.95f, 1.0f);
        private static readonly Color CHOIR = new Color(1.0f, 0.95f, 0.75f, 1.0f);
        private static readonly Color CHOIR_GOLD = new Color(1.0f, 0.82f, 0.5f, 1.0f);

        private static ProjectileHead Wisp(float size, Color color) => new ProjectileHead("SharedHeadWisp", size, color);

        private static ProjectileHead Whirl(float size, Color color) => new ProjectileHead("SharedHeadWhirl", size, color);

        private static ProjectileHead WispFlame(float size, Color color) => new ProjectileHead("SharedHeadWispFlame", size, color);

        private static ProjectileHead Crackle(float size, Color color) => new ProjectileHead("SharedStormWeb", size, color);

        /// <summary>The Eclipse Disc's black sun keeps its own colour (no tint).</summary>
        private static ProjectileHead EclipseCore(float size) => new ProjectileHead("SharedHeadEclipseCore", size, WHITE, false);

        private static ProjectileHead EclipseRing(float size, Color color) => new ProjectileHead("SharedHeadEclipseRing", size, color);

        /// <summary>The Hellfire Cask's dark smoke keeps its own colour (no tint).</summary>
        private static ProjectileHead Smoke(float size) => new ProjectileHead("SharedHeadSmoke", size, WHITE, false);

        internal static readonly (string look, bool particleOnly, ProjectileHead[] heads)[] heads =
        {
            // Spirit dart: a cold core, a spinning glint and a ghostly wisp tail (Soul Hail recolours it per spawn).
            ("SpiritDart", true, new[] { ProjectileCatalog.Core(0.16f, SPIRIT_CORE), ProjectileCatalog.Star(0.42f, SPIRIT), Wisp(0.5f, SPIRIT) }),
            // Ricochet disc: the shield mesh in a flat blade-whirl, its halo and a glint.
            ("RicochetDisc", false, new[] { Whirl(1.15f, DISC), ProjectileCatalog.Halo(0.85f, DISC), ProjectileCatalog.Star(0.38f, DISC_GLINT) }),
            // Eclipse disc: a black sun ringed in violet fire inside a violet whirl.
            ("EclipseDisc", true, new[] { EclipseCore(0.42f), EclipseRing(0.72f, ECLIPSE_RING), Whirl(1.1f, ECLIPSE) }),
            // Skewer javelin: the javelin mesh with a cold steel glint and a halo that lights the ground under it.
            ("SkewerJavelin", false, new[] { ProjectileCatalog.Star(0.45f, STEEL_GLINT), ProjectileCatalog.Halo(0.75f, STEEL) }),
            // Godspear: the gold spear with a white-gold core, a corona and a halo that lights the ground.
            ("Godspear", false, new[] { ProjectileCatalog.Core(0.28f, GOLD_CORE), ProjectileCatalog.Corona(0.7f, GOLD), ProjectileCatalog.Halo(0.9f, GOLD) }),
            // Fire flask: a burning wick and its glow.
            ("FireFlask", false, new[] { ProjectileCatalog.Flame(0.55f, FIRE), ProjectileCatalog.Halo(0.9f, FIRE) }),
            // Bramble rings: no mesh and no heads (BrambleHalo draws vines, thorns and leaves around the hero).
            ("BrambleRing", true, Array.Empty<ProjectileHead>()),
            ("BriarRing", true, Array.Empty<ProjectileHead>()),
            // Hellfire cask: bigger hellfire flames rolling black smoke.
            ("HellfireCask", false, new[] { ProjectileCatalog.Flame(0.65f, HELLFIRE), Smoke(0.6f), ProjectileCatalog.Halo(0.9f, HELLFIRE) }),
            // Thunderspire rod: crackling violet-white static around a white core.
            ("Thunderspire", false, new[] { ProjectileCatalog.Core(0.25f, THUNDER), Crackle(0.7f, THUNDER), ProjectileCatalog.Halo(0.85f, THUNDER_GLOW) }),
            // Wisp: a mint spirit flame around a pale core.
            ("Wisp", true, new[] { ProjectileCatalog.Core(0.2f, WISP_CORE), WispFlame(0.8f, WISP), ProjectileCatalog.Halo(1.0f, WISP) }),
            // Choir wisp: a warm white flame crowned with golden rays.
            ("ChoirWisp", true, new[] { ProjectileCatalog.Core(0.2f, CHOIR), WispFlame(0.7f, CHOIR), ProjectileCatalog.Corona(0.75f, CHOIR_GOLD) }),
        };
    }
}

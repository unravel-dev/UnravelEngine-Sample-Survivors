using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The Rogue's palette, the particle heads of the Rogue projectile looks (see ProjectileCatalog: one per look
    /// name) and the knife cut every Rogue blade leaves where it strikes. Bolts and knives keep their meshes and add
    /// steel glints; Widowmaker bolts shed venom crystals; shadow daggers burn with violet shadow flame. Venom Shivs
    /// throw the knife look with its glint tinted venom green (Nightshade Plague: berry violet), and Shadow Dancers'
    /// clones throw shadow daggers. Assassin's Mark has no projectile over its marks: it keeps a refreshed crimson
    /// reticle over each instead, so no glowing ring or red light pool can read as an enemy telegraph.
    /// </summary>
    internal static class RogueLooks
    {
        /// <summary>A knife cut drifts this fast (m/s) along the blade's flight and lasts this long.</summary>
        private const float CUT_DRIFT = 0.5f;
        private const float CUT_SECONDS = 0.12f;

        /// <summary>Cold steel: crossbow, knives, sparks, glints.</summary>
        internal static readonly Color STEEL = new Color(0.78f, 0.88f, 1.0f, 1.0f);
        internal static readonly Color STEEL_WHITE = new Color(0.92f, 0.96f, 1.0f, 1.0f);
        /// <summary>Poison green: venom on bolts, Widowmaker's crystals, Smoke Roll's loaded crits.</summary>
        internal static readonly Color VENOM = new Color(0.45f, 1.0f, 0.42f, 1.0f);
        internal static readonly Color VENOM_CRYSTAL = new Color(0.68f, 1.0f, 0.74f, 1.0f);
        /// <summary>Shadow violet: clones, daggers, smoke, Storm of Steel, full Momentum.</summary>
        internal static readonly Color SHADOW_VIOLET = new Color(0.58f, 0.32f, 1.0f, 1.0f);
        internal static readonly Color PALE_VIOLET = new Color(0.85f, 0.72f, 1.0f, 1.0f);
        /// <summary>Storm of Steel: steel washed with violet.</summary>
        internal static readonly Color STORM_STEEL = new Color(0.8f, 0.7f, 1.0f, 1.0f);
        /// <summary>Crimson: Assassin's Mark and Death Sentence only (leaning magenta, away from telegraph red-orange).</summary>
        internal static readonly Color CRIMSON = new Color(0.92f, 0.06f, 0.24f, 1.0f);
        /// <summary>Death Sentence's rune lines: deep crimson-magenta.</summary>
        internal static readonly Color SENTENCE_RUNE = new Color(0.75f, 0.05f, 0.25f, 1.0f);
        /// <summary>Venom Shivs: the shiv glint and the bright splash of a rupture.</summary>
        internal static readonly Color SHIV_GLINT = new Color(0.6f, 1.0f, 0.55f, 1.0f);
        /// <summary>Nightshade Plague: berry violet blooming out of the venom green.</summary>
        internal static readonly Color NIGHTSHADE = new Color(0.72f, 0.3f, 1.0f, 1.0f);
        /// <summary>Shadow Dancers: the clones' rim and eyes, and Legion of Shadows' brighter rim and shadow flames.</summary>
        internal static readonly Color DANCER_RIM = new Color(0.62f, 0.38f, 1.0f, 1.0f);
        internal static readonly Color DANCER_EYES = new Color(0.86f, 0.78f, 1.0f, 1.0f);
        internal static readonly Color LEGION_RIM = new Color(0.74f, 0.5f, 1.0f, 1.0f);

        internal static readonly (string look, bool particleOnly, ProjectileHead[] heads)[] heads =
        {
            ("CrossbowBolt", false, new[] { ProjectileCatalog.Core(0.14f, STEEL_WHITE), ProjectileCatalog.Halo(0.55f, STEEL) }),
            ("WidowBolt", false, new[]
            {
                ProjectileCatalog.Core(0.18f, STEEL_WHITE), ProjectileCatalog.Halo(0.6f, VENOM), new ProjectileHead("RogueCrystalTrail", 0.22f, VENOM_CRYSTAL),
            }),
            ("ThrownKnife", false, new[] { new ProjectileHead("RogueKnifeHead", 0.34f, STEEL) }),
            ("ShadowDagger", false, new[] { ProjectileCatalog.Core(0.14f, PALE_VIOLET), ProjectileCatalog.Flame(0.45f, SHADOW_VIOLET) }),
        };

        /// <summary>A crisp cut across a blade's path where it strikes (the RogueKnifeCut emitter), drifting along its flight.</summary>
        internal static void EmitKnifeCut(Effects effects, int knifeCut, Vector3 impact, Vector3 travel, Color color, float size)
        {
            effects.EmitMoving(knifeCut, impact, travel * CUT_DRIFT, 1, color, size, CUT_SECONDS);
        }
    }
}

using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The Knight's palette and the particle heads of the Knight projectile looks (see ProjectileCatalog: one entry per
    /// look name). Aegis shields keep their mesh clear: their only head is a short gold wake that begins behind them
    /// (AegisOrbit adds rim glints). Verdict swords keep their mesh inside a holy glow that streams motes; reflected
    /// shots are bolts of light. The crescent wave carries no head: OathbladeArc draws its afterimages turned along its
    /// flight. A Dawn Hammer is two glowing gold looks spinning flat together about the head: the head (the embedded
    /// cylinder laid across the haft) inside a halo and a stream of motes, and the haft (the adventurers' wand, pivoted
    /// at its grip, laid back from the head, its tip a spike through the head); Skybreaker's hammer is larger and
    /// paler, in a storm-azure halo crackling with lightning.
    /// </summary>
    internal static class KnightLooks
    {
        /// <summary>Spin of a Dawn Hammer about its head (degrees per second); DawnHammer's whirl follows the same angle.</summary>
        internal const float HAMMER_SPIN = 600.0f;
        /// <summary>Height above the feet the Knight's effects treat as the chest (hit glints, flashes, the shields' orbit).</summary>
        internal const float CHEST_HEIGHT = 1.0f;

        /// <summary>The Knight's gold: hammers, glints, sparks.</summary>
        internal static readonly Color GOLD = new Color(1.0f, 0.8f, 0.4f, 1.0f);
        internal static readonly Color PALE_GOLD = new Color(1.0f, 0.88f, 0.55f, 1.0f);
        /// <summary>Holy light: motes, cross flares, faded shields.</summary>
        internal static readonly Color HOLY = new Color(1.0f, 0.95f, 0.8f, 1.0f);
        /// <summary>A whiter holy light: blade glints, the cross flare of a block.</summary>
        internal static readonly Color HOLY_LIGHT = new Color(1.0f, 0.96f, 0.86f, 1.0f);
        /// <summary>The evolutions' celestial azure.</summary>
        internal static readonly Color AZURE = new Color(0.72f, 0.88f, 1.0f, 1.0f);
        /// <summary>Skybreaker's pale gold motes.</summary>
        internal static readonly Color SKY_GOLD = new Color(1.0f, 0.82f, 0.5f, 1.0f);
        /// <summary>Skybreaker's storm light.</summary>
        internal static readonly Color STORM = new Color(0.66f, 0.84f, 1.0f, 1.0f);

        internal static readonly (string look, bool particleOnly, ProjectileHead[] heads)[] heads =
        {
            ("AegisShield", false, new[] { new ProjectileHead("KnightShieldWake", 0.45f, GOLD) }),
            ("AegisReflect", true, new[] { ProjectileCatalog.Core(0.3f), ProjectileCatalog.Star(0.8f, AZURE), ProjectileCatalog.Halo(1.2f, PALE_GOLD) }),
            ("VerdictSword", false, new[] { ProjectileCatalog.Halo(1.8f, PALE_GOLD), new ProjectileHead("KnightSwordAura", 0.35f, HOLY) }),
            // Dawn Hammer: a gold halo that lights the ground and motes falling away.
            ("KnightDawnHammer", false, new[] { ProjectileCatalog.Halo(0.8f, GOLD), new ProjectileHead("KnightSwordAura", 0.24f, HOLY) }),
            // Skybreaker: a storm-azure halo, lightning crackling over the head and pale motes falling away.
            ("KnightSkyHammer", false, new[]
            {
                ProjectileCatalog.Halo(0.9f, STORM), new ProjectileHead("KnightHammerCrackle", 0.7f, STORM), new ProjectileHead("KnightSwordAura", 0.26f, SKY_GOLD),
            }),
        };
    }
}

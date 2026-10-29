using System;
using System.Globalization;
using System.Text;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Who an item belongs to, as a tag: "KNIGHT UNIQUE", "SHARED", "SHARED PASSIVE", "BLESSING".</summary>
    internal struct Ownership
    {
        /// <summary>Classes of a tag element that shows nothing.</summary>
        public const string NO_TAG_CLASSES = "own own-none";

        public string text;
        /// <summary>Classes of the tag element: "own own-knight" and the like.</summary>
        public string classes;
        /// <summary>Emblem image key: the hero's badge, or the shared glyph.</summary>
        public string emblem;

        public bool isEmpty => string.IsNullOrEmpty(text);

        /// <summary>The classes to write: <see cref="NO_TAG_CLASSES"/> (hidden) for no tag.</summary>
        public string tagClasses => isEmpty ? NO_TAG_CLASSES : classes;

        /// <summary>The emblem to write: a placeholder for no tag (the tag is hidden).</summary>
        public string tagEmblem => isEmpty ? UIText.Icon(UIText.PLACEHOLDER_ICON) : emblem;
    }

    /// <summary>
    /// Text helpers for the screens: cached integer strings (no allocation per frame for counters and damage
    /// numbers), times, thousands, RML escaping, the card description formatter, rarity lines and ownership tags.
    /// Main thread.
    /// </summary>
    internal static class UIText
    {
        public const string GLYPH_ROOT = "app:/data/UI/Glyphs/";
        /// <summary>Icon shown where an item has no icon of its own.</summary>
        public const string PLACEHOLDER_ICON = "skull";
        private const string SHARED_EMBLEM = "massiveMultiplayer";
        private const int CACHE_SIZE = 20000;
        private const char LINE_BREAK = (char)10;
        private const char CARRIAGE_RETURN = (char)13;

        /// <summary>Frame classes of cards and reels, indexed by <see cref="Rarity"/>; theme.rcss colours them.</summary>
        public static readonly string[] FRAME_CLASSES =
        {
            "r-common", "r-uncommon", "r-rare", "r-epic", "r-evolution", "r-boon",
        };

        private static readonly string[] plain = new string[CACHE_SIZE];
        private static readonly string[] crit = new string[CACHE_SIZE];
        private static readonly string[] plus = new string[CACHE_SIZE];
        private static readonly StringBuilder idBuilder = new StringBuilder(64);
        private static readonly StringBuilder rmlBuilder = new StringBuilder(256);

        /// <summary>"1234" from a cache for 0..19999.</summary>
        public static string Int(int value)
        {
            if (value < 0 || value >= CACHE_SIZE)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
            return plain[value] ??= value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>"1234!" for critical hits.</summary>
        public static string Crit(int value)
        {
            if (value < 0 || value >= CACHE_SIZE)
            {
                return value.ToString(CultureInfo.InvariantCulture) + "!";
            }
            return crit[value] ??= value.ToString(CultureInfo.InvariantCulture) + "!";
        }

        /// <summary>"+12" for heals.</summary>
        public static string Plus(int value)
        {
            if (value < 0 || value >= CACHE_SIZE)
            {
                return "+" + value.ToString(CultureInfo.InvariantCulture);
            }
            return plus[value] ??= "+" + value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>"12,345".</summary>
        public static string Thousands(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>"MM:SS".</summary>
        public static string Time(float seconds)
        {
            int total = Math.Max(0, (int)seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>A compact number: 950, 12.4k, 1.2M.</summary>
        public static string Compact(float value)
        {
            if (value >= 1000000.0f)
            {
                return (value / 1000000.0f).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            }
            if (value >= 10000.0f)
            {
                return (value / 1000.0f).ToString("0.0", CultureInfo.InvariantCulture) + "k";
            }
            return Thousands((long)MathF.Round(value));
        }

        /// <summary>The icon asset key of an item, hero or UI id (<see cref="WeaponCatalog.IconKey"/>).</summary>
        public static string Icon(string id) => WeaponCatalog.IconKey(id);

        public static string Glyph(string name) => GLYPH_ROOT + name + ".png";

        /// <summary>
        /// An icon asset key: <paramref name="key"/> when it is a full key ("app:/data/UI/Icons/x.png"), the icon
        /// of <paramref name="key"/> or <paramref name="id"/> when either is a bare id, else the placeholder icon.
        /// </summary>
        public static string IconOr(string key, string id)
        {
            if (!string.IsNullOrEmpty(key))
            {
                return IsAssetKey(key) ? key : Icon(key);
            }
            if (string.IsNullOrEmpty(id))
            {
                return Icon(PLACEHOLDER_ICON);
            }
            return IsAssetKey(id) ? id : Icon(id);
        }

        /// <summary>Whether a string is a full asset key ("app:/...", "engine:/...") rather than a bare id.</summary>
        public static bool IsAssetKey(string text) => text.IndexOf(":/", StringComparison.Ordinal) > 0;

        /// <summary>snake_case id of a display name, apostrophes dropped: "Assassin's Mark" -> assassins_mark.</summary>
        public static string ToId(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            idBuilder.Clear();
            bool underscore = false;
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (underscore && idBuilder.Length > 0)
                    {
                        idBuilder.Append('_');
                    }
                    idBuilder.Append(char.ToLowerInvariant(c));
                    underscore = false;
                }
                else if (c != '\'')
                {
                    underscore = true;
                }
            }
            return idBuilder.ToString();
        }

        /// <summary>Escapes text for InnerRml.</summary>
        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        /// <summary>
        /// A card description as RML: one line per line break; in every ", "-separated part such as
        /// "Might +16% -> +24%" the old value dims, the arrow turns gold and the new value glows green.
        /// </summary>
        public static string FormatDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return "";
            }
            rmlBuilder.Clear();
            string[] lines = description.Split(LINE_BREAK);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    rmlBuilder.Append("<br/>");
                }
                string[] parts = lines[i].TrimEnd(CARRIAGE_RETURN).Split(", ");
                for (int p = 0; p < parts.Length; p++)
                {
                    if (p > 0)
                    {
                        rmlBuilder.Append(", ");
                    }
                    AppendPart(parts[p]);
                }
            }
            return rmlBuilder.ToString();
        }

        private static void AppendPart(string part)
        {
            int arrow = part.IndexOf("->", StringComparison.Ordinal);
            if (arrow < 0)
            {
                rmlBuilder.Append(Escape(part));
                return;
            }
            string left = part.Substring(0, arrow).TrimEnd();
            string right = part.Substring(arrow + 2).TrimStart();
            int split = FindValueStart(left);
            rmlBuilder.Append(Escape(left.Substring(0, split)));
            rmlBuilder.Append("<span class=\"old\">").Append(Escape(left.Substring(split))).Append("</span>");
            rmlBuilder.Append(" <span class=\"arrow\">&gt;</span> ");
            rmlBuilder.Append("<span class=\"new\">").Append(Escape(right)).Append("</span>");
        }

        /// <summary>Where the old value of "Might +16%" starts: at the first digit or sign.</summary>
        private static int FindValueStart(string left)
        {
            for (int i = 0; i < left.Length; i++)
            {
                char c = left[i];
                if (char.IsDigit(c) || c == '+' || c == '-')
                {
                    return i;
                }
            }
            return left.Length;
        }

        /// <summary>"COMMON WEAPON", "EPIC PASSIVE", "EVOLUTION", "BLESSING", "BOON".</summary>
        public static string RarityLine(UpgradeOffer offer)
        {
            switch (offer.kind)
            {
                case UpgradeOffer.Kind.Evolution:
                    return "EVOLUTION";
                case UpgradeOffer.Kind.Fallback:
                    return "BLESSING";
                case UpgradeOffer.Kind.Boon:
                    return "BOON";
            }
            string rarity = offer.rarity.ToString().ToUpperInvariant();
            return rarity + (offer.kind == UpgradeOffer.Kind.Weapon ? " WEAPON" : " PASSIVE");
        }

        /// <summary>"NEW", "RANK 3 > 4", "EVOLVE", "BLESSING", "BOON x2".</summary>
        public static string RankTag(UpgradeOffer offer)
        {
            switch (offer.kind)
            {
                case UpgradeOffer.Kind.Evolution:
                    return "EVOLVE";
                case UpgradeOffer.Kind.Fallback:
                    return "BLESSING";
                case UpgradeOffer.Kind.Boon:
                    return offer.isNew ? "BOON" : $"BOON x{offer.toRank}";
            }
            if (offer.isNew)
            {
                return "NEW";
            }
            return $"RANK {offer.fromRank} > {offer.toRank}";
        }

        /// <summary>A card's or chest reel's frame class: evolution gold, boon teal, else the rarity's.</summary>
        public static string OfferFrameClass(UpgradeOffer offer)
        {
            switch (offer.kind)
            {
                case UpgradeOffer.Kind.Evolution:
                    return "r-evolution";
                case UpgradeOffer.Kind.Boon:
                    return "r-boon";
            }
            return FrameClass(offer.rarity);
        }

        public static string FrameClass(Rarity rarity)
        {
            int index = (int)rarity;
            return index >= 0 && index < FRAME_CLASSES.Length ? FRAME_CLASSES[index] : FRAME_CLASSES[0];
        }

        /// <summary>Upper-case class name from <see cref="HeroCatalog"/> ("KNIGHT").</summary>
        public static string HeroName(HeroClass hero)
        {
            HeroInfo info = HeroCatalog.Get(hero);
            string name = info != null && !string.IsNullOrEmpty(info.name) ? info.name : hero.ToString();
            return name.ToUpperInvariant();
        }

        /// <summary>Lower-case class id of icons and classes ("knight": knight.png, own-knight).</summary>
        public static string HeroId(HeroClass hero) => hero.ToString().ToLowerInvariant();

        /// <summary>A tag for something of one class: "KNIGHT UNIQUE", "MAGE ACTIVE".</summary>
        public static Ownership ForHero(HeroClass hero, string suffix)
        {
            string id = HeroId(hero);
            return new Ownership
            {
                text = HeroName(hero) + " " + suffix,
                classes = "own own-" + id,
                emblem = Icon(id),
            };
        }

        /// <summary>
        /// Whose item an id is: class weapons (and their evolutions) are "[HERO] UNIQUE" in the class colour,
        /// shared weapons "SHARED", passives "SHARED PASSIVE", boons "NO SLOT - UNLIMITED", fallback cards
        /// "BLESSING"; unknown ids give no tag.
        /// </summary>
        public static Ownership GetOwnership(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return default;
            }
            WeaponDef weapon = WeaponCatalog.FindWeapon(id) ?? WeaponCatalog.FindWeaponByEvolution(id);
            if (weapon != null)
            {
                return weapon.isClassWeapon ? ForHero(weapon.heroClass, "UNIQUE") :
                       new Ownership { text = "SHARED", classes = "own own-shared", emblem = Glyph(SHARED_EMBLEM) };
            }
            if (WeaponCatalog.FindPassive(id) != null)
            {
                return new Ownership { text = "SHARED PASSIVE", classes = "own own-passive", emblem = Glyph(SHARED_EMBLEM) };
            }
            if (WeaponCatalog.FindBoon(id) != null)
            {
                return new Ownership { text = "NO SLOT - UNLIMITED", classes = "own own-boon", emblem = Glyph("plus") };
            }
            if (WeaponCatalog.FindFallback(id) != null)
            {
                return new Ownership { text = "BLESSING", classes = "own own-blessing", emblem = Icon("gold") };
            }
            return default;
        }

        /// <summary>Fills a tag element of the form &lt;div class="own"&gt;&lt;img class="own-emblem"/&gt;&lt;span class="own-text"/&gt;&lt;/div&gt;.</summary>
        public static void ApplyOwnership(UIElement tag, Ownership ownership)
        {
            if (tag == null)
            {
                return;
            }
            tag.SetAttribute("class", ownership.tagClasses);
            if (ownership.isEmpty)
            {
                return;
            }
            tag.Q(".own-emblem")?.SetAttribute("src", ownership.emblem);
            tag.Q(".own-text")?.SetText(ownership.text);
        }
    }
}

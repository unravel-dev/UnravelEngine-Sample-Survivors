using System;
using System.Globalization;
using System.Text;

namespace Survivors
{
    /// <summary>
    /// Card and HUD text for weapons, passives, boons and fallback cards: numbers with units, old -> new. Card text
    /// puts one change per line ("Damage 14 -> 18"), the form the level-up screen styles; summaries are one comma list.
    /// Every number is written with the invariant culture.
    /// </summary>
    public static class WeaponText
    {
        private const string ARROW = " -> ";
        private const string SEPARATOR = ", ";
        private const string LINE = "\n";
        /// <summary>Columns a short summary (HUD, pause sheet) shows.</summary>
        private const int SUMMARY_COLUMNS = 4;
        /// <summary>A new weapon's card has room for its description and this many numbers; the details screen shows them all.</summary>
        private const int NEW_CARD_COLUMNS = 2;
        private const float EPSILON = 1e-4f;
        /// <summary>Passive bonuses show whole percents; boons are small and show one decimal more.</summary>
        private const int PASSIVE_DECIMALS = 0;
        private const int BOON_DECIMALS = 1;

        private static readonly CultureInfo invariant = CultureInfo.InvariantCulture;
        /// <summary>Number formats by decimals shown (at most).</summary>
        private static readonly string[] decimalFormats = { "0", "0.#", "0.##" };

        /// <summary>A number as cards write it: "2.5", "40".</summary>
        public static string Number(float value, string format = "0.#") => value.ToString(format, invariant);

        /// <summary>A share as a percent: 0.025 -> "2.5%", -0.003 -> "-0.3%".</summary>
        public static string Percent(float share, string format = "0.#") => Number(share * 100.0f, format) + "%";

        /// <summary>A number that grows with the hero's level: "15 + level", "40 + 4 x level".</summary>
        public static string PlusPerLevel(float baseValue, float perLevel)
        {
            string growth = perLevel == 1.0f ? "level" : Number(perLevel) + " x level";
            return Number(baseValue) + " + " + growth;
        }

        /// <summary>A field value with its unit ("1.10 s", "2.8 m", "35%", "all").</summary>
        public static string FormatValue(WeaponField field, float value)
        {
            switch (field)
            {
                case WeaponField.Cooldown:
                case WeaponField.Interval:
                case WeaponField.Stagger:
                case WeaponField.SubInterval:
                case WeaponField.BurnTick:
                    return Number(value, "0.00") + " s";
                case WeaponField.Duration:
                    return value < 0.0f ? "permanent" : Number(value, "0.0") + " s";
                case WeaponField.Stun:
                case WeaponField.Fear:
                case WeaponField.Freeze:
                case WeaponField.SubDuration:
                case WeaponField.MajorDelay:
                    return Number(value, "0.0") + " s";
                case WeaponField.Radius:
                case WeaponField.Range:
                case WeaponField.Width:
                case WeaponField.Knockback:
                case WeaponField.SubRadius:
                case WeaponField.SubRange:
                case WeaponField.MajorRadius:
                    return Number(value) + " m";
                case WeaponField.Size:
                    return Number(value, "0.00") + " m";
                case WeaponField.Angle:
                    return Number(value, "0") + " deg";
                case WeaponField.Speed:
                case WeaponField.SubSpeed:
                    return Number(value, "0") + " m/s";
                case WeaponField.Chance:
                case WeaponField.Slow:
                case WeaponField.Bonus:
                case WeaponField.BonusCap:
                case WeaponField.CritChance:
                case WeaponField.CritDamage:
                case WeaponField.MoveSpeed:
                    return Percent(value, "0");
                case WeaponField.Pierce:
                    return value < 0.0f ? "all" : Number(value, "0");
                case WeaponField.SubCount:
                    return value < 0.0f ? "unlimited" : Number(value, "0");
                case WeaponField.Stacks:
                    return value < 0.0f ? "no cap" : Number(value, "0");
                default:
                    return Number(value);
            }
        }

        /// <summary>The first columns of a level: "Damage 14, Cooldown 1.10 s, Radius 2.8 m, Swings 1".</summary>
        public static string DescribeLevel(WeaponDef def, WeaponLevel level, int columns = SUMMARY_COLUMNS)
        {
            var text = new StringBuilder();
            int shown = Math.Min(columns, def.columns.Length);
            for (int i = 0; i < shown; i++)
            {
                WeaponColumn column = def.columns[i];
                Append(text, column.label + " " + FormatValue(column.field, level[column.field]), SEPARATOR);
            }
            return text.ToString();
        }

        /// <summary>A NEW weapon card: what it does, then its rank 1 numbers.</summary>
        public static string DescribeNewWeapon(WeaponDef def)
        {
            return def.description + LINE + DescribeLevel(def, def.GetLevel(1), NEW_CARD_COLUMNS) + ".";
        }

        /// <summary>A rank-up card: every column that changes, old -> new, then the rank's note.</summary>
        public static string DescribeRankUp(WeaponDef def, int fromRank, int toRank)
        {
            WeaponLevel from = def.GetLevel(fromRank);
            WeaponLevel to = def.GetLevel(toRank);
            var text = new StringBuilder();
            foreach (WeaponColumn column in def.columns)
            {
                float before = from[column.field];
                float after = to[column.field];
                if (MathF.Abs(after - before) > EPSILON)
                {
                    Append(text, column.label + " " + FormatValue(column.field, before) + ARROW + FormatValue(column.field, after), LINE);
                }
            }
            string note = def.GetNote(toRank);
            if (note.Length > 0)
            {
                Append(text, note, LINE);
            }
            return text.Length > 0 ? text.ToString() : "Stronger.";
        }

        /// <summary>A passive's bonus at a rank: "Max Health +20%, Regen +0.4 HP/s".</summary>
        public static string DescribePassive(PassiveDef def, int rank)
        {
            var text = new StringBuilder();
            foreach (PassiveEffect effect in def.effects)
            {
                Append(text, effect.label + " " + FormatBonus(effect, effect.perRank * rank, PASSIVE_DECIMALS), SEPARATOR);
            }
            return text.ToString();
        }

        /// <summary>A passive at a rank with its trait sentence (pause sheet).</summary>
        public static string DescribePassiveWithTrait(PassiveDef def, int rank)
        {
            string trait = PassiveTraits.Describe(def.id, rank);
            return trait.Length > 0 ? DescribePassive(def, rank) + ". " + trait : DescribePassive(def, rank);
        }

        /// <summary>A passive card: "Might +16% -> +24%" (or the rank 1 bonus when new).</summary>
        public static string DescribePassiveChange(PassiveDef def, int fromRank, int toRank)
        {
            string trait = PassiveTraits.Describe(def.id, toRank);
            if (fromRank <= 0)
            {
                return trait.Length > 0 ? DescribePassive(def, toRank) + "." + LINE + trait : DescribePassive(def, toRank) + ".";
            }
            var text = new StringBuilder();
            foreach (PassiveEffect effect in def.effects)
            {
                string before = FormatBonus(effect, effect.perRank * fromRank, PASSIVE_DECIMALS);
                string after = FormatBonus(effect, effect.perRank * toRank, PASSIVE_DECIMALS);
                Append(text, effect.label + " " + before + ARROW + after, LINE);
            }
            if (trait.Length > 0)
            {
                Append(text, trait, LINE);
            }
            return text.ToString();
        }

        /// <summary>
        /// A boon card: the first pick's bonus and that it takes no slot, or every stat's total old -> new once taken.
        /// </summary>
        public static string DescribeBoon(BoonDef def, int picks)
        {
            var text = new StringBuilder();
            foreach (PassiveEffect effect in def.effects)
            {
                string line = picks <= 0
                    ? effect.label + " " + FormatBonus(effect, effect.perRank, BOON_DECIMALS)
                    : effect.label + " " + FormatBonus(effect, effect.perRank * BoonDef.GetTotalShare(picks), BOON_DECIMALS) + ARROW +
                      FormatBonus(effect, effect.perRank * BoonDef.GetTotalShare(picks + 1), BOON_DECIMALS);
                Append(text, line, LINE);
            }
            Append(text, picks <= 0 ? "Takes no slot and stacks without limit." : "Each pick adds a little less.", LINE);
            return text.ToString();
        }

        /// <summary>A boon's total after <paramref name="picks"/> picks: "Might +12.5%".</summary>
        public static string DescribeBoonTotal(BoonDef def, int picks)
        {
            var text = new StringBuilder();
            foreach (PassiveEffect effect in def.effects)
            {
                Append(text, effect.label + " " + FormatBonus(effect, effect.perRank * BoonDef.GetTotalShare(picks), BOON_DECIMALS), SEPARATOR);
            }
            return text.ToString();
        }

        /// <summary>What a fallback card gives, as a sentence: "Heal 30% of max health.", "+20 gold.".</summary>
        public static string DescribeFallback(FallbackKind kind, float amount)
        {
            switch (kind)
            {
                case FallbackKind.Heal:
                    return $"Heal {Percent(amount)} of max health.";
                case FallbackKind.Might:
                    return $"+{Percent(amount)} Might for the rest of the run.";
                default:
                    return $"+{Number(amount, "0")} gold.";
            }
        }

        /// <summary>
        /// A passive or boon bonus with its sign and unit ("+16%", "+0.4 HP/s"): percents with
        /// <paramref name="decimals"/> decimals at most, flat values with one more.
        /// </summary>
        private static string FormatBonus(PassiveEffect effect, float value, int decimals)
        {
            string sign = value >= 0.0f ? "+" : "-";
            float magnitude = MathF.Abs(value);
            if (effect.isPercent || effect.showsPercent)
            {
                return sign + Percent(magnitude, decimalFormats[decimals]);
            }
            return sign + Number(magnitude, decimalFormats[decimals + 1]) + effect.unit;
        }

        private static void Append(StringBuilder text, string part, string separator)
        {
            if (text.Length > 0)
            {
                text.Append(separator);
            }
            text.Append(part);
        }
    }
}

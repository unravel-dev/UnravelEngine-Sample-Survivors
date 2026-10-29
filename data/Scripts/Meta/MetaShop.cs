using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>One permanent Armory upgrade: a stat bonus per rank, bought with gold banked from runs.</summary>
    public sealed class MetaUpgrade
    {
        /// <summary>Stable id, also the icon name (app:/data/UI/Icons/[id].png).</summary>
        public string id;
        public string name;
        /// <summary>What the upgrade does in a run, in full sentences (the Armory's inspect panel).</summary>
        public string summary;
        public int maxRank;
        /// <summary>Price of rank r before the shared fee: basePrice x r.</summary>
        public int basePrice;
        public StatId stat;
        /// <summary>Bonus per rank: a fraction for percent upgrades (0.05 = +5%), a value for flat ones.</summary>
        public float perRank;
        public bool isPercent;

        /// <summary>The bonus at a rank in words: "+15% Max Health", "+2 Armor".</summary>
        public string Describe(int rank)
        {
            float total = perRank * rank;
            switch (stat)
            {
                case StatId.Regeneration:
                    return $"+{total:0.0} HP/s Regeneration";
                case StatId.Cooldown:
                    return $"-{MathF.Abs(total) * 100.0f:0.#}% Cooldown";
                case StatId.Revival:
                    return rank == 1 ? "+1 Revival per run" : $"+{total:0} Revivals per run";
                case StatId.Reroll:
                    return $"+{total:0} Reroll per run";
                case StatId.Banish:
                    return $"+{total:0} Banish per run";
            }
            return isPercent ? $"+{total * 100.0f:0.#}% {GetStatName(stat)}" : $"+{total:0.#} {GetStatName(stat)}";
        }

        private static string GetStatName(StatId stat)
        {
            switch (stat)
            {
                case StatId.MaxHealth: return "Max Health";
                case StatId.Might: return "Might";
                case StatId.Armor: return "Armor";
                case StatId.MoveSpeed: return "Move Speed";
                case StatId.Area: return "Area";
                case StatId.PickupRadius: return "Pickup Radius";
                case StatId.Luck: return "Luck";
                case StatId.Growth: return "Growth";
                case StatId.Greed: return "Greed";
                default: return stat.ToString();
            }
        }
    }

    /// <summary>
    /// The Armory's permanent ranks: 14 upgrades, price = base x next rank + 5 x every rank owned, a free
    /// full refund, and the bonuses a new run starts with. Main thread.
    /// </summary>
    public static class MetaShop
    {
        /// <summary>Gold added to every price per rank owned across all upgrades.</summary>
        public const int SHARED_FEE = 5;

        /// <summary>Every upgrade in Armory order.</summary>
        public static readonly MetaUpgrade[] upgrades =
        {
            new MetaUpgrade { id = "vigor", name = "Vigor", summary = "Raises the maximum health your hero starts every run with.", maxRank = 5, basePrice = 70, stat = StatId.MaxHealth, perRank = 0.05f, isPercent = true },
            new MetaUpgrade { id = "might", name = "Might", summary = "Multiplies the damage of every weapon and ability.", maxRank = 5, basePrice = 100, stat = StatId.Might, perRank = 0.04f, isPercent = true },
            new MetaUpgrade { id = "plating", name = "Plating", summary = "Armor reduces the damage of every hit you take.", maxRank = 3, basePrice = 140, stat = StatId.Armor, perRank = 1.0f },
            new MetaUpgrade { id = "mending", name = "Mending", summary = "Heals your hero a little every second, all run long.", maxRank = 5, basePrice = 70, stat = StatId.Regeneration, perRank = 0.1f },
            new MetaUpgrade { id = "swiftness", name = "Swiftness", summary = "Your hero moves faster, to outrun the horde and sweep up pickups.", maxRank = 3, basePrice = 100, stat = StatId.MoveSpeed, perRank = 0.03f, isPercent = true },
            new MetaUpgrade { id = "haste", name = "Haste", summary = "Weapons attack more often. The active ability gets half of the bonus.", maxRank = 3, basePrice = 200, stat = StatId.Cooldown, perRank = -0.025f, isPercent = true },
            new MetaUpgrade { id = "reach", name = "Reach", summary = "Enlarges slashes, auras, explosions and every other attack area.", maxRank = 3, basePrice = 140, stat = StatId.Area, perRank = 0.05f, isPercent = true },
            new MetaUpgrade { id = "magnetism", name = "Magnetism", summary = "Pulls in experience gems and other pickups from farther away.", maxRank = 3, basePrice = 60, stat = StatId.PickupRadius, perRank = 0.15f, isPercent = true },
            new MetaUpgrade { id = "fortune", name = "Fortune", summary = "Luck raises the odds of rarer upgrade choices, of coin, food and magnet drops, and of fuller chests.", maxRank = 4, basePrice = 140, stat = StatId.Luck, perRank = 5.0f },
            new MetaUpgrade { id = "insight", name = "Insight", summary = "Growth gives more experience from every gem, so you level up sooner.", maxRank = 5, basePrice = 100, stat = StatId.Growth, perRank = 0.04f, isPercent = true },
            new MetaUpgrade { id = "avarice", name = "Avarice", summary = "Greed gives more gold from coins, chests and bosses, to spend here in the Armory.", maxRank = 5, basePrice = 60, stat = StatId.Greed, perRank = 0.08f, isPercent = true },
            new MetaUpgrade { id = "second_wind", name = "Second Wind", summary = "When you fall, you rise again with half your health and a moment of invulnerability.", maxRank = 1, basePrice = 1000, stat = StatId.Revival, perRank = 1.0f },
            new MetaUpgrade { id = "reroll", name = "Reroll", summary = "Trades all the choices of a level up for new ones. Every rank adds one use per run.", maxRank = 5, basePrice = 100, stat = StatId.Reroll, perRank = 1.0f },
            new MetaUpgrade { id = "banish", name = "Banish", summary = "Removes a level up choice for the rest of the run. Every rank adds one use per run.", maxRank = 3, basePrice = 140, stat = StatId.Banish, perRank = 1.0f },
        };

        /// <summary>Ranks owned across every upgrade (the shared fee counts them).</summary>
        public static int GetTotalRanks(SaveData save)
        {
            int total = 0;
            foreach (MetaUpgrade upgrade in upgrades)
            {
                total += save.GetRank(upgrade.id);
            }
            return total;
        }

        /// <summary>Price of the next rank, or -1 when the upgrade is maxed.</summary>
        public static int GetPrice(SaveData save, MetaUpgrade upgrade)
        {
            int next = save.GetRank(upgrade.id) + 1;
            if (next > upgrade.maxRank)
            {
                return -1;
            }
            return upgrade.basePrice * next + SHARED_FEE * GetTotalRanks(save);
        }

        public static bool CanBuy(SaveData save, MetaUpgrade upgrade)
        {
            int price = GetPrice(save, upgrade);
            return price >= 0 && save.gold >= price;
        }

        /// <summary>Buys the next rank and saves; false when maxed or too expensive.</summary>
        public static bool Buy(SaveData save, MetaUpgrade upgrade)
        {
            if (!CanBuy(save, upgrade))
            {
                return false;
            }
            int price = GetPrice(save, upgrade);
            save.gold -= price;
            save.goldSpent += price;
            save.SetRank(upgrade.id, save.GetRank(upgrade.id) + 1);
            save.Save();
            return true;
        }

        /// <summary>Removes every rank and pays back all gold spent on them; returns the gold refunded.</summary>
        public static int RefundAll(SaveData save)
        {
            int refunded = save.goldSpent;
            save.gold += refunded;
            save.goldSpent = 0;
            save.metaRanks.Clear();
            save.Save();
            return refunded;
        }

        /// <summary>Adds the save's meta ranks to a new run's stats (percent bonuses add to the same pool as
        /// passives, flat bonuses before percents).</summary>
        public static void ApplyRanks(SaveData save, PlayerStats stats)
        {
            if (save == null || stats == null)
            {
                return;
            }
            foreach (MetaUpgrade upgrade in upgrades)
            {
                int rank = save.GetRank(upgrade.id);
                if (rank <= 0)
                {
                    continue;
                }
                float amount = upgrade.perRank * rank;
                if (upgrade.isPercent)
                {
                    stats.AddPercent(upgrade.stat, amount);
                }
                else
                {
                    stats.AddFlat(upgrade.stat, amount);
                }
            }
        }

        /// <summary>Clamps ranks to their maximum, drops unknown ids and rebuilds the spent gold of saves that lack
        /// it (as if every rank was bought in Armory order).</summary>
        public static void SanitizeRanks(SaveData save)
        {
            var known = new Dictionary<string, int>();
            foreach (MetaUpgrade upgrade in upgrades)
            {
                int rank = Math.Clamp(save.GetRank(upgrade.id), 0, upgrade.maxRank);
                if (rank > 0)
                {
                    known[upgrade.id] = rank;
                }
            }
            save.metaRanks = known;
            if (save.goldSpent > 0 || known.Count == 0)
            {
                return;
            }
            int owned = 0;
            int spent = 0;
            foreach (MetaUpgrade upgrade in upgrades)
            {
                int rank = save.GetRank(upgrade.id);
                for (int r = 1; r <= rank; r++)
                {
                    spent += upgrade.basePrice * r + SHARED_FEE * owned;
                    owned++;
                }
            }
            save.goldSpent = spent;
        }
    }
}

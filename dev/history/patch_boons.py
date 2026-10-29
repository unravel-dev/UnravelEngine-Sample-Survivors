"""Boons (design update 3): slotless, unlimited stat picks with diminishing returns; Hearty Meal when hurt; chest boons."""
import os

from patchlib import edit

ROOT = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(ROOT, "src", "Scripts")
U = os.path.join(ROOT, "src", "UI")


def p(*parts):
    return os.path.join(S, *parts)


# --------------------------------------------------------------------------------------------------- stats
edit(p("Core", "Stats.cs"), [("""        /// <summary>Removes every bonus (a new run); bases stay.</summary>""", """        /// <summary>
        /// Whether more of a bonus would change nothing: the stat sits at its maximum, or at its minimum when
        /// <paramref name="lowerIsBetter"/> (Cooldown).
        /// </summary>
        public bool IsCapped(StatId stat, bool lowerIsBetter)
        {
            float value = Get(stat);
            return lowerIsBetter ? value <= minimum[(int)stat] + CAP_EPSILON : value >= maximum[(int)stat] - CAP_EPSILON;
        }

        /// <summary>Removes every bonus (a new run); bases stay.</summary>"""),
    ("""        private const int COUNT = (int)StatId.Count;""", """        private const int COUNT = (int)StatId.Count;
        private const float CAP_EPSILON = 1e-4f;"""),
])

# --------------------------------------------------------------------------------------------------- data
edit(p("Weapons", "WeaponData.cs"), [("""    /// <summary>One passive item of the design (section 4).</summary>""", """    /// <summary>
    /// A boon (design update 3): a small stat bonus that takes no slot and can be taken without limit. The n-th pick adds
    /// the first pick's bonus divided by the square root of n, so 10 picks are worth about 5 first picks.
    /// </summary>
    public sealed class BoonDef
    {
        public string id = "";
        public string name = "";
        /// <summary>The first pick's bonus per stat (<see cref="PassiveEffect.perRank"/>).</summary>
        public PassiveEffect[] effects = Array.Empty<PassiveEffect>();

        /// <summary>What the n-th pick (from 1) adds, as a share of the first pick.</summary>
        public static float GetPickShare(int pick) => 1.0f / MathF.Sqrt(Math.Max(1, pick));

        /// <summary>What <paramref name="picks"/> picks add together, as a share of the first pick.</summary>
        public static float GetTotalShare(int picks)
        {
            float total = 0.0f;
            for (int pick = 1; pick <= picks; pick++)
            {
                total += GetPickShare(pick);
            }
            return total;
        }
    }

    /// <summary>One passive item of the design (section 4).</summary>"""),
])

# --------------------------------------------------------------------------------------------------- catalog
edit(p("Weapons", "WeaponCatalog.cs"), [
    ("""        /// <summary>Hearty Meal, Coin Purse, Battle Hymn (design 2.6).</summary>""", """        /// <summary>The slotless boons (design update 3), first-pick values.</summary>
        public static readonly BoonDef[] boons =
        {
            Boon("boon_fleetfoot", "Fleetfoot", new PassiveEffect(StatId.MoveSpeed, true, 0.03f, "Move Speed")),
            Boon("boon_magnetism", "Magnetism", new PassiveEffect(StatId.PickupRadius, true, 0.10f, "Pickup Radius")),
            Boon("boon_vitality", "Vitality", new PassiveEffect(StatId.MaxHealth, true, 0.04f, "Max Health")),
            Boon("boon_mending", "Mending", new PassiveEffect(StatId.Regeneration, false, 0.1f, "Regen", " HP/s")),
            Boon("boon_hide", "Hide", new PassiveEffect(StatId.Armor, false, 0.6f, "Armor")),
            Boon("boon_edge", "Edge", new PassiveEffect(StatId.Might, true, 0.025f, "Might")),
            Boon("boon_alacrity", "Alacrity", new PassiveEffect(StatId.Cooldown, true, -0.015f, "Cooldown")),
            Boon("boon_breadth", "Breadth", new PassiveEffect(StatId.Area, true, 0.025f, "Area")),
            Boon("boon_persistence", "Persistence",
                 new PassiveEffect(StatId.Duration, true, 0.03f, "Duration"),
                 new PassiveEffect(StatId.ProjectileSpeed, true, 0.03f, "Projectile Speed")),
            Boon("boon_precision", "Precision", new PassiveEffect(StatId.CritChance, false, 0.012f, "Crit Chance", "", true)),
            Boon("boon_insight", "Insight", new PassiveEffect(StatId.Growth, true, 0.03f, "Growth")),
            Boon("boon_plunder", "Plunder", new PassiveEffect(StatId.Greed, true, 0.04f, "Greed")),
            Boon("boon_clover", "Clover", new PassiveEffect(StatId.Luck, false, 2.0f, "Luck")),
        };

        /// <summary>Share of level-ups that show one boon card while item cards are still available.</summary>
        public const float BOON_CARD_CHANCE = 0.35f;
        /// <summary>Below this share of max health a level-up may offer Hearty Meal, with this chance.</summary>
        public const float HEARTY_MEAL_HEALTH_SHARE = 0.5f;
        public const float HEARTY_MEAL_CHANCE = 0.6f;

        /// <summary>Hearty Meal, Coin Purse, Battle Hymn (design 2.6).</summary>"""),
    ("""        public static PassiveDef FindPassive(string id)""", """        public static int FindBoonIndex(string id)
        {
            for (int i = 0; i < boons.Length; i++)
            {
                if (boons[i].id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        public static BoonDef FindBoon(string id)
        {
            int index = FindBoonIndex(id);
            return index >= 0 ? boons[index] : null;
        }

        public static PassiveDef FindPassive(string id)"""),
    ("""        private static PassiveDef Passive(string id""", """        private static BoonDef Boon(string id, string name, params PassiveEffect[] effects)
        {
            return new BoonDef { id = id, name = name, effects = effects };
        }

        private static PassiveDef Passive(string id"""),
])

# --------------------------------------------------------------------------------------------------- card text
edit(p("Weapons", "WeaponText.cs"), [("""        public static string FormatPassiveValue(PassiveEffect effect, float value)""", """        /// <summary>
        /// A boon card: the first pick's bonus and that it takes no slot, or every stat's total old -> new once taken.
        /// </summary>
        public static string DescribeBoon(BoonDef def, int picks)
        {
            var text = new StringBuilder();
            foreach (PassiveEffect effect in def.effects)
            {
                string line = picks <= 0
                    ? effect.label + " " + FormatBoonValue(effect, effect.perRank)
                    : effect.label + " " + FormatBoonValue(effect, effect.perRank * BoonDef.GetTotalShare(picks)) + ARROW +
                      FormatBoonValue(effect, effect.perRank * BoonDef.GetTotalShare(picks + 1));
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
                Append(text, effect.label + " " + FormatBoonValue(effect, effect.perRank * BoonDef.GetTotalShare(picks)), SEPARATOR);
            }
            return text.ToString();
        }

        /// <summary>Boon values are small: one decimal for percents, two for flat values.</summary>
        public static string FormatBoonValue(PassiveEffect effect, float value)
        {
            string sign = value >= 0.0f ? "+" : "-";
            float magnitude = MathF.Abs(value);
            if (effect.isPercent || effect.showsPercent)
            {
                return sign + (magnitude * 100.0f).ToString("0.#", invariant) + "%";
            }
            return sign + magnitude.ToString("0.##", invariant) + effect.unit;
        }

        public static string FormatPassiveValue(PassiveEffect effect, float value)"""),
])

# --------------------------------------------------------------------------------------------------- loadout
edit(p("Weapons", "Loadout.cs"), [
    ("""            Evolution,
            /// <summary>Hearty Meal, Coin Purse, Battle Hymn: offered when nothing else is eligible.</summary>
            Fallback,
        }""", """            Evolution,
            /// <summary>Hearty Meal (when hurt), the chest's gold: cards that are not items.</summary>
            Fallback,
            /// <summary>A slotless boon (design update 3).</summary>
            Boon,
        }"""),
    ("""        private int battleHymns;
""", """        private int battleHymns;
        /// <summary>Times each boon was taken, by <see cref="WeaponCatalog.boons"/> index.</summary>
        private readonly int[] boonPicks = new int[WeaponCatalog.boons.Length];
"""),
    ("""                case UpgradeOffer.Kind.Fallback:
                    ApplyFallback(offer.id);
                    break;""", """                case UpgradeOffer.Kind.Fallback:
                    ApplyFallback(offer.id);
                    break;
                case UpgradeOffer.Kind.Boon:
                    ApplyBoon(offer.id);
                    break;"""),
    ("""        private void ApplyFallback(string id)""", """        /// <summary>Adds the next pick of a boon: the first pick's bonus divided by the square root of the pick count.</summary>
        private void ApplyBoon(string id)
        {
            int index = WeaponCatalog.FindBoonIndex(id);
            if (index < 0)
            {
                return;
            }
            int pick = ++boonPicks[index];
            float share = BoonDef.GetPickShare(pick);
            foreach (PassiveEffect effect in WeaponCatalog.boons[index].effects)
            {
                if (effect.isPercent)
                {
                    run.stats.AddPercent(effect.stat, effect.perRank * share);
                }
                else
                {
                    run.stats.AddFlat(effect.stat, effect.perRank * share);
                }
            }
        }

        /// <summary>Times the boon at <see cref="WeaponCatalog.boons"/> index <paramref name="index"/> was taken.</summary>
        public int GetBoonPicks(int index) => index >= 0 && index < boonPicks.Length ? boonPicks[index] : 0;

        private void ApplyFallback(string id)"""),
])

edit(p("Weapons", "Loadout.Offers.cs"), [
    ("""            foreach (FallbackDef fallback in WeaponCatalog.fallbacks)
            {
                if (!IsShown(shown, fallback.id))
                {
                    return MakeFallbackOffer(fallback);
                }
            }
            return MakeFallbackOffer(WeaponCatalog.fallbacks[0]);
        }""", """            int boon = PickBoon(rng, shown);
            return boon >= 0 ? MakeBoonOffer(boon) : MakeFallbackOffer(WeaponCatalog.fallbacks[0]);
        }"""),
    ("""            while (items.Count < count)
            {
                items.Add(MakeFallbackOffer(WeaponCatalog.chestGold));
            }""", """            while (items.Count < count)
            {
                int boon = PickBoon(rng, items);
                items.Add(boon >= 0 ? MakeBoonOffer(boon) : MakeFallbackOffer(WeaponCatalog.chestGold));
            }"""),
    ("""        private List<UpgradeOffer> RollCards(System.Random rng)
        {
            BuildPool();
            var picked = new List<Candidate>(CARD_COUNT);
            if (pool.Count > 0)
            {
                bool isOnboarding = run.level == 2 || run.level == 3;
                if ((isOnboarding || noNewWeaponStreak >= PITY_PICKS) && weaponCount < WEAPON_SLOTS)
                {
                    TakePick(rng, picked, PickFilter.NewWeapon);
                }
                if (!ContainsOwned(picked))
                {
                    TakePick(rng, picked, PickFilter.Owned);
                }
                while (picked.Count < CARD_COUNT && TakePick(rng, picked, PickFilter.Any))
                {
                }
            }
            var cards = new List<UpgradeOffer>(CARD_COUNT);
            foreach (Candidate candidate in picked)
            {
                cards.Add(MakeOffer(candidate));
            }
            AddFallbacks(cards);
            Shuffle(cards, rng);
            return cards;
        }""", """        /// <summary>
        /// Item cards first (onboarding / pity new weapon, an owned item, then any); one place goes to a boon on
        /// <see cref="WeaponCatalog.BOON_CARD_CHANCE"/> of level-ups, and boons fill every place items cannot. Below half
        /// health one card may become Hearty Meal.
        /// </summary>
        private List<UpgradeOffer> RollCards(System.Random rng)
        {
            BuildPool();
            int itemCards = rng.NextDouble() < WeaponCatalog.BOON_CARD_CHANCE ? CARD_COUNT - 1 : CARD_COUNT;
            var picked = new List<Candidate>(CARD_COUNT);
            if (pool.Count > 0)
            {
                bool isOnboarding = run.level == 2 || run.level == 3;
                if ((isOnboarding || noNewWeaponStreak >= PITY_PICKS) && weaponCount < WEAPON_SLOTS)
                {
                    TakePick(rng, picked, PickFilter.NewWeapon);
                }
                if (!ContainsOwned(picked) && picked.Count < itemCards)
                {
                    TakePick(rng, picked, PickFilter.Owned);
                }
                while (picked.Count < itemCards && TakePick(rng, picked, PickFilter.Any))
                {
                }
            }
            var cards = new List<UpgradeOffer>(CARD_COUNT);
            foreach (Candidate candidate in picked)
            {
                cards.Add(MakeOffer(candidate));
            }
            AddBoons(cards, rng);
            OfferHeartyMeal(cards, rng);
            Shuffle(cards, rng);
            return cards;
        }

        /// <summary>Fills empty card places with boons not shown yet (fallback cards only if every boon is capped).</summary>
        private void AddBoons(List<UpgradeOffer> cards, System.Random rng)
        {
            while (cards.Count < CARD_COUNT)
            {
                int boon = PickBoon(rng, cards);
                if (boon < 0)
                {
                    AddFallbacks(cards);
                    return;
                }
                cards.Add(MakeBoonOffer(boon));
            }
        }

        /// <summary>Below half health, sometimes turns a boon card (else the last card) into Hearty Meal.</summary>
        private void OfferHeartyMeal(List<UpgradeOffer> cards, System.Random rng)
        {
            HeroController hero = run.hero;
            bool isHurt = hero != null && hero.currentHealth < hero.maxHealth * WeaponCatalog.HEARTY_MEAL_HEALTH_SHARE;
            if (!isHurt || cards.Count == 0 || IsShown(cards, "hearty_meal") || rng.NextDouble() >= WeaponCatalog.HEARTY_MEAL_CHANCE)
            {
                return;
            }
            int place = cards.Count - 1;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].kind == UpgradeOffer.Kind.Boon)
                {
                    place = i;
                    break;
                }
            }
            cards[place] = MakeFallbackOffer(WeaponCatalog.FindFallback("hearty_meal"));
        }

        /// <summary>A random boon index not in <paramref name="shown"/> whose stats are not all capped, or -1.</summary>
        private int PickBoon(System.Random rng, IReadOnlyList<UpgradeOffer> shown)
        {
            int eligible = 0;
            for (int i = 0; i < WeaponCatalog.boons.Length; i++)
            {
                if (IsBoonEligible(i, shown))
                {
                    eligible++;
                }
            }
            if (eligible == 0)
            {
                return -1;
            }
            int choice = rng.Next(eligible);
            for (int i = 0; i < WeaponCatalog.boons.Length; i++)
            {
                if (IsBoonEligible(i, shown) && choice-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool IsBoonEligible(int index, IReadOnlyList<UpgradeOffer> shown)
        {
            BoonDef def = WeaponCatalog.boons[index];
            if (IsShown(shown, def.id))
            {
                return false;
            }
            foreach (PassiveEffect effect in def.effects)
            {
                if (!run.stats.IsCapped(effect.stat, effect.perRank < 0.0f))
                {
                    return true;
                }
            }
            return false;
        }

        private UpgradeOffer MakeBoonOffer(int index)
        {
            BoonDef def = WeaponCatalog.boons[index];
            int picks = GetBoonPicks(index);
            return new UpgradeOffer
            {
                kind = UpgradeOffer.Kind.Boon,
                id = def.id,
                name = def.name,
                rarity = Rarity.Common,
                icon = WeaponCatalog.IconKey(def.id),
                isNew = picks == 0,
                fromRank = picks,
                toRank = picks + 1,
                description = WeaponText.DescribeBoon(def, picks),
            };
        }"""),
])

# --------------------------------------------------------------------------------------------------- UI text
edit(p("UI", "UIText.cs"), [
    ("""                case UpgradeOffer.Kind.Fallback:
                    return "BLESSING";
            }
            string rarity = offer.rarity.ToString().ToUpperInvariant();""", """                case UpgradeOffer.Kind.Fallback:
                    return "BLESSING";
                case UpgradeOffer.Kind.Boon:
                    return "BOON";
            }
            string rarity = offer.rarity.ToString().ToUpperInvariant();"""),
    ("""                case UpgradeOffer.Kind.Fallback:
                    return "BLESSING";
            }
            if (offer.isNew)""", """                case UpgradeOffer.Kind.Fallback:
                    return "BLESSING";
                case UpgradeOffer.Kind.Boon:
                    return offer.isNew ? "BOON" : $"BOON x{offer.toRank}";
            }
            if (offer.isNew)"""),
    ("""        public static string FrameClass(Rarity rarity)""", """        /// <summary>A card's or chest reel's frame class: evolution gold, boon teal, else the rarity's.</summary>
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

        public static string FrameClass(Rarity rarity)"""),
    ("""            if (WeaponCatalog.FindFallback(id) != null)""", """            if (WeaponCatalog.FindBoon(id) != null)
            {
                return new Ownership { text = "BOON - NO SLOT", classes = "own own-boon", emblem = Glyph("plus") };
            }
            if (WeaponCatalog.FindFallback(id) != null)"""),
])

edit(p("UI", "Screens", "LevelUpScreen.cs"), [("""            card.SetClass(offer.kind == UpgradeOffer.Kind.Evolution ? "r-evolution" : UIText.FrameClass(offer.rarity), true);""",
                                                """            card.SetClass(UIText.OfferFrameClass(offer), true);""")])

edit(p("UI", "Screens", "PauseScreen.cs"), [("""                row.SetOwnership(view.id);
                model.passives.Add(row);
            }
        }""", """                row.SetOwnership(view.id);
                model.passives.Add(row);
            }
            for (int i = 0; i < WeaponCatalog.boons.Length; i++)
            {
                int picks = loadout.GetBoonPicks(i);
                if (picks <= 0)
                {
                    continue;
                }
                BoonDef boon = WeaponCatalog.boons[i];
                var row = new BuildRow
                {
                    icon = UIText.Icon(boon.id),
                    name = boon.name,
                    rank = "x" + UIText.Int(picks),
                    line = WeaponText.DescribeBoonTotal(boon, picks),
                };
                row.SetOwnership(boon.id);
                model.passives.Add(row);
            }
        }""")])

# --------------------------------------------------------------------------------------------------- styles
edit(os.path.join(U, "theme.rcss"), [(""".own-blessing { color: #ffd98a; border-color: #a07a30; background-color: #a07a302a; }""",
                                      """.own-blessing { color: #ffd98a; border-color: #a07a30; background-color: #a07a302a; }
.own-boon { color: #9ef0d2; border-color: #4fb894; background-color: #4fb8942a; }
.own-boon .own-emblem { image-color: #9ef0d2; }""")])
edit(os.path.join(U, "LevelUp.rcss"), [
    (""".card.r-evolution { border-color: #ffc94c; }""", """.card.r-evolution { border-color: #ffc94c; }
.card.r-boon { border-color: #6fdcb4; }"""),
    (""".card.r-evolution .card-glow {""", """.card.r-boon .card-glow { decorator: radial-gradient(circle at 50% 30%, #6fdcb44a, #6fdcb400 70%); }
.card.r-evolution .card-glow {"""),
    (""".card.r-evolution .card-rarity { color: #ffc94c; }""", """.card.r-evolution .card-rarity { color: #ffc94c; }
.card.r-boon .card-rarity { color: #6fdcb4; }"""),
])
edit(os.path.join(U, "Pause.rhtml"), [("""<div class="label section second">PASSIVES</div>""", """<div class="label section second">PASSIVES AND BOONS</div>""")])
print("boons patched")

edit(p("UI", "UIText.cs"), [("""            "r-common", "r-uncommon", "r-rare", "r-epic", "r-evolution",
        };""", """            "r-common", "r-uncommon", "r-rare", "r-epic", "r-evolution", "r-boon",
        };""")])
edit(p("UI", "Screens", "ChestScreen.cs"), [
    ("""            reel.tag?.SetText(isEvolution ? "EVOLVED" : item.isNew ? "NEW" : UIText.RankTag(item));""",
     """            reel.tag?.SetText(isEvolution ? "EVOLVED" : item.isNew && item.kind != UpgradeOffer.Kind.Boon ? "NEW" : UIText.RankTag(item));"""),
    ("""            reel.root.SetClass(isEvolution ? "r-evolution" : UIText.FrameClass(item.rarity), true);""",
     """            reel.root.SetClass(UIText.OfferFrameClass(item), true);"""),
])
edit(os.path.join(U, "Chest.rcss"), [
    (""".reel.r-evolution .reel-window { border-color: #ffc94c; }""", """.reel.r-evolution .reel-window { border-color: #ffc94c; }
.reel.r-boon .reel-window { border-color: #6fdcb4; }"""),
    (""".reel.r-epic .reel-glow { decorator: radial-gradient(circle at 50% 50%, #b05cff70, #b05cff00 70%); }""",
     """.reel.r-epic .reel-glow { decorator: radial-gradient(circle at 50% 50%, #b05cff70, #b05cff00 70%); }
.reel.r-boon .reel-glow { decorator: radial-gradient(circle at 50% 50%, #6fdcb460, #6fdcb400 70%); }"""),
])
print("chest boons patched")

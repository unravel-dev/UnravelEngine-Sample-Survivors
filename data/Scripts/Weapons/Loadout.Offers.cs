using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Level-up cards, reroll and banish, and chest items. Loadout is the one place their rules live: what a card
    /// offers, what may be banished, and what a reroll or banish costs. Deterministic per run seed: every roll draws from
    /// the <see cref="System.Random"/> the caller passes (the run's).
    /// </summary>
    public sealed partial class Loadout
    {
        private const int CARD_COUNT = 3;
        /// <summary>Level-up picks in a row without a new weapon before a new weapon card is forced.</summary>
        private const int PITY_PICKS = 5;
        /// <summary>While the hero's level is within these, every level-up offers a new weapon (while a slot is free).</summary>
        private const int FIRST_ONBOARDING_LEVEL = 2;
        private const int LAST_ONBOARDING_LEVEL = 3;
        private const float OWNED_WEIGHT = 1.6f;
        /// <summary>Luck counts in points: at 100 a rarity's weight grows by its whole <see cref="LUCK_SHIFT"/>.</summary>
        private const float LUCK_POINTS_FULL = 100.0f;
        private const int SMALL_CHEST_EVOLUTIONS = 1;
        private const int BIG_CHEST_EVOLUTIONS = 2;
        private const int BIG_CHEST_ITEMS = 5;

        /// <summary>Base weight per rarity (Common, Uncommon, Rare, Epic, Evolution).</summary>
        private static readonly float[] RARITY_WEIGHT = { 100.0f, 70.0f, 45.0f, 20.0f, 0.0f };
        /// <summary>How strongly Luck lifts each rarity: weight x (1 + luck / 100 x shift).</summary>
        private static readonly float[] LUCK_SHIFT = { 0.0f, 0.5f, 1.0f, 1.5f, 0.0f };

        private enum PickFilter
        {
            Any,
            NewWeapon,
            Owned,
        }

        /// <summary>An item that could be offered, with its weight.</summary>
        private struct Candidate
        {
            public UpgradeOffer.Kind kind;
            public WeaponDef weapon;
            public PassiveDef passive;
            /// <summary>Owned rank, 0 when not owned.</summary>
            public int rank;
            public float weight;
            /// <summary>Weapon or passive slot (chest rolls).</summary>
            public int slot;

            public string id => weapon != null ? weapon.id : passive.id;
            public bool isOwned => rank > 0;
        }

        /// <summary>Ids of the weapons, passives and boons banished this run: they never roll on a card again.</summary>
        private readonly HashSet<string> banished = new HashSet<string>();
        private readonly List<Candidate> pool = new List<Candidate>();
        private int noNewWeaponStreak;

        /// <summary>Three cards for a level-up.</summary>
        public List<UpgradeOffer> RollLevelUp(System.Random rng) => RollCards(rng);

        /// <summary>Spends a reroll charge and rolls three fresh cards into <paramref name="offers"/>; false (and null) when no charge is left.</summary>
        public bool TryReroll(System.Random rng, out List<UpgradeOffer> offers)
        {
            offers = null;
            if (rerollsLeft <= 0)
            {
                return false;
            }
            run.stats.AddFlat(StatId.Reroll, -1.0f);
            offers = RollCards(rng);
            return true;
        }

        /// <summary>
        /// Whether a card can be banished at all: weapons, passives and boons can; evolutions and fallback cards cannot.
        /// Charges are checked by <see cref="TryBanish"/> (<see cref="banishesLeft"/>).
        /// </summary>
        public bool CanBanish(UpgradeOffer offer)
        {
            if (offer == null)
            {
                return false;
            }
            return offer.kind == UpgradeOffer.Kind.Weapon || offer.kind == UpgradeOffer.Kind.Passive || offer.kind == UpgradeOffer.Kind.Boon;
        }

        /// <summary>
        /// Spends a banish charge: the card's item never rolls on a level-up card again this run (chests still rank up what
        /// the hero owns), and <paramref name="replacement"/> is a fresh card not among <paramref name="shown"/>. False, with
        /// no charge spent, when no charge is left or <see cref="CanBanish"/> refuses the card.
        /// </summary>
        public bool TryBanish(UpgradeOffer offer, IReadOnlyList<UpgradeOffer> shown, System.Random rng, out UpgradeOffer replacement)
        {
            replacement = null;
            if (!CanBanish(offer) || banishesLeft <= 0)
            {
                return false;
            }
            replacement = Banish(offer, shown, rng);
            run.stats.AddFlat(StatId.Banish, -1.0f);
            return true;
        }

        /// <summary>Removes a banishable card's item from the run's offers; returns the fresh card that replaces it.</summary>
        private UpgradeOffer Banish(UpgradeOffer offer, IReadOnlyList<UpgradeOffer> shown, System.Random rng)
        {
            banished.Add(offer.id);
            BuildPool();
            var excluded = new List<Candidate>();
            foreach (Candidate candidate in pool)
            {
                if (IsShown(shown, candidate.id))
                {
                    excluded.Add(candidate);
                }
            }
            int index = Pick(pool, rng, PickFilter.Any, excluded);
            if (index >= 0)
            {
                return MakeOffer(pool[index]);
            }
            int boon = PickBoon(rng, shown);
            return boon >= 0 ? MakeBoonOffer(boon) : MakeFallbackOffer(WeaponCatalog.heartyMeal);
        }

        /// <summary>The items of a chest: evolutions first, then rank-ups of owned items, gold when all is maxed.</summary>
        public List<UpgradeOffer> RollChest(System.Random rng, int count)
        {
            count = Math.Max(1, count);
            var items = new List<UpgradeOffer>(count);
            var weaponRanks = new int[WEAPON_SLOTS];
            var evolving = new bool[WEAPON_SLOTS];
            var passiveRanks = new int[PASSIVE_SLOTS];
            int evolutionLimit = count >= BIG_CHEST_ITEMS ? BIG_CHEST_EVOLUTIONS : SMALL_CHEST_EVOLUTIONS;
            for (int i = 0; i < weaponCount && items.Count < count && items.Count < evolutionLimit; i++)
            {
                if (CanEvolve(weaponObjects[i]))
                {
                    evolving[i] = true;
                    items.Add(MakeEvolutionOffer(weaponObjects[i]));
                }
            }
            for (int i = 0; i < weaponCount; i++)
            {
                weaponRanks[i] = weaponObjects[i].rank;
            }
            for (int i = 0; i < passiveCount; i++)
            {
                passiveRanks[i] = passiveObjects[i].rank;
            }
            var chestPool = new List<Candidate>();
            while (items.Count < count)
            {
                BuildChestPool(chestPool, weaponRanks, evolving, passiveRanks);
                int index = Pick(chestPool, rng, PickFilter.Any, null);
                if (index < 0)
                {
                    break;
                }
                Candidate candidate = chestPool[index];
                items.Add(MakeOffer(candidate));
                if (candidate.kind == UpgradeOffer.Kind.Weapon)
                {
                    weaponRanks[candidate.slot]++;
                }
                else
                {
                    passiveRanks[candidate.slot]++;
                }
            }
            while (items.Count < count)
            {
                int boon = PickBoon(rng, items);
                items.Add(boon >= 0 ? MakeBoonOffer(boon) : MakeFallbackOffer(WeaponCatalog.chestGold));
            }
            foreach (UpgradeOffer item in items)
            {
                item.isChestItem = true;
            }
            return items;
        }

        // -------------------------------------------------------------------------------------------------------
        // Rolling
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Item cards first (onboarding / pity new weapon, an owned item, then any); one place goes to a boon on
        /// <see cref="WeaponCatalog.BOON_CARD_CHANCE"/> of level-ups, and boons fill every place items cannot. Below half
        /// health one card may become Hearty Meal.
        /// </summary>
        private List<UpgradeOffer> RollCards(System.Random rng)
        {
            BuildPool();
            int itemCards = devForceBoons ? 0 : rng.NextDouble() < WeaponCatalog.BOON_CARD_CHANCE ? CARD_COUNT - 1 : CARD_COUNT;
            var picked = new List<Candidate>(CARD_COUNT);
            if (pool.Count > 0)
            {
                bool isOnboarding = run.level >= FIRST_ONBOARDING_LEVEL && run.level <= LAST_ONBOARDING_LEVEL;
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
            bool isShown = IsShown(cards, WeaponCatalog.heartyMeal.id);
            if (!isHurt || cards.Count == 0 || isShown || rng.NextDouble() >= WeaponCatalog.HEARTY_MEAL_CHANCE)
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
            cards[place] = MakeFallbackOffer(WeaponCatalog.heartyMeal);
        }

        /// <summary>A random boon index not banished, not in <paramref name="shown"/> and whose stats are not all capped, or -1.</summary>
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
            if (banished.Contains(def.id) || IsShown(shown, def.id))
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
        }

        /// <summary>Owned items below max rank, plus unowned ones while slots are free; class weapons of other classes and banished items never.</summary>
        private void BuildPool()
        {
            pool.Clear();
            float luck = run.stats[StatId.Luck];
            foreach (WeaponDef def in WeaponCatalog.weapons)
            {
                if (banished.Contains(def.id) || !def.IsOfferedTo(heroClass))
                {
                    continue;
                }
                Weapon owned = FindWeapon(def.id);
                if (owned != null)
                {
                    if (!owned.isEvolved && !owned.isMaxRank)
                    {
                        pool.Add(new Candidate { kind = UpgradeOffer.Kind.Weapon, weapon = def, rank = owned.rank, weight = GetWeight(def.rarity, true, luck), slot = owned.slot });
                    }
                }
                else if (weaponCount < WEAPON_SLOTS)
                {
                    pool.Add(new Candidate { kind = UpgradeOffer.Kind.Weapon, weapon = def, rank = 0, weight = GetWeight(def.rarity, false, luck), slot = -1 });
                }
            }
            foreach (PassiveDef def in WeaponCatalog.passives)
            {
                if (banished.Contains(def.id))
                {
                    continue;
                }
                PassiveSlot owned = FindPassive(def.id);
                if (owned != null)
                {
                    if (owned.rank < def.maxRank)
                    {
                        pool.Add(new Candidate { kind = UpgradeOffer.Kind.Passive, passive = def, rank = owned.rank, weight = GetWeight(def.rarity, true, luck) });
                    }
                }
                else if (passiveCount < PASSIVE_SLOTS)
                {
                    pool.Add(new Candidate { kind = UpgradeOffer.Kind.Passive, passive = def, rank = 0, weight = GetWeight(def.rarity, false, luck) });
                }
            }
        }

        /// <summary>Rank-ups of owned items at their ranks as the chest so far leaves them.</summary>
        private void BuildChestPool(List<Candidate> chestPool, int[] weaponRanks, bool[] evolving, int[] passiveRanks)
        {
            chestPool.Clear();
            float luck = run.stats[StatId.Luck];
            for (int i = 0; i < weaponCount; i++)
            {
                Weapon weapon = weaponObjects[i];
                if (!weapon.isEvolved && !evolving[i] && weaponRanks[i] < WeaponDef.MAX_RANK)
                {
                    chestPool.Add(new Candidate { kind = UpgradeOffer.Kind.Weapon, weapon = weapon.def, rank = weaponRanks[i], weight = GetWeight(weapon.def.rarity, true, luck), slot = i });
                }
            }
            for (int i = 0; i < passiveCount; i++)
            {
                PassiveDef def = passiveObjects[i].def;
                if (passiveRanks[i] < def.maxRank)
                {
                    chestPool.Add(new Candidate { kind = UpgradeOffer.Kind.Passive, passive = def, rank = passiveRanks[i], weight = GetWeight(def.rarity, true, luck), slot = i });
                }
            }
        }

        private static float GetWeight(Rarity rarity, bool isOwned, float luck)
        {
            int index = Math.Clamp((int)rarity, 0, RARITY_WEIGHT.Length - 1);
            float weight = RARITY_WEIGHT[index] * (1.0f + luck / LUCK_POINTS_FULL * LUCK_SHIFT[index]);
            return isOwned ? weight * OWNED_WEIGHT : weight;
        }

        /// <summary>Adds one weighted pick from the pool matching <paramref name="filter"/>; false when none is left.</summary>
        private bool TakePick(System.Random rng, List<Candidate> picked, PickFilter filter)
        {
            int index = Pick(pool, rng, filter, picked);
            if (index < 0)
            {
                return false;
            }
            picked.Add(pool[index]);
            return true;
        }

        /// <summary>A weighted random index of <paramref name="list"/> among entries that match and are not excluded, or -1.</summary>
        private static int Pick(List<Candidate> list, System.Random rng, PickFilter filter, List<Candidate> excluded)
        {
            double total = 0.0;
            for (int i = 0; i < list.Count; i++)
            {
                if (Accepts(list[i], filter, excluded))
                {
                    total += list[i].weight;
                }
            }
            if (total <= 0.0)
            {
                return -1;
            }
            double roll = rng.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (!Accepts(list[i], filter, excluded))
                {
                    continue;
                }
                last = i;
                roll -= list[i].weight;
                if (roll < 0.0)
                {
                    return i;
                }
            }
            return last;
        }

        private static bool Accepts(in Candidate candidate, PickFilter filter, List<Candidate> excluded)
        {
            if (candidate.weight <= 0.0f)
            {
                return false;
            }
            if (excluded != null)
            {
                foreach (Candidate other in excluded)
                {
                    if (other.id == candidate.id)
                    {
                        return false;
                    }
                }
            }
            switch (filter)
            {
                case PickFilter.NewWeapon:
                    return candidate.kind == UpgradeOffer.Kind.Weapon && !candidate.isOwned;
                case PickFilter.Owned:
                    return candidate.isOwned;
                default:
                    return true;
            }
        }

        private static bool ContainsOwned(List<Candidate> picked)
        {
            foreach (Candidate candidate in picked)
            {
                if (candidate.isOwned)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsShown(IReadOnlyList<UpgradeOffer> shown, string id)
        {
            if (shown == null)
            {
                return false;
            }
            for (int i = 0; i < shown.Count; i++)
            {
                if (shown[i] != null && shown[i].id == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Fills empty card places with fallback cards not shown yet.</summary>
        private static void AddFallbacks(List<UpgradeOffer> cards)
        {
            foreach (FallbackDef fallback in WeaponCatalog.fallbacks)
            {
                if (cards.Count >= CARD_COUNT)
                {
                    return;
                }
                if (!IsShown(cards, fallback.id))
                {
                    cards.Add(MakeFallbackOffer(fallback));
                }
            }
        }

        private static void Shuffle(List<UpgradeOffer> cards, System.Random rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Card text
        // -------------------------------------------------------------------------------------------------------

        private UpgradeOffer MakeOffer(in Candidate candidate)
        {
            return candidate.kind == UpgradeOffer.Kind.Weapon ? MakeWeaponOffer(candidate.weapon, candidate.rank) : MakePassiveOffer(candidate.passive, candidate.rank);
        }

        private static UpgradeOffer MakeWeaponOffer(WeaponDef def, int rank)
        {
            var offer = new UpgradeOffer
            {
                kind = UpgradeOffer.Kind.Weapon,
                id = def.id,
                name = def.name,
                rarity = def.rarity,
                icon = WeaponCatalog.IconKey(def.id),
                isNew = rank == 0,
                fromRank = rank,
                toRank = rank + 1,
                description = rank == 0 ? WeaponText.DescribeNewWeapon(def) : WeaponText.DescribeRankUp(def, rank, rank + 1),
            };
            if (def.evolution != null)
            {
                PassiveDef passive = WeaponCatalog.FindPassive(def.evolution.passiveId);
                offer.evolutionHint = "Evolves with " + (passive != null ? passive.name : def.evolution.passiveId);
                offer.evolutionHintIcon = WeaponCatalog.IconKey(def.evolution.passiveId);
            }
            return offer;
        }

        private UpgradeOffer MakePassiveOffer(PassiveDef def, int rank)
        {
            var offer = new UpgradeOffer
            {
                kind = UpgradeOffer.Kind.Passive,
                id = def.id,
                name = def.name,
                rarity = def.rarity,
                icon = WeaponCatalog.IconKey(def.id),
                isNew = rank == 0,
                fromRank = rank,
                toRank = rank + 1,
                description = WeaponText.DescribePassiveChange(def, rank, rank + 1),
            };
            WeaponDef paired = FindPairedWeapon(def.id);
            if (paired != null)
            {
                offer.evolutionHint = "Evolves " + paired.name + " into " + paired.evolution.name;
                offer.evolutionHintIcon = WeaponCatalog.IconKey(paired.evolution.id);
            }
            return offer;
        }

        /// <summary>The weapon a passive evolves for this hero: an owned one first, else one this class can be offered.</summary>
        private WeaponDef FindPairedWeapon(string passiveId)
        {
            for (int i = 0; i < weaponCount; i++)
            {
                WeaponDef def = weaponObjects[i].def;
                if (def.evolution != null && def.evolution.passiveId == passiveId && !weaponObjects[i].isEvolved)
                {
                    return def;
                }
            }
            foreach (WeaponDef def in WeaponCatalog.weapons)
            {
                if (def.IsOfferedTo(heroClass) && def.evolution != null && def.evolution.passiveId == passiveId)
                {
                    return def;
                }
            }
            return null;
        }

        private static UpgradeOffer MakeEvolutionOffer(Weapon weapon)
        {
            EvolutionDef evolution = weapon.def.evolution;
            return new UpgradeOffer
            {
                kind = UpgradeOffer.Kind.Evolution,
                id = evolution.id,
                name = evolution.name,
                rarity = Rarity.Evolution,
                icon = WeaponCatalog.IconKey(evolution.id),
                isNew = false,
                fromRank = WeaponDef.MAX_RANK,
                toRank = WeaponDef.MAX_RANK,
                description = evolution.description,
            };
        }

        private static UpgradeOffer MakeFallbackOffer(FallbackDef fallback)
        {
            return new UpgradeOffer
            {
                kind = UpgradeOffer.Kind.Fallback,
                id = fallback.id,
                name = fallback.name,
                rarity = Rarity.Common,
                icon = WeaponCatalog.IconKey(fallback.id),
                description = fallback.description,
            };
        }
    }
}

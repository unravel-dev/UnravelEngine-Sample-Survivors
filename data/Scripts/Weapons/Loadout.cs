using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The hero's weapons (6 slots), passives (6 slots), class kit and passive traits for one run, and the picks that
    /// change them. Level-up cards, reroll, banish and chest items live in Loadout.Offers.cs; the dev hooks in
    /// Loadout.Dev.cs. Weapons tick once per frame through <see cref="Tick"/>; kills arrive through
    /// <see cref="OnEnemyKilled"/> and hits through the run's combat events, and both are passed on to every weapon.
    /// Main thread.
    /// </summary>
    public sealed partial class Loadout
    {
        public const int WEAPON_SLOTS = 6;
        public const int PASSIVE_SLOTS = 6;
        /// <summary>The evolution burst rises from the hero's chest with this many particles.</summary>
        private const float EVOLVE_BURST_HEIGHT = 1.0f;
        private const int EVOLVE_BURST_COUNT = 30;

        private readonly Run run;
        private readonly HeroClass heroClass;
        private readonly WeaponContext context;
        private readonly ClassKit kit;
        /// <summary>The passive items' traits (Heartbeat, Spiked Plate, Tailwind, ...).</summary>
        public readonly PassiveTraits traits;
        private readonly Weapon[] weaponObjects = new Weapon[WEAPON_SLOTS];
        private readonly PassiveSlot[] passiveObjects = new PassiveSlot[PASSIVE_SLOTS];
        private readonly List<SlotView> weaponSlots = new List<SlotView>(WEAPON_SLOTS);
        private readonly List<SlotView> passiveSlots = new List<SlotView>(PASSIVE_SLOTS);
        /// <summary>Times each boon was taken, by <see cref="WeaponCatalog.boons"/> index.</summary>
        private readonly int[] boonPicks = new int[WeaponCatalog.boons.Length];
        private int weaponCount;
        private int passiveCount;

        /// <summary>A passive in a slot, with the stat bonuses its rank adds (a rank change adds only the difference).</summary>
        private sealed class PassiveSlot
        {
            public readonly PassiveDef def;
            private readonly StatBonus[] bonuses;
            public int rank;

            public PassiveSlot(PassiveDef def)
            {
                this.def = def;
                bonuses = new StatBonus[def.effects.Length];
                for (int i = 0; i < bonuses.Length; i++)
                {
                    bonuses[i] = def.effects[i].CreateBonus();
                }
            }

            public void SetRank(PlayerStats stats, int newRank)
            {
                rank = Math.Clamp(newRank, 1, def.maxRank);
                for (int i = 0; i < bonuses.Length; i++)
                {
                    bonuses[i].Set(stats, def.effects[i].perRank * rank);
                }
            }
        }

        public Loadout(Run run, HeroClass heroClass)
        {
            this.run = run;
            this.heroClass = heroClass;
            HeroClassDef classDef = HeroCatalog.GetClass(heroClass);
            activeName = classDef.activeName;
            activeIcon = WeaponCatalog.IconKey(classDef.activeId);
            signatureIcon = WeaponCatalog.IconKey(classDef.signatureId);
            context = new WeaponContext(run);
            context.loadout = this;
            kit = classDef.createKit(context);
            context.kit = kit;
            traits = new PassiveTraits(context, this);
            kit.activated += traits.OnActive;
            run.combatEvents.hit += OnEnemyDamaged;
            AddWeapon(WeaponCatalog.FindWeapon(classDef.startWeaponId));
            OnItemsChanged();
        }

        public IReadOnlyList<SlotView> weapons => weaponSlots;
        public IReadOnlyList<SlotView> passives => passiveSlots;

        /// <summary>The class active's name and icon key.</summary>
        public string activeName { get; }
        public string activeIcon { get; }
        /// <summary>0 when ready, 1 right after use.</summary>
        public float activeCooldownFraction => kit.activeCooldownFraction;

        /// <summary>The signature passive's icon key.</summary>
        public string signatureIcon { get; }

        /// <summary>Rerolls and banishes left this run (the Reroll / Banish stats).</summary>
        public int rerollsLeft => run.stats.GetCount(StatId.Reroll);
        public int banishesLeft => run.stats.GetCount(StatId.Banish);

        /// <summary>The rank of an owned passive, 0 when not owned.</summary>
        public int GetPassiveRank(string id)
        {
            PassiveSlot owned = FindPassive(id);
            return owned != null ? owned.rank : 0;
        }

        /// <summary>Times the boon at <see cref="WeaponCatalog.boons"/> index <paramref name="index"/> was taken.</summary>
        public int GetBoonPicks(int index) => index >= 0 && index < boonPicks.Length ? boonPicks[index] : 0;

        /// <summary>Display name of the weapon in a slot, for the results table.</summary>
        public string GetWeaponName(int slot) => slot >= 0 && slot < weaponCount ? weaponObjects[slot].displayName : "";

        /// <summary>The weapon in a slot (a hit's source), or null for an empty slot or a source that is no slot.</summary>
        public Weapon GetWeapon(int slot) => slot >= 0 && slot < weaponCount ? weaponObjects[slot] : null;

        /// <summary>Fires the weapons whose cooldowns ran out, steps lingering effects; once per frame.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0.0f)
            {
                return;
            }
            context.clock += dt;
            kit.Tick(dt);
            for (int i = 0; i < weaponCount; i++)
            {
                weaponObjects[i].Tick(dt);
            }
            context.zones.Tick(dt);
            traits.Tick(dt);
            context.feel.Flush(context.clock);
        }

        /// <summary>Uses the class active if it is ready; returns whether it fired.</summary>
        public bool TryActivate() => kit.TryActivate();

        /// <summary>Kill hooks (Bloodlust, Soul Hail, mark daggers, shatters). Runs inside the killing damage call.</summary>
        public void OnEnemyKilled(in EnemyDeath death)
        {
            kit.OnKill(death);
            traits.OnKill(death);
            for (int i = 0; i < weaponCount; i++)
            {
                weaponObjects[i].OnEnemyKilled(death);
            }
        }

        /// <summary>Hit hooks (Venom Shivs' ruptures, Prismatic Ray's scorch). Runs inside the damage call that hit.</summary>
        private void OnEnemyDamaged(int row, int source, float damage, bool isCritical)
        {
            for (int i = 0; i < weaponCount; i++)
            {
                weaponObjects[i].OnEnemyDamaged(row, source, damage, isCritical);
            }
        }

        /// <summary>Lets go of hero events; call when the run's loadout is replaced.</summary>
        public void Dispose()
        {
            run.combatEvents.hit -= OnEnemyDamaged;
            kit.activated -= traits.OnActive;
            traits.Dispose();
            kit.Dispose();
            DisposeWeapons();
        }

        private void DisposeWeapons()
        {
            for (int i = 0; i < weaponCount; i++)
            {
                weaponObjects[i].Dispose();
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Applying picks
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Applies a card or chest item.</summary>
        public void Apply(UpgradeOffer offer)
        {
            if (offer == null)
            {
                return;
            }
            switch (offer.kind)
            {
                case UpgradeOffer.Kind.Weapon:
                    ApplyWeapon(offer.id);
                    break;
                case UpgradeOffer.Kind.Passive:
                    ApplyPassive(offer.id);
                    break;
                case UpgradeOffer.Kind.Evolution:
                    ApplyEvolution(offer.id);
                    break;
                case UpgradeOffer.Kind.Fallback:
                    ApplyFallback(WeaponCatalog.FindFallback(offer.id));
                    break;
                case UpgradeOffer.Kind.Boon:
                    ApplyBoon(offer.id);
                    break;
            }
            if (!offer.isChestItem && offer.kind != UpgradeOffer.Kind.Fallback)
            {
                noNewWeaponStreak = offer.kind == UpgradeOffer.Kind.Weapon && offer.isNew ? 0 : noNewWeaponStreak + 1;
            }
            if (!offer.isChestItem)
            {
                traits.OnLevelUp();
            }
            OnItemsChanged();
        }

        private void ApplyWeapon(string id)
        {
            Weapon owned = FindWeapon(id);
            if (owned != null)
            {
                owned.SetRank(owned.rank + 1);
                return;
            }
            WeaponDef def = WeaponCatalog.FindWeapon(id);
            if (def != null && weaponCount < WEAPON_SLOTS)
            {
                AddWeapon(def);
            }
        }

        private void AddWeapon(WeaponDef def)
        {
            int slot = weaponCount++;
            Weapon weapon = def.Create(context, slot);
            weaponObjects[slot] = weapon;
            weapon.SetRank(1);
            run.record.ownedSince[slot] = run.runTime;
        }

        private void ApplyPassive(string id)
        {
            PassiveSlot owned = FindPassive(id);
            if (owned != null)
            {
                owned.SetRank(run.stats, owned.rank + 1);
                return;
            }
            PassiveDef def = WeaponCatalog.FindPassive(id);
            if (def != null && passiveCount < PASSIVE_SLOTS)
            {
                var slot = new PassiveSlot(def);
                passiveObjects[passiveCount++] = slot;
                slot.SetRank(run.stats, 1);
            }
        }

        private void ApplyEvolution(string evolutionId)
        {
            for (int i = 0; i < weaponCount; i++)
            {
                Weapon weapon = weaponObjects[i];
                if (weapon.def.evolution != null && weapon.def.evolution.id == evolutionId)
                {
                    weapon.Evolve();
                    Vector3 chest = run.hero.position + Ground.Up(EVOLVE_BURST_HEIGHT);
                    run.effects.Emit(run.effects.Find("EvolveBurst"), chest, EVOLVE_BURST_COUNT);
                    run.sounds.Play("evolve");
                    return;
                }
            }
        }

        /// <summary>Adds the next pick of a boon: the first pick's bonus divided by the square root of the pick count.</summary>
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
                effect.AddTo(run.stats, effect.perRank * share);
            }
        }

        private void ApplyFallback(FallbackDef fallback)
        {
            if (fallback == null)
            {
                return;
            }
            switch (fallback.kind)
            {
                case FallbackKind.Heal:
                    context.HealHero(run.hero.maxHealth * fallback.amount, HealSource.Card);
                    break;
                case FallbackKind.Gold:
                    run.AddGold((int)MathF.Round(fallback.amount));
                    break;
                case FallbackKind.GreedGold:
                    run.AddGold((int)MathF.Round(fallback.amount * run.stats[StatId.Greed]));
                    break;
                case FallbackKind.Might:
                    run.stats.AddPercent(StatId.Might, fallback.amount);
                    break;
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Slots
        // -------------------------------------------------------------------------------------------------------

        private Weapon FindWeapon(string id)
        {
            for (int i = 0; i < weaponCount; i++)
            {
                if (weaponObjects[i].def.id == id)
                {
                    return weaponObjects[i];
                }
            }
            return null;
        }

        private PassiveSlot FindPassive(string id)
        {
            for (int i = 0; i < passiveCount; i++)
            {
                if (passiveObjects[i].def.id == id)
                {
                    return passiveObjects[i];
                }
            }
            return null;
        }

        private bool OwnsPassive(string id) => FindPassive(id) != null;

        /// <summary>Rank 8, not evolved, and its paired passive owned: the next chest evolves it.</summary>
        private bool CanEvolve(Weapon weapon)
        {
            return weapon.def.evolution != null && !weapon.isEvolved && weapon.isMaxRank && OwnsPassive(weapon.def.evolution.passiveId);
        }

        /// <summary>After any pick: the traits and the evolution bonds read the passive ranks again, and the HUD views are rebuilt.</summary>
        private void OnItemsChanged()
        {
            traits.RefreshRanks();
            RefreshBonds();
            RebuildSlots();
        }

        /// <summary>Each evolved weapon's bond follows the rank of the passive that evolved it.</summary>
        private void RefreshBonds()
        {
            for (int i = 0; i < weaponCount; i++)
            {
                Weapon weapon = weaponObjects[i];
                if (weapon.def.evolution != null)
                {
                    weapon.SetBondRank(GetPassiveRank(weapon.def.evolution.passiveId));
                }
            }
        }

        private void RebuildSlots()
        {
            weaponSlots.Clear();
            for (int i = 0; i < weaponCount; i++)
            {
                Weapon weapon = weaponObjects[i];
                weaponSlots.Add(new SlotView
                {
                    id = weapon.displayId,
                    name = weapon.displayName,
                    icon = WeaponCatalog.IconKey(weapon.displayId),
                    rank = weapon.rank,
                    maxRank = WeaponDef.MAX_RANK,
                    isEvolved = weapon.isEvolved,
                    canEvolve = CanEvolve(weapon),
                    summary = WeaponText.DescribeLevel(weapon.def, weapon.level),
                });
            }
            passiveSlots.Clear();
            for (int i = 0; i < passiveCount; i++)
            {
                PassiveSlot passive = passiveObjects[i];
                passiveSlots.Add(new SlotView
                {
                    id = passive.def.id,
                    name = passive.def.name,
                    icon = WeaponCatalog.IconKey(passive.def.id),
                    rank = passive.rank,
                    maxRank = passive.def.maxRank,
                    summary = WeaponText.DescribePassiveWithTrait(passive.def, passive.rank),
                });
            }
        }
    }
}

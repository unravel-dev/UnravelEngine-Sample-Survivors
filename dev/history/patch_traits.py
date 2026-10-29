"""Passive traits and evolution bonds (design update 4): hooks into the loadout, kit, combat, strikes and weapon damage."""
import os

from patchlib import edit

ROOT = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(ROOT, "src", "Scripts")


def p(*parts):
    return os.path.join(S, *parts)


edit(p("Weapons", "Loadout.cs"), [
    ("""        private readonly ClassKit kit;""", """        private readonly ClassKit kit;
        /// <summary>The passive items' traits (Heartbeat, Spiked Plate, Tailwind, ...).</summary>
        public readonly PassiveTraits traits;"""),
    ("""            AddWeapon(WeaponCatalog.GetStartWeapon(heroClass));
            RebuildSlots();
        }""", """            AddWeapon(WeaponCatalog.GetStartWeapon(heroClass));
            RebuildSlots();
            traits = new PassiveTraits(run, this);
            kit.activated += traits.OnActive;
        }

        /// <summary>The rank of an owned passive, 0 when not owned.</summary>
        public int GetPassiveRank(string id)
        {
            PassiveSlot owned = FindPassive(id);
            return owned != null ? owned.rank : 0;
        }"""),
    ("""            context.zones.Tick(dt);
            context.feel.Flush(context.clock);""", """            context.zones.Tick(dt);
            traits.Tick(dt);
            context.feel.Flush(context.clock);"""),
    ("""            kit.OnKill(death);
            for (int i = 0; i < weaponCount; i++)""", """            kit.OnKill(death);
            traits.OnKill(death);
            for (int i = 0; i < weaponCount; i++)"""),
    ("""            kit.Dispose();""", """            kit.activated -= traits.OnActive;
            traits.Dispose();
            kit.Dispose();"""),
])

edit(p("Weapons", "Loadout.cs"), [("""            if (!offer.isChestItem && offer.kind != UpgradeOffer.Kind.Fallback)
            {
                noNewWeaponStreak = offer.kind == UpgradeOffer.Kind.Weapon && offer.isNew ? 0 : noNewWeaponStreak + 1;
            }
            RebuildSlots();""", """            if (!offer.isChestItem && offer.kind != UpgradeOffer.Kind.Fallback)
            {
                noNewWeaponStreak = offer.kind == UpgradeOffer.Kind.Weapon && offer.isNew ? 0 : noNewWeaponStreak + 1;
            }
            if (!offer.isChestItem)
            {
                traits.OnLevelUp();
            }
            RebuildSlots();""")])

edit(p("Weapons", "Kits", "ClassKit.cs"), [
    ("""        /// <summary>Kill rewards are multiplied by this while a weapon asks for it (Skullsplitter fissure kills).</summary>""",
     """        /// <summary>Raised after the active fires (Quickening Hourglass's Overdrive).</summary>
        public event Action activated;

        /// <summary>Kill rewards are multiplied by this while a weapon asks for it (Skullsplitter fissure kills).</summary>"""),
    ("""            cooldownLeft = activeCooldown;
            return true;""", """            cooldownLeft = activeCooldown;
            activated?.Invoke();
            return true;"""),
])

edit(p("Combat", "Combat.cs"), [("""            float healthBefore = enemies.GetHealthFraction(row);
            bool isHeavy""", """            float healthBefore = enemies.GetHealthFraction(row);
            if (healthBefore >= PassiveTraits.OPENING_STRIKE_HEALTH && run.loadout != null)
            {
                amount *= 1.0f + run.loadout.traits.openingStrikeBonus;
            }
            bool isHeavy""")])

edit(p("Weapons", "Framework", "Strikes.cs"), [("""            int found = enemies.QueryCircle(center, radius, rows);
            return Apply(found, center, center, damage, knockback, source, effect, mode, Vector3.zero, memory);""",
"""            int found = enemies.QueryCircle(center, radius, rows);
            int hits = Apply(found, center, center, damage, knockback, source, effect, mode, Vector3.zero, memory);
            if (hits > 0)
            {
                Run.current?.loadout?.traits.OnAreaHit(center, radius, damage);
            }
            return hits;""")])

edit(p("Weapons", "Framework", "Weapon.cs"), [
    ("""        protected float MightOf(float baseDamage, in CastInfo cast) => baseDamage * stats[StatId.Might] * cast.damageScale;""",
     """        protected float MightOf(float baseDamage, in CastInfo cast) => baseDamage * stats[StatId.Might] * cast.damageScale * bondFactor;"""),
    ("""        protected float MightOf(float baseDamage) => baseDamage * stats[StatId.Might];""",
     """        protected float MightOf(float baseDamage) => baseDamage * stats[StatId.Might] * bondFactor;

        /// <summary>
        /// The bond (design update 4): an evolution deals more per rank of the passive that evolved it, so ranking the
        /// passive stays worth it; 1 before the evolution.
        /// </summary>
        protected float bondFactor
        {
            get
            {
                Loadout loadout = ctx.run.loadout;
                if (!isEvolved || loadout == null)
                {
                    return 1.0f;
                }
                return 1.0f + PassiveTraits.BOND_PER_RANK * loadout.GetPassiveRank(def.evolution.passiveId);
            }
        }"""),
])

edit(p("Weapons", "WeaponText.cs"), [
    ("""            foreach (PassiveEffect effect in def.effects)
            {
                Append(text, effect.label + " " + FormatPassiveValue(effect, effect.perRank * rank), SEPARATOR);
            }
            return text.ToString();
        }""", """            foreach (PassiveEffect effect in def.effects)
            {
                Append(text, effect.label + " " + FormatPassiveValue(effect, effect.perRank * rank), SEPARATOR);
            }
            return text.ToString();
        }

        /// <summary>A passive at a rank with its trait sentence (pause sheet).</summary>
        public static string DescribePassiveWithTrait(PassiveDef def, int rank)
        {
            string trait = PassiveTraits.Describe(def.id, rank);
            return trait.Length > 0 ? DescribePassive(def, rank) + ". " + trait : DescribePassive(def, rank);
        }"""),
    ("""            if (fromRank <= 0)
            {
                return DescribePassive(def, toRank) + ".";
            }
            var text = new StringBuilder();
            foreach (PassiveEffect effect in def.effects)
            {
                Append(text, effect.label + " " + FormatPassiveValue(effect, effect.perRank * fromRank) + ARROW + FormatPassiveValue(effect, effect.perRank * toRank), LINE);
            }
            return text.ToString();""", """            string trait = PassiveTraits.Describe(def.id, toRank);
            if (fromRank <= 0)
            {
                return trait.Length > 0 ? DescribePassive(def, toRank) + "." + LINE + trait : DescribePassive(def, toRank) + ".";
            }
            var text = new StringBuilder();
            foreach (PassiveEffect effect in def.effects)
            {
                Append(text, effect.label + " " + FormatPassiveValue(effect, effect.perRank * fromRank) + ARROW + FormatPassiveValue(effect, effect.perRank * toRank), LINE);
            }
            if (trait.Length > 0)
            {
                Append(text, trait, LINE);
            }
            return text.ToString();"""),
])

edit(p("Weapons", "Loadout.cs"), [("""                    summary = WeaponText.DescribePassive(passive.def, passive.rank),""",
                                   """                    summary = WeaponText.DescribePassiveWithTrait(passive.def, passive.rank),""")])
edit(p("UI", "Screens", "DetailsScreen.cs"), [("""            SetText(description, "Every rank adds to your stats for the rest of the run. Passives are shared by all heroes.");""",
"""            string trait = def != null ? PassiveTraits.Describe(def.id, view.rank) : "";
            SetText(description, (trait.Length > 0 ? trait + " " : "") +
                                 $"The evolutions it unlocks deal +{PassiveTraits.BOND_PER_RANK * 100.0f:0}% damage per rank. Passives are shared by all heroes.");""")])
print("traits patched")

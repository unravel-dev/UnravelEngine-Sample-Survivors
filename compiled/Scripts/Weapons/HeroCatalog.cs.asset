using System;
using System.Collections.Generic;

namespace Survivors
{
    /// <summary>What the hero select screen shows about a class.</summary>
    public sealed class HeroInfo
    {
        public HeroClass heroClass;
        public string name = "";
        /// <summary>"The Oath That Holds".</summary>
        public string title = "";
        public string description = "";
        public string passiveName = "";
        public string passiveText = "";
        public string activeName = "";
        public string activeText = "";
        public string startWeaponName = "";
        public string startWeaponIcon = "";
        public float maxHealth;
        public float armor;
        public float moveSpeed;
        /// <summary>The save's unlock condition in words, shown while locked.</summary>
        public string unlockHint = "";
    }

    /// <summary>A starting stat a class sets instead of the <see cref="PlayerStats"/> default.</summary>
    public readonly struct StatOverride
    {
        public readonly StatId stat;
        public readonly float value;

        public StatOverride(StatId stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }

    /// <summary>
    /// One hero class: its starting stats (only those that differ from the <see cref="PlayerStats"/> defaults), its
    /// hero select text, its start weapon and its class kit (active ability and signature passive). The kit's texts are
    /// built from the kit's own numbers.
    /// </summary>
    public sealed class HeroClassDef
    {
        public HeroClass heroClass;
        public string name = "";
        /// <summary>"The Oath That Holds".</summary>
        public string title = "";
        public string description = "";
        /// <summary>The save's unlock condition in words, shown while locked.</summary>
        public string unlockHint = "";
        /// <summary>One of the class's own weapons.</summary>
        public string startWeaponId = "";
        /// <summary>Id (icon key) and name of the signature passive.</summary>
        public string signatureId = "";
        public string signatureName = "";
        public string signatureText = "";
        /// <summary>Id (icon key) and name of the active ability.</summary>
        public string activeId = "";
        public string activeName = "";
        public string activeText = "";
        public StatOverride[] stats = Array.Empty<StatOverride>();
        /// <summary>Builds the class's active and signature for a run.</summary>
        public Func<WeaponContext, ClassKit> createKit;
    }

    /// <summary>
    /// The hero classes, one definition each, indexed by <see cref="HeroClass"/>. The table is checked once, on first
    /// use: every class has its definition, its kit and a start weapon of its own.
    /// </summary>
    public static class HeroCatalog
    {
        private static readonly HeroClassDef[] classes =
        {
            new HeroClassDef
            {
                heroClass = HeroClass.Knight,
                name = "Knight",
                title = "The Oath That Holds",
                description = "The wall in the storm. Slow and armored, every swing is a heavy steel arc that throws skeletons back.",
                unlockHint = "Unlocked from the start.",
                startWeaponId = "oathblade_arc",
                signatureId = "bulwark",
                signatureName = "Bulwark",
                signatureText = KnightKit.SIGNATURE_TEXT,
                activeId = "shield_rush",
                activeName = "Shield Rush",
                activeText = KnightKit.ACTIVE_TEXT,
                stats = new[]
                {
                    Stat(StatId.MaxHealth, 130.0f), Stat(StatId.Regeneration, 0.4f), Stat(StatId.Armor, 4.0f), Stat(StatId.MoveSpeed, 4.7f),
                    Stat(StatId.BlockChance, KnightKit.BLOCK_CHANCE),
                },
                createKit = context => new KnightKit(context),
            },
            new HeroClassDef
            {
                heroClass = HeroClass.Barbarian,
                name = "Barbarian",
                title = "The Red Storm",
                description = "A walking avalanche with two axes. Huge cleaves, enormous knockback, and the more he kills the stronger he gets.",
                unlockHint = "Defeat the Gravewarden once.",
                startWeaponId = "ravager_chop",
                signatureId = "bloodlust",
                signatureName = "Bloodlust",
                signatureText = BarbarianKit.SIGNATURE_TEXT,
                activeId = "earthshaker_leap",
                activeName = "Earthshaker Leap",
                activeText = BarbarianKit.ACTIVE_TEXT,
                stats = new[]
                {
                    Stat(StatId.MaxHealth, 140.0f), Stat(StatId.Armor, 2.0f), Stat(StatId.MoveSpeed, 4.9f), Stat(StatId.Might, 1.10f),
                    Stat(StatId.Area, 1.10f), Stat(StatId.Knockback, 1.30f),
                },
                createKit = context => new BarbarianKit(context),
            },
            new HeroClassDef
            {
                heroClass = HeroClass.Mage,
                name = "Mage",
                title = "The Last Candle",
                description = "Fragile, far-reaching, spectacular. She never touches the horde; the sky does it for her.",
                unlockHint = "Reach level 20 in one run.",
                startWeaponId = "ember_orb",
                signatureId = "arcane_echo",
                signatureName = "Arcane Echo",
                signatureText = MageKit.SIGNATURE_TEXT,
                activeId = "blink",
                activeName = "Blink",
                activeText = MageKit.ACTIVE_TEXT,
                stats = new[]
                {
                    Stat(StatId.MaxHealth, 95.0f), Stat(StatId.MoveSpeed, 5.0f), Stat(StatId.Area, 1.10f), Stat(StatId.Cooldown, 0.92f),
                    Stat(StatId.Luck, 10.0f), Stat(StatId.PickupRadius, 2.6f),
                },
                createKit = context => new MageKit(context),
            },
            new HeroClassDef
            {
                heroClass = HeroClass.Rogue,
                name = "Rogue",
                title = "The Knife in the Fog",
                description = "A hooded blur that never stands still: crossbow bolts, thrown steel and an executioner's mark.",
                unlockHint = "Defeat the Hollow Archmage once.",
                startWeaponId = "repeater_crossbow",
                signatureId = "momentum",
                signatureName = "Momentum",
                signatureText = RogueKit.SIGNATURE_TEXT,
                activeId = "smoke_roll",
                activeName = "Smoke Roll",
                activeText = RogueKit.ACTIVE_TEXT,
                stats = new[]
                {
                    Stat(StatId.MaxHealth, 105.0f), Stat(StatId.Armor, 1.0f), Stat(StatId.MoveSpeed, 5.5f), Stat(StatId.Might, 1.15f),
                    Stat(StatId.CritChance, 0.15f), Stat(StatId.CritDamage, 1.75f), Stat(StatId.Luck, 10.0f), Stat(StatId.ProjectileSpeed, 1.10f),
                },
                createKit = context => new RogueKit(context),
            },
        };

        static HeroCatalog()
        {
            Validate();
        }

        /// <summary>The definition of a class; throws for a class the catalog does not know.</summary>
        public static HeroClassDef GetClass(HeroClass heroClass)
        {
            int index = (int)heroClass;
            if (index < 0 || index >= classes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(heroClass), heroClass, "HeroCatalog has no such hero class.");
            }
            return classes[index];
        }

        /// <summary>The hero select text and stat bars of a class, as a new object the caller may keep.</summary>
        public static HeroInfo Get(HeroClass heroClass)
        {
            HeroClassDef def = GetClass(heroClass);
            WeaponDef start = WeaponCatalog.FindWeapon(def.startWeaponId);
            var stats = new PlayerStats();
            ApplyClassStats(heroClass, stats);
            return new HeroInfo
            {
                heroClass = heroClass,
                name = def.name,
                title = def.title,
                description = def.description,
                passiveName = def.signatureName,
                passiveText = def.signatureText,
                activeName = def.activeName,
                activeText = def.activeText,
                startWeaponName = start.name,
                startWeaponIcon = WeaponCatalog.IconKey(start.id),
                maxHealth = stats.GetBase(StatId.MaxHealth),
                armor = stats.GetBase(StatId.Armor),
                moveSpeed = stats.GetBase(StatId.MoveSpeed),
                unlockHint = def.unlockHint,
            };
        }

        /// <summary>Writes the class's starting stats as bases over fresh <see cref="PlayerStats"/>.</summary>
        public static void ApplyClassStats(HeroClass heroClass, PlayerStats stats)
        {
            foreach (StatOverride entry in GetClass(heroClass).stats)
            {
                stats.SetBase(entry.stat, entry.value);
            }
        }

        private static StatOverride Stat(StatId stat, float value) => new StatOverride(stat, value);

        private static void Validate()
        {
            var problems = new List<string>();
            if ((int)HeroClass.Count != classes.Length)
            {
                problems.Add("Every hero class needs exactly one definition.");
            }
            for (int i = 0; i < classes.Length; i++)
            {
                ValidateClass(i, classes[i], problems);
            }
            if (problems.Count > 0)
            {
                throw new InvalidOperationException("HeroCatalog: " + string.Join(" ", problems));
            }
        }

        private static void ValidateClass(int index, HeroClassDef def, List<string> problems)
        {
            if ((int)def.heroClass != index)
            {
                problems.Add($"{def.heroClass} sits at index {index}.");
            }
            if (def.createKit == null)
            {
                problems.Add($"{def.heroClass} has no class kit.");
            }
            WeaponDef start = WeaponCatalog.FindWeapon(def.startWeaponId);
            if (start == null || !start.IsClassWeaponOf(def.heroClass))
            {
                problems.Add($"{def.heroClass} starts with {def.startWeaponId}, which is not one of its class weapons.");
            }
        }
    }
}

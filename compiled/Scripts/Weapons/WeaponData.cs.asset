using System;

namespace Survivors
{
    /// <summary>
    /// What kind of weapon it is, for the details card. Display only: each weapon class decides which stats it reads.
    /// </summary>
    [Flags]
    public enum WeaponTags
    {
        None = 0,
        Melee = 1 << 0,
        Projectile = 1 << 1,
        Area = 1 << 2,
        Lingering = 1 << 3,
        Summon = 1 << 4,
        SingleTarget = 1 << 5,
    }

    /// <summary>
    /// The numbers a weapon rank can carry. Each weapon uses the subset its rank table names; the weapon's class
    /// documents what each field means for it (Radius is a slash radius for one weapon and a fissure length for another).
    /// The Sub* fields describe a weapon's secondary effect (embers, a fissure, a thunderclap, a crater) and the Major*
    /// fields the big extra an evolution adds (a great meteor, the maw's slam, Smoke Roll's clones).
    /// </summary>
    public enum WeaponField
    {
        Damage,
        Cooldown,
        Radius,
        Count,
        /// <summary>Enemies passed through after the first; -1 = all.</summary>
        Pierce,
        /// <summary>Seconds a lingering effect lasts; -1 = permanent.</summary>
        Duration,
        /// <summary>Degrees of an arc or cone.</summary>
        Angle,
        /// <summary>Knockback distance (m).</summary>
        Knockback,
        Speed,
        Range,
        /// <summary>Seconds between ticks, zaps, dives or bolts of a burst.</summary>
        Interval,
        /// <summary>Seconds between the parts of one cast (swings, swords, meteors, novas).</summary>
        Stagger,
        Chance,
        /// <summary>Seconds a freeze lasts.</summary>
        Freeze,
        /// <summary>Seconds a stun lasts.</summary>
        Stun,
        /// <summary>Share of speed removed (0.25 = -25%).</summary>
        Slow,
        Width,
        Size,
        /// <summary>Burn damage per burn tick.</summary>
        Burn,
        /// <summary>Extra damage taken or dealt (0.2 = +20%).</summary>
        Bonus,
        BonusCap,
        CritChance,
        CritDamage,
        /// <summary>1 when projectiles come back to the hero.</summary>
        Returns,
        /// <summary>Homing turn rate, degrees per second.</summary>
        Turn,
        /// <summary>What one point of Amount adds (knives, nova radius share, duration share).</summary>
        AmountScale,
        MoveSpeed,
        Pull,
        Heal,
        HealCap,
        /// <summary>Every Nth swing, or a hit on an enemy carrying N stacks, does something extra.</summary>
        Every,
        /// <summary>Health share under which a marked target is executed.</summary>
        Execute,
        SubCount,
        SubDamage,
        SubRadius,
        SubSpeed,
        SubRange,
        SubDuration,
        /// <summary>Seconds between the ticks or pulses of the secondary effect (a crater, a pulse, a shock's rest).</summary>
        SubInterval,
        MajorDamage,
        MajorRadius,
        MajorDelay,
        /// <summary>Armor granted while the effect holds.</summary>
        Armor,
        /// <summary>Seconds enemies flee.</summary>
        Fear,
        /// <summary>Fury granted per enemy hit (Barbarian).</summary>
        Fury,
        /// <summary>A stack count: the most venom stacks one enemy carries (-1 = no cap), or the Momentum a weapon waits for.</summary>
        Stacks,
        /// <summary>Fury a kill by this weapon grants instead of the usual one (Barbarian).</summary>
        FuryPerKill,
        /// <summary>Experience a kill by this weapon grants on top of the gem (before Growth).</summary>
        ExperiencePerKill,
        /// <summary>Seconds between the ticks of a burn this weapon sets (<see cref="Burn"/> is the damage per tick).</summary>
        BurnTick,
        /// <summary>A multiplier on a number the weapon names (Venom Shivs: a rupture deals the venom left times this).</summary>
        Multiplier,
        /// <summary>How many of the evolution's big extra there are (Legion of Shadows: the clones each Smoke Roll leaves).</summary>
        MajorCount,
        FieldCount,
    }

    /// <summary>The numbers of one weapon rank (or of its evolution).</summary>
    public sealed class WeaponLevel
    {
        private readonly float[] values = new float[(int)WeaponField.FieldCount];

        public float this[WeaponField field] => values[(int)field];

        /// <summary>A count field rounded to an integer.</summary>
        public int GetCount(WeaponField field) => (int)MathF.Round(values[(int)field]);

        internal void Set(WeaponField field, float value) => values[(int)field] = value;

        internal WeaponLevel Clone()
        {
            var copy = new WeaponLevel();
            Array.Copy(values, copy.values, values.Length);
            return copy;
        }
    }

    /// <summary>A column of a weapon's rank table: which field, and how a card names it.</summary>
    public readonly struct WeaponColumn
    {
        public readonly WeaponField field;
        public readonly string label;

        public WeaponColumn(WeaponField field, string label)
        {
            this.field = field;
            this.label = label;
        }
    }

    /// <summary>A weapon's evolved form: its paired passive and the numbers that replace rank 8.</summary>
    public sealed class EvolutionDef
    {
        public string id = "";
        public string name = "";
        /// <summary>The passive that must be owned (any rank) for the weapon at rank 8 to evolve.</summary>
        public string passiveId = "";
        public string description = "";
        /// <summary>Rank 8 numbers with the evolution's overrides.</summary>
        public WeaponLevel level;
    }

    /// <summary>Builds the behaviour of a weapon definition in a loadout slot.</summary>
    public delegate Weapon WeaponFactory(WeaponContext context, WeaponDef def, int slot);

    /// <summary>One weapon: identity, offer rules, rank table, evolution and the class that runs it.</summary>
    public sealed class WeaponDef
    {
        public const int MAX_RANK = 8;

        public string id = "";
        public string name = "";
        /// <summary>What the weapon does, for the NEW card.</summary>
        public string description = "";
        public Rarity rarity;
        /// <summary>Only offered to <see cref="heroClass"/> when set.</summary>
        public bool isClassWeapon;
        /// <summary>The class a class weapon belongs to; ignore it for shared weapons (see <see cref="IsOfferedTo"/>).</summary>
        public HeroClass heroClass;
        public WeaponTags tags;
        /// <summary>The rank table's columns in card order.</summary>
        public WeaponColumn[] columns = Array.Empty<WeaponColumn>();
        /// <summary>Ranks 1..8 at index 0..7.</summary>
        public WeaponLevel[] levels = Array.Empty<WeaponLevel>();
        /// <summary>A note per rank (index 0..7) for the card, empty when none.</summary>
        public string[] notes = Array.Empty<string>();
        public EvolutionDef evolution;
        /// <summary>Builds the weapon's behaviour class.</summary>
        public WeaponFactory factory;

        public WeaponLevel GetLevel(int rank) => levels[Math.Clamp(rank, 1, MAX_RANK) - 1];

        public string GetNote(int rank) => rank >= 1 && rank <= notes.Length ? notes[rank - 1] : "";

        /// <summary>Whether a hero of <paramref name="hero"/> may be offered this weapon: shared weapons and its own class weapons.</summary>
        public bool IsOfferedTo(HeroClass hero) => !isClassWeapon || heroClass == hero;

        /// <summary>Whether this is a class weapon of <paramref name="hero"/> (its class kit's hooks apply to it).</summary>
        public bool IsClassWeaponOf(HeroClass hero) => isClassWeapon && heroClass == hero;

        /// <summary>The weapon's behaviour for a loadout slot.</summary>
        public Weapon Create(WeaponContext context, int slot) => factory(context, this, slot);
    }

    /// <summary>What a passive or boon changes: one stat, per rank or per pick.</summary>
    public readonly struct PassiveEffect
    {
        public readonly StatId stat;
        /// <summary>Percent bonuses use AddPercent; the others AddFlat.</summary>
        public readonly bool isPercent;
        public readonly float perRank;
        public readonly string label;
        /// <summary>Unit after a flat value (" HP/s"), empty for none.</summary>
        public readonly string unit;
        /// <summary>A flat 0..1 value shown as a percent (crit chance).</summary>
        public readonly bool showsPercent;

        public PassiveEffect(StatId stat, bool isPercent, float perRank, string label, string unit = "", bool showsPercent = false)
        {
            this.stat = stat;
            this.isPercent = isPercent;
            this.perRank = perRank;
            this.label = label;
            this.unit = unit;
            this.showsPercent = showsPercent;
        }

        /// <summary>A bonus of this effect's stat that moves with the rank, adding only the difference.</summary>
        public StatBonus CreateBonus() => new StatBonus(stat, isPercent);

        /// <summary>Adds <paramref name="amount"/> of this effect's stat once.</summary>
        public void AddTo(PlayerStats stats, float amount) => StatBonus.Add(stats, stat, isPercent, amount);
    }

    /// <summary>
    /// A boon: a small stat bonus that takes no slot and can be taken without limit. The n-th pick adds the first
    /// pick's bonus divided by the square root of n, so 10 picks are worth about 5 first picks.
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

    /// <summary>One passive item: its stat bonus per rank (its trait lives in <see cref="PassiveTraits"/>).</summary>
    public sealed class PassiveDef
    {
        public string id = "";
        public string name = "";
        public Rarity rarity;
        public int maxRank = 5;
        public PassiveEffect[] effects = Array.Empty<PassiveEffect>();
    }

    /// <summary>What a fallback card gives.</summary>
    public enum FallbackKind
    {
        /// <summary>Heals <see cref="FallbackDef.amount"/> of max health.</summary>
        Heal,
        /// <summary><see cref="FallbackDef.amount"/> gold.</summary>
        Gold,
        /// <summary><see cref="FallbackDef.amount"/> gold scaled by the Greed stat.</summary>
        GreedGold,
        /// <summary><see cref="FallbackDef.amount"/> percent Might for the rest of the run.</summary>
        Might,
    }

    /// <summary>A card offered when nothing else is eligible, and the chest's gold item.</summary>
    public sealed class FallbackDef
    {
        public string id = "";
        public string name = "";
        public FallbackKind kind;
        /// <summary>The share healed, the gold given or the Might bonus, by <see cref="kind"/>.</summary>
        public float amount;
        public string description = "";
    }
}

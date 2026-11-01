using System;

namespace Survivors
{
    /// <summary>Every stat a hero has. Weapons read the ones they use when they fire.</summary>
    public enum StatId
    {
        /// <summary>Health at full.</summary>
        MaxHealth,
        /// <summary>Health regained per second.</summary>
        Regeneration,
        /// <summary>Damage reduction: taken = raw x 15 / (15 + armor), at least 1 (see <see cref="PlayerStats.ARMOR_HALVES_AT"/>).</summary>
        Armor,
        /// <summary>Movement speed, m/s.</summary>
        MoveSpeed,
        /// <summary>Damage multiplier of every weapon.</summary>
        Might,
        /// <summary>Radius / length multiplier of areas, slashes and explosions.</summary>
        Area,
        /// <summary>Cooldown multiplier (1 = base; 0.7 = 30% shorter), never below 0.5.</summary>
        Cooldown,
        /// <summary>Extra projectiles, swings or summons per attack.</summary>
        Amount,
        /// <summary>Projectile speed multiplier.</summary>
        ProjectileSpeed,
        /// <summary>Duration multiplier of lingering effects.</summary>
        Duration,
        /// <summary>Radius (m) within which gems and gold fly to the hero.</summary>
        PickupRadius,
        /// <summary>0..100: rarer offers, better chests, more drops.</summary>
        Luck,
        /// <summary>Experience multiplier.</summary>
        Growth,
        /// <summary>Gold multiplier.</summary>
        Greed,
        /// <summary>Chance (0..1) a hit is critical.</summary>
        CritChance,
        /// <summary>Damage multiplier of a critical hit.</summary>
        CritDamage,
        /// <summary>Knockback multiplier.</summary>
        Knockback,
        /// <summary>Chance (0..1) to block a hit entirely (the Knight's Bulwark).</summary>
        BlockChance,
        /// <summary>Extra lives this run.</summary>
        Revival,
        /// <summary>Rerolls of level-up offers this run.</summary>
        Reroll,
        /// <summary>Items that can still be banished this run.</summary>
        Banish,
        Count,
    }

    /// <summary>
    /// The hero's stats: a base value per stat (from the class) plus flat and percent bonuses (passives, meta ranks,
    /// buffs). Final = clamp((base + flat) x (1 + percent), min, max). Reads are cached until a bonus changes.
    /// </summary>
    public sealed class PlayerStats
    {
        /// <summary>The Armor at which a hit is halved; every point of armor counts less than the one before.</summary>
        public const float ARMOR_HALVES_AT = 15.0f;
        /// <summary>The least damage a hit does, whatever the armor.</summary>
        private const float MIN_DAMAGE_TAKEN = 1.0f;
        /// <summary>Active abilities get this share of the Cooldown stat's reduction.</summary>
        private const float ACTIVE_COOLDOWN_SHARE = 0.5f;
        private const int COUNT = (int)StatId.Count;
        private const float CAP_EPSILON = 1e-4f;

        private readonly float[] baseValues = new float[COUNT];
        private readonly float[] flatBonus = new float[COUNT];
        private readonly float[] percentBonus = new float[COUNT];
        private readonly float[] finalValues = new float[COUNT];
        private readonly float[] minimum = new float[COUNT];
        private readonly float[] maximum = new float[COUNT];
        private bool isDirty = true;

        /// <summary>Raised after any bonus changes, so views and the hero can refresh.</summary>
        public event Action changed;

        public PlayerStats()
        {
            for (int i = 0; i < COUNT; i++)
            {
                maximum[i] = float.MaxValue;
            }
            Define(StatId.MaxHealth, 100.0f, 1.0f, float.MaxValue);
            Define(StatId.Regeneration, 0.0f, 0.0f, 6.0f);
            Define(StatId.Armor, 0.0f, 0.0f, 45.0f);
            Define(StatId.MoveSpeed, 5.0f, 1.0f, 8.0f);
            Define(StatId.Might, 1.0f, 0.1f, float.MaxValue);
            Define(StatId.Area, 1.0f, 0.25f, 2.0f);
            Define(StatId.Cooldown, 1.0f, 0.5f, 2.0f);
            Define(StatId.Amount, 0.0f, 0.0f, 4.0f);
            Define(StatId.ProjectileSpeed, 1.0f, 0.25f, 2.0f);
            Define(StatId.Duration, 1.0f, 0.25f, 2.0f);
            Define(StatId.PickupRadius, 2.0f, 0.5f, 6.0f);
            Define(StatId.Luck, 0.0f, 0.0f, 100.0f);
            Define(StatId.Growth, 1.0f, 0.0f, 2.0f);
            Define(StatId.Greed, 1.0f, 0.0f, 2.0f);
            Define(StatId.CritChance, 0.05f, 0.0f, 0.75f);
            Define(StatId.CritDamage, 1.5f, 1.0f, 3.0f);
            Define(StatId.Knockback, 1.0f, 0.0f, 2.0f);
            Define(StatId.BlockChance, 0.0f, 0.0f, 0.4f);
            Define(StatId.Revival, 0.0f, 0.0f, 2.0f);
            Define(StatId.Reroll, 0.0f, 0.0f, 5.0f);
            Define(StatId.Banish, 0.0f, 0.0f, 3.0f);
        }

        /// <summary>The final value of a stat.</summary>
        public float this[StatId stat] => Get(stat);

        public float Get(StatId stat)
        {
            if (isDirty)
            {
                Recompute();
            }
            return finalValues[(int)stat];
        }

        /// <summary>The integer value of a count stat (Amount, Revival, Reroll, Banish).</summary>
        public int GetCount(StatId stat) => (int)MathF.Round(Get(stat));

        public float GetBase(StatId stat) => baseValues[(int)stat];

        public void SetBase(StatId stat, float value)
        {
            baseValues[(int)stat] = value;
            MarkDirty();
        }

        /// <summary>Adds to a stat before percent bonuses (negative removes).</summary>
        public void AddFlat(StatId stat, float amount)
        {
            flatBonus[(int)stat] += amount;
            MarkDirty();
        }

        /// <summary>Adds a percent bonus (0.1 = +10%, -0.06 = -6%) to a stat.</summary>
        public void AddPercent(StatId stat, float amount)
        {
            percentBonus[(int)stat] += amount;
            MarkDirty();
        }

        /// <summary>
        /// Whether more of a bonus would change nothing: the stat sits at its maximum, or at its minimum for a bonus that
        /// lowers it (<paramref name="isReduction"/>, such as a Cooldown cut).
        /// </summary>
        public bool IsCapped(StatId stat, bool isReduction)
        {
            float value = Get(stat);
            return isReduction ? value <= minimum[(int)stat] + CAP_EPSILON : value >= maximum[(int)stat] - CAP_EPSILON;
        }

        /// <summary>The damage a raw hit does after armor.</summary>
        public float GetDamageTaken(float raw) => MathF.Max(MIN_DAMAGE_TAKEN, raw * armorPassThrough);

        /// <summary>What a raw hit is multiplied by so that armor counts only half (boss hits).</summary>
        public float halfArmorFactor
        {
            get
            {
                float armor = Get(StatId.Armor);
                return (ARMOR_HALVES_AT + armor) / (ARMOR_HALVES_AT + armor * 0.5f);
            }
        }

        /// <summary>Armor as the reduction it gives (0.5 = half damage), for the stat sheet.</summary>
        public float armorReduction => 1.0f - armorPassThrough;

        /// <summary>The cooldown multiplier of an active ability: the stat applies at half strength.</summary>
        public float activeCooldownFactor => 1.0f - (1.0f - Get(StatId.Cooldown)) * ACTIVE_COOLDOWN_SHARE;

        /// <summary>The share of a raw hit that armor lets through.</summary>
        private float armorPassThrough => ARMOR_HALVES_AT / (ARMOR_HALVES_AT + Get(StatId.Armor));

        private void Define(StatId stat, float baseValue, float min, float max)
        {
            baseValues[(int)stat] = baseValue;
            minimum[(int)stat] = min;
            maximum[(int)stat] = max;
        }

        private void MarkDirty()
        {
            isDirty = true;
            changed?.Invoke();
        }

        private void Recompute()
        {
            for (int i = 0; i < COUNT; i++)
            {
                float value = (baseValues[i] + flatBonus[i]) * (1.0f + percentBonus[i]);
                finalValues[i] = Math.Clamp(value, minimum[i], maximum[i]);
            }
            isDirty = false;
        }
    }
}

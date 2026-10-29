using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>How a weapon hit treats crits and its number.</summary>
    public struct HitOptions
    {
        /// <summary>Rolls a critical hit from the hero's stats (plus <see cref="critChanceBonus"/>).</summary>
        public bool canCrit;
        /// <summary>The hit is critical whatever the roll (Smoke Roll, Death Sentence).</summary>
        public bool forceCrit;
        public float critChanceBonus;
        /// <summary>Added to the crit damage multiplier (0.25 = +25%).</summary>
        public float critDamageBonus;
        /// <summary>Colour of the number: Normal picks Critical on a crit.</summary>
        public DamageKind numberKind;
        public bool hideNumber;

        /// <summary>A plain hit that may crit.</summary>
        public static HitOptions Default => new HitOptions { canCrit = true };
    }

    /// <summary>
    /// Hero damage against enemies, in one place: critical rolls, knockback from the hero's stats, damage numbers,
    /// hit sparks, hit sounds and per-weapon damage records. Weapons pass the damage they computed with Might.
    /// Main thread.
    /// </summary>
    public static class Combat
    {
        private const int SCRATCH_CAPACITY = 512;
        /// <summary>Damage numbers start at the top of the body; sparks fly from this share of its height.</summary>
        private const float SPARK_HEIGHT_SHARE = 0.6f;
        /// <summary>A hit this share of the enemy's health or more staggers it.</summary>
        private const float HEAVY_HIT_SHARE = 0.35f;

        private static readonly int[] scratch = new int[SCRATCH_CAPACITY];

        /// <summary>Damages one enemy as the hero; rolls a critical hit unless <paramref name="canCrit"/> is false.</summary>
        public static DamageResult DamageEnemy(int row, float damage, Vector3 push, int source, bool canCrit = true)
        {
            var options = new HitOptions { canCrit = canCrit };
            return DamageEnemy(row, damage, push, source, in options, out _);
        }

        /// <summary>Damages one enemy as the hero with explicit crit and number options; reports whether it crit.</summary>
        public static DamageResult DamageEnemy(int row, float damage, Vector3 push, int source, in HitOptions options, out bool isCritical)
        {
            isCritical = false;
            Run run = Run.current;
            if (run == null)
            {
                return DamageResult.Missed;
            }
            EnemyWorld enemies = run.enemies;
            if (!enemies.IsTargetable(row))
            {
                return DamageResult.Missed;
            }
            PlayerStats stats = run.stats;
            isCritical = options.forceCrit || options.canCrit && Random.Float() < stats[StatId.CritChance] + options.critChanceBonus;
            float critMultiplier = stats[StatId.CritDamage] + options.critDamageBonus;
            float amount = (isCritical ? damage * critMultiplier : damage) * enemies.GetDamageTakenMultiplier(row);
            Vector3 position = enemies.GetPosition(row);
            float height = enemies.GetHeight(row);
            float healthBefore = enemies.GetHealthFraction(row);
            if (healthBefore >= PassiveTraits.OPENING_STRIKE_HEALTH && run.loadout != null)
            {
                amount *= 1.0f + run.loadout.traits.openingStrikeBonus;
            }
            bool isHeavy = isCritical || healthBefore > 0.0f && amount >= HEAVY_HIT_SHARE * enemies.GetMaxHealth(row);
            DamageResult result = enemies.Damage(row, amount, push * stats[StatId.Knockback], source, isHeavy, isCritical);
            if (result == DamageResult.Missed)
            {
                return result;
            }
            run.record.AddDamage(source, amount);
            run.combatEvents.RaiseHit(row, source, amount, isCritical);
            if (!options.hideNumber && run.damageNumbers != null)
            {
                DamageKind kind = isCritical ? DamageKind.Critical : options.numberKind;
                run.damageNumbers.Show(position + new Vector3(0.0f, height, 0.0f), amount, kind);
            }
            Vector3 chest = position + new Vector3(0.0f, height * SPARK_HEIGHT_SHARE, 0.0f);
            run.feedback.OnEnemyHit(chest, isCritical, result == DamageResult.Killed);
            return result;
        }

        /// <summary>Damages every enemy whose ground circle touches a circle; returns how many were hit.</summary>
        public static int DamageCircle(Vector3 center, float radius, float damage, float knockback, int source)
        {
            Run run = Run.current;
            if (run == null)
            {
                return 0;
            }
            int found = run.enemies.QueryCircle(center, radius, scratch);
            for (int i = 0; i < found; i++)
            {
                int row = scratch[i];
                DamageEnemy(row, damage, GetPushFrom(center, run.enemies.GetPosition(row), knockback), source);
            }
            return found;
        }

        /// <summary>A knockback vector pointing from a source to an enemy.</summary>
        public static Vector3 GetPushFrom(Vector3 source, Vector3 target, float speed)
        {
            Vector3 away = target - source;
            away.y = 0.0f;
            float length = away.magnitude;
            return length > 1e-4f ? away * (speed / length) : Vector3.zero;
        }
    }
}

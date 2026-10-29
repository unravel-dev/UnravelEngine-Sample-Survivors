using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Which way an area hit pushes the enemies it hits.</summary>
    public enum PushMode : byte
    {
        /// <summary>Away from the center (or the segment).</summary>
        Outward,
        /// <summary>Toward the center.</summary>
        Inward,
        /// <summary>Mostly around the center, a little outward: enemies keep swirling.</summary>
        Swirl,
        /// <summary>Along a given direction.</summary>
        Along,
        /// <summary>Away from the segment's line, to whichever side the enemy stands.</summary>
        Sideways,
        None,
    }

    /// <summary>What a hit does besides damage: statuses and critical-hit changes.</summary>
    public struct HitEffect
    {
        public float stunSeconds;
        /// <summary>Speed multiplier while slowed (0.6 = -40%); 0 for no slow.</summary>
        public float slowFactor;
        public float slowSeconds;
        public float burnPerSecond;
        public float burnSeconds;
        public float freezeChance;
        public float freezeSeconds;
        /// <summary>Elites freeze as long as others (the enemy world halves elite freezes otherwise).</summary>
        public bool freezesElitesFully;
        public float markBonus;
        public float markSeconds;
        public float critChanceBonus;
        public float critDamageBonus;
        public bool forceCrit;
        /// <summary>Colour of the damage number (Burn, Frost, Echo); Normal shows Critical on a crit.</summary>
        public DamageKind numberKind;
        /// <summary>No damage number (small aura ticks).</summary>
        public bool hideNumber;
    }

    /// <summary>
    /// A ground circle that hurt at least one enemy: its center, radius and damage per enemy (after Might).
    /// </summary>
    public delegate void AreaHitHandler(Vector3 center, float radius, float damage);

    /// <summary>
    /// Hero damage for weapons, class kits and traits on top of <see cref="Combat"/>: area queries into its own buffers
    /// (so it can report what it hit), pushes by mode, statuses after the hit, per-cast hit memories, and the weapon's
    /// critical-hit bonuses and number colour passed to Combat as <see cref="HitOptions"/>. Circles that deal damage are
    /// reported through <see cref="areaHit"/> (Wide Sigil's Aftershock listens). Main thread.
    /// </summary>
    public sealed class Strikes
    {
        private const int CAPACITY = 1024;
        /// <summary>Bosses never freeze: a freeze slows them by this factor for <see cref="BOSS_SLOW_SECONDS"/>.</summary>
        private const float BOSS_SLOW_FACTOR = 0.5f;
        private const float BOSS_SLOW_SECONDS = 1.0f;
        /// <summary>
        /// The enemy world halves every elite freeze; a freeze that holds elites as long as the others asks it for twice
        /// the time.
        /// </summary>
        private const float FULL_ELITE_FREEZE_FACTOR = 2.0f;
        private const float SWIRL_TANGENT = 0.8f;
        private const float SWIRL_OUTWARD = 0.45f;
        private const float BOSS_SCORE = 1.0e9f;
        private const float ELITE_SCORE = 1.0e6f;

        private readonly EnemyWorld enemies;
        private readonly int[] rows = new int[CAPACITY];
        private readonly int[] hitRows = new int[CAPACITY];

        /// <summary>Enemies hurt or killed by the last area call.</summary>
        public int lastHitCount;
        /// <summary>Whether the last single hit was critical.</summary>
        public bool lastWasCritical;

        /// <summary>Raised after a <see cref="Circle"/> that dealt damage hurt at least one enemy (not for status-only circles).</summary>
        public event AreaHitHandler areaHit;

        public Strikes(Run run)
        {
            enemies = run.enemies;
        }

        /// <summary>Rows hurt or killed by the last area call (valid until the next call).</summary>
        public ReadOnlySpan<int> lastHits => hitRows.AsSpan(0, lastHitCount);

        /// <summary>Whether a row belongs to a boss (a proxy row a boss entity drives).</summary>
        public bool IsBoss(int row) => enemies.IsProxy(row);

        /// <summary>A knockback distance (m) as push speed; Combat applies the Knockback stat on top.</summary>
        public static float KnockbackOf(float metres) => metres * Tuning.KNOCKBACK_SPEED_PER_METRE;

        // -------------------------------------------------------------------------------------------------------
        // Single hits
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Damages one enemy (damage already scaled by Might), then applies the statuses of <paramref name="effect"/>. A hit
        /// without damage only applies the statuses and counts as Hurt.
        /// </summary>
        public DamageResult Hit(int row, float damage, Vector3 push, int source, in HitEffect effect = default)
        {
            lastWasCritical = false;
            if (!enemies.IsTargetable(row))
            {
                return DamageResult.Missed;
            }
            if (damage <= 0.0f)
            {
                ApplyStatus(row, source, effect);
                return DamageResult.Hurt;
            }
            var options = new HitOptions
            {
                canCrit = true,
                forceCrit = effect.forceCrit,
                critChanceBonus = effect.critChanceBonus,
                critDamageBonus = effect.critDamageBonus,
                numberKind = effect.numberKind,
                hideNumber = effect.hideNumber,
            };
            DamageResult result = Combat.DamageEnemy(row, damage, push, source, in options, out bool isCritical);
            lastWasCritical = isCritical && result != DamageResult.Missed;
            if (result == DamageResult.Hurt)
            {
                ApplyStatus(row, source, effect);
            }
            return result;
        }

        /// <summary>Applies statuses without damage (slows, stuns, freezes, marks, burns).</summary>
        public void ApplyStatus(int row, int source, in HitEffect effect)
        {
            if (effect.stunSeconds > 0.0f)
            {
                enemies.Stun(row, effect.stunSeconds);
            }
            if (effect.slowFactor > 0.0f && effect.slowSeconds > 0.0f)
            {
                enemies.Slow(row, effect.slowFactor, effect.slowSeconds);
            }
            if (effect.burnPerSecond > 0.0f && effect.burnSeconds > 0.0f)
            {
                enemies.Burn(row, effect.burnPerSecond, effect.burnSeconds, source);
            }
            if (effect.freezeChance > 0.0f && Random.Float() < effect.freezeChance)
            {
                Freeze(row, effect.freezeSeconds, effect.freezesElitesFully);
            }
            if (effect.markBonus > 0.0f && effect.markSeconds > 0.0f)
            {
                enemies.Mark(row, effect.markBonus, effect.markSeconds);
            }
        }

        /// <summary>Freezes an enemy; bosses are slowed instead.</summary>
        public void Freeze(int row, float seconds, bool freezesElitesFully)
        {
            if (!enemies.IsTargetable(row))
            {
                return;
            }
            if (IsBoss(row))
            {
                enemies.Slow(row, BOSS_SLOW_FACTOR, BOSS_SLOW_SECONDS);
                return;
            }
            enemies.Freeze(row, enemies.IsElite(row) && freezesElitesFully ? seconds * FULL_ELITE_FREEZE_FACTOR : seconds);
        }

        // -------------------------------------------------------------------------------------------------------
        // Area hits
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Damages every enemy touching a ground circle; returns how many were hit.</summary>
        /// <param name="knockback">Push speed (m/s) before the Knockback stat (Combat applies it).</param>
        /// <param name="memory">Enemies already hit by this cast are skipped; the ones hit now are added.</param>
        public int Circle(Vector3 center, float radius, float damage, float knockback, int source, in HitEffect effect = default,
                          PushMode mode = PushMode.Outward, HashSet<Enemy> memory = null)
        {
            int found = enemies.QueryCircle(center, radius, rows);
            int hits = Apply(found, center, center, damage, knockback, source, effect, mode, Vector3.zero, memory);
            if (hits > 0 && damage > 0.0f)
            {
                areaHit?.Invoke(center, radius, damage);
            }
            return hits;
        }

        /// <summary>Damages every enemy in a ground cone (pushed outward from the origin); returns how many were hit.</summary>
        public int Cone(Vector3 origin, Vector3 forward, float range, float halfAngle, float damage, float knockback, int source,
                        in HitEffect effect = default, HashSet<Enemy> memory = null)
        {
            int found = enemies.QueryCone(origin, forward, range, halfAngle, rows);
            return Apply(found, origin, origin, damage, knockback, source, effect, PushMode.Outward, Vector3.zero, memory);
        }

        /// <summary>Damages every enemy touching a ground segment swept by a radius; returns how many were hit.</summary>
        /// <param name="along">The push direction for <see cref="PushMode.Along"/>.</param>
        public int Capsule(Vector3 a, Vector3 b, float radius, float damage, float knockback, int source, in HitEffect effect = default,
                           PushMode mode = PushMode.Sideways, Vector3 along = default, HashSet<Enemy> memory = null)
        {
            int found = enemies.QueryCapsule(a, b, radius, rows);
            return Apply(found, a, b, damage, knockback, source, effect, mode, along, memory);
        }

        // -------------------------------------------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------------------------------------------

        /// <summary>The <paramref name="count"/> targetable enemies nearest to a point, nearest first.</summary>
        public int FindNearest(Vector3 center, float radius, Span<int> output, int count)
        {
            int found = enemies.QueryCircle(center, radius, rows);
            int wanted = Math.Min(Math.Min(count, output.Length), found);
            for (int k = 0; k < wanted; k++)
            {
                int best = k;
                float bestDistance = Ground.DistanceSquared(enemies.GetPosition(rows[k]), center);
                for (int i = k + 1; i < found; i++)
                {
                    float distance = Ground.DistanceSquared(enemies.GetPosition(rows[i]), center);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }
                (rows[k], rows[best]) = (rows[best], rows[k]);
                output[k] = rows[k];
            }
            return wanted;
        }

        /// <summary>The nearest targetable enemy within a radius that is not in <paramref name="exclude"/>.</summary>
        public bool FindNearestExcept(Vector3 center, float radius, ReadOnlySpan<Enemy> exclude, out int nearestRow)
        {
            int found = enemies.QueryCircle(center, radius, rows);
            nearestRow = -1;
            float best = float.MaxValue;
            for (int i = 0; i < found; i++)
            {
                int row = rows[i];
                if (Contains(exclude, enemies.GetEnemy(row)))
                {
                    continue;
                }
                float distance = Ground.DistanceSquared(enemies.GetPosition(row), center);
                if (distance < best)
                {
                    best = distance;
                    nearestRow = row;
                }
            }
            return nearestRow >= 0;
        }

        /// <summary>
        /// The strongest enemies within range, strongest first: bosses, then elites, then the most health left.
        /// Enemies in <paramref name="exclude"/> are skipped.
        /// </summary>
        public int FindStrongest(Vector3 center, float radius, ReadOnlySpan<Enemy> exclude, Span<int> output, int count)
        {
            int found = enemies.QueryCircle(center, radius, rows);
            int written = 0;
            int wanted = Math.Min(count, output.Length);
            while (written < wanted)
            {
                int best = -1;
                float bestScore = float.MinValue;
                for (int i = 0; i < found; i++)
                {
                    int row = rows[i];
                    if (row < 0 || Contains(exclude, enemies.GetEnemy(row)))
                    {
                        continue;
                    }
                    float score = GetStrength(row);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                output[written++] = rows[best];
                rows[best] = -1;
            }
            return written;
        }

        // -------------------------------------------------------------------------------------------------------
        // Internals
        // -------------------------------------------------------------------------------------------------------

        private int Apply(int found, Vector3 a, Vector3 b, float damage, float knockback, int source, in HitEffect effect, PushMode mode,
                          Vector3 along, HashSet<Enemy> memory)
        {
            int hits = 0;
            for (int i = 0; i < found; i++)
            {
                int row = rows[i];
                if (memory != null && !memory.Add(enemies.GetEnemy(row)))
                {
                    continue;
                }
                Vector3 push = GetPush(enemies.GetPosition(row), a, b, knockback, mode, along);
                DamageResult result = Hit(row, damage, push, source, effect);
                if (result == DamageResult.Missed)
                {
                    continue;
                }
                hitRows[hits++] = row;
            }
            lastHitCount = hits;
            return hits;
        }

        private static Vector3 GetPush(Vector3 position, Vector3 a, Vector3 b, float speed, PushMode mode, Vector3 along)
        {
            if (speed <= 0.0f)
            {
                return Vector3.zero;
            }
            switch (mode)
            {
                case PushMode.Outward:
                    return Combat.GetPushFrom(Ground.ClosestOnSegment(position, a, b), position, speed);
                case PushMode.Inward:
                    return Combat.GetPushFrom(position, a, speed);
                case PushMode.Swirl:
                {
                    Vector3 outward = Ground.Direction(a, position, Vector3.forward);
                    Vector3 swirl = Ground.Perpendicular(outward) * SWIRL_TANGENT + outward * SWIRL_OUTWARD;
                    return Ground.Normalize(swirl, outward) * speed;
                }
                case PushMode.Along:
                    return Ground.Normalize(along, Vector3.zero) * speed;
                case PushMode.Sideways:
                {
                    Vector3 axis = Ground.Direction(a, b, Vector3.forward);
                    Vector3 side = Ground.Perpendicular(axis);
                    float sign = Vector3.Dot(position - a, side) >= 0.0f ? 1.0f : -1.0f;
                    return side * (sign * speed);
                }
                default:
                    return Vector3.zero;
            }
        }

        /// <summary>How strong an enemy is for <see cref="FindStrongest"/>: bosses first, then elites, then health left.</summary>
        private float GetStrength(int row)
        {
            float score = enemies.GetHealth(row);
            if (IsBoss(row))
            {
                return score + BOSS_SCORE;
            }
            return enemies.IsElite(row) ? score + ELITE_SCORE : score;
        }

        private static bool Contains(ReadOnlySpan<Enemy> list, Enemy enemy)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(enemy))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

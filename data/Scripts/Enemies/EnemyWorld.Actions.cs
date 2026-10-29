using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>What the game does to enemies from the main thread: damage, kills, pushes, marches and placement.</summary>
    public sealed partial class EnemyWorld
    {
        private const float HIT_HOLD_SECONDS = 0.05f;
        private const float HIT_SQUASH_SECONDS = 0.1f;
        /// <summary>Knockback is divided by the type's weight, never by less than this.</summary>
        private const float MIN_PUSH_WEIGHT = 0.1f;
        /// <summary>Elites are this much heavier to knock back.</summary>
        private const float ELITE_PUSH_WEIGHT = 2.0f;
        /// <summary>A pull is this share of its speed at the centre, all of it at the rim.</summary>
        private const float PULL_CENTRE_SHARE = 0.3f;
        private const float MIN_PULL_RADIUS = 1e-3f;
        /// <summary>A bubble takes this much more from frozen enemies and critical hits.</summary>
        private const float BARRIER_BREAK_RATE = 2.0f;

        /// <summary>Damages an enemy.</summary>
        /// <param name="row">The enemy's row.</param>
        /// <param name="amount">Health it loses (a Warded elite's bubble takes it first).</param>
        /// <param name="push">Knockback velocity (m/s), divided by the type's weight.</param>
        /// <param name="source">Weapon slot for the death report, -1 for none.</param>
        /// <param name="isHeavy">Staggers an enemy that has a hit clip.</param>
        /// <param name="isCritical">A critical hit: breaks a bubble twice as fast.</param>
        public DamageResult Damage(int row, float amount, Vector3 push = default, int source = -1, bool isHeavy = false, bool isCritical = false)
        {
            if (!IsTargetable(row))
            {
                return DamageResult.Missed;
            }
            EnemyType kind = types[typeOf[row]];
            LoseHealth(row, amount, isCritical);
            StartFlash(row);
            statuses[row].holdTime = HIT_HOLD_SECONDS;
            statuses[row].squashTime = HIT_SQUASH_SECONDS;
            lastSource[row] = source;
            push.y = 0.0f;
            knockbacks[row] += push / MathF.Max(kind.weight * (elite[row] ? ELITE_PUSH_WEIGHT : 1.0f), MIN_PUSH_WEIGHT);
            if (health[row] <= 0.0f)
            {
                BeginDeath(row);
                return DamageResult.Killed;
            }
            if (isHeavy && states[row] == EnemyState.Chase && kind.HasClip(EnemyClip.Hit))
            {
                states[row] = EnemyState.Stagger;
                stateTime[row] = 0.0f;
                clips[row] = kind.GetClip(EnemyClip.Hit);
                clipTimes[row] = 0.0f;
            }
            return DamageResult.Hurt;
        }

        /// <summary>Kills an enemy with a death report.</summary>
        public bool Kill(Enemy enemy, int source = -1)
        {
            int row = GetRow(enemy);
            if (row < 0)
            {
                return false;
            }
            lastSource[row] = source;
            BeginDeath(row);
            return true;
        }

        /// <summary>
        /// Kills every regular enemy (proxies are spared) touching a circle, with death reports (shockwaves, the King's
        /// death wave, the clearing for his rise).
        /// </summary>
        /// <param name="killed">Scratch for the query; on return it holds the rows killed, whose positions stay readable
        /// while they die, so the caller can show the pops.</param>
        /// <returns>How many died.</returns>
        public int KillRegularIn(Vector3 center, float radius, int source, Span<int> killed)
        {
            int found = QueryCircle(center, radius, killed);
            int written = 0;
            for (int i = 0; i < found; i++)
            {
                int row = killed[i];
                // A death report can kill others (on-kill weapons): skip rows that died meanwhile.
                if (!IsTargetable(row) || IsProxy(row))
                {
                    continue;
                }
                lastSource[row] = source;
                BeginDeath(row);
                killed[written++] = row;
            }
            return written;
        }

        /// <summary>Removes an enemy without a death (a swarm that ran off, recycling).</summary>
        public bool Despawn(Enemy enemy)
        {
            int row = GetRow(enemy);
            if (row < 0)
            {
                return false;
            }
            aliveCount--;
            typeCounts[typeOf[row]]--;
            Release(row);
            return true;
        }

        /// <summary>Flashes an enemy white for a moment without damage (fuses, telegraphs).</summary>
        public void Flash(int row)
        {
            if (row >= 0 && row < rowCount && alive[row])
            {
                StartFlash(row);
            }
        }

        /// <summary>
        /// Pulls every enemy within a radius toward a point: knockback toward the centre at <paramref name="speed"/>,
        /// strongest at the rim and 30% of it at the centre (vortices); returns how many were pulled.
        /// </summary>
        public int Pull(Vector3 center, float radius, float speed, Span<int> scratch)
        {
            int found = QueryCircle(center, radius, scratch);
            for (int i = 0; i < found; i++)
            {
                int row = scratch[i];
                Vector3 toward = center - currentPositions[row];
                toward.y = 0.0f;
                float distance = toward.magnitude;
                if (distance < MIN_SEPARATION_DISTANCE)
                {
                    continue;
                }
                float falloff = Mathf.Lerp(PULL_CENTRE_SHARE, 1.0f, MathF.Min(distance / MathF.Max(radius, MIN_PULL_RADIUS), 1.0f));
                knockbacks[row] += toward / distance * (speed * falloff / MathF.Max(types[typeOf[row]].weight, MIN_PUSH_WEIGHT));
            }
            return found;
        }

        /// <summary>Pushes every enemy within a radius away from a point (shockwaves, the hero's dash).</summary>
        public int PushAway(Vector3 center, float radius, float speed, Span<int> scratch)
        {
            int found = QueryCircle(center, radius, scratch);
            for (int i = 0; i < found; i++)
            {
                int row = scratch[i];
                Vector3 away = currentPositions[row] - center;
                away.y = 0.0f;
                float distance = away.magnitude;
                away = distance > MIN_SEPARATION_DISTANCE ? away / distance : new Vector3(1.0f, 0.0f, 0.0f);
                knockbacks[row] += away * (speed / MathF.Max(types[typeOf[row]].weight, MIN_PUSH_WEIGHT));
            }
            return found;
        }

        /// <summary>
        /// Overrides an enemy's chase for some seconds: it walks to <paramref name="goal"/> (a point) or along it (a
        /// direction), ignoring the hero (encircling rings, swarm rushes). It still attacks when its abilities fire,
        /// and the march clock runs only while it walks.
        /// </summary>
        public void March(int row, Vector3 goal, bool toPoint, float speed, float seconds)
        {
            if (row < 0 || row >= rowCount || !alive[row])
            {
                return;
            }
            marchGoal[row] = toPoint ? goal : GetFlatDirection(goal, yaws[row]);
            marchToPoint[row] = toPoint;
            marchSpeed[row] = speed;
            marchTime[row] = seconds;
        }

        /// <summary>Puts a row somewhere at once (recycling far enemies, bosses driving their proxy row).</summary>
        public void SetPosition(int row, Vector3 position)
        {
            if (row < 0 || row >= rowCount || !alive[row])
            {
                return;
            }
            currentPositions[row] = position;
            nextPositions[row] = position;
            jumped[row] = true;
            isGridDirty = true;
        }

        /// <summary>Sets a row's heading (radians about up, 0 = +Z).</summary>
        public void SetYaw(int row, float yaw) => yaws[row] = yaw;

        /// <summary>Scales the damage a row takes (bosses guarding, channeling, dazed); 1 = normal.</summary>
        public void SetDamageTakenScale(int row, float scale)
        {
            if (row >= 0 && row < rowCount && alive[row])
            {
                damageTakenScale[row] = scale;
            }
        }

        /// <summary>
        /// Takes health, through the row's bubble first: frozen rows and critical hits break it twice as fast. Called
        /// from the step's workers for burn and venom too (own row only).
        /// </summary>
        private void LoseHealth(int row, float amount, bool isCritical = false)
        {
            ref RowStatus status = ref statuses[row];
            if (status.barrier > 0.0f)
            {
                float breakRate = isCritical || status.freezeTime > 0.0f ? BARRIER_BREAK_RATE : 1.0f;
                float absorbed = MathF.Min(status.barrier, amount * breakRate);
                status.barrier -= absorbed;
                amount -= absorbed / breakRate;
            }
            health[row] -= amount;
        }
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Read-only lookups and the spatial queries weapons and the game use. Main thread.</summary>
    public sealed partial class EnemyWorld
    {
        /// <summary>A cone whose forward is shorter than this has no direction and finds nothing.</summary>
        private const float MIN_CONE_FORWARD = 1e-5f;
        /// <summary>A segment shorter than this (squared, m^2) is treated as its first point.</summary>
        private const float MIN_SEGMENT_LENGTH_SQUARED = 1e-8f;

        private readonly Dictionary<long, int> clusterCells = new Dictionary<long, int>();
        /// <summary>Where <see cref="CollectBurning"/> stopped last time.</summary>
        private int burnCursor;

        /// <summary>The row of a live enemy, or -1.</summary>
        public int GetRow(Enemy enemy)
        {
            int row = enemy.row;
            return row >= 0 && row < rowCount && alive[row] && generations[row] == enemy.generation && states[row] != EnemyState.Dying ? row : -1;
        }

        public Enemy GetEnemy(int row) => MakeId(row);

        /// <summary>Whether weapons can hit the row now.</summary>
        public bool IsTargetable(int row) => row >= 0 && row < rowCount && alive[row] && states[row] != EnemyState.Dying && states[row] != EnemyState.Rising;

        public Vector3 GetPosition(int row) => currentPositions[row];

        public float GetRadius(int row) => radiusOf[row];

        public int GetType(int row) => typeOf[row];

        public bool IsElite(int row) => elite[row];

        /// <summary>Whether the row is a proxy: a boss, a lantern or a pyre whose owner drives it.</summary>
        public bool IsProxy(int row) => types[typeOf[row]].isProxy;

        /// <summary>Health left (not the fraction).</summary>
        public float GetHealth(int row) => health[row];

        public float GetMaxHealth(int row) => maxHealth[row];

        public float GetHealthFraction(int row) => maxHealth[row] > 0.0f ? health[row] / maxHealth[row] : 0.0f;

        /// <summary>The height of the row's chest, for hit effects and damage numbers.</summary>
        public float GetHeight(int row) => Tuning.ENEMY_BODY_HEIGHT * scaleOf[row];

        /// <summary>Live enemies of a type (archetype caps).</summary>
        public int CountOfType(int type)
        {
            return type >= 0 && type < typeCounts.Length ? typeCounts[type] : 0;
        }

        /// <summary>Targetable enemies whose ground circle touches a circle, in no particular order.</summary>
        /// <returns>How many were written, at most the buffer's length.</returns>
        public int QueryCircle(Vector3 center, float radius, Span<int> rows)
        {
            EnsureGrid();
            int found = grid.Query(center, radius + maxRadius, queryRows);
            int written = 0;
            for (int i = 0; i < found && written < rows.Length; i++)
            {
                int row = queryRows[i];
                if (!IsTargetable(row))
                {
                    continue;
                }
                float reach = radius + radiusOf[row];
                if (Ground.DistanceSquared(currentPositions[row], center) <= reach * reach)
                {
                    rows[written++] = row;
                }
            }
            return written;
        }

        /// <summary>Targetable enemies touching the ground segment a-b swept by a radius (beams, slashes, charges).</summary>
        public int QueryCapsule(Vector3 a, Vector3 b, float radius, Span<int> rows)
        {
            EnsureGrid();
            Vector3 middle = (a + b) * 0.5f;
            float halfLength = Ground.Distance(a, b) * 0.5f;
            int found = grid.Query(middle, halfLength + radius + maxRadius, queryRows);
            int written = 0;
            for (int i = 0; i < found && written < rows.Length; i++)
            {
                int row = queryRows[i];
                if (!IsTargetable(row))
                {
                    continue;
                }
                float reach = radius + radiusOf[row];
                if (GetSegmentDistanceSquared(currentPositions[row], a, b) <= reach * reach)
                {
                    rows[written++] = row;
                }
            }
            return written;
        }

        /// <summary>Targetable enemies in a ground cone (sweeps, breath).</summary>
        /// <param name="halfAngle">Half the cone's opening, degrees.</param>
        public int QueryCone(Vector3 origin, Vector3 forward, float range, float halfAngle, Span<int> rows)
        {
            EnsureGrid();
            var flat = new Vector3(forward.x, 0.0f, forward.z);
            float length = flat.magnitude;
            if (length < MIN_CONE_FORWARD)
            {
                return 0;
            }
            flat /= length;
            float cosHalf = MathF.Cos(halfAngle * DEGREES_TO_RADIANS);
            int found = grid.Query(origin, range + maxRadius, queryRows);
            int written = 0;
            for (int i = 0; i < found && written < rows.Length; i++)
            {
                int row = queryRows[i];
                if (!IsTargetable(row))
                {
                    continue;
                }
                Vector3 to = currentPositions[row] - origin;
                to.y = 0.0f;
                float distance = to.magnitude;
                if (distance > range + radiusOf[row])
                {
                    continue;
                }
                // Enemies overlapping the origin count, whatever their direction.
                if (distance <= radiusOf[row] || Vector3.Dot(to / distance, flat) >= cosHalf)
                {
                    rows[written++] = row;
                }
            }
            return written;
        }

        /// <summary>The targetable enemy nearest to a point.</summary>
        /// <returns>False when none is within <paramref name="maxDistance"/>.</returns>
        public bool QueryNearest(Vector3 center, float maxDistance, out int nearestRow)
        {
            EnsureGrid();
            int found = grid.Query(center, maxDistance, queryRows);
            nearestRow = -1;
            float best = float.MaxValue;
            for (int i = 0; i < found; i++)
            {
                int row = queryRows[i];
                if (!IsTargetable(row))
                {
                    continue;
                }
                float distance = Ground.DistanceSquared(currentPositions[row], center);
                if (distance < best)
                {
                    best = distance;
                    nearestRow = row;
                }
            }
            return nearestRow >= 0;
        }

        /// <summary>A random targetable enemy within a radius; the strongest when <paramref name="preferElite"/> finds one.</summary>
        public bool QueryRandom(Vector3 center, float radius, out int pickedRow, bool preferElite = false)
        {
            EnsureGrid();
            int found = grid.Query(center, radius, queryRows);
            pickedRow = -1;
            int seen = 0;
            for (int i = 0; i < found; i++)
            {
                int row = queryRows[i];
                if (!IsTargetable(row))
                {
                    continue;
                }
                if (preferElite && elite[row])
                {
                    pickedRow = row;
                    return true;
                }
                seen++;
                if (Random.Range(0, seen) == 0)
                {
                    pickedRow = row;
                }
            }
            return pickedRow >= 0;
        }

        /// <summary>
        /// Regular enemies (not elite, not proxies, not marching) farther than <paramref name="distance"/> from a
        /// point: the director moves them back into the fight.
        /// </summary>
        public int CollectFar(Vector3 center, float distance, Span<int> rows)
        {
            float limit = distance * distance;
            int written = 0;
            for (int row = 0; row < rowCount && written < rows.Length; row++)
            {
                if (!IsTargetable(row) || elite[row] || IsProxy(row) || marchTime[row] > 0.0f)
                {
                    continue;
                }
                if (Ground.DistanceSquared(currentPositions[row], center) > limit)
                {
                    rows[written++] = row;
                }
            }
            return written;
        }

        /// <summary>Rows of live elites (off-screen markers); returns how many were written.</summary>
        public int CollectElites(Span<int> rows)
        {
            int written = 0;
            for (int row = 0; row < rowCount && written < rows.Length; row++)
            {
                if (elite[row] && IsTargetable(row))
                {
                    rows[written++] = row;
                }
            }
            return written;
        }

        /// <summary>
        /// Up to rows.Length burning enemies, starting where the last call stopped so a big fire shows on everyone over
        /// a few calls (status effects drawn on a budget).
        /// </summary>
        public int CollectBurning(Span<int> rows)
        {
            int written = 0;
            for (int scanned = 0; scanned < rowCount && written < rows.Length; scanned++)
            {
                burnCursor = burnCursor + 1 < rowCount ? burnCursor + 1 : 0;
                if (statuses[burnCursor].burnTime > 0.0f && IsTargetable(burnCursor) && !IsProxy(burnCursor))
                {
                    rows[written++] = burnCursor;
                }
            }
            return written;
        }

        /// <summary>
        /// The center of the densest cell of <paramref name="cellSize"/> m among targetable enemies within
        /// <paramref name="range"/> of a point (weapons that aim at crowds).
        /// </summary>
        /// <returns>False when no enemy is in range.</returns>
        public bool FindCluster(Vector3 center, float range, float cellSize, out Vector3 point)
        {
            EnsureGrid();
            int found = grid.Query(center, range, queryRows);
            clusterCells.Clear();
            long best = 0;
            int bestCount = 0;
            for (int i = 0; i < found; i++)
            {
                int row = queryRows[i];
                if (!IsTargetable(row))
                {
                    continue;
                }
                long cell = GroundCell.GetKey(currentPositions[row], cellSize);
                clusterCells.TryGetValue(cell, out int count);
                clusterCells[cell] = ++count;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = cell;
                }
            }
            if (bestCount == 0)
            {
                point = center;
                return false;
            }
            point = GroundCell.GetCenter(best, cellSize);
            return true;
        }

        /// <summary>Squared ground distance from a point to the segment a-b.</summary>
        private static float GetSegmentDistanceSquared(Vector3 point, Vector3 a, Vector3 b)
        {
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float lengthSquared = abx * abx + abz * abz;
            float t = lengthSquared > MIN_SEGMENT_LENGTH_SQUARED ? Math.Clamp(((point.x - a.x) * abx + (point.z - a.z) * abz) / lengthSquared, 0.0f, 1.0f) : 0.0f;
            float dx = point.x - (a.x + abx * t);
            float dz = point.z - (a.z + abz * t);
            return dx * dx + dz * dz;
        }
    }
}

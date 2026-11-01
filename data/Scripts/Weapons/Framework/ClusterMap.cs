using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Cluster targeting: counts targetable enemies per 2 m ground cell in a window around the hero and
    /// keeps the 8 densest cells, each aimed at the average position of its enemies. Computed at most once per frame
    /// per range, only when a weapon asks. Main thread.
    /// </summary>
    public sealed class ClusterMap
    {
        public const int MAX_CELLS = 8;
        public const float CELL_SIZE = 2.0f;
        /// <summary>A boss counts as this many enemies, so spells aimed at crowds do not spend themselves on its stragglers.</summary>
        private const int BOSS_WEIGHT = 30;
        private const float MAX_RANGE = 20.0f;
        private const int GRID = (int)(MAX_RANGE * 2.0f / CELL_SIZE);
        private const int QUERY_CAPACITY = 2048;
        /// <summary>Ranges closer than this share one computation per frame.</summary>
        private const float RANGE_TOLERANCE = 0.01f;

        private readonly EnemyWorld enemies;
        private readonly int[] rows = new int[QUERY_CAPACITY];
        private readonly int[] counts = new int[GRID * GRID];
        private readonly float[] sumX = new float[GRID * GRID];
        private readonly float[] sumZ = new float[GRID * GRID];
        private readonly Vector3[] points = new Vector3[MAX_CELLS];
        private readonly int[] weights = new int[MAX_CELLS];
        private int cellCount;
        private long cachedFrame = -1;
        private float cachedRange;

        public ClusterMap(EnemyWorld enemies)
        {
            this.enemies = enemies;
        }

        /// <summary>Finds the densest cells within <paramref name="range"/> (at most 20 m); returns how many there are.</summary>
        public int Find(Vector3 center, float range)
        {
            range = MathF.Min(range, MAX_RANGE);
            if (cachedFrame == Time.frameCount && MathF.Abs(cachedRange - range) < RANGE_TOLERANCE)
            {
                return cellCount;
            }
            cachedFrame = Time.frameCount;
            cachedRange = range;
            Array.Clear(counts, 0, counts.Length);
            Array.Clear(sumX, 0, sumX.Length);
            Array.Clear(sumZ, 0, sumZ.Length);
            float originX = center.x - MAX_RANGE;
            float originZ = center.z - MAX_RANGE;
            int found = enemies.QueryCircle(center, range, rows);
            for (int i = 0; i < found; i++)
            {
                Vector3 position = enemies.GetPosition(rows[i]);
                int x = (int)((position.x - originX) / CELL_SIZE);
                int z = (int)((position.z - originZ) / CELL_SIZE);
                if (x < 0 || z < 0 || x >= GRID || z >= GRID)
                {
                    continue;
                }
                int cell = z * GRID + x;
                int weight = enemies.IsBoss(rows[i]) ? BOSS_WEIGHT : 1;
                counts[cell] += weight;
                sumX[cell] += position.x * weight;
                sumZ[cell] += position.z * weight;
            }
            SelectTop();
            return cellCount;
        }

        /// <summary>The aim point of the i-th densest cell (ground height).</summary>
        public Vector3 GetPoint(int index) => points[index];

        /// <summary>How many enemies the i-th densest cell holds.</summary>
        public int GetWeight(int index) => weights[index];

        /// <summary>A cell picked at random, weighted by how many enemies it holds.</summary>
        public int PickWeighted()
        {
            int total = 0;
            for (int i = 0; i < cellCount; i++)
            {
                total += weights[i];
            }
            if (total <= 0)
            {
                return 0;
            }
            int roll = Random.Range(0, total);
            for (int i = 0; i < cellCount; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }
            return cellCount - 1;
        }

        private void SelectTop()
        {
            cellCount = 0;
            for (int cell = 0; cell < counts.Length; cell++)
            {
                int count = counts[cell];
                if (count == 0 || cellCount == MAX_CELLS && count <= weights[MAX_CELLS - 1])
                {
                    continue;
                }
                // Insertion into the sorted top list (densest first); a full list drops its last entry.
                int slot = cellCount < MAX_CELLS ? cellCount++ : MAX_CELLS - 1;
                while (slot > 0 && weights[slot - 1] < count)
                {
                    weights[slot] = weights[slot - 1];
                    points[slot] = points[slot - 1];
                    slot--;
                }
                weights[slot] = count;
                points[slot] = new Vector3(sumX[cell] / count, 0.0f, sumZ[cell] / count);
            }
        }
    }
}

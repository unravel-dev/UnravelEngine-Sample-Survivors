import sys
p = sys.argv[1]
s = open(p, encoding='ascii').read()
def rep(a, b, n=1):
    global s
    assert s.count(a) == n, (a, s.count(a))
    s = s.replace(a, b)
rep("""        private readonly float[] burnDps;
""", """        private readonly float[] burnDps;
        private readonly float[] stunTime;
        private readonly float[] markTime;
        private readonly float[] markBonus;
        private readonly Dictionary<long, int> clusterCells = new Dictionary<long, int>();
""")
rep("""            burnDps = new float[size];
""", """            burnDps = new float[size];
            stunTime = new float[size];
            markTime = new float[size];
            markBonus = new float[size];
""")
rep("""            burnDps[row] = 0.0f;
            alive[row] = true;""", """            burnDps[row] = 0.0f;
            stunTime[row] = 0.0f;
            markTime[row] = 0.0f;
            markBonus[row] = 0.0f;
            alive[row] = true;""")
# step: stun behaves like freeze without the look
rep("""                if (freezeTime[row] > 0.0f)
                {
                    freezeTime[row] -= dt;""", """                if (freezeTime[row] > 0.0f || stunTime[row] > 0.0f)
                {
                    freezeTime[row] -= dt;
                    stunTime[row] -= dt;""")
rep("""            if (slowTime[row] > 0.0f)
            {
                slowTime[row] -= dt;
            }""", """            if (slowTime[row] > 0.0f)
            {
                slowTime[row] -= dt;
            }
            if (markTime[row] > 0.0f)
            {
                markTime[row] -= dt;
            }""")
rep("""        /// <summary>Whether the enemy burns now.</summary>""", """        /// <summary>Stops an enemy's movement and casting for some seconds without the frozen look.</summary>
        public void Stun(int row, float seconds)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            stunTime[row] = MathF.Max(stunTime[row], elite[row] ? seconds * 0.5f : seconds);
        }

        /// <summary>Marks an enemy: it takes <paramref name="bonus"/> more damage (0.5 = +50%) for some seconds.</summary>
        public void Mark(int row, float bonus, float seconds)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            markBonus[row] = MathF.Max(markTime[row] > 0.0f ? markBonus[row] : 0.0f, bonus);
            markTime[row] = MathF.Max(markTime[row], seconds);
        }

        public bool IsMarked(int row) => markTime[row] > 0.0f;

        /// <summary>The multiplier on damage the enemy takes now: frozen x1.25, marked x(1 + bonus).</summary>
        public float GetDamageTakenMultiplier(int row)
        {
            float multiplier = freezeTime[row] > 0.0f ? Tuning.FROZEN_DAMAGE_TAKEN : 1.0f;
            return markTime[row] > 0.0f ? multiplier * (1.0f + markBonus[row]) : multiplier;
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
                Vector3 position = currentPositions[row];
                long cell = ((long)MathF.Floor(position.x / cellSize) << 32) ^ (uint)(int)MathF.Floor(position.z / cellSize);
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
            float x = ((int)(best >> 32) + 0.5f) * cellSize;
            float z = ((int)(best & 0xffffffffL) + 0.5f) * cellSize;
            point = new Vector3(x, 0.0f, z);
            return true;
        }

        /// <summary>Whether the enemy burns now.</summary>""")
rep("""            burnTime[row] = 0.0f;
            freezeTime[row] = 0.0f;
            aliveCount--;""", """            burnTime[row] = 0.0f;
            freezeTime[row] = 0.0f;
            stunTime[row] = 0.0f;
            markTime[row] = 0.0f;
            aliveCount--;""")
open(p, 'w', encoding='ascii', newline='\n').write(s)
print('ok')

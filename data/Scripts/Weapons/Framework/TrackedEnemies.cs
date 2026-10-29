using System;

namespace Survivors
{
    /// <summary>
    /// A bounded list of the enemies a weapon shows a status on (poisoned, bloodied): each enemy once, a full list
    /// replacing its entries in turn. <see cref="Collect"/> hands out the rows of those still alive and still carrying
    /// the status, a budget at a time, continuing where the last call stopped; the others leave the list. Main thread;
    /// no allocations.
    /// </summary>
    public sealed class TrackedEnemies
    {
        private readonly EnemyWorld enemies;
        private readonly Func<int, bool> hasStatus;
        private readonly Enemy[] tracked;
        private int count;
        private int replaceCursor;
        private int visitCursor;

        /// <param name="hasStatus">Whether a live row still carries the status; build it once (it is kept).</param>
        public TrackedEnemies(EnemyWorld enemies, int capacity, Func<int, bool> hasStatus)
        {
            this.enemies = enemies;
            this.hasStatus = hasStatus;
            tracked = new Enemy[capacity];
        }

        /// <summary>Follows an enemy; one already followed stays as it is, and a full list replaces its entries in turn.</summary>
        public void Add(Enemy enemy)
        {
            for (int i = 0; i < count; i++)
            {
                if (tracked[i].Equals(enemy))
                {
                    return;
                }
            }
            if (count < tracked.Length)
            {
                tracked[count++] = enemy;
                return;
            }
            tracked[replaceCursor] = enemy;
            replaceCursor = (replaceCursor + 1) % tracked.Length;
        }

        /// <summary>
        /// Writes up to rows.Length rows of followed enemies that are alive and still carry the status, continuing where
        /// the last call stopped; the dead and the cured leave the list.
        /// </summary>
        public int Collect(Span<int> rows)
        {
            int written = 0;
            int visits = count;
            while (visits-- > 0 && written < rows.Length && count > 0)
            {
                if (visitCursor >= count)
                {
                    visitCursor = 0;
                }
                int row = enemies.GetRow(tracked[visitCursor]);
                if (row < 0 || !hasStatus(row))
                {
                    tracked[visitCursor] = tracked[--count];
                    continue;
                }
                rows[written++] = row;
                visitCursor++;
            }
            return written;
        }
    }
}

using System;

namespace Survivors
{
    /// <summary>
    /// Data a weapon keeps per enemy (a copy of its venom, when it last burst in thermal shock), stored by enemy row and
    /// checked by generation: a row that passes to a new enemy starts again from the fresh value, so nothing carries
    /// over from a dead enemy to the next one in its row. Grows with the enemy world, only when a row beyond it shows
    /// up. Main thread.
    /// </summary>
    public sealed class EnemyTable<T> where T : struct
    {
        private const int INITIAL_ROWS = 1024;
        /// <summary>The generation of a row no enemy has used yet.</summary>
        private const uint UNUSED = uint.MaxValue;

        private readonly T fresh;
        private uint[] generations = Array.Empty<uint>();
        private T[] values = Array.Empty<T>();

        /// <param name="fresh">What an enemy's entry holds before the weapon writes to it.</param>
        public EnemyTable(T fresh, int initialRows = INITIAL_ROWS)
        {
            this.fresh = fresh;
            Grow(initialRows);
        }

        /// <summary>The entry of an enemy; a fresh one when its row held another enemy until now.</summary>
        public ref T Get(Enemy enemy)
        {
            int row = enemy.row;
            if (row >= generations.Length)
            {
                Grow(Math.Max(row + 1, generations.Length * 2));
            }
            if (generations[row] != enemy.generation)
            {
                generations[row] = enemy.generation;
                values[row] = fresh;
            }
            return ref values[row];
        }

        /// <summary>Whether the table has an entry for this enemy (its row has not passed to another enemy since).</summary>
        public bool Holds(Enemy enemy)
        {
            int row = enemy.row;
            return row >= 0 && row < generations.Length && generations[row] == enemy.generation;
        }

        /// <summary>The entry of an enemy the table holds, else the fresh value; never adds one.</summary>
        public T Peek(Enemy enemy) => Holds(enemy) ? values[enemy.row] : fresh;

        private void Grow(int size)
        {
            int previous = generations.Length;
            Array.Resize(ref generations, size);
            Array.Resize(ref values, size);
            Array.Fill(generations, UNUSED, previous, size - previous);
        }
    }
}

using System;

namespace Survivors
{
    /// <summary>
    /// What Venom Shivs knows about the venom it applied: a copy of each enemy's stacks, per-stack damage and end time
    /// (the enemy world clears an enemy's venom the moment it dies, so a killing crit reads the copy to still rupture),
    /// and a bounded list of the enemies it poisoned for the status look. Times are the weapon's own clock. Main
    /// thread.
    /// </summary>
    internal sealed class VenomLedger
    {
        private const int TRACKED_CAPACITY = 128;

        private struct VenomCopy
        {
            public float stacks;
            public float damagePerStack;
            public float endTime;
        }

        private readonly EnemyWorld enemies;
        private readonly EnemyTable<VenomCopy> copies = new EnemyTable<VenomCopy>(default);
        private readonly TrackedEnemies poisoned;

        public VenomLedger(EnemyWorld enemies)
        {
            this.enemies = enemies;
            poisoned = new TrackedEnemies(enemies, TRACKED_CAPACITY, HasVenom);
        }

        /// <summary>Notes venom just applied to a live row: its stacks now, and an end no earlier than <paramref name="seconds"/> from now.</summary>
        public void Record(int row, float perStack, float seconds, float now)
        {
            Enemy enemy = enemies.GetEnemy(row);
            ref VenomCopy copy = ref copies.Get(enemy);
            bool isKnown = copy.endTime > now;
            copy.stacks = enemies.GetVenomStacks(row);
            copy.damagePerStack = isKnown ? MathF.Max(copy.damagePerStack, perStack) : perStack;
            copy.endTime = isKnown ? MathF.Max(copy.endTime, now + seconds) : now + seconds;
            poisoned.Add(enemy);
        }

        /// <summary>The stacks the copy holds for an enemy (0 when it is another enemy now or its venom ran out).</summary>
        public float GetStacks(Enemy enemy, float now)
        {
            VenomCopy copy = copies.Peek(enemy);
            return copy.endTime > now ? copy.stacks : 0.0f;
        }

        /// <summary>The venom damage the copy says an enemy had left to take.</summary>
        public float GetRemainingDamage(Enemy enemy, float now)
        {
            VenomCopy copy = copies.Peek(enemy);
            return copy.endTime > now ? copy.stacks * copy.damagePerStack * (copy.endTime - now) : 0.0f;
        }

        /// <summary>Forgets an enemy's venom (it was consumed by a rupture).</summary>
        public void Clear(Enemy enemy)
        {
            if (copies.Holds(enemy))
            {
                copies.Get(enemy).endTime = 0.0f;
            }
        }

        /// <summary>
        /// Writes up to rows.Length rows of poisoned enemies that still carry venom, continuing where the last call
        /// stopped (status looks on a budget); enemies whose venom ended or who died leave the list.
        /// </summary>
        public int CollectPoisoned(Span<int> rows) => poisoned.Collect(rows);

        private bool HasVenom(int row) => enemies.GetVenomStacks(row) > 0.0f;
    }
}

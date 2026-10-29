using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The numbers of Venom Shivs a rupture reads, from the weapon's level each time.</summary>
    internal struct VenomRules
    {
        /// <summary>Stacks an enemy needs for a Rogue crit to rupture it (Every).</summary>
        public float primedStacks;
        /// <summary>A rupture deals the venom damage left times this (Multiplier).</summary>
        public float multiplier;
        /// <summary>The most stacks one enemy carries (float.MaxValue: no cap).</summary>
        public float stackCap;
        /// <summary>The splash's reach (SubRadius after Area).</summary>
        public float splashRadius;
        /// <summary>Nightshade Plague: splashed primed neighbours rupture in turn.</summary>
        public bool canChain;
    }

    /// <summary>
    /// Venom Shivs' ruptures. A critical hit from a Rogue class weapon on a primed enemy queues one (its venom consumed
    /// at once, so a second crit in the same frame cannot rupture it twice); the due ones resolve on a budget: the
    /// burst on the enemy (the venom left times the multiplier), half its stacks (rounded up, counting at most
    /// <see cref="SPLASH_STACK_CAP"/>) splashed onto the <see cref="SPLASH_TARGETS"/> nearest enemies within the splash
    /// radius (never lifting one past that cap) and, for Nightshade Plague, the <see cref="CHAIN_BRANCH"/> most
    /// poisoned primed neighbours queued to rupture <see cref="CHAIN_DELAY"/> s later, for <see cref="CHAIN_DEPTH"/>
    /// generations. A killing crit still splashes: the stacks are read from the ledger once the enemy's venom is gone.
    /// The weapon applies the venom and shows what happened. Main thread; no allocations.
    /// </summary>
    internal sealed class VenomRuptures
    {
        private const int RUPTURE_CAPACITY = 48;
        private const int RUPTURES_PER_FRAME = 8;
        private const float SPLASH_SHARE = 0.5f;
        private const int SPLASH_CAPACITY = 24;
        private const int SPLASH_TARGETS = 8;
        private const float SPLASH_STACK_CAP = 10.0f;
        private const int CHAIN_DEPTH = 2;
        private const int CHAIN_BRANCH = 2;
        private const float CHAIN_DELAY = 0.12f;
        /// <summary>Venom globs arcing onto splashed neighbours: at most this many per rupture.</summary>
        private const int GLOB_MAX = 4;

        private struct Rupture
        {
            public Enemy target;
            /// <summary>Where it was queued (used once the enemy is gone).</summary>
            public Vector3 position;
            public float stacks;
            public float damage;
            public int depth;
            public float due;
            /// <summary>Trigger ruptures consume the venom when queued; chain ruptures when they resolve.</summary>
            public bool isConsumed;
        }

        private readonly VenomShivs shivs;
        private readonly EnemyWorld enemies;
        private readonly Strikes strikes;
        private readonly VenomLedger ledger;
        private readonly Rupture[] ruptures = new Rupture[RUPTURE_CAPACITY];
        private readonly int[] splashRows = new int[SPLASH_CAPACITY];
        private readonly int[] chainRows = new int[CHAIN_BRANCH];
        private readonly float[] chainStacks = new float[CHAIN_BRANCH];
        private int count;

        public VenomRuptures(VenomShivs shivs, WeaponContext context, VenomLedger ledger)
        {
            this.shivs = shivs;
            enemies = context.enemies;
            strikes = context.strikes;
            this.ledger = ledger;
        }

        /// <summary>A Rogue crit hit <paramref name="row"/>: a primed enemy's venom is consumed now and its rupture queued. Runs inside the damage call.</summary>
        public void QueueTrigger(int row, float now, in VenomRules rules)
        {
            if (count >= RUPTURE_CAPACITY)
            {
                return;
            }
            Enemy enemy = enemies.GetEnemy(row);
            bool isAlive = enemies.IsTargetable(row);
            float stacks = isAlive ? enemies.GetVenomStacks(row) : ledger.GetStacks(enemy, now);
            if (stacks < rules.primedStacks)
            {
                return;
            }
            float left = isAlive ? enemies.ConsumeVenom(row) : ledger.GetRemainingDamage(enemy, now);
            ledger.Clear(enemy);
            ruptures[count++] = new Rupture
            {
                target = enemy,
                position = enemies.GetPosition(row),
                stacks = stacks,
                damage = left * rules.multiplier,
                due = now,
                isConsumed = true,
            };
        }

        /// <summary>Resolves the ruptures that are due, at most <see cref="RUPTURES_PER_FRAME"/> per frame.</summary>
        public void Resolve(float now, in VenomRules rules)
        {
            int resolved = 0;
            int i = 0;
            while (i < count && resolved < RUPTURES_PER_FRAME)
            {
                if (ruptures[i].due > now)
                {
                    i++;
                    continue;
                }
                Rupture due = ruptures[i];
                ruptures[i] = ruptures[--count];
                Resolve(due, now, rules);
                resolved++;
            }
        }

        /// <summary>A due rupture: the burst on its enemy (if alive), the splash, then its look and Nightshade's cloud.</summary>
        private void Resolve(in Rupture rupture, float now, in VenomRules rules)
        {
            Enemy target = rupture.target;
            int row = enemies.GetRow(target);
            bool isSameEnemy = target.row >= 0 && enemies.GetEnemy(target.row).Equals(target);
            Vector3 position = isSameEnemy ? enemies.GetPosition(target.row) : rupture.position;
            float height = isSameEnemy ? enemies.GetHeight(target.row) : Tuning.ENEMY_BODY_HEIGHT;
            float stacks = rupture.stacks;
            float burst = rupture.damage;
            if (!rupture.isConsumed)
            {
                stacks = row >= 0 ? enemies.GetVenomStacks(row) : ledger.GetStacks(target, now);
                if (stacks < rules.primedStacks)
                {
                    return;
                }
                burst = (row >= 0 ? enemies.ConsumeVenom(row) : ledger.GetRemainingDamage(target, now)) * rules.multiplier;
                ledger.Clear(target);
            }
            if (row >= 0 && burst > 0.0f)
            {
                strikes.Hit(row, burst, Vector3.zero, shivs.slot);
            }
            Splash(target, position, stacks, rupture.depth, now, rules);
            shivs.ShowRupture(position, height, stacks, rupture.depth == 0);
            shivs.LeaveCloud(position);
        }

        /// <summary>Half the stacks onto the nearest enemies around; Nightshade queues the most poisoned of them to rupture next.</summary>
        private void Splash(Enemy source, Vector3 position, float stacks, int depth, float now, in VenomRules rules)
        {
            float cap = MathF.Min(rules.stackCap, SPLASH_STACK_CAP);
            float share = MathF.Ceiling(MathF.Min(stacks, SPLASH_STACK_CAP) * SPLASH_SHARE);
            int found = strikes.FindNearest(position, rules.splashRadius, splashRows, SPLASH_TARGETS + 1);
            bool canChain = rules.canChain && depth < CHAIN_DEPTH;
            int chained = 0;
            int globs = 0;
            for (int i = 0; i < found; i++)
            {
                int row = splashRows[i];
                if (enemies.GetEnemy(row).Equals(source))
                {
                    continue;
                }
                shivs.Poison(row, share, cap);
                if (globs < GLOB_MAX)
                {
                    shivs.ShowGlob(position, enemies.GetPosition(row));
                    globs++;
                }
                if (canChain)
                {
                    chained = KeepChainCandidate(row, chained, rules.primedStacks);
                }
            }
            for (int k = 0; k < chained && count < RUPTURE_CAPACITY; k++)
            {
                int row = chainRows[k];
                ruptures[count++] = new Rupture
                {
                    target = enemies.GetEnemy(row),
                    position = enemies.GetPosition(row),
                    depth = depth + 1,
                    due = now + CHAIN_DELAY,
                };
            }
        }

        /// <summary>Keeps the <see cref="CHAIN_BRANCH"/> most poisoned primed neighbours not queued already.</summary>
        private int KeepChainCandidate(int row, int chained, float primedStacks)
        {
            float stacks = enemies.GetVenomStacks(row);
            if (stacks < primedStacks || IsQueued(enemies.GetEnemy(row)))
            {
                return chained;
            }
            if (chained < CHAIN_BRANCH)
            {
                chainRows[chained] = row;
                chainStacks[chained] = stacks;
                return chained + 1;
            }
            int weakest = 0;
            for (int k = 1; k < CHAIN_BRANCH; k++)
            {
                if (chainStacks[k] < chainStacks[weakest])
                {
                    weakest = k;
                }
            }
            if (stacks > chainStacks[weakest])
            {
                chainRows[weakest] = row;
                chainStacks[weakest] = stacks;
            }
            return chained;
        }

        private bool IsQueued(Enemy enemy)
        {
            for (int i = 0; i < count; i++)
            {
                if (ruptures[i].target.Equals(enemy))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

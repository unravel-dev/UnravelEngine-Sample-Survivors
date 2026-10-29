using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Statuses: the timed effects weapons, casters and affixes put on a row, how they change its damage taken and
    /// speed, and how they count down. Apply methods run on the main thread and honour the type's
    /// <see cref="StatusImmunity"/>; the countdown and damage over time run in the step on worker threads, each worker
    /// writing only its own rows.
    /// </summary>
    public sealed partial class EnemyWorld
    {
        public const int STATUS_SOURCES = 16;
        /// <summary>Elites shake off freezes, stuns and confusion in this share of the time.</summary>
        private const float ELITE_STATUS_RESIST = 0.5f;
        /// <summary>A freeze on a type immune to it slows it to this share of its speed for the freeze's seconds.</summary>
        private const float FREEZE_IMMUNE_SLOW = 0.5f;
        private const float FLASH_SECONDS = 0.08f;
        /// <summary>Seconds after a flash before the next one: a body under constant damage blinks instead of staying white.</summary>
        private const float FLASH_GAP = 0.22f;
        private const float LAUNCH_SECONDS = 0.45f;
        /// <summary>Launch heights are divided by the type's weight, but never multiplied by a light one.</summary>
        private const float MIN_LAUNCH_WEIGHT = 1.0f;
        /// <summary>Damage multiplier on frozen enemies.</summary>
        private const float FROZEN_DAMAGE_TAKEN = 1.25f;
        private const float WARD_SPEED = 1.3f;
        private const float WARD_DAMAGE_TAKEN = 0.75f;

        private readonly RowStatus[] statuses;
        /// <summary>Burn and venom damage a row took in this step (summed by source in FinishStep, on the main thread).</summary>
        private readonly float[] statusDealt;

        /// <summary>Multiplies the seconds of burns, chills and freezes (Everburn Wick's Kindle).</summary>
        public float statusDurationScale = 1.0f;
        /// <summary>Multiplies burn damage (Everburn Wick's Kindle).</summary>
        public float burnDamageScale = 1.0f;
        /// <summary>Burn and venom damage dealt since the owner last read it, by the source that applied it (weapon slot,
        /// up to <see cref="STATUS_SOURCES"/>); the owner adds it to its records and clears it.</summary>
        public readonly float[] statusDamageBySource = new float[STATUS_SOURCES];

        /// <summary>
        /// Everything timed on one row. Each timer counts down in seconds and its effect holds while it is above zero;
        /// the haste and barrier of elite affixes last for the row's life.
        /// </summary>
        private struct RowStatus
        {
            public float slowTime;
            public float slowFactor;
            public float freezeTime;
            public float stunTime;
            public float burnTime;
            public float burnDps;
            public float venomTime;
            public float venomStacks;
            public float venomDps;
            public float markTime;
            public float markBonus;
            public float exposeTime;
            public float exposeBonus;
            public float bloodTime;
            public float wardTime;
            public float confuseTime;
            public float launchTime;
            public float launchHeight;
            /// <summary>Hit reaction: the clip holds still.</summary>
            public float holdTime;
            /// <summary>Hit reaction: the body squashes.</summary>
            public float squashTime;
            /// <summary>Hit reaction: the body draws white while above zero; below zero it counts the gap to the next flash.</summary>
            public float flashTime;
            /// <summary>Hasted elites: movement speed multiplier.</summary>
            public float hasteSpeed;
            /// <summary>Hasted elites: multiplier of every cast's wind-up.</summary>
            public float windupScale;
            /// <summary>Warded elites: damage the bubble absorbs before health.</summary>
            public float barrier;

            /// <summary>A fresh row: no status, ready to flash.</summary>
            public void Reset()
            {
                this = default;
                slowFactor = 1.0f;
                flashTime = -FLASH_GAP;
                hasteSpeed = 1.0f;
                windupScale = 1.0f;
            }

            /// <summary>Counts every timer down (damage over time is dealt by the world before this).</summary>
            public void Tick(float dt)
            {
                slowTime = CountDown(slowTime, dt);
                freezeTime = CountDown(freezeTime, dt);
                stunTime = CountDown(stunTime, dt);
                burnTime = CountDown(burnTime, dt);
                venomTime = CountDown(venomTime, dt);
                markTime = CountDown(markTime, dt);
                exposeTime = CountDown(exposeTime, dt);
                bloodTime = CountDown(bloodTime, dt);
                wardTime = CountDown(wardTime, dt);
                confuseTime = CountDown(confuseTime, dt);
                launchTime = CountDown(launchTime, dt);
                holdTime = CountDown(holdTime, dt);
                squashTime = CountDown(squashTime, dt);
                flashTime = MathF.Max(flashTime - dt, -FLASH_GAP);
            }

            /// <summary>A dying body sheds the statuses that change its look or its death report; its launch pose stays.</summary>
            public void ClearOnDeath()
            {
                burnTime = 0.0f;
                freezeTime = 0.0f;
                stunTime = 0.0f;
                markTime = 0.0f;
                exposeTime = 0.0f;
                venomTime = 0.0f;
                bloodTime = 0.0f;
            }

            private static float CountDown(float time, float dt) => time > 0.0f ? time - dt : time;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Applying statuses (main thread)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Slows an enemy to <paramref name="factor"/> of its speed for some seconds (the stronger slow wins).</summary>
        public void Slow(int row, float factor, float seconds)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            ref RowStatus status = ref statuses[row];
            if (status.slowTime <= 0.0f || factor < status.slowFactor)
            {
                status.slowFactor = factor;
            }
            status.slowTime = MathF.Max(status.slowTime, seconds * statusDurationScale);
        }

        /// <summary>Stops an enemy where it stands; elites thaw twice as fast, freeze-immune types are slowed instead.</summary>
        public void Freeze(int row, float seconds)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            if (IsImmune(row, StatusImmunity.Freeze))
            {
                Slow(row, FREEZE_IMMUNE_SLOW, seconds);
                return;
            }
            ref RowStatus status = ref statuses[row];
            status.freezeTime = MathF.Max(status.freezeTime, ResistIfElite(row, seconds * statusDurationScale));
        }

        /// <summary>Sets an enemy on fire: damage per second for some seconds (the stronger burn wins).</summary>
        public void Burn(int row, float damagePerSecond, float seconds, int source = -1)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            ref RowStatus status = ref statuses[row];
            status.burnDps = MathF.Max(status.burnDps, damagePerSecond * burnDamageScale);
            status.burnTime = MathF.Max(status.burnTime, seconds * statusDurationScale);
            lastSource[row] = source;
        }

        /// <summary>Stops an enemy's movement and casting for some seconds without the frozen look.</summary>
        public void Stun(int row, float seconds)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            ref RowStatus status = ref statuses[row];
            status.stunTime = MathF.Max(status.stunTime, ResistIfElite(row, seconds));
        }

        /// <summary>Marks an enemy: it takes <paramref name="bonus"/> more damage (0.5 = +50%) for some seconds.</summary>
        public void Mark(int row, float bonus, float seconds)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            ref RowStatus status = ref statuses[row];
            status.markBonus = MathF.Max(status.markTime > 0.0f ? status.markBonus : 0.0f, bonus);
            status.markTime = MathF.Max(status.markTime, seconds);
        }

        /// <summary>
        /// Exposes an enemy: it takes <paramref name="bonus"/> more damage from everything (0.2 = +20%) for some seconds;
        /// the strongest exposure wins. Separate from marks, so it shows no mark.
        /// </summary>
        public void Expose(int row, float bonus, float seconds)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            ref RowStatus status = ref statuses[row];
            status.exposeBonus = MathF.Max(status.exposeTime > 0.0f ? status.exposeBonus : 0.0f, bonus);
            status.exposeTime = MathF.Max(status.exposeTime, seconds);
        }

        /// <summary>
        /// Adds venom stacks: each stack deals <paramref name="damagePerStack"/> per second for <paramref name="seconds"/>
        /// (refreshed by every application), up to <paramref name="maxStacks"/>; the stronger per-stack damage wins.
        /// </summary>
        public void AddVenom(int row, float stacks, float damagePerStack, float seconds, float maxStacks, int source = -1)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            ref RowStatus status = ref statuses[row];
            float current = status.venomTime > 0.0f ? status.venomStacks : 0.0f;
            status.venomStacks = MathF.Min(MathF.Max(maxStacks, current), current + stacks);
            status.venomDps = MathF.Max(status.venomTime > 0.0f ? status.venomDps : 0.0f, damagePerStack);
            status.venomTime = MathF.Max(status.venomTime, seconds);
            lastSource[row] = source;
        }

        /// <summary>Removes an enemy's venom and returns the damage it had left to deal (stacks x damage x seconds left).</summary>
        public float ConsumeVenom(int row)
        {
            if (row < 0 || row >= rowCount || statuses[row].venomTime <= 0.0f)
            {
                return 0.0f;
            }
            ref RowStatus status = ref statuses[row];
            float remaining = status.venomStacks * status.venomDps * status.venomTime;
            status.venomTime = 0.0f;
            status.venomStacks = 0.0f;
            return remaining;
        }

        /// <summary>Bloodies an enemy for some seconds (Butcher's Chains): a tag weapons and Fury read.</summary>
        public void Bloody(int row, float seconds)
        {
            if (IsTargetable(row))
            {
                statuses[row].bloodTime = MathF.Max(statuses[row].bloodTime, seconds);
            }
        }

        /// <summary>Confuses an enemy: it wanders, ignores the hero and casts nothing for some seconds (smoke).</summary>
        public void Confuse(int row, float seconds)
        {
            if (IsTargetable(row) && !IsImmune(row, StatusImmunity.Confuse))
            {
                statuses[row].confuseTime = MathF.Max(statuses[row].confuseTime, ResistIfElite(row, seconds));
            }
        }

        /// <summary>Pops an enemy up off the ground for a moment (looks only; it keeps its place on the ground plane).</summary>
        public void Launch(int row, float height)
        {
            if (IsTargetable(row) && !IsImmune(row, StatusImmunity.Launch))
            {
                statuses[row].launchTime = LAUNCH_SECONDS;
                statuses[row].launchHeight = height / MathF.Max(types[typeOf[row]].weight, MIN_LAUNCH_WEIGHT);
            }
        }

        /// <summary>Wards an enemy (Hex Casters, pyres): faster and tougher for some seconds.</summary>
        public void Ward(int row, float seconds)
        {
            if (IsTargetable(row) && !IsImmune(row, StatusImmunity.Ward))
            {
                statuses[row].wardTime = MathF.Max(statuses[row].wardTime, seconds);
            }
        }

        /// <summary>Hastes an enemy for the rest of its life (Hasted elites): it moves faster and winds its casts up faster.</summary>
        /// <param name="speedScale">Movement speed multiplier (1.4 = +40%).</param>
        /// <param name="windupScale">Multiplier of every wind-up (0.75 = a quarter shorter).</param>
        public void Haste(int row, float speedScale, float windupScale)
        {
            if (IsLiveRow(row))
            {
                statuses[row].hasteSpeed = speedScale;
                statuses[row].windupScale = windupScale;
            }
        }

        /// <summary>
        /// Gives an enemy a bubble that absorbs <paramref name="amount"/> damage before its health (Warded elites);
        /// frozen enemies and critical hits break it twice as fast.
        /// </summary>
        public void AddBarrier(int row, float amount)
        {
            if (IsLiveRow(row))
            {
                statuses[row].barrier += amount;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Reading statuses
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The speed multiplier of the slow on an enemy now (1 when not slowed); proxies apply it themselves.</summary>
        public float GetSlowFactor(int row) => statuses[row].slowTime > 0.0f ? statuses[row].slowFactor : 1.0f;

        /// <summary>The venom stacks on an enemy now (0 when it carries none).</summary>
        public float GetVenomStacks(int row) => statuses[row].venomTime > 0.0f ? statuses[row].venomStacks : 0.0f;

        public bool IsBloodied(int row) => statuses[row].bloodTime > 0.0f;

        /// <summary>Whether the enemy carries the Hasted affix's speed.</summary>
        public bool IsHasted(int row) => statuses[row].hasteSpeed > 1.0f;

        /// <summary>Damage the enemy's bubble still absorbs (Warded elites); 0 without one.</summary>
        public float GetBarrier(int row) => statuses[row].barrier;

        /// <summary>Whether the enemy burns now.</summary>
        public bool IsBurning(int row) => statuses[row].burnTime > 0.0f;

        /// <summary>Whether the enemy is frozen now.</summary>
        public bool IsFrozen(int row) => statuses[row].freezeTime > 0.0f;

        public bool IsStunned(int row) => statuses[row].stunTime > 0.0f;

        /// <summary>
        /// The multiplier on damage the enemy takes now: its owner's scale (<see cref="SetDamageTakenScale"/>), frozen
        /// x1.25, warded x0.75, exposed x(1 + bonus), marked x(1 + bonus).
        /// </summary>
        public float GetDamageTakenMultiplier(int row)
        {
            ref RowStatus status = ref statuses[row];
            float multiplier = damageTakenScale[row] * (status.freezeTime > 0.0f ? FROZEN_DAMAGE_TAKEN : 1.0f);
            if (status.wardTime > 0.0f)
            {
                multiplier *= WARD_DAMAGE_TAKEN;
            }
            if (status.exposeTime > 0.0f)
            {
                multiplier *= 1.0f + status.exposeBonus;
            }
            return status.markTime > 0.0f ? multiplier * (1.0f + status.markBonus) : multiplier;
        }

        private bool IsImmune(int row, StatusImmunity status) => types[typeOf[row]].IsImmune(status);

        /// <summary>Whether the row holds a live enemy, rising or not (affixes go on as an elite climbs out).</summary>
        private bool IsLiveRow(int row) => row >= 0 && row < rowCount && alive[row] && states[row] != EnemyState.Dying;

        private float ResistIfElite(int row, float seconds) => elite[row] ? seconds * ELITE_STATUS_RESIST : seconds;

        private void StartFlash(int row)
        {
            if (statuses[row].flashTime <= -FLASH_GAP)
            {
                statuses[row].flashTime = FLASH_SECONDS;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Countdown (worker threads: each call touches only its own row)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Whether a freeze or a stun holds the row in place this frame.</summary>
        private bool IsHeld(int row) => statuses[row].freezeTime > 0.0f || statuses[row].stunTime > 0.0f;

        /// <summary>The row's speed multiplier from slows, wards and haste.</summary>
        private float GetSpeedFactor(int row)
        {
            ref RowStatus status = ref statuses[row];
            float slow = status.slowTime > 0.0f ? status.slowFactor : 1.0f;
            return slow * (status.wardTime > 0.0f ? WARD_SPEED : 1.0f) * status.hasteSpeed;
        }

        /// <summary>Deals the row's burn and venom for one step, then counts its timers down.</summary>
        private void TickStatus(int row, float dt)
        {
            ref RowStatus status = ref statuses[row];
            if (status.venomTime > 0.0f)
            {
                DealDamageOverTime(row, status.venomStacks * status.venomDps * dt);
            }
            if (status.burnTime > 0.0f)
            {
                DealDamageOverTime(row, status.burnDps * dt);
            }
            status.Tick(dt);
        }

        /// <summary>Burn or venom damage: scaled by the owner's damage taken, counted for its source, deadly in FinishStep.</summary>
        private void DealDamageOverTime(int row, float damage)
        {
            float dealt = damage * damageTakenScale[row];
            LoseHealth(row, dealt);
            statusDealt[row] += dealt;
            if (health[row] <= 0.0f)
            {
                diedInStep[row] = true;
            }
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>What Prismatic Ray's thermal shock does this frame, from the ray's level and stats.</summary>
    internal struct ShockSettings
    {
        /// <summary>A burst's damage after Might, before it grows with the hero's level.</summary>
        public float damage;
        /// <summary>A burst's radius after Area.</summary>
        public float radius;
        /// <summary>Seconds before the same enemy can burst again.</summary>
        public float restSeconds;
        /// <summary>The ray's beams: each adds to the shock budget.</summary>
        public int beams;
        /// <summary>Enemies within this distance of the mage are checked even while the ray rests.</summary>
        public float reach;
        public float fxScale;
        /// <summary>Aurora Lance's colours.</summary>
        public bool isAurora;
    }

    /// <summary>
    /// Thermal shock, the Mage-wide mechanic of Prismatic Ray: an enemy that burns and is chilled or frozen, from any
    /// source (Ember Orb burns, Starfall's impacts and craters scorch, Rime Nova chills and freezes, the ray does both),
    /// bursts for the shock damage x (1 + 0.1 x player level) in the shock radius, at most once per rest per enemy.
    /// Enemies the ray just crossed burst at once; enemies near the mage are checked every
    /// <see cref="CHECK_INTERVAL"/> s even while the ray rests. Bursts spend a budget refilled at
    /// <see cref="SHOCKS_PER_SECOND_PER_BEAM"/> per beam. Starfall deals fire damage without a burn status, so a hit
    /// from a Starfall slot counts as burning for <see cref="SCORCH_SECONDS"/>.
    /// Look: a flash, fire tongues, ice spikes and debris, a plume of steam and a warm and a cold ring (Aurora Lance:
    /// green and teal). Main thread; no allocations per frame.
    /// </summary>
    internal sealed class ThermalShock
    {
        private const int QUERY_CAPACITY = 1024;
        private const int MAX_FRESH = 24;
        private const float GROWTH_PER_LEVEL = 0.1f;
        private const float CHECK_INTERVAL = 0.1f;
        private const float SHOCKS_PER_SECOND_PER_BEAM = 1.0f;
        private const float BURST_PER_BEAM = 3.0f;
        private const float KNOCKBACK = 0.6f;
        private const float BURST_HEIGHT = 1.3f;
        private const float COLD_RING_SHARE = 1.3f;
        private const float VOLUME = 0.7f;
        private const float BOOM_VOLUME = 0.45f;
        private const float PITCH = 0.9f;
        /// <summary>How long a Starfall hit counts as burning (its craters tick every 0.5 s).</summary>
        private const float SCORCH_SECONDS = 0.8f;
        private const float NEVER = float.MinValue;

        private static readonly Color ICE = new Color(0.7f, 0.92f, 1.0f, 1.0f);
        private static readonly Color SHOCK_WHITE = new Color(1.0f, 0.95f, 0.9f, 1.0f);
        private static readonly Color AURORA_GREEN = new Color(0.4f, 1.0f, 0.7f, 1.0f);
        private static readonly Color AURORA_TEAL = new Color(0.3f, 0.85f, 1.0f, 1.0f);

        /// <summary>An enemy's last burst and how long it counts as scorched.</summary>
        private struct ShockState
        {
            public float shockAt;
            public float scorchUntil;
        }

        private readonly WeaponContext ctx;
        private readonly EnemyWorld enemies;
        private readonly int slot;
        private readonly int[] rows = new int[QUERY_CAPACITY];
        private readonly int[] fresh = new int[MAX_FRESH];
        private readonly EnemyTable<ShockState> states = new EnemyTable<ShockState>(new ShockState { shockAt = NEVER, scorchUntil = NEVER });
        private readonly int shockRing;
        private readonly FxComposite shockFire;
        private readonly FxComposite shockFrost;
        private readonly FxComposite shockCore;
        private int freshCount;
        private int scanCursor;
        private float checkTimer;
        /// <summary>Starts full (clamped to the capacity on the first frame).</summary>
        private float budget = float.MaxValue;

        /// <param name="slot">The ray's slot: the source of the bursts.</param>
        public ThermalShock(WeaponContext context, int slot)
        {
            ctx = context;
            enemies = context.enemies;
            this.slot = slot;
            Effects effects = context.effects;
            shockRing = effects.Find("MageShockRing");
            shockFire = new FxComposite(effects, new FxLayer("FireBall", 4, 0.6f), new FxLayer("FireSparks", 10));
            shockFrost = new FxComposite(effects, new FxLayer("SolarRays", 1, 1.2f), new FxLayer("FrostSpikes", 7), new FxLayer("IceShatter", 4, 0.0f, false));
            shockCore = new FxComposite(effects, new FxLayer("MageShockFlash", 1, 1.2f), new FxLayer("MageShockSteam", 5, 0.0f, false));
        }

        /// <summary>An enemy the ray's beams just hurt: it bursts first, this frame.</summary>
        public void NoteFresh(int row)
        {
            if (freshCount < MAX_FRESH)
            {
                fresh[freshCount++] = row;
            }
        }

        /// <summary>A hit from any source: Starfall's (or Cataclysm's) scorch the enemy. Runs inside the damage call: state only.</summary>
        public void OnEnemyDamaged(int row, int source, float now)
        {
            if (row >= 0 && ctx.loadout.GetWeapon(source) is Starfall)
            {
                states.Get(enemies.GetEnemy(row)).scorchUntil = now + SCORCH_SECONDS;
            }
        }

        /// <summary>Refills the budget, bursts the fresh enemies, and every check interval the hot and cold ones around the mage.</summary>
        public void Update(float dt, float now, Vector3 center, in ShockSettings settings)
        {
            int beams = Math.Max(1, settings.beams);
            budget = MathF.Min(BURST_PER_BEAM * beams, budget + SHOCKS_PER_SECOND_PER_BEAM * beams * dt);
            for (int i = 0; i < freshCount; i++)
            {
                TryShock(fresh[i], now, settings);
            }
            freshCount = 0;
            checkTimer -= dt;
            if (checkTimer <= 0.0f)
            {
                checkTimer += CHECK_INTERVAL;
                ShockNearby(center, now, settings);
            }
        }

        /// <summary>Burning and chilled enemies near the mage; the scan starts at a turning offset.</summary>
        private void ShockNearby(Vector3 center, float now, in ShockSettings settings)
        {
            if (budget < 1.0f)
            {
                return;
            }
            int found = enemies.QueryCircle(center, settings.reach, rows);
            if (found == 0)
            {
                return;
            }
            scanCursor = (scanCursor + 1) % found;
            for (int i = 0; i < found && budget >= 1.0f; i++)
            {
                TryShock(rows[(scanCursor + i) % found], now, settings);
            }
        }

        private void TryShock(int row, float now, in ShockSettings settings)
        {
            if (budget < 1.0f || !enemies.IsTargetable(row) || !IsHot(row, now) || !IsCold(row))
            {
                return;
            }
            ref ShockState state = ref states.Get(enemies.GetEnemy(row));
            if (now - state.shockAt < settings.restSeconds)
            {
                return;
            }
            state.shockAt = now;
            budget -= 1.0f;
            Burst(enemies.GetPosition(row), settings);
        }

        private bool IsHot(int row, float now) => enemies.IsBurning(row) || states.Get(enemies.GetEnemy(row)).scorchUntil > now;

        private bool IsCold(int row) => enemies.IsFrozen(row) || enemies.GetSlowFactor(row) < 1.0f;

        private void Burst(Vector3 position, in ShockSettings settings)
        {
            Vector3 ground = Ground.Flat(position);
            float radius = settings.radius;
            float damage = settings.damage * (1.0f + GROWTH_PER_LEVEL * ctx.run.level);
            var effect = new HitEffect { numberKind = DamageKind.Burn };
            ctx.strikes.Circle(ground, radius, damage, Strikes.KnockbackOf(KNOCKBACK), slot, effect);
            Vector3 burst = ground + Ground.Up(BURST_HEIGHT);
            float scale = settings.fxScale;
            shockCore.Play(burst, radius, scale, settings.isAurora ? AURORA_GREEN : SHOCK_WHITE);
            shockFire.Play(burst, radius, scale, MageLooks.FIRE);
            shockFrost.Play(burst, radius, scale, settings.isAurora ? AURORA_TEAL : ICE);
            Vector3 floor = new Vector3(ground.x, Tuning.TELEGRAPH_HEIGHT, ground.z);
            ctx.effects.EmitSized(shockRing, floor, 1, MageLooks.FIRE, radius * 2.0f);
            ctx.effects.EmitSized(shockRing, floor, 1, settings.isAurora ? AURORA_TEAL : ICE, radius * 2.0f * COLD_RING_SHARE);
            ctx.sounds.PlayAt("frost_shatter", ground, VOLUME, PITCH);
            ctx.sounds.PlayAt("ember_boom", ground, BOOM_VOLUME);
        }
    }
}

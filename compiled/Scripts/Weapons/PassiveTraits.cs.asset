using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The traits of the passive items, each scaled by the passive's rank: Heartbeat (Iron Heart), Spiked Plate (Plate
    /// Pauldron), Tailwind (Windstride Boots), Opening Strike (Whetstone), Soul Magnet (Lodestone Charm), Aftershock
    /// (Wide Sigil), Kindle (Everburn Wick), Epiphany (Tome of Insight), Exploit (Keen Eye), Lucky Break (Lucky
    /// Knucklebone) and Overdrive (Quickening Hourglass). The loadout owns it, forwards ticks, kills, level-ups and
    /// active uses, and calls <see cref="RefreshRanks"/> when its passives change; area hits arrive from
    /// <see cref="Strikes.areaHit"/>. Damage it causes is queued and dealt in <see cref="Tick"/>, never inside a damage
    /// call. Main thread.
    /// </summary>
    public sealed class PassiveTraits
    {
        private const float HEARTBEAT_SECONDS = 10.0f;
        private const float HEARTBEAT_SHARE = 0.015f;
        private const float SPIKES_DAMAGE = 12.0f;
        private const float SPIKES_DAMAGE_PER_ARMOR = 3.0f;
        private const float SPIKES_RADIUS = 2.5f;
        private const float SPIKES_KNOCKBACK = 1.5f;
        private const float SPIKES_GAP = 0.5f;
        private const float DODGE_PER_RANK = 0.03f;
        private const float OPENING_STRIKE_PER_RANK = 0.06f;
        /// <summary>Enemies at or above this health share take the Opening Strike bonus.</summary>
        public const float OPENING_STRIKE_HEALTH = 0.75f;
        private const float SOUL_MAGNET_BASE_SECONDS = 65.0f;
        private const float SOUL_MAGNET_SECONDS_PER_RANK = 9.0f;
        private const float AFTERSHOCK_PER_RANK = 0.08f;
        private const float AFTERSHOCK_DELAY = 0.5f;
        /// <summary>Area hits smaller than this radius (single targets, thin lines) make no aftershock.</summary>
        private const float AFTERSHOCK_MIN_RADIUS = 1.5f;
        private const int AFTERSHOCK_CAPACITY = 64;
        /// <summary>Lingering zones tick often: their aftershock rings show at most this often.</summary>
        private const float AFTERSHOCK_RING_GAP = 0.12f;
        private const float KINDLE_DURATION_PER_RANK = 0.12f;
        private const float KINDLE_BURN_PER_RANK = 0.08f;
        private const float EPIPHANY_RADIUS = 7.0f;
        private const float EPIPHANY_DAMAGE = 15.0f;
        private const float EPIPHANY_DAMAGE_PER_LEVEL = 3.0f;
        private const float EPIPHANY_KNOCKBACK = 3.0f;
        private const float EPIPHANY_TRAUMA = 0.2f;
        private const float EPIPHANY_SHAKE_SECONDS = 0.25f;
        private const float EXPLOIT_PER_RANK = 0.04f;
        private const float EXPLOIT_SECONDS = 3.0f;
        private const float LUCKY_COIN_PER_RANK = 0.008f;
        private const float LUCKY_ROAST_PER_RANK = 0.005f;
        private const float LUCKY_ROAST_GAP = 30.0f;
        private const int MAX_ROASTS_ON_GROUND = 3;
        /// <summary>Luck counts in points: 100 points double Lucky Break's chances.</summary>
        private const float LUCK_POINTS_PER_DOUBLE = 100.0f;
        private const float OVERDRIVE_COOLDOWN = -0.25f;
        private const float OVERDRIVE_SECONDS_PER_RANK = 0.6f;
        /// <summary>Evolutions deal this much more per rank of the passive that evolved them (the bond).</summary>
        public const float BOND_PER_RANK = 0.06f;
        private const float TEXT_HEIGHT = 2.4f;
        private const float DODGE_PUFF_HEIGHT = 0.8f;
        /// <summary>Soul Magnet's look in metres.</summary>
        private const float SOUL_MAGNET_LOOK_SIZE = 1.5f;

        private static readonly Color HEART_RED = new Color(1.0f, 0.35f, 0.42f, 1.0f);
        private static readonly Color STEEL = new Color(0.86f, 0.9f, 1.0f, 1.0f);
        private static readonly Color WIND = new Color(0.75f, 0.95f, 1.0f, 1.0f);
        private static readonly Color RUNE = new Color(0.62f, 0.86f, 1.0f, 1.0f);
        private static readonly Color ARCANE = new Color(0.72f, 0.55f, 1.0f, 1.0f);
        private static readonly Color SOUL = new Color(0.45f, 1.0f, 0.82f, 1.0f);
        private static readonly Color HASTE = new Color(1.0f, 0.86f, 0.45f, 1.0f);

        /// <summary>A queued aftershock pulse.</summary>
        private struct Pulse
        {
            public float time;
            public Vector3 center;
            public float radius;
            public float damage;
        }

        private readonly Run run;
        private readonly WeaponContext context;
        private readonly Loadout loadout;
        /// <summary>This run's hero and stats: a new run makes new ones before this loadout is disposed.</summary>
        private readonly HeroController hero;
        private readonly PlayerStats stats;
        private readonly Pulse[] pulses = new Pulse[AFTERSHOCK_CAPACITY];
        private readonly FxComposite heartbeatLook;
        private readonly FxComposite spikesLook;
        private readonly FxComposite aftershockLook;
        private readonly FxComposite epiphanyLook;
        private readonly FxComposite soulMagnetLook;
        private readonly FxComposite dodgeLook;
        private readonly FxComposite overdriveLook;
        private int pulseCount;
        private float clock;
        private float lastAftershockRing = -AFTERSHOCK_RING_GAP;
        private float heartbeatTimer = HEARTBEAT_SECONDS;
        private float soulMagnetTimer;
        private bool isSpikePending;
        private float lastSpikes = -SPIKES_GAP;
        private int pendingEpiphanies;
        private float lastRoast = -LUCKY_ROAST_GAP;
        private float overdriveLeft;
        private bool isOverdriveApplied;
        private int appliedKindleRank = -1;
        /// <summary>The ranks of the passives with a trait, 0 when not owned (see <see cref="RefreshRanks"/>).</summary>
        private int ironHeartRank;
        private int platePauldronRank;
        private int windstrideBootsRank;
        private int lodestoneCharmRank;
        private int wideSigilRank;
        private int everburnWickRank;
        private int tomeOfInsightRank;
        private int keenEyeRank;
        private int luckyKnuckleboneRank;
        private int quickeningHourglassRank;

        public PassiveTraits(WeaponContext context, Loadout loadout)
        {
            this.context = context;
            this.loadout = loadout;
            run = context.run;
            hero = context.hero;
            stats = context.stats;
            Effects effects = context.effects;
            // Size units: metres (heartbeat, soul magnet, dodge, overdrive) or the hit radius (spikes, aftershock, epiphany).
            heartbeatLook = new FxComposite(effects, new FxLayer("CoreFlash", 1, 1.6f), FxLayer.Ground("CoreRing", 6.0f),
                                            new FxLayer("CoreMotes", 10));
            spikesLook = new FxComposite(effects, new FxLayer("CoreFlash", 1, 0.5f), FxLayer.Ground("CoreRing", FxSizes.RING_SIZE_PER_RADIUS),
                                         new FxLayer("CoreShards", 22));
            aftershockLook = new FxComposite(effects, FxLayer.Ground("CoreRune", FxSizes.SIGIL_SIZE_PER_RADIUS),
                                             FxLayer.Ground("CoreRing", FxSizes.RING_SIZE_PER_RADIUS));
            epiphanyLook = new FxComposite(effects, new FxLayer("CoreFlash", 1, 0.45f), FxLayer.Ground("CoreRing", FxSizes.RING_SIZE_PER_RADIUS),
                                           FxLayer.Ground("CoreTwirl", 2.4f), new FxLayer("CoreMotes", 24));
            soulMagnetLook = new FxComposite(effects, FxLayer.Ground("CoreTwirl", 4.0f), new FxLayer("CoreMotes", 14));
            dodgeLook = new FxComposite(effects, new FxLayer("CoreWindPuff", 10));
            overdriveLook = new FxComposite(effects, new FxLayer("CoreFlash", 1, 1.4f), FxLayer.Ground("CoreRing", 4.0f),
                                            new FxLayer("CoreMotes", 12));
            run.combatEvents.hit += OnHit;
            hero.damaged += OnHeroDamaged;
            hero.dodged += OnDodged;
            context.strikes.areaHit += OnAreaHit;
        }

        /// <summary>Lets go of hero events (the run's loadout is replaced).</summary>
        public void Dispose()
        {
            hero.damaged -= OnHeroDamaged;
            hero.dodged -= OnDodged;
            run.combatEvents.hit -= OnHit;
            context.strikes.areaHit -= OnAreaHit;
            EndOverdrive();
        }

        /// <summary>Opening Strike's damage bonus (0 without Whetstone) against enemies at or above 75% health.</summary>
        public float openingStrikeBonus { get; private set; }

        /// <summary>Reads the ranks of the trait passives again; the loadout calls it whenever its passives change.</summary>
        public void RefreshRanks()
        {
            ironHeartRank = loadout.GetPassiveRank(PassiveIds.IRON_HEART);
            platePauldronRank = loadout.GetPassiveRank(PassiveIds.PLATE_PAULDRON);
            windstrideBootsRank = loadout.GetPassiveRank(PassiveIds.WINDSTRIDE_BOOTS);
            lodestoneCharmRank = loadout.GetPassiveRank(PassiveIds.LODESTONE_CHARM);
            wideSigilRank = loadout.GetPassiveRank(PassiveIds.WIDE_SIGIL);
            everburnWickRank = loadout.GetPassiveRank(PassiveIds.EVERBURN_WICK);
            tomeOfInsightRank = loadout.GetPassiveRank(PassiveIds.TOME_OF_INSIGHT);
            keenEyeRank = loadout.GetPassiveRank(PassiveIds.KEEN_EYE);
            luckyKnuckleboneRank = loadout.GetPassiveRank(PassiveIds.LUCKY_KNUCKLEBONE);
            quickeningHourglassRank = loadout.GetPassiveRank(PassiveIds.QUICKENING_HOURGLASS);
            openingStrikeBonus = OPENING_STRIKE_PER_RANK * loadout.GetPassiveRank(PassiveIds.WHETSTONE);
        }

        /// <summary>Once per frame while playing.</summary>
        public void Tick(float dt)
        {
            clock += dt;
            hero.dodgeChance = DODGE_PER_RANK * windstrideBootsRank;
            ApplyKindle();
            TickHeartbeat(dt);
            TickSoulMagnet(dt);
            TickOverdrive(dt);
            ReleaseSpikes();
            ReleaseEpiphanies();
            ReleasePulses();
        }

        // -------------------------------------------------------------------------------------------------------
        // Hooks
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Lucky Break: kills sometimes drop a coin, and now and then a roast. Runs inside the killing damage call.</summary>
        public void OnKill(in EnemyDeath death)
        {
            int rank = luckyKnuckleboneRank;
            if (rank <= 0 || !death.isRewarded)
            {
                return;
            }
            float luck = 1.0f + stats[StatId.Luck] / LUCK_POINTS_PER_DOUBLE;
            if (run.rng.NextDouble() < LUCKY_COIN_PER_RANK * rank * luck)
            {
                run.pickups.Spawn(PickupKind.Gold, death.position, 1.0f);
            }
            bool canRoast = run.runTime - lastRoast >= LUCKY_ROAST_GAP && run.pickups.CountOf(PickupKind.Food) < MAX_ROASTS_ON_GROUND;
            if (canRoast && run.rng.NextDouble() < LUCKY_ROAST_PER_RANK * rank * luck)
            {
                lastRoast = run.runTime;
                run.pickups.Spawn(PickupKind.Food, death.position, 1.0f);
            }
        }

        /// <summary>Epiphany: a level-up pick queues a nova for when play resumes.</summary>
        public void OnLevelUp()
        {
            if (tomeOfInsightRank > 0)
            {
                pendingEpiphanies++;
            }
        }

        /// <summary>Overdrive: after the active, weapons recharge faster for a moment.</summary>
        public void OnActive()
        {
            int rank = quickeningHourglassRank;
            if (rank <= 0)
            {
                return;
            }
            overdriveLeft = MathF.Max(overdriveLeft, OVERDRIVE_SECONDS_PER_RANK * rank);
            if (!isOverdriveApplied)
            {
                isOverdriveApplied = true;
                stats.AddPercent(StatId.Cooldown, OVERDRIVE_COOLDOWN);
            }
            overdriveLook.Play(hero.position, FxSizes.METRE_UNIT, 1.0f, HASTE);
        }

        /// <summary>Aftershock: an area hit that dealt damage pulses again a moment later for a share of it.</summary>
        private void OnAreaHit(Vector3 center, float radius, float damage)
        {
            int rank = wideSigilRank;
            if (rank <= 0 || damage <= 0.0f || radius < AFTERSHOCK_MIN_RADIUS || pulseCount >= AFTERSHOCK_CAPACITY)
            {
                return;
            }
            pulses[pulseCount++] = new Pulse
            {
                time = clock + AFTERSHOCK_DELAY,
                center = center,
                radius = radius,
                damage = damage * AFTERSHOCK_PER_RANK * rank,
            };
        }

        /// <summary>Exploit: crits expose the enemy. Runs inside the damage call (state only).</summary>
        private void OnHit(int row, int source, float amount, bool isCritical)
        {
            if (isCritical && keenEyeRank > 0)
            {
                run.enemies.Expose(row, EXPLOIT_PER_RANK * keenEyeRank, EXPLOIT_SECONDS);
            }
        }

        /// <summary>Spiked Plate: a hit queues the spikes (they burst on the next tick, at most every 0.5 s).</summary>
        private void OnHeroDamaged(float amount, Vector3 from)
        {
            if (platePauldronRank > 0)
            {
                isSpikePending = true;
            }
        }

        /// <summary>Tailwind: a dodged hit shows DODGE and a gust.</summary>
        private void OnDodged(Vector3 from)
        {
            Vector3 position = hero.position;
            run.damageNumbers?.ShowText(position + Ground.Up(TEXT_HEIGHT), "DODGE", DamageKind.Frost);
            dodgeLook.Play(position + Ground.Up(DODGE_PUFF_HEIGHT), FxSizes.METRE_UNIT, 1.0f, WIND);
        }

        // -------------------------------------------------------------------------------------------------------
        // Timed traits
        // -------------------------------------------------------------------------------------------------------

        private void TickHeartbeat(float dt)
        {
            int rank = ironHeartRank;
            if (rank <= 0)
            {
                heartbeatTimer = HEARTBEAT_SECONDS;
                return;
            }
            heartbeatTimer -= dt;
            if (heartbeatTimer > 0.0f)
            {
                return;
            }
            heartbeatTimer = HEARTBEAT_SECONDS;
            context.HealHero(hero.maxHealth * HEARTBEAT_SHARE * rank, HealSource.Passive);
            heartbeatLook.Play(hero.position, FxSizes.METRE_UNIT, 1.0f, HEART_RED);
        }

        private void TickSoulMagnet(float dt)
        {
            int rank = lodestoneCharmRank;
            if (rank <= 0)
            {
                soulMagnetTimer = GetSoulMagnetSeconds(1);
                return;
            }
            soulMagnetTimer = MathF.Min(soulMagnetTimer, GetSoulMagnetSeconds(rank)) - dt;
            if (soulMagnetTimer > 0.0f)
            {
                return;
            }
            soulMagnetTimer = GetSoulMagnetSeconds(rank);
            run.pickups.AttractAll();
            soulMagnetLook.Play(hero.position, SOUL_MAGNET_LOOK_SIZE, 1.0f, SOUL);
            run.sounds.Play("lodestone");
        }

        private static float GetSoulMagnetSeconds(int rank) => SOUL_MAGNET_BASE_SECONDS - SOUL_MAGNET_SECONDS_PER_RANK * rank;

        private void TickOverdrive(float dt)
        {
            if (overdriveLeft <= 0.0f)
            {
                return;
            }
            overdriveLeft -= dt;
            if (overdriveLeft <= 0.0f)
            {
                EndOverdrive();
            }
        }

        private void EndOverdrive()
        {
            overdriveLeft = 0.0f;
            if (isOverdriveApplied)
            {
                isOverdriveApplied = false;
                stats.AddPercent(StatId.Cooldown, -OVERDRIVE_COOLDOWN);
            }
        }

        /// <summary>Kindle scales burns, chills and freezes through the enemy world.</summary>
        private void ApplyKindle()
        {
            int rank = everburnWickRank;
            if (rank == appliedKindleRank)
            {
                return;
            }
            appliedKindleRank = rank;
            run.enemies.statusDurationScale = 1.0f + KINDLE_DURATION_PER_RANK * rank;
            run.enemies.burnDamageScale = 1.0f + KINDLE_BURN_PER_RANK * rank;
        }

        // -------------------------------------------------------------------------------------------------------
        // Queued damage
        // -------------------------------------------------------------------------------------------------------

        private void ReleaseSpikes()
        {
            if (!isSpikePending || clock - lastSpikes < SPIKES_GAP)
            {
                return;
            }
            isSpikePending = false;
            lastSpikes = clock;
            float damage = context.MightOf(SPIKES_DAMAGE * platePauldronRank + SPIKES_DAMAGE_PER_ARMOR * stats[StatId.Armor]);
            Vector3 center = hero.position;
            Combat.DamageCircle(center, SPIKES_RADIUS, damage, Strikes.KnockbackOf(SPIKES_KNOCKBACK), RunRecord.OTHER_SOURCE);
            spikesLook.Play(center, SPIKES_RADIUS, 1.0f, STEEL);
        }

        private void ReleaseEpiphanies()
        {
            if (pendingEpiphanies <= 0 || run.phase != RunPhase.Playing)
            {
                return;
            }
            pendingEpiphanies = 0;
            int rank = tomeOfInsightRank;
            if (rank <= 0)
            {
                return;
            }
            float damage = context.MightOf((EPIPHANY_DAMAGE + EPIPHANY_DAMAGE_PER_LEVEL * run.level) * rank);
            Vector3 center = hero.position;
            Combat.DamageCircle(center, EPIPHANY_RADIUS, damage, Strikes.KnockbackOf(EPIPHANY_KNOCKBACK), RunRecord.OTHER_SOURCE);
            epiphanyLook.Play(center, EPIPHANY_RADIUS, 1.0f, ARCANE);
            run.feedback.Shake(EPIPHANY_TRAUMA, EPIPHANY_SHAKE_SECONDS);
        }

        private void ReleasePulses()
        {
            int kept = 0;
            for (int i = 0; i < pulseCount; i++)
            {
                Pulse pulse = pulses[i];
                if (clock < pulse.time)
                {
                    pulses[kept++] = pulse;
                    continue;
                }
                Combat.DamageCircle(pulse.center, pulse.radius, pulse.damage, 0.0f, RunRecord.OTHER_SOURCE);
                if (clock - lastAftershockRing >= AFTERSHOCK_RING_GAP)
                {
                    lastAftershockRing = clock;
                    aftershockLook.Play(pulse.center, pulse.radius, 1.0f, RUNE);
                }
            }
            pulseCount = kept;
        }

        // -------------------------------------------------------------------------------------------------------
        // Text
        // -------------------------------------------------------------------------------------------------------

        /// <summary>A passive's trait at a rank, as a sentence for cards, details and the pause sheet; empty for none.</summary>
        public static string Describe(string passiveId, int rank)
        {
            rank = Math.Max(1, rank);
            switch (passiveId)
            {
                case PassiveIds.IRON_HEART:
                    return $"Heartbeat: heal {Percent(HEARTBEAT_SHARE * rank)} of max health every {Number(HEARTBEAT_SECONDS)} s.";
                case PassiveIds.PLATE_PAULDRON:
                    return $"Spiked Plate: hits on you burst spikes for {Number(SPIKES_DAMAGE * rank)} + {Number(SPIKES_DAMAGE_PER_ARMOR)} x Armor.";
                case PassiveIds.WINDSTRIDE_BOOTS:
                    return $"Tailwind: {Percent(DODGE_PER_RANK * rank)} chance to dodge hits.";
                case PassiveIds.WHETSTONE:
                    return $"Opening Strike: +{Percent(OPENING_STRIKE_PER_RANK * rank)} damage above {Percent(OPENING_STRIKE_HEALTH)} health.";
                case PassiveIds.LODESTONE_CHARM:
                    return $"Soul Magnet: all gems fly to you every {Number(GetSoulMagnetSeconds(rank), "0")} s.";
                case PassiveIds.WIDE_SIGIL:
                    return $"Aftershock: area hits pulse again for {Percent(AFTERSHOCK_PER_RANK * rank)}.";
                case PassiveIds.EVERBURN_WICK:
                    return $"Kindle: burn, chill, freeze +{Percent(KINDLE_DURATION_PER_RANK * rank)} time, burns +{Percent(KINDLE_BURN_PER_RANK * rank)}.";
                case PassiveIds.TOME_OF_INSIGHT:
                    return $"Epiphany: level-ups blast {WeaponText.PlusPerLevel(EPIPHANY_DAMAGE * rank, EPIPHANY_DAMAGE_PER_LEVEL * rank)} around you.";
                case PassiveIds.KEEN_EYE:
                    return $"Exploit: crits expose foes, +{Percent(EXPLOIT_PER_RANK * rank)} damage taken.";
                case PassiveIds.LUCKY_KNUCKLEBONE:
                    return $"Lucky Break: kills drop coins ({Percent(LUCKY_COIN_PER_RANK * rank)}) and roasts.";
                case PassiveIds.QUICKENING_HOURGLASS:
                    return $"Overdrive: your active speeds weapons {Percent(-OVERDRIVE_COOLDOWN)} for {Number(OVERDRIVE_SECONDS_PER_RANK * rank)} s.";
                default:
                    return "";
            }
        }

        private static string Percent(float share) => WeaponText.Percent(share);

        private static string Number(float value, string format = "0.#") => WeaponText.Number(value, format);
    }
}

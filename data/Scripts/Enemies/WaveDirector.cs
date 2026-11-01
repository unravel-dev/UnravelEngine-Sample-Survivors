using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Spawns the run: a threat budget spent every half second in groups of one archetype by the minute's mix, a floor
    /// and a cap on enemies alive, tint tiers by minute, forward-biased placement just outside the view, recycling of
    /// enemies left far behind, and the timeline of events (rings, rushes, the archer line, elites with affixes,
    /// mini-bosses, the Last Tide and the boss). It owns the Grave Lanterns and the <see cref="BossManager"/>. Main
    /// thread.
    /// </summary>
    public sealed class WaveDirector
    {
        /// <summary>Each Dread level adds this share to enemy and boss health (0.25 = +25%).</summary>
        public const float DREAD_HEALTH_PER_LEVEL = 0.25f;
        /// <summary>Each Dread level adds this share to the damage of enemies and bosses.</summary>
        public const float DREAD_DAMAGE_PER_LEVEL = 0.1f;
        /// <summary>Each Dread level adds this share to the spawn budget.</summary>
        public const float DREAD_BUDGET_PER_LEVEL = 0.15f;
        /// <summary>Each Dread level adds this share to the experience of kills (kept small: experience is power).</summary>
        public const float DREAD_EXPERIENCE_PER_LEVEL = 0.05f;
        /// <summary>Each Dread level adds this share to the gold of coins, chests and bosses: the reward for the risk.</summary>
        public const float DREAD_GOLD_PER_LEVEL = 0.1f;
        /// <summary>Enemy health over the run: 1 + PER_MINUTE t + PER_MINUTE_SQUARED t^2 (x3.1 at 5:00, x7.5 at 10:00, x12.6 at 14:00).</summary>
        private const float HEALTH_PER_MINUTE = 0.2f;
        private const float HEALTH_PER_MINUTE_SQUARED = 0.045f;
        /// <summary>Enemy damage over the run: 1 + PER_MINUTE t.</summary>
        private const float DAMAGE_PER_MINUTE = 0.07f;
        /// <summary>Experience of a kill over the run: 1 + PER_MINUTE t.</summary>
        private const float EXPERIENCE_PER_MINUTE = 0.1f;
        private const float PULSE_SECONDS = 0.5f;
        private const float BUDGET_CARRY_SECONDS = 3.0f;
        /// <summary>Budget per second: (BASE + PER_MINUTE t + PER_MINUTE_SQUARED t^2) x the event factor x the Dread factor.</summary>
        private const float BUDGET_BASE = 1.2f;
        private const float BUDGET_PER_MINUTE = 1.1f;
        private const float BUDGET_PER_MINUTE_SQUARED = 0.07f;
        /// <summary>Enemies alive the director keeps at least: BASE + PER_MINUTE t, at most MAX.</summary>
        private const float FLOOR_BASE = 20.0f;
        private const float FLOOR_PER_MINUTE = 50.0f;
        private const float FLOOR_MAX = 800.0f;
        /// <summary>Enemies alive past which the budget buys nothing: BASE + PER_MINUTE t, at most MAX.</summary>
        private const float CAP_BASE = 300.0f;
        private const float CAP_PER_MINUTE = 200.0f;
        private const float CAP_MAX = 3000.0f;
        private const int BOSS_FIGHT_FLOOR = 50;
        private const int BOSS_FIGHT_CAP = 400;
        /// <summary>Behind a mini-boss's ward the horde thins to a trickle: the boss is the fight.</summary>
        private const int MINI_BOSS_FLOOR = 25;
        private const int MINI_BOSS_CAP = 120;
        private const int FLOOR_TOP_UP_PER_PULSE = 20;
        /// <summary>Floor top-ups are Shamblers this often (always in the first minute), Runners otherwise.</summary>
        private const double FILLER_SHAMBLER_SHARE = 0.8;
        private const float FILLER_RUNNERS_FROM_MINUTE = 1.0f;
        /// <summary>A bought group has 3 to 8 enemies (the upper bound is exclusive).</summary>
        private const int GROUP_MIN = 3;
        private const int GROUP_MAX_EXCLUSIVE = 9;
        /// <summary>A group spreads over a disc of BASE + PER_ENEMY x size metres.</summary>
        private const float GROUP_SPREAD_BASE = 1.0f;
        private const float GROUP_SPREAD_PER_ENEMY = 0.25f;
        private const float SPAWN_RADIUS_MIN = 22.0f;
        private const float SPAWN_RADIUS_MAX = 26.0f;
        private const float FORWARD_SHARE = 0.5f;
        private const float FORWARD_HALF_ANGLE = 60.0f;
        /// <summary>Above this speed (m/s) the hero counts as moving, and spawns lean ahead of it.</summary>
        private const float MOVING_SPEED = 1.0f;
        /// <summary>Spawns keep this far inside the arena edge.</summary>
        private const float ARENA_MARGIN = 2.0f;
        private const float RECYCLE_DISTANCE = 40.0f;
        private const float RECYCLE_INTERVAL = 1.0f;
        private const float ELITE_DISTANCE = 20.0f;
        private const float AFFIX_FROM_MINUTE = 8.0f;
        /// <summary>Hasted elites: +40% speed, wind-ups x0.75.</summary>
        private const float HASTE_SPEED = 1.4f;
        private const float HASTE_WINDUP = 0.75f;
        /// <summary>Warded elites: a bubble worth 30% of their health.</summary>
        private const float WARD_BARRIER_SHARE = 0.3f;
        /// <summary>Splitter elites: five Grave Runners burst from the body.</summary>
        private const int SPLITTER_RUNNERS = 5;
        private const float SPLITTER_RADIUS = 1.2f;
        private const float MINI_BOSS_BUDGET = 0.25f;
        private const float MINI_BOSS_DISTANCE = 14.0f;
        private const float BOSS_BUDGET = 0.3f;
        private const float LAST_TIDE_BUDGET = 1.5f;
        private const float LAST_TIDE_SECONDS = 45.0f;
        private const float SPAWNS_STOP_SECOND = 13.0f * 60.0f + 45.0f;
        private const float BOSS_WARNING_SECOND = 13.0f * 60.0f + 50.0f;
        private const float BOSS_SECOND = 14.0f * 60.0f;
        private const float BOSS_DISTANCE = 12.0f;
        private const float DEV_LANTERN_DISTANCE = 8.0f;
        private const float BOSS_CLEAR_RADIUS = 8.0f;
        private const int OPENING_RING_COUNT = 20;
        private const float OPENING_RING_MIN = 14.0f;
        private const float OPENING_RING_SPREAD = 4.0f;
        private const float RING_MARCH_SPEED = 1.6f;
        private const float RING_MARCH_SECONDS = 8.0f;
        /// <summary>The dead of a trap ring: how many times their kind's health, and how many tiers darker and larger.</summary>
        private const float RING_TOUGHNESS_EARLY = 4.0f;
        private const float RING_TOUGHNESS = 8.0f;
        private const float RING_TOUGHNESS_ARMORED = 3.0f;
        private const int RING_TINT_TIERS = 2;
        private const float RUSH_SPEED = 6.5f;
        private const float RUSH_LENGTH = 60.0f;
        private const float RUSH_WIDTH = 20.0f;
        private const float RUSH_DISTANCE = 26.0f;
        private const float RUSH_WARNING = 2.0f;
        /// <summary>Each further line of a rush comes this much later than the one before.</summary>
        private const float RUSH_LINE_DELAY = 2.0f;
        private const float BANNER_SECONDS = 2.5f;
        private const float LAST_TIDE_BANNER_SECONDS = 3.0f;
        private const float BOSS_WARNING_BANNER_SECONDS = 4.0f;
        private const float MIN_HEADING_SQUARED = 0.01f;
        private const float FULL_TURN_DEGREES = 360.0f;
        private const float SECONDS_PER_MINUTE = 60.0f;
        /// <summary>Kills the director makes are credited to no weapon.</summary>
        private const int NO_SOURCE = -1;
        private const int SCRATCH = 1024;

        /// <summary>Enemy mix per minute (share of regular spawns): Shambler, Runner, Archer, Shieldbearer, Cinder Skull, Hex Caster.</summary>
        private static readonly float[,] MIX =
        {
            { 100, 0, 0, 0, 0, 0 },
            { 85, 15, 0, 0, 0, 0 },
            { 70, 15, 15, 0, 0, 0 },
            { 60, 15, 15, 10, 0, 0 },
            { 55, 15, 10, 10, 10, 0 },
            { 50, 15, 10, 10, 10, 5 },
            { 45, 15, 12, 12, 8, 8 },
            { 40, 20, 12, 12, 8, 8 },
            { 40, 15, 12, 15, 10, 8 },
            { 38, 15, 12, 15, 10, 10 },
            { 35, 15, 14, 16, 10, 10 },
            { 35, 15, 12, 16, 10, 12 },
            { 30, 20, 12, 18, 10, 10 },
            { 35, 20, 10, 15, 12, 8 },
            { 55, 20, 0, 15, 10, 0 },
        };

        private readonly Run run;
        private readonly GraveLanterns lanterns;

        /// <summary>The "dead close in" trap.</summary>
        public readonly DeadRing deadRing;
        private readonly List<TimedEvent> events = new List<TimedEvent>();
        private readonly List<PendingRush> rushes = new List<PendingRush>();
        /// <summary>Splitter elites alive: their death spawns Runners.</summary>
        private readonly HashSet<Enemy> splitters = new HashSet<Enemy>();
        private readonly EliteAffixLooks affixLooks;
        private readonly int[] scratch = new int[SCRATCH];
        private readonly int eliteBeam;
        private int nextEvent;
        private float budget;
        private float pulseTimer;
        private float recycleTimer;
        private float lastTideUntil = -1.0f;
        private bool isBossFight;
        private bool spawnsStopped;
        /// <summary>Dread chosen for this run (0..5): tougher enemies, a bigger budget, more experience.</summary>
        private int dread;

        /// <summary>Dev (balance runs): plays the current run at a Dread level without touching the save.</summary>
        public void DevSetDread(int level) => dread = Math.Clamp(level, 0, SaveData.MAX_DREAD);

        /// <summary>What hurt the hero last, for the death screen.</summary>
        public string lastDamageSource = "";
        /// <summary>Dev: no timeline, rushes or budget spawns (bosses still fight), for isolated boss reviews.</summary>
        public bool devQuiet;

        /// <summary>The bosses of the run (mini-bosses and the Hollow King).</summary>
        public readonly BossManager bosses;

        private float seconds => run.runTime;
        private float minutes => run.runTime / SECONDS_PER_MINUTE;

        /// <summary>Health multiplier of enemies spawned now: the run's growth x <see cref="dreadScale"/>.</summary>
        public float healthScale => (1.0f + HEALTH_PER_MINUTE * minutes + HEALTH_PER_MINUTE_SQUARED * minutes * minutes) * dreadScale;

        /// <summary>The Dread part of enemy and boss health.</summary>
        public float dreadScale => 1.0f + DREAD_HEALTH_PER_LEVEL * dread;

        /// <summary>The Dread part of enemy and boss damage.</summary>
        public float dreadDamageScale => 1.0f + DREAD_DAMAGE_PER_LEVEL * dread;

        /// <summary>The Dread multiplier of gold picked up.</summary>
        public float goldScale => 1.0f + DREAD_GOLD_PER_LEVEL * dread;

        /// <summary>Damage multiplier of enemies spawned now: the run's growth x <see cref="dreadDamageScale"/>.</summary>
        public float damageScale => (1.0f + DAMAGE_PER_MINUTE * minutes) * dreadDamageScale;

        /// <summary>Experience multiplier of a kill now: (1 + PER_MINUTE t) x the Dread bonus.</summary>
        public float experienceScale => (1.0f + EXPERIENCE_PER_MINUTE * minutes) * (1.0f + DREAD_EXPERIENCE_PER_LEVEL * dread);

        /// <summary>Whether the Hollow King fight runs.</summary>
        public bool isBossActive => isBossFight;

        /// <summary>The tint tier of enemies spawned now.</summary>
        public Tint currentTint => minutes < 4.0f ? Tint.Bone : minutes < 8.0f ? Tint.Ash : minutes < 12.0f ? Tint.Blood : Tint.Void;

        private enum EventKind
        {
            Ring,
            Rush,
            ArcherLine,
            Elite,
            MiniBoss,
            LastTide,
            StopSpawns,
            BossWarning,
            Boss,
        }

        /// <summary>Who comes in a ring or a line: an archetype, how many, how far from the hero.</summary>
        private struct EnemyGroup
        {
            public Archetype archetype;
            public int count;
            public float radius;

            /// <summary>Times its kind's health (a trap ring's dead).</summary>
            public float toughness;

            public EnemyGroup(Archetype archetype, int count, float radius, float toughness = 1.0f)
            {
                this.archetype = archetype;
                this.count = count;
                this.radius = radius;
                this.toughness = toughness;
            }
        }

        /// <summary>One moment of the timeline. Build it with the factory of its kind; each fills only its own fields.</summary>
        private struct TimedEvent
        {
            public float minute;
            public EventKind kind;
            /// <summary>Rings and the archer line: who comes.</summary>
            public EnemyGroup group;
            /// <summary>Rings: a second group on its own circle (count 0 = none).</summary>
            public EnemyGroup secondGroup;
            /// <summary>Rushes: lines of Runners, and Runners per line.</summary>
            public int lines;
            public int perLine;
            /// <summary>Elites: how many.</summary>
            public int elites;
            /// <summary>Mini-bosses and the boss: which one.</summary>
            public BossKind boss;

            public static TimedEvent Ring(float minute, EnemyGroup group, EnemyGroup secondGroup = default)
            {
                return new TimedEvent { minute = minute, kind = EventKind.Ring, group = group, secondGroup = secondGroup };
            }

            public static TimedEvent ArcherLine(float minute, EnemyGroup group)
            {
                return new TimedEvent { minute = minute, kind = EventKind.ArcherLine, group = group };
            }

            public static TimedEvent Rush(float minute, int lines, int perLine)
            {
                return new TimedEvent { minute = minute, kind = EventKind.Rush, lines = lines, perLine = perLine };
            }

            public static TimedEvent Elites(float minute, int count)
            {
                return new TimedEvent { minute = minute, kind = EventKind.Elite, elites = count };
            }

            public static TimedEvent MiniBoss(float minute, BossKind boss)
            {
                return new TimedEvent { minute = minute, kind = EventKind.MiniBoss, boss = boss };
            }

            public static TimedEvent Boss(float minute, BossKind boss)
            {
                return new TimedEvent { minute = minute, kind = EventKind.Boss, boss = boss };
            }

            /// <summary>An event with no data of its own (the Last Tide, the spawn stop, the boss warning).</summary>
            public static TimedEvent Moment(float minute, EventKind kind)
            {
                return new TimedEvent { minute = minute, kind = kind };
            }
        }

        /// <summary>A rush line warned about and not sent yet.</summary>
        private struct PendingRush
        {
            public float spawnAt;
            public Vector3 direction;
            public int count;
        }

        /// <summary>From minute 8 every elite rolls one.</summary>
        private enum EliteAffix
        {
            /// <summary>Faster, and its wind-ups are shorter.</summary>
            Hasted,
            /// <summary>Its death spawns five Grave Runners (no experience).</summary>
            Splitter,
            /// <summary>A bubble absorbs damage first; frozen or critical hits break it twice as fast.</summary>
            Warded,
            Count,
        }

        public WaveDirector(Run run)
        {
            this.run = run;
            bosses = new BossManager(run);
            lanterns = new GraveLanterns(run, this);
            deadRing = new DeadRing(run);
            affixLooks = new EliteAffixLooks(run);
            eliteBeam = run.effects.Find(BossFx.ELITE_BEAM);
            BuildTimeline();
        }

        /// <summary>Starts the timeline from 0:00 (a new run).</summary>
        public void Reset()
        {
            Clear();
            nextEvent = 0;
            budget = 0.0f;
            pulseTimer = 0.0f;
            recycleTimer = 0.0f;
            lastTideUntil = -1.0f;
            isBossFight = false;
            spawnsStopped = false;
            lastDamageSource = "";
            dread = Math.Clamp(run.save != null ? run.save.selectedDread : 0, 0, SaveData.MAX_DREAD);
            lanterns.Reset();
            PlaceOpeningRing();
        }

        /// <summary>Dev (tests of later minutes): the timeline's events before this run time never fire.</summary>
        public void DevSkipTimeline(float seconds)
        {
            while (nextEvent < events.Count && events[nextEvent].minute * SECONDS_PER_MINUTE < seconds)
            {
                nextEvent++;
            }
        }

        /// <summary>Whether a ground point lies outside a ward that holds the hero (the test autopilot avoids it).</summary>
        public bool IsOutsideWard(Vector3 point)
        {
            BarrowWard bossWard = bosses.ward;
            return deadRing.ward.IsOutside(point) || (bossWard != null && bossWard.IsOutside(point));
        }

        /// <summary>Dev: lights lanterns near the hero now.</summary>
        public void DevLanterns(int count) => lanterns.DevPlaceNear(count, DEV_LANTERN_DISTANCE);

        /// <summary>Removes everything the director placed (lanterns, bosses, pending rushes).</summary>
        public void Clear()
        {
            rushes.Clear();
            splitters.Clear();
            lanterns.Clear();
            deadRing.Clear();
            bosses.Clear();
            run.abilityExecutor?.Clear();
        }

        public void Tick(float dt)
        {
            if (!devQuiet)
            {
                RunEvents();
                RunRushes();
            }
            bosses.Tick(dt);
            if (!spawnsStopped && !devQuiet)
            {
                SpendBudget(dt);
            }
            recycleTimer -= dt;
            if (recycleTimer <= 0.0f)
            {
                recycleTimer = RECYCLE_INTERVAL;
                Recycle();
            }
            lanterns.Tick(dt);
            deadRing.Tick(dt);
            affixLooks.Tick(dt);
        }

        public void OnEnemyDied(in EnemyDeath death)
        {
            if (splitters.Remove(death.enemy))
            {
                SpawnCircle(Archetype.Runner, SPLITTER_RUNNERS, death.position, SPLITTER_RADIUS, false);
            }
            bosses.OnEnemyDied(death);
            lanterns.OnEnemyDied(death);
        }

        /// <summary>A ground point kept inside the arena edge by a margin.</summary>
        public Vector3 ClampToArena(Vector3 position)
        {
            float limit = run.arenaRadius - ARENA_MARGIN;
            var flat = new Vector3(position.x, 0.0f, position.z);
            float length = flat.magnitude;
            return length > limit ? flat * (limit / length) : flat;
        }

        // -------------------------------------------------------------------------------------------------------
        // Budget spawns
        // -------------------------------------------------------------------------------------------------------

        private void SpendBudget(float dt)
        {
            float t = minutes;
            float rate = (BUDGET_BASE + BUDGET_PER_MINUTE * t + BUDGET_PER_MINUTE_SQUARED * t * t) * GetBudgetFactor() * (1.0f + DREAD_BUDGET_PER_LEVEL * dread);
            budget = MathF.Min(budget + rate * dt, rate * BUDGET_CARRY_SECONDS);
            pulseTimer -= dt;
            if (pulseTimer > 0.0f)
            {
                return;
            }
            pulseTimer = PULSE_SECONDS;
            int alive = TopUpFloor(run.enemies.count, t);
            BuyGroups(alive, GetAliveCap(t));
        }

        /// <summary>Below the floor, a free group of filler (up to 20 a pulse) comes in; returns the count alive after it.</summary>
        private int TopUpFloor(int alive, float t)
        {
            int floor = GetAliveFloor(t);
            if (alive >= floor)
            {
                return alive;
            }
            int topUp = Math.Min(FLOOR_TOP_UP_PER_PULSE, floor - alive);
            Archetype filler = run.rng.NextDouble() < FILLER_SHAMBLER_SHARE || t < FILLER_RUNNERS_FROM_MINUTE ? Archetype.Shambler : Archetype.Runner;
            SpawnGroup(filler, topUp);
            return alive + topUp;
        }

        /// <summary>Spends the budget on groups of the minute's mix until it runs short or the cap is reached.</summary>
        private void BuyGroups(int alive, int cap)
        {
            while (alive < cap)
            {
                Archetype archetype = PickArchetype();
                ArchetypeInfo info = EnemyCatalog.Info(archetype);
                int size = run.rng.Next(GROUP_MIN, GROUP_MAX_EXCLUSIVE);
                float cost = info.cost * size;
                if (budget < cost)
                {
                    return;
                }
                budget -= cost;
                SpawnGroup(archetype, size);
                alive += size;
            }
        }

        private int GetAliveFloor(float t)
        {
            if (isBossFight)
            {
                return BOSS_FIGHT_FLOOR;
            }
            return bosses.isMiniBossAlive ? MINI_BOSS_FLOOR : (int)MathF.Min(FLOOR_MAX, FLOOR_BASE + FLOOR_PER_MINUTE * t);
        }

        private int GetAliveCap(float t)
        {
            if (isBossFight)
            {
                return BOSS_FIGHT_CAP;
            }
            return bosses.isMiniBossAlive ? MINI_BOSS_CAP : (int)MathF.Min(CAP_MAX, CAP_BASE + CAP_PER_MINUTE * t);
        }

        private float GetBudgetFactor()
        {
            if (isBossFight)
            {
                return BOSS_BUDGET;
            }
            if (seconds < lastTideUntil)
            {
                return LAST_TIDE_BUDGET;
            }
            if (bosses.isMiniBossAlive)
            {
                return MINI_BOSS_BUDGET;
            }
            return 1.0f;
        }

        /// <summary>An archetype by the minute's mix (capped archetypes give way).</summary>
        public Archetype PickArchetype()
        {
            int minute = Math.Clamp((int)minutes, 0, MIX.GetLength(0) - 1);
            Span<float> weights = stackalloc float[(int)Archetype.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = GetMixWeight(minute, (Archetype)i);
            }
            int picked = WeightedPick.Pick(run.rng, weights);
            return picked >= 0 ? (Archetype)picked : Archetype.Shambler;
        }

        /// <summary>
        /// The mix share of an archetype, zero when its cap is reached (and for ranged casters while a boss fights); the
        /// pick spreads that share over the others in proportion.
        /// </summary>
        private float GetMixWeight(int minute, Archetype archetype)
        {
            float weight = MIX[minute, (int)archetype];
            if ((isBossFight || bosses.isMiniBossAlive) && (archetype == Archetype.Archer || archetype == Archetype.HexCaster))
            {
                return 0.0f;
            }
            ArchetypeInfo info = EnemyCatalog.Info(archetype);
            if (info.cap > 0 && CountArchetype(archetype) >= info.cap)
            {
                return 0.0f;
            }
            return weight;
        }

        private int CountArchetype(Archetype archetype)
        {
            int total = 0;
            for (int tint = 0; tint < (int)Tint.Count; tint++)
            {
                total += run.enemies.CountOfType(EnemyCatalog.Get(archetype, (Tint)tint));
            }
            return total;
        }

        /// <summary>Options for an enemy of the minute: the director's health and damage scales, rewarded.</summary>
        private SpawnOptions GetScaledOptions(bool rises)
        {
            return new SpawnOptions { healthScale = healthScale, damageScale = damageScale, rises = rises, isRewarded = true };
        }

        private void SpawnGroup(Archetype archetype, int count)
        {
            Vector3 center = GetSpawnPoint();
            int type = EnemyCatalog.Get(archetype, currentTint);
            SpawnOptions options = GetScaledOptions(false);
            float spread = GROUP_SPREAD_BASE + count * GROUP_SPREAD_PER_ENEMY;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spread;
                Vector3 position = ClampToArena(center + new Vector3(offset.x, 0.0f, offset.y));
                run.enemies.Spawn(type, position, in options);
            }
        }

        /// <summary>A point just outside the view, ahead of the hero half of the time while it moves.</summary>
        private Vector3 GetSpawnPoint()
        {
            HeroController hero = run.hero;
            Vector3 velocity = hero.currentVelocity;
            velocity.y = 0.0f;
            float angle;
            if (velocity.magnitude > MOVING_SPEED && run.rng.NextDouble() < FORWARD_SHARE)
            {
                angle = Ground.ToYaw(velocity) + ((float)run.rng.NextDouble() * 2.0f - 1.0f) * FORWARD_HALF_ANGLE;
            }
            else
            {
                angle = (float)run.rng.NextDouble() * FULL_TURN_DEGREES;
            }
            float distance = SPAWN_RADIUS_MIN + (float)run.rng.NextDouble() * (SPAWN_RADIUS_MAX - SPAWN_RADIUS_MIN);
            return ClampToArena(hero.position + Ground.FromYaw(angle) * distance);
        }

        /// <summary>Moves enemies left far behind to the forward spawn arc instead of spawning new ones.</summary>
        private void Recycle()
        {
            int found = run.enemies.CollectFar(run.hero.position, RECYCLE_DISTANCE, scratch);
            for (int i = 0; i < found; i++)
            {
                run.enemies.SetPosition(scratch[i], GetSpawnPoint());
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Timeline
        // -------------------------------------------------------------------------------------------------------

        private void BuildTimeline()
        {
            events.Clear();
            events.Add(TimedEvent.Ring(2.5f, new EnemyGroup(Archetype.Shambler, 40, 15.0f, RING_TOUGHNESS_EARLY)));
            events.Add(TimedEvent.Rush(1.5f, 1, 24));
            events.Add(TimedEvent.Elites(3.0f, 1));
            events.Add(TimedEvent.Elites(4.5f, 1));
            events.Add(TimedEvent.Rush(4.5f, 2, 30));
            events.Add(TimedEvent.MiniBoss(5.0f, BossKind.Gravewarden));
            events.Add(TimedEvent.Elites(6.0f, 1));
            events.Add(TimedEvent.Ring(6.5f, new EnemyGroup(Archetype.Shambler, 60, 15.0f, RING_TOUGHNESS), new EnemyGroup(Archetype.Shieldbearer, 8, 12.0f, RING_TOUGHNESS_ARMORED)));
            events.Add(TimedEvent.Elites(7.0f, 1));
            events.Add(TimedEvent.Rush(7.5f, 3, 40));
            events.Add(TimedEvent.Elites(8.0f, 2));
            events.Add(TimedEvent.Ring(8.5f, new EnemyGroup(Archetype.CinderSkull, 40, 16.0f, RING_TOUGHNESS)));
            events.Add(TimedEvent.Elites(9.0f, 1));
            events.Add(TimedEvent.Ring(9.0f, new EnemyGroup(Archetype.Shieldbearer, 50, 16.0f, RING_TOUGHNESS_ARMORED)));
            events.Add(TimedEvent.ArcherLine(9.5f, new EnemyGroup(Archetype.Archer, 24, 13.0f)));
            events.Add(TimedEvent.MiniBoss(10.0f, BossKind.HollowArchmage));
            events.Add(TimedEvent.Elites(10.5f, 1));
            events.Add(TimedEvent.Elites(11.0f, 1));
            events.Add(TimedEvent.Rush(11.0f, 4, 40));
            events.Add(TimedEvent.Elites(11.75f, 1));
            events.Add(TimedEvent.Ring(12.0f, new EnemyGroup(Archetype.Shambler, 120, 15.0f, RING_TOUGHNESS), new EnemyGroup(Archetype.CinderSkull, 30, 18.0f, RING_TOUGHNESS)));
            events.Add(TimedEvent.Elites(12.5f, 2));
            events.Add(TimedEvent.Moment(13.0f, EventKind.LastTide));
            events.Add(TimedEvent.Rush(13.0f, 6, 30));
            events.Add(TimedEvent.Elites(13.25f, 1));
            events.Add(TimedEvent.Moment(SPAWNS_STOP_SECOND / SECONDS_PER_MINUTE, EventKind.StopSpawns));
            events.Add(TimedEvent.Moment(BOSS_WARNING_SECOND / SECONDS_PER_MINUTE, EventKind.BossWarning));
            events.Add(TimedEvent.Boss(BOSS_SECOND / SECONDS_PER_MINUTE, BossKind.HollowKing));
            events.Sort((a, b) => a.minute.CompareTo(b.minute));
        }

        private void RunEvents()
        {
            while (nextEvent < events.Count && minutes >= events[nextEvent].minute)
            {
                Fire(events[nextEvent]);
                nextEvent++;
            }
        }

        private void Fire(in TimedEvent timed)
        {
            switch (timed.kind)
            {
                case EventKind.Ring:
                    StartDeadRing(timed);
                    break;
                case EventKind.Rush:
                    QueueRush(timed.lines, timed.perLine);
                    break;
                case EventKind.ArcherLine:
                    SpawnCircle(timed.group.archetype, timed.group.count, run.hero.position, timed.group.radius, true);
                    run.ui.ShowBanner("Archers take aim", BANNER_SECONDS, true);
                    break;
                case EventKind.Elite:
                    for (int i = 0; i < timed.elites; i++)
                    {
                        SpawnElite();
                    }
                    break;
                case EventKind.MiniBoss:
                    bosses.Spawn(timed.boss, PointInFront(MINI_BOSS_DISTANCE));
                    break;
                case EventKind.LastTide:
                    lastTideUntil = seconds + LAST_TIDE_SECONDS;
                    run.ui.ShowBanner("The Last Tide", LAST_TIDE_BANNER_SECONDS, true);
                    run.sounds.Play("last_tide");
                    break;
                case EventKind.StopSpawns:
                    spawnsStopped = true;
                    break;
                case EventKind.BossWarning:
                    run.ui.ShowBanner("The ground splits...", BOSS_WARNING_BANNER_SECONDS, true);
                    run.sounds.Play("bell_toll");
                    break;
                case EventKind.Boss:
                    StartBossFight(timed.boss);
                    break;
            }
        }

        /// <summary>Clears the ground in front of the hero and raises the boss there; the budget and music change.</summary>
        private void StartBossFight(BossKind boss)
        {
            Vector3 position = PointInFront(BOSS_DISTANCE);
            run.enemies.KillRegularIn(position, BOSS_CLEAR_RADIUS, NO_SOURCE, scratch);
            isBossFight = true;
            spawnsStopped = false;
            bosses.Spawn(boss, position);
            run.music.Play(MusicPlayer.Track.Boss);
        }

        private Vector3 PointInFront(float distance)
        {
            Vector3 direction = run.hero.lastMoveDirection;
            if (direction.sqrMagnitude < MIN_HEADING_SQUARED)
            {
                direction = Vector3.forward;
            }
            return ClampToArena(run.hero.position + direction.normalized * distance);
        }

        /// <summary>Twenty Shamblers rise around the hero at the start, before the budget has bought anything.</summary>
        private void PlaceOpeningRing()
        {
            Vector3 center = run.hero != null ? run.hero.position : Vector3.zero;
            int type = EnemyCatalog.Get(Archetype.Shambler, Tint.Bone);
            var options = new SpawnOptions { healthScale = 1.0f, damageScale = 1.0f, rises = true, isRewarded = true };
            for (int i = 0; i < OPENING_RING_COUNT; i++)
            {
                float angle = (float)run.rng.NextDouble() * FULL_TURN_DEGREES;
                float distance = OPENING_RING_MIN + (float)run.rng.NextDouble() * OPENING_RING_SPREAD;
                run.enemies.Spawn(type, ClampToArena(center + Ground.FromYaw(angle) * distance), in options);
            }
        }

        /// <summary>A group evenly on a circle around the hero; they walk to its center for a while before chasing.</summary>
        /// <summary>"The dead close in": the trap of tough dead and soul fire around the hero (see <see cref="DeadRing"/>); none while a boss fights.</summary>
        private void StartDeadRing(in TimedEvent timed)
        {
            if (bosses.featured != null || deadRing.isActive)
            {
                return;
            }
            Vector3 center = run.hero.position;
            deadRing.Begin(center, MathF.Max(timed.group.radius, timed.secondGroup.radius));
            SpawnRingGroup(center, timed.group);
            SpawnRingGroup(center, timed.secondGroup);
            run.sounds.Play("ring_rattle");
            run.ui.ShowBanner("The dead close in", BANNER_SECONDS, true);
        }

        private void SpawnRingGroup(Vector3 center, in EnemyGroup group)
        {
            var tint = (Tint)Math.Min((int)currentTint + RING_TINT_TIERS, (int)Tint.Count - 1);
            int type = EnemyCatalog.Get(group.archetype, tint);
            SpawnOptions options = GetScaledOptions(true);
            options.healthScale *= group.toughness;
            for (int i = 0; i < group.count; i++)
            {
                deadRing.Add(SpawnMarcher(type, center, Ground.FromYaw(i * FULL_TURN_DEGREES / group.count) * group.radius, options));
            }
        }

        /// <summary>Enemies evenly on a circle around a point (map events); they walk to it for a while before chasing.</summary>
        public void SpawnRingAround(Vector3 center, Archetype archetype, int count, float radius)
        {
            int type = EnemyCatalog.Get(archetype, currentTint);
            SpawnOptions options = GetScaledOptions(true);
            for (int i = 0; i < count; i++)
            {
                SpawnMarcher(type, center, Ground.FromYaw(i * FULL_TURN_DEGREES / count) * radius, options);
            }
        }

        private Enemy SpawnMarcher(int type, Vector3 center, Vector3 offset, in SpawnOptions options)
        {
            Enemy enemy = run.enemies.Spawn(type, ClampToArena(center + offset), in options);
            if (enemy.isValid)
            {
                run.enemies.March(enemy.row, center, true, RING_MARCH_SPEED, RING_MARCH_SECONDS);
            }
            return enemy;
        }

        private void SpawnCircle(Archetype archetype, int count, Vector3 center, float radius, bool rewarded)
        {
            int type = EnemyCatalog.Get(archetype, currentTint);
            SpawnOptions options = GetScaledOptions(true);
            options.isRewarded = rewarded;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = ClampToArena(center + Ground.FromYaw(i * FULL_TURN_DEGREES / count) * radius);
                run.enemies.Spawn(type, position, in options);
            }
        }

        /// <summary>Warns, then sends lines of Runners straight across the field.</summary>
        private void QueueRush(int lines, int perLine)
        {
            for (int line = 0; line < lines; line++)
            {
                Vector3 from = Ground.FromYaw((float)run.rng.NextDouble() * FULL_TURN_DEGREES);
                float warning = RUSH_WARNING + line * RUSH_LINE_DELAY;
                rushes.Add(new PendingRush { spawnAt = seconds + warning, direction = -from, count = perLine });
                run.ui.ShowEdgeWarning(from, warning);
            }
            run.sounds.Play("rush_rumble");
        }

        private void RunRushes()
        {
            for (int i = rushes.Count - 1; i >= 0; i--)
            {
                if (rushes[i].spawnAt > seconds)
                {
                    continue;
                }
                PendingRush rush = rushes[i];
                rushes.RemoveAt(i);
                SendRush(rush);
            }
        }

        /// <summary>A line of Runners across the rush's path behind the hero, marching through.</summary>
        private void SendRush(in PendingRush rush)
        {
            Vector3 start = run.hero.position - rush.direction * RUSH_DISTANCE;
            var side = new Vector3(rush.direction.z, 0.0f, -rush.direction.x);
            int type = EnemyCatalog.Get(Archetype.Runner, currentTint);
            SpawnOptions options = GetScaledOptions(false);
            for (int k = 0; k < rush.count; k++)
            {
                float offset = (k / (float)Math.Max(rush.count - 1, 1) - 0.5f) * RUSH_WIDTH;
                Vector3 position = ClampToArena(start + side * offset);
                Enemy enemy = run.enemies.Spawn(type, position, in options);
                run.enemies.March(enemy.row, rush.direction, false, RUSH_SPEED, RUSH_LENGTH / RUSH_SPEED);
            }
        }

        private void SpawnElite()
        {
            float angle = (float)run.rng.NextDouble() * FULL_TURN_DEGREES;
            SpawnEliteAt(ClampToArena(run.hero.position + Ground.FromYaw(angle) * ELITE_DISTANCE));
        }

        /// <summary>An elite of the minute's mix at a point, with a beam, a horn and (from minute 8) an affix.</summary>
        public void SpawnEliteAt(Vector3 position)
        {
            Archetype archetype = PickArchetype();
            SpawnOptions options = GetScaledOptions(true);
            options.isElite = true;
            Enemy elite = run.enemies.Spawn(EnemyCatalog.Get(archetype, currentTint), position, in options);
            if (!elite.isValid)
            {
                return;
            }
            run.effects.Emit(eliteBeam, position, 1);
            run.sounds.Play("elite_horn");
            if (minutes >= AFFIX_FROM_MINUTE)
            {
                GiveAffix(elite);
            }
        }

        private void GiveAffix(Enemy elite)
        {
            var affix = (EliteAffix)run.rng.Next((int)EliteAffix.Count);
            switch (affix)
            {
                case EliteAffix.Hasted:
                    run.enemies.Haste(elite.row, HASTE_SPEED, HASTE_WINDUP);
                    break;
                case EliteAffix.Splitter:
                    splitters.Add(elite);
                    break;
                case EliteAffix.Warded:
                    run.enemies.AddBarrier(elite.row, WARD_BARRIER_SHARE * run.enemies.GetMaxHealth(elite.row));
                    break;
            }
        }
    }
}

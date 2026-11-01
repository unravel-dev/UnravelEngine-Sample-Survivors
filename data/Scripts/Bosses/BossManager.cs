using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The run's bosses.</summary>
    public enum BossKind
    {
        Gravewarden,
        HollowArchmage,
        HollowKing,
        Count,
    }

    /// <summary>
    /// Spawns and steps the mini-bosses and the Hollow King: entities with full animation driving a proxy row of the
    /// enemy world, so every weapon hits them like any enemy (see <see cref="Boss"/>). Owns what outlives one boss:
    /// the pending hazards, the ward around the fight, Death's Mark, the boss bar and off-screen marker, the camera zoom of
    /// a boss that has one, the drops and save flags of a kill, the +1 reroll per mini-boss (at most +2 per run) and the
    /// King's victory sequence. Main thread; the director calls <see cref="Tick"/> once per frame while the run plays,
    /// before the enemy world steps.
    /// </summary>
    public sealed class BossManager
    {
        private const int MAX_REROLLS_FROM_BOSSES = 2;
        private const float BANNER_SECONDS = 2.5f;
        private const int DEATH_BONE_FRAGMENTS = 120;
        private const float DEATH_BURST_HEIGHT = 2.0f;
        private const float DEATH_BURST_SIZE = 6.0f;
        private const float DEATH_BURST_SECONDS = 1.0f;
        private const float DEATH_SHAKE = 0.5f;
        private const float DEATH_SHAKE_SECONDS = 0.5f;
        /// <summary>A mini-boss's chest also holds a Lodestone of this value.</summary>
        private const float LODESTONE_VALUE = 1.0f;
        /// <summary>A beaten mini-boss leaves roasts for the wounds of the fight.</summary>
        private const int DEATH_ROASTS = 2;
        /// <summary>A mini-boss falls in slow motion for this long (real seconds).</summary>
        private const float DEATH_SLOW_SCALE = 0.25f;
        private const float DEATH_SLOW_SECONDS = 0.8f;

        private readonly Run run;
        private readonly List<Boss> bosses = new List<Boss>(4);
        private BossFx fx;
        private BossHazards hazards;
        private float slowMotionLeft;
        private KingVictory victory;
        private Vector3[] obstacles;
        private int rerollsGranted;
        private bool isBarShown;

        public BossManager(Run run)
        {
            this.run = run;
            mark = new DeathMark(run);
        }

        /// <summary>The ward around the fight; null before the first boss rises (see <see cref="EnsureShared"/>).</summary>
        public BarrowWard ward { get; private set; }

        /// <summary>What lands after the boss moved on; null before the first boss rises.</summary>
        public BossHazards pending => hazards;

        /// <summary>What boss hits do to the hero (see <see cref="DeathMark"/>).</summary>
        public DeathMark mark { get; }

        /// <summary>Whether a mini-boss is alive (the director halves its budget meanwhile).</summary>
        public bool isMiniBossAlive
        {
            get
            {
                for (int i = 0; i < bosses.Count; i++)
                {
                    if (bosses[i].isMiniBoss && bosses[i].isAlive)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>The newest living boss (the test autopilot dances around it), or null.</summary>
        public Boss featured
        {
            get
            {
                for (int i = bosses.Count - 1; i >= 0; i--)
                {
                    if (bosses[i].isAlive)
                    {
                        return bosses[i];
                    }
                }
                return null;
            }
        }

        /// <summary>The featured boss with its health, for test reports; "-" with none.</summary>
        public string featuredReport => featured != null ? $"{featured.kind}:{featured.healthFraction:0.00}" : "-";

        /// <summary>Whether the King's victory sequence is playing.</summary>
        public bool isVictoryPlaying => victory != null && victory.isPlaying;

        /// <summary>Raises a boss at a ground position (the director's timeline: 5:00, 10:00, 14:00).</summary>
        public void Spawn(BossKind kind, Vector3 position)
        {
            EnsureShared();
            if (EnemyCatalog.bossTypes[(int)kind] < 0)
            {
                Log.Error($"BossManager: {kind} has no proxy type; the boss is skipped.");
                return;
            }
            Boss boss = Create(kind);
            bosses.Add(boss);
            boss.Spawn(position);
            if (!ward.isUp)
            {
                run.mapEvents.Clear();
                run.director.deadRing.Clear();
                ward.Raise((run.hero.position + boss.groundPosition) * 0.5f, boss.arenaRadius, boss.arenaColor, true);
            }
            if (boss.cameraZoom != Boss.NO_ZOOM)
            {
                run.cameraRig.zoom = boss.cameraZoom;
            }
        }

        public void Tick(float dt)
        {
            if (fx == null)
            {
                return;
            }
            hazards.Tick(dt);
            ward.Tick(dt);
            mark.Tick(dt);
            TickSlowMotion();
            for (int i = bosses.Count - 1; i >= 0; i--)
            {
                Boss boss = bosses[i];
                boss.Tick(dt);
                if (boss.life == BossLife.Gone)
                {
                    bosses.RemoveAt(i);
                }
            }
            victory.Tick(Time.unscaledDeltaTime);
            UpdateBarAndMarkers();
        }

        /// <summary>Removes every boss and pending effect (new run).</summary>
        public void Clear()
        {
            foreach (Boss boss in bosses)
            {
                boss.Remove();
            }
            bosses.Clear();
            hazards?.Clear();
            ward?.Lower();
            victory?.Cancel();
            mark.ResetRun();
            slowMotionLeft = 0.0f;
            rerollsGranted = 0;
            if (fx != null)
            {
                fx.dangerColor = Tuning.DANGER_COLOR;
            }
            if (isBarShown)
            {
                isBarShown = false;
                run.ui?.HideBossBar();
            }
            if (run.cameraRig != null)
            {
                run.cameraRig.zoom = Boss.NO_ZOOM;
            }
        }

        /// <summary>Every death of the run; the ones of boss rows start the boss's death and pay its drops.</summary>
        public void OnEnemyDied(in EnemyDeath death)
        {
            for (int i = 0; i < bosses.Count; i++)
            {
                Boss boss = bosses[i];
                if (boss.life != BossLife.Fighting || !boss.proxy.Equals(death.enemy))
                {
                    continue;
                }
                Vector3 at = boss.groundPosition;
                boss.OnProxyDied();
                run.save?.RecordBossDefeated(boss.defeatId);
                if (featured == null)
                {
                    ward.Lower();
                    mark.Clear();
                }
                if (boss.isMiniBoss)
                {
                    OnMiniBossKilled(boss, at);
                }
                else
                {
                    victory.Begin(at, boss.goldReward);
                }
                return;
            }
        }

        private Boss Create(BossKind kind) => kind switch
        {
            BossKind.Gravewarden => new Gravewarden(run, fx, hazards, obstacles),
            BossKind.HollowArchmage => new HollowArchmage(run, fx, hazards, obstacles),
            BossKind.HollowKing => new HollowKing(run, fx, hazards, obstacles),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a boss."),
        };

        /// <summary>Effects, sounds, hazards and the victory sequence are shared by every boss of the run.</summary>
        private void EnsureShared()
        {
            if (fx != null)
            {
                return;
            }
            fx = new BossFx(run.effectsRoot);
            hazards = new BossHazards(run, fx);
            ward = new BarrowWard(run);
            victory = new KingVictory(run, fx);
            obstacles = CollectObstacles();
        }

        /// <summary>Scenery circles (x, z, radius) bosses walk around, like the enemy world's.</summary>
        private static Vector3[] CollectObstacles()
        {
            Entity[] marked = Scene.FindEntitiesWithComponent<ArenaObstacle>();
            var circles = new Vector3[marked.Length];
            for (int i = 0; i < marked.Length; i++)
            {
                Vector3 position = marked[i].transform.position;
                circles[i] = new Vector3(position.x, position.z, marked[i].GetComponent<ArenaObstacle>().radius);
            }
            return circles;
        }

        /// <summary>A beaten mini-boss's slow motion ends after real seconds.</summary>
        private void TickSlowMotion()
        {
            if (slowMotionLeft <= 0.0f)
            {
                return;
            }
            slowMotionLeft -= Time.unscaledDeltaTime;
            if (slowMotionLeft <= 0.0f && run.phase == RunPhase.Playing)
            {
                run.clock.EndSlowMotion();
            }
        }

        /// <summary>
        /// Mini-boss drops: a chest of 3 items (5 with luck), the boss's gold, a Lodestone, roasts and +1 reroll (at most
        /// +2 per run), with a burst, a moment of slow motion and a banner.
        /// </summary>
        private void OnMiniBossKilled(Boss boss, Vector3 at)
        {
            PickupWorld pickups = run.pickups;
            pickups.Spawn(PickupKind.Chest, at, run.drops.RollChestItems(Drops.ChestSource.MiniBoss));
            pickups.Spawn(PickupKind.Gold, at, boss.goldReward);
            pickups.Spawn(PickupKind.Magnet, at, LODESTONE_VALUE);
            for (int i = 0; i < DEATH_ROASTS; i++)
            {
                pickups.Spawn(PickupKind.Food, at, 1.0f);
            }
            bool isRerollGranted = rerollsGranted < MAX_REROLLS_FROM_BOSSES;
            if (isRerollGranted)
            {
                rerollsGranted++;
                run.stats.AddFlat(StatId.Reroll, 1.0f);
            }
            Vector3 chest = at + new Vector3(0.0f, DEATH_BURST_HEIGHT, 0.0f);
            fx.Burst(fx.bossBoneBurst, chest, DEATH_BONE_FRAGMENTS);
            fx.Burst(fx.bossDeathBurst, chest, 1, Color.white, DEATH_BURST_SIZE, DEATH_BURST_SECONDS);
            fx.Burst(fx.eliteBeam, at, 1);
            run.sounds.PlayAt("boss_death", at);
            run.sounds.PlayAt("boss_chest_drop", at);
            run.clock.SetSlowMotion(DEATH_SLOW_SCALE);
            slowMotionLeft = DEATH_SLOW_SECONDS;
            run.feedback.Shake(DEATH_SHAKE, DEATH_SHAKE_SECONDS);
            run.ui.FlashScreen(false);
            string banner = isRerollGranted ? $"{boss.displayName} falls  +1 Reroll" : $"{boss.displayName} falls";
            run.ui.ShowBanner(banner, BANNER_SECONDS);
        }

        /// <summary>The bar follows the King when he is up, else the newest mini-boss; every live boss gets a marker.</summary>
        private void UpdateBarAndMarkers()
        {
            Boss featured = null;
            for (int i = 0; i < bosses.Count; i++)
            {
                Boss boss = bosses[i];
                if (!boss.isAlive)
                {
                    continue;
                }
                run.ui.ShowMarker(boss.groundPosition, MarkerKind.Boss);
                // Bosses are listed in spawn order: a later boss takes the bar unless the King already holds it.
                if (featured == null || featured.isMiniBoss)
                {
                    featured = boss;
                }
            }
            if (featured != null && !isVictoryPlaying)
            {
                isBarShown = true;
                string stackTitle = mark.stackTitle;
                run.ui.SetBossBar(featured.displayName, stackTitle.Length > 0 ? stackTitle : featured.title, featured.healthFraction, featured.phaseMarks);
                return;
            }
            if (isBarShown)
            {
                isBarShown = false;
                run.ui.HideBossBar();
            }
        }
    }
}

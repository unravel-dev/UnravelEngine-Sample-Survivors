using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The run director and the game's composition root: builds every system when play starts, runs the menus, starts
    /// and ends runs, and steps the systems in a fixed order each frame (hero, director, enemies, projectiles, pickups,
    /// enemy abilities, weapons, map events, HUD), then draws them. It owns the phase machine: level-ups and chests open
    /// only from <see cref="RunPhase.Playing"/> (one that arrives while a menu holds the run waits its turn), and game
    /// time follows the phase through the <see cref="GameClock"/>. Systems report what happened through C# events and
    /// hand work to each other through interfaces (enemy commands, projectile handlers, the hero as a target); this
    /// class wires them. Other scripts reach the run through <see cref="current"/>. The per-frame steps live in
    /// Run.Frame.cs and the test hooks in Run.Dev.cs. Main thread.
    /// </summary>
    [ScriptSourceFile]
    [AutoStaticsCleanup]
    public partial class Run : ScriptComponent
    {
        private const int ENEMY_CAPACITY = 3400;
        private const int PROJECTILE_CAPACITY = 4096;
        private const int PICKUP_CAPACITY = 2048;
        private const int GEM_CAP = 600;
        private const int SCRATCH_CAPACITY = 512;
        /// <summary>The title runs at normal speed whatever the test speed of runs.</summary>
        private const float MENU_SPEED = 1.0f;
        private const float LEVEL_UP_INVULNERABLE = 0.8f;
        private const float LEVEL_UP_PUSH_RADIUS = 4.0f;
        private const float LEVEL_UP_PUSH_SPEED = 30.0f;
        private const float LEVEL_UP_RING_HEIGHT = 0.1f;
        /// <summary>Gold paid for skipping every card of a level-up.</summary>
        private const int SKIPPED_CARD_GOLD = 5;
        /// <summary>Gold per chest item, rolled between these (inclusive).</summary>
        private const int CHEST_GOLD_MIN = 15;
        private const int CHEST_GOLD_MAX = 30;
        private const float VICTORY_SLOW_MOTION = 0.25f;
        private const float DEFEAT_SLOW_MOTION = 0.5f;
        /// <summary>Real seconds between the end of a run and the results screen.</summary>
        private const float RESULTS_DELAY = 2.2f;
        private const float NO_END_TIMER = -1.0f;

        [Header("Scene")]
        [Tooltip("The gameplay camera.")]
        public Entity cameraEntity;
        [Tooltip("Parent of every particle effect the game fires by name.")]
        public Entity effectsRoot;
        [Tooltip("Where the hero starts a run.")]
        public Entity heroSpawn;
        [Tooltip("Parent of the UI documents.")]
        public Entity uiRoot;
        [Tooltip("Ambient effects that travel with the hero (fog, fireflies); kept on the hero's ground position.")]
        public Entity followEffects;
        [Tooltip("Health ring under the hero's feet (a child of the follow root), tinted by health.")]
        public Entity heroRing;
        [Tooltip("Warm light over the hero (a child of the follow root), so the hero reads in the crowd.")]
        public Entity heroLight;
        [Tooltip("On-screen stick and skill button for touch screens; shown while a run plays.")]
        public Entity touchControls;
        [Tooltip("The Knight's body.")]
        public Prefab knightPrefab;
        [Tooltip("The Barbarian's body.")]
        public Prefab barbarianPrefab;
        [Tooltip("The Mage's body.")]
        public Prefab magePrefab;
        [Tooltip("The Rogue's body.")]
        public Prefab roguePrefab;

        [Header("Menus")]
        [Tooltip("Parent of the four display heroes of the title and hero select, children named like HeroClass.")]
        public Entity menuStage;
        [Tooltip("Light that follows the hero focused on hero select.")]
        public Entity menuSpotlight;

        [Header("Rules")]
        [Tooltip("Every hero is playable without unlocking.")]
        public bool unlockAllHeroes = true;
        [Tooltip("Radius (m) of the playable ground.")]
        public float arenaRadius = 95.0f;
        [Tooltip("Seed of the run's own dice (waves, drops, offers, chests, events); 0 picks one from the clock. Crits, blocks and dodges roll the engine's Random and do not repeat.")]
        public int seed = 0;

        [Header("Development")]
        [Tooltip("Starts a run with the class below right away, skipping the menus (tests and capture).")]
        public bool autoStart = false;
        [Tooltip("The class an automatic start plays.")]
        public HeroClass autoStartClass = HeroClass.Knight;
        [Tooltip("Run time (s) an automatic start begins at, to test later minutes.")]
        public float autoStartTime = 0.0f;
        [Tooltip("Level-ups take a random card and chests open without their reel (unattended tests).")]
        public bool devAutoPick = false;
        [Tooltip("The hero walks by itself: away from nearby enemies while circling the arena (unattended tests).")]
        public bool devAutopilot = false;
        [Tooltip("Game speed during runs (unattended tests run faster than real time).")]
        public float devSpeed = 1.0f;

        /// <summary>Hero hits as they land (passive traits, weapons that react to crits); cleared each run.</summary>
        public readonly CombatEvents combatEvents = new CombatEvents();
        private readonly int[] scratchRows = new int[SCRATCH_CAPACITY];
        /// <summary>Item counts of chests collected while a menu held the run.</summary>
        private readonly Queue<int> pendingChests = new Queue<int>();
        private ArenaGround arena = ArenaGround.Empty();
        /// <summary>Light pillars over the chests on the ground (created with the first run).</summary>
        private ChestBeacons chestBeacons;
        private MenuStage stage;
        private HeroPresence presence;
        private TouchControls touch;
        private RunMarkers markers;
        private StatusFlames statusFlames;
        private Autopilot autopilot;
        private Progression progression;
        private Entity heroEntity;
        private float endTimer = NO_END_TIMER;
        private int levelUpRing = Effects.UNRESOLVED;
        /// <summary>Set once OnStart completed: a start that threw (its error already logged) leaves the run idle
        /// instead of failing again every frame on half-built systems.</summary>
        private bool isReady;

        /// <summary>The run being played, or null outside play mode.</summary>
        public static Run current { get; private set; }

        /// <summary>What the run did; the one source of its clock, level and gold.</summary>
        public RunRecord record { get; private set; } = new RunRecord();
        public PlayerStats stats { get; private set; }
        /// <summary>The only writer of the engine's time scale.</summary>
        public GameClock clock { get; private set; }
        public EnemyWorld enemies { get; private set; }
        public ProjectileWorld projectiles { get; private set; }
        public PickupWorld pickups { get; private set; }
        /// <summary>What enemies drop and what pickups do.</summary>
        public Drops drops { get; private set; }
        public Effects effects { get; private set; }
        public Sounds sounds { get; private set; }
        public MusicPlayer music { get; private set; }
        public Feedback feedback { get; private set; }
        public CameraRig cameraRig { get; private set; }
        public CameraComponent gameCamera { get; private set; }
        public DamageNumbers damageNumbers { get; private set; }
        public HeroController hero { get; private set; }
        public Loadout loadout { get; private set; }
        public WaveDirector director { get; private set; }
        public EnemyAbilityExecutor abilityExecutor { get; private set; }
        /// <summary>The optional challenges of the moor (created with the first run).</summary>
        public MapEvents mapEvents { get; private set; }
        public GameUI ui { get; private set; }
        public SaveData save { get; private set; }
        /// <summary>The run's dice, seeded by <see cref="seed"/>.</summary>
        public System.Random rng { get; private set; }
        public HeroClass heroClass { get; private set; }
        public RunPhase phase { get; private set; } = RunPhase.Menu;
        /// <summary>Seconds of the run so far (game time).</summary>
        public float runTime { get => record.timeSurvived; private set => record.timeSurvived = value; }
        /// <summary>The hero's level.</summary>
        public int level { get => record.levelReached; private set => record.levelReached = value; }
        /// <summary>Gold picked up this run (banked when it ends).</summary>
        public int gold { get => record.goldEarned; private set => record.goldEarned = value; }
        /// <summary>Run time in minutes, the unit the balance formulas use.</summary>
        public float minutes => runTime / 60.0f;
        /// <summary>The XP bar's fill, 0..1.</summary>
        public float experienceFraction => progression.experienceFraction;

        /// <summary>Experience needed to go from <paramref name="currentLevel"/> to the next.</summary>
        public static float GetExperienceToNext(int currentLevel) => Progression.GetExperienceToNext(currentLevel);

        public override void OnStart()
        {
            current = this;
            rng = new System.Random(seed != 0 ? seed : Environment.TickCount);
            save = SaveData.Load();
            stats = new PlayerStats();
            clock = new GameClock();
            progression = new Progression(record);
            BuildPresentation();
            BuildWorld();
            ui = new GameUI(this, uiRoot);
            damageNumbers = ui.damageNumbers;
            ui.heroPreviewChanged += stage.Focus;
            ShowTitle();
            if (autoStart)
            {
                StartRun(autoStartClass);
                runTime = autoStartTime;
            }
            isReady = true;
        }

        public override void OnDestroy()
        {
            loadout?.Dispose();
            mapEvents?.Clear();
            chestBeacons?.Reset();
            enemies?.Dispose();
            pickups?.DisposeLooks();
            ui?.Dispose();
            if (ReferenceEquals(current, this))
            {
                current = null;
            }
            GameClock.ResetEngineScale();
        }

        public override void OnUpdate()
        {
            if (!isReady)
            {
                return;
            }
            float unscaledDt = Time.unscaledDeltaTime;
            clock.Update(unscaledDt);
            feedback.Update(unscaledDt);
            music.Tick(this, unscaledDt);
            switch (phase)
            {
                case RunPhase.Playing:
                    StepPlaying(Time.deltaTime);
                    break;
                case RunPhase.Ended:
                    StepEnded(Time.deltaTime, unscaledDt);
                    break;
            }
            UpdateView(unscaledDt);
            touch.SetShown(phase == RunPhase.Playing && !devAutopilot, save.settings);
            ui.Update(unscaledDt);
        }

        public override void OnLateUpdate()
        {
            if (!isReady)
            {
                return;
            }
            pickups.PulseGlow(Time.time);
            enemies.Draw();
            projectiles.Draw();
            pickups.Draw();
            mapEvents?.Draw();
        }

        /// <summary>Whether ground is clear of the arena's obstacles within a radius of a point.</summary>
        public bool IsOpenGround(Vector3 position, float radius) => arena.IsOpenGround(position, radius);

        /// <summary>Effects, sounds, music, game feel, the camera and the scene pieces of the menus and the hero.</summary>
        private void BuildPresentation()
        {
            effects = new Effects();
            effects.Collect(effectsRoot);
            levelUpRing = effects.Find("LevelUpRing");
            sounds = new Sounds();
            SoundCatalog.Register(sounds);
            music = new MusicPlayer();
            feedback = new Feedback(effects, sounds, clock);
            gameCamera = cameraEntity.IsValid() ? cameraEntity.GetComponent<CameraComponent>() : null;
            cameraRig = new CameraRig(cameraEntity);
            feedback.SetCamera(cameraRig);
            stage = new MenuStage(menuStage, menuSpotlight);
            presence = new HeroPresence(followEffects, heroRing, heroLight);
            touch = new TouchControls(touchControls);
            markers = new RunMarkers(this);
        }

        /// <summary>The worlds of rows (enemies, projectiles, pickups) on the arena's ground and the systems that feed them.</summary>
        private void BuildWorld()
        {
            arena = ArenaGround.Collect();
            enemies = new EnemyWorld(ENEMY_CAPACITY) { arenaRadius = arenaRadius };
            EnemyCatalog.Register(enemies);
            enemies.SetObstacles(arena.obstacles);
            enemies.onDeath = OnEnemyDeath;
            projectiles = new ProjectileWorld(PROJECTILE_CAPACITY) { enemies = enemies, effects = effects };
            ProjectileCatalog.Register(projectiles);
            projectiles.heroDamagedBy += OnProjectileHurtHero;
            pickups = new PickupWorld(PICKUP_CAPACITY, GEM_CAP);
            PickupCatalog.Register(pickups);
            pickups.onCollect = OnPickup;
            drops = new Drops(this);
            abilityExecutor = new EnemyAbilityExecutor(this);
            enemies.commandSink = abilityExecutor;
            director = new WaveDirector(this);
            statusFlames = new StatusFlames(effects, enemies);
            autopilot = new Autopilot(this);
        }

        /// <summary>Back to the title: leaves any run and shows the menu stage at normal speed.</summary>
        public void ShowTitle()
        {
            if (phase != RunPhase.Menu || hero != null)
            {
                LeaveRun();
            }
            phase = RunPhase.Menu;
            clock.Restart(MENU_SPEED);
            presence.SetShown(false);
            stage.Show(cameraRig);
            ui.ShowTitle();
        }

        /// <summary>Whether the save lets the player pick a class.</summary>
        public bool IsUnlocked(HeroClass heroClass) => unlockAllHeroes || save.IsHeroUnlocked(heroClass);

        /// <summary>Starts a run with a class: spawns its body, applies class and meta stats, gives the start weapon.</summary>
        public void StartRun(HeroClass chosen)
        {
            ClearWorld();
            ResetRunState(chosen);
            stage.Hide();
            presence.SetShown(true);
            cameraRig.FrameGameplay();
            SpawnHero(chosen);
            hero.BeginRun(stats, record, enemies);
            projectiles.heroTarget = hero;
            CreateRunSystems(chosen);
            cameraRig.Snap(hero.position);
            phase = RunPhase.Playing;
            clock.Restart(devSpeed);
            ui.ShowHud();
            sounds.Play("run_start");
            music.Play(MusicPlayer.Track.Battle);
        }

        /// <summary>Leaves the run for the title (pause menu "Abandon").</summary>
        public void AbandonRun()
        {
            EndRun(false, "Abandoned");
            ui.HideAll();
            ShowTitle();
        }

        /// <summary>The body prefab of a class.</summary>
        public Prefab GetHeroPrefab(HeroClass chosen) => chosen switch
        {
            HeroClass.Knight => knightPrefab,
            HeroClass.Barbarian => barbarianPrefab,
            HeroClass.Mage => magePrefab,
            HeroClass.Rogue => roguePrefab,
            _ => throw new ArgumentOutOfRangeException(nameof(chosen)),
        };

        /// <summary>Zero time, level 1, no experience, gold or queued menus; the class and the Armory ranks set the stats.</summary>
        private void ResetRunState(HeroClass chosen)
        {
            heroClass = chosen;
            record.Reset();
            progression.Reset();
            pendingChests.Clear();
            endTimer = NO_END_TIMER;
            stats = new PlayerStats();
            HeroCatalog.ApplyClassStats(chosen, stats);
            MetaShop.ApplyRanks(save, stats);
        }

        /// <summary>A fresh loadout, chest beacons, map events, drop timers and waves for a new run.</summary>
        private void CreateRunSystems(HeroClass chosen)
        {
            loadout?.Dispose();
            combatEvents.Clear();
            loadout = new Loadout(this, chosen);
            chestBeacons ??= new ChestBeacons(this);
            chestBeacons.Reset();
            mapEvents ??= new MapEvents(this);
            mapEvents.Reset();
            drops.Reset();
            director.Reset();
        }

        private void SpawnHero(HeroClass chosen)
        {
            if (heroEntity.IsValid())
            {
                Scene.DestroyEntity(heroEntity);
            }
            Vector3 start = heroSpawn.IsValid() ? heroSpawn.transform.position : Vector3.zero;
            Prefab prefab = GetHeroPrefab(chosen);
            heroEntity = AssetChecks.IsLoaded(prefab) ? Scene.Instantiate(prefab, start) : Entity.Invalid;
            hero = heroEntity.IsValid() ? heroEntity.GetComponent<HeroController>() : null;
            if (hero == null)
            {
                throw new InvalidOperationException($"The {chosen} prefab has no HeroController.");
            }
            hero.damaged += OnHeroDamaged;
            hero.died += OnHeroDied;
            hero.revived += OnHeroRevived;
        }

        /// <summary>Removes the last run's hero and everything it fought (back to the menus).</summary>
        private void LeaveRun()
        {
            ClearWorld();
            if (heroEntity.IsValid())
            {
                Scene.DestroyEntity(heroEntity);
            }
            heroEntity = Entity.Invalid;
            hero = null;
            loadout?.Dispose();
            loadout = null;
        }

        private void ClearWorld()
        {
            enemies.Clear();
            projectiles.Clear(Team.Hero);
            projectiles.Clear(Team.Enemy);
            pickups.Clear();
            effects.ResetAll();
            director?.Clear();
            mapEvents?.Clear();
        }

        /// <summary>Adds experience (Growth applied by the caller); every level gained queues a level-up.</summary>
        public void AddExperience(float amount) => progression.AddExperience(amount);

        /// <summary>Adds gold picked up this run (Greed applied by the caller).</summary>
        public void AddGold(int amount) => gold += amount;

        /// <summary>
        /// Opens a chest: rolls its items, applies them and shows the reel. A chest collected while a menu holds the run
        /// waits for it; one collected after the run ended is lost with the run.
        /// </summary>
        public void OpenChest(int itemCount, Vector3 position)
        {
            switch (phase)
            {
                case RunPhase.Playing:
                    ShowChest(itemCount);
                    break;
                case RunPhase.LevelUp:
                case RunPhase.Chest:
                case RunPhase.Paused:
                    pendingChests.Enqueue(itemCount);
                    break;
            }
        }

        /// <summary>Holds the run on the pause menu (only from play).</summary>
        public void Pause()
        {
            if (phase != RunPhase.Playing)
            {
                return;
            }
            phase = RunPhase.Paused;
            clock.Freeze();
            ui.ShowPause();
        }

        /// <summary>Back to play from the pause menu, a level-up or a chest, with a short ramp of time from slow to full.</summary>
        public void Resume()
        {
            if (phase != RunPhase.Paused && phase != RunPhase.LevelUp && phase != RunPhase.Chest)
            {
                return;
            }
            phase = RunPhase.Playing;
            ui.ShowHud();
            clock.ResumeWithRamp();
        }

        /// <summary>Opens a menu the run queued while it was busy: chests first, level-ups once no hit stop holds time.</summary>
        private void OpenQueuedMenu()
        {
            if (pendingChests.Count > 0)
            {
                ShowChest(pendingChests.Dequeue());
            }
            else if (!clock.isStopping && progression.TryTakeLevelUp())
            {
                OpenLevelUp();
            }
        }

        /// <summary>Holds the run for a level-up's card pick; the test autopilot picks at once.</summary>
        private void OpenLevelUp()
        {
            phase = RunPhase.LevelUp;
            clock.Freeze();
            enemies.PushAway(hero.position, LEVEL_UP_PUSH_RADIUS, LEVEL_UP_PUSH_SPEED, scratchRows);
            effects.Emit(levelUpRing, hero.position + new Vector3(0.0f, LEVEL_UP_RING_HEIGHT, 0.0f), 1);
            sounds.Play("level_up");
            List<UpgradeOffer> offers = loadout.RollLevelUp(rng);
            if (devAutoPick)
            {
                OnLevelUpChosen(autopilot.PickCard(offers));
                return;
            }
            ui.ShowLevelUp(offers, OnLevelUpChosen);
        }

        /// <summary>Applies a picked card (null = skipped, paid in gold), then opens the next pending level-up or resumes.</summary>
        private void OnLevelUpChosen(UpgradeOffer offer)
        {
            if (phase != RunPhase.LevelUp)
            {
                return;
            }
            if (offer != null)
            {
                loadout.Apply(offer);
                sounds.Play("card_pick");
            }
            else
            {
                AddGold(SKIPPED_CARD_GOLD);
            }
            if (progression.TryTakeLevelUp())
            {
                OpenLevelUp();
                return;
            }
            Resume();
            hero.SetInvulnerable(LEVEL_UP_INVULNERABLE);
        }

        /// <summary>Holds the run while a chest of <paramref name="itemCount"/> items rolls, pays and shows its reel.</summary>
        private void ShowChest(int itemCount)
        {
            phase = RunPhase.Chest;
            clock.Freeze();
            record.chestsOpened++;
            List<UpgradeOffer> items = loadout.RollChest(rng, itemCount);
            foreach (UpgradeOffer item in items)
            {
                loadout.Apply(item);
            }
            int chestGold = rng.Next(CHEST_GOLD_MIN, CHEST_GOLD_MAX + 1) * Math.Max(1, itemCount);
            AddGold(chestGold);
            sounds.Play("chest_open");
            if (devAutoPick)
            {
                Resume();
                return;
            }
            ui.ShowChest(items, chestGold, Resume);
        }

        /// <summary>The last boss fell: slow motion, then the victory screen.</summary>
        public void OnBossDefeated() => EndRun(true, "");

        /// <summary>Ends the run from any phase but the menus: drops queued menus, saves, slows time and plays the stinger.</summary>
        private void EndRun(bool isVictory, string cause)
        {
            if (phase == RunPhase.Ended || phase == RunPhase.Menu)
            {
                return;
            }
            phase = RunPhase.Ended;
            progression.DropPending();
            pendingChests.Clear();
            mapEvents?.Clear();
            record.isVictory = isVictory;
            record.deathCause = cause;
            save.OnRunEnded(record, heroClass);
            save.Save();
            endTimer = RESULTS_DELAY;
            clock.SetSlowMotion(isVictory ? VICTORY_SLOW_MOTION : DEFEAT_SLOW_MOTION);
            music.Play(isVictory ? MusicPlayer.Track.Victory : MusicPlayer.Track.Defeat);
        }
    }
}

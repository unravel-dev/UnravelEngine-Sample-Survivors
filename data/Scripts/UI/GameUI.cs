using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>What an off-screen marker points at (see <see cref="GameUI.ShowMarker"/>).</summary>
    public enum MarkerKind
    {
        Elite,
        Chest,
        Boss,
        Treasure,
        Shrine,
        Robber,
        Pyres,
        Moonwell,
        Food,
        Magnet,
        Bell,
        Frost,
    }

    /// <summary>
    /// Every screen of the game over the UI documents under the run's UI root: title, hero select, HUD, level-up
    /// cards, chest reel, pause sheet, results, armory, settings, item details, and the overlay (banners, boss bar,
    /// edge warnings, off-screen markers, low-health vignette, hero health bar, damage numbers). One document per
    /// entity (manifest_ui.md lists names and keys). One screen at a time is modal: it reads the menu input. Menus
    /// run on unscaled time while Time.timeScale is 0; keyboard and gamepad drive a focus ring, mouse and touch
    /// click. Main thread.
    /// </summary>
    public sealed class GameUI
    {
        public readonly DamageNumbers damageNumbers;

        internal readonly Run run;
        internal readonly UIInput input = new UIInput();
        internal readonly UILayout layout = new UILayout();
        internal readonly TitleScreen title;
        internal readonly HeroSelectScreen heroSelect;
        internal readonly HudScreen hud;
        internal readonly LevelUpScreen levelUp;
        internal readonly ChestScreen chest;
        internal readonly PauseScreen pause;
        internal readonly ResultsScreen results;
        internal readonly ArmoryScreen armory;
        internal readonly SettingsScreen settings;
        internal readonly OverlayScreen overlay;
        internal readonly DetailsScreen details;
        private readonly List<UIScreen> screens = new List<UIScreen>();
        /// <summary>Settings while the run has no save (never written to disk).</summary>
        private readonly GameSettings defaultSettings = new GameSettings();
        /// <summary>The item a HUD tap asked for while playing: Run.Pause calls ShowPause, which opens it.</summary>
        private DetailsRequest detailsOnPause;
        /// <summary>The screen that reads menu input (null during play).</summary>
        private UIScreen modal;
        /// <summary>Where Settings / Armory return to.</summary>
        private UIScreen returnTo;

        /// <summary>Raised when hero select focuses a class (show its model on a turntable).</summary>
        public event Action<HeroClass> heroPreviewChanged;

        /// <summary>The player's settings: the save's, or defaults while there is no save.</summary>
        internal GameSettings gameSettings => run.save?.settings ?? defaultSettings;

        /// <summary>Dp per rem in the current layout (10 x the UI scale).</summary>
        internal float remDp => layout.remDp;

        public GameUI(Run run, Entity uiRoot)
        {
            this.run = run;
            title = Add(new TitleScreen(this));
            heroSelect = Add(new HeroSelectScreen(this));
            hud = Add(new HudScreen(this));
            overlay = Add(new OverlayScreen(this));
            levelUp = Add(new LevelUpScreen(this));
            chest = Add(new ChestScreen(this));
            pause = Add(new PauseScreen(this));
            results = Add(new ResultsScreen(this));
            armory = Add(new ArmoryScreen(this));
            settings = Add(new SettingsScreen(this));
            details = Add(new DetailsScreen(this));
            foreach (UIScreen screen in screens)
            {
                screen.Attach(uiRoot);
            }
            damageNumbers = new DamageNumbers(this, overlay.documentComponent, overlay.OnHeroHit);
            Screen.resized += ApplyLayoutToAll;
            ApplySettings();
        }

        private T Add<T>(T screen) where T : UIScreen
        {
            screens.Add(screen);
            return screen;
        }

        public void Dispose()
        {
            Screen.resized -= ApplyLayoutToAll;
            damageNumbers.Dispose();
        }

        /// <summary>Input, animations, banners and damage numbers; once per frame with unscaled time.</summary>
        public void Update(float unscaledDt)
        {
            input.Update(unscaledDt, modal != null);
            if (input.deviceChanged)
            {
                foreach (UIScreen screen in screens)
                {
                    ApplyDeviceClasses(screen.GetRoot());
                }
            }
            modal?.ProcessInput(input);
            foreach (UIScreen screen in screens)
            {
                if (layout.IsStale(screen))
                {
                    ApplyLayout(screen);
                }
                if (screen.isShown)
                {
                    screen.Tick(unscaledDt);
                }
            }
            damageNumbers.Update(unscaledDt);
        }

        public void HideAll()
        {
            foreach (UIScreen screen in screens)
            {
                screen.Hide();
            }
            modal = null;
            returnTo = null;
            damageNumbers.Clear();
        }

        /// <summary>Title screen; its Play button leads to hero select, which calls Run.StartRun.</summary>
        public void ShowTitle()
        {
            HideAll();
            OpenModal(title);
        }

        public void ShowHud()
        {
            foreach (UIScreen screen in screens)
            {
                if (screen != hud && screen != overlay)
                {
                    screen.Hide();
                }
            }
            modal = null;
            returnTo = null;
            hud.Show();
            hud.SetModal(false);
            overlay.Show();
        }

        /// <summary>Refreshes the HUD from the run (health, XP, level, timer, kills, gold, slots, active cooldown).</summary>
        public void UpdateHud()
        {
            hud.UpdateFromRun();
            overlay.UpdateFromRun();
        }

        /// <summary>The card picker; calls <paramref name="onChosen"/> with the pick, or null for Skip.</summary>
        public void ShowLevelUp(List<UpgradeOffer> offers, Action<UpgradeOffer> onChosen)
        {
            EnterGameplayMenu();
            levelUp.Prepare(offers, onChosen);
            OpenModal(levelUp);
        }

        /// <summary>The chest reel with its (already applied) items and gold; calls <paramref name="onDone"/> when closed.</summary>
        public void ShowChest(List<UpgradeOffer> items, int gold, Action onDone)
        {
            EnterGameplayMenu();
            chest.Prepare(items, gold, onDone);
            OpenModal(chest);
        }

        /// <summary>The pause sheet; Run.Pause calls it. After a HUD tap it opens the tapped item's details instead.</summary>
        public void ShowPause()
        {
            DetailsRequest request = detailsOnPause;
            detailsOnPause = null;
            if (request != null)
            {
                ShowDetails(request);
                return;
            }
            overlay.Hide();
            damageNumbers.Clear();
            hud.Hide();
            OpenModal(pause);
        }

        public void ShowResults(RunRecord record)
        {
            HideAll();
            results.Prepare(record);
            OpenModal(results);
        }

        /// <summary>A centered banner for a few seconds (events, warnings).</summary>
        public void ShowBanner(string text, float seconds, bool isWarning = false)
        {
            overlay.ShowBanner(text, seconds, isWarning);
        }

        /// <summary>Shows or updates the boss bar; phase marks are health fractions (0.65, 0.3).</summary>
        public void SetBossBar(string name, string title, float fraction, float[] phaseMarks)
        {
            overlay.SetBossBar(name, title, fraction, phaseMarks);
        }

        public void HideBossBar()
        {
            overlay.HideBossBar();
        }

        /// <summary>Danger chevrons at the screen edge toward a world direction from the hero.</summary>
        public void ShowEdgeWarning(Vector3 direction, float seconds)
        {
            overlay.ShowEdgeWarning(direction, seconds);
        }

        /// <summary>
        /// An arrow at the screen edge toward something off screen (elites, chests, the boss, the crimson gem
        /// cluster). Call it every frame for every tracked target while playing; targets on screen show nothing.
        /// </summary>
        public void ShowMarker(Vector3 world, MarkerKind kind)
        {
            overlay.RequestMarker(world, kind);
        }

        /// <summary>The map event's tracker (title, goal, progress, clock); call it every frame while the event runs.</summary>
        public void ShowEventTracker(MapEvent mapEvent)
        {
            overlay.SetEvent(mapEvent);
        }

        public void HideEventTracker()
        {
            overlay.HideEvent();
        }

        /// <summary>
        /// A small health bar over an elite's head. Call it every frame while playing for each live elite with its
        /// head position (world) and health fraction (0..1); at most 8 bars per frame (later calls are ignored), bars
        /// off screen or not refreshed this frame hide, and nothing draws while the "Elite health bars" setting is off.
        /// </summary>
        public void ShowEliteBar(Vector3 worldHead, float healthFraction)
        {
            if (gameSettings.showEliteBars)
            {
                overlay.RequestEliteBar(worldHead, healthFraction);
            }
        }

        /// <summary>A short white or red flash over the whole screen (holy bell, revive).</summary>
        public void FlashScreen(bool isDanger)
        {
            overlay.Flash(isDanger);
        }

        /// <summary>
        /// Details of the weapon in a HUD slot (0..5), as a tap on the slot opens them: while playing it pauses
        /// first (Run.Pause), while paused it replaces the pause sheet. Closing resumes play.
        /// </summary>
        public void OpenWeaponDetails(int slot) => OpenDetails(new DetailsRequest { kind = DetailsKind.Weapon, slot = slot });

        /// <summary>Applies the saved volumes, screen shake and hero effects opacity to the engine and the run.</summary>
        public void ApplySettings()
        {
            GameSettings current = gameSettings;
            Audio.masterVolume = current.masterVolume;
            Audio.SetBusVolume(AudioBus.Music, current.musicVolume);
            Audio.SetBusVolume(AudioBus.Sfx, current.sfxVolume);
            Audio.SetBusVolume(AudioBus.Ui, current.sfxVolume);
            if (run.feedback != null)
            {
                run.feedback.shakeScale = current.screenShake;
            }
            if (run.effects != null)
            {
                run.effects.heroOpacity = current.effectsOpacity;
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Navigation between screens (called by the screens)
        // ---------------------------------------------------------------------------------------------------

        /// <summary>Makes <paramref name="screen"/> the modal screen: hides the previous one and shows it.</summary>
        internal void OpenModal(UIScreen screen)
        {
            if (modal != null && modal != screen)
            {
                modal.Hide();
            }
            modal = screen;
            screen.Show();
        }

        /// <summary>Opens Settings or Armory over the current menu and remembers where to come back.</summary>
        internal void OpenSubmenu(UIScreen screen)
        {
            returnTo = modal;
            OpenModal(screen);
        }

        /// <summary>Back from Settings / Armory.</summary>
        internal void CloseSubmenu()
        {
            UIScreen target = returnTo ?? title;
            returnTo = null;
            OpenModal(target);
        }

        internal void OpenHeroSelect()
        {
            HideAll();
            OpenModal(heroSelect);
        }

        /// <summary>Back to the title from any menu (the run is left).</summary>
        internal void BackToTitle()
        {
            if (run.phase == RunPhase.Menu)
            {
                ShowTitle();
                return;
            }
            run.ShowTitle();
        }

        internal void NotifyHeroPreview(HeroClass heroClass) => heroPreviewChanged?.Invoke(heroClass);

        /// <summary>
        /// Opens the details of a weapon, passive or the active ability the player tapped on the HUD. While playing,
        /// play pauses through the run's own pause (Run.Pause: phase Paused, time scale 0), whose ShowPause call
        /// opens the details instead of the pause sheet; while paused the details replace the pause sheet.
        /// </summary>
        internal void OpenDetails(DetailsRequest request)
        {
            if (request == null)
            {
                return;
            }
            if (run.phase == RunPhase.Playing)
            {
                detailsOnPause = request;
                run.Pause();
                detailsOnPause = null;
                return;
            }
            if (run.phase == RunPhase.Paused)
            {
                ShowDetails(request);
            }
        }

        /// <summary>The details of a hero's starting kit: hero select stays up behind them.</summary>
        internal void OpenHeroDetails(DetailsRequest request)
        {
            details.Prepare(request);
            modal = details;
            details.Show();
        }

        /// <summary>Closes the details: hero select takes the input back, or play resumes (Run.Resume shows the HUD
        /// again).</summary>
        internal void CloseDetails()
        {
            details.Hide();
            if (run.phase == RunPhase.Menu)
            {
                modal = heroSelect;
                return;
            }
            if (run.phase == RunPhase.Paused)
            {
                run.Resume();
                return;
            }
            ShowHud();
        }

        private void ShowDetails(DetailsRequest request)
        {
            EnterGameplayMenu();
            details.Prepare(request);
            OpenModal(details);
        }

        /// <summary>The HUD stays visible behind level-up, chest and details; the overlay hides so no damage number
        /// floats over the dimmed world.</summary>
        private void EnterGameplayMenu()
        {
            overlay.Hide();
            damageNumbers.Clear();
            hud.Show();
            hud.SetModal(true);
        }

        // ---------------------------------------------------------------------------------------------------
        // Sounds, layout, device classes
        // ---------------------------------------------------------------------------------------------------

        internal void PlaySound(string name)
        {
            run.sounds?.Play(name);
        }

        /// <summary>Re-scales every document (screen size, safe area or the Interface size setting changed).</summary>
        internal void ApplyLayoutToAll()
        {
            foreach (UIScreen screen in screens)
            {
                ApplyLayout(screen);
            }
        }

        internal void ApplyLayout(UIScreen screen) => layout.Apply(screen, gameSettings);

        /// <summary>Body classes for hints and the focus ring: input-kbd / input-pad / input-touch, show-focus.</summary>
        internal void ApplyDeviceClasses(UIElement root)
        {
            if (root == null)
            {
                return;
            }
            InputDevice device = input.device;
            root.SetClass("input-kbd", device == InputDevice.Keyboard || device == InputDevice.Mouse);
            root.SetClass("input-pad", device == InputDevice.Gamepad);
            root.SetClass("input-touch", device == InputDevice.Touch);
            root.SetClass("show-focus", input.isFocusDevice);
        }
    }
}

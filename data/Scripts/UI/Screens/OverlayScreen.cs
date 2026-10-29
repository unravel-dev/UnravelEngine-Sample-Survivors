using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The overlay over the world during play, made of widgets that each own their part of the document:
    /// <see cref="HeroVitals"/> (low-health vignette, hit pulse, screen flash, the hero's health bar),
    /// <see cref="WorldMarkers"/> (off-screen markers, elite health bars, edge danger chevrons), the
    /// <see cref="BossBar"/>, the map <see cref="EventTracker"/> and the <see cref="BannerQueue"/>. Damage numbers
    /// share the document (<see cref="DamageNumbers"/>). Positions are written per frame with numeric style
    /// setters; everything else only when it changes, so a frame allocates nothing. Main thread.
    /// </summary>
    internal sealed class OverlayScreen : UIScreen
    {
        private readonly HeroVitals vitals = new HeroVitals();
        private readonly WorldMarkers markers = new WorldMarkers();
        private readonly BossBar bossBar;
        private readonly EventTracker eventTracker = new EventTracker();
        private readonly BannerQueue banners;
        /// <summary>The loadout of the run the overlay shows; a new one means a new run.</summary>
        private Loadout shownLoadout;

        public OverlayScreen(GameUI ui) : base(ui, "Overlay", "app:/data/UI/Overlay.rhtml")
        {
            Action<string> playSound = ui.PlaySound;
            bossBar = new BossBar(playSound);
            banners = new BannerQueue(playSound);
        }

        protected override void OnLoaded(UIDocument loaded)
        {
            vitals.Bind(loaded);
            markers.Bind(loaded);
            bossBar.Bind(loaded);
            eventTracker.Bind(loaded);
            banners.Bind(loaded);
        }

        public override void Show()
        {
            base.Show();
            if (!ReferenceEquals(run.loadout, shownLoadout))
            {
                ResetForRun();
            }
        }

        public override void Hide()
        {
            base.Hide();
            markers.ClearRequests();
        }

        /// <summary>A new run: no boss bar, banners, warnings, pulses or event left from the last one.</summary>
        private void ResetForRun()
        {
            shownLoadout = run.loadout;
            bossBar.Hide();
            banners.Reset();
            markers.Reset();
            vitals.Reset();
            eventTracker.Hide();
        }

        // ---------------------------------------------------------------------------------------------------
        // Requests from the game (through GameUI)
        // ---------------------------------------------------------------------------------------------------

        /// <summary>One elite health bar this frame; calls beyond the pool size are ignored.</summary>
        public void RequestEliteBar(Vector3 worldHead, float healthFraction) => markers.RequestEliteBar(worldHead, healthFraction);

        /// <summary>One marker this frame; calls beyond the pool size are ignored.</summary>
        public void RequestMarker(Vector3 world, MarkerKind kind) => markers.RequestMarker(world, kind);

        /// <summary>Danger chevrons toward a world direction from the hero, with the warning sound.</summary>
        public void ShowEdgeWarning(Vector3 direction, float seconds)
        {
            if (markers.AddEdgeWarning(direction, seconds))
            {
                ui.PlaySound("danger_warning");
            }
        }

        public void SetEvent(MapEvent mapEvent) => eventTracker.Set(mapEvent);

        public void HideEvent() => eventTracker.Hide();

        public void ShowBanner(string text, float seconds, bool isWarning) => banners.Show(text, seconds, isWarning);

        public void Flash(bool isDanger) => vitals.Flash(isDanger);

        /// <summary>A hit on the hero: the vignette pulses red for a moment.</summary>
        public void OnHeroHit() => vitals.OnHit();

        public void SetBossBar(string name, string title, float fraction, float[] phaseMarks) => bossBar.Set(name, title, fraction, phaseMarks);

        public void HideBossBar() => bossBar.Hide();

        // ---------------------------------------------------------------------------------------------------
        // Per frame
        // ---------------------------------------------------------------------------------------------------

        /// <summary>The hero's health bar from the run (only while playing).</summary>
        public void UpdateFromRun()
        {
            if (isReady)
            {
                vitals.UpdateBar(run, GetPixelsPerDp());
            }
        }

        public override void Tick(float unscaledDt)
        {
            if (!isReady)
            {
                markers.ClearRequests();
                return;
            }
            float pixelsPerDp = GetPixelsPerDp();
            vitals.Tick(run, unscaledDt);
            markers.Tick(run, unscaledDt, pixelsPerDp, ui.remDp * pixelsPerDp);
            bossBar.Tick(unscaledDt);
            banners.Tick(unscaledDt);
        }

        private float GetPixelsPerDp()
        {
            float value = document.pixelsPerDp;
            return value > 0.0f ? value : 1.0f;
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The hero's state over the world: a red vignette that deepens below 30% health and beats like a heart (faster
    /// below 15%), a red pulse on each hit, the white or red screen flash, and the health bar above the hero's
    /// head (green, amber below half, red below a quarter). Part of the overlay. Main thread.
    /// </summary>
    internal sealed class HeroVitals
    {
        private const float LOW_HEALTH = 0.30f;
        private const float CRITICAL_HEALTH = 0.15f;
        private const float VIGNETTE_MAX = 0.6f;
        private const float HIT_PULSE_SECONDS = 0.3f;
        private const float HIT_PULSE_ALPHA = 0.45f;
        private const float FLASH_SECONDS = 0.25f;
        private const float FLASH_ALPHA = 0.55f;
        /// <summary>Metres above the hero's feet the health bar floats.</summary>
        private const float BAR_HEIGHT = 2.35f;
        private const float BAR_AMBER_BELOW = 0.5f;
        private const float BAR_RED_BELOW = 0.25f;

        private enum Heartbeat
        {
            Unknown,
            None,
            Slow,
            Fast,
        }

        private enum BarColor
        {
            Unknown,
            Green,
            Amber,
            Red,
        }

        private UIElement vignetteImage;
        private UIElement flash;
        private UIElement bar;
        private OpacityWriter vignetteOpacity;
        private OpacityWriter flashOpacity;
        private ProgressWriter barFill;
        private VisibilityWriter barVisibility;
        private Heartbeat heartbeat;
        private BarColor barColor;
        private float hitPulse;
        private float flashLevel;
        private bool isFlashDanger;

        /// <summary>Fetches the elements of a freshly loaded overlay document.</summary>
        public void Bind(UIDocument document)
        {
            vignetteOpacity.Bind(document.Q("#vignette"));
            vignetteImage = document.Q("#vignette-img");
            flash = document.Q("#flash");
            flashOpacity.Bind(flash);
            bar = document.Q("#herobar");
            barFill.Bind(document.Q<UIProgress>("#herobar-fill"));
            barVisibility.Bind(bar);
            heartbeat = Heartbeat.Unknown;
            barColor = BarColor.Unknown;
        }

        /// <summary>A new run: no pulse or flash left from the last one.</summary>
        public void Reset()
        {
            hitPulse = 0.0f;
            flashLevel = 0.0f;
        }

        /// <summary>A short white flash, or a red one for danger.</summary>
        public void Flash(bool isDanger)
        {
            flashLevel = 1.0f;
            if (isDanger != isFlashDanger)
            {
                isFlashDanger = isDanger;
                flash?.SetClass("danger", isDanger);
            }
        }

        /// <summary>A hit on the hero: the vignette pulses red for a moment.</summary>
        public void OnHit()
        {
            hitPulse = 1.0f;
        }

        /// <summary>Places the health bar over the hero's head (only while playing).</summary>
        public void UpdateBar(Run run, float pixelsPerDp)
        {
            HeroController hero = run.hero;
            CameraComponent camera = run.gameCamera;
            bool isShown = hero != null && hero.isAlive && !ReferenceEquals(camera, null);
            if (isShown)
            {
                Vector3 screen = camera.WorldToScreenPoint(hero.position + new Vector3(0.0f, BAR_HEIGHT, 0.0f));
                isShown = screen.z > 0.0f;
                if (isShown)
                {
                    bar.style.left = screen.x / pixelsPerDp;
                    bar.style.top = screen.y / pixelsPerDp;
                }
                WriteHealth(Math.Clamp(hero.healthFraction, 0.0f, 1.0f));
            }
            barVisibility.Write(isShown);
        }

        /// <summary>Vignette, hit pulse and flash; every frame.</summary>
        public void Tick(Run run, float dt)
        {
            HeroController hero = run.hero;
            float health = hero != null && hero.isAlive ? hero.healthFraction : 1.0f;
            float low = health < LOW_HEALTH ? (LOW_HEALTH - health) / LOW_HEALTH * VIGNETTE_MAX : 0.0f;
            hitPulse = MathF.Max(0.0f, hitPulse - dt / HIT_PULSE_SECONDS);
            vignetteOpacity.Write(MathF.Max(low, hitPulse * HIT_PULSE_ALPHA));
            SetHeartbeat(health < CRITICAL_HEALTH ? Heartbeat.Fast : health < LOW_HEALTH ? Heartbeat.Slow : Heartbeat.None);
            flashLevel = MathF.Max(0.0f, flashLevel - dt / FLASH_SECONDS);
            flashOpacity.Write(flashLevel * flashLevel * FLASH_ALPHA);
        }

        private void WriteHealth(float fraction)
        {
            barFill.Write(fraction);
            BarColor color = fraction > BAR_AMBER_BELOW ? BarColor.Green : fraction > BAR_RED_BELOW ? BarColor.Amber : BarColor.Red;
            if (color == barColor)
            {
                return;
            }
            barColor = color;
            bar.SetClass("amber", color == BarColor.Amber);
            bar.SetClass("red", color == BarColor.Red);
        }

        private void SetHeartbeat(Heartbeat beat)
        {
            if (beat == heartbeat || vignetteImage == null)
            {
                return;
            }
            heartbeat = beat;
            vignetteImage.SetClass("beat-slow", beat == Heartbeat.Slow);
            vignetteImage.SetClass("beat-fast", beat == Heartbeat.Fast);
        }
    }
}

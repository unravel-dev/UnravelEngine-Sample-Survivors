using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Event banners across the upper screen, one at a time: each drops in with a sound, stays its seconds (at least
    /// one) and fades out over the last 0.3 s; the next one waits in a queue. Warnings are red. Part of the overlay.
    /// Main thread.
    /// </summary>
    internal sealed class BannerQueue
    {
        private const float FADE_SECONDS = 0.3f;
        private const float MIN_SECONDS = 1.0f;
        private const string SHOWN_CLASS = "shown";

        private struct Banner
        {
            public string text;
            public float seconds;
            public bool isWarning;
        }

        private readonly Action<string> playSound;
        private readonly Queue<Banner> queue = new Queue<Banner>();
        private UIElement banner;
        private UIElement inner;
        private UIElement text;
        private OpacityWriter opacity;
        private bool isActive;
        private float remaining;

        public BannerQueue(Action<string> playSound)
        {
            this.playSound = playSound;
        }

        /// <summary>Fetches the elements of a freshly loaded overlay document.</summary>
        public void Bind(UIDocument document)
        {
            banner = document.Q("#banner");
            inner = document.Q("#banner-inner");
            text = document.Q("#banner-text");
            opacity.Bind(banner);
        }

        /// <summary>A new run: no banner showing or waiting.</summary>
        public void Reset()
        {
            queue.Clear();
            isActive = false;
            banner?.SetClass(SHOWN_CLASS, false);
        }

        /// <summary>Queues a banner (upper-cased); it shows at once when none is up.</summary>
        public void Show(string message, float seconds, bool isWarning)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            queue.Enqueue(new Banner { text = message.ToUpperInvariant(), seconds = MathF.Max(MIN_SECONDS, seconds), isWarning = isWarning });
            if (!isActive)
            {
                ShowNext();
            }
        }

        /// <summary>The fade and the hand-over to the next banner; every frame.</summary>
        public void Tick(float dt)
        {
            if (!isActive)
            {
                return;
            }
            remaining -= dt;
            opacity.Write(Math.Clamp(remaining / FADE_SECONDS, 0.0f, 1.0f));
            if (remaining > 0.0f)
            {
                return;
            }
            isActive = false;
            banner.SetClass(SHOWN_CLASS, false);
            ShowNext();
        }

        private void ShowNext()
        {
            if (queue.Count == 0 || banner == null)
            {
                return;
            }
            Banner next = queue.Dequeue();
            isActive = true;
            remaining = next.seconds;
            text.SetText(next.text);
            banner.SetClass("warning", next.isWarning);
            banner.SetClass(SHOWN_CLASS, true);
            opacity.Write(1.0f);
            inner?.RestartAnimation();
            playSound(next.isWarning ? "banner_warning" : "banner");
        }
    }
}

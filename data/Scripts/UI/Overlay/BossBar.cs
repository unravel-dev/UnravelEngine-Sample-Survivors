using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The boss bar at the top center: title, name, the health fill over a white ghost chunk (it holds a moment
    /// after a hit, then drains to the fill) and phase pips at the boss's phase marks; a pip the health falls past
    /// breaks with a shard burst. Part of the overlay. Main thread.
    /// </summary>
    internal sealed class BossBar
    {
        private const float GHOST_HOLD_SECONDS = 0.4f;
        private const float GHOST_DRAIN_PER_SECOND = 0.5f;
        /// <summary>A fraction lower than the last by more than this is a hit (the ghost chunk shows).</summary>
        private const float HIT_EPSILON = 1e-5f;
        private const string SHOWN_CLASS = "shown";
        private const string BROKEN_CLASS = "broken";

        private readonly Action<string> playSound;
        private readonly List<UIElement> pips = new List<UIElement>();
        private readonly StringBuilder rml = new StringBuilder(256);
        private UIElement bar;
        private UIElement titleText;
        private UIElement nameText;
        private UIElement pipLayer;
        private UIElement shards;
        private ProgressWriter fill;
        private ProgressWriter ghost;
        private string shownName;
        private string shownTitle;
        private float[] shownMarks;
        private bool[] marksBroken = Array.Empty<bool>();
        private bool isVisible;
        private float fraction = 1.0f;
        private float ghostFraction = 1.0f;
        private float ghostHold;

        public BossBar(Action<string> playSound)
        {
            this.playSound = playSound;
        }

        /// <summary>Fetches the elements of a freshly loaded overlay document; a bar that was up stays up.</summary>
        public void Bind(UIDocument document)
        {
            bar = document.Q("#bossbar");
            titleText = document.Q("#boss-title");
            nameText = document.Q("#boss-name");
            pipLayer = document.Q("#boss-pips");
            shards = document.Q("#boss-shards");
            fill.Bind(document.Q<UIProgress>("#boss-fill"));
            ghost.Bind(document.Q<UIProgress>("#boss-ghost"));
            shownName = null;
            shownTitle = null;
            shownMarks = null;
            pips.Clear();
            if (isVisible)
            {
                bar?.SetClass(SHOWN_CLASS, true);
            }
        }

        /// <summary>Shows or updates the bar; phase marks are health fractions (0.65, 0.3).</summary>
        public void Set(string name, string title, float healthFraction, float[] phaseMarks)
        {
            if (!isVisible)
            {
                Appear(phaseMarks);
            }
            if (name != shownName && nameText != null)
            {
                shownName = name;
                nameText.SetText(name ?? "");
            }
            if (title != shownTitle && titleText != null)
            {
                shownTitle = title;
                titleText.SetText(string.IsNullOrEmpty(title) ? "" : title.ToUpperInvariant());
            }
            if (!ReferenceEquals(phaseMarks, shownMarks))
            {
                BuildPips(phaseMarks);
            }
            TrackGhost(Math.Clamp(healthFraction, 0.0f, 1.0f));
            BreakPassedPips();
        }

        public void Hide()
        {
            isVisible = false;
            bar?.SetClass(SHOWN_CLASS, false);
        }

        /// <summary>The ghost chunk's hold and drain; every frame.</summary>
        public void Tick(float dt)
        {
            if (!isVisible)
            {
                return;
            }
            if (ghostHold > 0.0f)
            {
                ghostHold -= dt;
            }
            else
            {
                ghostFraction = MathF.Max(fraction, ghostFraction - GHOST_DRAIN_PER_SECOND * dt);
            }
            fill.Write(fraction);
            ghost.Write(ghostFraction);
        }

        private void Appear(float[] phaseMarks)
        {
            isVisible = true;
            fraction = 1.0f;
            ghostFraction = 1.0f;
            ghostHold = 0.0f;
            BuildPips(phaseMarks);
            bar?.SetClass(SHOWN_CLASS, true);
            playSound("boss_bar_appear");
        }

        /// <summary>A hit leaves the health it took as the ghost chunk, held for a moment; a heal lifts the ghost.</summary>
        private void TrackGhost(float healthFraction)
        {
            if (healthFraction < fraction - HIT_EPSILON)
            {
                if (ghostHold <= 0.0f)
                {
                    ghostFraction = MathF.Max(ghostFraction, fraction);
                }
                ghostHold = GHOST_HOLD_SECONDS;
            }
            else if (healthFraction > ghostFraction)
            {
                ghostFraction = healthFraction;
            }
            fraction = healthFraction;
        }

        private void BuildPips(float[] marks)
        {
            shownMarks = marks;
            marksBroken = new bool[marks != null ? marks.Length : 0];
            pips.Clear();
            if (pipLayer == null)
            {
                return;
            }
            rml.Clear();
            for (int i = 0; i < marksBroken.Length; i++)
            {
                rml.Append("<div class=\"boss-pip\" style=\"left: ").Append(FormatPercent(marks[i]))
                   .Append(";\"><div class=\"boss-pip-line\"/><div class=\"boss-pip-gem\"/></div>");
                marksBroken[i] = fraction <= marks[i];
            }
            pipLayer.InnerRml = rml.ToString();
            pips.AddRange(pipLayer.QueryAll(".boss-pip"));
            for (int i = 0; i < pips.Count && i < marksBroken.Length; i++)
            {
                pips[i].SetClass(BROKEN_CLASS, marksBroken[i]);
            }
        }

        /// <summary>Breaks every pip the health fell to or below since the last call, with a shard burst.</summary>
        private void BreakPassedPips()
        {
            for (int i = 0; shownMarks != null && i < shownMarks.Length && i < marksBroken.Length; i++)
            {
                if (marksBroken[i] || fraction > shownMarks[i])
                {
                    continue;
                }
                marksBroken[i] = true;
                if (i < pips.Count)
                {
                    pips[i].SetClass(BROKEN_CLASS, true);
                }
                BurstShards(shownMarks[i]);
                playSound("boss_bar_shatter");
            }
        }

        private void BurstShards(float mark)
        {
            if (shards == null)
            {
                return;
            }
            shards.style["left"] = (mark * 100.0f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
            shards.SetClass("burst", true);
            foreach (UIElement shard in shards.children)
            {
                shard.RestartAnimation();
            }
        }

        private static string FormatPercent(float mark) =>
            (Math.Clamp(mark, 0.0f, 1.0f) * 100.0f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The map event's tracker: a panel under the gold with the event's icon (what it pays), title, goal, clock and
    /// bar, tinted by the event's kind; it turns green when won and red when lost. Text is written only when it
    /// changes. Part of the overlay. Main thread.
    /// </summary>
    internal sealed class EventTracker
    {
        private const string SHOWN_CLASS = "shown";
        private const string DONE_TEXT = "Done.";
        private const string FAILED_TEXT = "Failed.";

        private UIElement panel;
        private UIElement icon;
        private UIElement titleText;
        private UIElement goalText;
        private UIElement timeText;
        private ProgressWriter fill;
        private bool isVisible;
        private MapEventKind kind;
        private MapEventState state;
        private string shownTitle;
        private string shownGoal;
        private int shownSeconds;

        /// <summary>Fetches the elements of a freshly loaded overlay document (the tracker starts hidden).</summary>
        public void Bind(UIDocument document)
        {
            panel = document.Q("#event");
            icon = document.Q("#event-icon");
            titleText = document.Q("#event-title");
            goalText = document.Q("#event-goal");
            timeText = document.Q("#event-time");
            fill.Bind(document.Q<UIProgress>("#event-fill"));
            isVisible = false;
            kind = MapEventKind.Count;
            state = MapEventState.Expired;
            shownTitle = null;
            shownGoal = null;
            shownSeconds = -1;
        }

        /// <summary>Shows the tracker for <paramref name="mapEvent"/>; call it every frame while the event runs.</summary>
        public void Set(MapEvent mapEvent)
        {
            if (ReferenceEquals(panel, null))
            {
                return;
            }
            WriteKind(mapEvent);
            WriteState(mapEvent.state);
            WriteTexts(mapEvent);
            fill.Write(Math.Clamp(mapEvent.progress, 0.0f, 1.0f));
            if (!isVisible)
            {
                isVisible = true;
                panel.SetClass(SHOWN_CLASS, true);
            }
        }

        public void Hide()
        {
            if (!isVisible || ReferenceEquals(panel, null))
            {
                return;
            }
            isVisible = false;
            panel.SetClass(SHOWN_CLASS, false);
        }

        private void WriteKind(MapEvent mapEvent)
        {
            if (mapEvent.kind == kind)
            {
                return;
            }
            panel.SetClass(GetKindClass(kind), false);
            kind = mapEvent.kind;
            panel.SetClass(GetKindClass(kind), true);
            icon?.SetAttribute("src", mapEvent.icon);
        }

        private void WriteState(MapEventState next)
        {
            if (next == state)
            {
                return;
            }
            state = next;
            panel.SetClass("won", state == MapEventState.Won);
            panel.SetClass("lost", state == MapEventState.Lost);
        }

        private void WriteTexts(MapEvent mapEvent)
        {
            string title = mapEvent.title;
            if (!ReferenceEquals(title, shownTitle))
            {
                shownTitle = title;
                titleText?.SetText(title.ToUpperInvariant());
            }
            string goal = mapEvent.isOver ? (mapEvent.state == MapEventState.Won ? DONE_TEXT : FAILED_TEXT) : mapEvent.goal;
            if (goal != shownGoal)
            {
                shownGoal = goal;
                goalText?.SetText(goal);
            }
            int seconds = mapEvent.isOver ? 0 : (int)MathF.Ceiling(MathF.Max(0.0f, mapEvent.timeLeft));
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                timeText?.SetText(mapEvent.isOver ? "" : UIText.Time(seconds));
            }
        }

        private static string GetKindClass(MapEventKind kind)
        {
            switch (kind)
            {
                case MapEventKind.Shrine: return "shrine";
                case MapEventKind.Robber: return "robber";
                case MapEventKind.Pyres: return "pyres";
                case MapEventKind.Moonwell: return "moonwell";
                default: return "none";
            }
        }
    }
}

"""Map events (design: map events): hooks in the run, the director, the enemy catalog, the heal telemetry, the
overlay's tracker and markers. One-shot; the event classes live in data/Scripts/Events."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from patchlib import edit  # noqa: E402

DATA = os.path.join(HERE, "..", "..", "data")
S = os.path.join(DATA, "Scripts")
UI = os.path.join(DATA, "UI")

edit(os.path.join(S, "Core", "RunRecord.cs"), [
    ("""        /// <summary>Level-up cards (Hearty Meal).</summary>
        Card,
""", """        /// <summary>Level-up cards (Hearty Meal).</summary>
        Card,
        /// <summary>Map events (the Moonwell).</summary>
        Event,
"""),
])

edit(os.path.join(S, "Enemies", "EnemyCatalog.cs"), [
    ("""        public static Prefab lanternPrefab;
""", """        public static Prefab lanternPrefab;
        /// <summary>A bone pyre's proxy row type (map events): never moves, health set at spawn, no contact damage.</summary>
        public static int pyreType = -1;
"""),
    ("""            lanternPrefab = Assets.GetAsset<Prefab>("app:/data/Prefabs/Props/GraveLantern.pfb");
""", """            lanternPrefab = Assets.GetAsset<Prefab>("app:/data/Prefabs/Props/GraveLantern.pfb");
            pyreType = world.Register(new EnemyType
            {
                name = "BonePyre",
                displayName = "Bone Pyre",
                isProxy = true,
                health = 1.0f,
                radius = 0.6f,
                weight = 1000.0f,
                contactDamage = 0.0f,
                experience = 0.0f,
                eliteHealth = 1.0f,
                eliteScale = 1.0f,
            });
"""),
])

edit(os.path.join(S, "Enemies", "WaveDirector.cs"), [
    ("""        /// <summary>Health multiplier of enemies spawned now: (1 + 0.15 t + 0.02 t^2) x (1 + 0.15 dread).</summary>
        public float healthScale => (1.0f + 0.15f * minutes + 0.02f * minutes * minutes) * (1.0f + 0.15f * dread);
""", """        /// <summary>Health multiplier of enemies spawned now: (1 + 0.15 t + 0.02 t^2) x <see cref="dreadScale"/>.</summary>
        public float healthScale => (1.0f + 0.15f * minutes + 0.02f * minutes * minutes) * dreadScale;

        /// <summary>The Dread part of enemy health: 1 + 0.15 dread.</summary>
        public float dreadScale => 1.0f + 0.15f * dread;
"""),
    ("""        private Archetype PickArchetype()
""", """        /// <summary>An archetype by the minute's mix (capped archetypes give way).</summary>
        public Archetype PickArchetype()
"""),
    ("""        private Vector3 ClampToArena(Vector3 position)
""", """        public Vector3 ClampToArena(Vector3 position)
"""),
    ("""        /// <summary>Enemies evenly on a circle around the hero; they walk to its center for a while before chasing.</summary>
        private void SpawnRing(Archetype archetype, int count, float radius)
        {
            Vector3 center = run.hero.position;
            int type = EnemyCatalog.Get(archetype, currentTint);
""", """        /// <summary>Enemies evenly on a circle around the hero; they walk to its center for a while before chasing.</summary>
        private void SpawnRing(Archetype archetype, int count, float radius)
        {
            SpawnRingAround(run.hero.position, archetype, count, radius);
        }

        /// <summary>Enemies evenly on a circle around a point (map events too); they walk to it for a while before chasing.</summary>
        public void SpawnRingAround(Vector3 center, Archetype archetype, int count, float radius)
        {
            int type = EnemyCatalog.Get(archetype, currentTint);
"""),
    ("""        private void SpawnElite()
        {
            Archetype archetype = PickArchetype();
            float angle = (float)run.rng.NextDouble() * MathF.PI * 2.0f;
            Vector3 position = ClampToArena(run.hero.position + new Vector3(MathF.Sin(angle), 0.0f, MathF.Cos(angle)) * ELITE_DISTANCE);
            Enemy elite""", """        private void SpawnElite()
        {
            float angle = (float)run.rng.NextDouble() * MathF.PI * 2.0f;
            SpawnEliteAt(ClampToArena(run.hero.position + new Vector3(MathF.Sin(angle), 0.0f, MathF.Cos(angle)) * ELITE_DISTANCE));
        }

        /// <summary>An elite of the minute's mix at a point, with a beam, a horn and (from minute 8) an affix.</summary>
        public void SpawnEliteAt(Vector3 position)
        {
            Archetype archetype = PickArchetype();
            Enemy elite"""),
])

RUN = os.path.join(S, "Core", "Run.cs")
edit(RUN, [
    ("""        /// <summary>Light pillars over the chests on the ground (created with the first run).</summary>
        private ChestBeacons chestBeacons;
""", """        /// <summary>Light pillars over the chests on the ground (created with the first run).</summary>
        private ChestBeacons chestBeacons;
        /// <summary>The optional challenges of the moor (created with the first run).</summary>
        public MapEvents mapEvents;
        /// <summary>Ground circles (x, z, radius) of the arena's obstacles.</summary>
        private Vector3[] obstacles = Array.Empty<Vector3>();
"""),
    ("""            enemies.SetObstacles(CollectObstacles());
""", """            obstacles = CollectObstacles();
            enemies.SetObstacles(obstacles);
"""),
    ("""            pickups.Draw();
            director.Draw();
        }
""", """            pickups.Draw();
            director.Draw();
            mapEvents?.Draw();
        }

        /// <summary>Whether ground is clear of the arena's obstacles within a radius of a point.</summary>
        public bool IsOpenGround(Vector3 position, float radius)
        {
            foreach (Vector3 circle in obstacles)
            {
                float dx = position.x - circle.x;
                float dz = position.z - circle.y;
                float reach = radius + circle.z;
                if (dx * dx + dz * dz < reach * reach)
                {
                    return false;
                }
            }
            return true;
        }
"""),
    ("""            chestBeacons ??= new ChestBeacons(this);
            chestBeacons.Reset();
""", """            chestBeacons ??= new ChestBeacons(this);
            chestBeacons.Reset();
            mapEvents ??= new MapEvents(this);
            mapEvents.Reset();
"""),
    ("""            effects.ResetAll();
            director?.Clear();
""", """            effects.ResetAll();
            director?.Clear();
            mapEvents?.Clear();
"""),
    ("""            loadout.Tick(dt);
            RecordStatusDamage();
            chestBeacons.Tick(dt);
""", """            loadout.Tick(dt);
            RecordStatusDamage();
            mapEvents.Tick(dt);
            chestBeacons.Tick(dt);
"""),
    ("""        private readonly Vector3[] markerPositions = new Vector3[8];

        /// <summary>Off-screen arrows toward elites and chests (the UI draws only those off screen).</summary>
        /// <summary>Flames licking""", """        private readonly Vector3[] markerPositions = new Vector3[8];

        /// <summary>Flames licking"""),
    ("""        private void ShowMarkers()
        {""", """        /// <summary>Badges over chests on screen and arrows toward chests, the gem cluster and elites off screen (the map
        /// event marks itself first; its robber is left out here).</summary>
        private void ShowMarkers()
        {"""),
    ("""                ui.ShowEliteBar(position + new Vector3(0.0f, enemies.GetHeight(row) + ELITE_BAR_LIFT, 0.0f), enemies.GetHealthFraction(row));
                if (i < eliteMarkers)""", """                ui.ShowEliteBar(position + new Vector3(0.0f, enemies.GetHeight(row) + ELITE_BAR_LIFT, 0.0f), enemies.GetHealthFraction(row));
                if (i < eliteMarkers && !mapEvents.Owns(row))"""),
    ("""            director.OnEnemyDied(death);
            Drops.OnEnemyDeath(this, death);
""", """            director.OnEnemyDied(death);
            mapEvents?.OnEnemyDied(death);
            Drops.OnEnemyDeath(this, death);
"""),
    ("""            phase = RunPhase.Ended;
            record.isVictory = isVictory;
""", """            phase = RunPhase.Ended;
            mapEvents?.Clear();
            record.isVictory = isVictory;
"""),
    ("""        /// <summary>Dev: level-ups offer only boons while on (card reviews).</summary>""",
     """        /// <summary>Dev: starts a map event (Shrine, Robber, Pyres, Moonwell) 9 m ahead of the hero now.</summary>
        public void DevEvent(string kind)
        {
            if (mapEvents != null && hero != null && Enum.TryParse(kind, true, out MapEventKind parsed) && parsed != MapEventKind.Count)
            {
                mapEvents.DevStart(parsed);
            }
        }

        /// <summary>Dev: level-ups offer only boons while on (card reviews).</summary>"""),
])

edit(os.path.join(S, "UI", "GameUI.cs"), [
    ("""        Boss,
        Treasure,
    }
""", """        Boss,
        Treasure,
        Shrine,
        Robber,
        Pyres,
        Moonwell,
    }
"""),
    ("""        /// <summary>
        /// A small health bar over an elite's head""", """        /// <summary>The map event's tracker (title, goal, progress, clock); call it every frame while the event runs.</summary>
        public void ShowEventTracker(MapEvent mapEvent)
        {
            overlay.SetEvent(mapEvent);
        }

        public void HideEventTracker()
        {
            overlay.HideEvent();
        }

        /// <summary>
        /// A small health bar over an elite's head"""),
])

OVERLAY = os.path.join(S, "UI", "Screens", "OverlayScreen.cs")
edit(OVERLAY, [
    ("""    /// a white ghost chunk and pip shatter (design 7.3), and queued event banners. Positions are written per frame
    /// with numeric style setters and cached transform strings (no allocations per frame). Main thread.""",
     """    /// a white ghost chunk and pip shatter (design 7.3), queued event banners and the map event's tracker. Positions
    /// are written per frame with numeric style setters and cached transform strings (no allocations per frame).
    /// Main thread."""),
    ("""        /// <summary>The marker shows a chest on screen: its icon bobs above it and the arrow hides.</summary>""",
     """        /// <summary>The marker shows a chest or a map event on screen: its icon bobs above it and the arrow hides.</summary>"""),
    ("""        /// <summary>Metres above a chest its on-screen icon floats.</summary>
        private const float CHEST_ICON_HEIGHT = 3.4f;
""", """        /// <summary>Metres above a chest its on-screen icon floats.</summary>
        private const float CHEST_ICON_HEIGHT = 3.4f;
        /// <summary>Metres above a map event (or its robber) its on-screen icon floats.</summary>
        private const float EVENT_ICON_HEIGHT = 4.2f;
        /// <summary>The tracker's bar is rewritten when it moves more than this.</summary>
        private const float EVENT_FILL_EPSILON = 0.002f;
"""),
    ("""        private UIElement banner;
        private UIElement bannerInner;
        private UIElement bannerText;
""", """        private UIElement banner;
        private UIElement bannerInner;
        private UIElement bannerText;
        private UIElement eventPanel;
        private UIElement eventIcon;
        private UIElement eventTitle;
        private UIElement eventGoal;
        private UIElement eventTime;
        private UIProgress eventFill;
        private bool eventVisible;
        private MapEventKind eventKind = MapEventKind.Count;
        private MapEventState eventState = MapEventState.Expired;
        private string eventTitleText;
        private string eventGoalText;
        private int eventSeconds = -1;
        private float eventFillWritten = -1.0f;
"""),
    ("""            bannerText = loaded.Q("#banner-text");
""", """            bannerText = loaded.Q("#banner-text");
            eventPanel = loaded.Q("#event");
            eventIcon = loaded.Q("#event-icon");
            eventTitle = loaded.Q("#event-title");
            eventGoal = loaded.Q("#event-goal");
            eventTime = loaded.Q("#event-time");
            eventFill = loaded.Q<UIProgress>("#event-fill");
            eventVisible = false;
            eventKind = MapEventKind.Count;
            eventState = MapEventState.Expired;
            eventTitleText = null;
            eventGoalText = null;
            eventSeconds = -1;
            eventFillWritten = -1.0f;
"""),
    ("""            hitPulse = 0.0f;
            flashLevel = 0.0f;
        }
""", """            hitPulse = 0.0f;
            flashLevel = 0.0f;
            HideEvent();
        }
"""),
    ("""        public void RequestMarker(Vector3 world, MarkerKind kind)
        {""", """        /// <summary>
        /// The map event's tracker: a panel under the gold with the event's icon (what it pays), title, goal, clock and
        /// bar, tinted by the kind; it turns green when won and red when lost. Text is written only when it changes.
        /// </summary>
        public void SetEvent(MapEvent mapEvent)
        {
            if (ReferenceEquals(eventPanel, null))
            {
                return;
            }
            if (mapEvent.kind != eventKind)
            {
                eventPanel.SetClass(GetEventClass(eventKind), false);
                eventKind = mapEvent.kind;
                eventPanel.SetClass(GetEventClass(eventKind), true);
                eventIcon?.SetAttribute("src", mapEvent.icon);
            }
            if (mapEvent.state != eventState)
            {
                eventState = mapEvent.state;
                eventPanel.SetClass("won", eventState == MapEventState.Won);
                eventPanel.SetClass("lost", eventState == MapEventState.Lost);
            }
            string title = mapEvent.title;
            if (!ReferenceEquals(title, eventTitleText))
            {
                eventTitleText = title;
                eventTitle?.SetText(title.ToUpperInvariant());
            }
            string goal = mapEvent.isOver ? (mapEvent.state == MapEventState.Won ? "Done." : "Failed.") : mapEvent.goal;
            if (goal != eventGoalText)
            {
                eventGoalText = goal;
                eventGoal?.SetText(goal);
            }
            int seconds = mapEvent.isOver ? 0 : (int)MathF.Ceiling(MathF.Max(0.0f, mapEvent.timeLeft));
            if (seconds != eventSeconds)
            {
                eventSeconds = seconds;
                eventTime?.SetText(mapEvent.isOver ? "" : UIText.Time(seconds));
            }
            float fill = Math.Clamp(mapEvent.progress, 0.0f, 1.0f);
            if (MathF.Abs(fill - eventFillWritten) > EVENT_FILL_EPSILON && !ReferenceEquals(eventFill, null))
            {
                eventFillWritten = fill;
                eventFill.value = fill;
            }
            if (!eventVisible)
            {
                eventVisible = true;
                eventPanel.SetClass("shown", true);
            }
        }

        public void HideEvent()
        {
            if (!eventVisible || ReferenceEquals(eventPanel, null))
            {
                return;
            }
            eventVisible = false;
            eventPanel.SetClass("shown", false);
        }

        public void RequestMarker(Vector3 world, MarkerKind kind)
        {"""),
    ("""                        if (requests[r].kind == MarkerKind.Chest)
                        {
                            Vector3 above = camera.WorldToScreenPoint(requests[r].world + new Vector3(0.0f, CHEST_ICON_HEIGHT, 0.0f));""",
     """                        float lift = GetFloatHeight(requests[r].kind);
                        if (lift > 0.0f)
                        {
                            Vector3 above = camera.WorldToScreenPoint(requests[r].world + new Vector3(0.0f, lift, 0.0f));"""),
    ("""        private static string GetMarkerClass(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Chest: return "chest";
                case MarkerKind.Boss: return "boss";
                case MarkerKind.Treasure: return "treasure";
                default: return "elite";
            }
        }

        private static string GetMarkerIcon(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Chest: return UIText.Icon("chest");
                case MarkerKind.Boss: return UIText.Icon("dread");
                case MarkerKind.Treasure: return UIText.Icon("xp");
                default: return UIText.Icon("skull");
            }
        }""", """        private static string GetMarkerClass(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Chest: return "chest";
                case MarkerKind.Boss: return "boss";
                case MarkerKind.Treasure: return "treasure";
                case MarkerKind.Shrine: return "shrine";
                case MarkerKind.Robber: return "robber";
                case MarkerKind.Pyres: return "pyres";
                case MarkerKind.Moonwell: return "moonwell";
                default: return "elite";
            }
        }

        private static string GetMarkerIcon(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Chest: return UIText.Icon("chest");
                case MarkerKind.Boss: return UIText.Icon("dread");
                case MarkerKind.Treasure: return UIText.Icon("xp");
                case MarkerKind.Shrine: return UIText.Icon("chest");
                case MarkerKind.Robber: return UIText.Icon("gold");
                case MarkerKind.Pyres: return UIText.Icon("skull");
                case MarkerKind.Moonwell: return UIText.Icon("mending");
                default: return UIText.Icon("skull");
            }
        }

        /// <summary>Metres above the target an on-screen badge floats for kinds that show one (chests, map events);
        /// 0 for kinds that show only off-screen arrows.</summary>
        private static float GetFloatHeight(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Chest: return CHEST_ICON_HEIGHT;
                case MarkerKind.Shrine:
                case MarkerKind.Robber:
                case MarkerKind.Pyres:
                case MarkerKind.Moonwell: return EVENT_ICON_HEIGHT;
                default: return 0.0f;
            }
        }

        private static string GetEventClass(MapEventKind kind)
        {
            switch (kind)
            {
                case MapEventKind.Shrine: return "shrine";
                case MapEventKind.Robber: return "robber";
                case MapEventKind.Pyres: return "pyres";
                case MapEventKind.Moonwell: return "moonwell";
                default: return "none";
            }
        }"""),
])

edit(os.path.join(UI, "Overlay.rhtml"), [
    ("""    markers, edge danger chevrons, the boss bar and event banners. Nothing here takes the pointer.""",
     """    markers, edge danger chevrons, the boss bar, event banners and the map event tracker. Nothing here takes
    the pointer."""),
    ("""        <div id="banner">""", """        <div id="event">
            <div id="event-head">
                <img id="event-icon" src="app:/data/UI/Icons/chest.png"/>
                <div id="event-title">SHRINE OF VIGIL</div>
                <div id="event-time" class="number">0:30</div>
            </div>
            <div id="event-goal">Hold the ring for 10 seconds.</div>
            <div id="event-frame"><progress id="event-fill" direction="right" value="0"/></div>
        </div>

        <div id="banner">"""),
])

edit(os.path.join(UI, "Overlay.rcss"), [
    (""".marker.chest .marker-arrow { image-color: #ffd98a; }
.marker.boss .marker-arrow { image-color: #ff4a2a; }
.marker.treasure .marker-arrow { image-color: #ff3b4e; }
""", """.marker.chest .marker-arrow { image-color: #ffd98a; }
.marker.boss .marker-arrow { image-color: #ff4a2a; }
.marker.treasure .marker-arrow { image-color: #ff3b4e; }
.marker.shrine .marker-arrow { image-color: #ffe6a8; }
.marker.robber .marker-arrow { image-color: #ffc247; }
.marker.pyres .marker-arrow { image-color: #78ff5c; }
.marker.moonwell .marker-arrow { image-color: #94d2ff; }
"""),
    (""".marker.inview .marker-arrow { visibility: hidden; }
""", """.marker.inview .marker-arrow { visibility: hidden; }
.marker.inview.pyres { border-color: #78ff5c; box-shadow: #58ff4c90 0 0 1.8rem 0.3rem; }
.marker.inview.moonwell { border-color: #94d2ff; box-shadow: #6cc0ff90 0 0 1.8rem 0.3rem; }
"""),
    ("""/* ---------- event banner (center top) ---------- */""", """/* ---------- map event tracker (right, under the gold) ---------- */

#event {
	position: absolute;
	top: 10.5rem;
	right: 2rem;
	width: 30rem;
	padding: 0.8rem 1.2rem 1rem 1.4rem;
	decorator: horizontal-gradient(#0c0a1200 #0c0a12e0);
	border-right: 0.3rem #ffd98a;
	visibility: hidden;
}

#event.shown {
	visibility: visible;
	animation: 0.4s back-out event-in;
}

@keyframes event-in {
	0% { opacity: 0; transform: translateX(3rem); }
	100% { opacity: 1; transform: translateX(0rem); }
}

#event-head {
	display: flex;
	flex-direction: row;
	align-items: center;
}

#event-icon {
	width: 2.8rem;
	height: 2.8rem;
	margin-right: 0.8rem;
}

#event-title {
	flex: 1 1 auto;
	font-family: "GT Heavy";
	font-size: 1.6rem;
	letter-spacing: 0.2rem;
	color: #ffd98a;
	font-effect: outline(2dp #140a04);
}

#event-time {
	font-size: 2rem;
	color: #ffffff;
	font-effect: outline(2dp #0c0a12);
}

#event-goal {
	margin: 0.4rem 0 0.6rem 0;
	font-size: 1.5rem;
	color: #ece4d4;
	font-effect: outline(2dp #0c0a12);
}

#event-frame {
	height: 1rem;
	border: 1dp #0a0810;
	border-radius: 0.5rem;
	background-color: #0c0a12d0;
}

#event-fill {
	display: block;
	width: 100%;
	height: 100%;
	border-radius: 0.5rem;
}

#event-fill fill {
	border-radius: 0.5rem;
	decorator: horizontal-gradient(#b8892e #ffe6a0);
}

#event.robber { border-color: #ffc247; }
#event.robber #event-title { color: #ffc247; }
#event.robber #event-fill fill { decorator: horizontal-gradient(#a8701c #ffd060); }
#event.pyres { border-color: #78ff5c; }
#event.pyres #event-title { color: #a8ff8c; }
#event.pyres #event-fill fill { decorator: horizontal-gradient(#2f8a24 #9aff7c); }
#event.moonwell { border-color: #94d2ff; }
#event.moonwell #event-title { color: #b8e2ff; }
#event.moonwell #event-fill fill { decorator: horizontal-gradient(#3a78b0 #b8e6ff); }
#event.won #event-title { color: #a8ff9a; }
#event.lost #event-title { color: #ff9a8a; }
#event.lost #event-fill fill { decorator: horizontal-gradient(#6a2018 #b04030); }

/* ---------- event banner (center top) ---------- */"""),
])

edit(os.path.join(UI, "phone.rcss"), [
    ("""body.phone #banner { top: 21rem; }
""", """body.phone #banner { top: 21rem; }
body.phone #event { top: 12rem; right: 1.6rem; width: 30rem; }
body.phone #event-title { font-size: 1.7rem; }
body.phone #event-goal { font-size: 1.7rem; }
body.phone #event-time { font-size: 2.2rem; }
"""),
])
print("map events patched")

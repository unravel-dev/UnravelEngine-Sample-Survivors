"""Chest beacon (design update 6): pickup query, run wiring, and the overlay's on-screen chest icon."""
import os

from patchlib import edit

ROOT = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(ROOT, "src", "Scripts")
U = os.path.join(ROOT, "src", "UI")


def p(*parts):
    return os.path.join(S, *parts)


edit(p("Pickups", "PickupWorld.cs"), [("""        /// <summary>The nearest pickup to a point (gems first), for the test autopilot.</summary>""",
                                       """        /// <summary>Every chest on the ground with its item count (chest beacons); returns how many were written.</summary>
        public int CollectChests(Span<Vector3> positionsOut, Span<float> itemsOut)
        {
            int written = 0;
            int capacity = Math.Min(positionsOut.Length, itemsOut.Length);
            for (int row = 0; row < rowCount && written < capacity; row++)
            {
                if (alive[row] && kinds[row] == PickupKind.Chest)
                {
                    positionsOut[written] = positions[row];
                    itemsOut[written] = values[row];
                    written++;
                }
            }
            return written;
        }

        /// <summary>The nearest pickup to a point (gems first), for the test autopilot.</summary>""")])

edit(p("Core", "Run.cs"), [
    ("""        public readonly CombatEvents combatEvents = new CombatEvents();""", """        public readonly CombatEvents combatEvents = new CombatEvents();
        /// <summary>Light pillars over the chests on the ground (created with the first run).</summary>
        private ChestBeacons chestBeacons;"""),
    ("""            loadout = new Loadout(this, chosen);
            Drops.Reset();""", """            loadout = new Loadout(this, chosen);
            chestBeacons ??= new ChestBeacons(this);
            chestBeacons.Reset();
            Drops.Reset();"""),
    ("""            loadout.Tick(dt);
            ShowMarkers();""", """            loadout.Tick(dt);
            chestBeacons.Tick(dt);
            ShowMarkers();"""),
])

edit(p("UI", "Screens", "OverlayScreen.cs"), [
    ("""        private readonly int[] markerAngle = new int[MARKER_COUNT];""", """        private readonly int[] markerAngle = new int[MARKER_COUNT];
        /// <summary>The marker shows a chest on screen: its icon bobs above it and the arrow hides.</summary>
        private readonly bool[] markerInView = new bool[MARKER_COUNT];
        /// <summary>Metres above a chest its on-screen icon floats.</summary>
        private const float CHEST_ICON_HEIGHT = 2.6f;"""),
    ("""                    Vector3 screen = camera.WorldToScreenPoint(requests[r].world);
                    bool behind = screen.z <= 0.0f;
                    if (!behind && screen.x >= 0.0f && screen.x <= width && screen.y >= 0.0f && screen.y <= height)
                    {
                        continue;
                    }""", """                    Vector3 screen = camera.WorldToScreenPoint(requests[r].world);
                    bool behind = screen.z <= 0.0f;
                    if (!behind && screen.x >= 0.0f && screen.x <= width && screen.y >= 0.0f && screen.y <= height)
                    {
                        if (requests[r].kind == MarkerKind.Chest)
                        {
                            Vector3 above = camera.WorldToScreenPoint(requests[r].world + new Vector3(0.0f, CHEST_ICON_HEIGHT, 0.0f));
                            PlaceMarker(shown, new Vector2(above.x, above.y), new Vector2(0.0f, -1.0f), requests[r].kind, pixelsPerDp, true);
                            shown++;
                        }
                        continue;
                    }"""),
    ("""                    PlaceMarker(shown, edge, toward, requests[r].kind, pixelsPerDp);""",
     """                    PlaceMarker(shown, edge, toward, requests[r].kind, pixelsPerDp, false);"""),
    ("""        private void PlaceMarker(int index, Vector2 position, Vector2 direction, MarkerKind kind, float pixelsPerDp)
        {
            UIElement marker = markers[index];
            if (ReferenceEquals(marker, null))
            {
                return;
            }""", """        private void PlaceMarker(int index, Vector2 position, Vector2 direction, MarkerKind kind, float pixelsPerDp, bool inView)
        {
            UIElement marker = markers[index];
            if (ReferenceEquals(marker, null))
            {
                return;
            }
            if (inView != markerInView[index])
            {
                markerInView[index] = inView;
                marker.SetClass("inview", inView);
            }"""),
])

edit(os.path.join(U, "Overlay.rcss"), [(""".marker.treasure .marker-arrow { image-color: #ff3b4e; }""", """.marker.treasure .marker-arrow { image-color: #ff3b4e; }

/* A chest on screen: its icon bobs above it, over the horde, without the arrow. */
.marker.inview .marker-arrow { visibility: hidden; }
.marker.inview .marker-icon {
	width: 4.2rem;
	height: 4.2rem;
	margin-left: -2.1rem;
	margin-top: -2.1rem;
	animation: 0.7s sine-in-out infinite alternate marker-bob;
}

@keyframes marker-bob {
	0% { transform: translateY(0rem) scale(1); }
	100% { transform: translateY(-0.9rem) scale(1.08); }
}""")])
print("beacon patched")

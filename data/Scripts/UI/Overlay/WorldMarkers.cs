using System;
using System.Collections.Generic;
using System.Globalization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Everything the overlay pins to the world, from pools in the document: off-screen markers (an arrow at the
    /// screen edge toward the target; chests and map events on screen get a badge bobbing above them instead),
    /// health bars over elites' heads, and danger chevrons at the screen edge toward a direction from the hero.
    /// Markers and elite bars are requested every frame and hide when not requested again; chevrons last a few
    /// seconds. Rotations are cached transform strings, so a frame allocates nothing. Part of the overlay. Main thread.
    /// </summary>
    internal sealed class WorldMarkers
    {
        private const int MARKER_COUNT = 8;
        private const int ELITE_BAR_COUNT = 8;
        private const int EDGE_COUNT = 4;
        /// <summary>Pixels beyond the screen edge an elite bar may sit before it hides.</summary>
        private const float ELITE_BAR_MARGIN_PIXELS = 40.0f;
        /// <summary>Rotations come in steps of 360 / ANGLE_STEPS degrees.</summary>
        private const int ANGLE_STEPS = 72;
        private const int NO_ANGLE = -1;
        private const float EDGE_MARGIN_REM = 6.0f;
        private const float MARKER_MARGIN_REM = 4.0f;
        /// <summary>A chevron lasts at least this long.</summary>
        private const float EDGE_MIN_SECONDS = 0.5f;
        /// <summary>Metres from the hero toward the danger that set the chevron's screen direction.</summary>
        private const float EDGE_PROBE_METRES = 5.0f;
        /// <summary>Metres above a chest its on-screen badge floats.</summary>
        private const float CHEST_ICON_HEIGHT = 3.4f;
        /// <summary>Metres above a map event (or its robber) its on-screen badge floats.</summary>
        private const float EVENT_ICON_HEIGHT = 4.2f;
        private const float MIN_DIRECTION_SQUARED = 1e-4f;
        private const float MIN_WORLD_DIRECTION_SQUARED = 1e-6f;

        private static readonly string[] rotations = BuildRotations();

        private struct MarkerRequest
        {
            public Vector3 world;
            public MarkerKind kind;
        }

        private struct EliteBarRequest
        {
            public Vector3 world;
            public float fraction;
        }

        private struct Marker
        {
            public UIElement element;
            public UIElement arrow;
            public UIElement icon;
            public VisibilityWriter visibility;
            public MarkerKind kind;
            public int angle;
            /// <summary>Shows a chest, a power-up or a map event on screen: its badge bobs above it and the arrow hides.</summary>
            public bool isInView;
        }

        private struct EliteBar
        {
            public UIElement element;
            public ProgressWriter fill;
            public VisibilityWriter visibility;
        }

        private struct Edge
        {
            public UIElement element;
            public VisibilityWriter visibility;
            public Vector3 direction;
            public float remaining;
            public int angle;
        }

        private readonly MarkerRequest[] markerRequests = new MarkerRequest[MARKER_COUNT];
        private readonly EliteBarRequest[] eliteRequests = new EliteBarRequest[ELITE_BAR_COUNT];
        private readonly Marker[] markers = new Marker[MARKER_COUNT];
        private readonly EliteBar[] eliteBars = new EliteBar[ELITE_BAR_COUNT];
        private readonly Edge[] edges = new Edge[EDGE_COUNT];
        private int markerRequestCount;
        private int eliteRequestCount;

        /// <summary>Fetches the pools of a freshly loaded overlay document.</summary>
        public void Bind(UIDocument document)
        {
            List<UIElement> markerList = document.QueryAll(".marker");
            for (int i = 0; i < MARKER_COUNT; i++)
            {
                UIElement element = i < markerList.Count ? markerList[i] : null;
                markers[i] = new Marker
                {
                    element = element,
                    arrow = element?.Q(".marker-arrow"),
                    icon = element?.Q(".marker-icon"),
                    kind = MarkerKind.Elite,
                    angle = NO_ANGLE,
                };
                markers[i].visibility.Bind(element);
            }
            List<UIElement> barList = document.QueryAll(".elitebar");
            for (int i = 0; i < ELITE_BAR_COUNT; i++)
            {
                UIElement element = i < barList.Count ? barList[i] : null;
                eliteBars[i].element = element;
                eliteBars[i].fill.Bind(element?.Q<UIProgress>(".elitebar-fill"));
                eliteBars[i].visibility.Bind(element);
            }
            List<UIElement> edgeList = document.QueryAll(".edge");
            for (int i = 0; i < EDGE_COUNT; i++)
            {
                edges[i].element = i < edgeList.Count ? edgeList[i] : null;
                edges[i].visibility.Bind(edges[i].element);
                edges[i].angle = NO_ANGLE;
            }
        }

        /// <summary>A new run: no chevrons left from the last one.</summary>
        public void Reset()
        {
            for (int i = 0; i < EDGE_COUNT; i++)
            {
                edges[i].remaining = 0.0f;
            }
        }

        /// <summary>Drops this frame's requests (the overlay is hidden or not loaded).</summary>
        public void ClearRequests()
        {
            markerRequestCount = 0;
            eliteRequestCount = 0;
        }

        /// <summary>One marker this frame; calls beyond MARKER_COUNT are ignored.</summary>
        public void RequestMarker(Vector3 world, MarkerKind kind)
        {
            if (markerRequestCount >= MARKER_COUNT)
            {
                return;
            }
            markerRequests[markerRequestCount] = new MarkerRequest { world = world, kind = kind };
            markerRequestCount++;
        }

        /// <summary>One elite health bar this frame; calls beyond ELITE_BAR_COUNT are ignored.</summary>
        public void RequestEliteBar(Vector3 worldHead, float healthFraction)
        {
            if (eliteRequestCount >= ELITE_BAR_COUNT)
            {
                return;
            }
            eliteRequests[eliteRequestCount] = new EliteBarRequest { world = worldHead, fraction = Math.Clamp(healthFraction, 0.0f, 1.0f) };
            eliteRequestCount++;
        }

        /// <summary>Chevrons toward a world direction from the hero for some seconds, in a free slot (else the one
        /// closest to expiring); false for a direction without a ground component.</summary>
        public bool AddEdgeWarning(Vector3 direction, float seconds)
        {
            direction.y = 0.0f;
            if (direction.sqrMagnitude < MIN_WORLD_DIRECTION_SQUARED)
            {
                return false;
            }
            int slot = FindEdgeSlot();
            edges[slot].direction = direction.normalized;
            edges[slot].remaining = MathF.Max(EDGE_MIN_SECONDS, seconds);
            return true;
        }

        /// <summary>Places this frame's requests and the live chevrons; every frame.</summary>
        public void Tick(Run run, float dt, float pixelsPerDp, float remPixels)
        {
            CameraComponent camera = run.gameCamera;
            bool isPlaying = !ReferenceEquals(camera, null) && run.phase == RunPhase.Playing;
            Vector2 viewport = isPlaying ? GetViewport(camera) : Vector2.zero;
            PlaceEliteBars(camera, isPlaying, viewport, pixelsPerDp);
            PlaceMarkers(camera, isPlaying, viewport, pixelsPerDp, MARKER_MARGIN_REM * remPixels);
            PlaceEdges(run, dt, pixelsPerDp, EDGE_MARGIN_REM * remPixels);
            ClearRequests();
        }

        private int FindEdgeSlot()
        {
            int slot = 0;
            float lowest = float.MaxValue;
            for (int i = 0; i < EDGE_COUNT; i++)
            {
                if (edges[i].remaining <= 0.0f)
                {
                    return i;
                }
                if (edges[i].remaining < lowest)
                {
                    lowest = edges[i].remaining;
                    slot = i;
                }
            }
            return slot;
        }

        // ---------------------------------------------------------------------------------------------------
        // Elite bars
        // ---------------------------------------------------------------------------------------------------

        /// <summary>Places this frame's elite bars over their heads; bars without a request, or off screen, hide.</summary>
        private void PlaceEliteBars(CameraComponent camera, bool isPlaying, Vector2 viewport, float pixelsPerDp)
        {
            int shown = 0;
            for (int r = 0; isPlaying && r < eliteRequestCount && shown < ELITE_BAR_COUNT; r++)
            {
                Vector3 screen = camera.WorldToScreenPoint(eliteRequests[r].world);
                if (screen.z <= 0.0f || !IsNearViewport(screen, viewport, ELITE_BAR_MARGIN_PIXELS))
                {
                    continue;
                }
                ref EliteBar bar = ref eliteBars[shown];
                if (ReferenceEquals(bar.element, null))
                {
                    break;
                }
                bar.element.style.left = screen.x / pixelsPerDp;
                bar.element.style.top = screen.y / pixelsPerDp;
                bar.fill.Write(eliteRequests[r].fraction);
                bar.visibility.Write(true);
                shown++;
            }
            for (int i = shown; i < ELITE_BAR_COUNT; i++)
            {
                eliteBars[i].visibility.Write(false);
            }
        }

        private static bool IsNearViewport(Vector3 screen, Vector2 viewport, float margin)
        {
            return screen.x >= -margin && screen.x <= viewport.x + margin && screen.y >= -margin && screen.y <= viewport.y + margin;
        }

        // ---------------------------------------------------------------------------------------------------
        // Markers
        // ---------------------------------------------------------------------------------------------------

        private void PlaceMarkers(CameraComponent camera, bool isPlaying, Vector2 viewport, float pixelsPerDp, float margin)
        {
            int shown = 0;
            Vector2 center = viewport * 0.5f;
            for (int r = 0; isPlaying && r < markerRequestCount && shown < MARKER_COUNT; r++)
            {
                MarkerRequest request = markerRequests[r];
                Vector3 screen = camera.WorldToScreenPoint(request.world);
                bool isBehind = screen.z <= 0.0f;
                if (!isBehind && screen.x >= 0.0f && screen.x <= viewport.x && screen.y >= 0.0f && screen.y <= viewport.y)
                {
                    if (TryPlaceBadge(shown, camera, request, viewport, pixelsPerDp, margin))
                    {
                        shown++;
                    }
                    continue;
                }
                Vector2 toward = new Vector2(screen.x, screen.y) - center;
                if (isBehind)
                {
                    toward = -toward;
                }
                if (toward.sqrMagnitude < MIN_DIRECTION_SQUARED)
                {
                    continue;
                }
                Vector2 edge = ClampToEdge(center, toward, viewport, margin);
                PlaceMarker(shown, edge, toward, request.kind, pixelsPerDp, false);
                shown++;
            }
            for (int i = shown; i < MARKER_COUNT; i++)
            {
                markers[i].visibility.Write(false);
            }
        }

        /// <summary>An on-screen target of a kind with a badge (chests, map events): the badge floats above it, kept
        /// on screen near the top edge. False for kinds that show only off-screen arrows.</summary>
        private bool TryPlaceBadge(int index, CameraComponent camera, MarkerRequest request, Vector2 viewport, float pixelsPerDp, float margin)
        {
            float lift = GetBadgeHeight(request.kind);
            if (lift <= 0.0f)
            {
                return false;
            }
            Vector3 above = camera.WorldToScreenPoint(request.world + new Vector3(0.0f, lift, 0.0f));
            float badgeY = Math.Clamp(above.y, margin, viewport.y - margin);
            PlaceMarker(index, new Vector2(above.x, badgeY), new Vector2(0.0f, -1.0f), request.kind, pixelsPerDp, true);
            return true;
        }

        private void PlaceMarker(int index, Vector2 position, Vector2 direction, MarkerKind kind, float pixelsPerDp, bool isInView)
        {
            ref Marker marker = ref markers[index];
            if (ReferenceEquals(marker.element, null))
            {
                return;
            }
            if (isInView != marker.isInView)
            {
                marker.isInView = isInView;
                marker.element.SetClass("inview", isInView);
            }
            marker.element.style.left = position.x / pixelsPerDp;
            marker.element.style.top = position.y / pixelsPerDp;
            int angle = GetAngleStep(direction);
            if (angle != marker.angle)
            {
                marker.angle = angle;
                marker.arrow.style["transform"] = rotations[angle];
            }
            if (kind != marker.kind)
            {
                marker.element.SetClass(GetMarkerClass(marker.kind), false);
                marker.kind = kind;
                marker.element.SetClass(GetMarkerClass(kind), true);
                marker.icon.SetAttribute("src", GetMarkerIcon(kind));
            }
            marker.visibility.Write(true);
        }

        // ---------------------------------------------------------------------------------------------------
        // Edge chevrons
        // ---------------------------------------------------------------------------------------------------

        private void PlaceEdges(Run run, float dt, float pixelsPerDp, float margin)
        {
            CameraComponent camera = run.gameCamera;
            HeroController hero = run.hero;
            for (int i = 0; i < EDGE_COUNT; i++)
            {
                ref Edge edge = ref edges[i];
                if (ReferenceEquals(edge.element, null))
                {
                    continue;
                }
                edge.remaining -= dt;
                bool isShown = edge.remaining > 0.0f && hero != null && !ReferenceEquals(camera, null) &&
                               TryPlaceEdge(ref edge, camera, hero.position, pixelsPerDp, margin);
                edge.visibility.Write(isShown);
            }
        }

        /// <summary>Points the chevrons at the screen edge along the danger's direction as the camera sees it.</summary>
        private static bool TryPlaceEdge(ref Edge edge, CameraComponent camera, Vector3 heroPosition, float pixelsPerDp, float margin)
        {
            Vector3 from = camera.WorldToScreenPoint(heroPosition);
            Vector3 to = camera.WorldToScreenPoint(heroPosition + edge.direction * EDGE_PROBE_METRES);
            Vector2 toward = new Vector2(to.x - from.x, to.y - from.y);
            if (from.z <= 0.0f || to.z <= 0.0f || toward.sqrMagnitude <= MIN_DIRECTION_SQUARED)
            {
                return false;
            }
            Vector2 viewport = GetViewport(camera);
            Vector2 position = ClampToEdge(viewport * 0.5f, toward, viewport, margin);
            edge.element.style.left = position.x / pixelsPerDp;
            edge.element.style.top = position.y / pixelsPerDp;
            int angle = GetAngleStep(toward);
            if (angle != edge.angle)
            {
                edge.angle = angle;
                edge.element.style["transform"] = rotations[angle];
            }
            return true;
        }

        // ---------------------------------------------------------------------------------------------------
        // Geometry and kinds
        // ---------------------------------------------------------------------------------------------------

        /// <summary>The camera's viewport in pixels: the space of WorldToScreenPoint and of the document.</summary>
        private static Vector2 GetViewport(CameraComponent camera)
        {
            Vector2 size = camera.viewportSize;
            return size.x > 0.0f && size.y > 0.0f ? size : new Vector2(Screen.width, Screen.height);
        }

        /// <summary>Where a ray from the center along <paramref name="direction"/> meets the viewport rectangle inset
        /// by <paramref name="margin"/>.</summary>
        private static Vector2 ClampToEdge(Vector2 center, Vector2 direction, Vector2 viewport, float margin)
        {
            float halfWidth = MathF.Max(1.0f, viewport.x * 0.5f - margin);
            float halfHeight = MathF.Max(1.0f, viewport.y * 0.5f - margin);
            float sx = MathF.Abs(direction.x) > 1e-5f ? halfWidth / MathF.Abs(direction.x) : float.MaxValue;
            float sy = MathF.Abs(direction.y) > 1e-5f ? halfHeight / MathF.Abs(direction.y) : float.MaxValue;
            return center + direction * MathF.Min(sx, sy);
        }

        /// <summary>The direction's angle as an index into the cached rotations.</summary>
        private static int GetAngleStep(Vector2 direction)
        {
            float degrees = MathF.Atan2(direction.y, direction.x) * 180.0f / MathF.PI;
            if (degrees < 0.0f)
            {
                degrees += 360.0f;
            }
            return (int)MathF.Round(degrees / (360.0f / ANGLE_STEPS)) % ANGLE_STEPS;
        }

        private static string[] BuildRotations()
        {
            var result = new string[ANGLE_STEPS];
            for (int i = 0; i < ANGLE_STEPS; i++)
            {
                result[i] = "rotate(" + (i * 360 / ANGLE_STEPS).ToString(CultureInfo.InvariantCulture) + "deg)";
            }
            return result;
        }

        private static string GetMarkerClass(MarkerKind kind)
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
                case MarkerKind.Food: return "food";
                case MarkerKind.Magnet: return "magnet";
                case MarkerKind.Bell: return "bell";
                case MarkerKind.Frost: return "frost";
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
                case MarkerKind.Food: return UIText.Icon("drop_food");
                case MarkerKind.Magnet: return UIText.Icon("drop_magnet");
                case MarkerKind.Bell: return UIText.Icon("drop_bell");
                case MarkerKind.Frost: return UIText.Icon("drop_frost");
                default: return UIText.Icon("skull");
            }
        }

        /// <summary>Metres above the target an on-screen badge floats for kinds that show one (chests, power-ups, map events);
        /// 0 for kinds that show only off-screen arrows.</summary>
        private static float GetBadgeHeight(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Chest:
                case MarkerKind.Food:
                case MarkerKind.Magnet:
                case MarkerKind.Bell:
                case MarkerKind.Frost: return CHEST_ICON_HEIGHT;
                case MarkerKind.Shrine:
                case MarkerKind.Robber:
                case MarkerKind.Pyres:
                case MarkerKind.Moonwell: return EVENT_ICON_HEIGHT;
                default: return 0.0f;
            }
        }
    }
}

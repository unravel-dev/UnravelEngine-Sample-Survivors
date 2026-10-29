using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The optional challenges of the moor.</summary>
    public enum MapEventKind
    {
        Shrine,
        Robber,
        Pyres,
        Moonwell,
        Count,
    }

    /// <summary>
    /// Map events: one optional challenge at a time between the director's waves. The first comes at 1:30, the next
    /// 70-100 s after the last one ended; none starts while a mini-boss fights or after 12:30 (the Last Tide and the
    /// Hollow King own the end). Each is placed 18-24 m from the hero on open ground (the robber in view), announced
    /// with a banner, marked over the horde on screen and by an arrow off it, and tracked on the overlay: title, goal,
    /// progress and clock. The Moonwell comes more often while the hero is hurt; the same kind never comes twice in a
    /// row. Main thread.
    /// </summary>
    public sealed class MapEvents
    {
        private const float FIRST_SECOND = 90.0f;
        private const float GAP_MIN = 70.0f;
        private const float GAP_MAX = 100.0f;
        private const float LAST_START_SECOND = 12.5f * 60.0f;
        /// <summary>When an event may not start now (a mini-boss fights), the next try comes this much later.</summary>
        private const float RETRY_SECONDS = 5.0f;
        private const float PLACE_DISTANCE_MIN = 18.0f;
        private const float PLACE_DISTANCE_MAX = 24.0f;
        private const float ROBBER_DISTANCE = 12.0f;
        private const float DEV_DISTANCE = 9.0f;
        /// <summary>Ground kept clear of arena obstacles around an event (m).</summary>
        private const float CLEAR_RADIUS = 6.0f;
        private const float EDGE_MARGIN = 10.0f;
        private const int PLACE_TRIES = 16;
        /// <summary>Seconds an ended event stays on the tracker (won, lost) before it goes.</summary>
        private const float LINGER_SECONDS = 2.5f;
        private const float HURT_HEALTH = 0.7f;
        private const float MOONWELL_WEIGHT_HURT = 1.5f;
        private const float MOONWELL_WEIGHT_HEALTHY = 0.35f;
        private const float OTHER_WEIGHT = 1.0f;
        private const float FULL_TURN_DEGREES = 360.0f;
        private const float MIN_HEADING_SQUARED = 0.01f;

        private readonly Run run;
        private readonly EventFx fx;
        private MapEvent current;
        private MapEventKind lastKind = MapEventKind.Count;
        private float nextAt = FIRST_SECOND;

        /// <summary>The event on the moor, or null.</summary>
        public MapEvent active => current;

        public MapEvents(Run run)
        {
            this.run = run;
            fx = new EventFx(run.effects);
        }

        /// <summary>A new run: no event, the first one at 1:30.</summary>
        public void Reset()
        {
            Clear();
            lastKind = MapEventKind.Count;
            nextAt = FIRST_SECOND;
        }

        /// <summary>Removes the event and what it placed (run end, a new run).</summary>
        public void Clear()
        {
            current?.Clear();
            current = null;
            run.ui.HideEventTracker();
        }

        /// <summary>Once per frame while playing, before the markers.</summary>
        public void Tick(float dt)
        {
            if (current == null)
            {
                if (run.runTime >= nextAt)
                {
                    TryStart();
                }
                return;
            }
            current.Step(dt);
            if (current.state == MapEventState.Expired || current.overSeconds >= LINGER_SECONDS)
            {
                End();
                return;
            }
            run.ui.ShowEventTracker(current);
            if (!current.isOver)
            {
                run.ui.ShowMarker(current.markerPosition, current.markerKind);
            }
        }

        /// <summary>Draws the event's props (shrine, pyres).</summary>
        public void Draw()
        {
            current?.Draw();
        }

        public void OnEnemyDied(in EnemyDeath death)
        {
            current?.OnEnemyDied(death);
        }

        /// <summary>Whether an enemy row belongs to the event (the robber): the event's marker stands for it.</summary>
        public bool Owns(int row) => current != null && current.Owns(row);

        /// <summary>Dev: starts an event of a kind 9 m ahead of the hero now, whatever the clock says.</summary>
        public void DevStart(MapEventKind kind)
        {
            Clear();
            Vector3 place = run.director.ClampToArena(run.hero.position + GetHeading() * DEV_DISTANCE);
            Start(kind, place);
        }

        private void TryStart()
        {
            if (run.runTime >= LAST_START_SECOND)
            {
                nextAt = float.MaxValue;
                return;
            }
            if (run.director.bosses.isMiniBossAlive || run.director.isBossActive || !run.hero.isAlive)
            {
                nextAt = run.runTime + RETRY_SECONDS;
                return;
            }
            MapEventKind kind = PickKind();
            float distance = kind == MapEventKind.Robber ? ROBBER_DISTANCE : PLACE_DISTANCE_MIN;
            float spread = kind == MapEventKind.Robber ? 0.0f : PLACE_DISTANCE_MAX - PLACE_DISTANCE_MIN;
            if (!TryPlace(distance, spread, out Vector3 place))
            {
                nextAt = run.runTime + RETRY_SECONDS;
                return;
            }
            Start(kind, place);
        }

        private void Start(MapEventKind kind, Vector3 place)
        {
            current = Create(kind, place);
            lastKind = kind;
        }

        private MapEvent Create(MapEventKind kind, Vector3 place) => kind switch
        {
            MapEventKind.Shrine => new ShrineEvent(run, fx, place),
            MapEventKind.Robber => new RobberEvent(run, fx, place),
            MapEventKind.Pyres => new PyresEvent(run, fx, place),
            MapEventKind.Moonwell => new MoonwellEvent(run, fx, place),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a map event."),
        };

        private void End()
        {
            current.Clear();
            current = null;
            run.ui.HideEventTracker();
            nextAt = run.runTime + GAP_MIN + (float)run.rng.NextDouble() * (GAP_MAX - GAP_MIN);
        }

        private MapEventKind PickKind()
        {
            Span<float> weights = stackalloc float[(int)MapEventKind.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = GetWeight((MapEventKind)i);
            }
            int picked = WeightedPick.Pick(run.rng, weights);
            return picked >= 0 ? (MapEventKind)picked : MapEventKind.Shrine;
        }

        /// <summary>Never the same kind twice in a row; the Moonwell comes more often while the hero is hurt.</summary>
        private float GetWeight(MapEventKind kind)
        {
            if (kind == lastKind)
            {
                return 0.0f;
            }
            if (kind == MapEventKind.Moonwell)
            {
                return run.hero.healthFraction < HURT_HEALTH ? MOONWELL_WEIGHT_HURT : MOONWELL_WEIGHT_HEALTHY;
            }
            return OTHER_WEIGHT;
        }

        /// <summary>A spot at a distance from the hero, inside the arena and clear of its obstacles.</summary>
        private bool TryPlace(float distance, float spread, out Vector3 place)
        {
            Vector3 hero = run.hero.position;
            float limit = run.arenaRadius - EDGE_MARGIN;
            for (int i = 0; i < PLACE_TRIES; i++)
            {
                float angle = (float)run.rng.NextDouble() * FULL_TURN_DEGREES;
                float reach = distance + (float)run.rng.NextDouble() * spread;
                place = hero + Ground.FromYaw(angle) * reach;
                place.y = 0.0f;
                if (place.magnitude <= limit && run.IsOpenGround(place, CLEAR_RADIUS))
                {
                    return true;
                }
            }
            place = hero;
            return false;
        }

        private Vector3 GetHeading()
        {
            Vector3 direction = run.hero.lastMoveDirection;
            direction.y = 0.0f;
            return direction.sqrMagnitude > MIN_HEADING_SQUARED ? direction.normalized : Vector3.forward;
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Where a map event stands.</summary>
    public enum MapEventState
    {
        /// <summary>Placed and marked, waiting for the hero; it fades when ignored.</summary>
        Waiting,
        /// <summary>Started: its clock runs.</summary>
        Active,
        Won,
        Lost,
        /// <summary>Ignored until it faded.</summary>
        Expired,
    }

    /// <summary>
    /// One map event (<see cref="MapEvents"/>): placed at its center, stepped once per frame until it is won, lost or
    /// ignored. Waiting it counts down <see cref="WAIT_SECONDS"/>; started, its own limit. Kinds fill in the rules,
    /// the looks (steady looks are re-emitted every <see cref="EventFx.REFRESH"/> s) and the text of the tracker; the
    /// base gives them the banners, the start, win and loss moments, the waiting look and the surges of the dead.
    /// Main thread.
    /// </summary>
    public abstract class MapEvent
    {
        /// <summary>Seconds a placed event waits for the hero before it fades.</summary>
        protected const float WAIT_SECONDS = 40.0f;
        private const float BANNER_SECONDS = 2.5f;
        private const float WIN_SHAKE = 0.25f;
        private const float WIN_SHAKE_SECONDS = 0.3f;

        protected readonly Run run;
        protected readonly EventFx fx;
        protected readonly Vector3 center;
        private float refreshTimer;
        private float surgeTimer;

        public MapEventState state = MapEventState.Waiting;
        /// <summary>Seconds left: to come (waiting) or to finish (started).</summary>
        public float timeLeft = WAIT_SECONDS;
        /// <summary>The tracker's bar (0..1).</summary>
        public float progress;
        /// <summary>Seconds since the event was won or lost.</summary>
        public float overSeconds;

        /// <summary>Won, lost or faded.</summary>
        public bool isOver => state >= MapEventState.Won;
        public abstract MapEventKind kind { get; }
        public abstract string title { get; }
        /// <summary>What to do now, one short sentence for the tracker.</summary>
        public abstract string goal { get; }
        public abstract Color color { get; }
        /// <summary>The tracker's icon: what the event pays.</summary>
        public abstract string icon { get; }
        /// <summary>The marker over the event on screen and at the edge off it.</summary>
        public abstract MarkerKind markerKind { get; }
        /// <summary>Where the marker points.</summary>
        public virtual Vector3 markerPosition => center;

        /// <summary>A wave of the dead an event calls: how often, how many (a base plus some per minute), how far out.</summary>
        protected readonly struct SurgeRule
        {
            public readonly float interval;
            public readonly int baseCount;
            public readonly float perMinute;
            public readonly float radius;

            public SurgeRule(float interval, int baseCount, float perMinute, float radius)
            {
                this.interval = interval;
                this.baseCount = baseCount;
                this.perMinute = perMinute;
                this.radius = radius;
            }
        }

        protected MapEvent(Run run, EventFx fx, Vector3 center)
        {
            this.run = run;
            this.fx = fx;
            this.center = center;
        }

        /// <summary>Runs the clock and the rules; called by MapEvents once per frame while playing.</summary>
        public void Step(float dt)
        {
            if (isOver)
            {
                overSeconds += dt;
                return;
            }
            timeLeft -= dt;
            refreshTimer -= dt;
            bool refresh = refreshTimer <= 0.0f;
            refreshTimer = refresh ? EventFx.REFRESH : refreshTimer;
            Tick(dt, refresh);
            if (isOver || timeLeft > 0.0f)
            {
                return;
            }
            if (state == MapEventState.Waiting)
            {
                Expire();
            }
            else
            {
                TimeOut();
            }
        }

        /// <summary>The rules for one frame; <paramref name="refresh"/> is set when the steady looks are due.</summary>
        protected abstract void Tick(float dt, bool refresh);

        /// <summary>The started event's clock ran out.</summary>
        protected abstract void TimeOut();

        /// <summary>Draws the event's props, if it has any.</summary>
        public virtual void Draw()
        {
        }

        public virtual void OnEnemyDied(in EnemyDeath death)
        {
        }

        /// <summary>Removes what the event placed.</summary>
        public virtual void Clear()
        {
        }

        /// <summary>Whether an enemy row belongs to the event (its own marker stands for it).</summary>
        public virtual bool Owns(int row) => false;

        /// <summary>Ignored: it fades without a word.</summary>
        protected virtual void Expire()
        {
            state = MapEventState.Expired;
            fx.PlayBurst(fx.fade, center, color);
        }

        /// <summary>Tells the player a new event is on the moor.</summary>
        protected void Announce(string banner)
        {
            run.ui.ShowBanner(banner, BANNER_SECONDS);
        }

        /// <summary>Starts the clock with a banner, the bell and a burst of the event's colour; the first surge comes at once.</summary>
        protected void Activate(float seconds, string banner)
        {
            state = MapEventState.Active;
            timeLeft = seconds;
            surgeTimer = 0.0f;
            run.ui.ShowBanner(banner, BANNER_SECONDS, true);
            run.sounds.Play("bell_toll");
            fx.PlayBurst(fx.start, center, color);
        }

        protected void Win(string banner)
        {
            state = MapEventState.Won;
            run.ui.ShowBanner(banner, BANNER_SECONDS);
            run.sounds.Play("reel_land_evolution");
            run.feedback.Shake(WIN_SHAKE, WIN_SHAKE_SECONDS);
            fx.PlayBurst(fx.win, center, color);
        }

        protected void Lose(string banner)
        {
            state = MapEventState.Lost;
            run.ui.ShowBanner(banner, BANNER_SECONDS, true);
            run.sounds.PlayAt("grave_burst", center);
            fx.PlayBurst(fx.fade, center, color);
        }

        /// <summary>The look of an event waiting for the hero: its pillar and its zone in its colour.</summary>
        protected void ShowWaiting(float radius, float glow)
        {
            fx.Beacon(center, color);
            fx.Zone(center, radius, color, glow);
        }

        /// <summary>Counts the surge clock down; when it runs out, the dead of the minute's mix rise on a ring around the
        /// event and march on its center.</summary>
        protected void TickSurge(float dt, in SurgeRule rule)
        {
            surgeTimer -= dt;
            if (surgeTimer > 0.0f)
            {
                return;
            }
            surgeTimer = rule.interval;
            int count = rule.baseCount + (int)(rule.perMinute * run.minutes);
            run.director.SpawnRingAround(center, run.director.PickArchetype(), count, rule.radius);
        }

        protected bool IsHeroWithin(float radius)
        {
            HeroController hero = run.hero;
            return hero != null && hero.isAlive && Ground.DistanceSquared(hero.position, center) <= radius * radius;
        }

        protected static Color Dim(Color color, float factor) => new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// "The dead close in": a trap, not a wave. Tough dead (far more health than their kind, a darker and larger tier) rise on
    /// circles around the hero and march in, and a ring of soul fire closes in behind them: a dash or a blink through it
    /// burns the hero and throws them back. The only way out is through: the ring breaks when most of the dead have fallen,
    /// and pays a gem worth most of a level and a roast. If time runs out first the fire just fades, paying nothing. No
    /// ring rises while a boss fights. Main thread; the director owns it.
    /// </summary>
    public sealed class DeadRing
    {
        /// <summary>The fire starts this far outside the outermost dead and closes to this radius as they march in.</summary>
        private const float WARD_MARGIN = 1.5f;
        private const float WARD_CLOSED_RADIUS = 8.0f;
        private const float WARD_CLOSE_SECONDS = 8.0f;
        /// <summary>The ring breaks once this share of its dead has fallen.</summary>
        private const float BREAK_SHARE = 0.6f;
        private const float TIME_LIMIT = 24.0f;
        /// <summary>The gem of a broken ring, as a share of the experience to the next level.</summary>
        private const float REWARD_LEVEL_SHARE = 0.6f;
        private const float BREAK_BANNER_SECONDS = 2.5f;
        private const float BREAK_SHAKE = 0.4f;
        private const float BREAK_SHAKE_SECONDS = 0.4f;

        private static readonly Color SOUL_GREEN = new Color(0.45f, 1.0f, 0.42f, 1.0f);

        private readonly Run run;
        private readonly EventFx fx;
        private readonly List<Enemy> dead = new List<Enemy>();
        private int startCount;
        private float timeLeft;

        public DeadRing(Run run)
        {
            this.run = run;
            fx = new EventFx(run.effects);
            ward = new BarrowWard(run);
        }

        /// <summary>The fire around the trap.</summary>
        public BarrowWard ward { get; }

        /// <summary>Whether the trap holds the hero.</summary>
        public bool isActive => ward.isUp;

        /// <summary>The dead still standing and how many rose, for test reports; "-" with no ring.</summary>
        public string report => isActive ? $"{CountStanding()}/{startCount}" : "-";

        /// <summary>The fire rises around a point, just outside the outermost dead.</summary>
        public void Begin(Vector3 center, float outerRadius)
        {
            dead.Clear();
            startCount = 0;
            timeLeft = TIME_LIMIT;
            ward.Raise(center, outerRadius + WARD_MARGIN, SOUL_GREEN, false);
            ward.CloseTo(WARD_CLOSED_RADIUS, WARD_CLOSE_SECONDS);
        }

        /// <summary>One of the dead belongs to the trap: its fall counts toward breaking the ring.</summary>
        public void Add(Enemy enemy)
        {
            if (enemy.isValid)
            {
                dead.Add(enemy);
                startCount++;
            }
        }

        public void Tick(float dt)
        {
            if (!isActive)
            {
                return;
            }
            ward.Tick(dt);
            timeLeft -= dt;
            if (CountStanding() <= startCount * (1.0f - BREAK_SHARE))
            {
                Break();
            }
            else if (timeLeft <= 0.0f)
            {
                ward.Lower();
                fx.PlayBurst(fx.fade, run.hero.position, SOUL_GREEN);
            }
        }

        /// <summary>No trap (a new run, a boss rising).</summary>
        public void Clear()
        {
            ward.Lower();
            dead.Clear();
        }

        private int CountStanding()
        {
            int standing = 0;
            foreach (Enemy enemy in dead)
            {
                standing += run.enemies.GetRow(enemy) >= 0 ? 1 : 0;
            }
            return standing;
        }

        /// <summary>The hero fought through: the fire falls with a burst, and the ring pays.</summary>
        private void Break()
        {
            ward.Lower();
            Vector3 at = run.hero.position;
            float value = MathF.Round(Progression.GetExperienceToNext(run.level) * REWARD_LEVEL_SHARE);
            run.pickups.Spawn(PickupWorld.GetGemKind(value), at, value);
            run.pickups.Spawn(PickupKind.Food, at, 1.0f);
            fx.PlayBurst(fx.win, at, SOUL_GREEN);
            run.ui.ShowBanner("The ring breaks", BREAK_BANNER_SECONDS, true);
            run.sounds.Play("reel_land_evolution");
            run.feedback.Shake(BREAK_SHAKE, BREAK_SHAKE_SECONDS);
        }
    }
}

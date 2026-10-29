using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Grave Robber (the chase): a gilded thief with a stolen chest breaks from the dead 12 m from the hero and runs for
    /// 20 s, always away from the hero, weaving, at 85% of the hero's speed (half once he is 16 m clear, to taunt it),
    /// turning back from the arena's edge. Health 260 + 120 per minute. Every tenth of his health he loses spills a
    /// coin. Caught: an elite's loot (Drops: chest, coin bag, roast odds) and a purse of 20 + 3 per minute gold. Not
    /// caught: he digs out and is gone. Look: gold glints trail him; he is an elite (bar and gilding).
    /// </summary>
    public sealed class RobberEvent : MapEvent
    {
        private const float LIMIT_SECONDS = 20.0f;
        private const float HEALTH_BASE = 260.0f;
        private const float HEALTH_PER_MINUTE = 120.0f;
        private const float SPEED_SHARE = 0.85f;
        private const float TAUNT_SHARE = 0.5f;
        private const float TAUNT_DISTANCE = 16.0f;
        private const float STEER_INTERVAL = 0.2f;
        private const float MARCH_SECONDS = 0.3f;
        private const float WEAVE_DEGREES = 35.0f;
        private const float WEAVE_HERTZ = 0.22f;
        private const float EDGE_MARGIN = 14.0f;
        private const float EDGE_PULL = 1.5f;
        private const float COIN_STEP = 0.1f;
        private const float COIN_VALUE = 2.0f;
        private const float PURSE_BASE = 20.0f;
        private const float PURSE_PER_MINUTE = 3.0f;
        private const int TRAIL_COUNT = 3;

        private static readonly Color GOLD = new Color(1.0f, 0.76f, 0.28f, 1.0f);

        private readonly Enemy robber;
        private Vector3 lastPosition;
        private float steerTimer;
        private float coinMark = 1.0f;
        private float age;

        public override MapEventKind kind => MapEventKind.Robber;
        public override string title => "Grave Robber";
        public override string goal => "Catch him before he digs out.";
        public override Color color => GOLD;
        public override string icon => UIText.Icon("gold");
        public override Vector3 markerPosition => lastPosition;
        public override MarkerKind markerKind => MarkerKind.Robber;

        public RobberEvent(Run run, EventFx fx, Vector3 center) : base(run, fx, center)
        {
            lastPosition = center;
            WaveDirector director = run.director;
            int type = EnemyCatalog.Get(Archetype.Runner, director.currentTint);
            var options = new SpawnOptions
            {
                isElite = true,
                health = (HEALTH_BASE + HEALTH_PER_MINUTE * run.minutes) * director.dreadScale,
                damageScale = director.damageScale,
                isRewarded = true,
            };
            robber = run.enemies.Spawn(type, center, in options);
            Activate(LIMIT_SECONDS, "A grave robber flees with a chest");
            run.sounds.Play("elite_horn");
        }

        protected override void Tick(float dt, bool refresh)
        {
            EnemyWorld enemies = run.enemies;
            int row = enemies.GetRow(robber);
            if (row < 0)
            {
                return;
            }
            age += dt;
            lastPosition = enemies.GetPosition(row);
            progress = 1.0f - enemies.GetHealthFraction(row);
            SpillCoins(enemies.GetHealthFraction(row));
            steerTimer -= dt;
            if (steerTimer <= 0.0f)
            {
                steerTimer = STEER_INTERVAL;
                Flee(enemies, row);
            }
            if (refresh)
            {
                fx.Emit(fx.sparkle, lastPosition + new Vector3(0.0f, 0.3f, 0.0f), TRAIL_COUNT, GOLD);
            }
        }

        /// <summary>Away from the hero, weaving, turned back from the edge; slower once he is far ahead.</summary>
        private void Flee(EnemyWorld enemies, int row)
        {
            Vector3 hero = run.hero.position;
            Vector3 away = Ground.Direction(hero, lastPosition, Vector3.forward);
            away = Ground.Rotate(away, MathF.Sin(age * WEAVE_HERTZ * MathF.PI * 2.0f) * WEAVE_DEGREES);
            Vector3 flat = Ground.Flat(lastPosition);
            if (flat.magnitude > run.arenaRadius - EDGE_MARGIN)
            {
                away = Ground.Normalize(away - flat.normalized * EDGE_PULL, away);
            }
            bool isFar = Ground.DistanceSquared(hero, lastPosition) > TAUNT_DISTANCE * TAUNT_DISTANCE;
            float speed = run.stats[StatId.MoveSpeed] * (isFar ? TAUNT_SHARE : SPEED_SHARE);
            enemies.March(row, away, false, speed, MARCH_SECONDS);
        }

        private void SpillCoins(float healthFraction)
        {
            while (healthFraction < coinMark - COIN_STEP && coinMark > COIN_STEP)
            {
                coinMark -= COIN_STEP;
                run.pickups.Spawn(PickupKind.Gold, lastPosition, COIN_VALUE);
            }
        }

        public override void OnEnemyDied(in EnemyDeath death)
        {
            if (isOver || !death.enemy.Equals(robber))
            {
                return;
            }
            progress = 1.0f;
            run.pickups.Spawn(PickupKind.Gold, death.position, MathF.Round(PURSE_BASE + PURSE_PER_MINUTE * run.minutes));
            Win("The robber is caught");
        }

        protected override void TimeOut()
        {
            if (run.enemies.Despawn(robber))
            {
                fx.Emit(fx.sparkle, lastPosition, TRAIL_COUNT * 4, GOLD);
            }
            Lose("The robber digs out");
        }

        public override bool Owns(int row) => row >= 0 && run.enemies.GetRow(robber) == row;

        public override void Clear()
        {
            if (state == MapEventState.Active)
            {
                run.enemies.Despawn(robber);
            }
        }
    }
}

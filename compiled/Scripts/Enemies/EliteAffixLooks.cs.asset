using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// What an elite's affix looks like, so the player can read it before the fight: Hasted elites shed amber wind
    /// streaks as they run, Warded elites pulse an ice-blue ring around them while their bubble holds. Splitters show
    /// nothing until they split. Looks are emitted on a short interval for the elites alive. Main thread.
    /// </summary>
    internal sealed class EliteAffixLooks
    {
        private const float HASTE_INTERVAL = 0.15f;
        private const float BARRIER_INTERVAL = 0.6f;
        private const int MAX_ELITES = 16;
        /// <summary>Wind streaks start at waist height.</summary>
        private const float STREAK_HEIGHT_SHARE = 0.45f;
        private const int STREAKS_PER_EMIT = 2;
        /// <summary>The ward ring's size (its diameter) per metre of the elite's radius, and its life.</summary>
        private const float RING_SIZE_PER_RADIUS = 3.0f;
        private const float RING_SECONDS = 0.7f;
        private const float GROUND_LIFT = 0.05f;

        private static readonly Color HASTE_AMBER = new Color(1.0f, 0.72f, 0.3f, 1.0f);
        private static readonly Color BARRIER_ICE = new Color(0.55f, 0.85f, 1.0f, 1.0f);

        private readonly Run run;
        private readonly int windPuff;
        private readonly int wardRing;
        private readonly int[] elites = new int[MAX_ELITES];
        private float hasteTimer;
        private float barrierTimer;

        public EliteAffixLooks(Run run)
        {
            this.run = run;
            windPuff = run.effects.Find("CoreWindPuff");
            wardRing = run.effects.Find("WardRing");
        }

        /// <summary>Once per frame while the run plays.</summary>
        public void Tick(float dt)
        {
            hasteTimer -= dt;
            barrierTimer -= dt;
            bool hastes = hasteTimer <= 0.0f;
            bool barriers = barrierTimer <= 0.0f;
            if (!hastes && !barriers)
            {
                return;
            }
            hasteTimer = hastes ? HASTE_INTERVAL : hasteTimer;
            barrierTimer = barriers ? BARRIER_INTERVAL : barrierTimer;
            EnemyWorld enemies = run.enemies;
            int found = enemies.CollectElites(elites);
            for (int i = 0; i < found; i++)
            {
                int row = elites[i];
                if (hastes && enemies.IsHasted(row))
                {
                    ShowHaste(enemies, row);
                }
                if (barriers && enemies.GetBarrier(row) > 0.0f)
                {
                    ShowBarrier(enemies, row);
                }
            }
        }

        private void ShowHaste(EnemyWorld enemies, int row)
        {
            Vector3 at = enemies.GetPosition(row) + new Vector3(0.0f, enemies.GetHeight(row) * STREAK_HEIGHT_SHARE, 0.0f);
            run.effects.Emit(windPuff, at, STREAKS_PER_EMIT, HASTE_AMBER);
        }

        private void ShowBarrier(EnemyWorld enemies, int row)
        {
            Vector3 at = enemies.GetPosition(row);
            at.y = GROUND_LIFT;
            run.effects.Emit(wardRing, at, 1, BARRIER_ICE, enemies.GetRadius(row) * RING_SIZE_PER_RADIUS, RING_SECONDS);
        }
    }
}

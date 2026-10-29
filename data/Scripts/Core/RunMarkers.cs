using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The HUD's pointers to what matters on the field, refreshed every frame of play: badges over chests on screen and
    /// arrows toward chests, the gem cluster and elites off screen, and health bars over elites. The active map event
    /// marks itself, so its robber gets no elite arrow here. Main thread.
    /// </summary>
    public sealed class RunMarkers
    {
        /// <summary>Elites with a health bar per frame, and chest and elite markers together; the UI draws at most 8.</summary>
        private const int MAX_MARKERS = 8;
        /// <summary>Elite health bars float this far (m) above the head.</summary>
        private const float ELITE_BAR_LIFT = 0.35f;

        private readonly Run run;
        private readonly Vector3[] chests = new Vector3[MAX_MARKERS];
        private readonly int[] elites = new int[MAX_MARKERS];

        public RunMarkers(Run run)
        {
            this.run = run;
        }

        /// <summary>Sends this frame's markers and elite bars to the HUD.</summary>
        public void Show()
        {
            GameUI ui = run.ui;
            int chestCount = run.pickups.CollectPositions(PickupKind.Chest, chests);
            for (int i = 0; i < chestCount; i++)
            {
                ui.ShowMarker(chests[i], MarkerKind.Chest);
            }
            if (run.pickups.TryGetHugeGem(out Vector3 cluster))
            {
                ui.ShowMarker(cluster, MarkerKind.Treasure);
            }
            ShowElites(Math.Max(0, MAX_MARKERS - chestCount));
        }

        /// <summary>A health bar over every elite found, and an arrow toward the first <paramref name="markerBudget"/>.</summary>
        private void ShowElites(int markerBudget)
        {
            EnemyWorld enemies = run.enemies;
            int count = enemies.CollectElites(elites);
            for (int i = 0; i < count; i++)
            {
                int row = elites[i];
                Vector3 position = enemies.GetPosition(row);
                run.ui.ShowEliteBar(position + new Vector3(0.0f, enemies.GetHeight(row) + ELITE_BAR_LIFT, 0.0f), enemies.GetHealthFraction(row));
                if (i < markerBudget && !run.mapEvents.Owns(row))
                {
                    run.ui.ShowMarker(position, MarkerKind.Elite);
                }
            }
        }
    }
}

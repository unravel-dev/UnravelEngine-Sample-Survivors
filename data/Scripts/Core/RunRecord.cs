using System;
using System.Collections.Generic;

namespace Survivors
{
    /// <summary>Where the hero's healing came from (balance telemetry).</summary>
    public enum HealSource
    {
        Regen,
        /// <summary>The class signature or active (Fury, Bulwark, Blood Price, Bloodletting).</summary>
        Class,
        /// <summary>Weapons and evolutions (Dawnbreaker, Briar Sanctum).</summary>
        Weapon,
        /// <summary>Passive item traits (Heartbeat).</summary>
        Passive,
        /// <summary>Roasts on the ground.</summary>
        Food,
        /// <summary>Level-up cards (Hearty Meal).</summary>
        Card,
        /// <summary>Map events (the Moonwell).</summary>
        Event,
        Other,
        Count,
    }

    /// <summary>
    /// What a run did, for the pause sheet, the results screen and the save: damage, kills and time per weapon slot, and
    /// the run's clock, level and gold earned (the run reads them from here). Reset when a run starts.
    /// </summary>
    public sealed class RunRecord
    {
        public const int MAX_SOURCES = 16;
        /// <summary>The slot index damage without a weapon counts under (bombs, explosions, actives).</summary>
        public const int OTHER_SOURCE = MAX_SOURCES - 1;
        private const float SECONDS_PER_MINUTE = 60.0f;

        /// <summary>Damage dealt per weapon slot (index = slot), and by sources without a slot at the end.</summary>
        public readonly float[] damageBySource = new float[MAX_SOURCES];
        public readonly int[] killsBySource = new int[MAX_SOURCES];
        /// <summary>Run time (s) each slot was first filled.</summary>
        public readonly float[] ownedSince = new float[MAX_SOURCES];
        public float damageTaken;
        /// <summary>Health restored per <see cref="HealSource"/> (only what landed below max health).</summary>
        public readonly float[] healedBySource = new float[(int)HealSource.Count];
        public float highestHit;
        public int kills;
        public int elitesKilled;
        /// <summary>Gold picked up this run (Greed applied); banked when the run ends.</summary>
        public int goldEarned;
        public int chestsOpened;
        /// <summary>Game seconds the run has lasted.</summary>
        public float timeSurvived;
        /// <summary>The hero's level.</summary>
        public int levelReached = 1;
        public bool isVictory;
        public string deathCause = "";
        /// <summary>Level reached at the end of each minute.</summary>
        public readonly List<int> levelByMinute = new List<int>();

        public void Reset()
        {
            Array.Clear(damageBySource, 0, MAX_SOURCES);
            Array.Clear(killsBySource, 0, MAX_SOURCES);
            Array.Clear(ownedSince, 0, MAX_SOURCES);
            damageTaken = 0.0f;
            Array.Clear(healedBySource, 0, healedBySource.Length);
            highestHit = 0.0f;
            kills = 0;
            elitesKilled = 0;
            goldEarned = 0;
            chestsOpened = 0;
            timeSurvived = 0.0f;
            levelReached = 1;
            isVictory = false;
            deathCause = "";
            levelByMinute.Clear();
        }

        public void AddDamage(int source, float amount)
        {
            damageBySource[ClampSource(source)] += amount;
            highestHit = MathF.Max(highestHit, amount);
        }

        public void AddHeal(HealSource source, float amount)
        {
            healedBySource[Math.Clamp((int)source, 0, healedBySource.Length - 1)] += amount;
        }

        /// <summary>Total health restored this run.</summary>
        public float totalHealed
        {
            get
            {
                float total = 0.0f;
                foreach (float amount in healedBySource)
                {
                    total += amount;
                }
                return total;
            }
        }

        public void AddKill(int source)
        {
            kills++;
            killsBySource[ClampSource(source)]++;
        }

        /// <summary>Moves burn and venom damage the enemies credited per source into the record and zeroes it there.</summary>
        public void TakeStatusDamage(float[] dealtBySource)
        {
            for (int source = 0; source < dealtBySource.Length; source++)
            {
                if (dealtBySource[source] > 0.0f)
                {
                    damageBySource[Math.Min(source, MAX_SOURCES - 1)] += dealtBySource[source];
                    dealtBySource[source] = 0.0f;
                }
            }
        }

        /// <summary>Notes the level for every minute the run completed since the last call.</summary>
        public void TrackLevelByMinute()
        {
            int minute = (int)(timeSurvived / SECONDS_PER_MINUTE);
            while (levelByMinute.Count < minute)
            {
                levelByMinute.Add(levelReached);
            }
        }

        private static int ClampSource(int source) => source >= 0 && source < MAX_SOURCES ? source : OTHER_SOURCE;
    }
}

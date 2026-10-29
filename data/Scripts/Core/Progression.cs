using System;

namespace Survivors
{
    /// <summary>
    /// The hero's experience: the XP curve, the experience gathered toward the next level, and the level-ups earned but
    /// not yet picked, which the run opens one at a time. The level itself is kept in the run's record. Main thread.
    /// </summary>
    public sealed class Progression
    {
        // Experience from level L to the next: 5 + 9 (L - 1) + 2.4 (L - 1)^2, rounded.
        private const float EXPERIENCE_BASE = 5.0f;
        private const float EXPERIENCE_PER_LEVEL = 9.0f;
        private const float EXPERIENCE_PER_LEVEL_SQUARED = 2.4f;

        private readonly RunRecord record;

        public Progression(RunRecord record)
        {
            this.record = record;
        }

        /// <summary>Experience gathered toward the next level.</summary>
        public float experience { get; private set; }

        /// <summary>Level-ups earned and not opened yet.</summary>
        public int pendingLevelUps { get; private set; }

        /// <summary>The XP bar's fill, 0..1.</summary>
        public float experienceFraction => experience / GetExperienceToNext(record.levelReached);

        /// <summary>Experience needed to go from <paramref name="currentLevel"/> to the next.</summary>
        public static float GetExperienceToNext(int currentLevel)
        {
            float steps = currentLevel - 1;
            return MathF.Round(EXPERIENCE_BASE + EXPERIENCE_PER_LEVEL * steps + EXPERIENCE_PER_LEVEL_SQUARED * steps * steps);
        }

        /// <summary>No experience and nothing pending (a new run; the record holds level 1).</summary>
        public void Reset()
        {
            experience = 0.0f;
            pendingLevelUps = 0;
        }

        /// <summary>Forgets the level-ups not opened yet (the run ended).</summary>
        public void DropPending() => pendingLevelUps = 0;

        /// <summary>Adds experience (Growth applied by the caller); every level gained queues a level-up.</summary>
        public void AddExperience(float amount)
        {
            experience += amount;
            float needed = GetExperienceToNext(record.levelReached);
            while (experience >= needed)
            {
                experience -= needed;
                record.levelReached++;
                pendingLevelUps++;
                needed = GetExperienceToNext(record.levelReached);
            }
        }

        /// <summary>Takes one pending level-up to open; false when none waits.</summary>
        public bool TryTakeLevelUp()
        {
            if (pendingLevelUps <= 0)
            {
                return false;
            }
            pendingLevelUps--;
            return true;
        }
    }
}

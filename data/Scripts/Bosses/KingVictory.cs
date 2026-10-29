using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The Hollow King's fall. Time slows to 0.25 for 1.5 s and the camera eases in 10%; 300 bone fragments burst from
    /// the body; a death wave runs out at 20 m/s and kills every enemy it reaches (a cascade of pops in slow motion);
    /// every gem flies to the hero; the King's gold shower is paid; the 5-item chest falls at the hero's feet and
    /// opens; then the run ends as a victory (the results screen follows <see cref="Run"/>'s own delay). Its clock is
    /// unscaled time, so the wave keeps its speed through the slow motion, and it stops while the chest reel holds the
    /// game. Gems collected meanwhile only chime: the run is won, and level-up cards must not open over the victory.
    /// Main thread.
    /// </summary>
    public sealed class KingVictory
    {
        private const float SLOW_TIME_SCALE = 0.25f;
        private const float SLOW_SECONDS = 1.5f;
        private const float VICTORY_ZOOM = 0.9f;
        private const float WAVE_SPEED = 20.0f;
        /// <summary>The wave covers the view at the boss zoom with a margin.</summary>
        private const float WAVE_RADIUS = 34.0f;
        private const float WAVE_PUFF_SECONDS = 0.12f;
        private const int BONE_FRAGMENTS = 300;
        private const float BONE_HEIGHT = 3.5f;
        private const float DEATH_BURST_SIZE = 12.0f;
        private const float DEATH_BURST_SECONDS = 1.2f;
        private const float DEATH_SHAKE = 0.8f;
        private const float DEATH_SHAKE_SECONDS = 1.5f;
        private const float GOLD_AT = 0.15f;
        private const float ATTRACT_AT = 0.6f;
        private const float ATTRACT_AGAIN_AT = 1.75f;
        private const float CHEST_AT = 1.6f;
        /// <summary>Seconds of this sequence before the run ends; Run waits its own delay for the results screen.</summary>
        private const float DEFEAT_AT = 1.9f;
        private const float HERO_SAFE_SECONDS = 12.0f;
        /// <summary>Most kills a frame of the wave shows with a pop and a sound; the rest die quietly.</summary>
        private const int POPS_PER_FRAME = 40;
        private const float POP_HEIGHT = 0.8f;
        private const int POP_SHARDS = 6;
        private const int SCRATCH = 4096;
        private const int GOLD_SHOWER_COINS = 60;
        private static readonly Color WAVE_COLOR = new Color(0.55f, 1.0f, 0.65f, 1.0f);

        private readonly Run run;
        private readonly BossFx fx;
        private readonly int[] scratch = new int[SCRATCH];
        private Vector3 center;
        private int gold;
        private float time;
        private float waveRadius;
        private bool isRunning;
        private bool hasPaidGold;
        private bool hasAttracted;
        private bool hasAttractedAgain;
        private bool hasDroppedChest;
        private bool hasEndedSlowMotion;

        public KingVictory(Run run, BossFx fx)
        {
            this.run = run;
            this.fx = fx;
        }

        /// <summary>Whether the sequence is playing (the boss bar and markers stay hidden).</summary>
        public bool isPlaying => isRunning;

        /// <summary>Starts the fall at the King's position.</summary>
        /// <param name="goldReward">Gold the shower pays (before Greed).</param>
        public void Begin(Vector3 at, int goldReward)
        {
            center = BossGeometry.Flat(at);
            gold = goldReward;
            time = 0.0f;
            waveRadius = 0.0f;
            isRunning = true;
            hasPaidGold = false;
            hasAttracted = false;
            hasAttractedAgain = false;
            hasDroppedChest = false;
            hasEndedSlowMotion = false;
            run.clock.SetSlowMotion(SLOW_TIME_SCALE);
            run.cameraRig.zoom = VICTORY_ZOOM;
            run.hero?.SetInvulnerable(HERO_SAFE_SECONDS);
            run.projectiles.Clear(Team.Enemy);
            run.pickups.gemsGiveExperience = false;
            run.ui.HideBossBar();
            run.ui.FlashScreen(false);
            run.feedback.Shake(DEATH_SHAKE, DEATH_SHAKE_SECONDS);
            Vector3 bones = center + new Vector3(0.0f, BONE_HEIGHT, 0.0f);
            fx.Burst(fx.bossBoneBurst, bones, BONE_FRAGMENTS);
            fx.Burst(fx.bossDeathBurst, bones, 1, Color.white, DEATH_BURST_SIZE, DEATH_BURST_SECONDS);
            run.sounds.PlayAt("king_death", center);
            run.sounds.Play("death_wave");
        }

        /// <summary>Steps the sequence on unscaled time; the manager calls it while the run plays.</summary>
        public void Tick(float unscaledDt)
        {
            if (!isRunning)
            {
                return;
            }
            time += unscaledDt;
            if (!hasEndedSlowMotion && time >= SLOW_SECONDS)
            {
                EndSlowMotion();
            }
            SpreadWave();
            if (!hasPaidGold && time >= GOLD_AT)
            {
                hasPaidGold = true;
                PayGold();
            }
            if (!hasAttracted && time >= ATTRACT_AT)
            {
                hasAttracted = true;
                run.pickups.AttractAll();
            }
            if (!hasDroppedChest && time >= CHEST_AT)
            {
                hasDroppedChest = true;
                DropChest();
            }
            if (!hasAttractedAgain && time >= ATTRACT_AGAIN_AT)
            {
                hasAttractedAgain = true;
                run.pickups.AttractAll();
            }
            if (time >= DEFEAT_AT)
            {
                Finish();
            }
        }

        /// <summary>Stops the sequence and gives time and the gems back (new run).</summary>
        public void Cancel()
        {
            if (!isRunning)
            {
                return;
            }
            isRunning = false;
            if (!hasEndedSlowMotion)
            {
                EndSlowMotion();
            }
            run.pickups.gemsGiveExperience = true;
        }

        /// <summary>Time runs at the run's speed again (hit stops in between were the clock's to handle).</summary>
        private void EndSlowMotion()
        {
            hasEndedSlowMotion = true;
            run.clock.EndSlowMotion();
        }

        private void SpreadWave()
        {
            if (waveRadius >= WAVE_RADIUS)
            {
                return;
            }
            waveRadius = MathF.Min(time * WAVE_SPEED, WAVE_RADIUS);
            fx.Burst(fx.deathWave, new Vector3(center.x, Tuning.TELEGRAPH_HEIGHT, center.z), 1, WAVE_COLOR, waveRadius * 2.0f, WAVE_PUFF_SECONDS);
            EnemyWorld enemies = run.enemies;
            int killed = enemies.KillRegularIn(center, waveRadius, RunRecord.OTHER_SOURCE, scratch);
            for (int i = 0; i < killed && i < POPS_PER_FRAME; i++)
            {
                Vector3 at = enemies.GetPosition(scratch[i]);
                fx.Burst(fx.boneBurst, at + new Vector3(0.0f, POP_HEIGHT, 0.0f), POP_SHARDS);
                run.sounds.PlayAt("enemy_death", at);
            }
        }

        /// <summary>The gold is paid at once (with Greed); the shower is its picture.</summary>
        private void PayGold()
        {
            run.AddGold((int)MathF.Round(gold * run.stats[StatId.Greed]));
            fx.Burst(fx.goldShower, center + new Vector3(0.0f, BONE_HEIGHT, 0.0f), GOLD_SHOWER_COINS);
            run.sounds.Play("gold_shower");
        }

        /// <summary>The King's chest lands where the hero stands, so it opens without a walk.</summary>
        private void DropChest()
        {
            if (run.hero == null)
            {
                return;
            }
            Vector3 at = BossGeometry.Flat(run.hero.position);
            run.pickups.Spawn(PickupKind.Chest, at, run.drops.RollChestItems(Drops.ChestSource.Boss), false);
            fx.Burst(fx.eliteBeam, at, 1);
            run.sounds.PlayAt("boss_chest_drop", at);
        }

        private void Finish()
        {
            isRunning = false;
            run.pickups.gemsGiveExperience = true;
            run.OnBossDefeated();
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Game feel in one place: hit sparks and sounds (throttled so a crowd does not roar), hit stops on big moments,
    /// camera shake scaled by the player's setting, kill pops, the hero's hurt and revival. Systems report events; this
    /// decides how loud they read.
    /// Hit stops are held by the run's <see cref="GameClock"/>. Main thread.
    /// </summary>
    public sealed class Feedback
    {
        /// <summary>Hit sounds per second at most, whatever the number of hits.</summary>
        private const float HIT_SOUNDS_PER_SECOND = 18.0f;
        private const float KILL_SOUNDS_PER_SECOND = 14.0f;
        /// <summary>Sounds a quiet moment can save up for the next burst of hits or kills.</summary>
        private const float MAX_SOUND_BUDGET = 3.0f;
        private const int HIT_SPARKS = 4;
        private const int CRIT_SPARKS = 10;
        private const int KILL_BONES = 8;
        /// <summary>A hit that takes more than this share of the hero's max health shakes harder.</summary>
        private const float HEAVY_HURT_SHARE = 0.2f;
        private const float HEAVY_HURT_SHAKE = 0.45f;
        private const float LIGHT_HURT_SHAKE = 0.25f;
        private const float HURT_SHAKE_SECONDS = 0.35f;
        private const float REVIVE_SHAKE = 0.5f;
        private const float REVIVE_SHAKE_SECONDS = 0.5f;

        private readonly Effects effects;
        private readonly Sounds sounds;
        private readonly GameClock clock;
        private readonly int hitSpark;
        private readonly int critSpark;
        private readonly int killBurst;
        private readonly int reviveBurst;
        private CameraRig camera;
        private float hitSoundBudget;
        private float killSoundBudget;

        /// <summary>Player setting (GameUI.ApplySettings): camera shake strength, 0..1.</summary>
        public float shakeScale = 1.0f;

        public Feedback(Effects effects, Sounds sounds, GameClock clock)
        {
            this.effects = effects;
            this.sounds = sounds;
            this.clock = clock;
            hitSpark = effects.Find("HitSpark");
            critSpark = effects.Find("CritSpark");
            killBurst = effects.Find("BoneBurst");
            reviveBurst = effects.Find("ReviveBurst");
        }

        /// <summary>Whether a hit stop holds time now.</summary>
        public bool isStopping => clock.isStopping;

        public void SetCamera(CameraRig rig) => camera = rig;

        /// <summary>Refills the sound budgets; call once per frame with unscaled time.</summary>
        public void Update(float unscaledDt)
        {
            hitSoundBudget = MathF.Min(hitSoundBudget + HIT_SOUNDS_PER_SECOND * unscaledDt, MAX_SOUND_BUDGET);
            killSoundBudget = MathF.Min(killSoundBudget + KILL_SOUNDS_PER_SECOND * unscaledDt, MAX_SOUND_BUDGET);
        }

        public void OnEnemyHit(Vector3 chest, bool isCritical, bool isKill)
        {
            effects.Emit(isCritical ? critSpark : hitSpark, chest, isCritical ? CRIT_SPARKS : HIT_SPARKS);
            if (isKill)
            {
                effects.Emit(killBurst, chest, KILL_BONES);
                if (killSoundBudget >= 1.0f)
                {
                    killSoundBudget -= 1.0f;
                    sounds.PlayAt("enemy_death", chest);
                }
                return;
            }
            if (hitSoundBudget >= 1.0f)
            {
                hitSoundBudget -= 1.0f;
                sounds.PlayAt(isCritical ? "hit_crit" : "hit", chest);
            }
        }

        /// <summary>The hero took a hit of <paramref name="share"/> of its max health: a shake by the hit's weight and a grunt.</summary>
        public void OnHeroHurt(float share)
        {
            Shake(share > HEAVY_HURT_SHARE ? HEAVY_HURT_SHAKE : LIGHT_HURT_SHAKE, HURT_SHAKE_SECONDS);
            sounds.Play("hero_hurt");
        }

        /// <summary>A revival brought the hero back at <paramref name="position"/>.</summary>
        public void OnHeroRevived(Vector3 position)
        {
            effects.Emit(reviveBurst, position, 1);
            sounds.Play("revive");
            Shake(REVIVE_SHAKE, REVIVE_SHAKE_SECONDS);
        }

        /// <summary>Freezes the world for a moment (big hits, boss phases); see <see cref="GameClock.HitStop"/>.</summary>
        public void HitStop(float seconds, bool bypassGap = false) => clock.HitStop(seconds, bypassGap);

        /// <summary>Ends a running hit stop at once.</summary>
        public void CancelHitStop() => clock.CancelHitStop();

        /// <summary>Shakes the camera; <paramref name="strength"/> is metres at full shake, scaled by the setting.</summary>
        public void Shake(float strength, float seconds)
        {
            if (shakeScale > 0.0f)
            {
                camera?.Shake(strength * shakeScale, seconds);
            }
        }
    }
}

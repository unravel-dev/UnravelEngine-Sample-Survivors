using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The only writer of <see cref="Time.timeScale"/>. Game time runs, from the strongest rule down:
    /// <list type="bullet">
    /// <item>not at all while a menu holds the run (<see cref="Freeze"/>: pause, level-up, chest);</item>
    /// <item>at 5% during a hit stop (<see cref="HitStop"/>);</item>
    /// <item>at a fixed slow-motion scale while one is set (<see cref="SetSlowMotion"/>: the end of a run);</item>
    /// <item>otherwise at <see cref="speed"/>, eased back up from 30% for a quarter second after a menu closes.</item>
    /// </list>
    /// Every change recomputes the scale from these rules, so a hit stop that ends returns to whatever holds then.
    /// Call <see cref="Update"/> once per frame with unscaled time. Main thread.
    /// </summary>
    public sealed class GameClock
    {
        private const float HIT_STOP_SCALE = 0.05f;
        /// <summary>At most one hit stop starts per this many real seconds, unless a caller bypasses the gap.</summary>
        private const float HIT_STOP_GAP = 0.3f;
        private const float RAMP_START = 0.3f;
        private const float RAMP_SECONDS = 0.25f;
        private const float NO_RAMP = -1.0f;
        private const float NO_SLOW_MOTION = 0.0f;

        private bool isFrozen;
        private float slowMotion = NO_SLOW_MOTION;
        private float rampTime = NO_RAMP;
        private float hitStopRemaining;
        private float lastHitStop = float.NegativeInfinity;
        private float runSpeed = 1.0f;

        /// <summary>Multiplies game time outside freezes, hit stops and slow motion (unattended tests run faster).</summary>
        public float speed
        {
            get => runSpeed;
            set
            {
                runSpeed = value;
                Apply();
            }
        }

        /// <summary>Whether a hit stop holds time now.</summary>
        public bool isStopping => hitStopRemaining > 0.0f;

        /// <summary>Game time at <paramref name="newSpeed"/> with nothing else holding it (a run starts, the title shows).</summary>
        public void Restart(float newSpeed)
        {
            isFrozen = false;
            slowMotion = NO_SLOW_MOTION;
            rampTime = NO_RAMP;
            hitStopRemaining = 0.0f;
            runSpeed = newSpeed;
            Apply();
        }

        /// <summary>Stops game time while a menu holds the run; ends any hit stop.</summary>
        public void Freeze()
        {
            isFrozen = true;
            hitStopRemaining = 0.0f;
            rampTime = NO_RAMP;
            Apply();
        }

        /// <summary>Lets time run again after a menu, easing up from 30% so the world does not jump at the player.</summary>
        public void ResumeWithRamp()
        {
            isFrozen = false;
            rampTime = 0.0f;
            Apply();
        }

        /// <summary>Runs game time at a fixed <paramref name="scale"/> until <see cref="EndSlowMotion"/>; ends any hit stop.</summary>
        public void SetSlowMotion(float scale)
        {
            slowMotion = scale;
            hitStopRemaining = 0.0f;
            rampTime = NO_RAMP;
            Apply();
        }

        /// <summary>Back to <see cref="speed"/> after <see cref="SetSlowMotion"/>.</summary>
        public void EndSlowMotion()
        {
            slowMotion = NO_SLOW_MOTION;
            Apply();
        }

        /// <summary>
        /// Nearly freezes the world for <paramref name="seconds"/> of real time (big hits, boss phases). A longer request
        /// replaces a shorter one; requests within the gap of the last one are dropped unless <paramref name="bypassGap"/>;
        /// nothing happens while frozen.
        /// </summary>
        public void HitStop(float seconds, bool bypassGap = false)
        {
            if (isFrozen || seconds <= hitStopRemaining)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (!bypassGap && now - lastHitStop < HIT_STOP_GAP)
            {
                return;
            }
            lastHitStop = now;
            hitStopRemaining = seconds;
            Apply();
        }

        /// <summary>Ends a running hit stop at once.</summary>
        public void CancelHitStop()
        {
            if (hitStopRemaining > 0.0f)
            {
                hitStopRemaining = 0.0f;
                Apply();
            }
        }

        /// <summary>Runs out hit stops and the ramp; call once per frame with unscaled time.</summary>
        public void Update(float unscaledDt)
        {
            bool hasChanged = false;
            if (hitStopRemaining > 0.0f)
            {
                hitStopRemaining = MathF.Max(hitStopRemaining - unscaledDt, 0.0f);
                hasChanged = true;
            }
            if (rampTime >= 0.0f && !isFrozen)
            {
                rampTime += unscaledDt;
                if (rampTime >= RAMP_SECONDS)
                {
                    rampTime = NO_RAMP;
                }
                hasChanged = true;
            }
            if (hasChanged)
            {
                Apply();
            }
        }

        /// <summary>Hands the engine back its normal time scale (play stops).</summary>
        public static void ResetEngineScale() => Time.timeScale = 1.0f;

        private void Apply() => Time.timeScale = GetScale();

        private float GetScale()
        {
            if (isFrozen)
            {
                return 0.0f;
            }
            if (hitStopRemaining > 0.0f)
            {
                return HIT_STOP_SCALE;
            }
            if (slowMotion > NO_SLOW_MOTION)
            {
                return slowMotion;
            }
            return runSpeed * GetRampFactor();
        }

        private float GetRampFactor()
        {
            return rampTime < 0.0f ? 1.0f : Mathf.Lerp(RAMP_START, 1.0f, MathF.Min(rampTime / RAMP_SECONDS, 1.0f));
        }
    }
}

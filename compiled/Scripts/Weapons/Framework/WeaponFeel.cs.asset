using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Game feel rules the weapons share: hit stops only during play (Feedback keeps the global
    /// 0.30 s gap); camera trauma; and the hero's upper-body attack clips, at most one per 0.4 s unless a higher
    /// priority asks (active > evolved > class weapon > shared). Requests made in one frame are settled once, at its end.
    /// </summary>
    public sealed class WeaponFeel
    {
        public const int PRIORITY_SHARED = 0;
        public const int PRIORITY_CLASS = 1;
        public const int PRIORITY_EVOLVED = 2;
        public const int PRIORITY_ACTIVE = 3;

        private const float ATTACK_CLIP_GAP = 0.4f;
        private const float SHAKE_BASE_SECONDS = 0.2f;
        private const float SHAKE_SECONDS_PER_TRAUMA = 0.8f;
        private const int NO_PRIORITY = -1;
        /// <summary>A time long before the run started: the first clip is never held back.</summary>
        private const float NEVER = -10.0f;

        private readonly Run run;
        private readonly Feedback feedback;
        private readonly HeroController hero;
        private string pendingAttack;
        private int pendingPriority = NO_PRIORITY;
        private float lastAttackTime = NEVER;
        private int lastAttackPriority = NO_PRIORITY;

        public WeaponFeel(Run run)
        {
            this.run = run;
            feedback = run.feedback;
            hero = run.hero;
        }

        /// <summary>Asks for a hit stop (Feedback drops it within 0.30 s of the last one); false outside play.</summary>
        public bool HitStop(float seconds)
        {
            if (run.phase != RunPhase.Playing)
            {
                return false;
            }
            feedback.HitStop(seconds);
            return true;
        }

        /// <summary>Camera trauma, from 0.08 (a chop) to 0.6 (a great meteor); stronger trauma shakes longer.</summary>
        public void Shake(float trauma)
        {
            feedback.Shake(trauma, SHAKE_BASE_SECONDS + trauma * SHAKE_SECONDS_PER_TRAUMA);
        }

        /// <summary>Asks for an upper-body attack clip this frame; the highest priority of the frame wins.</summary>
        public void RequestAttack(string state, int priority)
        {
            if (priority > pendingPriority)
            {
                pendingAttack = state;
                pendingPriority = priority;
            }
        }

        /// <summary>Plays the frame's winning attack clip if the clip budget allows; once per frame.</summary>
        public void Flush(float clock)
        {
            if (pendingAttack == null)
            {
                return;
            }
            bool isFree = clock - lastAttackTime >= ATTACK_CLIP_GAP || pendingPriority > lastAttackPriority;
            if (isFree && hero != null)
            {
                hero.PlayAttack(pendingAttack);
                lastAttackTime = clock;
                lastAttackPriority = pendingPriority;
            }
            pendingAttack = null;
            pendingPriority = NO_PRIORITY;
        }
    }
}

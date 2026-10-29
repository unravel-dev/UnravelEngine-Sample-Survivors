using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Plays a boss's controller states (base layer, full body; every boss controller has the same state names) and
    /// fits clips to the attacks' timings: a swing is sped up or slowed down so its impact frame
    /// lands exactly on the attack's hit moment, a rise or a channel is stretched to its length. Locomotion is a 1D
    /// blend on "Speed" in the clip's own scale (idle 0, walk 1.5, run 4.0 m/s), so world speed is divided by the
    /// model scale. Playback speed is per component: every non-locomotion state sets its own, locomotion resets it.
    /// </summary>
    public sealed class BossAnimator
    {
        public const string LOCOMOTION = "Locomotion";
        private const string SPEED_PARAMETER = "Speed";
        private const string CLIP_ROOT = "app:/data/Characters/Bosses";
        private const float MIN_PLAYBACK = 0.25f;
        private const float MAX_PLAYBACK = 3.0f;
        private const float LOCOMOTION_FADE = 0.25f;
        private const float DEFAULT_IMPACT_SHARE = 0.5f;

        private readonly AnimationComponent component;
        private readonly float scale;
        private readonly string clipPrefix;
        private readonly Dictionary<string, float> lengths = new Dictionary<string, float>();
        private string current = "";
        private float playback = 1.0f;

        /// <param name="entity">The boss entity (its root or a child has the AnimationComponent).</param>
        /// <param name="model">The skeleton model the boss clips come from (Skeleton_Warrior, Skeleton_Mage).</param>
        /// <param name="scale">The model scale of the boss.</param>
        public BossAnimator(Entity entity, string model, float scale)
        {
            component = entity.IsValid() ? entity.GetComponentInChildren<AnimationComponent>() : null;
            this.scale = MathF.Max(scale, 0.01f);
            clipPrefix = $"{CLIP_ROOT}/{model}/{model}_";
            if (entity.IsValid() && component == null)
            {
                Log.Warning($"BossAnimator: {entity.name} has no AnimationComponent; the boss does not animate.");
            }
        }

        /// <summary>The state playing now (as last requested).</summary>
        public string state => current;

        /// <summary>Whether the controller's base layer is in a state (false without a component).</summary>
        public bool IsIn(string stateName) => component != null && component.IsInState(stateName);

        /// <summary>Cross-fades to a state at a playback speed.</summary>
        public void Play(string stateName, float fade, float speed = 1.0f)
        {
            current = stateName;
            if (component == null)
            {
                return;
            }
            component.CrossFade(stateName, fade);
            SetPlayback(speed);
        }

        /// <summary>Plays a state so the whole clip lasts <paramref name="seconds"/>.</summary>
        public void PlayStretched(string stateName, float seconds, float fade)
        {
            float length = GetLength(stateName);
            Play(stateName, fade, length > 0.0f && seconds > 0.0f ? length / seconds : 1.0f);
        }

        /// <summary>Plays a state so its impact frame (the blow, the release) lands <paramref name="seconds"/> from now.</summary>
        public void PlayToImpact(string stateName, float seconds, float fade)
        {
            float impact = GetLength(stateName) * GetImpactShare(stateName);
            Play(stateName, fade, impact > 0.0f && seconds > 0.0f ? impact / seconds : 1.0f);
        }

        /// <summary>Changes the playback speed of the current state (0 holds the pose).</summary>
        public void SetPlayback(float speed)
        {
            float clamped = speed <= 0.0f ? 0.0f : Math.Clamp(speed, MIN_PLAYBACK, MAX_PLAYBACK);
            if (component == null || MathF.Abs(clamped - playback) < 1e-4f)
            {
                return;
            }
            playback = clamped;
            component.Speed = clamped;
        }

        /// <summary>Walks (or idles) at a world speed in m/s, fading in from any other state.</summary>
        public void Walk(float worldSpeed)
        {
            if (current != LOCOMOTION)
            {
                Play(LOCOMOTION, LOCOMOTION_FADE);
            }
            SetPlayback(1.0f);
            component?.SetFloat(SPEED_PARAMETER, worldSpeed / scale);
        }

        /// <summary>Seconds a state's clip lasts at playback 1, or 0 when its clip is unknown.</summary>
        public float GetLength(string stateName)
        {
            if (lengths.TryGetValue(stateName, out float length))
            {
                return length;
            }
            string clip = GetClipName(stateName);
            length = 0.0f;
            if (clip != null)
            {
                AnimationClip asset = Assets.GetAsset<AnimationClip>(clipPrefix + clip + ".anim");
                length = asset != null && asset.uid != Guid.Empty ? asset.length : 0.0f;
            }
            lengths[stateName] = length;
            return length;
        }

        /// <summary>The clip of each controller state.</summary>
        private static string GetClipName(string stateName) => stateName switch
        {
            "Attack" => "1H_Melee_Attack_Chop",
            "AttackSlice" => "1H_Melee_Attack_Slice_Horizontal",
            "HeavyChop" => "2H_Melee_Attack_Chop",
            "HeavySlice" => "2H_Melee_Attack_Slice",
            "Spin" => "2H_Melee_Attack_Spinning",
            "JumpChop" => "1H_Melee_Attack_Jump_Chop",
            "Cast" => "Spellcast_Shoot",
            "CastRaise" => "Spellcast_Raise",
            "CastLong" => "Spellcast_Long",
            "Summon" => "Spellcast_Summon",
            "Taunt" => "Taunt",
            "Block" => "Blocking",
            "Hit" => "Hit_A",
            "Kneel" => "Hit_B",
            "Death" => "Death_A",
            "Rise" => "Skeletons_Awaken_Floor_Long",
            _ => null,
        };

        /// <summary>
        /// Share of a clip at which its blow lands or its spell releases, measured on the KayKit skeleton clips by
        /// forward kinematics of the right hand: between its fastest motion and its lowest point after the wind-up for
        /// swings, at the hands' highest point for raises.
        /// </summary>
        private static float GetImpactShare(string stateName) => stateName switch
        {
            "Attack" => 0.62f,
            "AttackSlice" => 0.45f,
            "HeavyChop" => 0.5f,
            "HeavySlice" => 0.38f,
            "JumpChop" => 0.6f,
            "Cast" => 0.35f,
            "CastRaise" => 0.32f,
            "Summon" => 0.7f,
            "Taunt" => 0.63f,
            _ => DEFAULT_IMPACT_SHARE,
        };
    }
}

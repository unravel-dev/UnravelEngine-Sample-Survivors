using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The gameplay camera: follows a point from a fixed three-quarter angle looking toward +Z with a little lag and
    /// lead, zooms for moments (boss intro, death) and shakes with smooth noise. Main thread; call <see cref="Update"/>
    /// after the hero moved.
    /// </summary>
    public sealed class CameraRig
    {
        private const float FOLLOW_TIME = 0.18f;
        private const float LEAD_SECONDS = 0.25f;
        private const float ZOOM_TIME = 0.6f;
        private const float SHAKE_FREQUENCY = 22.0f;
        /// <summary>Offset between the noise rows of the two shake axes, so they move independently.</summary>
        private const float SHAKE_AXIS_OFFSET = 17.3f;
        private const float MAX_NOISE_SEED = 100.0f;
        private const float GAMEPLAY_PITCH = 52.0f;
        private const float GAMEPLAY_DISTANCE = 21.0f;

        private readonly Entity camera;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private float distance;
        private float distanceVelocity;
        private float shakeStrength;
        private float shakeTime;
        private float shakeDuration;
        private float noiseSeed;

        /// <summary>Degrees the camera looks down.</summary>
        public float pitch = GAMEPLAY_PITCH;
        /// <summary>Distance from the focus at normal zoom.</summary>
        public float baseDistance = GAMEPLAY_DISTANCE;
        /// <summary>Multiplies the distance; eased toward.</summary>
        public float zoom = 1.0f;
        /// <summary>Added to the followed point (cinematics, captures).</summary>
        public Vector3 focusOffset = Vector3.zero;

        public Entity entity => camera;

        public CameraRig(Entity camera)
        {
            this.camera = camera;
            distance = baseDistance;
            noiseSeed = Random.Range(0.0f, MAX_NOISE_SEED);
        }

        /// <summary>The run's framing: the gameplay pitch and distance, no focus offset (menus and captures change them).</summary>
        public void FrameGameplay()
        {
            pitch = GAMEPLAY_PITCH;
            baseDistance = GAMEPLAY_DISTANCE;
            focusOffset = Vector3.zero;
        }

        /// <summary>Jumps to a focus without easing (run start).</summary>
        public void Snap(Vector3 target)
        {
            focus = target;
            focusVelocity = Vector3.zero;
            distance = baseDistance * zoom;
            Apply(Vector3.zero);
        }

        /// <summary>Follows <paramref name="target"/>, leading along <paramref name="velocity"/>.</summary>
        public void Update(Vector3 target, Vector3 velocity, float unscaledDt)
        {
            if (!camera.IsValid() || unscaledDt <= 0.0f)
            {
                return;
            }
            Vector3 lead = new Vector3(velocity.x, 0.0f, velocity.z) * LEAD_SECONDS;
            focus = Vector3.SmoothDamp(focus, target + lead + focusOffset, ref focusVelocity, FOLLOW_TIME, float.MaxValue, unscaledDt);
            distance = Mathf.SmoothDamp(distance, baseDistance * zoom, ref distanceVelocity, ZOOM_TIME, float.MaxValue, unscaledDt);
            Apply(GetShake(unscaledDt));
        }

        /// <summary>Adds shake; strength in metres of offset at its peak, fading over <paramref name="seconds"/>.</summary>
        public void Shake(float strength, float seconds)
        {
            float remaining = shakeDuration > 0.0f ? shakeStrength * MathF.Max(1.0f - shakeTime / shakeDuration, 0.0f) : 0.0f;
            if (strength < remaining)
            {
                return;
            }
            shakeStrength = strength;
            shakeDuration = seconds;
            shakeTime = 0.0f;
        }

        private Vector3 GetShake(float dt)
        {
            if (shakeDuration <= 0.0f || shakeTime >= shakeDuration)
            {
                return Vector3.zero;
            }
            shakeTime += dt;
            float fade = 1.0f - shakeTime / shakeDuration;
            float amount = shakeStrength * fade * fade;
            float t = Time.realtimeSinceStartup * SHAKE_FREQUENCY;
            float x = Mathf.PerlinNoise(noiseSeed, t) * 2.0f - 1.0f;
            float y = Mathf.PerlinNoise(noiseSeed + SHAKE_AXIS_OFFSET, t) * 2.0f - 1.0f;
            return new Vector3(x, y, 0.0f) * amount;
        }

        private void Apply(Vector3 shake)
        {
            Quaternion rotation = Quaternion.Euler(pitch, 0.0f, 0.0f);
            Vector3 back = rotation * Vector3.forward;
            Vector3 position = focus - back * distance + rotation * shake;
            camera.transform.position = position;
            camera.transform.rotation = rotation;
        }
    }
}

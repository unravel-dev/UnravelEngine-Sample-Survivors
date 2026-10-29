using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Drawing: the rows sorted into one bucket per (type, look) and drawn with one instanced call per bucket, with
    /// last frame's transforms and clip states for motion vectors. Main thread, once per frame after the step.
    /// </summary>
    public sealed partial class EnemyWorld
    {
        /// <summary>How much a hit body widens and flattens at the start of its squash.</summary>
        private const float HIT_SQUASH_AMOUNT = 0.15f;
        /// <summary>Depth (m at scale 1) a rising enemy without a rise clip starts below the ground.</summary>
        private const float RISE_DEPTH = 1.2f;
        private const float HIT_FLASH_INTENSITY = 0.35f;
        /// <summary>Elite eye glow (through the body's eye mask) and the gilded tint on its bones.</summary>
        private const float ELITE_GLOW_INTENSITY = 6.0f;
        private const float BURN_GLOW_INTENSITY = 0.35f;
        private const float FROZEN_GLOW_INTENSITY = 0.15f;

        private static readonly Color ELITE_TINT = new Color(1.0f, 0.8f, 0.45f, 1.0f);
        private static readonly Color BURN_GLOW = new Color(1.0f, 0.42f, 0.08f, 1.0f);
        private static readonly Color FROZEN_TINT = new Color(0.62f, 0.85f, 1.0f, 1.0f);
        private static readonly Color FROZEN_GLOW = new Color(0.35f, 0.75f, 1.0f, 1.0f);

        private readonly Matrix4x4[] rowTransforms;
        private readonly BakedAnimationState[] rowStates;
        private readonly Matrix4x4[] drawTransforms;
        private readonly Matrix4x4[] drawPrevious;
        private readonly BakedAnimationState[] drawStates;
        private readonly BakedAnimationState[] drawPreviousStates;
        private readonly int[] drawLook;
        private int[] bucketCounts = Array.Empty<int>();
        private int[] bucketStarts = Array.Empty<int>();

        public bool castShadows = true;

        /// <summary>Draws every enemy; call once per frame after the step.</summary>
        public void Draw()
        {
            if (rowCount == 0)
            {
                return;
            }
            CountBuckets();
            FillBuckets();
            DrawBuckets();
        }

        /// <summary>Picks each drawn row's look and sizes the buckets.</summary>
        private void CountBuckets()
        {
            int lookCount = (int)EnemyLook.Count;
            Array.Clear(bucketCounts, 0, bucketCounts.Length);
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row] || IsProxy(row))
                {
                    continue;
                }
                int look = (int)GetLook(row);
                drawLook[row] = look;
                bucketCounts[typeOf[row] * lookCount + look]++;
            }
            int start = 0;
            for (int bucket = 0; bucket < bucketCounts.Length; bucket++)
            {
                bucketStarts[bucket] = start;
                start += bucketCounts[bucket];
                bucketCounts[bucket] = 0;
            }
        }

        /// <summary>Writes each drawn row's transform and clip state (now and last frame) into its bucket.</summary>
        private void FillBuckets()
        {
            int lookCount = (int)EnemyLook.Count;
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row] || IsProxy(row))
                {
                    continue;
                }
                int bucket = typeOf[row] * lookCount + drawLook[row];
                int slot = bucketStarts[bucket] + bucketCounts[bucket]++;
                Matrix4x4 world = MakeTransform(row, types[typeOf[row]]);
                var state = new BakedAnimationState(clips[row], clipTimes[row]);
                drawPrevious[slot] = jumped[row] ? world : rowTransforms[row];
                drawTransforms[slot] = world;
                drawPreviousStates[slot] = jumped[row] ? state : rowStates[row];
                drawStates[slot] = state;
                rowTransforms[row] = world;
                rowStates[row] = state;
                jumped[row] = false;
            }
        }

        private void DrawBuckets()
        {
            int lookCount = (int)EnemyLook.Count;
            for (int type = 0; type < types.Count; type++)
            {
                EnemyType kind = types[type];
                if (kind.baked == null || !kind.baked.isValid)
                {
                    continue;
                }
                for (int look = 0; look < lookCount; look++)
                {
                    int bucket = type * lookCount + look;
                    int first = bucketStarts[bucket];
                    int drawCount = bucketCounts[bucket];
                    if (drawCount == 0)
                    {
                        continue;
                    }
                    Graphics.DrawMeshInstanced(kind.baked,
                                               kind.material,
                                               drawTransforms.AsSpan(first, drawCount),
                                               drawStates.AsSpan(first, drawCount),
                                               drawPrevious.AsSpan(first, drawCount),
                                               drawPreviousStates.AsSpan(first, drawCount),
                                               castShadows,
                                               materialInstance: kind.looks[look]);
                }
            }
        }

        private EnemyLook GetLook(int row)
        {
            ref RowStatus status = ref statuses[row];
            if (status.flashTime > 0.0f && states[row] != EnemyState.Dying)
            {
                return EnemyLook.Flash;
            }
            if (status.freezeTime > 0.0f)
            {
                return EnemyLook.Frozen;
            }
            if (status.burnTime > 0.0f)
            {
                return EnemyLook.Burning;
            }
            return elite[row] ? EnemyLook.Elite : EnemyLook.Normal;
        }

        private Matrix4x4 MakeTransform(int row, EnemyType kind)
        {
            ref RowStatus status = ref statuses[row];
            Vector3 position = currentPositions[row];
            float scale = scaleOf[row];
            float squashXZ = 1.0f;
            float squashY = 1.0f;
            if (states[row] == EnemyState.Dying)
            {
                float shrink = stateTime[row] - GetDeathLength(row);
                if (shrink > 0.0f)
                {
                    scale *= MathF.Max(1.0f - shrink / DEATH_SHRINK_SECONDS, 0.0f);
                }
            }
            else if (states[row] == EnemyState.Rising && !kind.HasClip(EnemyClip.Rise))
            {
                // Climbs out of the ground: starts below it and rises over the rise time.
                float t = Mathf.Clamp01(stateTime[row] / RISE_SECONDS_WITHOUT_CLIP);
                position.y -= (1.0f - t) * RISE_DEPTH * scale;
            }
            else if (status.squashTime > 0.0f)
            {
                float t = status.squashTime / HIT_SQUASH_SECONDS;
                squashXZ = 1.0f + HIT_SQUASH_AMOUNT * t;
                squashY = 1.0f - HIT_SQUASH_AMOUNT * t;
            }
            if (status.launchTime > 0.0f)
            {
                position.y += GetHop(1.0f - status.launchTime / LAUNCH_SECONDS) * status.launchHeight;
            }
            float yaw = yaws[row] + kind.yawOffset * DEGREES_TO_RADIANS;
            float cos = MathF.Cos(yaw) * scale * squashXZ;
            float sin = MathF.Sin(yaw) * scale * squashXZ;
            return new Matrix4x4(new Vector4(cos, 0.0f, -sin, 0.0f),
                                 new Vector4(0.0f, scale * squashY, 0.0f, 0.0f),
                                 new Vector4(sin, 0.0f, cos, 0.0f),
                                 new Vector4(position.x, position.y, position.z, 1.0f));
        }

        /// <summary>A hop's height over its progress t (0..1): a parabola from 0 up to 1 at the middle and back to 0.</summary>
        private static float GetHop(float t) => 4.0f * t * (1.0f - t);

        private static MaterialInstance[] CreateLooks(EnemyType type)
        {
            var looks = new MaterialInstance[(int)EnemyLook.Count];
            if (type.material == null)
            {
                return looks;
            }
            // Elites keep the body's own detail: gilded bones and blazing eyes (the eye mask), not a glowing silhouette.
            looks[(int)EnemyLook.Elite] = new MaterialInstance(type.material) { baseColor = ELITE_TINT, emissiveColor = type.eliteGlow, emissiveIntensity = ELITE_GLOW_INTENSITY };
            looks[(int)EnemyLook.Burning] = new MaterialInstance(type.material) { emissiveColor = BURN_GLOW, emissiveIntensity = BURN_GLOW_INTENSITY };
            looks[(int)EnemyLook.Frozen] = new MaterialInstance(type.material) { baseColor = FROZEN_TINT, emissiveColor = FROZEN_GLOW, emissiveIntensity = FROZEN_GLOW_INTENSITY };
            looks[(int)EnemyLook.Flash] = new MaterialInstance(type.material) { emissiveColor = Color.white, emissiveIntensity = HIT_FLASH_INTENSITY };
            if (type.bodyGlowMap != null)
            {
                for (int look = (int)EnemyLook.Burning; look < (int)EnemyLook.Count; look++)
                {
                    looks[look].emissiveMap = type.bodyGlowMap;
                }
            }
            return looks;
        }
    }
}

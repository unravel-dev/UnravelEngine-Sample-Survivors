using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>How a lightning bolt drawn by <see cref="LightningFx"/> looks: its emitters, strands, colours, jag and branches.</summary>
    public struct BoltStyle
    {
        /// <summary>The emitter of the lightning strands.</summary>
        public int strandEffect;
        /// <summary>The emitter of the soft glow under the strands.</summary>
        public int glowEffect;
        /// <summary>
        /// 0 for sized strands: the quad is sized to its segment and a slow drift turns it along it (SharedStormBolt).
        /// Otherwise the strand emitter's stretch_speed_scale: the strand is stretched by its speed (MageArcBolt).
        /// </summary>
        public float stretch;
        /// <summary>Strands drawn over each segment.</summary>
        public int strands;
        public Color core;
        public Color glow;
        public float seconds;
        /// <summary>Segment length (m); the strand texture adds the fine jags.</summary>
        public float step;
        /// <summary>The most segments one bolt is cut into.</summary>
        public int maxSegments;
        /// <summary>How far (m) each joint is pushed sideways and up or down, either way.</summary>
        public float jitter;
        /// <summary>The bolt's middle rises this share of its length, at most <see cref="archMax"/> m (0: a straight bolt).</summary>
        public float archShare;
        public float archMax;
        /// <summary>Chance of a short side branch at each joint (0: none).</summary>
        public float branchChance;
        /// <summary>A branch leaves the joint this much forward for one sideways, dipping up to <see cref="branchDrop"/> m.</summary>
        public float branchForward;
        public float branchLengthMin;
        public float branchLengthMax;
        public float branchDrop;
        /// <summary>Stretched strands: their width (m), or 0 to size them by <see cref="strandWidthShare"/> of the segment's length.</summary>
        public float strandWidth;
        public float strandWidthShare;
        /// <summary>Stretched strands: the glow's width (m).</summary>
        public float glowWidth;
    }

    /// <summary>
    /// Lightning drawn with particles: a bolt from one point to another cut into jagged segments (each joint pushed
    /// aside, the middle arched over the crowd when the style asks), each segment a few strands over a soft glow that
    /// overshoot it a little so the joints overlap, with short side branches dipping toward the ground. The look comes
    /// from a <see cref="BoltStyle"/>. Main thread; no allocations.
    /// </summary>
    public static class LightningFx
    {
        /// <summary>Strands overshoot their segment by this share so neighbouring segments overlap at the joints.</summary>
        private const float SEGMENT_OVERLAP = 1.08f;
        /// <summary>The slow drift (m/s) along a segment that turns a sized strand to it (align_to_direction).</summary>
        private const float SEGMENT_DRIFT = 0.05f;
        private const float MIN_SEGMENT = 1e-3f;

        /// <summary>A jagged bolt from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public static void DrawBolt(Effects effects, Vector3 a, Vector3 b, in BoltStyle style)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            int segments = Math.Clamp((int)(length / style.step), 1, style.maxSegments);
            Vector3 forward = Ground.Normalize(delta, Vector3.forward);
            Vector3 side = Ground.Perpendicular(forward);
            float arch = MathF.Min(length * style.archShare, style.archMax);
            Vector3 previous = a;
            for (int k = 1; k <= segments; k++)
            {
                float t = (float)k / segments;
                Vector3 next = k == segments ? b : a + delta * t + Ground.Up(arch * MathF.Sin(t * MathF.PI)) + Jitter(side, style.jitter);
                DrawSegment(effects, previous, next, style, style.strands);
                if (k < segments && style.branchChance > 0.0f && Random.Float() < style.branchChance)
                {
                    DrawBranch(effects, next, forward, side, style);
                }
                previous = next;
            }
        }

        /// <summary>One straight segment: <paramref name="strands"/> strands along p-q over a soft glow.</summary>
        public static void DrawSegment(Effects effects, Vector3 p, Vector3 q, in BoltStyle style, int strands)
        {
            Vector3 span = q - p;
            float length = span.magnitude;
            if (length < MIN_SEGMENT)
            {
                return;
            }
            Vector3 middle = (p + q) * 0.5f;
            if (style.stretch > 0.0f)
            {
                Vector3 velocity = span * (SEGMENT_OVERLAP / style.stretch);
                float width = style.strandWidth > 0.0f ? style.strandWidth : length * style.strandWidthShare;
                effects.EmitMoving(style.strandEffect, middle, velocity, strands, style.core, width, style.seconds);
                effects.EmitMoving(style.glowEffect, middle, velocity, 1, style.glow, style.glowWidth, style.seconds);
                return;
            }
            Vector3 drift = span * (SEGMENT_DRIFT / length);
            float size = length * SEGMENT_OVERLAP;
            effects.EmitMoving(style.strandEffect, middle, drift, strands, style.core, size, style.seconds);
            effects.EmitMoving(style.glowEffect, middle, drift, 1, style.glow, size, style.seconds);
        }

        private static Vector3 Jitter(Vector3 side, float amount)
        {
            return side * Random.Range(-amount, amount) + Ground.Up(Random.Range(-amount, amount));
        }

        /// <summary>A short side branch off a joint, reaching forward and dipping toward the ground.</summary>
        private static void DrawBranch(Effects effects, Vector3 at, Vector3 forward, Vector3 side, in BoltStyle style)
        {
            float sign = Random.Float() < 0.5f ? -1.0f : 1.0f;
            Vector3 direction = Ground.Normalize(forward * style.branchForward + side * sign, side);
            Vector3 end = at + direction * Random.Range(style.branchLengthMin, style.branchLengthMax) - Ground.Up(Random.Range(0.0f, style.branchDrop));
            DrawSegment(effects, at, end, style, 1);
        }
    }
}

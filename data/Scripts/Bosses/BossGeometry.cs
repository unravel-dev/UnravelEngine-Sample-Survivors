using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Ground-plane shapes of boss attacks (circle, sector, lane, ring band with gaps) and heading helpers. Every
    /// test ignores height. <c>grace</c> grows a shape past its drawn edge: callers pass the share of the hero's
    /// body that may overlap the decal before the hit counts, so a hero whose feet touch the edge is still safe.
    /// Headings here are radians about up, 0 = +Z (the convention of <see cref="EnemyWorld.SetYaw"/>); the helpers say
    /// so in their names. <see cref="Ground"/> has the same helpers in degrees.
    /// </summary>
    public static class BossGeometry
    {
        private const float MIN_LENGTH = 1e-4f;

        /// <summary>The vector with its height dropped.</summary>
        public static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0.0f, value.z);

        /// <summary>Distance between two points on the ground.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The unit ground direction from one point to another, or <paramref name="fallback"/> when they meet.</summary>
        public static Vector3 FlatDirection(Vector3 from, Vector3 to, Vector3 fallback)
        {
            var delta = new Vector3(to.x - from.x, 0.0f, to.z - from.z);
            float length = delta.magnitude;
            return length > MIN_LENGTH ? delta / length : fallback;
        }

        /// <summary>The unit ground direction of a heading in radians.</summary>
        public static Vector3 FromYawRadians(float yaw) => new Vector3(MathF.Sin(yaw), 0.0f, MathF.Cos(yaw));

        /// <summary>The heading of a ground direction, in radians.</summary>
        public static float ToYawRadians(Vector3 direction) => MathF.Atan2(direction.x, direction.z);

        /// <summary>A ground direction turned by some degrees about up (positive turns from +Z toward +X).</summary>
        public static Vector3 Rotate(Vector3 direction, float degrees) => FromYawRadians(ToYawRadians(direction) + degrees * Mathf.Deg2Rad);

        /// <summary>A uniform random point in a disc on the ground.</summary>
        public static Vector3 RandomInDisc(System.Random rng, Vector3 center, float radius)
        {
            float angle = (float)rng.NextDouble() * MathF.PI * 2.0f;
            float distance = MathF.Sqrt((float)rng.NextDouble()) * radius;
            return Flat(center) + FromYawRadians(angle) * distance;
        }

        public static bool InCircle(Vector3 point, Vector3 center, float radius, float grace)
        {
            return FlatDistance(point, center) <= radius + grace;
        }

        /// <summary>Whether a point lies in a sector (a cone or an arc) opening from an origin along a heading.</summary>
        /// <param name="halfAngleDegrees">Half the opening: 60 for a 120 degree cone, 100 for a 200 degree arc.</param>
        public static bool InSector(Vector3 point, Vector3 origin, Vector3 forward, float radius, float halfAngleDegrees, float grace)
        {
            var to = new Vector3(point.x - origin.x, 0.0f, point.z - origin.z);
            float distance = to.magnitude;
            if (distance > radius + grace)
            {
                return false;
            }
            if (distance <= grace)
            {
                return true;
            }
            float angle = MathF.Acos(Math.Clamp(Vector3.Dot(to / distance, Flat(forward).normalized), -1.0f, 1.0f));
            float excess = angle - halfAngleDegrees * Mathf.Deg2Rad;
            // Outside the opening, the point still counts when its distance to the nearest edge ray is within grace.
            return excess <= 0.0f || distance * MathF.Sin(MathF.Min(excess, MathF.PI * 0.5f)) <= grace;
        }

        /// <summary>Whether a point lies in a rectangle running <paramref name="length"/> metres from an origin.</summary>
        public static bool InLane(Vector3 point, Vector3 origin, Vector3 forward, float length, float halfWidth, float grace)
        {
            Vector3 along = Flat(forward).normalized;
            var to = new Vector3(point.x - origin.x, 0.0f, point.z - origin.z);
            float a = Vector3.Dot(to, along);
            if (a < -grace || a > length + grace)
            {
                return false;
            }
            float lateral = MathF.Abs(to.x * along.z - to.z * along.x);
            return lateral <= halfWidth + grace;
        }

        /// <summary>How far along a lane a point lies (metres from its origin; negative behind it).</summary>
        public static float GetAlong(Vector3 point, Vector3 origin, Vector3 forward)
        {
            return Vector3.Dot(new Vector3(point.x - origin.x, 0.0f, point.z - origin.z), Flat(forward).normalized);
        }

        /// <summary>The signed side of a point relative to a lane (+1 right of the heading, -1 left).</summary>
        public static float GetSide(Vector3 point, Vector3 origin, Vector3 forward)
        {
            var to = new Vector3(point.x - origin.x, 0.0f, point.z - origin.z);
            return to.x * forward.z - to.z * forward.x >= 0.0f ? 1.0f : -1.0f;
        }

        /// <summary>Distance on the ground from a point to the segment a-b.</summary>
        public static float SegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = new Vector3(b.x - a.x, 0.0f, b.z - a.z);
            var ap = new Vector3(point.x - a.x, 0.0f, point.z - a.z);
            float lengthSquared = ab.sqrMagnitude;
            float t = lengthSquared > MIN_LENGTH ? Math.Clamp(Vector3.Dot(ap, ab) / lengthSquared, 0.0f, 1.0f) : 0.0f;
            return (ap - ab * t).magnitude;
        }

        /// <summary>
        /// Whether a point lies in a ring band (radius +- half thickness) outside its two gaps. The gaps are centered
        /// on <paramref name="gapDirection"/> and its opposite; a point whose center is inside a gap is safe.
        /// </summary>
        public static bool InGappedRing(Vector3 point, Vector3 center, float radius, float halfThickness, Vector3 gapDirection, float gapHalfAngleDegrees, float grace)
        {
            var to = new Vector3(point.x - center.x, 0.0f, point.z - center.z);
            float distance = to.magnitude;
            if (MathF.Abs(distance - radius) > halfThickness + grace)
            {
                return false;
            }
            if (distance <= MIN_LENGTH)
            {
                return true;
            }
            float cosine = Vector3.Dot(to / distance, Flat(gapDirection).normalized);
            float gapCosine = MathF.Cos(gapHalfAngleDegrees * Mathf.Deg2Rad);
            // |cos| covers both gaps: the one along the direction and the one opposite.
            return MathF.Abs(cosine) < gapCosine;
        }
    }
}

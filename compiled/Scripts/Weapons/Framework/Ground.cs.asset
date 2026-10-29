using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Ground-plane (XZ) vector helpers the weapons share. Yaw 0 points along +Z and positive yaw turns toward +X, the
    /// same convention as the enemy world.
    /// </summary>
    public static class Ground
    {
        public const float DEGREES_TO_RADIANS = MathF.PI / 180.0f;
        /// <summary>Degrees in a full turn.</summary>
        public const float FULL_TURN = 360.0f;
        private const float MIN_LENGTH = 1e-4f;

        public static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0.0f, value.z);

        /// <summary>A straight-up offset of <paramref name="height"/> metres (add it to a ground point).</summary>
        public static Vector3 Up(float height) => new Vector3(0.0f, height, 0.0f);

        /// <summary>The <paramref name="k"/>-th of <paramref name="count"/> directions spread evenly around a ring, from a yaw in degrees.</summary>
        public static Vector3 RingDirection(float startYaw, int k, int count) => FromYaw(startYaw + FULL_TURN * k / count);

        /// <summary>
        /// The <paramref name="n"/>-th spare direction (from 1) of a fan around <paramref name="first"/>: alternately to one
        /// side and the other, one more step out every second direction.
        /// </summary>
        public static Vector3 Fan(Vector3 first, int n, float stepDegrees)
        {
            float side = n % 2 == 1 ? 1.0f : -1.0f;
            return Rotate(first, side * stepDegrees * ((n + 1) / 2));
        }

        /// <summary>The unit ground direction from one point to another, or <paramref name="fallback"/> when they meet.</summary>
        public static Vector3 Direction(Vector3 from, Vector3 to, Vector3 fallback)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            float length = MathF.Sqrt(dx * dx + dz * dz);
            return length > MIN_LENGTH ? new Vector3(dx / length, 0.0f, dz / length) : fallback;
        }

        /// <summary>A ground direction made unit length, or <paramref name="fallback"/> when it is zero.</summary>
        public static Vector3 Normalize(Vector3 direction, Vector3 fallback)
        {
            float length = MathF.Sqrt(direction.x * direction.x + direction.z * direction.z);
            return length > MIN_LENGTH ? new Vector3(direction.x / length, 0.0f, direction.z / length) : fallback;
        }

        /// <summary>Turns a ground direction by some degrees about the up axis.</summary>
        public static Vector3 Rotate(Vector3 direction, float degrees)
        {
            float radians = degrees * DEGREES_TO_RADIANS;
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            return new Vector3(direction.x * cos + direction.z * sin, 0.0f, direction.z * cos - direction.x * sin);
        }

        /// <summary>The ground direction of a yaw in degrees.</summary>
        public static Vector3 FromYaw(float degrees)
        {
            float radians = degrees * DEGREES_TO_RADIANS;
            return new Vector3(MathF.Sin(radians), 0.0f, MathF.Cos(radians));
        }

        /// <summary>The yaw (degrees) of a ground direction.</summary>
        public static float ToYaw(Vector3 direction) => MathF.Atan2(direction.x, direction.z) / DEGREES_TO_RADIANS;

        /// <summary>The ground direction a quarter turn clockwise (seen from above) of <paramref name="direction"/>.</summary>
        public static Vector3 Perpendicular(Vector3 direction) => new Vector3(direction.z, 0.0f, -direction.x);

        public static float DistanceSquared(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        public static float Distance(Vector3 a, Vector3 b) => MathF.Sqrt(DistanceSquared(a, b));

        /// <summary>A random ground offset inside a circle.</summary>
        public static Vector3 RandomInCircle(float radius)
        {
            Vector2 point = Random.insideUnitCircle;
            return new Vector3(point.x * radius, 0.0f, point.y * radius);
        }

        /// <summary>A random unit ground direction.</summary>
        public static Vector3 RandomDirection() => FromYaw(RandomYaw());

        /// <summary>A random yaw in degrees (the start of a ring of directions).</summary>
        public static float RandomYaw() => Random.Range(0.0f, FULL_TURN);

        /// <summary>The point of segment a-b closest to <paramref name="point"/> on the ground plane.</summary>
        public static Vector3 ClosestOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float lengthSquared = abx * abx + abz * abz;
            if (lengthSquared < MIN_LENGTH)
            {
                return a;
            }
            float t = Math.Clamp(((point.x - a.x) * abx + (point.z - a.z) * abz) / lengthSquared, 0.0f, 1.0f);
            return new Vector3(a.x + abx * t, a.y, a.z + abz * t);
        }
    }
}

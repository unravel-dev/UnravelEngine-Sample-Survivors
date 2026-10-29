using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Square cells of the ground plane as one long key: the x cell in the high 32 bits, the z cell in the low ones.
    /// Dictionaries keyed by it bin positions (crowd clusters, far gem merging).
    /// </summary>
    internal static class GroundCell
    {
        private const int HALF_BITS = 32;
        private const long LOW_HALF = 0xffffffffL;

        /// <summary>The key of the cell of <paramref name="cellSize"/> m holding a ground position.</summary>
        public static long GetKey(Vector3 position, float cellSize)
        {
            int x = (int)MathF.Floor(position.x / cellSize);
            int z = (int)MathF.Floor(position.z / cellSize);
            return ((long)x << HALF_BITS) | (uint)z;
        }

        /// <summary>The ground center of a cell key.</summary>
        public static Vector3 GetCenter(long key, float cellSize)
        {
            float x = ((int)(key >> HALF_BITS) + 0.5f) * cellSize;
            float z = ((int)(key & LOW_HALF) + 0.5f) * cellSize;
            return new Vector3(x, 0.0f, z);
        }
    }
}

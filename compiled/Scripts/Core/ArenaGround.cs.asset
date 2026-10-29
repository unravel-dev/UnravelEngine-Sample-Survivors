using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The arena's blocked ground: one circle per <see cref="ArenaObstacle"/> in the scene, collected once when play
    /// starts. Enemies steer around the circles; spawners and map events ask <see cref="IsOpenGround"/> before they
    /// place anything. Main thread.
    /// </summary>
    public sealed class ArenaGround
    {
        /// <summary>Ground circles of the obstacles, packed as (centre x, centre z, radius).</summary>
        public readonly Vector3[] obstacles;

        private ArenaGround(Vector3[] obstacles)
        {
            this.obstacles = obstacles;
        }

        /// <summary>Reads every ArenaObstacle in the scene.</summary>
        public static ArenaGround Collect()
        {
            Entity[] marked = Scene.FindEntitiesWithComponent<ArenaObstacle>();
            var circles = new Vector3[marked.Length];
            for (int i = 0; i < marked.Length; i++)
            {
                Vector3 position = marked[i].transform.position;
                circles[i] = new Vector3(position.x, position.z, marked[i].GetComponent<ArenaObstacle>().radius);
            }
            return new ArenaGround(circles);
        }

        /// <summary>An arena without obstacles.</summary>
        public static ArenaGround Empty() => new ArenaGround(Array.Empty<Vector3>());

        /// <summary>Whether ground is clear of every obstacle within <paramref name="radius"/> of a point.</summary>
        public bool IsOpenGround(Vector3 position, float radius)
        {
            foreach (Vector3 circle in obstacles)
            {
                float dx = position.x - circle.x;
                float dz = position.z - circle.y;
                float reach = radius + circle.z;
                if (dx * dx + dz * dz < reach * reach)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

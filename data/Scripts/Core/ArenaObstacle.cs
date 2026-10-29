using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Marks scenery enemies walk around: a circle of <see cref="radius"/> metres on the ground at the entity. The run
    /// collects every marker when play starts; the hero collides with the entity's physics shapes instead.
    /// </summary>
    [ScriptSourceFile]
    public class ArenaObstacle : ScriptComponent
    {
        [Tooltip("Radius (m) on the ground enemies keep out of.")]
        public float radius = 1.0f;
    }
}

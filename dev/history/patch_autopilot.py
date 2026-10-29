"""A test autopilot that plays well enough to compare classes: scores directions by danger, seeks pickups (food when
hurt, chests), engages as a brawler when healthy, and uses the class active when hemmed in or low."""
import os
import re

from patchlib import edit

ROOT = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(ROOT, "src", "Scripts")
RUN = os.path.join(S, "Core", "Run.cs")

edit(os.path.join(S, "Pickups", "PickupWorld.cs"), [("""        /// <summary>Sends every gem (and gold) on the ground to the hero.</summary>""",
"""        /// <summary>The nearest pickup of one kind within a distance (the test autopilot's food and chest runs).</summary>
        public bool FindNearestOfKind(Vector3 from, float maxDistance, PickupKind kind, out Vector3 position)
        {
            position = from;
            float best = maxDistance * maxDistance;
            bool found = false;
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row] || kinds[row] != kind)
                {
                    continue;
                }
                Vector3 offset = positions[row] - from;
                float distance = offset.x * offset.x + offset.z * offset.z;
                if (distance < best)
                {
                    best = distance;
                    position = positions[row];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Sends every gem (and gold) on the ground to the hero.</summary>""")])

with open(RUN, newline="") as f:
    text = f.read()
start = text.index("        private Vector2 GetAutopilotInput()")
end = text.index("        /// <summary>One line about the run for test logs.</summary>")
newline = "\r\n" if "\r\n" in text else "\n"
replacement = """        private const int AUTOPILOT_DIRECTIONS = 16;
        private const float AUTOPILOT_SCAN = 12.0f;
        private const float AUTOPILOT_ARENA = 30.0f;
        private static readonly float[] AUTOPILOT_PROBES = { 1.0f, 2.5f, 4.5f };
        private static readonly float[] AUTOPILOT_PROBE_WEIGHTS = { 1.0f, 0.6f, 0.3f };
        private Vector3 autopilotHeading = Vector3.forward;

        /// <summary>
        /// The test autopilot: scores 16 directions by the crowd it would walk into (probes 1, 2.5 and 4.5 m ahead),
        /// the arena edge, the pull of pickups (food when hurt, chests, gems when the way is clear) and, for healthy
        /// brawlers, the nearest enemy; it keeps a little of its last heading and uses the class active when hemmed in
        /// or low. A test player, not a good one: compare classes with it, do not tune feel by it.
        /// </summary>
        private Vector2 GetAutopilotInput()
        {
            Vector3 position = hero.position;
            float health = hero.healthFraction;
            bool isBrawler = (heroClass == HeroClass.Knight || heroClass == HeroClass.Barbarian) && health > 0.55f;
            int found = Math.Min(enemies.QueryCircle(position, AUTOPILOT_SCAN, scratchRows), scratchRows.Length);
            int close = 0;
            for (int i = 0; i < found; i++)
            {
                Vector3 offset = enemies.GetPosition(scratchRows[i]) - position;
                if (offset.x * offset.x + offset.z * offset.z < 2.6f * 2.6f)
                {
                    close++;
                }
            }
            if (close >= 6 || health < 0.35f && close >= 2)
            {
                loadout.TryActivate();
            }
            Vector3 pull = GetAutopilotPull(position, health, found);
            Vector3 best = autopilotHeading;
            float bestScore = float.MinValue;
            for (int k = 0; k < AUTOPILOT_DIRECTIONS; k++)
            {
                Vector3 direction = Ground.FromYaw(360.0f * k / AUTOPILOT_DIRECTIONS);
                float danger = 0.0f;
                for (int p = 0; p < AUTOPILOT_PROBES.Length; p++)
                {
                    Vector3 probe = position + direction * AUTOPILOT_PROBES[p];
                    for (int i = 0; i < found; i++)
                    {
                        Vector3 offset = enemies.GetPosition(scratchRows[i]) - probe;
                        danger += AUTOPILOT_PROBE_WEIGHTS[p] / (offset.x * offset.x + offset.z * offset.z + 0.5f);
                    }
                    var flat = new Vector3(probe.x, 0.0f, probe.z);
                    float outside = flat.magnitude - AUTOPILOT_ARENA;
                    danger += outside > 0.0f ? outside * 2.0f : 0.0f;
                }
                float score = -danger * (isBrawler ? 0.35f : 1.0f) + Vector3.Dot(direction, pull) + 0.25f * Vector3.Dot(direction, autopilotHeading);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = direction;
                }
            }
            autopilotHeading = best;
            return new Vector2(best.x, best.z);
        }

        /// <summary>Where the autopilot wants to go besides away from danger: food when hurt, chests, gems, the crowd for brawlers.</summary>
        private Vector3 GetAutopilotPull(Vector3 position, float health, int found)
        {
            if (health < 0.6f && pickups.FindNearestOfKind(position, 25.0f, PickupKind.Food, out Vector3 food))
            {
                return Toward(position, food) * 2.0f;
            }
            if (pickups.FindNearestOfKind(position, 20.0f, PickupKind.Chest, out Vector3 chest))
            {
                return Toward(position, chest) * 1.5f;
            }
            bool isBrawler = (heroClass == HeroClass.Knight || heroClass == HeroClass.Barbarian) && health > 0.55f;
            if (isBrawler && enemies.QueryNearest(position, 14.0f, out int nearest))
            {
                return Toward(position, enemies.GetPosition(nearest)) * 1.2f;
            }
            if (found < 8 && pickups.FindNearest(position, 14.0f, out Vector3 pickup))
            {
                return Toward(position, pickup) * 1.0f;
            }
            return Vector3.zero;
        }

        private static Vector3 Toward(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0.0f;
            return offset.sqrMagnitude > 1e-4f ? offset.normalized : Vector3.zero;
        }

""".replace("\n", newline)
text = text[:start] + replacement + text[end:]
with open(RUN, "w", newline="") as f:
    f.write(text)
print("autopilot patched")

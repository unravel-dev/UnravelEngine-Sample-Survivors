using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Chest beacons: a chest on the ground is impossible to miss under the horde. A light pillar stands on it, glints
    /// rise up the pillar and a ring breathes out over the ground every second; gold for one item, violet for three,
    /// shifting through the colours for five. A new chest shoots its pillar up from the ground with a flash and a
    /// chime. The overlay adds an icon above it while it is on screen and an edge arrow when it is not
    /// (Run.ShowMarkers). The looks are the map events' (<see cref="EventFx"/>). Main thread.
    /// </summary>
    public sealed class ChestBeacons
    {
        private const int MAX_CHESTS = 8;
        private const float BEAM_HEIGHT = 12.0f;
        private const float BEAM_INTERVAL = 0.3f;
        private const float SPARKLE_INTERVAL = 0.12f;
        private const int SPARKLES_PER_BURST = 2;
        private const float SPARKLE_HEIGHT = 0.3f;
        private const float PULSE_INTERVAL = 1.0f;
        private const float PULSE_RADIUS = 2.4f;
        /// <summary>Size unit of the drop burst (m): its layer sizes are shares of it.</summary>
        private const float DROP_RADIUS = 3.0f;
        /// <summary>A chest farther than this from every chest of the last frame is new.</summary>
        private const float NEW_DISTANCE = 0.3f;
        private const float PRISM_HUE_SPEED = 0.35f;
        private const float PRISM_SATURATION = 0.55f;
        private const int BIG_CHEST_ITEMS = 5;
        private const int MEDIUM_CHEST_ITEMS = 3;

        private static readonly Color GOLD = new Color(1.0f, 0.8f, 0.36f, 1.0f);
        private static readonly Color VIOLET = new Color(0.74f, 0.52f, 1.0f, 1.0f);

        private readonly Run run;
        private readonly EventFx fx;
        /// <summary>A new chest's pillar shooting up with a flash, a ring and a burst of glints.</summary>
        private readonly FxComposite drop;
        private readonly Vector3[] positions = new Vector3[MAX_CHESTS];
        private readonly float[] items = new float[MAX_CHESTS];
        private readonly Vector3[] known = new Vector3[MAX_CHESTS];
        private int knownCount;
        private float beamTimer;
        private float sparkleTimer;
        private float pulseTimer;
        private float clock;

        public ChestBeacons(Run run)
        {
            this.run = run;
            fx = new EventFx(run.effects);
            drop = new FxComposite(run.effects, new FxLayer("CoreFlash", 1, 0.8f), new FxLayer("CoreBeamBurst", 1, BEAM_HEIGHT / DROP_RADIUS),
                                   FxLayer.Ground("CoreRing", EventFx.RING_SIZE_PER_RADIUS), new FxLayer("CoreSparkle", 16));
        }

        /// <summary>Forgets the chests of the last run.</summary>
        public void Reset()
        {
            knownCount = 0;
        }

        /// <summary>Once per frame while playing.</summary>
        public void Tick(float dt)
        {
            clock += dt;
            int count = run.pickups.CollectChests(positions, items);
            for (int i = 0; i < count; i++)
            {
                if (!IsKnown(positions[i]))
                {
                    Drop(positions[i], GetColor(items[i]));
                }
            }
            Array.Copy(positions, known, count);
            knownCount = count;
            beamTimer -= dt;
            sparkleTimer -= dt;
            pulseTimer -= dt;
            bool beams = beamTimer <= 0.0f;
            bool sparkles = sparkleTimer <= 0.0f;
            bool pulses = pulseTimer <= 0.0f;
            beamTimer = beams ? BEAM_INTERVAL : beamTimer;
            sparkleTimer = sparkles ? SPARKLE_INTERVAL : sparkleTimer;
            pulseTimer = pulses ? PULSE_INTERVAL : pulseTimer;
            for (int i = 0; i < count; i++)
            {
                Color color = GetColor(items[i]);
                if (beams)
                {
                    fx.Beacon(positions[i], color, BEAM_HEIGHT);
                }
                if (sparkles)
                {
                    fx.Emit(fx.sparkle, positions[i] + new Vector3(0.0f, SPARKLE_HEIGHT, 0.0f), SPARKLES_PER_BURST, color);
                }
                if (pulses)
                {
                    fx.pulse.Play(positions[i], PULSE_RADIUS, 1.0f, color);
                }
            }
        }

        private bool IsKnown(Vector3 position)
        {
            for (int i = 0; i < knownCount; i++)
            {
                if ((known[i] - position).sqrMagnitude < NEW_DISTANCE * NEW_DISTANCE)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A new chest: its pillar shoots up with a flash, a ring and a burst of glints, and a chime plays.</summary>
        private void Drop(Vector3 position, Color color)
        {
            drop.Play(position, DROP_RADIUS, 1.0f, color);
            run.sounds.Play("reel_land_evolution");
        }

        /// <summary>Gold for one item, violet for three, a slow walk through the colours for five.</summary>
        private Color GetColor(float itemCount)
        {
            if (itemCount >= BIG_CHEST_ITEMS)
            {
                float hue = clock * PRISM_HUE_SPEED % 1.0f;
                return Color.HSVToRGB(hue, PRISM_SATURATION, 1.0f);
            }
            return itemCount >= MEDIUM_CHEST_ITEMS ? VIOLET : GOLD;
        }
    }
}

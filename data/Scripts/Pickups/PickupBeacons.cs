using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Pickup beacons: what matters on the ground is impossible to miss under the horde. A light pillar stands on it,
    /// glints rise up the pillar and a ring breathes out over the ground every second: gold for a chest of one item, violet
    /// for three, shifting through the colours for five; a power-up (roast, lodestone, bell, frost shard) burns in its
    /// own colour, also while it still hangs in a Grave Lantern. Something new shoots its pillar up from the ground with a
    /// flash and, on the ground, a chime. The overlay adds an icon above it while it is on screen and an edge arrow when it
    /// is not (<see cref="RunMarkers"/>). The looks are the map events' (<see cref="EventFx"/>). Main thread.
    /// </summary>
    public sealed class PickupBeacons
    {
        private const int MAX_BEACONS = 16;
        private const float CHEST_BEAM_HEIGHT = 12.0f;
        private const float POWER_UP_BEAM_HEIGHT = 8.0f;
        private const float BEAM_INTERVAL = 0.3f;
        private const float SPARKLE_INTERVAL = 0.12f;
        private const int SPARKLES_PER_BURST = 2;
        private const float SPARKLE_HEIGHT = 0.3f;
        private const float PULSE_INTERVAL = 1.0f;
        private const float PULSE_RADIUS = 2.4f;
        /// <summary>Size unit of the drop burst (m): its layer sizes are shares of it.</summary>
        private const float DROP_RADIUS = 3.0f;
        /// <summary>A beacon farther than this from every beacon of the last frame is new.</summary>
        private const float NEW_DISTANCE = 0.3f;
        private const float PRISM_HUE_SPEED = 0.35f;
        private const float PRISM_SATURATION = 0.55f;
        private const int BIG_CHEST_ITEMS = 5;
        private const int MEDIUM_CHEST_ITEMS = 3;
        private const float POWER_UP_CHIME_VOLUME = 0.6f;
        private const float POWER_UP_CHIME_PITCH = 1.25f;

        private static readonly Color GOLD = new Color(1.0f, 0.8f, 0.36f, 1.0f);
        private static readonly Color VIOLET = new Color(0.74f, 0.52f, 1.0f, 1.0f);

        private readonly Run run;
        private readonly EventFx fx;
        /// <summary>A new beacon's pillar shooting up with a flash, a ring and a burst of glints.</summary>
        private readonly FxComposite drop;
        private readonly PickupBeacon[] beacons = new PickupBeacon[MAX_BEACONS];
        private readonly Vector3[] known = new Vector3[MAX_BEACONS];
        private int count;
        private int knownCount;
        private float beamTimer;
        private float sparkleTimer;
        private float pulseTimer;
        private float clock;

        public PickupBeacons(Run run)
        {
            this.run = run;
            fx = new EventFx(run.effects);
            drop = new FxComposite(run.effects, new FxLayer("CoreFlash", 1, 0.8f), new FxLayer("CoreBeamBurst", 1, CHEST_BEAM_HEIGHT / DROP_RADIUS),
                                   FxLayer.Ground("CoreRing", EventFx.RING_SIZE_PER_RADIUS), new FxLayer("CoreSparkle", 16));
        }

        /// <summary>This frame's chests and power-ups, for the markers.</summary>
        public ReadOnlySpan<PickupBeacon> current => beacons.AsSpan(0, count);

        /// <summary>Forgets the beacons of the last run.</summary>
        public void Reset()
        {
            count = 0;
            knownCount = 0;
        }

        /// <summary>Once per frame while playing.</summary>
        public void Tick(float dt)
        {
            clock += dt;
            count = run.pickups.CollectBeaconed(beacons);
            for (int i = 0; i < count; i++)
            {
                if (!IsKnown(beacons[i].position))
                {
                    Drop(beacons[i]);
                }
                known[i] = beacons[i].position;
            }
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
                Vector3 position = beacons[i].position;
                Color color = GetColor(beacons[i]);
                if (beams)
                {
                    fx.Beacon(position, color, beacons[i].kind == PickupKind.Chest ? CHEST_BEAM_HEIGHT : POWER_UP_BEAM_HEIGHT);
                }
                if (sparkles)
                {
                    fx.Emit(fx.sparkle, position + new Vector3(0.0f, SPARKLE_HEIGHT, 0.0f), SPARKLES_PER_BURST, color);
                }
                if (pulses)
                {
                    fx.pulse.Play(position, PULSE_RADIUS, 1.0f, color);
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

        /// <summary>Something new: its pillar shoots up with a flash, a ring and a burst of glints; a chime when it lies on the ground.</summary>
        private void Drop(in PickupBeacon beacon)
        {
            drop.Play(beacon.position, DROP_RADIUS, 1.0f, GetColor(beacon));
            if (beacon.kind == PickupKind.Chest)
            {
                run.sounds.Play("reel_land_evolution");
            }
            else if (!beacon.isHeld)
            {
                run.sounds.Play("reel_land_evolution", POWER_UP_CHIME_VOLUME, POWER_UP_CHIME_PITCH);
            }
        }

        /// <summary>A power-up's own colour; a chest: gold for one item, violet for three, a slow walk through the colours for five.</summary>
        private Color GetColor(in PickupBeacon beacon)
        {
            if (beacon.kind != PickupKind.Chest)
            {
                return PickupCatalog.GetColor(beacon.kind);
            }
            if (beacon.value >= BIG_CHEST_ITEMS)
            {
                float hue = clock * PRISM_HUE_SPEED % 1.0f;
                return Color.HSVToRGB(hue, PRISM_SATURATION, 1.0f);
            }
            return beacon.value >= MEDIUM_CHEST_ITEMS ? VIOLET : GOLD;
        }
    }
}

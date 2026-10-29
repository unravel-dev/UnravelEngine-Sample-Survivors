using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The shared looks of map events and chest beacons: a light pillar over a spot, a steady ring on the edge of its
    /// ground and a soft glow over it (both re-emitted every <see cref="REFRESH"/> s), the start, win and fade bursts,
    /// and the bone pyres' flames. Every piece is tinted by the caller's colour.
    /// </summary>
    public sealed class EventFx
    {
        /// <summary>Seconds between re-emits of the steady looks (their particles live twice as long).</summary>
        public const float REFRESH = 0.3f;
        public const float BEAM_HEIGHT = 14.0f;
        /// <summary>Rings (circle_02) draw their line at 0.29 x the size: the size per radius of the ring they show.</summary>
        public const float RING_SIZE_PER_RADIUS = 3.4f;
        /// <summary>The floor glow fades out before its edge: its size per radius of the zone it fills.</summary>
        private const float FLOOR_SIZE_PER_RADIUS = 2.6f;
        private const float GROUND_LIFT = 0.05f;
        /// <summary>Size unit of the start, win and fade bursts (m): their layer sizes are shares of it.</summary>
        private const float BURST_UNIT = 3.0f;

        private readonly Effects effects;
        private readonly int beam;
        private readonly int ring;
        private readonly int floor;
        public readonly int flame;
        public readonly int motes;
        public readonly int sparkle;
        public readonly int rune;
        /// <summary>A ring breathing out over the ground; its size unit is the ring's radius.</summary>
        public readonly FxComposite pulse;
        public readonly FxComposite start;
        public readonly FxComposite win;
        public readonly FxComposite fade;
        public readonly FxComposite shatter;

        public EventFx(Effects effects)
        {
            this.effects = effects;
            beam = effects.Find("CoreBeam");
            ring = effects.Find("EventZoneRing");
            floor = effects.Find("EventZoneFloor");
            flame = effects.Find("EventFlame");
            motes = effects.Find("CoreMotes");
            sparkle = effects.Find("CoreSparkle");
            rune = effects.Find("CoreRune");
            pulse = new FxComposite(effects, FxLayer.Ground("CorePulseRing", RING_SIZE_PER_RADIUS));
            start = new FxComposite(effects, new FxLayer("CoreFlash", 1, 0.8f), new FxLayer("CoreBeamBurst", 1, BEAM_HEIGHT / BURST_UNIT),
                                    FxLayer.Ground("CoreRing", RING_SIZE_PER_RADIUS * 1.5f), new FxLayer("CoreSparkle", 12));
            win = new FxComposite(effects, new FxLayer("CoreFlash", 1, 1.2f), new FxLayer("CoreBeamBurst", 1, BEAM_HEIGHT * 1.5f / BURST_UNIT),
                                  FxLayer.Ground("CoreRing", RING_SIZE_PER_RADIUS * 2.0f), new FxLayer("CoreMotes", 30), new FxLayer("CoreSparkle", 24));
            fade = new FxComposite(effects, new FxLayer("GraveBurst", 1, 0.0f, false), FxLayer.Ground("CorePulseRing", RING_SIZE_PER_RADIUS),
                                   new FxLayer("CoreMotes", 10));
            shatter = new FxComposite(effects, new FxLayer("LanternShatter", 1, 0.0f, false), new FxLayer("BoneBurst", 1, 0.0f, false),
                                      new FxLayer("CoreFlash", 1, 0.5f), new FxLayer("CoreMotes", 14));
        }

        /// <summary>Plays a start, win or fade burst at its usual size.</summary>
        public void PlayBurst(FxComposite burst, Vector3 at, Color color)
        {
            burst.Play(at, BURST_UNIT, 1.0f, color);
        }

        /// <summary>The pillar over a spot.</summary>
        public void Beacon(Vector3 at, Color color, float height = BEAM_HEIGHT)
        {
            effects.EmitSized(beam, at, 1, color, height);
        }

        /// <summary>The ring on the edge of a zone and the glow over it (glow 0 draws only the ring), for one refresh.</summary>
        public void Zone(Vector3 at, float radius, Color color, float glow)
        {
            Vector3 ground = new Vector3(at.x, GROUND_LIFT, at.z);
            effects.EmitSized(ring, ground, 1, color, radius * RING_SIZE_PER_RADIUS);
            if (glow > 0.0f)
            {
                effects.EmitSized(floor, ground, 1, new Color(color.r * glow, color.g * glow, color.b * glow, color.a), radius * FLOOR_SIZE_PER_RADIUS);
            }
        }

        public void Emit(int effect, Vector3 at, int count, Color color)
        {
            effects.Emit(effect, at, count, color);
        }

        public void EmitSized(int effect, Vector3 at, Color color, float size)
        {
            effects.EmitSized(effect, at, 1, color, size);
        }
    }
}

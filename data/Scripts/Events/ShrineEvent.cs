using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Shrine of Vigil (hold the ground): a candle shrine in a 4.5 m ring of pale gold light. Stepping into the ring
    /// starts the vigil: 10 s inside it within 30 s (outside, the vigil drains at half the speed) while the dead rise
    /// on a ring every 2.5 s and march on the shrine, 10 + 2 per minute each wave. Kept: a chest of 3 items (1 before
    /// minute 5) and a shove that throws the dead out of the ring. Lost: the candles gutter and the dead stay.
    /// Look: the pillar while it waits; the ring brightens and the floor fills with light as the vigil grows, a rune
    /// flares under the shrine and motes rise off the hero while it stands inside.
    /// </summary>
    public sealed class ShrineEvent : MapEvent
    {
        private const float RADIUS = 4.5f;
        private const float HOLD_SECONDS = 10.0f;
        private const float DRAIN_SHARE = 0.5f;
        private const float LIMIT_SECONDS = 30.0f;
        private const float WAVE_INTERVAL = 2.5f;
        private const float WAVE_RADIUS = 13.0f;
        private const int WAVE_BASE = 10;
        private const float WAVE_PER_MINUTE = 2.0f;
        private const float BIG_CHEST_MINUTE = 5.0f;
        private const int SMALL_CHEST_ITEMS = 1;
        private const int BIG_CHEST_ITEMS = 3;
        /// <summary>The chest lands just in front of the shrine (m along -Z).</summary>
        private const float CHEST_OFFSET = 1.6f;
        private const float SHOVE_RADIUS = 7.0f;
        private const float SHOVE_SPEED = 16.0f;
        private const float MESH_SCALE = 1.4f;
        private const float RUNE_INTERVAL = 0.6f;
        /// <summary>The rune's outer circle is at 0.46 x its size.</summary>
        private const float RUNE_SIZE = 2.17f * 1.6f;
        private const float RUNE_HEIGHT = 0.06f;
        private const float GLOW_WAITING = 0.35f;
        private const float GLOW_MIN = 0.4f;
        private const float GLOW_MAX = 1.1f;
        /// <summary>The pillar grows from half height to full as the vigil fills.</summary>
        private const float BEAM_MIN_SHARE = 0.5f;
        private const int HERO_MOTES = 2;
        private const float HERO_MOTES_HEIGHT = 0.4f;
        private const float FULL_TURN_DEGREES = 360.0f;
        private const int SCRATCH = 512;

        private static readonly Color GOLD = new Color(1.0f, 0.86f, 0.52f, 1.0f);
        private static readonly Color PALE = new Color(0.85f, 0.85f, 0.8f, 1.0f);
        private static readonly SurgeRule SURGE = new SurgeRule(WAVE_INTERVAL, WAVE_BASE, WAVE_PER_MINUTE, WAVE_RADIUS);

        private readonly Mesh mesh;
        private readonly Matrix4x4[] transform = new Matrix4x4[1];
        private readonly int[] scratch = new int[SCRATCH];
        private float runeTimer;

        public override MapEventKind kind => MapEventKind.Shrine;
        public override string title => "Shrine of Vigil";
        public override string goal => state == MapEventState.Waiting ? "Step into the ring to begin the vigil."
                                     : "Hold the ring for 10 seconds.";
        public override Color color => GOLD;
        public override string icon => UIText.Icon("chest");
        public override MarkerKind markerKind => MarkerKind.Shrine;

        public ShrineEvent(Run run, EventFx fx, Vector3 center) : base(run, fx, center)
        {
            mesh = Assets.GetAsset<Mesh>("app:/data/Environment/Halloween/gltf/shrine_candles.gltf");
            transform[0] = Matrix4x4.TRS(center, Quaternion.Euler(0.0f, (float)run.rng.NextDouble() * FULL_TURN_DEGREES, 0.0f),
                                         new Vector3(MESH_SCALE, MESH_SCALE, MESH_SCALE));
            Announce("A shrine of vigil glows on the moor");
        }

        protected override void Tick(float dt, bool refresh)
        {
            bool inside = IsHeroWithin(RADIUS);
            if (state == MapEventState.Waiting)
            {
                if (refresh)
                {
                    ShowWaiting(RADIUS, GLOW_WAITING);
                }
                if (inside)
                {
                    Activate(LIMIT_SECONDS, "Hold the shrine");
                }
                return;
            }
            progress = Math.Clamp(progress + (inside ? dt : -dt * DRAIN_SHARE) / HOLD_SECONDS, 0.0f, 1.0f);
            TickSurge(dt, SURGE);
            ShowVigil(dt, refresh, inside);
            if (progress >= 1.0f)
            {
                Keep();
            }
        }

        private void ShowVigil(float dt, bool refresh, bool inside)
        {
            Color shade = Color.Lerp(PALE, GOLD, progress);
            if (refresh)
            {
                fx.Beacon(center, shade, EventFx.BEAM_HEIGHT * (BEAM_MIN_SHARE + (1.0f - BEAM_MIN_SHARE) * progress));
                fx.Zone(center, RADIUS, shade, GLOW_MIN + (GLOW_MAX - GLOW_MIN) * progress);
                if (inside)
                {
                    fx.Emit(fx.motes, run.hero.position + new Vector3(0.0f, HERO_MOTES_HEIGHT, 0.0f), HERO_MOTES, GOLD);
                }
            }
            runeTimer -= dt;
            if (runeTimer <= 0.0f)
            {
                runeTimer = RUNE_INTERVAL;
                fx.EmitSized(fx.rune, center + new Vector3(0.0f, RUNE_HEIGHT, 0.0f), shade, RUNE_SIZE);
            }
        }

        /// <summary>The vigil is kept: the chest, a shove that clears the ring, the win burst.</summary>
        private void Keep()
        {
            progress = 1.0f;
            int items = run.minutes < BIG_CHEST_MINUTE ? SMALL_CHEST_ITEMS : BIG_CHEST_ITEMS;
            run.pickups.Spawn(PickupKind.Chest, center + new Vector3(0.0f, 0.0f, -CHEST_OFFSET), items);
            run.enemies.PushAway(center, SHOVE_RADIUS, SHOVE_SPEED, scratch);
            Win("The vigil is kept");
        }

        protected override void TimeOut()
        {
            Lose("The candles gutter out");
        }

        public override void Draw()
        {
            if (state == MapEventState.Expired || !AssetChecks.IsLoaded(mesh))
            {
                return;
            }
            Graphics.DrawMeshInstanced(mesh, null, transform);
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The look of Butcher's Chains (the weapon decides what happens; this draws it). Iron chains of alternating flat
    /// and edge-on links run from the barbarian's hand to a crescent hook, re-emitted every frame so moving chains never
    /// smear, with a steel glint and a wake on the flying hook; the bite throws grit, dust and clods over a small crack,
    /// and a chain snapping back bows like a whip. Yanked enemies spurt blood and slide in on a puff of dust that
    /// travels exactly as far as they do, ploughing a furrow; bloodied enemies drip; a bloodied death bursts in blood
    /// and sends a red Fury streak into the barbarian. Maw of the Pit: blood-darkened chains with crimson glints; a
    /// dark blood vortex with crimson spiral arms collapses at his feet while blood mist spirals in, dust rises under
    /// the dragged crowd and the hooks scrape the ground; the slam lays the chains on the ground in a star around a
    /// crimson flash, shockwave, dust ring, blood, rocks and a glowing crack. Main thread; no allocations.
    /// </summary>
    internal sealed class ChainFx
    {
        private const float LINK_SPACING = 0.42f;
        /// <summary>A link's quad per metre of spacing: neighbours overlap like interlocked links.</summary>
        private const float LINK_SIZE_PER_SPACING = 1.45f;
        private const int MAX_LINKS = 30;
        private const float HAND_HEIGHT = 1.3f;
        private const float HAND_REACH = 0.35f;
        private const float HOOK_HEIGHT = 1.15f;
        private const float HOOK_DRAG_HEIGHT = 0.15f;
        private const float HOOK_SIZE = 0.95f;
        /// <summary>Links stop this short of the hook so its crescent shows.</summary>
        private const float HOOK_GAP = 0.35f;
        private const float GLINT_SIZE = 0.3f;
        private const float WAKE_SPEED = 6.0f;
        private const float WAKE_SIZE = 0.45f;
        private const float WAKE_SECONDS = 0.12f;
        private const float WHIP_BOW = 0.5f;
        /// <summary>The whip's bow swings at ~5 Hz (radians per second).</summary>
        private const float WHIP_RATE = 31.0f;
        private const float BITE_HEIGHT = 0.3f;
        private const float BITE_CRACK_SIZE = 1.4f;
        private const float BITE_CRACK_SECONDS = 1.2f;
        private const float THROW_DUST_SIZE = 0.5f;
        private const int THROW_SPARKS = 4;
        private const int CATCH_SPARKS = 3;
        private const float DRAG_DUST_HEIGHT = 0.15f;
        private const float DRAG_DUST_SIZE = 0.55f;
        private const float DRAG_DUST_SECONDS = 0.7f;
        private const float FURROW_MIN = 0.8f;
        /// <summary>A furrow stroke is sized this many times its length (trace_06's streak covers 40% of the quad).</summary>
        private const float FURROW_SIZE_PER_METRE = 2.6f;
        private const float FURROW_SECONDS = 1.4f;
        private const int YANK_SPURT = 2;
        private const float BLOOD_HEIGHT_SHARE = 0.6f;
        private const float DRIP_HEIGHT_SHARE = 0.7f;
        private const float DRIP_SPREAD = 0.15f;
        private const int DEATH_SPRAY = 5;
        private const float MOTE_HEIGHT = 0.8f;
        private const float MOTE_SPEED = 12.0f;
        private const float MOTE_SIZE = 0.3f;
        private const float MOTE_MIN_DISTANCE = 0.5f;
        /// <summary>The vortex quad per metre of reach (its arms fade out at ~0.42 x the quad: about the reach).</summary>
        private const float VORTEX_PER_REACH = 2.4f;
        private const float VORTEX_SECONDS = 0.6f;
        private const float SWIRL_PER_REACH = 1.2f;
        private const float SWIRL_SECONDS = 0.5f;
        private const int VORTEX_MIST = 3;
        private const float MIST_RIM_SHARE = 0.9f;
        private const float MIST_HEIGHT = 0.4f;
        private const float MIST_SPEED = 7.0f;
        /// <summary>The mist spirals half around, half inward: this scales that diagonal to unit length (1 / sqrt 2).</summary>
        private const float SPIRAL_DIAGONAL = 0.7071f;
        private const float MIST_SIZE = 0.9f;
        private const float MIST_SECONDS = 0.9f;
        private const int VORTEX_DUST_ROWS = 4;
        private const float VORTEX_DUST_SPEED = 6.0f;
        private const float SCRAPE_DUST_SIZE = 0.35f;
        /// <summary>The slam's burst sits at chest height so it shows over the pile the maw dragged in.</summary>
        private const float SLAM_HEIGHT = 1.2f;
        private const float SLAM_CHAIN_SHARE = 1.4f;
        private const float SLAM_CHAIN_HEIGHT = 0.08f;
        /// <summary>A ring of crimson air above the heads of the pile (a ring quad per metre of the slam radius).</summary>
        private const float SLAM_RING_HEIGHT = 1.8f;
        private const float SLAM_RING_PER_RADIUS = 3.2f;
        private const float SLAM_RING_SECONDS = 0.4f;
        private const float SLAM_CHAIN_SECONDS = 0.6f;
        private const float SLAM_CRACK_SHARE = 1.7f;
        private const float SLAM_CRACK_SECONDS = 2.0f;
        private const float SLAM_GLOW_SECONDS = 0.7f;

        private static readonly Color WHITE = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        /// <summary>The chains' steel: paler than BarbarianLooks.STEEL.</summary>
        private static readonly Color CHAIN_STEEL = new Color(0.95f, 0.9f, 0.82f, 1.0f);
        /// <summary>Maw of the Pit's chains: iron darkened with old blood.</summary>
        private static readonly Color BLOODY_IRON = new Color(1.0f, 0.62f, 0.58f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.GROUND_LIFT);

        private readonly Effects effects;
        private readonly EnemyWorld enemies;
        private readonly int link;
        private readonly int linkEdge;
        private readonly int hookLook;
        private readonly int glint;
        private readonly int streak;
        private readonly int grit;
        private readonly int dust;
        private readonly int crack;
        private readonly int crackGlow;
        private readonly int dragDust;
        private readonly int furrow;
        private readonly int bloodSpray;
        private readonly int bloodDrip;
        private readonly int furyMote;
        private readonly int vortex;
        private readonly int swirl;
        private readonly int mist;
        private readonly int airRing;
        private readonly FxComposite bite;
        private readonly FxComposite slamBurst;
        private readonly FxComposite slamGround;
        private float fxScale = 1.0f;
        private bool isMaw;

        public ChainFx(Effects effects, EnemyWorld enemies)
        {
            this.effects = effects;
            this.enemies = enemies;
            link = effects.Find("BarbarianChainLink");
            linkEdge = effects.Find("BarbarianChainLinkEdge");
            hookLook = effects.Find("BarbarianChainHook");
            glint = effects.Find("HeadStar");
            streak = effects.Find("BarbarianSweepStreak");
            grit = effects.Find("BarbarianGrit");
            dust = effects.Find("BarbarianDust");
            crack = effects.Find("BarbarianCrack");
            crackGlow = effects.Find("BarbarianCrackGlow");
            dragDust = effects.Find("BarbarianDragDust");
            furrow = effects.Find("BarbarianDragFurrow");
            bloodSpray = effects.Find("BarbarianBloodSpray");
            bloodDrip = effects.Find("BarbarianBloodDrip");
            furyMote = effects.Find("BarbarianFuryMote");
            vortex = effects.Find("BarbarianBloodVortex");
            swirl = effects.Find("BarbarianBloodSwirl");
            mist = effects.Find("BarbarianBloodMist");
            airRing = effects.Find("BarbarianRoarWave");
            // Size unit = 1 m.
            bite = new FxComposite(effects, new FxLayer("BarbarianGrit", 6), new FxLayer("BarbarianDust", 2, 0.5f, false),
                                   new FxLayer("BarbarianDirtSpray", 2, 0.0f, false));
            // Size unit = slam radius.
            slamBurst = new FxComposite(effects, new FxLayer("BarbarianFlash", 1, 0.8f), new FxLayer("BarbarianBloodSpray", 16, 0.0f, false),
                                        new FxLayer("BarbarianGrit", 16));
            slamGround = new FxComposite(effects, FxLayer.Ground("BarbarianShockRing", 2.2f, false), FxLayer.Ground("BarbarianDustRing", 2.4f, false),
                                         new FxLayer("BarbarianRocks", 10, 0.0f, false), new FxLayer("BarbarianBoulders", 2, 0.0f, false),
                                         new FxLayer("BarbarianDirtSpray", 6, 0.0f, false), new FxLayer("BarbarianDust", 6, 0.3f, false));
        }

        /// <summary>The weapon's grandeur and form, set whenever its rank or evolution changes.</summary>
        public void SetLook(float scale, bool isMawOfThePit)
        {
            fxScale = scale;
            isMaw = isMawOfThePit;
        }

        private Color ironColor => isMaw ? BLOODY_IRON : WHITE;

        /// <summary>The barbarian's hand, the end a chain leaves from toward <paramref name="direction"/>.</summary>
        public static Vector3 HandOf(Vector3 origin, Vector3 direction) => origin + Ground.Up(HAND_HEIGHT) + direction * HAND_REACH;

        /// <summary>
        /// One chain this frame: links from the hand to the hook (anchored at the hand, so they hold still while the
        /// chain pays out or snaps back; anchored at the hook while the Maw reels in, so they slide in with it), the
        /// hook, a glint and a wake while it flies, and a whip's bow while it snaps back. Every part lives
        /// <paramref name="lifetime"/>, about one frame.
        /// </summary>
        public void DrawChain(Vector3 origin, in ChainState chain, float elapsed, float lifetime)
        {
            Vector3 hand = HandOf(origin, chain.direction);
            bool isReeling = chain.phase == ChainPhase.Reel;
            Vector3 hook = Ground.Flat(origin) + chain.direction * (HAND_REACH + chain.tip) + Ground.Up(isReeling ? HOOK_DRAG_HEIGHT : HOOK_HEIGHT);
            float bow = chain.phase == ChainPhase.Back ? WHIP_BOW * chain.side * (chain.tip / chain.range) * MathF.Cos(WHIP_RATE * elapsed) : 0.0f;
            float spacing = MathF.Max(LINK_SPACING, chain.range / MAX_LINKS);
            DrawLinks(hand, hook, spacing, isReeling, bow, lifetime);
            effects.EmitMoving(hookLook, hook, -chain.direction * FxSizes.HEADING_SPEED, 1, ironColor, HOOK_SIZE * fxScale, lifetime);
            if (chain.phase == ChainPhase.Out)
            {
                effects.EmitSized(glint, hook, 1, isMaw ? BarbarianLooks.CRIMSON : CHAIN_STEEL, GLINT_SIZE * fxScale);
                effects.EmitMoving(streak, hook, chain.direction * WAKE_SPEED, 1, CHAIN_STEEL, WAKE_SIZE, WAKE_SECONDS);
            }
        }

        /// <summary>
        /// Alternating flat and edge-on links along a-b, every <paramref name="spacing"/> metres and stopping short of
        /// the hook at b; counted from a, or from b when <paramref name="fromHook"/>. A bow bends the chain sideways.
        /// </summary>
        private void DrawLinks(Vector3 a, Vector3 b, float spacing, bool fromHook, float bow, float lifetime)
        {
            Vector3 span = b - a;
            float length = span.magnitude - HOOK_GAP;
            if (length < spacing)
            {
                return;
            }
            Vector3 axis = span / span.magnitude;
            Vector3 heading = Ground.Normalize(axis, Vector3.forward) * FxSizes.HEADING_SPEED;
            Vector3 side = Ground.Perpendicular(Ground.Normalize(axis, Vector3.forward));
            float size = spacing * LINK_SIZE_PER_SPACING * fxScale;
            int links = Math.Min((int)(length / spacing), MAX_LINKS);
            Color color = ironColor;
            for (int k = 0; k < links; k++)
            {
                float along = fromHook ? length - spacing * (k + 0.5f) : spacing * (k + 0.5f);
                Vector3 at = a + axis * along + side * (bow * MathF.Sin(MathF.PI * along / length));
                effects.EmitMoving((k & 1) == 0 ? link : linkEdge, at, heading, 1, color, size, lifetime);
            }
        }

        /// <summary>He heaves the chains: stone sparks at the hand and a puff of dust at his feet.</summary>
        public void DrawThrow(Vector3 origin, Vector3 direction)
        {
            effects.Emit(grit, HandOf(origin, direction), THROW_SPARKS, CHAIN_STEEL);
            effects.EmitSized(dust, Ground.Flat(origin) + LIFT, 1, WHITE, THROW_DUST_SIZE);
        }

        /// <summary>The hook bites: grit, dust and clods over a small crack (crimson grit for the Maw).</summary>
        public void DrawBite(Vector3 end, Vector3 direction)
        {
            Vector3 ground = Ground.Flat(end) + LIFT;
            bite.Play(ground + Ground.Up(BITE_HEIGHT), FxSizes.METRE_UNIT, fxScale, isMaw ? BarbarianLooks.CRIMSON : CHAIN_STEEL);
            effects.EmitMoving(crack, ground, direction * FxSizes.HEADING_SPEED, 1, WHITE, BITE_CRACK_SIZE, BITE_CRACK_SECONDS);
        }

        /// <summary>The chain is back in his hand: a few stone sparks.</summary>
        public void DrawCatch(Vector3 origin, Vector3 direction)
        {
            effects.Emit(grit, HandOf(origin, direction), CATCH_SPARKS, CHAIN_STEEL);
        }

        /// <summary>A yanked enemy: blood spurts, and a puff of dust slides in with it over the furrow it ploughs.</summary>
        public void DrawYank(Vector3 position, Vector3 toward, float travel, float height)
        {
            effects.Emit(bloodSpray, position + Ground.Up(height * BLOOD_HEIGHT_SHARE), YANK_SPURT, WHITE);
            if (travel <= 0.0f)
            {
                return;
            }
            Vector3 feet = Ground.Flat(position);
            effects.EmitMoving(dragDust, feet + Ground.Up(DRAG_DUST_HEIGHT), toward * (travel * ButchersChains.ENEMY_KNOCKBACK_DAMPING), 1, DRAG_DUST_SIZE,
                               DRAG_DUST_SECONDS);
            if (travel >= FURROW_MIN)
            {
                effects.EmitMoving(furrow, feet + toward * (travel * 0.5f) + LIFT, toward * FxSizes.HEADING_SPEED, 1, WHITE,
                                   travel * FURROW_SIZE_PER_METRE, FURROW_SECONDS);
            }
        }

        /// <summary>A bloodied death bursts in blood and sends a red Fury streak into the barbarian's chest.</summary>
        public void DrawBloodiedDeath(Vector3 position, Vector3 heroChest)
        {
            Vector3 from = position + Ground.Up(MOTE_HEIGHT);
            effects.Emit(bloodSpray, from, DEATH_SPRAY, WHITE);
            Vector3 to = heroChest - from;
            float distance = to.magnitude;
            if (distance > MOTE_MIN_DISTANCE)
            {
                effects.EmitMoving(furyMote, from, to * (MOTE_SPEED / distance), 1, BarbarianLooks.FURY_RED, MOTE_SIZE, distance / MOTE_SPEED);
            }
        }

        /// <summary>A drop of blood falls from a bloodied enemy.</summary>
        public void DrawDrip(Vector3 position, float height)
        {
            Vector3 at = position + Ground.Up(height * DRIP_HEIGHT_SHARE) + Ground.RandomInCircle(DRIP_SPREAD);
            effects.Emit(bloodDrip, at, 1, WHITE);
        }

        /// <summary>The maw at his feet: the dark vortex and its crimson arms, renewed so they follow him.</summary>
        public void DrawMaw(Vector3 heroPosition, float reach)
        {
            Vector3 ground = Ground.Flat(heroPosition) + LIFT;
            effects.Emit(vortex, ground, 1, WHITE, reach * VORTEX_PER_REACH, VORTEX_SECONDS);
            effects.Emit(swirl, ground + LIFT, 1, BarbarianLooks.CRIMSON, reach * SWIRL_PER_REACH, SWIRL_SECONDS);
        }

        /// <summary>
        /// One drag tick: blood mist spirals in from the rim, dust rises under a few of the dragged and the reeling hooks
        /// scrape the ground.
        /// </summary>
        public void DrawDrag(Vector3 center, float reach, ReadOnlySpan<int> dragged, ReadOnlySpan<ChainState> chains)
        {
            Vector3 ground = Ground.Flat(center);
            for (int k = 0; k < VORTEX_MIST; k++)
            {
                Vector3 outward = Ground.RandomDirection();
                Vector3 spiral = (Ground.Perpendicular(outward) - outward) * (MIST_SPEED * SPIRAL_DIAGONAL);
                effects.EmitMoving(mist, ground + outward * (reach * MIST_RIM_SHARE) + Ground.Up(MIST_HEIGHT), spiral, 1, MIST_SIZE, MIST_SECONDS);
            }
            int stride = Math.Max(1, dragged.Length / VORTEX_DUST_ROWS);
            for (int i = 0; i < dragged.Length; i += stride)
            {
                Vector3 feet = Ground.Flat(enemies.GetPosition(dragged[i]));
                Vector3 inward = Ground.Direction(feet, ground, Vector3.forward);
                effects.EmitMoving(dragDust, feet + Ground.Up(DRAG_DUST_HEIGHT), inward * VORTEX_DUST_SPEED, 1, DRAG_DUST_SIZE, DRAG_DUST_SECONDS);
            }
            for (int i = 0; i < chains.Length; i++)
            {
                if (chains[i].phase != ChainPhase.Reel)
                {
                    continue;
                }
                Vector3 hook = ground + chains[i].direction * (HAND_REACH + chains[i].tip) + LIFT;
                effects.Emit(grit, hook, 1, BarbarianLooks.CRIMSON);
                effects.EmitSized(dust, hook, 1, WHITE, SCRAPE_DUST_SIZE);
            }
        }

        /// <summary>
        /// The slam: the chains whip down from his hands to the ground in a star, a crimson flash, blood and grit burst
        /// at chest height under a ring of crimson air (all over the heads of the pile), and on the ground a shockwave,
        /// a dust ring, rocks and a glowing crack.
        /// </summary>
        public void DrawSlam(Vector3 center, float radius, ReadOnlySpan<ChainState> chains)
        {
            Vector3 ground = Ground.Flat(center) + LIFT;
            slamBurst.Play(ground + Ground.Up(SLAM_HEIGHT), radius, fxScale, BarbarianLooks.CRIMSON);
            slamGround.Play(ground + Ground.Up(BITE_HEIGHT), radius, fxScale, BarbarianLooks.CRIMSON);
            Vector3 heading = Ground.RandomDirection() * FxSizes.HEADING_SPEED;
            effects.EmitMoving(crack, ground, heading, 1, WHITE, radius * SLAM_CRACK_SHARE, SLAM_CRACK_SECONDS);
            effects.EmitMoving(crackGlow, ground, heading, 1, BarbarianLooks.CRIMSON, radius * SLAM_CRACK_SHARE, SLAM_GLOW_SECONDS);
            effects.Emit(airRing, ground + Ground.Up(SLAM_RING_HEIGHT), 1, BarbarianLooks.CRIMSON, radius * SLAM_RING_PER_RADIUS, SLAM_RING_SECONDS);
            for (int i = 0; i < chains.Length; i++)
            {
                Vector3 direction = chains[i].direction;
                Vector3 b = ground + direction * (radius * SLAM_CHAIN_SHARE) + Ground.Up(SLAM_CHAIN_HEIGHT);
                DrawLinks(HandOf(center, direction), b, LINK_SPACING, false, 0.0f, SLAM_CHAIN_SECONDS);
                effects.EmitMoving(hookLook, b, -direction * FxSizes.HEADING_SPEED, 1, ironColor, HOOK_SIZE * fxScale, SLAM_CHAIN_SECONDS);
            }
        }
    }
}

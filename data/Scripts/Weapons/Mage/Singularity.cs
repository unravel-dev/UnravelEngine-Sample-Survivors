using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Singularity (Mage, Rare): a tear in the world opens on the densest crowd within Range (Count + Amount of them). For
    /// Duration seconds (Duration stat) it drags every enemy within Radius (Area) toward it (Pull) and grinds them (SubDamage
    /// every Interval); then it collapses and explodes: MajorDamage in MajorRadius (Area), hurling them out (Knockback) and
    /// stunning them (Stun). Bosses only feel the pull as they feel any slow. Echo casts glow violet.
    /// Evolution Event Horizon (+ Lodestone Charm): two tears, wider and harder, and what they drag in takes Bonus more damage
    /// for SubDuration seconds.
    /// Look: a black disc under a spinning violet vortex, a bright ring at its edge and motes streaming in; it shrinks to a point
    /// before the blast, a flash with a shock ring, rising stars and sparks over a violet stain.
    /// </summary>
    public sealed class Singularity : Weapon
    {
        private const int MAX_TEARS = 6;
        private const int SCRATCH = 1024;
        private const float GROUND_LIFT = 0.08f;
        /// <summary>The black heart hangs above the heads it drags in.</summary>
        private const float CORE_HEIGHT = 2.6f;
        private const float MOTE_HEIGHT = 1.2f;
        private const float REFRESH = 0.12f;
        /// <summary>The disc at its full size per metre of pull radius, and the vortex and ring around it.</summary>
        private const float CORE_SHARE = 0.42f;
        /// <summary>A soft black disc needs a few layers to read as a hole.</summary>
        private const int CORE_LAYERS = 4;
        private const float VORTEX_SHARE = 2.0f;
        private const float RING_SHARE = 3.4f;
        private const int MOTES_PER_REFRESH = 6;
        private const float MOTE_RING_SHARE = 0.9f;
        private const float MOTE_SECONDS = 0.9f;
        private const float MOTE_SIZE = 0.2f;
        private const float GROW_SECONDS = 0.25f;
        /// <summary>The last share of its life it shrinks toward the blast.</summary>
        private const float COLLAPSE_SHARE = 0.18f;
        private const float COLLAPSE_SIZE = 0.15f;
        private const float BLAST_TRAUMA = 0.32f;
        private const float BLAST_HEIGHT = 1.2f;
        private const float STAIN_SHARE = 2.2f;
        private const float STAIN_SECONDS = 2.5f;

        private static readonly Color VOID_VIOLET = new Color(0.62f, 0.32f, 1.0f, 1.0f);
        private static readonly Color VOID_PALE = new Color(0.85f, 0.7f, 1.0f, 1.0f);

        private struct Tear
        {
            public Vector3 center;
            public float age;
            public float life;
            public float tickTimer;
            public float refreshTimer;
            public float scale;
            public Color color;
        }

        private readonly Tear[] tears = new Tear[MAX_TEARS];
        private readonly int[] scratch = new int[SCRATCH];
        private readonly int core;
        private readonly int vortex;
        private readonly int ring;
        private readonly int motes;
        private readonly int stain;
        private readonly FxComposite blast;
        private int tearCount;

        public Singularity(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            core = effects.Find("MageVoidCore");
            vortex = effects.Find("MageVoidSwirl");
            ring = effects.Find("MageVoidRing");
            motes = effects.Find("MageVoidMote");
            stain = effects.Find("MageVoidStain");
            blast = Composite(new FxLayer("ArcaneFlash", 1, 0.8f), FxLayer.Ground("MageShockRing", 3.4f), new FxLayer("ArcaneStars", 24), new FxLayer("ArcaneMotes", 24),
                              new FxLayer("ArcaneTwirl", 1, 1.4f), new FxLayer("FireSparks", 18, 0.0f));
        }

        protected override bool Cast(in CastInfo cast)
        {
            float range = level[WeaponField.Range];
            int cells = FindClusters(range);
            if (cells == 0)
            {
                return false;
            }
            int count = level.GetCount(WeaponField.Count) + amount;
            for (int i = 0; i < count && tearCount < MAX_TEARS; i++)
            {
                Open(ctx.clusters.GetPoint(i % cells), cast);
            }
            RequestAttack("CastRaise");
            PlayAt("cataclysm_whistle", heroPosition);
            return true;
        }

        private void Open(Vector3 point, in CastInfo cast)
        {
            tears[tearCount++] = new Tear
            {
                center = Ground.Flat(point),
                life = DurationOf(level[WeaponField.Duration]),
                scale = cast.damageScale,
                color = Tint(cast, VOID_VIOLET),
            };
        }

        protected override void Update(float dt)
        {
            int i = 0;
            while (i < tearCount)
            {
                ref Tear tear = ref tears[i];
                tear.age += dt;
                if (tear.age >= tear.life)
                {
                    Collapse(tear);
                    tears[i] = tears[--tearCount];
                    continue;
                }
                Grind(ref tear, dt);
                i++;
            }
        }

        /// <summary>One frame of a tear: the pull, the grinding ticks and its look.</summary>
        private void Grind(ref Tear tear, float dt)
        {
            float radius = AreaOf(level[WeaponField.Radius]);
            enemies.Pull(tear.center, radius, level[WeaponField.Pull], scratch);
            tear.tickTimer -= dt;
            if (tear.tickTimer <= 0.0f)
            {
                tear.tickTimer += level[WeaponField.Interval];
                var effect = new HitEffect { markBonus = level[WeaponField.Bonus], markSeconds = level[WeaponField.SubDuration], hideNumber = true };
                strikes.Circle(tear.center, radius, MightOf(level[WeaponField.SubDamage]) * tear.scale, 0.0f, slot, effect, PushMode.None);
            }
            tear.refreshTimer -= dt;
            if (tear.refreshTimer <= 0.0f)
            {
                tear.refreshTimer += REFRESH;
                Show(tear, radius);
            }
        }

        /// <summary>The disc, vortex, ring and inward motes, grown in and shrinking toward the blast.</summary>
        private void Show(in Tear tear, float radius)
        {
            float grown = MathF.Min(tear.age / GROW_SECONDS, 1.0f);
            float collapse = MathF.Max(0.0f, tear.age / tear.life - (1.0f - COLLAPSE_SHARE)) / COLLAPSE_SHARE;
            float size = grown * Mathf.Lerp(1.0f, COLLAPSE_SIZE, collapse);
            float seconds = REFRESH * 2.0f;
            Vector3 ground = tear.center + Ground.Up(GROUND_LIFT);
            effects.Emit(vortex, ground, 1, tear.color, radius * VORTEX_SHARE * size, seconds);
            effects.Emit(ring, ground, 1, tear.color, radius * RING_SHARE * size, seconds);
            effects.Emit(core, tear.center + Ground.Up(CORE_HEIGHT), CORE_LAYERS, WHITE, radius * CORE_SHARE * size, seconds);
            float yaw = Ground.RandomYaw();
            for (int i = 0; i < MOTES_PER_REFRESH; i++)
            {
                Vector3 outward = Ground.RingDirection(yaw, i, MOTES_PER_REFRESH);
                Vector3 from = tear.center + outward * (radius * MOTE_RING_SHARE) + Ground.Up(MOTE_HEIGHT);
                effects.EmitMoving(motes, from, -outward * (radius / MOTE_SECONDS), 1, VOID_PALE, MOTE_SIZE, MOTE_SECONDS);
            }
        }

        /// <summary>The tear collapses: the blast hurls what it held, stunned, out over a stain.</summary>
        private void Collapse(in Tear tear)
        {
            float radius = AreaOf(level[WeaponField.MajorRadius]);
            var effect = new HitEffect { stunSeconds = level[WeaponField.Stun] };
            strikes.Circle(tear.center, radius, MightOf(level[WeaponField.MajorDamage]) * tear.scale, KnockbackOf(level[WeaponField.Knockback]), slot, effect);
            blast.Play(tear.center + Ground.Up(BLAST_HEIGHT), radius, fxScale, tear.color);
            EmitShape(stain, tear.center + Ground.Up(FxSizes.GROUND_LIFT), tear.color, radius * STAIN_SHARE, STAIN_SECONDS);
            PlayAt("cataclysm_impact", tear.center);
            ctx.feel.Shake(BLAST_TRAUMA);
        }
    }
}

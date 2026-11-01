using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Verdict (Knight, Rare): a giant spectral sword appears above the densest crowd within Range (a gold ring marks
    /// the spot), hovers, slams down Interval later (the telegraph), deals Damage in Radius (Area), stuns (Stun,
    /// Duration stat) and throws the crowd outward (Knockback), stays embedded SubDuration and dissolves. Extra swords
    /// (Count + Amount) strike the next densest crowds, Stagger apart.
    /// Evolution Final Verdict (+ Wide Sigil): more and heavier swords; each embedded sword releases a holy shockwave
    /// when it dissolves (SubDamage in SubRadius, Area).
    /// Look: a glint at the knight's raised hand; under each coming sword a gold ring and a turning holy seal, the sword
    /// appearing in a flash (its halo and motes are KnightLooks heads) and falling with wind lines. The slam: one flash,
    /// the holy cross flare, rays of light standing up and spreading, steel sparks, a shockwave, dust, rubble, the gold
    /// cracks and the seal burning under the sword, motes drifting up. The dissolving sword streams light. Final Verdict
    /// adds a column of celestial light from the sky that narrows onto the mark, a paler, grander slam, and its release
    /// shockwave: two rings, a flash and cross flare, light streaking outward, dust and motes.
    /// </summary>
    public sealed class Verdict : Weapon
    {
        private const int SUMMON = 0;
        private const int SLAM = 1;
        private const int IMPACT = 2;
        private const int RELEASE = 3;
        private const int MAX_SWORDS = 12;
        private const float HOVER_HEIGHT = 3.4f;
        private const float EMBED_HEIGHT = 1.1f;
        private const float HOVER_SHARE = 0.7f;
        /// <summary>The fall never takes less than this, however short the telegraph.</summary>
        private const float MIN_FALL_SECONDS = 0.02f;
        private const float SPARE_SPREAD = 1.6f;
        private const float SLAM_TRAUMA = 0.15f;
        private const float CRACK_SECONDS = 2.0f;
        private const float SHOCKWAVE_KNOCKBACK = 1.0f;
        private const float SHOCKWAVE_SECONDS = 0.4f;

        private const float RAISED_HAND_HEIGHT = 2.2f;
        private const float CAST_GLINT_SIZE = 1.3f;
        /// <summary>The ring and the seal under a coming sword outlive its telegraph by this much.</summary>
        private const float MARK_LINGER = 0.05f;
        private const float SUMMON_FLASH_SIZE = 1.2f;
        private const float SUMMON_GLINT_SIZE = 1.8f;
        private const float COLUMN_HEIGHT = 14.0f;
        /// <summary>The column outlives the telegraph by this much; its curve (KnightColumn) peaks at the slam.</summary>
        private const float COLUMN_LINGER = 0.2f;
        private const int SLAM_STREAKS = 6;
        private const float SLAM_STREAK_SPREAD = 0.45f;
        private const float SLAM_STREAK_DROP = 1.0f;
        private const float SLAM_STREAK_WIDTH = 0.8f;
        private const float SLAM_STREAK_EXTRA_SECONDS = 0.05f;
        private const float SLAM_GLINT_SIZE = 1.2f;
        private const float IMPACT_HEIGHT = 0.6f;
        private const float SWARM_HEIGHT = 0.3f;
        private const float FLASH_PER_RADIUS = 1.2f;
        private const float CROSS_PER_RADIUS = 0.9f;
        private const float CROSS_HEIGHT = 0.3f;
        private const float FINAL_SCALE = 1.2f;
        private const float RELEASE_GLINT_SIZE = 1.4f;
        private const float WAVE_FLASH_SIZE = 2.4f;
        private const float WAVE_CROSS_SIZE = 2.6f;
        private const float WAVE_INNER_RING_SHARE = 0.7f;
        private const float WAVE_INNER_RING_SECONDS = 0.32f;
        private const int WAVE_STREAKS = 16;
        private const float WAVE_STREAK_HEIGHT = 0.5f;
        private const float WAVE_STREAK_SPEED = 20.0f;
        private const float WAVE_STREAK_WIDTH = 0.9f;
        private const float WAVE_STREAK_SECONDS = 0.35f;

        /// <summary>The sword's gold and holy light: a touch deeper and warmer than KnightLooks.GOLD and HOLY.</summary>
        private static readonly Color SWORD_GOLD = new Color(1.0f, 0.82f, 0.38f, 1.0f);
        private static readonly Color WARM_HOLY = new Color(1.0f, 0.95f, 0.75f, 1.0f);
        /// <summary>Final Verdict's accent: celestial light from the sky.</summary>
        private static readonly Color COLUMN_AZURE = new Color(0.72f, 0.86f, 1.0f, 1.0f);
        private static readonly Color CELESTIAL = new Color(0.86f, 0.93f, 1.0f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.LOW_GROUND_LIFT);
        /// <summary>A static sword still needs a velocity: it orients the mesh, blade down.</summary>
        private static readonly Vector3 POINT_DOWN = new Vector3(0.0f, -0.01f, 0.0f);

        private readonly Vector3[] targets = new Vector3[MAX_SWORDS];
        private readonly int swordVisual;
        private readonly int telegraphRing;
        private readonly int crackDecal;
        private readonly int sigil;
        private readonly int sigilGlow;
        private readonly int column;
        private readonly int columnCore;
        private readonly int flash;
        private readonly int glint;
        private readonly int crossFlare;
        private readonly int shockRing;
        private readonly int windStreak;
        private readonly FxComposite slamBurst;
        private readonly FxComposite finalBurst;
        private readonly FxComposite bladeLight;
        private readonly FxComposite waveBurst;

        public Verdict(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            swordVisual = context.FindVisual("VerdictSword");
            telegraphRing = effects.Find("TelegraphRing");
            crackDecal = effects.Find("VerdictCrack");
            sigil = effects.Find("KnightSigil");
            sigilGlow = effects.Find("KnightSigilGlow");
            column = effects.Find("KnightColumn");
            columnCore = effects.Find("KnightColumnCore");
            flash = effects.Find("KnightFlash");
            glint = effects.Find("KnightGlint");
            crossFlare = effects.Find("KnightCrossFlare");
            shockRing = effects.Find("KnightShockRing");
            windStreak = effects.Find("KnightSweepStreak");
            slamBurst = Composite(new FxLayer("KnightRays", 8, 1.0f, true, true), new FxLayer("KnightSteelSparks", 18),
                                  new FxLayer("KnightDust", 5, 0.45f, false, true), new FxLayer("MeteorRocks", 8, 0.0f, false),
                                  new FxLayer("KnightMotes", 12));
            finalBurst = Composite(new FxLayer("KnightRays", 12, 1.1f, true, true), new FxLayer("KnightSteelSparks", 26),
                                   new FxLayer("KnightDust", 8, 0.45f, false, true), new FxLayer("MeteorRocks", 14, 0.0f, false),
                                   new FxLayer("KnightMotes", 20));
            bladeLight = Composite(new FxLayer("KnightBladeMotes", 14));
            waveBurst = Composite(new FxLayer("KnightDust", 8, 0.35f, false, true), new FxLayer("KnightMotes", 14),
                                  new FxLayer("KnightSteelSparks", 12));
        }

        protected override bool Cast(in CastInfo cast)
        {
            int cells = FindClusters(level[WeaponField.Range]);
            if (cells == 0)
            {
                return false;
            }
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_SWORDS);
            float radius = AreaOf(level[WeaponField.Radius]);
            int picked = PickTargets(cells, count, radius);
            for (int i = 0; i < picked; i++)
            {
                Schedule(level[WeaponField.Stagger] * i, new WeaponEvent { kind = SUMMON, position = targets[i], radius = radius, cast = cast });
            }
            RequestAttack("CastRaise");
            PlayAt("verdict_summon", heroPosition);
            EmitSized(glint, heroPosition + Ground.Up(RAISED_HAND_HEIGHT), WARM_HOLY, CAST_GLINT_SIZE * fxScale);
            return true;
        }

        /// <summary>The densest crowds, at least a blast radius apart; spare swords land around the densest one.</summary>
        private int PickTargets(int cells, int count, float radius)
        {
            int picked = 0;
            for (int cell = 0; cell < cells && picked < count; cell++)
            {
                Vector3 point = ctx.clusters.GetPoint(cell);
                if (IsFarFromPicked(point, picked, radius))
                {
                    targets[picked++] = point;
                }
            }
            Vector3 densest = targets[0];
            while (picked < count)
            {
                targets[picked++] = densest + Ground.RandomInCircle(radius * SPARE_SPREAD);
            }
            return picked;
        }

        private bool IsFarFromPicked(Vector3 point, int picked, float radius)
        {
            for (int i = 0; i < picked; i++)
            {
                if (Ground.DistanceSquared(point, targets[i]) < radius * radius)
                {
                    return false;
                }
            }
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            switch (e.kind)
            {
                case SUMMON:
                    Summon(e);
                    break;
                case SLAM:
                    Slam(e);
                    break;
                case IMPACT:
                    Impact(e);
                    break;
                case RELEASE:
                    Release(e);
                    break;
            }
        }

        private void Summon(in WeaponEvent e)
        {
            float telegraph = level[WeaponField.Interval];
            EmitShape(telegraphRing, e.position + LIFT, SWORD_GOLD, e.radius * 2.0f, telegraph + MARK_LINGER);
            PlaySummon(e.position, e.radius, telegraph);
            float hover = telegraph * HOVER_SHARE;
            SpawnVisual(swordVisual, ProjectileMotion.Static, e.position + Ground.Up(HOVER_HEIGHT), POINT_DOWN, hover);
            WeaponEvent slam = e;
            slam.kind = SLAM;
            Schedule(hover, slam);
        }

        /// <summary>The holy seal under the mark and the sword's arrival; Final Verdict's column of light from the sky.</summary>
        private void PlaySummon(Vector3 position, float radius, float telegraph)
        {
            Vector3 ground = position + LIFT;
            Vector3 hilt = position + Ground.Up(HOVER_HEIGHT);
            EmitShape(sigil, ground, isEvolved ? WARM_HOLY : SWORD_GOLD, radius * FxSizes.SIGIL_SIZE_PER_RADIUS, telegraph + MARK_LINGER);
            EmitSized(flash, hilt, WARM_HOLY, SUMMON_FLASH_SIZE);
            EmitSized(glint, hilt, WARM_HOLY, SUMMON_GLINT_SIZE * fxScale);
            if (isEvolved)
            {
                EmitShape(column, ground, COLUMN_AZURE, COLUMN_HEIGHT, telegraph + COLUMN_LINGER);
                EmitShape(columnCore, ground, CELESTIAL, COLUMN_HEIGHT, telegraph + COLUMN_LINGER);
            }
        }

        private void Slam(in WeaponEvent e)
        {
            float fall = MathF.Max(level[WeaponField.Interval] * (1.0f - HOVER_SHARE), MIN_FALL_SECONDS);
            var start = e.position + Ground.Up(HOVER_HEIGHT);
            var velocity = Ground.Up(-(HOVER_HEIGHT - EMBED_HEIGHT) / fall);
            SpawnVisual(swordVisual, ProjectileMotion.Linear, start, velocity, fall);
            PlayFall(start, velocity, fall);
            WeaponEvent impact = e;
            impact.kind = IMPACT;
            Schedule(fall, impact);
        }

        /// <summary>Wind lines falling with the sword around its blade, and a glint as it drops.</summary>
        private void PlayFall(Vector3 hilt, Vector3 velocity, float fall)
        {
            EmitSized(glint, hilt, WARM_HOLY, SLAM_GLINT_SIZE * fxScale);
            Vector3 blade = hilt - Ground.Up(SLAM_STREAK_DROP);
            for (int k = 0; k < SLAM_STREAKS; k++)
            {
                Vector3 side = Ground.RingDirection(0.0f, k, SLAM_STREAKS) * SLAM_STREAK_SPREAD;
                effects.EmitMoving(windStreak, blade + side, velocity, 1, WARM_HOLY, SLAM_STREAK_WIDTH, fall + SLAM_STREAK_EXTRA_SECONDS);
            }
        }

        private void Impact(in WeaponEvent e)
        {
            var effect = new HitEffect { stunSeconds = DurationOf(level[WeaponField.Stun]) };
            strikes.Circle(e.position, e.radius, DamageOf(WeaponField.Damage, e.cast), KnockbackOf(level[WeaponField.Knockback]), slot, effect);
            float embedded = level[WeaponField.SubDuration];
            SpawnVisual(swordVisual, ProjectileMotion.Static, e.position + Ground.Up(EMBED_HEIGHT), POINT_DOWN, embedded);
            PlayImpact(e.position, e.radius, embedded);
            PlayAt("verdict_slam", e.position);
            ctx.feel.Shake(SLAM_TRAUMA);
            WeaponEvent release = e;
            release.kind = RELEASE;
            Schedule(embedded, release);
        }

        /// <summary>The slam: flash, cross flare, shockwave, cracks and the burning seal, then the rays, sparks and debris.</summary>
        private void PlayImpact(Vector3 position, float radius, float embedded)
        {
            float grandeur = isEvolved ? FINAL_SCALE : 1.0f;
            Vector3 ground = position + LIFT;
            Vector3 hilt = position + Ground.Up(EMBED_HEIGHT + CROSS_HEIGHT);
            EmitSized(flash, position + Ground.Up(IMPACT_HEIGHT), WARM_HOLY, radius * FLASH_PER_RADIUS);
            EmitSized(crossFlare, hilt, isEvolved ? CELESTIAL : WARM_HOLY, radius * CROSS_PER_RADIUS * grandeur);
            EmitSized(shockRing, ground, isEvolved ? WARM_HOLY : SWORD_GOLD, radius * FxSizes.RING_SIZE_PER_RADIUS);
            EmitShape(crackDecal, ground, SWORD_GOLD, radius * 2.0f, CRACK_SECONDS);
            EmitShape(sigilGlow, ground, isEvolved ? WARM_HOLY : SWORD_GOLD, radius * FxSizes.SIGIL_SIZE_PER_RADIUS, embedded);
            Vector3 swarm = position + Ground.Up(SWARM_HEIGHT);
            if (isEvolved)
            {
                finalBurst.Play(swarm, radius, fxScale, CELESTIAL);
            }
            else
            {
                slamBurst.Play(swarm, radius, fxScale, SWORD_GOLD);
            }
        }

        /// <summary>The embedded sword dissolves; Final Verdict releases its holy shockwave.</summary>
        private void Release(in WeaponEvent e)
        {
            Vector3 blade = e.position + Ground.Up(EMBED_HEIGHT);
            bladeLight.Play(blade, FxSizes.METRE_UNIT, fxScale, WARM_HOLY);
            EmitSized(glint, blade, WARM_HOLY, RELEASE_GLINT_SIZE * fxScale);
            if (!isEvolved)
            {
                return;
            }
            float radius = AreaOf(level[WeaponField.SubRadius]);
            strikes.Circle(e.position, radius, DamageOf(WeaponField.SubDamage, e.cast), KnockbackOf(SHOCKWAVE_KNOCKBACK), slot);
            PlayHolyWave(e.position, radius);
            PlayAt("verdict_shockwave", e.position);
        }

        /// <summary>Final Verdict's release: two rings, a flash and the cross flare, light streaking outward, dust and motes.</summary>
        private void PlayHolyWave(Vector3 position, float radius)
        {
            Vector3 ground = position + LIFT;
            Vector3 blade = position + Ground.Up(EMBED_HEIGHT);
            EmitShape(shockRing, ground, WARM_HOLY, radius * FxSizes.RING_SIZE_PER_RADIUS, SHOCKWAVE_SECONDS);
            EmitShape(shockRing, ground, COLUMN_AZURE, radius * FxSizes.RING_SIZE_PER_RADIUS * WAVE_INNER_RING_SHARE, WAVE_INNER_RING_SECONDS);
            EmitSized(flash, blade, WARM_HOLY, WAVE_FLASH_SIZE);
            EmitSized(crossFlare, blade, CELESTIAL, WAVE_CROSS_SIZE);
            Vector3 streaks = position + Ground.Up(WAVE_STREAK_HEIGHT);
            for (int k = 0; k < WAVE_STREAKS; k++)
            {
                Vector3 outward = Ground.RingDirection(0.0f, k, WAVE_STREAKS);
                effects.EmitMoving(windStreak, streaks, outward * WAVE_STREAK_SPEED, 1, CELESTIAL, WAVE_STREAK_WIDTH, WAVE_STREAK_SECONDS);
            }
            waveBurst.Play(position + Ground.Up(SWARM_HEIGHT), radius, fxScale, WARM_HOLY);
        }
    }
}

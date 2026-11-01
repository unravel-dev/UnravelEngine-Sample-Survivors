using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Starfall (Mage, Uncommon): meteors (Count + Amount, Stagger apart) fall on crowds picked at random among the 8
    /// densest cells within Range, weighted by size. Each falls at a 60 deg angle from a random side for Interval while
    /// a shadow and a ring grow on the ground, then deals Damage in Radius (Area), knocks the crowd outward (Knockback)
    /// and leaves a burning crater for SubDuration (Duration stat): SubDamage every SubInterval.
    /// Evolution Cataclysm (+ Wide Sigil): a great meteor on the densest crowd (MajorDamage in MajorRadius, falling
    /// for MajorDelay), ringed by the meteors.
    /// Look: on each mark a shadow, a ring and a soft fill grow on the ground while the meteor (MageLooks: a white-hot
    /// core in a huge flame) falls trailing fire; the impact flashes, raises a pillar of fire, fireballs, a ground
    /// shock, dust, rocks, sparks, embers and smoke over a scorch, and its crater burns with licking flames. The great
    /// meteor is grander in every way, with solar rays and a solar halo. Echo casts glow violet.
    /// </summary>
    public sealed class Starfall : Weapon
    {
        private const int METEOR = 0;
        private const int IMPACT = 1;
        private const int NORMAL = 0;
        private const int GREAT = 1;
        private const int MAX_METEORS = 12;
        private const float FALL_HEIGHT = 14.0f;
        /// <summary>Horizontal start offset for a 60 deg fall: height / tan(60 deg).</summary>
        private const float FALL_OFFSET = FALL_HEIGHT * 0.57735f;
        private const float AIM_JITTER = 0.8f;
        private const float RING_SHARE = 0.75f;
        private const float CRATER_SHARE = 0.8f;
        /// <summary>Crater flames: one every interval, three at a time in the great meteor's crater.</summary>
        private const float CRATER_PARTICLE_INTERVAL = 0.15f;
        private const int CRATER_FLAMES = 1;
        private const int GREAT_CRATER_FLAMES = 3;
        private const float SCORCH_SECONDS = 4.0f;
        private const float IMPACT_HEIGHT = 0.6f;
        private const float IMPACT_TRAUMA = 0.20f;
        private const float GREAT_TRAUMA = 0.6f;

        private static readonly Color STAR = new Color(0.72f, 0.82f, 1.0f, 1.0f);
        private static readonly Color SHADOW = new Color(0.1f, 0.06f, 0.14f, 1.0f);
        private static readonly Color SCORCH = new Color(0.18f, 0.12f, 0.1f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.LOW_GROUND_LIFT);

        private readonly int meteorVisual;
        private readonly int greatVisual;
        private readonly int shadow;
        private readonly int telegraphRing;
        private readonly int telegraphFill;
        private readonly int scorch;
        private readonly FxComposite meteorImpact;
        private readonly FxComposite greatImpact;
        /// <summary>The craters' flames: a meteor's and the great meteor's, each in fire and in the echo's violet.</summary>
        private readonly ZoneLook crater;
        private readonly ZoneLook greatCrater;
        private readonly ZoneLook echoCrater;
        private readonly ZoneLook greatEchoCrater;

        public Starfall(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            meteorVisual = context.FindVisual("Meteor");
            greatVisual = context.FindVisual("GreatMeteor");
            shadow = effects.Find("StarfallShadow");
            telegraphRing = effects.Find("TelegraphRing");
            telegraphFill = effects.Find("TelegraphFill");
            scorch = effects.Find("ScorchDecal");
            meteorImpact = Composite(new FxLayer("FireFlash", 1, 1.2f), new FxLayer("MeteorPillar", 1, 1.6f), new FxLayer("FireBall", 4, 0.6f),
                                     FxLayer.Ground("FireShock", 2.4f), new FxLayer("MeteorDust", 5, 0.55f, false), new FxLayer("MeteorRocks", 10, 0.0f, false),
                                     new FxLayer("FireSparks", 18), new FxLayer("FireEmbers", 10), new FxLayer("FireSmoke", 3, 0.8f, false));
            greatImpact = Composite(new FxLayer("FireFlash", 1, 1.4f), new FxLayer("SolarRays", 1, 2.0f), new FxLayer("MeteorPillar", 1, 2.0f),
                                    new FxLayer("FireBall", 6, 0.5f), FxLayer.Ground("SolarHalo", 2.0f), FxLayer.Ground("FireShock", 2.4f),
                                    new FxLayer("MeteorDust", 10, 0.5f, false), new FxLayer("MeteorRocks", 26, 0.0f, false),
                                    new FxLayer("FireSparks", 36), new FxLayer("FireEmbers", 24), new FxLayer("FireSmoke", 6, 0.75f, false));
            int craterFire = effects.Find("CraterFire");
            crater = new ZoneLook(new ZoneStream(craterFire, CRATER_PARTICLE_INTERVAL, CRATER_FLAMES, MageLooks.FLAME));
            greatCrater = new ZoneLook(new ZoneStream(craterFire, CRATER_PARTICLE_INTERVAL, GREAT_CRATER_FLAMES, MageLooks.FLAME));
            echoCrater = new ZoneLook(new ZoneStream(craterFire, CRATER_PARTICLE_INTERVAL, CRATER_FLAMES, ECHO_TINT));
            greatEchoCrater = new ZoneLook(new ZoneStream(craterFire, CRATER_PARTICLE_INTERVAL, GREAT_CRATER_FLAMES, ECHO_TINT));
        }

        protected override bool Cast(in CastInfo cast)
        {
            int cells = FindClusters(level[WeaponField.Range]);
            if (cells == 0)
            {
                return false;
            }
            RequestAttack("CastRaise");
            if (isEvolved)
            {
                CastCataclysm(cast);
                return true;
            }
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_METEORS);
            float radius = AreaOf(level[WeaponField.Radius]);
            for (int i = 0; i < count; i++)
            {
                Vector3 target = ctx.clusters.GetPoint(ctx.clusters.PickWeighted()) + Ground.RandomInCircle(AIM_JITTER);
                Schedule(level[WeaponField.Stagger] * i, MeteorEvent(target, radius, level[WeaponField.Damage], false, cast));
            }
            return true;
        }

        private void CastCataclysm(in CastInfo cast)
        {
            Vector3 center = ctx.clusters.GetPoint(0);
            float greatRadius = AreaOf(level[WeaponField.MajorRadius]);
            Schedule(0.0f, MeteorEvent(center, greatRadius, level[WeaponField.MajorDamage], true, cast));
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_METEORS);
            float ring = greatRadius * RING_SHARE;
            float start = Ground.RandomYaw();
            for (int k = 0; k < count; k++)
            {
                Vector3 target = center + Ground.RingDirection(start, k, count) * ring;
                Schedule(level[WeaponField.Stagger] * (k + 1), MeteorEvent(target, AreaOf(level[WeaponField.Radius]), level[WeaponField.Damage], false, cast));
            }
        }

        /// <summary>A meteor (or the great one) on <paramref name="target"/>: its blast radius and base damage (before Might).</summary>
        private static WeaponEvent MeteorEvent(Vector3 target, float radius, float baseDamage, bool isGreat, in CastInfo cast)
        {
            return new WeaponEvent { kind = METEOR, position = target, radius = radius, value = baseDamage, index = isGreat ? GREAT : NORMAL, cast = cast };
        }

        private static bool IsGreat(in WeaponEvent e) => e.index == GREAT;

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == METEOR)
            {
                Launch(e);
            }
            else if (e.kind == IMPACT)
            {
                Impact(e);
            }
        }

        /// <summary>The meteor appears high up to one side and falls on its mark while the ground shows where.</summary>
        private void Launch(in WeaponEvent e)
        {
            bool isGreat = IsGreat(e);
            float fall = isGreat ? level[WeaponField.MajorDelay] : level[WeaponField.Interval];
            Vector3 ground = e.position + LIFT;
            Color color = Tint(e.cast, STAR);
            EmitShape(shadow, ground, SHADOW, e.radius * 2.0f, fall);
            EmitShape(telegraphRing, ground, color, e.radius * 2.0f, fall);
            EmitShape(telegraphFill, ground, color, e.radius * 2.0f, fall);
            Vector3 side = Ground.RandomDirection();
            Vector3 start = e.position + Ground.Up(FALL_HEIGHT) - side * FALL_OFFSET;
            Vector3 velocity = (e.position - start) * (1.0f / fall);
            SpawnVisual(isGreat ? greatVisual : meteorVisual, ProjectileMotion.Linear, start, velocity, fall);
            PlayAt(isGreat ? "cataclysm_whistle" : "starfall_whistle", e.position);
            WeaponEvent impact = e;
            impact.kind = IMPACT;
            Schedule(fall, impact);
        }

        private void Impact(in WeaponEvent e)
        {
            bool isGreat = IsGreat(e);
            var effect = new HitEffect { numberKind = NumberKind(e.cast) };
            strikes.Circle(e.position, e.radius, MightOf(e.value, e.cast), KnockbackOf(level[WeaponField.Knockback]), slot, effect);
            (isGreat ? greatImpact : meteorImpact).Play(e.position + Ground.Up(IMPACT_HEIGHT), e.radius, fxScale, Tint(e.cast, MageLooks.FLAME));
            EmitShape(scorch, e.position + LIFT, SCORCH, e.radius * 2.0f, SCORCH_SECONDS);
            PlayAt(isGreat ? "cataclysm_impact" : "starfall_impact", e.position);
            ctx.feel.Shake(isGreat ? GREAT_TRAUMA : IMPACT_TRAUMA);
            LeaveCrater(e, isGreat);
        }

        /// <summary>The impact's burning crater: a lingering zone that burns what stands in it.</summary>
        private void LeaveCrater(in WeaponEvent e, bool isGreat)
        {
            ZoneLook look = e.cast.isEcho ? isGreat ? greatEchoCrater : echoCrater : isGreat ? greatCrater : crater;
            ctx.zones.Add(new ZoneSpec
            {
                shape = ZoneShape.Circle,
                a = e.position,
                b = e.position,
                radius = e.radius * CRATER_SHARE,
                damage = DamageOf(WeaponField.SubDamage, e.cast),
                interval = level[WeaponField.SubInterval],
                duration = DurationOf(level[WeaponField.SubDuration]),
                source = slot,
                effect = new HitEffect { numberKind = NumberKind(e.cast, DamageKind.Burn) },
                look = look,
            });
        }
    }
}

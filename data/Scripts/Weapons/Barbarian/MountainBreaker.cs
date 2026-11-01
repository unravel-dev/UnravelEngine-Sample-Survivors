using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Mountain Breaker (Barbarian, Rare): the barbarian rips a boulder out of the ground and hurls it in a high arc (SubInterval
    /// seconds, Count + Amount of them Stagger apart) at the densest crowd within Range. It lands for Damage in Radius (Area),
    /// throws the crowd out (Knockback), stuns (Stun) and bursts into SubCount shards flying out SubRange metres, SubDamage
    /// each. Every enemy hit by the landing feeds Fury (Fury).
    /// Evolution Avalanche (+ Wide Sigil): three boulders, and each landing leaves a crater of lava (MajorDamage per half
    /// second for SubDuration) that burns what stands in it.
    /// Look: dust and rocks burst where the boulder is torn out; a ring on the ground shows where it will land; the landing
    /// is a flash, a shock ring, a rolling dust ring, boulders, rocks, clods, sparks and a radial crack. Avalanche cracks
    /// glow with lava, spitting embers.
    /// </summary>
    public sealed class MountainBreaker : Weapon
    {
        private const int THROW = 0;
        private const float GRAVITY = 25.0f;
        private const float LAUNCH_HEIGHT = 1.8f;
        /// <summary>A boulder that somehow never lands (it always does) is gone after its flight times this.</summary>
        private const float SAFETY_LIFETIME_SHARE = 3.0f;
        private const float LAND_TRAUMA = 0.28f;
        private const float SHARD_HEIGHT = 0.8f;
        private const float SHARD_RADIUS = 0.45f;
        private const float BURST_HEIGHT = 0.5f;
        private const float CRACK_SHARE = 1.6f;
        private const float CRACK_SECONDS = 2.5f;
        private const float LAVA_INTERVAL = 0.5f;
        private const float LAVA_FLAME_INTERVAL = 0.09f;
        private const int LAVA_FLAMES = 2;

        private readonly BarbarianKit barbarian;
        private readonly int boulderVisual;
        private readonly int shardVisual;
        private readonly int ring;
        private readonly int crack;
        private readonly int crackGlow;
        private readonly FxComposite tearOut;
        private readonly FxComposite landing;
        private readonly ZoneLook lavaLook;

        public MountainBreaker(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            barbarian = context.KitAs<BarbarianKit>();
            boulderVisual = context.FindVisual("Boulder");
            shardVisual = context.FindVisual("BoulderShard");
            ring = effects.Find("CorePulseRing");
            crack = effects.Find("BarbarianCrack");
            crackGlow = effects.Find("BarbarianCrackGlow");
            // Size unit = 1 m.
            tearOut = Composite(FxLayer.Ground("BarbarianDustRing", 3.0f, false), new FxLayer("BarbarianDust", 4, 0.7f, false),
                                new FxLayer("BarbarianRocks", 6, 0.0f, false), new FxLayer("BarbarianDirtSpray", 5, 0.0f, false));
            // Size unit = landing radius.
            landing = Composite(new FxLayer("BarbarianFlash", 1, 0.7f), FxLayer.Ground("BarbarianShockRing", 2.2f, false), FxLayer.Ground("BarbarianDustRing", 2.4f, false),
                                new FxLayer("BarbarianDust", 8, 0.25f, false), new FxLayer("BarbarianBoulders", 5, 0.0f, false), new FxLayer("BarbarianRocks", 12, 0.0f, false),
                                new FxLayer("BarbarianDirtSpray", 8, 0.0f, false), new FxLayer("BarbarianGrit", 16));
            lavaLook = new ZoneLook(new ZoneStream(effects.Find("BarbarianLavaSpurt"), LAVA_FLAME_INTERVAL, LAVA_FLAMES, BarbarianLooks.CRIMSON),
                                    new ZoneStream(effects.Find("BarbarianLavaEmbers"), LAVA_FLAME_INTERVAL, LAVA_FLAMES, WHITE));
        }

        private float flightSeconds => level[WeaponField.SubInterval];

        protected override bool Cast(in CastInfo cast)
        {
            float range = level[WeaponField.Range];
            if (FindClusters(range) == 0)
            {
                return false;
            }
            int boulders = level.GetCount(WeaponField.Count) + amount;
            for (int i = 0; i < boulders; i++)
            {
                Schedule(level[WeaponField.Stagger] * i, new WeaponEvent { kind = THROW, index = i });
            }
            RequestAttack("HeavyChop");
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == THROW)
            {
                Hurl(e.index);
            }
        }

        /// <summary>A boulder at the i-th densest crowd: it lands there after the flight time.</summary>
        private void Hurl(int index)
        {
            int cells = FindClusters(level[WeaponField.Range]);
            Vector3 target = cells > 0 ? ctx.clusters.GetPoint(index % cells) : heroPosition + facing * level[WeaponField.Range];
            float flight = flightSeconds;
            Vector3 start = heroPosition + Ground.Up(LAUNCH_HEIGHT);
            Vector3 across = Ground.Flat(target - start) * (1.0f / flight);
            float rise = (0.5f * GRAVITY * flight * flight - start.y) / flight;
            ProjectileSpawn spawn = NewProjectile(boulderVisual, ProjectileMotion.Arc, start, across + Ground.Up(rise), flight * SAFETY_LIFETIME_SHARE);
            spawn.gravity = GRAVITY;
            spawn.handler = this;
            Spawn(spawn);
            tearOut.Play(heroPosition, FxSizes.METRE_UNIT, fxScale, WHITE);
            float radius = AreaOf(level[WeaponField.Radius]);
            effects.Emit(ring, Ground.Flat(target) + Ground.Up(FxSizes.GROUND_LIFT), 1, BarbarianLooks.DUST_GOLD, radius * FxSizes.RING_SIZE_PER_RADIUS, flight);
            PlayAt("fault_stomp", heroPosition);
        }

        /// <summary>The boulder lands: the blow, the shards, the crack (and Avalanche's lava).</summary>
        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            Vector3 ground = Ground.Flat(position);
            float radius = AreaOf(level[WeaponField.Radius]);
            var effect = new HitEffect { stunSeconds = level[WeaponField.Stun] };
            int hits = strikes.Circle(ground, radius, MightOf(level[WeaponField.Damage]), KnockbackOf(level[WeaponField.Knockback]), slot, effect);
            barbarian?.AddFury(hits * level[WeaponField.Fury]);
            ThrowShards(ground);
            ShowLanding(ground, radius);
            if (isEvolved)
            {
                LeaveLava(ground, radius);
            }
            PlayAt("starfall_impact", ground);
            ctx.feel.Shake(LAND_TRAUMA);
        }

        private void ThrowShards(Vector3 ground)
        {
            int shards = level.GetCount(WeaponField.SubCount);
            float speed = SpeedOf(level[WeaponField.SubSpeed]);
            float start = Ground.RandomYaw();
            for (int i = 0; i < shards; i++)
            {
                Vector3 direction = Ground.RingDirection(start, i, shards);
                ProjectileSpawn spawn = NewProjectile(shardVisual, ProjectileMotion.Linear, ground + Ground.Up(SHARD_HEIGHT), direction * speed, level[WeaponField.SubRange] / speed);
                spawn.damage = MightOf(level[WeaponField.SubDamage]);
                spawn.radius = SHARD_RADIUS;
                spawn.pierce = 1;
                spawn.knockback = KnockbackOf(1.0f);
                Spawn(spawn);
            }
        }

        private void ShowLanding(Vector3 ground, float radius)
        {
            landing.Play(ground + Ground.Up(BURST_HEIGHT), radius, fxScale, BarbarianLooks.STEEL);
            Vector3 decal = ground + Ground.Up(FxSizes.GROUND_LIFT);
            float size = radius * CRACK_SHARE;
            effects.EmitMoving(crack, decal, Ground.RandomDirection() * FxSizes.HEADING_SPEED, 1, WHITE, size, CRACK_SECONDS);
            if (isEvolved)
            {
                effects.EmitMoving(crackGlow, decal, Ground.RandomDirection() * FxSizes.HEADING_SPEED, 1, BarbarianLooks.CRIMSON, size, level[WeaponField.SubDuration]);
            }
        }

        private void LeaveLava(Vector3 ground, float radius)
        {
            ctx.zones.Add(new ZoneSpec
            {
                shape = ZoneShape.Circle,
                a = ground,
                b = ground,
                radius = radius,
                damage = MightOf(level[WeaponField.MajorDamage]),
                interval = LAVA_INTERVAL,
                duration = DurationOf(level[WeaponField.SubDuration]),
                source = slot,
                effect = new HitEffect { numberKind = DamageKind.Burn },
                look = lavaLook,
            });
        }
    }
}

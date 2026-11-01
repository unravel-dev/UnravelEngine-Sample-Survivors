using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Lance of Dawn (Knight, Rare): a spear of light (Count + Amount, Stagger apart) flies at the densest crowd within Range
    /// (Speed, Projectile Speed) through everything on its way (Damage, a line Width wide) and bursts there into a cross of
    /// beams: SubCount arms Radius long (Area), SubRadius wide, SubDamage each, stunning (Stun) and hurling the dead aside.
    /// Evolution Solar Cross (+ Tome of Insight): eight arms, a sun where the lance lands.
    /// Look: a huge glowing sword trailing gold motes and streaks; the burst is a flash, a cross flare, a shock ring and gold
    /// beams racing out along the arms, with rays and motes rising from them. Solar Cross adds a burning halo and rays.
    /// </summary>
    public sealed class LanceOfDawn : Weapon
    {
        private const int THROW = 0;
        private const float LAUNCH_HEIGHT = 1.4f;
        private const float MIN_FLIGHT_METRES = 4.0f;
        private const float BEAM_SECONDS = 0.45f;
        /// <summary>The beam's line runs from the quad's center to its edge.</summary>
        private const float BEAM_SIZE_PER_LENGTH = 2.0f;
        private const float BEAM_HEIGHT = 0.5f;
        private const float BURST_HEIGHT = 1.0f;
        private const float BURST_TRAUMA = 0.22f;
        private const int ARM_MOTES = 6;

        private readonly HashSet<Enemy> crossMemory = new HashSet<Enemy>();
        private readonly int lanceVisual;
        private readonly int beam;
        private readonly int motes;
        private readonly FxComposite burst;
        private readonly FxComposite solarBurst;
        private readonly FxComposite hit;

        public LanceOfDawn(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            lanceVisual = context.FindVisual("DawnLance");
            beam = effects.Find("KnightCrossBeam");
            motes = effects.Find("KnightMotes");
            burst = Composite(new FxLayer("KnightFlash", 1, 0.5f), new FxLayer("KnightCrossFlare", 1, 0.9f), FxLayer.Ground("KnightShockRing", 1.2f),
                              new FxLayer("KnightMotes", 18), new FxLayer("KnightSteelSparks", 14));
            solarBurst = Composite(new FxLayer("KnightFlash", 1, 0.8f), new FxLayer("KnightCrossFlare", 1, 1.2f), new FxLayer("SolarRays", 1, 1.0f),
                                   new FxLayer("SolarHalo", 1, 1.0f), FxLayer.Ground("KnightShockRing", 1.5f), new FxLayer("KnightMotes", 30));
            hit = Composite(new FxLayer("KnightSteelSparks", 3));
        }

        protected override bool Cast(in CastInfo cast)
        {
            float range = level[WeaponField.Range];
            if (FindClusters(range) == 0)
            {
                return false;
            }
            int lances = level.GetCount(WeaponField.Count) + amount;
            for (int i = 0; i < lances; i++)
            {
                Schedule(level[WeaponField.Stagger] * i, new WeaponEvent { kind = THROW, index = i });
            }
            RequestAttack("Throw");
            PlayAt("verdict_summon", heroPosition);
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == THROW)
            {
                Throw(e.index);
            }
        }

        /// <summary>A lance at the i-th densest crowd (the densest again when there are fewer crowds than lances).</summary>
        private void Throw(int index)
        {
            int cells = FindClusters(level[WeaponField.Range]);
            Vector3 target = cells > 0 ? ctx.clusters.GetPoint(index % cells) : heroPosition + facing * level[WeaponField.Range];
            Vector3 direction = Ground.Direction(heroPosition, target, facing);
            float distance = MathF.Max(Ground.Distance(heroPosition, target), MIN_FLIGHT_METRES);
            float speed = SpeedOf(level[WeaponField.Speed]);
            ProjectileSpawn spawn = NewProjectile(lanceVisual, ProjectileMotion.Linear, heroPosition + Ground.Up(LAUNCH_HEIGHT), direction * speed, distance / speed);
            spawn.damage = MightOf(level[WeaponField.Damage]);
            spawn.radius = AreaOf(level[WeaponField.Width]) * 0.5f;
            spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
            spawn.handler = this;
            Spawn(spawn);
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn lance = ref world.Get(projectile);
            hit.Play(lance.position, FxSizes.METRE_UNIT, fxScale, KnightLooks.GOLD);
            HitAlongFlight(enemyRow, lance);
        }

        /// <summary>The lance landed: the cross bursts around it, turned along its flight.</summary>
        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            ref ProjectileSpawn lance = ref world.Get(projectile);
            Vector3 center = Ground.Flat(position);
            Vector3 flight = FlightOf(lance);
            int arms = level.GetCount(WeaponField.SubCount);
            float length = AreaOf(level[WeaponField.Radius]);
            float halfWidth = AreaOf(level[WeaponField.SubRadius]);
            var effect = new HitEffect { stunSeconds = level[WeaponField.Stun] };
            crossMemory.Clear();
            for (int i = 0; i < arms; i++)
            {
                Vector3 arm = Ground.Rotate(flight, Ground.FULL_TURN * i / arms);
                strikes.Capsule(center, center + arm * length, halfWidth, MightOf(level[WeaponField.SubDamage]), KnockbackOf(level[WeaponField.Knockback]), slot,
                                effect, PushMode.Sideways, default, crossMemory);
                ShowArm(center, arm, length);
            }
            (isEvolved ? solarBurst : burst).Play(center + Ground.Up(BURST_HEIGHT), length, fxScale, KnightLooks.GOLD);
            PlayAt("verdict_slam", center);
            ctx.feel.Shake(BURST_TRAUMA);
        }

        /// <summary>One arm of the cross: a gold beam from the center with motes rising along it.</summary>
        private void ShowArm(Vector3 center, Vector3 arm, float length)
        {
            Vector3 from = center + Ground.Up(BEAM_HEIGHT);
            effects.EmitMoving(beam, from, arm * FxSizes.HEADING_SPEED, 1, KnightLooks.GOLD, length * BEAM_SIZE_PER_LENGTH, BEAM_SECONDS);
            for (int i = 1; i <= ARM_MOTES; i++)
            {
                Emit(motes, from + arm * (length * i / ARM_MOTES), 1, KnightLooks.HOLY);
            }
        }
    }
}

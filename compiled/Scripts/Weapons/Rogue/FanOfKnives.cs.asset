using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Fan of Knives (Rogue, Uncommon): SubCount volleys, Stagger apart, each sending knives (Count, plus AmountScale
    /// per point of Amount) out in a full ring at Speed (Projectile Speed), the ring turning Angle degrees per volley;
    /// each knife hits for Damage through Pierce enemies, out to Range. Knives that reach their range stick in the
    /// ground for SubDuration, unless they return (Returns, from rank 8).
    /// Evolution Storm of Steel (+ Echo Quiver): two volleys of more knives that always return.
    /// Look: each volley swirls the air around the rogue and sends a steel ring out with the knives; every knife leaves
    /// with a glint and a crescent and flies with a glinting star; strikes are crisp cuts across its path, and knives
    /// that stick in the ground kick dust and glint. Storm of Steel turns it into a whirl of blades: a wide violet-steel
    /// swirl, crescents sliding around the rogue, metal glints flung out and a shadow shockwave at the feet.
    /// </summary>
    public sealed class FanOfKnives : Weapon
    {
        private const int VOLLEY = 0;
        /// <summary>userValue flag of a knife that sticks in the ground at the end of its range.</summary>
        private const int STICK_FLAG = 1;
        /// <summary>A returning knife flies its range out and the same back.</summary>
        private const float OUT_AND_BACK = 2.0f;
        private const int MAX_KNIVES = 40;
        private const float KNIFE_RADIUS = 0.3f;
        private const float LAUNCH_HEIGHT = 1.0f;
        private const float LAUNCH_OUT = 0.4f;
        private const float STUCK_HEIGHT = 0.12f;
        private const float STUCK_TILT = 0.7f;
        private const float RANGE_END_TOLERANCE = 0.03f;
        /// <summary>Volleys wait for an enemy a little beyond the knives' range.</summary>
        private const float WAKE_MARGIN = 2.0f;
        /// <summary>The knife ring grows with the knives for this long, then fades.</summary>
        private const float RING_SECONDS = 0.2f;
        private const float KNIFE_GLINT_SIZE = 0.38f;
        /// <summary>Crescents leave with the knives at this share of their speed.</summary>
        private const float ARC_OUT = 0.7f;
        private const float ARC_SPEED_SHARE = 0.6f;
        private const float ARC_SIZE = 0.9f;
        private const float ARC_SECONDS = 0.14f;
        /// <summary>Storm of Steel crescents start this far out and slide around the rogue (counter-clockwise from above).</summary>
        private const float BLADE_RADIUS = 1.3f;
        private const float BLADE_SPEED = 8.0f;
        private const float BLADE_SIZE = 1.2f;
        private const float BLADE_SECONDS = 0.24f;
        private const float CUT_SIZE = 0.8f;

        private readonly int knifeVisual;
        private readonly int fanRing;
        private readonly int glint;
        private readonly int knifeArc;
        private readonly int stormBlade;
        private readonly int knifeCut;
        private readonly FxComposite fanThrow;
        private readonly FxComposite stormThrow;
        private readonly FxComposite knifeHit;
        private readonly FxComposite knifeThunk;
        private float ringAngle;

        public FanOfKnives(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            knifeVisual = context.FindVisual("ThrownKnife");
            fanRing = effects.Find("RogueFanRing");
            glint = effects.Find("RogueGlint");
            knifeArc = effects.Find("RogueKnifeArc");
            stormBlade = effects.Find("RogueStormBlade");
            knifeCut = effects.Find("RogueKnifeCut");
            fanThrow = Composite(new FxLayer("RogueFanSwirl", 1, 3.2f), new FxLayer("RogueSparks", 4));
            stormThrow = Composite(new FxLayer("RogueFlash", 1, 1.0f), new FxLayer("RogueFanSwirl", 1, 5.2f), new FxLayer("RogueStormGlints", 10),
                                   new FxLayer("RogueSparks", 8), FxLayer.Ground("RogueShadowShock", 4.5f));
            knifeHit = Composite(new FxLayer("RogueSparks", 2));
            knifeThunk = Composite(new FxLayer("RogueDust", 2, 0.35f, false), new FxLayer("RogueSparks", 2), new FxLayer("RogueGlint", 1, 0.4f));
        }

        protected override bool Cast(in CastInfo cast)
        {
            if (!TryNearest(level[WeaponField.Range] + WAKE_MARGIN, out _))
            {
                return false;
            }
            int volleys = Math.Max(1, level.GetCount(WeaponField.SubCount));
            for (int v = 0; v < volleys; v++)
            {
                Schedule(level[WeaponField.Stagger] * v, new WeaponEvent { kind = VOLLEY, cast = cast });
            }
            RequestAttack("UpperSpin");
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == VOLLEY)
            {
                Throw();
            }
        }

        private void Throw()
        {
            int count = Math.Min(level.GetCount(WeaponField.Count) + level.GetCount(WeaponField.AmountScale) * amount, MAX_KNIVES);
            ringAngle += level[WeaponField.Angle];
            bool returns = level[WeaponField.Returns] > 0.0f;
            float speed = SpeedOf(level[WeaponField.Speed]);
            float range = level[WeaponField.Range];
            Vector3 origin = heroPosition + Ground.Up(LAUNCH_HEIGHT);
            Color color = steelColor;
            for (int k = 0; k < count; k++)
            {
                Vector3 direction = Ground.RingDirection(ringAngle, k, count);
                ProjectileSpawn spawn = NewProjectile(knifeVisual, returns ? ProjectileMotion.Boomerang : ProjectileMotion.Linear, origin + direction * LAUNCH_OUT,
                                                      direction * speed, returns ? OUT_AND_BACK * range / speed : range / speed);
                spawn.damage = DamageOf(WeaponField.Damage, CastInfo.normal);
                spawn.radius = KNIFE_RADIUS;
                spawn.pierce = level.GetCount(WeaponField.Pierce);
                spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
                spawn.handler = this;
                spawn.userValue = returns ? 0 : STICK_FLAG;
                spawn.headTint = isEvolved ? RogueLooks.STORM_STEEL : default;
                Spawn(spawn);
                EmitKnifeLaunch(origin, direction, speed, color);
            }
            EmitShape(fanRing, origin, color, 2.0f * (LAUNCH_OUT + speed * RING_SECONDS), RING_SECONDS);
            (isEvolved ? stormThrow : fanThrow).Play(origin, FxSizes.METRE_UNIT, fxScale, color);
            PlayAt("knife_fan", origin);
        }

        /// <summary>A glint where the knife leaves and a crescent with it; Storm of Steel's crescents whirl around instead.</summary>
        private void EmitKnifeLaunch(Vector3 origin, Vector3 direction, float speed, Color color)
        {
            EmitSized(glint, origin + direction * LAUNCH_OUT, color, KNIFE_GLINT_SIZE * fxScale);
            if (isEvolved)
            {
                Vector3 around = -Ground.Perpendicular(direction);
                effects.EmitMoving(stormBlade, origin + direction * BLADE_RADIUS, around * BLADE_SPEED, 1, color, BLADE_SIZE * fxScale, BLADE_SECONDS);
                return;
            }
            effects.EmitMoving(knifeArc, origin + direction * ARC_OUT, direction * (speed * ARC_SPEED_SHARE), 1, color, ARC_SIZE * fxScale, ARC_SECONDS);
        }

        private Color steelColor => isEvolved ? RogueLooks.STORM_STEEL : RogueLooks.STEEL;

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn knife = ref world.Get(projectile);
            Vector3 impact = knife.position;
            if (HitAlongFlight(enemyRow, knife, WithAutoCrit(default)) == DamageResult.Missed)
            {
                return;
            }
            RogueLooks.EmitKnifeCut(effects, knifeCut, impact, FlightOf(knife), steelColor, CUT_SIZE * fxScale);
            knifeHit.Play(impact, FxSizes.METRE_UNIT, fxScale, steelColor);
        }

        /// <summary>A knife that flew its whole range sticks in the ground, point down, for a moment.</summary>
        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            ref ProjectileSpawn knife = ref world.Get(projectile);
            if ((knife.userValue & STICK_FLAG) == 0 || world.GetAge(projectile) < knife.lifetime - RANGE_END_TOLERANCE)
            {
                return;
            }
            Vector3 tilted = FlightOf(knife) - Ground.Up(STUCK_TILT);
            Vector3 stuck = Ground.Flat(position) + Ground.Up(STUCK_HEIGHT);
            SpawnVisual(knifeVisual, ProjectileMotion.Static, stuck, tilted, level[WeaponField.SubDuration]);
            knifeThunk.Play(stuck, FxSizes.METRE_UNIT, fxScale, RogueLooks.STEEL);
        }
    }
}

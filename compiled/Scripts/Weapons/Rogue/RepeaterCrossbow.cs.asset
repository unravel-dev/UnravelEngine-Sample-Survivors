using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Repeater Crossbow (Rogue, Common): a burst of Count + Amount fast bolts (Speed, Projectile Speed), Interval
    /// apart, each dealing Damage through Pierce enemies; the burst spreads over the crowd, bolt k aimed at the k-th
    /// nearest enemy within Range (no second bolt into a body the first one kills). Critical hits freeze their target
    /// for 20 ms, and from rank 7 deal +CritDamage; every 4th volley ends with a (cosmetic) reload.
    /// Evolution Widowmaker (+ Keen Eye): more bolts that pierce everything, +CritChance, and critical hits ricochet to
    /// one more enemy within SubRange.
    /// Look: each bolt leaves the crossbow with a steel muzzle flick and a spray of sparks, and flies with a thin steel
    /// tracer to its target; hits glint and drip poison, crits flash and ring. Widowmaker bolts trace venom green,
    /// shed crystals along their whole flight and burst in crystal needles and bouncing shards.
    /// </summary>
    public sealed class RepeaterCrossbow : Weapon
    {
        private const int BOLT = 0;
        private const int RELOAD = 1;
        private const int VOLLEYS_PER_RELOAD = 4;
        /// <summary>The reload waits for the shoot clip's slot (one upper clip per 0.4 s).</summary>
        private const float RELOAD_MIN_DELAY = 0.42f;
        private const int RICOCHET_FLAG = 1;
        private const float BOLT_RADIUS = 0.25f;
        private const float LAUNCH_HEIGHT = 1.2f;
        private const float LAUNCH_FORWARD = 0.4f;
        private const float CRIT_FREEZE_SECONDS = 0.02f;
        /// <summary>A burst spreads over at most this many of the nearest enemies; later bolts aim at the last of them.</summary>
        private const int MAX_SPREAD = 8;
        /// <summary>Muzzle flick length (m) and life; it drifts slowly along the shot so it points that way.</summary>
        private const float MUZZLE_SIZE = 0.75f;
        private const float MUZZLE_SECONDS = 0.07f;
        private const float MUZZLE_DRIFT = 0.2f;
        private const float MUZZLE_SPARK_SPREAD = 14.0f;
        private const float MUZZLE_SPARK_JITTER = 6.0f;
        private const float MUZZLE_SPARK_SPEED = 9.0f;
        /// <summary>Each muzzle spark flies at between these shares of the spark speed.</summary>
        private const float MUZZLE_SPARK_SPEED_MIN = 0.7f;
        private const float MUZZLE_SPARK_SPEED_MAX = 1.2f;
        private const float MUZZLE_SPARK_SIZE = 0.06f;
        private const float MUZZLE_SPARK_SECONDS = 0.16f;
        private const float VENOM_RING_SIZE = 0.7f;
        /// <summary>Tracer width (m); it flies to the target and a little beyond, or the whole way for bolts that pierce all.</summary>
        private const float TRACER_WIDTH = 0.07f;
        private const float WIDOW_TRACER_WIDTH = 0.05f;
        private const float TRACER_OVERSHOOT = 1.5f;

        private static readonly Vector3 CHEST = new Vector3(0.0f, LAUNCH_HEIGHT, 0.0f);

        private readonly Enemy[] ricochetExclude = new Enemy[1];
        private readonly int[] nearest = new int[MAX_SPREAD];
        private readonly int boltVisual;
        private readonly int widowVisual;
        private readonly int muzzleFlick;
        private readonly int muzzleSparks;
        private readonly int venomRing;
        private readonly int boltTracer;
        private readonly int widowTracer;
        private readonly FxComposite boltHit;
        private readonly FxComposite boltCrit;
        private readonly FxComposite widowHit;
        private readonly FxComposite widowCrit;
        private readonly FxComposite reloadClick;
        private int volleys;

        public RepeaterCrossbow(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            boltVisual = context.FindVisual("CrossbowBolt");
            widowVisual = context.FindVisual("WidowBolt");
            muzzleFlick = effects.Find("RogueMuzzleFlick");
            muzzleSparks = effects.Find("RogueSparks");
            venomRing = effects.Find("RogueVenomRing");
            boltTracer = effects.Find("RogueBoltTracer");
            widowTracer = effects.Find("RogueWidowTracer");
            boltHit = Composite(new FxLayer("RogueFlash", 1, 0.55f), new FxLayer("RogueGlint", 1, 0.7f), new FxLayer("RogueSparks", 6),
                                new FxLayer("RogueVenomDrops", 3, 0.0f, false));
            boltCrit = Composite(new FxLayer("RogueFlash", 1, 0.9f), new FxLayer("RogueGlint", 1, 1.4f), new FxLayer("RogueVenomRing", 1, 1.1f),
                                 new FxLayer("RogueSparks", 14), new FxLayer("RogueVenomDrops", 6, 0.0f, false));
            widowHit = Composite(new FxLayer("RogueFlash", 1, 0.6f), new FxLayer("RogueGlint", 1, 0.8f), new FxLayer("RogueShardBurst", 7),
                                 new FxLayer("RogueCrystalShatter", 3, 0.0f, false));
            widowCrit = Composite(new FxLayer("RogueFlash", 1, 1.0f), new FxLayer("RogueGlint", 1, 1.6f), new FxLayer("RogueVenomRing", 1, 1.5f),
                                  new FxLayer("RogueShardBurst", 16), new FxLayer("RogueCrystalShatter", 7, 0.0f, false),
                                  new FxLayer("RogueVenomDrops", 5, 0.0f, false));
            reloadClick = Composite(new FxLayer("RogueGlint", 1, 0.5f), new FxLayer("RogueSparks", 3));
        }

        protected override bool Cast(in CastInfo cast)
        {
            if (!TryNearest(level[WeaponField.Range], out _))
            {
                return false;
            }
            int bolts = level.GetCount(WeaponField.Count) + amount;
            for (int i = 0; i < bolts; i++)
            {
                Schedule(level[WeaponField.Interval] * i, new WeaponEvent { kind = BOLT, cast = cast, index = i });
            }
            RequestAttack("Shoot");
            volleys++;
            if (volleys % VOLLEYS_PER_RELOAD == 0)
            {
                Schedule(MathF.Max(RELOAD_MIN_DELAY, level[WeaponField.Interval] * bolts), new WeaponEvent { kind = RELOAD });
            }
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == RELOAD)
            {
                RequestAttack("Reload");
                reloadClick.Play(heroPosition + CHEST, FxSizes.METRE_UNIT, fxScale, RogueLooks.STEEL);
                return;
            }
            if (e.kind != BOLT)
            {
                return;
            }
            float range = level[WeaponField.Range];
            int found = FindNearest(range, nearest, Math.Min(e.index + 1, MAX_SPREAD));
            bool hasTarget = found > 0;
            int target = hasTarget ? nearest[found - 1] : -1;
            Vector3 direction = hasTarget ? Ground.Direction(heroPosition, enemies.GetPosition(target), facing) : facing;
            Vector3 origin = heroPosition + direction * LAUNCH_FORWARD + CHEST;
            float reach = hasTarget ? Ground.Distance(origin, enemies.GetPosition(target)) : float.MaxValue;
            Fire(origin, direction, 0, reach);
            EmitMuzzle(origin, direction);
            PlayAt("crossbow_twang", origin);
        }

        /// <summary>Fires a bolt and its tracer; <paramref name="reach"/> is the distance to the enemy it is aimed at.</summary>
        private void Fire(Vector3 origin, Vector3 direction, int flags, float reach)
        {
            float speed = SpeedOf(level[WeaponField.Speed]);
            float flight = level[WeaponField.Range] / speed;
            ProjectileSpawn spawn = NewProjectile(isEvolved ? widowVisual : boltVisual, ProjectileMotion.Linear, origin, direction * speed, flight);
            spawn.damage = DamageOf(WeaponField.Damage, CastInfo.normal);
            spawn.radius = BOLT_RADIUS;
            spawn.pierce = (flags & RICOCHET_FLAG) != 0 ? 0 : level.GetCount(WeaponField.Pierce);
            spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
            spawn.handler = this;
            spawn.userValue = flags;
            Spawn(spawn);
            float tracerSeconds = spawn.pierce < 0 ? flight : MathF.Min(flight, (reach + TRACER_OVERSHOOT) / speed);
            effects.EmitMoving(isEvolved ? widowTracer : boltTracer, origin, spawn.velocity, 1, isEvolved ? RogueLooks.VENOM : RogueLooks.STEEL_WHITE,
                               (isEvolved ? WIDOW_TRACER_WIDTH : TRACER_WIDTH) * fxScale, tracerSeconds);
        }

        /// <summary>A steel flick along the shot and three sparks sprayed ahead; Widowmaker adds a venom ring.</summary>
        private void EmitMuzzle(Vector3 origin, Vector3 direction)
        {
            Color color = isEvolved ? RogueLooks.VENOM : RogueLooks.STEEL_WHITE;
            effects.EmitMoving(muzzleFlick, origin, direction * MUZZLE_DRIFT, 1, color, MUZZLE_SIZE * fxScale, MUZZLE_SECONDS);
            for (int k = -1; k <= 1; k++)
            {
                float angle = k * MUZZLE_SPARK_SPREAD + Random.Range(-MUZZLE_SPARK_JITTER, MUZZLE_SPARK_JITTER);
                Vector3 velocity = Ground.Rotate(direction, angle) * (MUZZLE_SPARK_SPEED * Random.Range(MUZZLE_SPARK_SPEED_MIN, MUZZLE_SPARK_SPEED_MAX));
                effects.EmitMoving(muzzleSparks, origin, velocity, 1, RogueLooks.STEEL, MUZZLE_SPARK_SIZE, MUZZLE_SPARK_SECONDS);
            }
            if (isEvolved)
            {
                EmitSized(venomRing, origin, RogueLooks.VENOM, VENOM_RING_SIZE * fxScale);
            }
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn bolt = ref world.Get(projectile);
            var effect = WithAutoCrit(new HitEffect
            {
                critChanceBonus = level[WeaponField.CritChance],
                critDamageBonus = level[WeaponField.CritDamage],
            });
            Vector3 position = enemies.GetPosition(enemyRow);
            Enemy struck = enemies.GetEnemy(enemyRow);
            bool canRicochet = isEvolved && (bolt.userValue & RICOCHET_FLAG) == 0;
            Vector3 impact = bolt.position;
            DamageResult result = HitAlongFlight(enemyRow, bolt, effect);
            if (result != DamageResult.Missed)
            {
                PlayHit(impact, strikes.lastWasCritical);
            }
            if (!strikes.lastWasCritical)
            {
                return;
            }
            if (result == DamageResult.Hurt)
            {
                enemies.Stun(enemyRow, CRIT_FREEZE_SECONDS);
            }
            if (canRicochet)
            {
                Ricochet(position, struck);
            }
        }

        private void PlayHit(Vector3 at, bool isCritical)
        {
            if (isEvolved)
            {
                (isCritical ? widowCrit : widowHit).Play(at, FxSizes.METRE_UNIT, fxScale, RogueLooks.VENOM);
                return;
            }
            (isCritical ? boltCrit : boltHit).Play(at, FxSizes.METRE_UNIT, fxScale, RogueLooks.STEEL);
        }

        private void Ricochet(Vector3 from, Enemy struck)
        {
            ricochetExclude[0] = struck;
            if (!strikes.FindNearestExcept(from, level[WeaponField.SubRange], ricochetExclude, out int next))
            {
                return;
            }
            Vector3 direction = Ground.Direction(from, enemies.GetPosition(next), facing);
            Vector3 origin = Ground.Flat(from) + direction * LAUNCH_FORWARD + CHEST;
            Fire(origin, direction, RICOCHET_FLAG, Ground.Distance(origin, enemies.GetPosition(next)));
            EmitSized(venomRing, origin, RogueLooks.VENOM, VENOM_RING_SIZE * fxScale);
            PlayAt("widow_ricochet", from);
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Rampage (Barbarian, Uncommon): for Duration seconds (Duration stat; again after Cooldown) the barbarian runs MoveSpeed
    /// faster and every SubRange metres he runs his foot comes down: Damage in Radius (Area), throwing the dead aside
    /// (Knockback) and staggering them (Stun); every Interval he stands still he stamps instead. Each stomp's hits feed Fury.
    /// Evolution Juggernaut (+ Windstride Boots): the rampage never ends.
    /// Look: red flames lick around him while it lasts; each stomp is a shock ring, a rolling dust ring, a crack, rocks,
    /// clods and grit around his foot, blood red at full Fury. Juggernaut cracks glow crimson.
    /// </summary>
    public sealed class Rampage : Weapon
    {
        /// <summary>Movement in one frame beyond this is a dash or a teleport, not a stride.</summary>
        private const float MAX_STEP = 2.0f;
        /// <summary>The foot comes down this far ahead of the body.</summary>
        private const float FOOT_AHEAD = 0.5f;
        private const float BURST_HEIGHT = 0.25f;
        private const float CRACK_SHARE = 1.5f;
        private const float CRACK_SECONDS = 1.4f;
        private const float STOMP_TRAUMA = 0.06f;
        private const float STOMP_VOLUME = 0.55f;
        private const float FLAME_INTERVAL = 0.07f;
        private const int FLAMES = 2;
        private const float FLAME_SPREAD = 0.45f;
        private const float FLAME_LOW = 0.2f;
        private const float FLAME_HIGH = 1.5f;
        private const float FLAME_MIN = 0.45f;
        private const float FLAME_MAX = 0.8f;
        private const float GLOW_INTENSITY = 0.25f;
        private const float GLOW_SECONDS = 0.1f;

        private readonly BarbarianKit barbarian;
        private readonly int crack;
        private readonly int crackGlow;
        private readonly int flame;
        private readonly FxComposite stomp;
        private readonly FxComposite roar;
        private StatBonus speedBonus = new StatBonus(StatId.MoveSpeed, true);
        private float timeLeft;
        private float travelled;
        private float standing;
        private float flameTimer;
        private Vector3 lastPosition;

        public Rampage(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            barbarian = context.KitAs<BarbarianKit>();
            crack = effects.Find("BarbarianCrack");
            crackGlow = effects.Find("BarbarianCrackGlow");
            flame = effects.Find("BarbarianFuryFlame");
            lastPosition = heroPosition;
            // Size unit = stomp radius.
            stomp = Composite(FxLayer.Ground("BarbarianShockRing", 2.2f, false), FxLayer.Ground("BarbarianDustRing", 2.2f, false), new FxLayer("BarbarianRocks", 3, 0.0f, false),
                              new FxLayer("BarbarianDirtSpray", 3, 0.0f, false), new FxLayer("BarbarianGrit", 6), new FxLayer("BarbarianDust", 2, 0.25f, false));
            // Size unit = 1 m.
            roar = Composite(new FxLayer("BarbarianFlash", 1, 2.0f), new FxLayer("BarbarianFuryFlame", 10, 0.9f), new FxLayer("BarbarianFuryEmbers", 12));
        }

        private bool isRampaging => isEvolved || timeLeft > 0.0f;

        protected override void OnDispose() => speedBonus.Set(stats, 0.0f);

        protected override void OnLevelChanged()
        {
            // Juggernaut's lasting speed shows at once.
            if (isRampaging)
            {
                speedBonus.Set(stats, level[WeaponField.MoveSpeed]);
            }
        }

        protected override bool Cast(in CastInfo cast)
        {
            if (isEvolved)
            {
                return true;
            }
            timeLeft = DurationOf(level[WeaponField.Duration]);
            speedBonus.Set(stats, level[WeaponField.MoveSpeed]);
            roar.Play(heroPosition + Ground.Up(1.0f), FxSizes.METRE_UNIT, fxScale, BarbarianLooks.FURY_RED);
            PlayAt("war_roar", heroPosition);
            return true;
        }

        protected override void Update(float dt)
        {
            Vector3 position = heroPosition;
            float step = Ground.Distance(lastPosition, position);
            lastPosition = position;
            if (!isRampaging || !hero.isAlive)
            {
                return;
            }
            if (timeLeft > 0.0f)
            {
                timeLeft -= dt;
                if (timeLeft <= 0.0f && !isEvolved)
                {
                    speedBonus.Set(stats, 0.0f);
                }
            }
            ShowFlames(position, dt);
            standing = step > 0.0f ? 0.0f : standing + dt;
            travelled += step <= MAX_STEP ? step : 0.0f;
            bool isStamping = standing >= level[WeaponField.Interval];
            if (travelled < level[WeaponField.SubRange] && !isStamping)
            {
                return;
            }
            travelled = 0.0f;
            standing = 0.0f;
            Stomp(position + facing * FOOT_AHEAD);
        }

        private void Stomp(Vector3 foot)
        {
            Vector3 ground = Ground.Flat(foot);
            float radius = AreaOf(level[WeaponField.Radius]);
            var effect = new HitEffect { stunSeconds = level[WeaponField.Stun] };
            int hits = strikes.Circle(ground, radius, MightOf(level[WeaponField.Damage]), KnockbackOf(level[WeaponField.Knockback]), slot, effect);
            barbarian?.AddFury(hits * level[WeaponField.Fury]);
            Color tint = barbarian != null && barbarian.isFuryFull ? BarbarianLooks.FURY_RED : BarbarianLooks.STEEL;
            stomp.Play(ground + Ground.Up(BURST_HEIGHT), radius, fxScale, tint);
            Vector3 decal = ground + Ground.Up(FxSizes.LOW_GROUND_LIFT);
            effects.EmitMoving(crack, decal, Ground.RandomDirection() * FxSizes.HEADING_SPEED, 1, WHITE, radius * CRACK_SHARE, CRACK_SECONDS);
            if (isEvolved)
            {
                effects.EmitMoving(crackGlow, decal, Ground.RandomDirection() * FxSizes.HEADING_SPEED, 1, BarbarianLooks.CRIMSON, radius * CRACK_SHARE, CRACK_SECONDS);
            }
            PlayAt("fault_stomp", ground, STOMP_VOLUME);
            ctx.feel.Shake(STOMP_TRAUMA);
        }

        /// <summary>Red flames around the body and a faint red glow while the rampage lasts.</summary>
        private void ShowFlames(Vector3 position, float dt)
        {
            flameTimer -= dt;
            if (flameTimer > 0.0f)
            {
                return;
            }
            flameTimer = FLAME_INTERVAL;
            for (int i = 0; i < FLAMES; i++)
            {
                Vector3 at = position + Ground.RandomInCircle(FLAME_SPREAD) + Ground.Up(Random.Range(FLAME_LOW, FLAME_HIGH));
                effects.EmitSized(flame, at, 1, BarbarianLooks.FURY_RED, Random.Range(FLAME_MIN, FLAME_MAX));
            }
            hero.SetGlow(BarbarianLooks.FURY_RED, GLOW_INTENSITY, GLOW_SECONDS);
        }
    }
}

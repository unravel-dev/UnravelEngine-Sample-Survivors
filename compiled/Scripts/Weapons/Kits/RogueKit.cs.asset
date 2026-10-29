using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Rogue. Active Smoke Roll: an invulnerable roll that drops a smoke cloud in which enemies lose the hero and
    /// wander, and makes the next Rogue weapon hits critical (<see cref="PrepareHit"/>). Signature Momentum: moving
    /// builds stacks of crit chance, Might, Cooldown and move speed; standing still a moment resets them; at full
    /// Momentum crits heal (Bloodletting). <see cref="ACTIVE_TEXT"/> and <see cref="SIGNATURE_TEXT"/> state the numbers.
    /// Look: the smoke bomb pops with a violet flash and steel shrapnel, billows pale grey-violet smoke 2-3 m tall and
    /// sends a dark puff ring rolling out to the cloud's edge over a shadow shockwave; for the cloud's life thin smoke
    /// wisps rise all over its radius at knee to chest height. The roll leaves violet afterimages and shadow smoke, and
    /// kicks dust where it ends. The loaded critical hits circle the rogue as poison-green glints until spent.
    /// Momentum streams steel wind streaks, denser with every stack; at full Momentum a shadow shockwave and glints mark
    /// it, violet wisps curl from the feet and faint violet afterimages trail the rogue.
    /// </summary>
    public sealed class RogueKit : ClassKit
    {
        private const float ROLL_COOLDOWN = 6.0f;
        private const float ROLL_DISTANCE = 5.0f;
        private const float ROLL_SECONDS = 0.35f;
        private const float ROLL_INVULNERABLE = 0.45f;
        private const float SMOKE_RADIUS = 3.0f;
        private const float SMOKE_SECONDS = 3.0f;
        private const float SMOKE_TICK = 0.25f;
        private const float SMOKE_CONFUSE = 0.5f;
        /// <summary>Cloud wisps: this many per interval at random points of this share of the radius, between the two
        /// heights, until this long before the cloud ends (the last wisps thin out around its end).</summary>
        private const float SMOKE_PUFF_INTERVAL = 0.1f;
        private const int SMOKE_PUFF_COUNT = 2;
        private const float SMOKE_PUFF_SPREAD = 0.9f;
        private const float SMOKE_PUFF_LOW = 0.5f;
        private const float SMOKE_PUFF_HIGH = 1.0f;
        private const float SMOKE_PUFF_SIZE_MIN = 1.2f;
        private const float SMOKE_PUFF_SIZE_MAX = 1.6f;
        private const float SMOKE_PUFF_END_MARGIN = 0.6f;
        private const int AUTO_CRITS = 3;
        private const float MOMENTUM_STEP = 0.5f;
        private const float MOMENTUM_RESET = 0.4f;
        private const int MOMENTUM_MAX = 10;
        /// <summary>Bloodletting: a crit at full Momentum heals this much, at most this share of max health per second.</summary>
        private const float BLOODLETTING_HEAL = 0.2f;
        private const float BLOODLETTING_SHARE_PER_SECOND = 0.015f;
        private const float CRIT_PER_STACK = 0.03f;
        private const float SPEED_PER_STACK = 0.01f;
        private const float MIGHT_PER_STACK = 0.02f;
        private const float COOLDOWN_PER_STACK = -0.015f;
        private const float WISP_INTERVAL = 0.12f;
        private const int WISP_COUNT = 2;
        private const float WISP_HEIGHT = 0.1f;
        /// <summary>The kit's layered moments keep their particle counts: they do not grow with weapon ranks.</summary>
        private const float FX_INTENSITY = 1.0f;
        /// <summary>The bomb's billows start at chest height; its dark puff ring rolls out low over the ground.</summary>
        private const float BOMB_HEIGHT = 0.9f;
        private const float RING_HEIGHT = 0.35f;
        /// <summary>Roll afterimages: a violet shade this tall every interval along the roll, at this opacity (they overlap).</summary>
        private const float AFTERIMAGE_INTERVAL = 0.035f;
        private const float AFTERIMAGE_ALPHA = 0.55f;
        private const float AFTERIMAGE_HEIGHT = 1.9f;
        private const float AFTERIMAGE_SECONDS = 0.3f;
        private const float ROLL_PUFF_SIZE = 0.7f;
        private const float ROLL_PUFF_HEIGHT = 0.5f;
        /// <summary>Loaded critical hits: glints circling the chest (degrees per second).</summary>
        private const float CHARGE_RADIUS = 0.6f;
        private const float CHARGE_HEIGHT = 1.25f;
        private const float CHARGE_TURN = 300.0f;
        private const float CHARGE_SIZE = 0.24f;
        /// <summary>Momentum wind streaks: one per interval, from the slow interval at 1 stack to the fast one at full.</summary>
        private const float STREAK_INTERVAL_SLOW = 0.22f;
        private const float STREAK_INTERVAL_FAST = 0.05f;
        private const float STREAK_SPEED = 7.0f;
        private const float STREAK_WIDTH = 0.05f;
        private const float STREAK_SECONDS = 0.2f;
        private const float STREAK_SIDE = 0.45f;
        private const float STREAK_LOW = 0.3f;
        private const float STREAK_HIGH = 1.7f;
        private const float STREAK_MIN_ALPHA = 0.35f;
        /// <summary>Full Momentum afterimages: fainter and slower than the roll's.</summary>
        private const float GHOST_ALPHA = 0.25f;
        private const float GHOST_SECONDS = 0.25f;
        private const float BURST_HEIGHT = 1.0f;

        /// <summary>Momentum, for the hero select and details screens.</summary>
        public static readonly string SIGNATURE_TEXT =
            $"Every {WeaponText.Number(MOMENTUM_STEP)} s of movement grants 1 Momentum (max {MOMENTUM_MAX}): +{WeaponText.Percent(CRIT_PER_STACK)} crit chance, " +
            $"+{WeaponText.Percent(MIGHT_PER_STACK)} damage, {WeaponText.Percent(COOLDOWN_PER_STACK)} cooldowns and +{WeaponText.Percent(SPEED_PER_STACK)} move speed each. " +
            $"Standing still resets it. At full Momentum crits heal, up to {WeaponText.Percent(BLOODLETTING_SHARE_PER_SECOND)} of max health per second.";

        /// <summary>Smoke Roll, for the hero select and details screens.</summary>
        public static readonly string ACTIVE_TEXT =
            $"Roll {WeaponText.Number(ROLL_DISTANCE)} m, invulnerable, leaving a smoke cloud that blinds enemies for {WeaponText.Number(SMOKE_SECONDS)} s. " +
            $"The next {AUTO_CRITS} Rogue weapon hits crit. Cooldown {WeaponText.Number(ROLL_COOLDOWN)} s.";

        private readonly int smokeCloud;
        private readonly int afterimage;
        private readonly int rollPuff;
        private readonly int critCharge;
        private readonly int speedStreak;
        private readonly int momentumWisps;
        private readonly FxComposite smokeBomb;
        private readonly FxComposite smokeRing;
        private readonly FxComposite rollLanding;
        private readonly FxComposite momentumBurst;
        private int autoCrits;
        private int momentum;
        private HealBudget bloodletting = new HealBudget(BLOODLETTING_SHARE_PER_SECOND);
        private StatBonus momentumCrit = new StatBonus(StatId.CritChance, false);
        private StatBonus momentumSpeed = new StatBonus(StatId.MoveSpeed, true);
        private StatBonus momentumMight = new StatBonus(StatId.Might, true);
        private StatBonus momentumCooldown = new StatBonus(StatId.Cooldown, true);
        private float movingTime;
        private float stillTime;
        private float wispTimer;
        private float rollTimer;
        private float afterimageTimer;
        private float chargeAngle;
        private float streakTimer;
        private Vector3 cloudCenter;
        private float cloudTimer;
        private float cloudPuffTimer;

        /// <summary>
        /// Smoke Roll dropped its cloud: the cloud's center (on the ground) and radius (Legion of Shadows leaves clones
        /// around it). Raised during the activation, outside any damage call.
        /// </summary>
        public event Action<Vector3, float> smokeDropped;

        public RogueKit(WeaponContext context) : base(context, HeroClass.Rogue)
        {
            smokeCloud = effects.Find("RogueSmokeCloud");
            afterimage = effects.Find("RogueCloneRim");
            rollPuff = effects.Find("RogueShadowPuff");
            critCharge = effects.Find("RogueKnifeHead");
            speedStreak = effects.Find("RogueSpeedStreak");
            momentumWisps = effects.Find("RogueMomentumWisp");
            smokeBomb = new FxComposite(effects, new FxLayer("RogueFlash", 1, 2.0f), new FxLayer("RogueSmokeBurst", 16, 1.2f, false),
                                        new FxLayer("RogueShadowPuff", 4, 0.9f, false), new FxLayer("RogueSparks", 10));
            smokeRing = new FxComposite(effects, new FxLayer("RogueSmokeRing", 18, 0.9f, false), FxLayer.Ground("RogueShadowShock", SMOKE_RADIUS * 2.0f));
            rollLanding = new FxComposite(effects, new FxLayer("RogueDust", 3, 0.5f, false));
            momentumBurst = new FxComposite(effects, new FxLayer("RogueFlash", 1, 0.9f), new FxLayer("RogueStormGlints", 8),
                                            FxLayer.Ground("RogueShadowShock", 3.0f));
            ctx.run.combatEvents.hit += OnHit;
        }

        public override void Dispose()
        {
            ctx.run.combatEvents.hit -= OnHit;
        }

        /// <summary>Bloodletting: crits at full Momentum heal, within the per-second cap.</summary>
        private void OnHit(int row, int source, float amount, bool isCritical)
        {
            if (!isCritical || momentum < MOMENTUM_MAX)
            {
                return;
            }
            bloodletting.Heal(hero, BLOODLETTING_HEAL, HealSource.Class);
        }

        protected override float baseActiveCooldown => ROLL_COOLDOWN;
        public override int signatureStacks => momentum;

        /// <summary>A Rogue weapon hit spends one of Smoke Roll's guaranteed critical hits.</summary>
        public override void PrepareHit(ref HitEffect effect)
        {
            if (autoCrits <= 0)
            {
                return;
            }
            autoCrits--;
            effect.forceCrit = true;
        }

        protected override bool Activate()
        {
            Vector3 origin = hero.position;
            Vector3 direction = dashDirection;
            hero.Dash(direction, ctx.GetTravel(origin, direction, ROLL_DISTANCE), ROLL_SECONDS, true);
            hero.SetInvulnerable(ROLL_INVULNERABLE);
            hero.PlayFullBody("Dodge", ROLL_SECONDS);
            smokeBomb.Play(origin + Ground.Up(BOMB_HEIGHT), FxSizes.METRE_UNIT, FX_INTENSITY, RogueLooks.PALE_VIOLET);
            smokeRing.Play(origin + Ground.Up(RING_HEIGHT), FxSizes.METRE_UNIT, FX_INTENSITY, RogueLooks.PALE_VIOLET);
            ctx.zones.Add(new ZoneSpec
            {
                shape = ZoneShape.Circle,
                a = origin,
                b = origin,
                radius = SMOKE_RADIUS,
                interval = SMOKE_TICK,
                duration = SMOKE_SECONDS,
                source = RunRecord.OTHER_SOURCE,
                confuseSeconds = SMOKE_CONFUSE,
            });
            cloudCenter = origin;
            cloudTimer = SMOKE_SECONDS - SMOKE_PUFF_END_MARGIN;
            cloudPuffTimer = 0.0f;
            autoCrits = AUTO_CRITS;
            rollTimer = ROLL_SECONDS;
            afterimageTimer = 0.0f;
            sounds.PlayAt("smoke_poof", origin);
            sounds.PlayAt("roll_whoosh", origin);
            smokeDropped?.Invoke(origin, SMOKE_RADIUS);
            return true;
        }

        protected override void Step(float dt)
        {
            bloodletting.Refill(hero.maxHealth, dt);
            if (hero.isMoving)
            {
                stillTime = 0.0f;
                movingTime += dt;
                while (movingTime >= MOMENTUM_STEP)
                {
                    movingTime -= MOMENTUM_STEP;
                    GainMomentum();
                }
            }
            else
            {
                movingTime = 0.0f;
                stillTime += dt;
                if (stillTime >= MOMENTUM_RESET)
                {
                    momentum = 0;
                }
            }
            ApplyMomentum();
            ShowCloud(dt);
            ShowRoll(dt);
            ShowCritCharges(dt);
            ShowMomentum(dt);
        }

        private void GainMomentum()
        {
            if (momentum >= MOMENTUM_MAX)
            {
                return;
            }
            momentum++;
            if (momentum == MOMENTUM_MAX)
            {
                sounds.Play("momentum_max");
                momentumBurst.Play(hero.position + Ground.Up(BURST_HEIGHT), FxSizes.METRE_UNIT, FX_INTENSITY, RogueLooks.SHADOW_VIOLET);
            }
        }

        /// <summary>Moves the Momentum bonuses to the current stacks, once per frame.</summary>
        private void ApplyMomentum()
        {
            momentumCrit.Set(stats, CRIT_PER_STACK * momentum);
            momentumSpeed.Set(stats, SPEED_PER_STACK * momentum);
            momentumMight.Set(stats, MIGHT_PER_STACK * momentum);
            momentumCooldown.Set(stats, COOLDOWN_PER_STACK * momentum);
        }

        /// <summary>The cloud's wisps over its whole radius (the zone draws nothing itself: its puffs would be born in the ground).</summary>
        private void ShowCloud(float dt)
        {
            if (cloudTimer <= 0.0f)
            {
                return;
            }
            cloudTimer -= dt;
            cloudPuffTimer -= dt;
            if (cloudPuffTimer > 0.0f)
            {
                return;
            }
            cloudPuffTimer += SMOKE_PUFF_INTERVAL;
            for (int k = 0; k < SMOKE_PUFF_COUNT; k++)
            {
                Vector3 at = cloudCenter + Ground.RandomInCircle(SMOKE_RADIUS * SMOKE_PUFF_SPREAD) +
                             new Vector3(0.0f, Random.Range(SMOKE_PUFF_LOW, SMOKE_PUFF_HIGH), 0.0f);
                effects.EmitSized(smokeCloud, at, 1, Color.white, Random.Range(SMOKE_PUFF_SIZE_MIN, SMOKE_PUFF_SIZE_MAX));
            }
        }

        /// <summary>While rolling: violet afterimages and shadow smoke along the path; dust where the roll ends.</summary>
        private void ShowRoll(float dt)
        {
            if (rollTimer <= 0.0f)
            {
                return;
            }
            rollTimer -= dt;
            afterimageTimer -= dt;
            Vector3 foot = hero.position;
            if (afterimageTimer <= 0.0f)
            {
                afterimageTimer += AFTERIMAGE_INTERVAL;
                Color shade = RogueLooks.SHADOW_VIOLET;
                shade.a = AFTERIMAGE_ALPHA;
                effects.Emit(afterimage, foot, 1, shade, AFTERIMAGE_HEIGHT, AFTERIMAGE_SECONDS);
                effects.EmitSized(rollPuff, foot + Ground.Up(ROLL_PUFF_HEIGHT), 1, Color.white, ROLL_PUFF_SIZE);
            }
            if (rollTimer <= 0.0f)
            {
                rollLanding.Play(foot, FxSizes.METRE_UNIT, FX_INTENSITY, Color.white);
            }
        }

        /// <summary>The critical hits Smoke Roll loaded circle the chest as poison-green glints, one per hit left.</summary>
        private void ShowCritCharges(float dt)
        {
            if (autoCrits <= 0)
            {
                return;
            }
            chargeAngle += CHARGE_TURN * dt;
            Vector3 center = hero.position + Ground.Up(CHARGE_HEIGHT);
            for (int k = 0; k < autoCrits; k++)
            {
                Vector3 at = center + Ground.RingDirection(chargeAngle, k, AUTO_CRITS) * CHARGE_RADIUS;
                effects.EmitSized(critCharge, at, 1, RogueLooks.VENOM, CHARGE_SIZE);
            }
        }

        /// <summary>Wind streaks while moving with Momentum; at full Momentum violet wisps and faint afterimages too.</summary>
        private void ShowMomentum(float dt)
        {
            if (momentum <= 0)
            {
                return;
            }
            float share = (float)momentum / MOMENTUM_MAX;
            streakTimer -= dt;
            if (hero.isMoving && streakTimer <= 0.0f)
            {
                streakTimer = STREAK_INTERVAL_SLOW + (STREAK_INTERVAL_FAST - STREAK_INTERVAL_SLOW) * share;
                EmitStreak(share);
            }
            if (momentum < MOMENTUM_MAX)
            {
                return;
            }
            wispTimer -= dt;
            if (wispTimer <= 0.0f)
            {
                wispTimer = WISP_INTERVAL;
                effects.Emit(momentumWisps, hero.position + Ground.Up(WISP_HEIGHT), WISP_COUNT, RogueLooks.SHADOW_VIOLET);
                if (hero.isMoving)
                {
                    Color ghost = RogueLooks.SHADOW_VIOLET;
                    ghost.a = GHOST_ALPHA;
                    effects.Emit(afterimage, hero.position, 1, ghost, AFTERIMAGE_HEIGHT, GHOST_SECONDS);
                }
            }
        }

        /// <summary>A steel streak beside the rogue blowing backward; brighter with more stacks.</summary>
        private void EmitStreak(float share)
        {
            Vector3 forward = hero.lastMoveDirection;
            Vector3 side = Ground.Perpendicular(forward) * Random.Range(-STREAK_SIDE, STREAK_SIDE);
            Vector3 at = hero.position + side + new Vector3(0.0f, Random.Range(STREAK_LOW, STREAK_HIGH), 0.0f);
            Color color = RogueLooks.STEEL;
            color.a = STREAK_MIN_ALPHA + (1.0f - STREAK_MIN_ALPHA) * share;
            effects.EmitMoving(speedStreak, at, forward * -STREAK_SPEED, 1, color, STREAK_WIDTH, STREAK_SECONDS);
        }
    }
}

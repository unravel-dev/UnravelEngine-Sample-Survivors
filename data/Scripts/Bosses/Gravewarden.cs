using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The Gravewarden, the 5:00 mini-boss: a giant bone knight with a blade and a great shield. At range it Shield
    /// Charges down a lane, up close it brings an Overhead Cleave down in a cone, and every 20 s it raises Bone Guard:
    /// for 3 s the shield turns 80% of the damage away while the hero stands in front of it, so the player learns to
    /// flank. A charge leaves it dazed and open (+30% damage taken). A hero who stays at its feet gets a Grave Stomp: a
    /// ring that fills, then a blow that throws the hero out. At half health its Wrath breaks out: it stands untouchable
    /// for a moment, a shockwave throws the hero back and Shieldbearers rise; from then on it walks faster, charges twice
    /// and follows every cleave with a backswing. Damage is not scaled by time.
    /// </summary>
    public sealed class Gravewarden : Boss
    {
        private const float HEALTH = 5500.0f;
        private const float CONTACT_DAMAGE = 22.0f;
        private const int GOLD = 30;
        private const float WALK_SPEED = 2.4f;
        private const float TURN_RATE = 150.0f;
        private const float AIM_TURN_RATE = 720.0f;
        private const float STOP_DISTANCE = 2.6f;
        private const float GAP_SECONDS = 1.0f;
        private const float FIRST_ATTACK_DELAY = 1.2f;

        private const float CHARGE_WINDUP = 1.0f;
        private const float SECOND_CHARGE_WINDUP = 0.55f;
        private const float CHARGE_SPEED = 12.0f;
        private const float CHARGE_LENGTH = 10.0f;
        private const float CHARGE_HALF_WIDTH = 1.0f;
        private const float CHARGE_DAMAGE = 50.0f;
        private const float CHARGE_KNOCKBACK = 3.0f;
        private const float CHARGE_RECOVERY = 1.2f;
        private const float CHARGE_MIN_RANGE = 5.0f;
        private const float CHARGE_MAX_RANGE = 12.0f;
        private const float CHARGE_COOLDOWN = 6.0f;
        /// <summary>A charge that moves less than this share of its stride in a frame hit the arena edge.</summary>
        private const float CHARGE_BLOCKED_SHARE = 0.3f;
        private const float CHARGE_DUST_INTERVAL = 0.08f;
        private const float DAZED_DAMAGE_TAKEN = 1.3f;

        private const float CLEAVE_WINDUP = 0.9f;
        private const float CLEAVE_RANGE = 3.5f;
        private const float CLEAVE_HALF_ANGLE = 60.0f;
        private const float CLEAVE_DAMAGE = 55.0f;
        private const float BACKSWING_WINDUP = 0.5f;
        private const float CLEAVE_RECOVERY = 0.8f;
        /// <summary>The cleave starts when the hero is this close (center to center).</summary>
        private const float CLEAVE_TRIGGER = 4.0f;

        /// <summary>Seconds at its feet (within the reach past its body) before a Grave Stomp comes.</summary>
        private const float HUG_SECONDS = 1.5f;
        private const float HUG_REACH = 2.2f;
        private const float STOMP_WINDUP = 0.75f;
        private const float STOMP_RADIUS = 5.5f;
        private const float STOMP_DAMAGE = 45.0f;
        private const float STOMP_THROW = 5.0f;
        private const float STOMP_RECOVERY = 0.7f;
        private const float STOMP_DAMAGE_TAKEN = 1.3f;

        private const float WRATH_SHARE = 0.5f;
        private const float WRATH_SECONDS = 1.8f;
        private const float WRATH_WAVE_AT = 0.9f;
        private const float WRATH_RADIUS = 9.0f;
        private const float WRATH_DAMAGE = 35.0f;
        private const float WRATH_THROW = 5.0f;
        private const int WRATH_GUARDS = 6;
        private const float WRATH_GUARD_RING = 6.0f;
        private const float WRATH_SPEED = 1.25f;
        private const float WRATH_GAP = 0.7f;
        private const float WRATH_BANNER_SECONDS = 2.5f;
        private const float FULL_TURN_DEGREES = 360.0f;

        private const float GUARD_FIRST = 10.0f;
        private const float GUARD_INTERVAL = 20.0f;
        private const float GUARD_SECONDS = 3.0f;
        private const float GUARD_DAMAGE_TAKEN = 0.2f;
        private const float GUARD_TURN_RATE = 50.0f;
        private const float GUARD_GLOW_INTERVAL = 0.3f;
        private const float GUARD_TINK_INTERVAL = 0.15f;

        private static readonly Color BONE_TINT = new Color(0.91f, 0.88f, 0.8f, 1.0f);
        private static readonly Color GOLD_TRIM = new Color(1.0f, 0.8f, 0.42f, 1.0f);
        private static readonly Color GOLD_GLOW = new Color(1.0f, 0.72f, 0.28f, 1.0f);
        private static readonly Color GUARD_COLOR = new Color(1.0f, 0.9f, 0.6f, 1.0f);
        private static readonly float[] PHASE_MARKS = { WRATH_SHARE };

        private enum Maneuver
        {
            None,
            Charge,
            Cleave,
            Guard,
            Stomp,
            Wrath,
        }

        private Maneuver move = Maneuver.None;
        private float moveTime;
        private Vector3 aim;
        private bool stung;
        private bool struck;
        private bool charging;
        private bool chargeHit;
        private float charged;
        private float dazedUntil;
        private float dustTimer;
        private float gap = FIRST_ATTACK_DELAY;
        private float chargeCooldown;
        private float guardTimer = GUARD_FIRST;
        private float guardGlowTimer;
        private float lastTink = -1.0f;
        private float hugTime;
        private bool isWrathful;
        private int chargesLeft;
        private int backswingsLeft;
        private float windup;

        public Gravewarden(Run run, BossFx fx, BossHazards hazards, Vector3[] obstacles)
            : base(run, fx, hazards, BossKind.Gravewarden, obstacles)
        {
        }

        public override string title => "Warden of the Barrow";

        public override float[] phaseMarks => PHASE_MARKS;

        public override int goldReward => GOLD;

        public override string defeatId => SaveData.BOSS_GRAVEWARDEN;

        protected override float maxHealth => HEALTH;

        protected override float contactDamage => CONTACT_DAMAGE;

        protected override string prefabKey => "app:/data/Prefabs/Bosses/Boss_Gravewarden.pfb";

        protected override string modelName => "Skeleton_Warrior";

        protected override float riseSeconds => 2.6f;

        protected override string riseBanner => "The Gravewarden awakens";

        protected override Color bodyTint => BONE_TINT;

        protected override Color glowColor => GOLD_GLOW;

        protected override Color propTint => GOLD_TRIM;

        protected override void TickFight(float dt)
        {
            chargeCooldown -= dt;
            if (move == Maneuver.None)
            {
                guardTimer -= dt;
                gap -= dt;
            }
            if (!hasHero)
            {
                move = Maneuver.None;
                Idle(dt);
                return;
            }
            if (!isWrathful && healthFraction <= WRATH_SHARE)
            {
                isWrathful = true;
                Begin(Maneuver.Wrath);
            }
            switch (move)
            {
                case Maneuver.Charge:
                    TickCharge(dt);
                    return;
                case Maneuver.Cleave:
                    TickCleave(dt);
                    return;
                case Maneuver.Guard:
                    TickGuard(dt);
                    return;
                case Maneuver.Stomp:
                    TickStomp(dt);
                    return;
                case Maneuver.Wrath:
                    TickWrath(dt);
                    return;
            }
            float distance = heroDistance;
            hugTime = distance <= radius + HUG_REACH ? hugTime + dt : 0.0f;
            if (hugTime >= HUG_SECONDS)
            {
                Begin(Maneuver.Stomp);
                return;
            }
            if (guardTimer <= 0.0f)
            {
                Begin(Maneuver.Guard);
                return;
            }
            if (gap <= 0.0f && distance <= CLEAVE_TRIGGER)
            {
                Begin(Maneuver.Cleave);
                return;
            }
            if (gap <= 0.0f && chargeCooldown <= 0.0f && distance >= CHARGE_MIN_RANGE && distance <= CHARGE_MAX_RANGE)
            {
                Begin(Maneuver.Charge);
                return;
            }
            WalkTowards(heroPosition, STOP_DISTANCE, isWrathful ? WALK_SPEED * WRATH_SPEED : WALK_SPEED, TURN_RATE, dt);
        }

        /// <summary>The gap after an attack: shorter in its Wrath and when enraged.</summary>
        private float NextGap(float seconds) => ScaleGap(isWrathful ? seconds * WRATH_GAP : seconds);

        protected override void OnDamaged(float amount)
        {
            if (move != Maneuver.Guard || !IsHeroInFront() || time - lastTink < GUARD_TINK_INTERVAL)
            {
                return;
            }
            lastTink = time;
            fx.Burst(fx.guardSpark, GetChest(radius), 6);
            run.sounds.PlayAt("guard_tink", position);
        }

        private void Begin(Maneuver next)
        {
            move = next;
            moveTime = 0.0f;
            stung = false;
            struck = false;
            aim = toHero;
            speed = 0.0f;
            switch (next)
            {
                case Maneuver.Charge:
                    chargesLeft = isWrathful ? 1 : 0;
                    StartCharge(CHARGE_WINDUP);
                    break;
                case Maneuver.Cleave:
                    backswingsLeft = isWrathful ? 1 : 0;
                    StartCleave(CLEAVE_WINDUP);
                    break;
                case Maneuver.Stomp:
                    hugTime = 0.0f;
                    animator.PlayToImpact("Attack", STOMP_WINDUP, 0.1f);
                    fx.Circle(position, STOMP_RADIUS, STOMP_WINDUP);
                    run.sounds.PlayAt("warden_cleave_sting", position);
                    break;
                case Maneuver.Wrath:
                    animator.Play("Block", 0.1f);
                    fx.Circle(position, WRATH_RADIUS, WRATH_WAVE_AT);
                    run.sounds.PlayAt("king_roar", position);
                    run.ui.ShowBanner("The Warden's wrath breaks out", WRATH_BANNER_SECONDS, true);
                    break;
                case Maneuver.Guard:
                    guardGlowTimer = 0.0f;
                    animator.Play("Block", 0.2f);
                    run.sounds.PlayAt("warden_guard_up", position);
                    break;
            }
        }

        private void End(float nextGap)
        {
            move = Maneuver.None;
            gap = NextGap(nextGap);
            animator.Walk(0.0f);
        }

        /// <summary>A charge's wind-up: the lane fills while the warden braces behind its shield.</summary>
        private void StartCharge(float seconds)
        {
            windup = seconds;
            moveTime = 0.0f;
            charging = false;
            chargeHit = false;
            charged = 0.0f;
            aim = toHero;
            animator.Play("Block", 0.15f);
            fx.Lane(position, aim, CHARGE_LENGTH, seconds);
        }

        /// <summary>A cleave's wind-up: the cone fills, then the blade comes down.</summary>
        private void StartCleave(float seconds)
        {
            windup = seconds;
            moveTime = 0.0f;
            stung = false;
            struck = false;
            aim = toHero;
            animator.PlayToImpact("Attack", seconds, 0.1f);
            fx.Cone(position, aim, CLEAVE_RANGE, seconds);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Shield Charge: a lane fills, the warden dashes down it shoving the hero aside, then reels dazed.
        // -------------------------------------------------------------------------------------------------------------

        private void TickCharge(float dt)
        {
            moveTime += dt;
            if (moveTime < windup)
            {
                TurnTowards(aim, AIM_TURN_RATE, dt);
                PlaySting(ref stung, moveTime, "warden_charge_sting");
                return;
            }
            if (!charging && charged < CHARGE_LENGTH)
            {
                charging = true;
                yaw = BossGeometry.ToYawRadians(aim);
                // The shield stays up in the run cycle's arms; the legs sell the 12 m/s.
                animator.Walk(CHARGE_SPEED);
                run.sounds.PlayAt("warden_charge_go", position);
            }
            if (charging)
            {
                StepCharge(dt);
                return;
            }
            // Dazed after the charge: open to punishment, glowing.
            vulnerability = DAZED_DAMAGE_TAKEN;
            glowBoost = RECOVERY_GLOW;
            Stand(dt);
            if (moveTime >= dazedUntil)
            {
                End(GAP_SECONDS);
                chargeCooldown = CHARGE_COOLDOWN;
            }
        }

        private void StepCharge(float dt)
        {
            float stride = MathF.Min(CHARGE_SPEED * speedFactor * dt, CHARGE_LENGTH - charged);
            Vector3 before = position;
            float moved = Move(aim * stride);
            charged += stride;
            dustTimer -= dt;
            if (dustTimer <= 0.0f)
            {
                dustTimer = CHARGE_DUST_INTERVAL;
                fx.Burst(fx.slamDust, position, 1, Color.white, radius * 2.0f, 0.5f);
            }
            // The drawn lane is the promise: its half width, not the body, decides the hit.
            if (!chargeHit && hasHero && BossGeometry.SegmentDistance(heroPosition, before, position) <= CHARGE_HALF_WIDTH + heroGrace)
            {
                chargeHit = true;
                if (HitHero(CHARGE_DAMAGE, position))
                {
                    // Shoved forward and out of the lane.
                    float side = BossGeometry.GetSide(heroPosition, before, aim);
                    var lateral = new Vector3(aim.z, 0.0f, -aim.x) * side;
                    PushHero(aim * 0.6f + lateral * 0.8f, CHARGE_KNOCKBACK);
                    run.sounds.PlayAt("warden_charge_hit", heroPosition);
                    run.feedback.Shake(0.35f, 0.3f);
                }
            }
            bool isBlocked = stride > 1e-4f && moved < stride * CHARGE_BLOCKED_SHARE;
            if (charged < CHARGE_LENGTH && !isBlocked)
            {
                return;
            }
            // The charge ends: the shield slams down; a wrathful warden turns and comes again, else it reels.
            charging = false;
            charged = CHARGE_LENGTH;
            speed = 0.0f;
            if (chargesLeft > 0)
            {
                chargesLeft--;
                run.sounds.PlayAt("warden_charge_stop", position);
                StartCharge(SECOND_CHARGE_WINDUP);
                return;
            }
            dazedUntil = moveTime + CHARGE_RECOVERY;
            fx.Burst(fx.slamDust, position + aim * radius, 1, Color.white, radius * 3.0f, 0.7f);
            fx.Burst(fx.dazed, position + new Vector3(0.0f, HEAD_HEIGHT * scale, 0.0f), 1, Color.white, scale * 0.6f, CHARGE_RECOVERY);
            run.sounds.PlayAt("warden_charge_stop", position);
            run.sounds.PlayAt("boss_dazed", position);
            run.feedback.Shake(0.3f, 0.3f);
            animator.Play("Hit", 0.1f);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Overhead Cleave: a cone fills over the wind-up, then the blade comes down on it.
        // -------------------------------------------------------------------------------------------------------------

        private void TickCleave(float dt)
        {
            moveTime += dt;
            if (!struck)
            {
                TurnTowards(aim, AIM_TURN_RATE, dt);
                PlaySting(ref stung, moveTime, "warden_cleave_sting");
                if (moveTime < windup)
                {
                    return;
                }
                struck = true;
                animator.SetPlayback(1.0f);
                fx.Oriented(fx.enemySwing, position + new Vector3(0.0f, scale * 0.6f, 0.0f), aim, Color.white, CLEAVE_RANGE * 2.0f, 0.2f);
                fx.Burst(fx.slamDust, position + aim * (CLEAVE_RANGE * 0.75f), 1, Color.white, 3.0f, 0.6f);
                run.sounds.PlayAt("warden_cleave_hit", position);
                run.feedback.Shake(0.3f, 0.25f);
                if (BossGeometry.InSector(heroPosition, position, aim, CLEAVE_RANGE, CLEAVE_HALF_ANGLE, heroGrace))
                {
                    HitHero(CLEAVE_DAMAGE, position);
                }
            }
            glowBoost = RECOVERY_GLOW;
            if (backswingsLeft > 0 && moveTime >= windup + BACKSWING_WINDUP)
            {
                backswingsLeft--;
                StartCleave(BACKSWING_WINDUP);
                return;
            }
            if (moveTime >= windup + CLEAVE_RECOVERY)
            {
                End(GAP_SECONDS);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Grave Stomp: a hero at its feet gets a ring that fills, then is thrown out; the warden stands open after it.
        // -------------------------------------------------------------------------------------------------------------

        private void TickStomp(float dt)
        {
            moveTime += dt;
            Stand(dt);
            if (!struck)
            {
                if (moveTime < STOMP_WINDUP)
                {
                    return;
                }
                struck = true;
                animator.SetPlayback(1.0f);
                fx.Burst(fx.slamDust, position, 1, Color.white, STOMP_RADIUS * 2.0f, 0.7f);
                fx.Burst(fx.phaseShockwave, new Vector3(position.x, Tuning.TELEGRAPH_HEIGHT, position.z), 1, Color.white, STOMP_RADIUS * 2.0f, 0.4f);
                run.sounds.PlayAt("warden_cleave_hit", position);
                run.feedback.Shake(0.4f, 0.3f);
                if (BossGeometry.InCircle(heroPosition, position, STOMP_RADIUS, heroGrace) && HitHero(STOMP_DAMAGE, position))
                {
                    KnockHeroFrom(position, STOMP_THROW);
                }
            }
            vulnerability = STOMP_DAMAGE_TAKEN;
            glowBoost = RECOVERY_GLOW;
            if (moveTime >= STOMP_WINDUP + STOMP_RECOVERY)
            {
                End(GAP_SECONDS);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Wrath: at half health, untouchable for a moment: a shockwave throws the hero back and Shieldbearers rise.
        // -------------------------------------------------------------------------------------------------------------

        private void TickWrath(float dt)
        {
            moveTime += dt;
            Stand(dt);
            isInvulnerable = true;
            glowBoost = RECOVERY_GLOW;
            if (!struck && moveTime >= WRATH_WAVE_AT)
            {
                struck = true;
                fx.Burst(fx.phaseShockwave, new Vector3(position.x, Tuning.TELEGRAPH_HEIGHT, position.z), 1, GOLD_GLOW, WRATH_RADIUS * 2.0f, 0.5f);
                run.sounds.PlayAt("phase_shockwave", position);
                run.feedback.Shake(0.6f, 0.5f);
                if (BossGeometry.InCircle(heroPosition, position, WRATH_RADIUS, heroGrace) && HitHero(WRATH_DAMAGE, position))
                {
                    KnockHeroFrom(position, WRATH_THROW);
                }
                for (int i = 0; i < WRATH_GUARDS; i++)
                {
                    SpawnAdd(Archetype.Shieldbearer, position + Ground.FromYaw(i * FULL_TURN_DEGREES / WRATH_GUARDS) * WRATH_GUARD_RING);
                }
                run.sounds.PlayAt("summon", position);
            }
            if (moveTime >= WRATH_SECONDS)
            {
                End(GAP_SECONDS);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Bone Guard: behind the shield, hits from the front half barely hurt; the hero must flank.
        // -------------------------------------------------------------------------------------------------------------

        private void TickGuard(float dt)
        {
            moveTime += dt;
            Stand(dt);
            TurnTowards(toHero, GUARD_TURN_RATE * speedFactor, dt);
            // The hit direction is not known, so the hero's side stands in for it: hits from the front are shielded.
            vulnerability = IsHeroInFront() ? GUARD_DAMAGE_TAKEN : 1.0f;
            guardGlowTimer -= dt;
            if (guardGlowTimer <= 0.0f)
            {
                guardGlowTimer = GUARD_GLOW_INTERVAL;
                fx.Burst(fx.guardGlow, GetChest(radius), 1, GUARD_COLOR, scale * 1.0f, GUARD_GLOW_INTERVAL * 1.5f);
            }
            if (moveTime >= GUARD_SECONDS)
            {
                guardTimer = GUARD_INTERVAL;
                End(GAP_SECONDS * 0.5f);
            }
        }

        private bool IsHeroInFront() => Vector3.Dot(forward, toHero) >= 0.0f;
    }
}

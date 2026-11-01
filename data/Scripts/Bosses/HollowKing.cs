using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The Hollow King, the 14:00 boss: a 7.5 m skeleton king with a great axe and a green core. Its health grows with
    /// the hero's level. Three phases (100-65-30-0% health) add attacks, shorten wind-ups (telegraph scale 1.0 / 0.9 /
    /// 0.8) and gaps (1.6 / 1.2 / 0.9 s) and speed its walk (2.6 / 3.0 / 3.4 m/s). Between phases it kneels
    /// invulnerable for 2.5 s, roars, and a shockwave pushes the hero 4 m and kills every add within 10 m. In phase 2 its
    /// Cleave Sweep comes back as a reverse swing (phase 3: twice) and Soul Chains drag the hero in while a ring around
    /// the King fills: run against them or take the blast. In phase 3 the ward closes in: the last stand is fought close.
    /// 180 s into the fight it enrages: every attack, wind-ups x0.6, violet decals, an extra Bone Rain every 5 s. Attacks
    /// are picked by weight, never the same one twice in a row; every big wind-up has its own audio sting 0.2 s in;
    /// recoveries make the core glow brighter.
    /// </summary>
    public sealed class HollowKing : Boss
    {
        /// <summary>Health: this much per hero level, clamped between MIN_HEALTH and MAX_HEALTH.</summary>
        private const float HEALTH_PER_LEVEL = 2200.0f;
        private const float MIN_HEALTH = 55000.0f;
        private const float MAX_HEALTH = 100000.0f;
        /// <summary>The gold shower of his fall.</summary>
        private const int GOLD = 200;
        /// <summary>The camera pulls out 15% for his size and reach.</summary>
        private const float CAMERA_ZOOM = 1.15f;
        private const float RISE_BANNER_SECONDS = 3.0f;
        private const float RISE_SHAKE = 0.45f;
        /// <summary>Seconds of fighting before the hard enrage.</summary>
        private const float ENRAGE_SECONDS = 180.0f;
        private const float ARENA_RADIUS = 24.0f;
        /// <summary>Phase 3: the ward closes in to this radius over this long.</summary>
        private const float LAST_STAND_RADIUS = 14.0f;
        private const float LAST_STAND_SECONDS = 25.0f;
        private const float PHASE_TWO_SHARE = 0.65f;
        private const float PHASE_THREE_SHARE = 0.3f;
        private static readonly float[] PHASE_MARKS = { PHASE_TWO_SHARE, PHASE_THREE_SHARE };
        private static readonly float[] WALK_SPEEDS = { 2.6f, 3.0f, 3.4f };
        private static readonly float[] GAPS = { 1.6f, 1.2f, 0.9f };
        private static readonly float[] TELEGRAPH_SCALES = { 1.0f, 0.9f, 0.8f };
        private const float ENRAGE_TELEGRAPH_SCALE = 0.6f;
        private const float TURN_RATE = 110.0f;
        private const float AIM_TURN_RATE = 540.0f;
        private const float STOP_DISTANCE = 4.0f;
        private const float FIRST_ATTACK_DELAY = 1.0f;

        private const float CONTACT_DAMAGE = 30.0f;

        private const float CLEAVE_WINDUP = 1.0f;
        private const float CLEAVE_RADIUS = 6.5f;
        private const float CLEAVE_HALF_ANGLE = 100.0f;
        private const float CLEAVE_DAMAGE = 75.0f;
        /// <summary>A reverse swing's wind-up as a share of the first swing's, after this share of its recovery.</summary>
        private const float COMBO_WINDUP_SHARE = 0.65f;
        private const float COMBO_RECOVERY_SHARE = 0.4f;
        private const float CLEAVE_KNOCKBACK = 4.0f;
        private const float CLEAVE_RECOVERY = 1.2f;

        private const float SLAM_WINDUP = 1.2f;
        private const float SLAM_LENGTH = 14.0f;
        private const float SLAM_HALF_WIDTH = 1.5f;
        /// <summary>Spikes erupt at 0.04 s per metre: 25 m/s.</summary>
        private const float SLAM_ERUPTION_SPEED = 25.0f;
        private const float SLAM_DAMAGE = 90.0f;
        private const float SLAM_RECOVERY = 1.4f;
        private const float SLAM_STUCK_DAMAGE_TAKEN = 1.5f;
        private const float SLAM_IMPACT_OFFSET = 3.0f;
        private const float SLAM_HOLD_AFTER = 0.25f;
        private const float SLAM_PULL_BEFORE_END = 0.35f;

        private const float RAIN_WINDUP = 1.4f;
        private const int RAIN_CIRCLES = 10;
        private const int RAIN_CIRCLES_LATE = 16;
        private const int RAIN_ON_HERO = 3;
        private const float RAIN_HERO_INTERVAL = 0.3f;
        private const float RAIN_RADIUS = 1.8f;
        private const float RAIN_SPREAD = 9.0f;
        private const float RAIN_DAMAGE = 50.0f;
        private const float RAIN_CAST = 0.65f;
        private const float RAIN_RECOVERY = 0.6f;
        private const float EXTRA_RAIN_INTERVAL = 5.0f;
        private const int EXTRA_RAIN_RANDOM = 3;

        private const float RAISE_CHANNEL = 2.0f;
        private const float RAISE_RECOVERY = 0.5f;
        private const float RAISE_DAMAGE_TAKEN = 1.5f;
        private const float RAISE_GLOW = 2.5f;
        private const int RAISE_SHAMBLERS = 24;
        private const int RAISE_SHIELDBEARERS = 6;
        private const int RAISE_CINDER_SKULLS = 8;
        private const float RAISE_INNER_RING = 10.0f;
        private const float RAISE_OUTER_RING = 11.5f;
        private const float RAISE_ARENA_MARGIN = 2.0f;
        private const float FULL_TURN_DEGREES = 360.0f;
        private const float RAISE_MOTES_INTERVAL = 0.5f;
        private const int RAISE_MOTES_PER_POINT = 3;

        private const float SPIRAL_WINDUP = 0.8f;
        private const float SPIRAL_SECONDS = 4.0f;
        private const float SPIRAL_RADIUS = 3.5f;
        private const float SPIRAL_DAMAGE = 30.0f;
        private const float SPIRAL_INTERVAL = 0.5f;
        private const float SPIRAL_SPEED = 3.2f;
        private const float SPIRAL_SPEED_LATE = 3.8f;
        private const float SPIRAL_SPIN_UP_PLAYBACK = 0.35f;
        private const float SPIRAL_SPIN_PLAYBACK = 1.3f;
        private const float SPIRAL_RECOVERY = 1.5f;
        private const float SPIRAL_DIZZY_DAMAGE_TAKEN = 1.5f;

        /// <summary>Soul Nova wind-up at telegraph scale 1 (1.0 s in phase 3, where it starts).</summary>
        private const float NOVA_WINDUP = 1.25f;
        private const int NOVA_RINGS = 3;
        private const float NOVA_SPEED = 6.0f;
        private const float NOVA_RING_INTERVAL = 0.7f;
        private const float NOVA_HALF_THICKNESS = 0.6f;
        private const float NOVA_MAX_RADIUS = 20.0f;
        private const float NOVA_GAP_HALF_ANGLE = 30.0f;
        private const float NOVA_GAP_TURN = 30.0f;
        private const float NOVA_DAMAGE = 55.0f;
        private const float NOVA_RECOVERY = 1.0f;
        private static readonly float[] NOVA_WARNING_RADII = { 4.0f, 5.5f, 7.0f };

        private const float CHAINS_WINDUP = 2.4f;
        private const float CHAINS_RADIUS = 7.0f;
        private const float CHAINS_PULL = 2.6f;
        private const float CHAINS_DAMAGE = 85.0f;
        private const float CHAINS_RECOVERY = 1.0f;
        private const float CHAINS_DAMAGE_TAKEN = 1.3f;
        private const float CHAIN_LINK_INTERVAL = 0.06f;
        private const float CHAIN_LINK_SPACING = 1.1f;
        private const float CHAIN_LINK_SIZE = 0.5f;
        private const int CHAIN_LINKS_MAX = 24;

        private const float TRANSITION_SECONDS = 2.5f;
        private const float TRANSITION_ROAR_AT = 0.9f;
        private const float TRANSITION_WAVE_AT = 1.5f;
        private const float SHOCKWAVE_REACH = 14.0f;
        private const float SHOCKWAVE_PUSH = 4.0f;
        private const float SHOCKWAVE_DAMAGE = 30.0f;
        private const float SHOCKWAVE_ADD_RADIUS = 10.0f;
        private const float SHOCKWAVE_SIZE = 28.0f;
        private const float TRANSITION_GAP = 0.8f;
        /// <summary>The bone pop of an add the shockwave kills: its height (m) and shard count.</summary>
        private const float POP_HEIGHT = 0.8f;
        private const int POP_SHARDS = 6;

        private const float CORE_SIZE_PER_SCALE = 0.35f;
        private const float CORE_FORWARD_PER_SCALE = 0.12f;
        private const float LATE_PHASE_GLOW = 1.3f;
        private const float CRACK_PULSE_INTERVAL = 1.6f;
        private const float CRACK_PULSE_SIZE = 12.0f;
        private const int SCRATCH = 1024;

        private static readonly Color VOID_TINT = new Color(0.36f, 0.27f, 0.47f, 1.0f);
        private static readonly Color CORE_GREEN = new Color(0.3f, 1.0f, 0.45f, 1.0f);
        private static readonly Color RAISE_GREEN = new Color(0.45f, 1.0f, 0.3f, 1.0f);

        private enum Attack
        {
            None = -1,
            CleaveSweep,
            GraveSlam,
            BoneRain,
            RaiseLegion,
            DeathSpiral,
            SoulNova,
            SoulChains,
            Count,
        }

        /// <summary>When and how often an attack comes.</summary>
        private readonly struct AttackRule
        {
            /// <summary>How often it is picked among the available attacks.</summary>
            public readonly float weight;
            /// <summary>The phase that unlocks it (the enrage unlocks everything).</summary>
            public readonly int firstPhase;
            /// <summary>Seconds before it can come again.</summary>
            public readonly float cooldown;
            /// <summary>Farthest hero (center distance) it starts at.</summary>
            public readonly float maxRange;

            public AttackRule(float weight, int firstPhase, float cooldown, float maxRange)
            {
                this.weight = weight;
                this.firstPhase = firstPhase;
                this.cooldown = cooldown;
                this.maxRange = maxRange;
            }
        }

        /// <summary>The rules by <see cref="Attack"/>.</summary>
        private static readonly AttackRule[] ATTACKS =
        {
            new AttackRule(3.0f, 1, 0.0f, 7.5f),
            new AttackRule(3.0f, 1, 0.0f, 13.0f),
            new AttackRule(2.0f, 1, 0.0f, 22.0f),
            new AttackRule(2.0f, 2, 20.0f, 40.0f),
            new AttackRule(2.0f, 2, 0.0f, 12.0f),
            new AttackRule(2.0f, 3, 12.0f, 18.0f),
            new AttackRule(2.5f, 2, 14.0f, 20.0f),
        };

        private readonly float[] cooldowns = new float[(int)Attack.Count];
        private readonly Vector3[] raisePoints = new Vector3[RAISE_SHAMBLERS + RAISE_SHIELDBEARERS + RAISE_CINDER_SKULLS];
        private readonly Archetype[] raiseKinds = new Archetype[RAISE_SHAMBLERS + RAISE_SHIELDBEARERS + RAISE_CINDER_SKULLS];
        private readonly Vector3[] novaGaps = new Vector3[NOVA_RINGS];
        private readonly int[] scratch = new int[SCRATCH];
        private readonly float healthTarget;
        private readonly int chainLink;

        private int phase = 1;
        private int combosLeft;
        private float chainTimer;
        private Attack attack = Attack.None;
        private Attack lastAttack = Attack.None;
        private float attackTime;
        private float windup;
        private Vector3 aim;
        private bool stung;
        private bool struck;
        private bool released;
        private float gap = FIRST_ATTACK_DELAY;
        private int raiseCount;
        private int heroRainPlaced;
        private float followTimer;
        private float spinHitTimer;
        private float whooshTimer;
        private float motesTimer;
        private float extraRainTimer;
        private int extraRainPending;
        private float extraRainNext;
        private float crackTimer;
        private float coreTimer;
        private bool isTransitioning;
        private float transitionTime;
        private bool roared;
        private bool waved;

        public HollowKing(Run run, BossFx fx, BossHazards hazards, Vector3[] obstacles)
            : base(run, fx, hazards, BossKind.HollowKing, obstacles)
        {
            healthTarget = Math.Clamp(HEALTH_PER_LEVEL * run.level, MIN_HEALTH, MAX_HEALTH);
            chainLink = run.effects.Find("BossChainLink");
        }

        public override string title => "Lord of the Barrows";

        public override float[] phaseMarks => PHASE_MARKS;

        public override bool isMiniBoss => false;

        public override int goldReward => GOLD;

        public override string defeatId => SaveData.BOSS_HOLLOW_KING;

        public override float cameraZoom => CAMERA_ZOOM;

        public override float arenaRadius => ARENA_RADIUS;

        /// <summary>clamp(2200 x level, 55000, 100000), with the level he was summoned at.</summary>
        protected override float maxHealth => healthTarget;

        protected override float contactDamage => CONTACT_DAMAGE;

        protected override float enrageSeconds => ENRAGE_SECONDS;

        protected override string prefabKey => "app:/data/Prefabs/Bosses/Boss_HollowKing.pfb";

        protected override string modelName => "Skeleton_Warrior";

        protected override float riseSeconds => 3.0f;

        protected override string riseBanner => "The Hollow King rises";

        protected override float riseBannerSeconds => RISE_BANNER_SECONDS;

        protected override string riseSound => "king_rise";

        protected override float riseShake => RISE_SHAKE;

        protected override Color bodyTint => VOID_TINT;

        protected override Color glowColor => CORE_GREEN;

        /// <summary>The body lies until the victory screen covers it.</summary>
        protected override float corpseSeconds => 30.0f;

        private float telegraphScale => isEnraged ? ENRAGE_TELEGRAPH_SCALE : TELEGRAPH_SCALES[phase - 1];

        private bool isLate => phase >= 3 || isEnraged;

        protected override void TickFight(float dt)
        {
            for (int i = 0; i < cooldowns.Length; i++)
            {
                cooldowns[i] -= dt;
            }
            CheckPhase();
            TickBehaviour(dt);
            // After the behaviour: the core reads this frame's recovery glow.
            TickCore(dt);
        }

        private void TickBehaviour(float dt)
        {
            if (isTransitioning)
            {
                TickTransition(dt);
                return;
            }
            TickExtraRain(dt);
            if (!hasHero)
            {
                CancelAttack();
                Idle(dt);
                return;
            }
            if (attack == Attack.None)
            {
                gap -= dt;
                if (gap <= 0.0f && TryBeginAttack())
                {
                    TickAttack(dt);
                }
                else
                {
                    WalkTowards(heroPosition, STOP_DISTANCE, WALK_SPEEDS[phase - 1], TURN_RATE, dt);
                }
            }
            else
            {
                TickAttack(dt);
            }
        }

        /// <summary>Its spin hits on its own beat.</summary>
        protected override bool IsTouchingHero() => attack != Attack.DeathSpiral && base.IsTouchingHero();

        // -------------------------------------------------------------------------------------------------------------
        // Phases, enrage, the core
        // -------------------------------------------------------------------------------------------------------------

        private void CheckPhase()
        {
            if (isTransitioning)
            {
                return;
            }
            float health = healthFraction;
            if (phase == 1 && health <= PHASE_TWO_SHARE)
            {
                BeginTransition();
            }
            else if (phase == 2 && health <= PHASE_THREE_SHARE)
            {
                BeginTransition();
            }
        }

        protected override void OnEnraged() => extraRainTimer = EXTRA_RAIN_INTERVAL;

        /// <summary>The green core in the rib cage: a glow that pulses with the King's state, brighter in recoveries.</summary>
        private void TickCore(float dt)
        {
            if (isLate)
            {
                glowBoost = MathF.Max(glowBoost, LATE_PHASE_GLOW);
                crackTimer -= dt;
                if (crackTimer <= 0.0f)
                {
                    crackTimer = CRACK_PULSE_INTERVAL;
                    fx.Burst(fx.crackPulse, new Vector3(position.x, Tuning.TELEGRAPH_HEIGHT, position.z), 1, isEnraged ? fx.dangerColor : CORE_GREEN, CRACK_PULSE_SIZE, CRACK_PULSE_INTERVAL);
                }
            }
            coreTimer -= dt;
            if (coreTimer > 0.0f)
            {
                return;
            }
            coreTimer = FOLLOW_EFFECT_INTERVAL;
            bool isRaising = attack == Attack.RaiseLegion && !released;
            float boost = isRaising ? RAISE_GLOW : glowBoost;
            Color color = isRaising ? RAISE_GREEN : CORE_GREEN;
            fx.Burst(fx.coreGlow, GetChest(CORE_FORWARD_PER_SCALE * scale), 1, color, CORE_SIZE_PER_SCALE * scale * boost, FOLLOW_EFFECT_INTERVAL * 2.5f);
        }

        private void BeginTransition()
        {
            CancelAttack();
            isTransitioning = true;
            transitionTime = 0.0f;
            roared = false;
            waved = false;
            speed = 0.0f;
            animator.Play("Kneel", 0.08f);
            run.feedback.HitStop(0.1f, bypassGap: true);
            run.feedback.Shake(0.6f, 0.6f);
            run.sounds.PlayAt("king_phase_break", position);
            fx.Burst(fx.bossBoneBurst, GetChest(), 60);
        }

        private void TickTransition(float dt)
        {
            transitionTime += dt;
            isInvulnerable = true;
            glowBoost = RECOVERY_GLOW;
            Stand(dt);
            TurnTowards(toHero, TURN_RATE, dt);
            if (!roared && transitionTime >= TRANSITION_ROAR_AT)
            {
                roared = true;
                animator.PlayToImpact("CastRaise", TRANSITION_WAVE_AT - TRANSITION_ROAR_AT, 0.15f);
                run.sounds.PlayAt("king_roar", position);
            }
            if (!waved && transitionTime >= TRANSITION_WAVE_AT)
            {
                waved = true;
                Shockwave();
            }
            if (transitionTime < TRANSITION_SECONDS)
            {
                return;
            }
            isTransitioning = false;
            phase = Math.Min(phase + 1, 3);
            gap = TRANSITION_GAP;
            if (phase == 3)
            {
                run.director.bosses.ward.CloseTo(LAST_STAND_RADIUS, LAST_STAND_SECONDS);
                run.ui.ShowBanner("The barrow closes in", 3.0f, true);
            }
            animator.Walk(0.0f);
        }

        /// <summary>Pushes the hero 4 m (15 damage) and kills every add within 10 m: their gems are a gift.</summary>
        private void Shockwave()
        {
            fx.Burst(fx.phaseShockwave, new Vector3(position.x, Tuning.TELEGRAPH_HEIGHT, position.z), 1, Color.white, SHOCKWAVE_SIZE, 0.5f);
            run.sounds.PlayAt("phase_shockwave", position);
            run.feedback.Shake(0.6f, 0.5f);
            if (hasHero && heroDistance <= SHOCKWAVE_REACH)
            {
                HitHero(SHOCKWAVE_DAMAGE, position);
                KnockHeroFrom(position, SHOCKWAVE_PUSH);
            }
            EnemyWorld enemies = run.enemies;
            int killed = enemies.KillRegularIn(position, SHOCKWAVE_ADD_RADIUS, RunRecord.OTHER_SOURCE, scratch);
            for (int i = 0; i < killed; i++)
            {
                fx.Burst(fx.boneBurst, enemies.GetPosition(scratch[i]) + new Vector3(0.0f, POP_HEIGHT, 0.0f), POP_SHARDS);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // The enrage's extra Bone Rain
        // -------------------------------------------------------------------------------------------------------------

        private void TickExtraRain(float dt)
        {
            if (!isEnraged)
            {
                return;
            }
            extraRainTimer -= dt;
            if (extraRainTimer <= 0.0f && hasHero)
            {
                extraRainTimer = EXTRA_RAIN_INTERVAL;
                extraRainPending = RAIN_ON_HERO;
                extraRainNext = 0.0f;
                for (int i = 0; i < EXTRA_RAIN_RANDOM; i++)
                {
                    AddRainCircle(BossGeometry.RandomInDisc(run.rng, heroPosition, RAIN_SPREAD));
                }
            }
            if (extraRainPending <= 0)
            {
                return;
            }
            extraRainNext -= dt;
            if (extraRainNext <= 0.0f && hasHero)
            {
                extraRainNext = RAIN_HERO_INTERVAL;
                extraRainPending--;
                AddRainCircle(heroPosition);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Attack selection
        // -------------------------------------------------------------------------------------------------------------

        private bool IsAvailable(Attack candidate, float distance)
        {
            int index = (int)candidate;
            AttackRule rule = ATTACKS[index];
            bool isUnlocked = isEnraged || phase >= rule.firstPhase;
            return isUnlocked && candidate != lastAttack && cooldowns[index] <= 0.0f && distance <= rule.maxRange;
        }

        private bool TryBeginAttack()
        {
            float distance = heroDistance;
            Span<float> weights = stackalloc float[(int)Attack.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = IsAvailable((Attack)i, distance) ? ATTACKS[i].weight : 0.0f;
            }
            int picked = WeightedPick.Pick(run.rng, weights);
            if (picked < 0)
            {
                return false;
            }
            BeginAttack((Attack)picked);
            return true;
        }

        private void BeginAttack(Attack next)
        {
            attack = next;
            lastAttack = next;
            attackTime = 0.0f;
            stung = false;
            struck = false;
            released = false;
            aim = toHero;
            speed = 0.0f;
            cooldowns[(int)next] = ATTACKS[(int)next].cooldown;
            switch (next)
            {
                case Attack.CleaveSweep:
                    combosLeft = isEnraged ? 2 : phase - 1;
                    StartSwing(CLEAVE_WINDUP * telegraphScale);
                    break;
                case Attack.GraveSlam:
                    windup = SLAM_WINDUP * telegraphScale;
                    animator.PlayToImpact("HeavyChop", windup, 0.12f);
                    fx.Lane(position, aim, SLAM_LENGTH, windup);
                    break;
                case Attack.BoneRain:
                    windup = RAIN_WINDUP * telegraphScale;
                    heroRainPlaced = 0;
                    animator.Play("CastRaise", 0.15f);
                    int count = isLate ? RAIN_CIRCLES_LATE : RAIN_CIRCLES;
                    for (int i = 0; i < count - RAIN_ON_HERO; i++)
                    {
                        AddRainCircle(BossGeometry.RandomInDisc(run.rng, heroPosition, RAIN_SPREAD));
                    }
                    break;
                case Attack.RaiseLegion:
                    windup = RAISE_CHANNEL;
                    motesTimer = 0.0f;
                    PlanRaise();
                    animator.PlayToImpact("Summon", RAISE_CHANNEL, 0.2f);
                    SetGlowColor(RAISE_GREEN);
                    break;
                case Attack.DeathSpiral:
                    windup = SPIRAL_WINDUP * telegraphScale;
                    animator.Play("Spin", 0.15f, SPIRAL_SPIN_UP_PLAYBACK);
                    fx.Circle(position, SPIRAL_RADIUS, windup);
                    break;
                case Attack.SoulChains:
                    windup = CHAINS_WINDUP * telegraphScale;
                    chainTimer = 0.0f;
                    animator.PlayToImpact("CastRaise", windup, 0.15f);
                    fx.Circle(position, CHAINS_RADIUS, windup);
                    run.sounds.PlayAt("king_raise_sting", position);
                    break;
                case Attack.SoulNova:
                    windup = NOVA_WINDUP * telegraphScale;
                    animator.PlayToImpact("CastRaise", windup, 0.15f);
                    float baseTurn = (float)run.rng.NextDouble() * 180.0f;
                    for (int i = 0; i < NOVA_RINGS; i++)
                    {
                        novaGaps[i] = BossGeometry.Rotate(aim, baseTurn + i * NOVA_GAP_TURN);
                        fx.NovaWarning(position, NOVA_WARNING_RADII[i], novaGaps[i], windup);
                    }
                    break;
            }
        }

        private void EndAttack(float extraGap)
        {
            attack = Attack.None;
            gap = ScaleGap(GAPS[phase - 1]) + extraGap;
            animator.Walk(0.0f);
        }

        /// <summary>One sweep of the Cleave: the arc fills over the wind-up.</summary>
        private void StartSwing(float seconds)
        {
            windup = seconds;
            attackTime = 0.0f;
            stung = false;
            struck = false;
            aim = toHero;
            animator.PlayToImpact("HeavySlice", windup, 0.12f);
            fx.Arc(position, aim, CLEAVE_RADIUS, windup);
        }

        /// <summary>Drops the attack in progress (phase change, hero gone); hazards already on the ground still land.</summary>
        private void CancelAttack()
        {
            if (attack == Attack.RaiseLegion)
            {
                SetGlowColor(CORE_GREEN);
            }
            attack = Attack.None;
            animator.SetPlayback(1.0f);
        }

        private void TickAttack(float dt)
        {
            attackTime += dt;
            switch (attack)
            {
                case Attack.CleaveSweep:
                    TickCleave(dt);
                    break;
                case Attack.GraveSlam:
                    TickSlam(dt);
                    break;
                case Attack.BoneRain:
                    TickRain(dt);
                    break;
                case Attack.RaiseLegion:
                    TickRaise(dt);
                    break;
                case Attack.DeathSpiral:
                    TickSpiral(dt);
                    break;
                case Attack.SoulNova:
                    TickNova(dt);
                    break;
                case Attack.SoulChains:
                    TickChains(dt);
                    break;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Cleave Sweep: a wide arc in front of the King, then a knockback swing.
        // -------------------------------------------------------------------------------------------------------------

        private void TickCleave(float dt)
        {
            Stand(dt);
            if (!struck)
            {
                TurnTowards(aim, AIM_TURN_RATE, dt);
                PlaySting(ref stung, attackTime, "king_cleave_sting");
                if (attackTime < windup)
                {
                    return;
                }
                struck = true;
                animator.SetPlayback(1.0f);
                fx.Oriented(fx.cleaveTrail, position + new Vector3(0.0f, CHEST_HEIGHT * scale * 0.5f, 0.0f), aim, Color.white, CLEAVE_RADIUS * 2.0f, 0.3f);
                fx.Burst(fx.slamDust, position + aim * (CLEAVE_RADIUS * 0.6f), 1, Color.white, CLEAVE_RADIUS, 0.8f);
                run.sounds.PlayAt("king_cleave_hit", position);
                run.feedback.Shake(0.45f, 0.35f);
                if (BossGeometry.InSector(heroPosition, position, aim, CLEAVE_RADIUS, CLEAVE_HALF_ANGLE, heroGrace)
                    && HitHero(CLEAVE_DAMAGE, position))
                {
                    KnockHeroFrom(position, CLEAVE_KNOCKBACK);
                }
            }
            glowBoost = RECOVERY_GLOW;
            if (combosLeft > 0 && attackTime >= windup + CLEAVE_RECOVERY * COMBO_RECOVERY_SHARE)
            {
                combosLeft--;
                StartSwing(CLEAVE_WINDUP * telegraphScale * COMBO_WINDUP_SHARE);
                return;
            }
            if (attackTime >= windup + CLEAVE_RECOVERY)
            {
                EndAttack(0.0f);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Soul Chains: green chains drag the hero toward the King while a ring around him fills; whoever is inside when
        // it ends takes the blast. He stands open for a moment after it.
        // -------------------------------------------------------------------------------------------------------------

        private void TickChains(float dt)
        {
            Stand(dt);
            TurnTowards(toHero, TURN_RATE, dt);
            if (attackTime < windup)
            {
                if (hasHero)
                {
                    run.hero.AddPull(-toHero * (CHAINS_PULL * speedFactor));
                    DrawChain(dt);
                }
                return;
            }
            if (!struck)
            {
                struck = true;
                animator.SetPlayback(1.0f);
                fx.Burst(fx.phaseShockwave, new Vector3(position.x, Tuning.TELEGRAPH_HEIGHT, position.z), 1, RAISE_GREEN, CHAINS_RADIUS * 2.0f, 0.45f);
                fx.Burst(fx.slamDust, position, 1, Color.white, CHAINS_RADIUS * 1.6f, 0.8f);
                run.sounds.PlayAt("phase_shockwave", position);
                run.feedback.Shake(0.55f, 0.45f);
                if (BossGeometry.InCircle(heroPosition, position, CHAINS_RADIUS, heroGrace))
                {
                    HitHero(CHAINS_DAMAGE, position);
                }
            }
            vulnerability = CHAINS_DAMAGE_TAKEN;
            glowBoost = RECOVERY_GLOW;
            if (attackTime >= windup + CHAINS_RECOVERY)
            {
                EndAttack(0.0f);
            }
        }

        /// <summary>Glowing links from the King's chest to the hero, re-drawn on an interval.</summary>
        private void DrawChain(float dt)
        {
            chainTimer -= dt;
            if (chainTimer > 0.0f)
            {
                return;
            }
            chainTimer = CHAIN_LINK_INTERVAL;
            Vector3 from = GetChest();
            Vector3 to = heroPosition + new Vector3(0.0f, CHEST_HEIGHT, 0.0f);
            int links = Math.Clamp((int)((to - from).magnitude / CHAIN_LINK_SPACING), 1, CHAIN_LINKS_MAX);
            for (int i = 0; i <= links; i++)
            {
                run.effects.EmitSized(chainLink, Vector3.Lerp(from, to, (float)i / links), 1, RAISE_GREEN, CHAIN_LINK_SIZE);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Grave Slam: a lane, then spikes erupting along it; the axe sticks in the ground and leaves him open.
        // -------------------------------------------------------------------------------------------------------------

        private void TickSlam(float dt)
        {
            Stand(dt);
            if (!struck)
            {
                TurnTowards(aim, AIM_TURN_RATE, dt);
                PlaySting(ref stung, attackTime, "king_slam_sting");
                if (attackTime < windup)
                {
                    return;
                }
                struck = true;
                animator.SetPlayback(1.0f);
                Vector3 impact = position + aim * SLAM_IMPACT_OFFSET;
                fx.Burst(fx.slamDust, impact, 1, Color.white, 7.0f, 1.0f);
                run.sounds.PlayAt("king_slam_hit", impact);
                run.feedback.Shake(0.6f, 0.45f);
                hazards.AddLine(position, aim, SLAM_LENGTH, SLAM_HALF_WIDTH, SLAM_ERUPTION_SPEED, SLAM_DAMAGE * damageScale, displayName);
            }
            float recovery = attackTime - windup;
            // The axe bites into the ground: the pose holds, then the King wrenches it free.
            animator.SetPlayback(recovery >= SLAM_HOLD_AFTER && recovery < SLAM_RECOVERY - SLAM_PULL_BEFORE_END ? 0.0f : 1.0f);
            vulnerability = SLAM_STUCK_DAMAGE_TAKEN;
            glowBoost = RECOVERY_GLOW;
            if (recovery >= SLAM_RECOVERY)
            {
                animator.SetPlayback(1.0f);
                EndAttack(0.0f);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Bone Rain: circles around the hero and a few on it one after another; bones fall on each.
        // -------------------------------------------------------------------------------------------------------------

        private void TickRain(float dt)
        {
            Stand(dt);
            TurnTowards(toHero, TURN_RATE, dt);
            PlaySting(ref stung, attackTime, "king_rain_sting");
            while (heroRainPlaced < RAIN_ON_HERO && attackTime >= heroRainPlaced * RAIN_HERO_INTERVAL)
            {
                heroRainPlaced++;
                AddRainCircle(heroPosition);
            }
            if (attackTime >= RAIN_CAST)
            {
                // The King walks again while the bones fall.
                EndAttack(RAIN_RECOVERY);
            }
        }

        private void AddRainCircle(Vector3 center)
        {
            float fill = RAIN_WINDUP * telegraphScale;
            Vector3 point = ClampToArena(center, RAIN_RADIUS);
            fx.Circle(point, RAIN_RADIUS, fill);
            hazards.AddCircle(point, RAIN_RADIUS, fill, RAIN_DAMAGE * damageScale, displayName, fx.boneRainImpact, Color.white, "bone_rain_impact", true);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Raise the Legion: a channel that leaves him open (green core), then the dead rise on two rings around the
        // hero (no experience).
        // -------------------------------------------------------------------------------------------------------------

        private void PlanRaise()
        {
            Vector3 center = heroPosition;
            raiseCount = 0;
            float turn = (float)run.rng.NextDouble() * 360.0f;
            AddRing(center, RAISE_INNER_RING, RAISE_SHAMBLERS, Archetype.Shambler, turn);
            AddRing(center, RAISE_OUTER_RING, RAISE_SHIELDBEARERS, Archetype.Shieldbearer, turn + 15.0f);
            if (isLate)
            {
                AddRing(center, RAISE_OUTER_RING, RAISE_CINDER_SKULLS, Archetype.CinderSkull, turn + 37.5f);
            }
        }

        private void AddRing(Vector3 center, float ringRadius, int count, Archetype archetype, float turnDegrees)
        {
            for (int i = 0; i < count && raiseCount < raisePoints.Length; i++)
            {
                float angle = turnDegrees + i * FULL_TURN_DEGREES / count;
                raisePoints[raiseCount] = ClampToArena(center + Ground.FromYaw(angle) * ringRadius, RAISE_ARENA_MARGIN);
                raiseKinds[raiseCount] = archetype;
                raiseCount++;
            }
        }

        private void TickRaise(float dt)
        {
            Stand(dt);
            PlaySting(ref stung, attackTime, "king_raise_sting");
            if (attackTime < RAISE_CHANNEL)
            {
                vulnerability = RAISE_DAMAGE_TAKEN;
                motesTimer -= dt;
                if (motesTimer <= 0.0f)
                {
                    motesTimer = RAISE_MOTES_INTERVAL;
                    for (int i = 0; i < raiseCount; i++)
                    {
                        fx.Burst(fx.summonMotes, raisePoints[i], RAISE_MOTES_PER_POINT);
                    }
                }
                return;
            }
            if (!released)
            {
                released = true;
                SetGlowColor(CORE_GREEN);
                for (int i = 0; i < raiseCount; i++)
                {
                    SpawnAdd(raiseKinds[i], raisePoints[i]);
                }
                run.sounds.PlayAt("summon", position);
                run.feedback.Shake(0.3f, 0.4f);
            }
            glowBoost = RECOVERY_GLOW;
            if (attackTime >= RAISE_CHANNEL + RAISE_RECOVERY)
            {
                EndAttack(0.0f);
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Death Spiral: he spins after the hero inside a danger ring that hurts on a beat, then stands dizzy and open.
        // -------------------------------------------------------------------------------------------------------------

        private void TickSpiral(float dt)
        {
            PlaySting(ref stung, attackTime, "king_spin_sting");
            if (attackTime < windup)
            {
                Stand(dt);
                return;
            }
            float spinTime = attackTime - windup;
            if (spinTime < SPIRAL_SECONDS)
            {
                if (!struck)
                {
                    struck = true;
                    spinHitTimer = 0.0f;
                    followTimer = 0.0f;
                    whooshTimer = 0.0f;
                    animator.SetPlayback(SPIRAL_SPIN_PLAYBACK);
                }
                StepSpin(dt);
                return;
            }
            if (!released)
            {
                released = true;
                speed = 0.0f;
                animator.Play("Hit", 0.15f);
                fx.Burst(fx.dazed, position + new Vector3(0.0f, HEAD_HEIGHT * scale, 0.0f), 1, Color.white, scale * 0.5f, SPIRAL_RECOVERY);
                run.sounds.PlayAt("boss_dazed", position);
            }
            Stand(dt);
            vulnerability = SPIRAL_DIZZY_DAMAGE_TAKEN;
            glowBoost = RECOVERY_GLOW;
            if (spinTime >= SPIRAL_SECONDS + SPIRAL_RECOVERY)
            {
                EndAttack(0.0f);
            }
        }

        private void StepSpin(float dt)
        {
            float chase = (isLate ? SPIRAL_SPEED_LATE : SPIRAL_SPEED) * speedFactor;
            Vector3 toward = toHero;
            float distance = heroDistance;
            Move(toward * MathF.Min(chase * dt, distance));
            followTimer -= dt;
            if (followTimer <= 0.0f)
            {
                followTimer = FOLLOW_EFFECT_INTERVAL;
                fx.RingOutline(position, SPIRAL_RADIUS, FOLLOW_EFFECT_INTERVAL * 2.0f);
                fx.Burst(fx.spinWind, position + new Vector3(0.0f, 1.0f, 0.0f), 1, Color.white, SPIRAL_RADIUS * 2.0f, FOLLOW_EFFECT_INTERVAL * 3.0f);
            }
            whooshTimer -= dt;
            if (whooshTimer <= 0.0f)
            {
                whooshTimer = SPIRAL_INTERVAL;
                run.sounds.PlayAt("king_spin_whoosh", position);
            }
            spinHitTimer -= dt;
            if (spinHitTimer <= 0.0f && BossGeometry.InCircle(heroPosition, position, SPIRAL_RADIUS, heroGrace)
                && HitHero(SPIRAL_DAMAGE, position))
            {
                spinHitTimer = SPIRAL_INTERVAL;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Soul Nova: expanding rings, each with two gaps that turn from ring to ring; the hero threads the gaps.
        // -------------------------------------------------------------------------------------------------------------

        private void TickNova(float dt)
        {
            Stand(dt);
            PlaySting(ref stung, attackTime, "king_nova_sting");
            if (!struck)
            {
                if (attackTime < windup)
                {
                    return;
                }
                struck = true;
                animator.SetPlayback(1.0f);
                run.feedback.Shake(0.4f, 0.4f);
                for (int i = 0; i < NOVA_RINGS; i++)
                {
                    hazards.AddRing(position, i * NOVA_RING_INTERVAL, NOVA_SPEED, NOVA_HALF_THICKNESS, NOVA_MAX_RADIUS, novaGaps[i], NOVA_GAP_HALF_ANGLE, NOVA_DAMAGE * damageScale, displayName, "soul_nova_ring");
                }
            }
            glowBoost = RECOVERY_GLOW;
            if (attackTime >= windup + NOVA_RECOVERY)
            {
                EndAttack(0.0f);
            }
        }

        protected override void OnKilled()
        {
            CancelAttack();
            isTransitioning = false;
        }
    }
}

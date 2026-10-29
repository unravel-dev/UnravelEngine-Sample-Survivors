using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The Hollow Archmage, the 10:00 mini-boss: a giant skeleton mage that keeps 7 m from the hero. It casts a Bone
    /// Spiral (three rotating arms of slow orbs), a Grave Field (five filling circles on and around the hero) and Summon
    /// Guard (eight Shieldbearers in a ring, channelled while it takes +50% damage). A hero who closes within 3 m makes
    /// it blink 8 m away, leaving a ghost behind. Never the same spell twice in a row.
    /// </summary>
    public sealed class HollowArchmage : Boss
    {
        private const float HEALTH = 13000.0f;
        private const int GOLD = 60;
        private const float WALK_SPEED = 2.2f;
        private const float TURN_RATE = 240.0f;
        private const float HOLD_MIN = 6.0f;
        private const float HOLD_MAX = 8.5f;
        private const float RETREAT_SHARE = 0.8f;
        private const float STRAFE_SHARE = 0.5f;
        private const float STRAFE_SWITCH_SECONDS = 4.0f;
        private const float GAP_SECONDS = 1.0f;
        private const float FIRST_ATTACK_DELAY = 1.0f;
        private const float CAST_RANGE = 16.0f;
        private const float FULL_TURN_DEGREES = 360.0f;

        private const float SPIRAL_WINDUP = 0.8f;
        private const float SPIRAL_SECONDS = 3.0f;
        private const int SPIRAL_ARMS = 3;
        private const float SPIRAL_TURN_DEGREES = 40.0f;
        private const float SPIRAL_INTERVAL = 0.2f;
        private const float SPIRAL_RECOVERY = 0.6f;
        private const float ORB_SPEED = 6.0f;
        private const float ORB_DAMAGE = 12.0f;
        private const float ORB_RANGE = 20.0f;
        private const float ORB_RADIUS = 0.3f;
        private const float ORB_SCALE = 1.3f;
        private const float SIGIL_SIZE = 6.0f;

        private const int FIELD_CIRCLES = 5;
        /// <summary>The ring of circles around the hero starts at a random turn of up to this many degrees.</summary>
        private const float FIELD_MAX_TURN = 90.0f;
        private const float FIELD_RADIUS = 2.5f;
        private const float FIELD_SPREAD = 3.5f;
        private const float FIELD_FILL = 1.2f;
        private const float FIELD_DAMAGE = 35.0f;
        private const float FIELD_RECOVERY = 0.8f;

        private const float SUMMON_CHANNEL = 2.0f;
        private const int SUMMON_COUNT = 8;
        private const float SUMMON_RING = 4.0f;
        private const float SUMMON_ARENA_MARGIN = 2.0f;
        private const float SUMMON_DAMAGE_TAKEN = 1.5f;
        private const float SUMMON_FIRST = 6.0f;
        private const float SUMMON_COOLDOWN = 20.0f;
        private const float SUMMON_RECOVERY = 0.5f;
        private const float MOTES_INTERVAL = 0.5f;
        private const int MOTES_PER_PULSE = 4;

        private const float BLINK_TRIGGER = 3.0f;
        private const float BLINK_DISTANCE = 8.0f;
        private const float BLINK_COOLDOWN = 6.0f;
        private const float GHOST_SECONDS = 1.5f;
        private const float BLINK_GAP = 0.6f;
        private static readonly float[] BLINK_ANGLES = { 0.0f, 45.0f, -45.0f, 90.0f, -90.0f, 135.0f, -135.0f };

        private static readonly Color ASH_TINT = new Color(0.55f, 0.59f, 0.66f, 1.0f);
        private static readonly Color VIOLET_GLOW = new Color(0.72f, 0.35f, 1.0f, 1.0f);
        private static readonly Color SIGIL_COLOR = new Color(0.66f, 0.3f, 1.0f, 1.0f);

        private enum Spell
        {
            None = -1,
            BoneSpiral,
            GraveField,
            SummonGuard,
            Count,
        }

        /// <summary>How often each spell is picked, by <see cref="Spell"/>.</summary>
        private static readonly float[] WEIGHTS = { 3.0f, 3.0f, 2.0f };

        private readonly Vector3[] summonPoints = new Vector3[SUMMON_COUNT];
        private readonly int orbVisual;
        private Spell spell = Spell.None;
        private Spell lastSpell = Spell.None;
        private float spellTime;
        private bool stung;
        private bool released;
        private float gap = FIRST_ATTACK_DELAY;
        private float summonCooldown = SUMMON_FIRST;
        private float blinkCooldown;
        private float strafeSign = 1.0f;
        private float strafeTimer = STRAFE_SWITCH_SECONDS;
        private float spiralBase;
        private float volleyTimer;
        private float motesTimer;

        public HollowArchmage(Run run, BossFx fx, BossHazards hazards, Vector3[] obstacles)
            : base(run, fx, hazards, BossKind.HollowArchmage, obstacles)
        {
            int look = run.projectiles.FindVisual("ArchmageOrb");
            orbVisual = look >= 0 ? look : run.projectiles.FindVisual("HexOrb");
        }

        public override string title => "Voice of the Barrow";

        public override int goldReward => GOLD;

        public override string defeatId => SaveData.BOSS_ARCHMAGE;

        protected override float maxHealth => HEALTH;

        protected override string prefabKey => "app:/data/Prefabs/Bosses/Boss_HollowArchmage.pfb";

        protected override string modelName => "Skeleton_Mage";

        protected override float riseSeconds => 2.6f;

        protected override string riseBanner => "The Hollow Archmage rises";

        protected override Color bodyTint => ASH_TINT;

        protected override Color glowColor => VIOLET_GLOW;

        protected override void TickFight(float dt)
        {
            summonCooldown -= dt;
            blinkCooldown -= dt;
            if (!hasHero)
            {
                spell = Spell.None;
                Idle(dt);
                return;
            }
            if (spell != Spell.None)
            {
                TickSpell(dt);
                return;
            }
            gap -= dt;
            float distance = heroDistance;
            if (blinkCooldown <= 0.0f && distance <= BLINK_TRIGGER + radius)
            {
                Blink();
                return;
            }
            if (gap <= 0.0f && distance <= CAST_RANGE && TryBeginSpell())
            {
                return;
            }
            KeepRange(distance, dt);
        }

        /// <summary>Holds 6-8.5 m: backs away, closes in, or circles the hero; always faces it.</summary>
        private void KeepRange(float distance, float dt)
        {
            Vector3 toward = toHero;
            TurnTowards(toward, TURN_RATE, dt);
            if (distance < HOLD_MIN)
            {
                Glide(-toward, WALK_SPEED * RETREAT_SHARE, dt);
                return;
            }
            if (distance > HOLD_MAX)
            {
                Glide(toward, WALK_SPEED, dt);
                return;
            }
            strafeTimer -= dt;
            if (strafeTimer <= 0.0f)
            {
                strafeTimer = STRAFE_SWITCH_SECONDS;
                strafeSign = -strafeSign;
            }
            var tangent = new Vector3(toward.z, 0.0f, -toward.x) * strafeSign;
            Glide(tangent, WALK_SPEED * STRAFE_SHARE, dt);
        }

        private bool TryBeginSpell()
        {
            Span<float> weights = stackalloc float[(int)Spell.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = GetWeight((Spell)i);
            }
            int picked = WeightedPick.Pick(run.rng, weights);
            if (picked < 0)
            {
                return false;
            }
            BeginSpell((Spell)picked);
            return true;
        }

        private float GetWeight(Spell candidate)
        {
            if (candidate == lastSpell || (candidate == Spell.SummonGuard && summonCooldown > 0.0f))
            {
                return 0.0f;
            }
            return WEIGHTS[(int)candidate];
        }

        private void BeginSpell(Spell next)
        {
            spell = next;
            lastSpell = next;
            spellTime = 0.0f;
            stung = false;
            released = false;
            speed = 0.0f;
            switch (next)
            {
                case Spell.BoneSpiral:
                    spiralBase = Ground.ToYaw(toHero);
                    volleyTimer = 0.0f;
                    animator.PlayToImpact("CastRaise", SPIRAL_WINDUP, 0.15f);
                    fx.Oriented(fx.archmageSigil, new Vector3(position.x, Tuning.TELEGRAPH_HEIGHT, position.z), forward, SIGIL_COLOR, SIGIL_SIZE, SPIRAL_WINDUP + SPIRAL_SECONDS);
                    break;
                case Spell.GraveField:
                    animator.PlayToImpact("Cast", 0.5f, 0.12f);
                    PlaceGraveField();
                    break;
                case Spell.SummonGuard:
                    summonCooldown = SUMMON_COOLDOWN;
                    motesTimer = 0.0f;
                    animator.PlayToImpact("Summon", SUMMON_CHANNEL, 0.2f);
                    for (int i = 0; i < SUMMON_COUNT; i++)
                    {
                        float angle = i * FULL_TURN_DEGREES / SUMMON_COUNT;
                        summonPoints[i] = ClampToArena(position + Ground.FromYaw(angle) * SUMMON_RING, SUMMON_ARENA_MARGIN);
                    }
                    run.sounds.PlayAt("archmage_summon_sting", position);
                    break;
            }
        }

        private void EndSpell()
        {
            spell = Spell.None;
            gap = GAP_SECONDS;
            animator.Walk(0.0f);
        }

        private void TickSpell(float dt)
        {
            spellTime += dt;
            Stand(dt);
            switch (spell)
            {
                case Spell.BoneSpiral:
                    TickSpiral(dt);
                    break;
                case Spell.GraveField:
                    TickGraveField(dt);
                    break;
                case Spell.SummonGuard:
                    TickSummon(dt);
                    break;
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Bone Spiral: a violet sigil, then arms of slow orbs that turn as they fire.
        // -------------------------------------------------------------------------------------------------------------

        private void TickSpiral(float dt)
        {
            PlaySting(ref stung, spellTime, "archmage_spiral_sting");
            if (spellTime < SPIRAL_WINDUP)
            {
                TurnTowards(toHero, TURN_RATE, dt);
                return;
            }
            float spinTime = spellTime - SPIRAL_WINDUP;
            if (!released)
            {
                released = true;
                animator.PlayStretched("CastLong", SPIRAL_SECONDS, 0.15f);
            }
            if (spinTime < SPIRAL_SECONDS)
            {
                volleyTimer -= dt;
                while (volleyTimer <= 0.0f)
                {
                    volleyTimer += SPIRAL_INTERVAL;
                    FireVolley(spiralBase + spinTime * SPIRAL_TURN_DEGREES);
                }
                return;
            }
            glowBoost = RECOVERY_GLOW;
            if (spinTime >= SPIRAL_SECONDS + SPIRAL_RECOVERY)
            {
                EndSpell();
            }
        }

        private void FireVolley(float baseDegrees)
        {
            if (orbVisual < 0)
            {
                return;
            }
            float height = run.projectiles.GetVisual(orbVisual).height;
            for (int arm = 0; arm < SPIRAL_ARMS; arm++)
            {
                Vector3 direction = Ground.FromYaw(baseDegrees + arm * FULL_TURN_DEGREES / SPIRAL_ARMS);
                run.projectiles.Spawn(new ProjectileSpawn
                {
                    team = Team.Enemy,
                    sourceName = displayName,
                    visual = orbVisual,
                    motion = ProjectileMotion.Linear,
                    position = position + direction * radius + new Vector3(0.0f, height, 0.0f),
                    velocity = direction * ORB_SPEED,
                    lifetime = ORB_RANGE / ORB_SPEED,
                    damage = ORB_DAMAGE,
                    radius = ORB_RADIUS,
                    pierce = 0,
                    scale = ORB_SCALE,
                    source = -1,
                });
            }
            run.sounds.PlayAt("archmage_orb", position);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Grave Field: one circle on the hero and a ring of them around it, all filling at once.
        // -------------------------------------------------------------------------------------------------------------

        private void PlaceGraveField()
        {
            Vector3 center = heroPosition;
            float turn = (float)run.rng.NextDouble() * FIELD_MAX_TURN;
            AddFieldCircle(center);
            for (int i = 0; i < FIELD_CIRCLES - 1; i++)
            {
                float angle = turn + i * FULL_TURN_DEGREES / (FIELD_CIRCLES - 1);
                AddFieldCircle(center + Ground.FromYaw(angle) * FIELD_SPREAD);
            }
        }

        /// <summary>A filling circle that bursts in the Hex Casters' red-orange.</summary>
        private void AddFieldCircle(Vector3 center)
        {
            Vector3 point = ClampToArena(center, FIELD_RADIUS);
            fx.Circle(point, FIELD_RADIUS, FIELD_FILL);
            hazards.AddCircle(point, FIELD_RADIUS, FIELD_FILL, FIELD_DAMAGE, displayName, fx.graveBurst, EnemyAbilityExecutor.GRAVE_BURST_COLOR, "grave_burst", false);
        }

        private void TickGraveField(float dt)
        {
            TurnTowards(toHero, TURN_RATE, dt);
            PlaySting(ref stung, spellTime, "archmage_field_sting");
            if (spellTime >= FIELD_FILL)
            {
                glowBoost = RECOVERY_GLOW;
            }
            if (spellTime >= FIELD_FILL + FIELD_RECOVERY)
            {
                EndSpell();
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Summon Guard: a long channel that leaves it open, then a ring of Shieldbearers (no experience) rises.
        // -------------------------------------------------------------------------------------------------------------

        private void TickSummon(float dt)
        {
            if (spellTime < SUMMON_CHANNEL)
            {
                vulnerability = SUMMON_DAMAGE_TAKEN;
                glowBoost = RECOVERY_GLOW;
                motesTimer -= dt;
                if (motesTimer <= 0.0f)
                {
                    motesTimer = MOTES_INTERVAL;
                    for (int i = 0; i < SUMMON_COUNT; i++)
                    {
                        fx.Burst(fx.summonMotes, summonPoints[i], MOTES_PER_PULSE);
                    }
                }
                return;
            }
            if (!released)
            {
                released = true;
                for (int i = 0; i < SUMMON_COUNT; i++)
                {
                    SpawnAdd(Archetype.Shieldbearer, summonPoints[i]);
                }
                run.sounds.PlayAt("summon", position);
                run.feedback.Shake(0.25f, 0.3f);
            }
            if (spellTime >= SUMMON_CHANNEL + SUMMON_RECOVERY)
            {
                EndSpell();
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Blink: a hero too close sends it away from the hero; a ghost lingers where it stood.
        // -------------------------------------------------------------------------------------------------------------

        private void Blink()
        {
            Vector3 hero = heroPosition;
            Vector3 away = BossGeometry.FlatDirection(hero, position, -forward);
            Vector3 best = position;
            float bestDistance = -1.0f;
            // Prefer straight away from the hero; near the arena edge, the first side that keeps its distance.
            for (int i = 0; i < BLINK_ANGLES.Length; i++)
            {
                Vector3 candidate = ClampToArena(hero + BossGeometry.Rotate(away, BLINK_ANGLES[i]) * BLINK_DISTANCE, radius + 1.0f);
                float distance = BossGeometry.FlatDistance(candidate, hero);
                if (distance > bestDistance + 0.5f)
                {
                    best = candidate;
                    bestDistance = distance;
                }
                if (distance >= BLINK_DISTANCE - 0.5f)
                {
                    break;
                }
            }
            Vector3 from = position;
            fx.Burst(fx.archmageGhost, from + new Vector3(0.0f, CHEST_HEIGHT * scale, 0.0f), 1, VIOLET_GLOW, scale * 1.8f, GHOST_SECONDS);
            fx.Burst(fx.blinkFlash, GetChest(), 1, VIOLET_GLOW, scale * 1.2f, 0.35f);
            Move(best - position);
            yaw = BossGeometry.ToYawRadians(BossGeometry.FlatDirection(position, hero, forward));
            speed = 0.0f;
            fx.Burst(fx.blinkFlash, GetChest(), 1, VIOLET_GLOW, scale * 1.2f, 0.35f);
            run.sounds.PlayAt("archmage_blink", position);
            blinkCooldown = BLINK_COOLDOWN;
            gap = MathF.Max(gap, BLINK_GAP);
        }
    }
}

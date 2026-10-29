using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The four playable classes.</summary>
    public enum HeroClass
    {
        Knight,
        Barbarian,
        Mage,
        Rogue,
        /// <summary>How many classes there are; not a class.</summary>
        Count,
    }

    /// <summary>
    /// The hero entity: walks by input through a Character Controller, plays its animation controller (locomotion on
    /// the base layer, weapon swings on an upper-body layer, full-body moves for actives), takes hits through block,
    /// ward, dodge, armor and invulnerability windows, regenerates, and rises again with a revival. The run owns the
    /// stats, the record and the weapons and hands them over with <see cref="BeginRun"/>; this script is the body.
    /// Every timer counts down by the game time <see cref="Tick"/> receives, so menus and hit stops hold them.
    /// Main thread.
    /// </summary>
    [ScriptSourceFile]
    public class HeroController : ScriptComponent, IHeroTarget
    {
        private const string BASE_STATE = "Locomotion";
        private const string UPPER_LAYER = "Upper";
        private const string BLOCK_STATE = "Block";
        private const string DEATH_STATE = "Death";
        private const string SPEED_PARAMETER = "Speed";
        /// <summary>Max Health before a run binds the stats.</summary>
        private const float UNBOUND_MAX_HEALTH = 100.0f;
        private const float MAX_HEALTH_EPSILON = 1e-3f;
        private const float INVULNERABLE_AFTER_HIT = 0.25f;
        private const float HIT_FLASH_SECONDS = 0.1f;
        private const float HIT_FLASH_INTENSITY = 2.0f;
        private const float BLINK_HERTZ = 10.0f;
        private const float BLINK_INTENSITY = 0.6f;
        private static readonly Color BLINK_COLOR = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        private const float RESET_FADE_SECONDS = 0.1f;
        private const float ATTACK_FADE_SECONDS = 0.05f;
        private const float FULL_BODY_FADE_SECONDS = 0.08f;
        private const float BASE_RETURN_FADE_SECONDS = 0.15f;
        /// <summary>The upper layer's weight rises to 1 in this time when a swing starts and falls back in the next.</summary>
        private const float UPPER_FADE_IN_SECONDS = 0.05f;
        private const float UPPER_FADE_OUT_SECONDS = 0.15f;
        /// <summary>A swing plays at least this long, then ends once its state reaches <see cref="UPPER_END_TIME"/>.</summary>
        private const float UPPER_MIN_SECONDS = 0.1f;
        /// <summary>Normalized state time at which a swing counts as done.</summary>
        private const float UPPER_END_TIME = 0.92f;
        private const float BODY_BLOCK_PER_ENEMY = 0.08f;
        private const float BODY_BLOCK_FLOOR = 0.4f;
        /// <summary>Enemies within this share of the body radius count as overlapping the hero.</summary>
        private const float BODY_BLOCK_RADIUS_SHARE = 0.6f;
        private const int NEARBY_CAPACITY = 32;
        private const float TURN_SPEED = 14.0f;
        private const float MIN_MOVE_INPUT = 0.15f;
        /// <summary>Faster than this (m/s) the hero counts as moving: it turns to its heading and moving traits apply.</summary>
        private const float MOVING_SPEED = 0.2f;
        private const float REVIVE_HEALTH = 0.5f;
        private const float REVIVE_INVULNERABLE = 3.0f;
        private const float HIT_PUSH_RADIUS = 1.6f;
        private const float HIT_PUSH_SPEED = 6.0f;

        [Tooltip("The class this body plays.")]
        public HeroClass heroClass = HeroClass.Knight;

        [Tooltip("Ground radius (m) enemies measure their reach to.")]
        public float bodyRadius = 0.45f;

        [Tooltip("Emissive the hero flashes with when hit.")]
        public Color hitFlashColor = new Color(1.0f, 0.15f, 0.1f, 1.0f);

        /// <summary>Chance (0..1) to dodge a hit entirely; set by the loadout.</summary>
        public float dodgeChance;

        private PlayerStats stats;
        private RunRecord record;
        private EnemyWorld enemies;
        private CharacterControllerComponent controller;
        private AnimationComponent animator;
        private MaterialInstance look;
        private readonly int[] nearby = new int[NEARBY_CAPACITY];
        private float health;
        private float knownMaxHealth;
        private bool isDead;
        private Vector3 velocity;
        private Vector3 facing = Vector3.forward;
        private Vector3 moveDirection = Vector3.forward;
        private Vector3 dashVelocity;
        private bool isDashInvulnerable;
        private int wardCharges;
        private Color glowColor;
        private float glowIntensity;
        private int upperLayer = -1;
        private float upperWeight;
        private bool isUpperPlaying;
        private float upperTimer;
        private string fullBodyState;
        // Seconds left of each window; Tick counts them down.
        private float invulnerableTime;
        private float devInvulnerableTime;
        private float flashTime;
        private float glowTime;
        private float walkLockTime;
        private float fullBodyTime;
        private float dashTime;
        private float wardTime;

        /// <summary>Raised when a hit lands (after armor), with the damage taken and where it came from.</summary>
        public event Action<float, Vector3> damaged;
        /// <summary>Raised when the Knight's block negates a hit.</summary>
        public event Action<Vector3> blocked;
        /// <summary>Raised when a ward charge absorbs a hit (the Mage's Arcane Ward).</summary>
        public event Action<Vector3> warded;
        /// <summary>Raised when a hit is dodged (Windstride Boots' Tailwind).</summary>
        public event Action<Vector3> dodged;
        /// <summary>Raised when health reaches zero with no revival left.</summary>
        public event Action died;
        /// <summary>Raised when a revival brings the hero back.</summary>
        public event Action revived;

        public Vector3 position => owner.transform.position;
        public float radius => bodyRadius;
        public bool isTargetable => !isDead && owner.IsValid();
        public bool isAlive => !isDead;
        public float currentHealth => health;
        public float maxHealth => stats != null ? stats[StatId.MaxHealth] : UNBOUND_MAX_HEALTH;
        public float healthFraction => maxHealth > 0.0f ? health / maxHealth : 0.0f;
        /// <summary>World velocity this frame (m/s).</summary>
        public Vector3 currentVelocity => velocity;
        /// <summary>Where the hero faces (ground plane, unit).</summary>
        public Vector3 facingDirection => facing;
        /// <summary>The last direction the hero walked (ground plane, unit); FACING weapons aim along it.</summary>
        public Vector3 lastMoveDirection => moveDirection;
        public bool isMoving => velocity.sqrMagnitude > MOVING_SPEED * MOVING_SPEED;
        public bool isInvulnerable => invulnerableTime > 0.0f || isDashInvulnerable || devInvulnerableTime > 0.0f;
        /// <summary>True while an active ability moves the hero.</summary>
        public bool isDashing => dashTime > 0.0f;
        /// <summary>Ward charges left now (0 once the ward expired).</summary>
        public int wardHits => wardTime > 0.0f ? wardCharges : 0;

        public override void OnStart()
        {
            controller = owner.GetComponent<CharacterControllerComponent>();
            animator = owner.GetComponent<AnimationComponent>();
            ModelComponent model = owner.GetComponent<ModelComponent>();
            look = model != null ? model.GetMaterialInstance(0) : null;
            if (animator != null)
            {
                upperLayer = animator.GetLayerIndex(UPPER_LAYER);
                if (upperLayer >= 0)
                {
                    animator.SetLayerWeight(upperLayer, 0.0f);
                }
            }
            Vector3 forward = owner.transform.forward;
            facing = new Vector3(forward.x, 0.0f, forward.z).normalized;
            moveDirection = facing;
        }

        public override void OnDestroy()
        {
            Unbind();
        }

        /// <summary>
        /// Takes the run's stats (health, armor, block, revivals, move speed), record (healing) and enemies (pushed back
        /// on a hit, crowding the hero), and fills health for a new run. The hero follows Max Health changes from here on.
        /// </summary>
        public void BeginRun(PlayerStats runStats, RunRecord runRecord, EnemyWorld runEnemies)
        {
            Unbind();
            stats = runStats;
            record = runRecord;
            enemies = runEnemies;
            stats.changed += OnStatsChanged;
            isDead = false;
            health = maxHealth;
            knownMaxHealth = maxHealth;
            wardCharges = 0;
            wardTime = 0.0f;
            dodgeChance = 0.0f;
            invulnerableTime = 0.0f;
            dashTime = 0.0f;
            isDashInvulnerable = false;
            velocity = Vector3.zero;
            animator?.CrossFade(BASE_STATE, RESET_FADE_SECONDS);
        }

        /// <summary>Moves and animates the hero by <paramref name="dt"/> of game time; the run calls it once per frame
        /// while playing.</summary>
        public void Tick(float dt, Vector2 input)
        {
            CountDownTimers(dt);
            if (isDead || dt <= 0.0f || stats == null)
            {
                UpdateAnimation(0.0f, dt);
                return;
            }
            Regenerate(dt);
            Vector3 desired = GetInputDirection(input);
            Move(desired, dt);
            Turn(desired, dt);
            UpdateAnimation(velocity.magnitude, dt);
            UpdateFlash();
        }

        /// <summary>Takes a hit; returns whether it hurt (not blocked, warded, dodged or ignored).</summary>
        public bool TakeDamage(float amount, Vector3 from)
        {
            if (isDead || isInvulnerable || stats == null)
            {
                return false;
            }
            if (TryAvoidHit(from))
            {
                return false;
            }
            ApplyHit(amount, from);
            if (health <= 0.0f && !TryRevive())
            {
                Die();
            }
            return true;
        }

        /// <summary>Restores health up to the maximum and records it under <paramref name="source"/>; returns what landed.</summary>
        public float Heal(float amount, HealSource source = HealSource.Other)
        {
            if (isDead || amount <= 0.0f)
            {
                return 0.0f;
            }
            float healed = MathF.Min(amount, maxHealth - health);
            if (healed <= 0.0f)
            {
                return 0.0f;
            }
            health += healed;
            record?.AddHeal(source, healed);
            return healed;
        }

        /// <summary>The next <paramref name="hits"/> hits within <paramref name="seconds"/> are absorbed (the stronger ward wins).</summary>
        public void Ward(int hits, float seconds)
        {
            wardCharges = Math.Max(wardHits, hits);
            wardTime = MathF.Max(wardTime, seconds);
        }

        public void SetInvulnerable(float seconds) => invulnerableTime = MathF.Max(invulnerableTime, seconds);

        /// <summary>Dev: invulnerable for some seconds without the i-frame blink, so captures show the real hero.</summary>
        public void SetDevInvulnerable(float seconds) => devInvulnerableTime = seconds;

        /// <summary>Makes the hero glow (Shield Rush gold, full Fury red) for some seconds; hit flashes win over it.</summary>
        public void SetGlow(Color color, float intensity, float seconds)
        {
            glowColor = color;
            glowIntensity = intensity;
            glowTime = seconds;
        }

        /// <summary>Moves the hero along <paramref name="direction"/> for a distance over some seconds (actives).</summary>
        public void Dash(Vector3 direction, float distance, float seconds, bool invulnerable)
        {
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                direction = moveDirection;
            }
            dashVelocity = direction.normalized * (distance / MathF.Max(seconds, 1e-3f));
            dashTime = seconds;
            isDashInvulnerable = invulnerable;
        }

        /// <summary>Puts the hero somewhere at once (blink, revive).</summary>
        public void Teleport(Vector3 target)
        {
            if (controller != null)
            {
                controller.Warp(target);
            }
            else
            {
                owner.transform.position = target;
            }
        }

        /// <summary>Plays an upper-body state of the controller's Upper layer (a weapon swing); legs keep walking.</summary>
        public void PlayAttack(string state)
        {
            if (animator == null || upperLayer < 0 || isDead)
            {
                return;
            }
            animator.CrossFade(state, ATTACK_FADE_SECONDS, upperLayer);
            isUpperPlaying = true;
            upperTimer = 0.0f;
        }

        /// <summary>Plays a base-layer state that owns the whole body for some seconds (spin, leap, roll).</summary>
        public void PlayFullBody(string state, float seconds, bool stopsWalking = false)
        {
            if (animator == null)
            {
                return;
            }
            animator.CrossFade(state, FULL_BODY_FADE_SECONDS, 0);
            walkLockTime = stopsWalking ? seconds : 0.0f;
            fullBodyState = state;
            fullBodyTime = seconds;
        }

        private void Unbind()
        {
            if (stats != null)
            {
                stats.changed -= OnStatsChanged;
            }
        }

        private void CountDownTimers(float dt)
        {
            invulnerableTime -= dt;
            devInvulnerableTime -= dt;
            flashTime -= dt;
            glowTime -= dt;
            walkLockTime -= dt;
            fullBodyTime -= dt;
            wardTime -= dt;
        }

        /// <summary>A block (the Knight's Bulwark), a ward charge or a dodge turns the hit away, with short i-frames.</summary>
        private bool TryAvoidHit(Vector3 from)
        {
            if (Random.Float() < stats[StatId.BlockChance])
            {
                invulnerableTime = INVULNERABLE_AFTER_HIT;
                PlayAttack(BLOCK_STATE);
                blocked?.Invoke(from);
                return true;
            }
            if (wardHits > 0)
            {
                wardCharges--;
                invulnerableTime = INVULNERABLE_AFTER_HIT;
                warded?.Invoke(from);
                return true;
            }
            if (dodgeChance > 0.0f && Random.Float() < dodgeChance)
            {
                invulnerableTime = INVULNERABLE_AFTER_HIT;
                dodged?.Invoke(from);
                return true;
            }
            return false;
        }

        /// <summary>Takes the hit through armor, flashes, pushes the nearest enemies back and reports it.</summary>
        private void ApplyHit(float amount, Vector3 from)
        {
            float taken = stats.GetDamageTaken(amount);
            health -= taken;
            invulnerableTime = INVULNERABLE_AFTER_HIT;
            flashTime = HIT_FLASH_SECONDS;
            PushEnemiesBack();
            damaged?.Invoke(taken, from);
        }

        /// <summary>Spends a revival, if one is left, to come back at half health with a long invulnerability.</summary>
        private bool TryRevive()
        {
            if (stats.GetCount(StatId.Revival) <= 0)
            {
                return false;
            }
            stats.AddFlat(StatId.Revival, -1.0f);
            health = maxHealth * REVIVE_HEALTH;
            invulnerableTime = REVIVE_INVULNERABLE;
            revived?.Invoke();
            return true;
        }

        private void Die()
        {
            health = 0.0f;
            isDead = true;
            velocity = Vector3.zero;
            PlayFullBody(DEATH_STATE, float.MaxValue);
            died?.Invoke();
        }

        /// <summary>Keeps health in step when Max Health changes: a raise adds the difference, a cut clamps.</summary>
        private void OnStatsChanged()
        {
            float max = maxHealth;
            if (MathF.Abs(max - knownMaxHealth) <= MAX_HEALTH_EPSILON)
            {
                return;
            }
            if (knownMaxHealth > 0.0f && max > knownMaxHealth)
            {
                health += max - knownMaxHealth;
            }
            health = MathF.Min(health, max);
            knownMaxHealth = max;
        }

        /// <summary>Walks toward <paramref name="desired"/> (slowed by crowds), rides a dash, or stands for a full-body move.</summary>
        private void Move(Vector3 desired, float dt)
        {
            if (dashTime > 0.0f)
            {
                dashTime -= dt;
                velocity = dashVelocity;
                if (dashTime <= 0.0f)
                {
                    isDashInvulnerable = false;
                }
            }
            else
            {
                velocity = walkLockTime > 0.0f ? Vector3.zero : desired * (stats[StatId.MoveSpeed] * GetBodyBlockFactor());
            }
            if (controller != null)
            {
                controller.Move(velocity * dt);
            }
            else
            {
                owner.transform.position += velocity * dt;
            }
        }

        /// <summary>Remembers the walked direction and turns the body toward it (or along a dash) while moving.</summary>
        private void Turn(Vector3 desired, float dt)
        {
            if (desired.sqrMagnitude > 0.0f)
            {
                moveDirection = desired.normalized;
            }
            if (!isMoving)
            {
                return;
            }
            Vector3 turnTo = dashTime > 0.0f ? dashVelocity.normalized : moveDirection;
            facing = Vector3.Slerp(facing, turnTo, MathF.Min(1.0f, TURN_SPEED * dt)).normalized;
            owner.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }

        private void UpdateAnimation(float speed, float dt)
        {
            if (animator == null)
            {
                return;
            }
            animator.SetFloat(SPEED_PARAMETER, speed);
            if (fullBodyState != null && !isDead && fullBodyTime <= 0.0f)
            {
                fullBodyState = null;
                animator.CrossFade(BASE_STATE, BASE_RETURN_FADE_SECONDS, 0);
            }
            if (upperLayer >= 0)
            {
                UpdateUpperLayer(dt);
            }
        }

        /// <summary>Fades the swing layer in while a swing plays and out once it is done.</summary>
        private void UpdateUpperLayer(float dt)
        {
            if (isUpperPlaying)
            {
                upperTimer += dt;
                upperWeight = MathF.Min(1.0f, upperWeight + dt / UPPER_FADE_IN_SECONDS);
                isUpperPlaying = upperTimer <= UPPER_MIN_SECONDS || animator.IsInTransition(upperLayer) ||
                                 animator.GetStateTime(upperLayer) < UPPER_END_TIME;
            }
            else
            {
                upperWeight = MathF.Max(0.0f, upperWeight - dt / UPPER_FADE_OUT_SECONDS);
            }
            animator.SetLayerWeight(upperLayer, upperWeight);
        }

        /// <summary>Hit flash first, then an ability glow, then the i-frame blink; otherwise the material's own look.</summary>
        private void UpdateFlash()
        {
            if (look == null)
            {
                return;
            }
            bool isBlinkOff = invulnerableTime > 0.0f && !isDashInvulnerable && ((int)(invulnerableTime * BLINK_HERTZ * 2.0f) & 1) == 1;
            if (flashTime > 0.0f)
            {
                look.emissiveColor = hitFlashColor;
                look.emissiveIntensity = HIT_FLASH_INTENSITY;
            }
            else if (glowTime > 0.0f)
            {
                look.emissiveColor = glowColor;
                look.emissiveIntensity = glowIntensity;
            }
            else if (isBlinkOff)
            {
                look.emissiveColor = BLINK_COLOR;
                look.emissiveIntensity = BLINK_INTENSITY;
            }
            else if (look.hasOverrides)
            {
                look.RevertAll();
            }
        }

        private void Regenerate(float dt)
        {
            float regen = stats[StatId.Regeneration];
            if (regen > 0.0f)
            {
                Heal(regen * dt, HealSource.Regen);
            }
        }

        /// <summary>Ground-plane direction from input; the camera looks toward +Z so input maps straight.</summary>
        private static Vector3 GetInputDirection(Vector2 input)
        {
            var direction = new Vector3(input.x, 0.0f, input.y);
            float length = direction.magnitude;
            if (length < MIN_MOVE_INPUT)
            {
                return Vector3.zero;
            }
            return length > 1.0f ? direction / length : direction;
        }

        /// <summary>Crowds slow the hero: 8% per overlapping enemy, never below 40%.</summary>
        private float GetBodyBlockFactor()
        {
            if (enemies == null)
            {
                return 1.0f;
            }
            int overlapping = enemies.QueryCircle(position, bodyRadius * BODY_BLOCK_RADIUS_SHARE, nearby);
            return MathF.Max(BODY_BLOCK_FLOOR, 1.0f - BODY_BLOCK_PER_ENEMY * overlapping);
        }

        private void PushEnemiesBack()
        {
            enemies?.PushAway(position, HIT_PUSH_RADIUS, HIT_PUSH_SPEED, nearby);
        }
    }
}

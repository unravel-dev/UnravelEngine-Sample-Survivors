using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Where a boss is in its life.</summary>
    public enum BossLife
    {
        /// <summary>Climbing out of the ground. It has no proxy row yet, so weapons cannot hit it.</summary>
        Rising,
        /// <summary>Walking and attacking; weapons hit its proxy row.</summary>
        Fighting,
        /// <summary>Its row died: the death clip plays, then the body sinks into the ground.</summary>
        Dying,
        /// <summary>The entity is gone; the manager forgets the boss.</summary>
        Gone,
    }

    /// <summary>
    /// One boss: an entity instantiated from its prefab (full animation through its controller) that owns a proxy row
    /// of the <see cref="EnemyWorld"/>. Weapons hit the row like any enemy; every frame the boss copies its position
    /// into the row, reads its health from it and sets the damage the row takes (guarding, channeling, dazed,
    /// invulnerable). The base class runs the life cycle (rise, fight, death, sink), movement on the ground plane
    /// (arena edge, scenery circles, the slows weapons put on the row; its proxy type turns freezes into slows), the
    /// entrance, the hit flash and the eye glow. Subclasses give their numbers and implement the fight in
    /// <see cref="TickFight"/>. Main thread.
    /// </summary>
    public abstract class Boss
    {
        /// <summary>Share of the hero's radius a hit shape reaches past its drawn edge.</summary>
        public const float HERO_GRACE_SHARE = 0.5f;
        /// <summary>The <see cref="cameraZoom"/> of a boss that leaves the camera as it is.</summary>
        public const float NO_ZOOM = 1.0f;
        /// <summary>Seconds into a wind-up its audio sting plays.</summary>
        protected const float STING_DELAY = 0.2f;
        /// <summary>Glow multiplier while the boss recovers from an attack: "hit me now".</summary>
        protected const float RECOVERY_GLOW = 1.5f;
        protected const float KNOCKBACK_SECONDS = 0.25f;
        /// <summary>Chest height of a skeleton at scale 1 (m): core glows, dazed stars, guard shimmer.</summary>
        protected const float CHEST_HEIGHT = 1.1f;
        protected const float HEAD_HEIGHT = 1.75f;
        /// <summary>Seconds between two puffs of a following effect (spin rings, core glow).</summary>
        protected const float FOLLOW_EFFECT_INTERVAL = 0.1f;

        private const float ACCELERATION = 6.0f;
        private const float FLASH_SECONDS = 0.08f;
        private const float FLASH_INTERVAL = 0.12f;
        private const float FLASH_STRENGTH = 0.45f;
        private const float BASE_GLOW = 2.0f;
        private const float HEALTH_EPSILON = 0.01f;
        private const float MIN_DAMAGE_SCALE_CHANGE = 1e-4f;
        private const float MIN_GLOW_CHANGE = 1e-3f;
        private const float MIN_TURN_DIRECTION_SQUARED = 1e-6f;
        private const float MIN_MODEL_SCALE = 0.01f;
        /// <summary>A rise restarted late still plays for at least this long.</summary>
        private const float MIN_RISE_RESTART_SECONDS = 0.1f;
        /// <summary>Hero grace (m) when there is no hero to measure.</summary>
        private const float DEFAULT_HERO_GRACE = 0.2f;
        private const float ARENA_MARGIN = 1.0f;
        private const float SPAWN_ARENA_MARGIN = 2.0f;
        private const float DEATH_FADE = 0.12f;
        private const float SINK_SECONDS = 1.6f;
        private const float SINK_DEPTH_PER_SCALE = 0.8f;
        private const float RISE_DUST_SIZE_PER_RADIUS = 3.0f;
        private const float RISE_DUST_INTERVAL = 0.6f;
        private const float SINK_DUST_SECONDS = 1.0f;
        private const float DEFAULT_RISE_BANNER_SECONDS = 2.5f;
        private const float DEFAULT_RISE_SHAKE = 0.3f;
        private const float DEFAULT_CORPSE_SECONDS = 2.5f;

        protected readonly Run run;
        protected readonly BossFx fx;
        protected readonly BossHazards hazards;
        private readonly Vector3[] obstacles;
        private readonly int proxyType;

        public readonly BossKind kind;
        /// <summary>The name on the boss bar (the proxy type's display name).</summary>
        public readonly string displayName;
        public Entity entity;
        /// <summary>The boss's row in the enemy world while it fights.</summary>
        public Enemy proxy = Enemy.None;
        public BossLife life = BossLife.Rising;

        protected BossAnimator animator;
        protected Vector3 position;
        /// <summary>Heading in radians about up, 0 = +Z.</summary>
        protected float yaw;
        /// <summary>Current walking speed (m/s).</summary>
        protected float speed;
        /// <summary>Seconds since the boss appeared (game time).</summary>
        protected float time;
        /// <summary>Seconds in the current life stage.</summary>
        protected float lifeTime;
        /// <summary>Ground radius of the body (m), from the proxy type.</summary>
        protected readonly float radius;
        /// <summary>Model scale of the entity.</summary>
        protected float scale = 1.0f;

        /// <summary>Damage taken multiplier of the boss's state this frame (dazed, channeling, guarding); subclasses set it.</summary>
        protected float vulnerability = 1.0f;
        /// <summary>Takes no damage this frame (phase transitions).</summary>
        protected bool isInvulnerable;
        /// <summary>Eye glow multiplier this frame (recovery windows, channels).</summary>
        protected float glowBoost = 1.0f;

        private TransformComponent transform;
        private MaterialInstance bodyLook;
        private MaterialInstance eyeLook;
        private float lastHealth;
        private float flash;
        private float lastFlashTime = -1.0f;
        /// <summary>The slow weapons put on the proxy row, read every frame.</summary>
        private float worldSlowFactor = 1.0f;
        private float appliedDamageScale = 1.0f;
        private Color appliedBodyEmissive = Color.black;
        private float appliedGlow = -1.0f;
        private float sinkOffset;
        private float riseDustTimer;
        private bool isRiseChecked;

        protected Boss(Run run, BossFx fx, BossHazards hazards, BossKind kind, Vector3[] obstacles)
        {
            this.run = run;
            this.fx = fx;
            this.hazards = hazards;
            this.kind = kind;
            this.obstacles = obstacles ?? Array.Empty<Vector3>();
            proxyType = EnemyCatalog.bossTypes[(int)kind];
            EnemyType type = proxyType >= 0 ? run.enemies.registeredTypes[proxyType] : null;
            radius = type != null ? type.radius * type.scale : 1.0f;
            displayName = type != null ? type.displayName : kind.ToString();
        }

        // -------------------------------------------------------------------------------------------------------------
        // What each boss defines
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The line under the name on the boss bar.</summary>
        public abstract string title { get; }

        /// <summary>Health fractions where the boss bar shows phase pips; null for none (the same array every frame).</summary>
        public virtual float[] phaseMarks => null;

        /// <summary>Whether this is one of the two mini-bosses (the director halves its budget meanwhile).</summary>
        public virtual bool isMiniBoss => true;

        /// <summary>Gold its death pays: a coin pile for a mini-boss, the victory's shower for the King.</summary>
        public abstract int goldReward { get; }

        /// <summary>The id the save records when it falls (kills unlock heroes).</summary>
        public abstract string defeatId { get; }

        /// <summary>Camera distance multiplier while it fights; <see cref="NO_ZOOM"/> leaves the camera as it is.</summary>
        public virtual float cameraZoom => NO_ZOOM;

        /// <summary>Health of its proxy row when it starts fighting.</summary>
        protected abstract float maxHealth { get; }

        protected abstract string prefabKey { get; }

        /// <summary>The skeleton model whose clips the controller plays.</summary>
        protected abstract string modelName { get; }

        /// <summary>Seconds the rise from the ground lasts.</summary>
        protected abstract float riseSeconds { get; }

        /// <summary>The banner of its entrance.</summary>
        protected abstract string riseBanner { get; }

        /// <summary>Seconds the entrance banner stays.</summary>
        protected virtual float riseBannerSeconds => DEFAULT_RISE_BANNER_SECONDS;

        /// <summary>The sound of its entrance.</summary>
        protected virtual string riseSound => "boss_rise";

        /// <summary>Strength of the camera shake over its rise.</summary>
        protected virtual float riseShake => DEFAULT_RISE_SHAKE;

        /// <summary>Base color multiplied into the body texture.</summary>
        protected abstract Color bodyTint { get; }

        /// <summary>Emissive color of the eyes (the glow slot).</summary>
        protected abstract Color glowColor { get; }

        /// <summary>Base color of the held props (blade, shield, staff, axe); white keeps them as authored.</summary>
        protected virtual Color propTint => Color.white;

        /// <summary>Seconds the body lies after its death clip before it sinks.</summary>
        protected virtual float corpseSeconds => DEFAULT_CORPSE_SECONDS;

        /// <summary>One frame of the fight: movement, attack selection, attacks.</summary>
        protected abstract void TickFight(float dt);

        /// <summary>Called when the row lost health since the last frame.</summary>
        protected virtual void OnDamaged(float amount)
        {
        }

        /// <summary>The row died; the death clip has started.</summary>
        protected virtual void OnKilled()
        {
        }

        // -------------------------------------------------------------------------------------------------------------
        // Life cycle (the manager calls these)
        // -------------------------------------------------------------------------------------------------------------

        public Vector3 groundPosition => position;

        /// <summary>Health left as a share of the maximum (1 while rising).</summary>
        public float healthFraction
        {
            get
            {
                int row = run.enemies.GetRow(proxy);
                if (row >= 0)
                {
                    return run.enemies.GetHealthFraction(row);
                }
                return life == BossLife.Rising ? 1.0f : 0.0f;
            }
        }

        /// <summary>Whether the boss is rising or fighting.</summary>
        public bool isAlive => life == BossLife.Rising || life == BossLife.Fighting;

        /// <summary>Instantiates the body facing the hero and starts the rise.</summary>
        public void Spawn(Vector3 at)
        {
            position = ClampToArena(at, SPAWN_ARENA_MARGIN);
            Vector3 hero = run.hero != null ? run.hero.position : position + Vector3.forward;
            yaw = BossGeometry.ToYawRadians(BossGeometry.FlatDirection(position, hero, Vector3.forward));
            Prefab prefab = Assets.GetAsset<Prefab>(prefabKey);
            Quaternion rotation = Quaternion.Euler(0.0f, yaw * Mathf.Rad2Deg, 0.0f);
            entity = prefab != null && prefab.uid != Guid.Empty ? Scene.Instantiate(prefab, position, rotation, Entity.Invalid) : Entity.Invalid;
            if (!entity.IsValid())
            {
                Log.Error($"Boss: {prefabKey} did not instantiate; {displayName} fights without a body.");
            }
            transform = entity.IsValid() ? entity.transform : null;
            scale = transform != null ? MathF.Max(transform.scale.x, MIN_MODEL_SCALE) : 1.0f;
            animator = new BossAnimator(entity, modelName, scale);
            SetUpLooks();
            animator.PlayStretched("Rise", riseSeconds, 0.0f);
            life = BossLife.Rising;
            lifeTime = 0.0f;
            fx.Burst(fx.riseDust, position, 1, Color.white, radius * RISE_DUST_SIZE_PER_RADIUS, riseSeconds);
            AnnounceRise();
        }

        public void Tick(float dt)
        {
            time += dt;
            lifeTime += dt;
            switch (life)
            {
                case BossLife.Rising:
                    TickRise(dt);
                    break;
                case BossLife.Fighting:
                    TickFighting(dt);
                    break;
                case BossLife.Dying:
                    TickDeath(dt);
                    break;
            }
        }

        /// <summary>The boss's row died (the manager matched the death report).</summary>
        public void OnProxyDied()
        {
            if (life != BossLife.Fighting)
            {
                return;
            }
            life = BossLife.Dying;
            lifeTime = 0.0f;
            proxy = Enemy.None;
            speed = 0.0f;
            flash = 0.0f;
            glowBoost = 1.0f;
            animator.Play("Death", DEATH_FADE);
            UpdateLooks();
            OnKilled();
        }

        /// <summary>Removes the body at once (new run).</summary>
        public void Remove()
        {
            if (entity.IsValid())
            {
                Scene.DestroyEntity(entity);
            }
            entity = Entity.Invalid;
            transform = null;
            life = BossLife.Gone;
        }

        /// <summary>The entrance: its banner, its sound and the ground shaking over the rise.</summary>
        private void AnnounceRise()
        {
            run.ui.ShowBanner(riseBanner, riseBannerSeconds, true);
            run.sounds.PlayAt(riseSound, position);
            run.feedback.Shake(riseShake, riseSeconds);
        }

        private void TickRise(float dt)
        {
            if (!isRiseChecked)
            {
                // A controller that had not started on the spawn frame drops the cross-fade: ask again.
                isRiseChecked = true;
                if (!animator.IsIn("Rise"))
                {
                    animator.PlayStretched("Rise", MathF.Max(riseSeconds - lifeTime, MIN_RISE_RESTART_SECONDS), 0.0f);
                }
            }
            riseDustTimer -= dt;
            if (riseDustTimer <= 0.0f)
            {
                riseDustTimer = RISE_DUST_INTERVAL;
                fx.Burst(fx.riseDust, position, 1, Color.white, radius * RISE_DUST_SIZE_PER_RADIUS, RISE_DUST_INTERVAL * 2.0f);
            }
            ApplyTransform();
            if (lifeTime < riseSeconds)
            {
                return;
            }
            var options = new SpawnOptions { health = maxHealth * run.director.dreadScale, damageScale = 1.0f, isRewarded = true };
            proxy = run.enemies.Spawn(proxyType, position, in options);
            if (!proxy.isValid)
            {
                // The world is full this frame; try again next frame.
                return;
            }
            life = BossLife.Fighting;
            lifeTime = 0.0f;
            int row = run.enemies.GetRow(proxy);
            lastHealth = row >= 0 ? run.enemies.GetHealth(row) : 0.0f;
            animator.Walk(0.0f);
        }

        private void TickFighting(float dt)
        {
            int row = run.enemies.GetRow(proxy);
            if (row < 0)
            {
                // The row vanished without a death report (the world was cleared).
                Remove();
                return;
            }
            TrackHealth(row, dt);
            worldSlowFactor = run.enemies.GetSlowFactor(row);
            vulnerability = 1.0f;
            isInvulnerable = false;
            glowBoost = 1.0f;
            TickFight(dt);
            ApplyDamageTaken(row);
            ApplyTransform();
            run.enemies.SetPosition(row, position);
            run.enemies.SetYaw(row, yaw);
            UpdateLooks();
        }

        private void TickDeath(float dt)
        {
            float sinkStart = animator.GetLength("Death") + corpseSeconds;
            if (lifeTime >= sinkStart)
            {
                float t = Math.Clamp((lifeTime - sinkStart) / SINK_SECONDS, 0.0f, 1.0f);
                sinkOffset = -t * SINK_DEPTH_PER_SCALE * scale;
                if (t >= 1.0f)
                {
                    fx.Burst(fx.riseDust, position, 1, Color.white, radius * RISE_DUST_SIZE_PER_RADIUS, SINK_DUST_SECONDS);
                    Remove();
                    return;
                }
            }
            ApplyTransform();
        }

        // -------------------------------------------------------------------------------------------------------------
        // Health, damage taken, looks
        // -------------------------------------------------------------------------------------------------------------

        private void TrackHealth(int row, float dt)
        {
            float health = run.enemies.GetHealth(row);
            if (health < lastHealth - HEALTH_EPSILON)
            {
                OnDamaged(lastHealth - health);
                if (time - lastFlashTime >= FLASH_INTERVAL)
                {
                    flash = 1.0f;
                    lastFlashTime = time;
                }
            }
            lastHealth = health;
            flash = MathF.Max(0.0f, flash - dt / FLASH_SECONDS);
        }

        /// <summary>Movement multiplier from the slows weapons put on the row.</summary>
        protected float speedFactor => worldSlowFactor;

        /// <summary>Hands this frame's vulnerability to the row (only when it changed).</summary>
        private void ApplyDamageTaken(int row)
        {
            float scaleNow = isInvulnerable ? 0.0f : vulnerability;
            if (MathF.Abs(scaleNow - appliedDamageScale) > MIN_DAMAGE_SCALE_CHANGE)
            {
                appliedDamageScale = scaleNow;
                run.enemies.SetDamageTakenScale(row, scaleNow);
            }
        }

        private void SetUpLooks()
        {
            if (!entity.IsValid())
            {
                return;
            }
            ModelComponent body = entity.GetComponent<ModelComponent>() ?? entity.GetComponentInChildren<ModelComponent>();
            if (body == null)
            {
                return;
            }
            bodyLook = body.GetMaterialInstance(0);
            bodyLook.baseColor = bodyTint;
            bodyLook.emissiveColor = Color.black;
            // KayKit skeletons keep the eyes in a second slot with an emissive material.
            if (body.GetSharedMaterialsCount() > 1)
            {
                eyeLook = body.GetMaterialInstance(1);
                eyeLook.emissiveColor = glowColor;
            }
            Color props = propTint;
            if (props.r >= 1.0f && props.g >= 1.0f && props.b >= 1.0f)
            {
                return;
            }
            uint bodyId = body.owner.Id;
            foreach (ModelComponent model in entity.GetComponentsInChildren<ModelComponent>())
            {
                if (model.owner.Id != bodyId)
                {
                    model.GetMaterialInstance(0).baseColor = props;
                }
            }
        }

        /// <summary>The hit flash on the body and the eye glow, written only when they change.</summary>
        private void UpdateLooks()
        {
            if (bodyLook != null)
            {
                Color emissive = Color.white * (flash * FLASH_STRENGTH);
                if (emissive != appliedBodyEmissive)
                {
                    appliedBodyEmissive = emissive;
                    bodyLook.emissiveColor = emissive;
                }
            }
            if (eyeLook != null)
            {
                float glow = BASE_GLOW * glowBoost;
                if (MathF.Abs(glow - appliedGlow) > MIN_GLOW_CHANGE)
                {
                    appliedGlow = glow;
                    eyeLook.emissiveIntensity = glow;
                }
            }
        }

        /// <summary>Changes the eye glow color (the King's core turns green while it raises the dead).</summary>
        protected void SetGlowColor(Color color)
        {
            if (eyeLook != null)
            {
                eyeLook.emissiveColor = color;
            }
        }

        private void ApplyTransform()
        {
            if (transform == null)
            {
                return;
            }
            transform.position = new Vector3(position.x, sinkOffset, position.z);
            transform.rotation = Quaternion.Euler(0.0f, yaw * Mathf.Rad2Deg, 0.0f);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Helpers for the fights
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The unit ground heading the boss faces.</summary>
        protected Vector3 forward => BossGeometry.FromYawRadians(yaw);

        /// <summary>Whether there is a hero to fight (alive).</summary>
        protected bool hasHero => run.hero != null && run.hero.isAlive;

        protected Vector3 heroPosition => run.hero != null ? BossGeometry.Flat(run.hero.position) : position;

        protected float heroDistance => BossGeometry.FlatDistance(position, heroPosition);

        protected Vector3 toHero => BossGeometry.FlatDirection(position, heroPosition, forward);

        /// <summary>How far past a drawn edge the hero's body may reach before a hit counts.</summary>
        protected float heroGrace => run.hero != null ? run.hero.radius * HERO_GRACE_SHARE : DEFAULT_HERO_GRACE;

        /// <summary>A point at chest height in front of the body (core glows, shields).</summary>
        protected Vector3 GetChest(float forwardOffset = 0.0f) => position + forward * forwardOffset + new Vector3(0.0f, CHEST_HEIGHT * scale, 0.0f);

        /// <summary>Turns toward a ground direction at most <paramref name="degreesPerSecond"/>.</summary>
        protected void TurnTowards(Vector3 direction, float degreesPerSecond, float dt)
        {
            if (BossGeometry.Flat(direction).sqrMagnitude < MIN_TURN_DIRECTION_SQUARED)
            {
                return;
            }
            float target = BossGeometry.ToYawRadians(direction);
            float delta = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, target * Mathf.Rad2Deg);
            float step = degreesPerSecond * dt;
            yaw += Math.Clamp(delta, -step, step) * Mathf.Deg2Rad;
        }

        /// <summary>Walks along the facing toward a point, turning at a rate, stopping at a distance.</summary>
        protected void WalkTowards(Vector3 target, float stopDistance, float walkSpeed, float turnRate, float dt)
        {
            Vector3 direction = BossGeometry.FlatDirection(position, target, forward);
            TurnTowards(direction, turnRate, dt);
            float distance = BossGeometry.FlatDistance(position, target);
            float wanted = distance > stopDistance ? walkSpeed * speedFactor : 0.0f;
            // Big bodies turn before they stride: no speed while the target is behind.
            float alignment = MathF.Max(Vector3.Dot(forward, direction), 0.0f);
            speed = Mathf.MoveTowards(speed, wanted * alignment, ACCELERATION * dt);
            float stride = MathF.Min(speed * dt, MathF.Max(distance - stopDistance, 0.0f));
            Move(forward * stride);
            animator.Walk(speed);
        }

        /// <summary>Moves along any ground direction (backing away, strafing) while the facing is left alone.</summary>
        protected void Glide(Vector3 direction, float glideSpeed, float dt)
        {
            speed = Mathf.MoveTowards(speed, glideSpeed * speedFactor, ACCELERATION * dt);
            Move(BossGeometry.Flat(direction).normalized * (speed * dt));
            animator.Walk(speed);
        }

        /// <summary>Stops walking (eases the walk out) without leaving the state an attack plays.</summary>
        protected void Stand(float dt)
        {
            speed = Mathf.MoveTowards(speed, 0.0f, ACCELERATION * 2.0f * dt);
            if (animator.state == BossAnimator.LOCOMOTION)
            {
                animator.Walk(speed);
            }
        }

        /// <summary>Stops and idles in locomotion (no hero to fight).</summary>
        protected void Idle(float dt)
        {
            speed = Mathf.MoveTowards(speed, 0.0f, ACCELERATION * 2.0f * dt);
            animator.Walk(speed);
        }

        /// <summary>Moves the body by a ground offset, kept inside the arena and out of the scenery.</summary>
        /// <returns>The distance actually moved.</returns>
        protected float Move(Vector3 offset)
        {
            Vector3 before = position;
            position = ClampToArena(position + BossGeometry.Flat(offset), ARENA_MARGIN + radius);
            for (int i = 0; i < obstacles.Length; i++)
            {
                var center = new Vector3(obstacles[i].x, 0.0f, obstacles[i].y);
                float reach = obstacles[i].z + radius;
                float distance = BossGeometry.FlatDistance(position, center);
                if (distance < reach)
                {
                    position = center + BossGeometry.FlatDirection(center, position, -forward) * reach;
                }
            }
            return BossGeometry.FlatDistance(before, position);
        }

        /// <summary>A ground point kept inside the arena by a margin.</summary>
        protected Vector3 ClampToArena(Vector3 point, float margin)
        {
            Vector3 flat = BossGeometry.Flat(point);
            Vector3 center = BossGeometry.Flat(run.enemies.arenaCenter);
            float limit = MathF.Max(run.arenaRadius - margin, 1.0f);
            Vector3 offset = flat - center;
            float length = offset.magnitude;
            return length > limit ? center + offset * (limit / length) : flat;
        }

        /// <summary>Plays a wind-up's audio sting once, <see cref="STING_DELAY"/> seconds in.</summary>
        protected void PlaySting(ref bool played, float actionTime, string sound)
        {
            if (!played && actionTime >= STING_DELAY)
            {
                played = true;
                run.sounds.PlayAt(sound, position);
            }
        }

        /// <summary>Hurts the hero as this boss (death screen cause).</summary>
        protected bool HitHero(float amount, Vector3 from) => DamageHero(run, amount, from, displayName);

        /// <summary>Pushes the hero away from a point by some metres.</summary>
        protected void KnockHeroFrom(Vector3 from, float metres)
        {
            PushHero(BossGeometry.FlatDirection(from, heroPosition, forward), metres);
        }

        /// <summary>Pushes the hero along a ground direction by some metres.</summary>
        protected void PushHero(Vector3 direction, float metres)
        {
            if (run.hero != null && run.hero.isAlive)
            {
                run.hero.Dash(direction, metres, KNOCKBACK_SECONDS, false);
            }
        }

        /// <summary>Spawns an unrewarded add of an archetype in the current tint, rising from the ground.</summary>
        protected void SpawnAdd(Archetype archetype, Vector3 at)
        {
            WaveDirector director = run.director;
            var options = new SpawnOptions { healthScale = director.healthScale, damageScale = director.damageScale, rises = true, isRewarded = false };
            run.enemies.Spawn(EnemyCatalog.Get(archetype, director.currentTint), ClampToArena(at, SPAWN_ARENA_MARGIN), in options);
        }

        /// <summary>Hurts the hero for a boss attack (scaled by Dread) and names the source for the death screen.</summary>
        /// <returns>Whether the hit landed (not dodged, blocked or ignored).</returns>
        public static bool DamageHero(Run run, float amount, Vector3 from, string source)
        {
            HeroController target = run.hero;
            if (target == null || !target.isTargetable || amount <= 0.0f)
            {
                return false;
            }
            if (!target.TakeDamage(amount * run.director.dreadDamageScale, from))
            {
                return false;
            }
            run.director.lastDamageSource = source;
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Runs enemy abilities on the main thread: telegraphs at cast start, the effect at the hit moment (melee cones,
    /// bolts, grave circles, summons, lunges, explosions, wards), exploder fuses and chain detonations, and the global
    /// limits that keep a crowd of casters readable. Enemy telegraphs are red-orange only.
    /// </summary>
    public sealed class EnemyAbilityExecutor : IEnemyCommandSink
    {
        /// <summary>Most grave circles on the ground at once; casts past it show nothing and hit nothing.</summary>
        private const int MAX_GRAVE_CIRCLES = 8;
        /// <summary>Seconds between two summons anywhere on the field.</summary>
        private const float SUMMON_INTERVAL = 2.0f;
        /// <summary>A Cinder Skull that dies of anything but its own fuse bursts after this delay, hurting enemies only.</summary>
        private const float CHAIN_DETONATION_DELAY = 0.3f;
        private const float CHAIN_DETONATION_DAMAGE = 60.0f;
        private const float CHAIN_DETONATION_RADIUS = 2.5f;
        /// <summary>Detonations shove the enemies they hit this far (m).</summary>
        private const float DETONATION_PUSH_METRES = 1.0f;
        private const float FUSE_BLINK_START_HERTZ = 4.0f;
        private const float FUSE_BLINK_END_HERTZ = 12.0f;
        private const float MIN_FUSE_SECONDS = 1e-3f;
        /// <summary>Seconds between two ward rings (and ward sounds) anywhere: a crowd of casters must not paint the ground.</summary>
        private const float WARD_FX_INTERVAL = 1.5f;
        /// <summary>The ward ring outlives the wind-up by this much, so it is still there when the ward lands.</summary>
        private const float WARD_RING_LINGER = 0.4f;
        private const int SUMMON_MOTES = 12;
        /// <summary>Effect heights per unit of the caster's model scale.</summary>
        private const float LUNGE_FLARE_HEIGHT = 1.4f;
        private const float SWING_HEIGHT = 1.0f;
        private const float SWING_SECONDS = 0.18f;
        /// <summary>Melee reach and spread: the hero counts as in front when this close, whatever the angle.</summary>
        private const float MELEE_POINT_BLANK = 0.3f;
        private const float MIN_MELEE_SPREAD = 1.0f;
        private const float BURST_HEIGHT = 0.1f;
        private const float BURST_SIZE_PER_RADIUS = 2.2f;
        private const float BURST_SECONDS = 0.5f;
        private const float BLAST_HEIGHT = 0.3f;
        private const float BLAST_SECONDS = 0.5f;
        private const float CHAIN_BLAST_SECONDS = 0.45f;
        private const float CHAIN_SPARK_SIZE = 1.2f;
        private const float CHAIN_SPARK_SECONDS = 0.25f;
        private const float EXPLODE_SHAKE = 0.2f;
        private const float EXPLODE_SHAKE_SECONDS = 0.25f;
        /// <summary>Enemy bolts fly a little past their range before they fade.</summary>
        private const float PROJECTILE_EXTRA_SECONDS = 0.5f;
        private const float PROJECTILE_RADIUS = 0.25f;
        private const float MIN_PROJECTILE_SPEED = 0.1f;
        private const float FULL_CIRCLE_DEGREES = 360.0f;
        private const int SCRATCH = 512;
        private const string AIM_LINE = "AimLine";
        private const string CINDER_BLAST = "CinderBlast";
        private const string WARD_RING = "WardRing";
        private const string LUNGE_FLARE = "LungeFlare";
        private const string FIRE_SOUND = "bolt_fire";

        /// <summary>The red-orange of a grave circle's burst (the Hollow Archmage's field bursts in it too).</summary>
        public static readonly Color GRAVE_BURST_COLOR = new Color(1.0f, 0.35f, 0.2f, 1.0f);
        private static readonly Color CHAIN_SPARK_COLOR = new Color(1.0f, 0.55f, 0.2f, 1.0f);
        private static readonly Color WARD_COLOR = new Color(0.6f, 0.3f, 1.0f, 1.0f);

        private readonly Run run;
        private readonly List<Detonation> detonations = new List<Detonation>();
        private readonly List<Fuse> fuses = new List<Fuse>();
        private readonly List<float> circleEnds = new List<float>();
        /// <summary>Cinder Skulls that blew themselves up: their death must not chain a second burst.</summary>
        private readonly HashSet<Enemy> selfDetonated = new HashSet<Enemy>();
        /// <summary>Grave circle casts started past <see cref="MAX_GRAVE_CIRCLES"/>: their hit is skipped too.</summary>
        private readonly HashSet<Enemy> suppressedCasts = new HashSet<Enemy>();
        private readonly Dictionary<string, int> projectileVisuals = new Dictionary<string, int>();
        private readonly int[] scratch = new int[SCRATCH];
        private readonly int telegraphFill;
        private readonly int telegraphRing;
        private readonly int telegraphCone;
        private readonly int aimLine;
        private readonly int burstBlast;
        private readonly int fireBlast;
        private readonly int summonMotes;
        private readonly int wardRing;
        private readonly int lungeFlare;
        private readonly int swingTrail;
        private float time;
        private float lastSummon = float.MinValue;
        private float lastWardRing = float.MinValue;
        private float lastWardSound = float.MinValue;

        private struct Detonation
        {
            public float at;
            public Vector3 position;
            public float damageScale;
        }

        private struct Fuse
        {
            public Enemy enemy;
            public float start;
            public float end;
            public float nextBlink;
        }

        public EnemyAbilityExecutor(Run run)
        {
            this.run = run;
            Effects effects = run.effects;
            telegraphFill = effects.Find(BossFx.TELEGRAPH_FILL);
            telegraphRing = effects.Find(BossFx.TELEGRAPH_RING);
            telegraphCone = effects.Find(BossFx.TELEGRAPH_CONE);
            aimLine = effects.Find(AIM_LINE);
            burstBlast = effects.Find(BossFx.GRAVE_BURST);
            fireBlast = effects.Find(CINDER_BLAST);
            summonMotes = effects.Find(BossFx.SUMMON_MOTES);
            wardRing = effects.Find(WARD_RING);
            lungeFlare = effects.Find(LUNGE_FLARE);
            swingTrail = effects.Find(BossFx.ENEMY_SWING);
        }

        public void Execute(EnemyWorld world, in EnemyCommand command)
        {
            EnemyType kind = world.registeredTypes[command.type];
            EnemyAbility ability = kind.abilities[command.ability];
            if (command.phase == EnemyCommand.Phase.Begin)
            {
                Begin(world, ability, command);
                return;
            }
            switch (ability.effect)
            {
                case EnemyEffect.Melee:
                    HitMelee(kind, ability, command);
                    break;
                case EnemyEffect.Projectile:
                    FireProjectiles(kind, ability, command);
                    break;
                case EnemyEffect.GroundBurst:
                    HitGroundBurst(kind, ability, command);
                    break;
                case EnemyEffect.Summon:
                    Summon(world, ability, command);
                    break;
                case EnemyEffect.Charge:
                    HitHeroInCircle(kind, ability, command.position, ability.radius, command.damageScale);
                    break;
                case EnemyEffect.Explode:
                    Explode(world, kind, ability, command);
                    break;
                case EnemyEffect.Ward:
                    WardAllies(world, ability, command);
                    break;
            }
        }

        /// <summary>Delayed detonations, fuse blinks and circle bookkeeping; once per frame (game time).</summary>
        public void Tick(float dt)
        {
            time += dt;
            TickDetonations();
            TickFuses();
            for (int i = circleEnds.Count - 1; i >= 0; i--)
            {
                if (circleEnds[i] <= time)
                {
                    circleEnds.RemoveAt(i);
                }
            }
        }

        /// <summary>A Cinder Skull that died of anything but its own fuse bursts a moment later, hurting only other enemies.</summary>
        public void OnEnemyDied(in EnemyDeath death)
        {
            suppressedCasts.Remove(death.enemy);
            if (selfDetonated.Remove(death.enemy))
            {
                return;
            }
            EnemyType kind = run.enemies.registeredTypes[death.type];
            if (!kind.detonatesOnDeath)
            {
                return;
            }
            detonations.Add(new Detonation { at = time + CHAIN_DETONATION_DELAY, position = death.position, damageScale = 1.0f });
            run.effects.Emit(fireBlast, death.position + Vector3.up * BLAST_HEIGHT, 1, CHAIN_SPARK_COLOR, CHAIN_SPARK_SIZE, CHAIN_SPARK_SECONDS);
        }

        /// <summary>Removes every pending effect (new run).</summary>
        public void Clear()
        {
            detonations.Clear();
            fuses.Clear();
            circleEnds.Clear();
            selfDetonated.Clear();
            suppressedCasts.Clear();
            time = 0.0f;
            lastSummon = float.MinValue;
            lastWardRing = float.MinValue;
            lastWardSound = float.MinValue;
        }

        private void TickDetonations()
        {
            for (int i = detonations.Count - 1; i >= 0; i--)
            {
                if (detonations[i].at > time)
                {
                    continue;
                }
                Detonation detonation = detonations[i];
                detonations.RemoveAt(i);
                DetonateOnEnemies(detonation.position, detonation.damageScale, CHAIN_DETONATION_RADIUS);
            }
        }

        /// <summary>A lit fuse blinks its skull faster and faster until the blast.</summary>
        private void TickFuses()
        {
            for (int i = fuses.Count - 1; i >= 0; i--)
            {
                Fuse fuse = fuses[i];
                int row = run.enemies.GetRow(fuse.enemy);
                if (row < 0 || time >= fuse.end)
                {
                    fuses.RemoveAt(i);
                    continue;
                }
                if (time < fuse.nextBlink)
                {
                    continue;
                }
                run.enemies.Flash(row);
                float t = (time - fuse.start) / MathF.Max(fuse.end - fuse.start, MIN_FUSE_SECONDS);
                float hertz = Mathf.Lerp(FUSE_BLINK_START_HERTZ, FUSE_BLINK_END_HERTZ, t);
                fuse.nextBlink = time + 1.0f / hertz;
                fuses[i] = fuse;
            }
        }

        /// <summary>The cast start: telegraphs that fill over the caster's wind-up, fuses and cast sounds.</summary>
        private void Begin(EnemyWorld world, EnemyAbility ability, in EnemyCommand command)
        {
            Vector3 ground = new Vector3(0.0f, Tuning.TELEGRAPH_HEIGHT, 0.0f);
            Vector3 heading = command.direction * BossFx.ORIENT_SPEED;
            switch (ability.effect)
            {
                case EnemyEffect.Melee when ability.telegraph:
                    // Heavy swings show a cone that fills over the wind-up.
                    run.effects.EmitMoving(telegraphCone, command.position + ground, heading, 1, (ability.radius + world.targetRadius) * 2.0f, command.windup);
                    break;
                case EnemyEffect.Projectile:
                    run.effects.EmitMoving(aimLine, command.position + ground, heading, 1, ability.range, command.windup);
                    break;
                case EnemyEffect.GroundBurst:
                    if (circleEnds.Count >= MAX_GRAVE_CIRCLES)
                    {
                        suppressedCasts.Add(command.enemy);
                        return;
                    }
                    circleEnds.Add(time + command.windup);
                    ShowCircle(command.target, ability.radius, command.windup);
                    break;
                case EnemyEffect.Summon:
                    run.effects.Emit(summonMotes, command.position + ground, SUMMON_MOTES);
                    break;
                case EnemyEffect.Charge:
                    run.effects.Emit(lungeFlare, command.position + new Vector3(0.0f, command.scale * LUNGE_FLARE_HEIGHT, 0.0f), 1);
                    break;
                case EnemyEffect.Explode:
                    fuses.Add(new Fuse { enemy = command.enemy, start = time, end = time + command.windup, nextBlink = time });
                    ShowCircle(command.position, ability.radius, command.windup);
                    run.sounds.PlayAt("cinder_fuse", command.position);
                    break;
                case EnemyEffect.Ward:
                    if (time - lastWardRing >= WARD_FX_INTERVAL)
                    {
                        lastWardRing = time;
                        run.effects.Emit(wardRing, command.position + ground, 1, WARD_COLOR, ability.radius * 2.0f, command.windup + WARD_RING_LINGER);
                    }
                    break;
            }
            if (!string.IsNullOrEmpty(ability.castSound))
            {
                run.sounds.PlayAt(ability.castSound, command.position);
            }
        }

        private void HitMelee(EnemyType kind, EnemyAbility ability, in EnemyCommand command)
        {
            HeroController hero = run.hero;
            if (hero == null || !hero.isTargetable)
            {
                return;
            }
            Vector3 swingAt = command.position + new Vector3(0.0f, command.scale * SWING_HEIGHT, 0.0f);
            run.effects.EmitMoving(swingTrail, swingAt, command.direction * BossFx.ORIENT_SPEED, 1, ability.radius * 2.0f, SWING_SECONDS);
            Vector3 to = hero.position - command.position;
            to.y = 0.0f;
            float distance = to.magnitude;
            float reach = ability.radius + hero.radius + command.radius;
            if (distance > reach)
            {
                return;
            }
            float halfAngle = MathF.Max(ability.spread, MIN_MELEE_SPREAD) * 0.5f;
            if (distance > MELEE_POINT_BLANK && Vector3.Angle(to / distance, command.direction) > halfAngle)
            {
                return;
            }
            DamageHero(kind, ability.damage * command.damageScale, command.position);
        }

        private void HitGroundBurst(EnemyType kind, EnemyAbility ability, in EnemyCommand command)
        {
            if (suppressedCasts.Remove(command.enemy))
            {
                return;
            }
            Vector3 burstAt = command.target + new Vector3(0.0f, BURST_HEIGHT, 0.0f);
            run.effects.Emit(burstBlast, burstAt, 1, GRAVE_BURST_COLOR, ability.radius * BURST_SIZE_PER_RADIUS, BURST_SECONDS);
            run.sounds.PlayAt("grave_burst", command.target);
            HitHeroInCircle(kind, ability, command.target, ability.radius, command.damageScale);
        }

        private void HitHeroInCircle(EnemyType kind, EnemyAbility ability, Vector3 center, float radius, float damageScale)
        {
            HeroController hero = run.hero;
            if (hero == null || !hero.isTargetable)
            {
                return;
            }
            Vector3 to = hero.position - center;
            to.y = 0.0f;
            if (to.magnitude <= radius + hero.radius)
            {
                DamageHero(kind, ability.damage * damageScale, center);
            }
        }

        /// <summary>The ability's count of bolts, fanned over its spread (a full circle spaces them evenly).</summary>
        private void FireProjectiles(EnemyType kind, EnemyAbility ability, in EnemyCommand command)
        {
            int visual = GetProjectileVisual(ability.visual);
            if (visual < 0)
            {
                return;
            }
            int count = ability.count;
            float spread = ability.spread;
            float height = run.projectiles.GetVisual(visual).height;
            Vector3 origin = command.position + new Vector3(0.0f, height, 0.0f);
            float baseYaw = MathF.Atan2(command.direction.x, command.direction.z);
            bool isFullCircle = spread >= FULL_CIRCLE_DEGREES;
            float step = count > 1 ? spread * Mathf.Deg2Rad / (isFullCircle ? count : count - 1) : 0.0f;
            float first = count > 1 && !isFullCircle ? -spread * 0.5f * Mathf.Deg2Rad : 0.0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = BossGeometry.FromYawRadians(baseYaw + first + step * i);
                run.projectiles.Spawn(new ProjectileSpawn
                {
                    team = Team.Enemy,
                    sourceName = kind.displayName,
                    visual = visual,
                    motion = ProjectileMotion.Linear,
                    position = origin,
                    velocity = direction * ability.speed,
                    lifetime = ability.range / MathF.Max(ability.speed, MIN_PROJECTILE_SPEED) + PROJECTILE_EXTRA_SECONDS,
                    damage = ability.damage * command.damageScale,
                    radius = PROJECTILE_RADIUS,
                    pierce = 0,
                    scale = 1.0f,
                    source = -1,
                });
            }
            run.sounds.PlayAt(FIRE_SOUND, command.position);
        }

        /// <summary>The projectile look of an ability, looked up once per name (-1 when the catalog has none).</summary>
        private int GetProjectileVisual(string name)
        {
            if (!projectileVisuals.TryGetValue(name, out int visual))
            {
                visual = run.projectiles.FindVisual(name);
                projectileVisuals[name] = visual;
            }
            return visual;
        }

        private void Summon(EnemyWorld world, EnemyAbility ability, in EnemyCommand command)
        {
            if (time - lastSummon < SUMMON_INTERVAL || world.count >= ability.summonLimit || ability.summonType < 0)
            {
                return;
            }
            lastSummon = time;
            WaveDirector director = run.director;
            var options = new SpawnOptions { healthScale = director.healthScale, damageScale = director.damageScale, rises = true, isRewarded = false };
            for (int i = 0; i < ability.count; i++)
            {
                Vector3 position = command.position + Ground.FromYaw(i * FULL_CIRCLE_DEGREES / ability.count) * ability.radius;
                world.Spawn(ability.summonType, position, in options);
            }
            run.sounds.PlayAt("summon", command.position);
        }

        private void Explode(EnemyWorld world, EnemyType kind, EnemyAbility ability, in EnemyCommand command)
        {
            run.effects.Emit(fireBlast, command.position + Vector3.up * BLAST_HEIGHT, 1, Color.white, ability.radius * 2.0f, BLAST_SECONDS);
            run.sounds.PlayAt("cinder_blast", command.position);
            run.feedback.Shake(EXPLODE_SHAKE, EXPLODE_SHAKE_SECONDS);
            HitHeroInCircle(kind, ability, command.position, ability.radius, command.damageScale);
            // Marked before the blast: the blast may kill the skull itself, and that death must not chain.
            selfDetonated.Add(command.enemy);
            DetonateOnEnemies(command.position, 1.0f, ability.radius);
            world.Kill(command.enemy);
        }

        private void DetonateOnEnemies(Vector3 position, float damageScale, float radius)
        {
            run.effects.Emit(fireBlast, position + Vector3.up * BLAST_HEIGHT, 1, Color.white, radius * 2.0f, CHAIN_BLAST_SECONDS);
            run.sounds.PlayAt("cinder_blast", position);
            EnemyWorld world = run.enemies;
            int found = world.QueryCircle(position, radius, scratch);
            for (int i = 0; i < found; i++)
            {
                int row = scratch[i];
                Vector3 push = Combat.GetPushFrom(position, world.GetPosition(row), DETONATION_PUSH_METRES * Tuning.KNOCKBACK_SPEED_PER_METRE);
                Combat.DamageEnemy(row, CHAIN_DETONATION_DAMAGE * damageScale, push, RunRecord.OTHER_SOURCE, false);
            }
        }

        private void WardAllies(EnemyWorld world, EnemyAbility ability, in EnemyCommand command)
        {
            int found = world.QueryCircle(command.position, ability.radius, scratch);
            for (int i = 0; i < found; i++)
            {
                world.Ward(scratch[i], ability.chargeTime);
            }
            if (time - lastWardSound >= WARD_FX_INTERVAL)
            {
                lastWardSound = time;
                run.sounds.PlayAt("ward", command.position);
            }
        }

        private void ShowCircle(Vector3 center, float radius, float seconds)
        {
            Vector3 position = center + new Vector3(0.0f, Tuning.TELEGRAPH_HEIGHT, 0.0f);
            run.effects.Emit(telegraphRing, position, 1, Tuning.DANGER_COLOR, radius * 2.0f, seconds);
            run.effects.Emit(telegraphFill, position, 1, Tuning.DANGER_COLOR, radius * 2.0f, seconds);
        }

        private void DamageHero(EnemyType kind, float amount, Vector3 from)
        {
            if (run.hero.TakeDamage(amount, from))
            {
                run.director.lastDamageSource = kind.displayName;
            }
        }
    }
}

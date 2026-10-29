using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Aegis Orbit (Knight, Uncommon): spectral shields (Count + Amount, Size radius after Area) circle the knight on
    /// an orbit of Radius (Area) at Speed degrees per second for Duration (Duration stat), re-hitting an enemy at most
    /// every Interval and knocking it outward (Knockback); they destroy the enemy projectiles they touch. The orbit
    /// breathes a little so the ring sweeps a band instead of a line.
    /// Evolution Bastion Halo (+ Plate Pauldron): permanent shields on two counter-rotating rings; destroyed
    /// projectiles are reflected at the nearest enemy (SubDamage at SubSpeed).
    /// Look: the shields arrive with a flash, a golden shockwave at the orbit and a glint where each one appears. The
    /// shield meshes stay clear: each trails a short gold wake that begins behind it (KnightLooks) and in turn catches
    /// a crisp gold glint on its upper leading rim. A hit flashes a star, a block bursts in a holy cross flare and steel
    /// sparks, an ending shield dissolves into motes. Bastion Halo draws both orbits as celestial azure rings of light
    /// at shield height, arrives and blocks in azure, and its reflected shots leave as bolts of light with a glint.
    /// </summary>
    public sealed class AegisOrbit : Weapon
    {
        private const int MAX_SHIELDS = 12;
        private const int MAX_ENEMY_SHOTS = 64;
        private const float BREATH_SHARE = 0.12f;
        private const float BREATH_RATE = 0.7f;
        private const float INNER_RING_SHARE = 0.72f;
        private const float OUTWARD_SHARE = 0.8f;
        private const float TANGENT_SHARE = 0.4f;
        private const float REFLECT_RANGE = 14.0f;
        private const float REFLECT_RADIUS = 0.3f;
        private const float REFLECT_KNOCKBACK = 0.8f;
        private const float REFLECT_LIFETIME_MARGIN = 0.25f;

        private const float SUMMON_FLASH_SIZE = 1.4f;
        private const float SHIELD_GLINT_SIZE = 1.2f;
        private const float HIT_GLINT_SIZE = 0.8f;
        private const float BLOCK_FLASH_SIZE = 1.0f;
        private const float BLOCK_CROSS_SIZE = 1.3f;
        private const float FADE_GLINT_SIZE = 0.9f;
        private const float REFLECT_GLINT_SIZE = 1.0f;
        /// <summary>Each shield catches a rim glint about this often; the glints take the shields in turn.</summary>
        private const float RIM_GLINT_PERIOD = 0.3f;
        private const float RIM_GLINT_SIZE = 0.7f;
        private const float RIM_GLINT_SECONDS = 0.22f;
        /// <summary>The glint's place on the shield (upper leading rim), in metres from its centre.</summary>
        private const float RIM_HEIGHT = 0.45f;
        private const float RIM_FORWARD = 0.25f;
        private const float HALO_INTERVAL = 0.03f;
        private const float HALO_SECONDS = 0.07f;

        /// <summary>The shields' gold: a little brighter than KnightLooks.GOLD.</summary>
        private static readonly Color SHIELD_GOLD = new Color(1.0f, 0.85f, 0.45f, 1.0f);
        /// <summary>Bastion Halo's accent: celestial azure, a touch paler than KnightLooks.AZURE.</summary>
        private static readonly Color BASTION_AZURE = new Color(0.74f, 0.88f, 1.0f, 1.0f);
        private static readonly Color HALO_LIGHT = new Color(0.8f, 0.91f, 1.0f, 0.6f);
        private static readonly Vector3 CHEST = Ground.Up(KnightLooks.CHEST_HEIGHT);

        private readonly int[] shields = new int[MAX_SHIELDS];
        private readonly float[] baseRadius = new float[MAX_SHIELDS];
        private readonly int[] enemyShots = new int[MAX_ENEMY_SHOTS];
        private readonly int shieldVisual;
        private readonly int reflectVisual;
        private readonly int flash;
        private readonly int glint;
        private readonly int crossFlare;
        private readonly int shockRing;
        private readonly int haloRing;
        private readonly FxComposite summonMotes;
        private readonly FxComposite blockBurst;
        private readonly FxComposite hitSparks;
        private readonly FxComposite fadeMotes;
        private int shieldCount;
        private bool isClearing;
        private float rimTimer;
        private int rimIndex;
        private float haloTimer;

        public AegisOrbit(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            shieldVisual = context.FindVisual("AegisShield");
            reflectVisual = context.FindVisual("AegisReflect");
            flash = effects.Find("KnightFlash");
            glint = effects.Find("KnightGlint");
            crossFlare = effects.Find("KnightCrossFlare");
            shockRing = effects.Find("KnightShockRing");
            haloRing = effects.Find("KnightHaloRing");
            summonMotes = Composite(new FxLayer("KnightMotes", 14));
            blockBurst = Composite(new FxLayer("KnightSteelSparks", 12), new FxLayer("KnightMotes", 4));
            hitSparks = Composite(new FxLayer("KnightSteelSparks", 3));
            fadeMotes = Composite(new FxLayer("KnightMotes", 6));
            Array.Fill(shields, -1);
        }

        /// <summary>Bastion Halo's shields stay for good (its Duration reads permanent on the card).</summary>
        private bool isPermanent => isEvolved;
        private int desiredCount => Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_SHIELDS);

        protected override bool usesCooldown => !isPermanent;

        protected override bool Cast(in CastInfo cast)
        {
            Summon();
            return true;
        }

        protected override void OnLevelChanged()
        {
            if (CountAlive() > 0)
            {
                Summon();
            }
        }

        /// <summary>The weapon leaves: its shields go at once, without their fade.</summary>
        protected override void OnDispose() => ClearShields();

        protected override void Update(float dt)
        {
            if (isPermanent && CountAlive() != desiredCount)
            {
                Summon();
            }
            if (CountAlive() == 0)
            {
                return;
            }
            Breathe();
            BlockEnemyShots();
            DrawRimGlint(dt);
            if (isEvolved)
            {
                DrawHalo(dt);
            }
        }

        private void Summon()
        {
            bool hadShields = CountAlive() > 0;
            ClearShields();
            int count = desiredCount;
            float radius = AreaOf(level[WeaponField.Radius]);
            float lifetime = isPermanent ? PERMANENT : DurationOf(level[WeaponField.Duration]);
            float speed = level[WeaponField.Speed] * Ground.DEGREES_TO_RADIANS;
            float shieldRadius = AreaOf(level[WeaponField.Size]);
            bool hasTwoRings = isEvolved;
            for (int i = 0; i < count; i++)
            {
                bool isInner = hasTwoRings && i % 2 == 1;
                ProjectileSpawn spawn = NewProjectile(shieldVisual, ProjectileMotion.Orbit, heroPosition, Vector3.zero, lifetime);
                spawn.orbitRadius = isInner ? radius * INNER_RING_SHARE : radius;
                spawn.orbitSpeed = isInner ? -speed : speed;
                spawn.orbitAngle = MathF.PI * 2.0f * i / count;
                spawn.damage = MightOf(level[WeaponField.Damage]);
                spawn.radius = shieldRadius;
                spawn.rehitInterval = level[WeaponField.Interval];
                spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
                spawn.handler = this;
                int row = Spawn(spawn);
                if (row >= 0)
                {
                    baseRadius[shieldCount] = spawn.orbitRadius;
                    shields[shieldCount++] = row;
                }
            }
            if (!hadShields)
            {
                PlaySummon(radius);
                PlayAt("aegis_summon", heroPosition);
            }
        }

        /// <summary>The shields arrive: a flash at the knight, a shockwave out to the orbit, rising motes, a glint at each.</summary>
        private void PlaySummon(float radius)
        {
            Vector3 chest = heroPosition + CHEST;
            Color color = isEvolved ? BASTION_AZURE : SHIELD_GOLD;
            EmitSized(flash, chest, color, SUMMON_FLASH_SIZE);
            EmitSized(shockRing, heroPosition + Ground.Up(FxSizes.GROUND_LIFT), color, radius * FxSizes.RING_SIZE_PER_RADIUS);
            summonMotes.Play(chest, FxSizes.METRE_UNIT, fxScale, SHIELD_GOLD);
            for (int i = 0; i < shieldCount; i++)
            {
                if (IsOwned(shields[i], this, shieldVisual))
                {
                    ref ProjectileSpawn shield = ref projectiles.Get(shields[i]);
                    Vector3 offset = new Vector3(MathF.Sin(shield.orbitAngle), 0.0f, MathF.Cos(shield.orbitAngle)) * shield.orbitRadius;
                    EmitSized(glint, heroPosition + offset + CHEST, KnightLooks.HOLY_LIGHT, SHIELD_GLINT_SIZE * fxScale);
                }
            }
        }

        private void ClearShields()
        {
            isClearing = true;
            for (int i = 0; i < shieldCount; i++)
            {
                ExpireOwned(ref shields[i], this, shieldVisual);
            }
            shieldCount = 0;
            isClearing = false;
        }

        private int CountAlive()
        {
            int alive = 0;
            for (int i = 0; i < shieldCount; i++)
            {
                if (IsOwned(shields[i], this, shieldVisual))
                {
                    alive++;
                }
            }
            return alive;
        }

        /// <summary>Swings each ring's radius in and out (rings opposite to each other).</summary>
        private void Breathe()
        {
            float swing = (Mathf.PingPong(clock * BREATH_RATE, 1.0f) * 2.0f - 1.0f) * BREATH_SHARE;
            for (int i = 0; i < shieldCount; i++)
            {
                if (!IsOwned(shields[i], this, shieldVisual))
                {
                    continue;
                }
                float sign = i % 2 == 0 ? 1.0f : -1.0f;
                projectiles.Get(shields[i]).orbitRadius = baseRadius[i] * (1.0f + swing * sign);
            }
        }

        /// <summary>
        /// A crisp gold glint on the upper leading rim of the next shield in turn; it moves with the shield for its short
        /// life, so it stays on the rim.
        /// </summary>
        private void DrawRimGlint(float dt)
        {
            rimTimer -= dt;
            if (rimTimer > 0.0f || shieldCount == 0)
            {
                return;
            }
            rimTimer = RIM_GLINT_PERIOD / shieldCount;
            rimIndex = (rimIndex + 1) % shieldCount;
            if (!IsOwned(shields[rimIndex], this, shieldVisual))
            {
                return;
            }
            ref ProjectileSpawn shield = ref projectiles.Get(shields[rimIndex]);
            Vector3 rim = shield.position + FlightOf(shield) * RIM_FORWARD + Ground.Up(RIM_HEIGHT);
            effects.EmitMoving(glint, rim, shield.velocity, 1, SHIELD_GOLD, RIM_GLINT_SIZE * fxScale, RIM_GLINT_SECONDS);
        }

        /// <summary>Bastion Halo: both orbits as thin rings of light at shield height, breathing with the shields.</summary>
        private void DrawHalo(float dt)
        {
            haloTimer -= dt;
            if (haloTimer > 0.0f)
            {
                return;
            }
            haloTimer = HALO_INTERVAL;
            float height = heroPosition.y + KnightLooks.CHEST_HEIGHT;
            float outer = 0.0f;
            float inner = 0.0f;
            for (int i = 0; i < shieldCount; i++)
            {
                if (!IsOwned(shields[i], this, shieldVisual))
                {
                    continue;
                }
                ref ProjectileSpawn shield = ref projectiles.Get(shields[i]);
                height = shield.position.y;
                if (i % 2 == 0 && outer <= 0.0f)
                {
                    outer = shield.orbitRadius;
                }
                else if (i % 2 == 1 && inner <= 0.0f)
                {
                    inner = shield.orbitRadius;
                }
            }
            var center = new Vector3(heroPosition.x, height, heroPosition.z);
            if (outer > 0.0f)
            {
                EmitShape(haloRing, center, HALO_LIGHT, outer * FxSizes.RING_SIZE_PER_RADIUS, HALO_SECONDS);
            }
            if (inner > 0.0f)
            {
                EmitShape(haloRing, center, HALO_LIGHT, inner * FxSizes.RING_SIZE_PER_RADIUS, HALO_SECONDS);
            }
        }

        /// <summary>Every enemy projectile touching a shield is destroyed (no detonation) and, for Bastion Halo, reflected.</summary>
        private void BlockEnemyShots()
        {
            for (int i = 0; i < shieldCount; i++)
            {
                if (!IsOwned(shields[i], this, shieldVisual))
                {
                    continue;
                }
                ref ProjectileSpawn shield = ref projectiles.Get(shields[i]);
                int shots = projectiles.QueryCircle(Team.Enemy, shield.position, shield.radius * shield.scale, enemyShots);
                for (int s = 0; s < shots; s++)
                {
                    int shot = enemyShots[s];
                    if (projectiles.IsAlive(shot))
                    {
                        Block(shot, projectiles.Get(shot).position);
                    }
                }
            }
        }

        private void Block(int shot, Vector3 position)
        {
            projectiles.Remove(shot);
            PlayBlock(position);
            PlayAt("aegis_block", position);
            if (isEvolved)
            {
                Reflect(position);
            }
        }

        /// <summary>A block: a flash, the holy cross flare, steel sparks and a few motes.</summary>
        private void PlayBlock(Vector3 position)
        {
            Color color = isEvolved ? BASTION_AZURE : SHIELD_GOLD;
            EmitSized(flash, position, color, BLOCK_FLASH_SIZE);
            EmitSized(crossFlare, position, KnightLooks.HOLY_LIGHT, BLOCK_CROSS_SIZE * fxScale);
            blockBurst.Play(position, FxSizes.METRE_UNIT, fxScale, SHIELD_GOLD);
        }

        private void Reflect(Vector3 from)
        {
            if (!enemies.QueryNearest(from, REFLECT_RANGE, out int target))
            {
                return;
            }
            Vector3 direction = Ground.Direction(from, enemies.GetPosition(target), facing);
            float speed = SpeedOf(level[WeaponField.SubSpeed]);
            ProjectileSpawn spawn = NewProjectile(reflectVisual, ProjectileMotion.Linear, from, direction * speed, REFLECT_RANGE / speed + REFLECT_LIFETIME_MARGIN);
            spawn.damage = MightOf(level[WeaponField.SubDamage]);
            spawn.radius = REFLECT_RADIUS;
            spawn.pierce = 0;
            spawn.knockback = KnockbackOf(REFLECT_KNOCKBACK);
            Spawn(spawn);
            EmitSized(glint, from, BASTION_AZURE, REFLECT_GLINT_SIZE * fxScale);
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn shield = ref world.Get(projectile);
            Vector3 position = enemies.GetPosition(enemyRow);
            Vector3 outward = Ground.Direction(heroPosition, position, facing);
            Vector3 tangent = Ground.Normalize(shield.velocity, outward);
            Vector3 push = Ground.Normalize(outward * OUTWARD_SHARE + tangent * TANGENT_SHARE, outward) * shield.knockback;
            if (strikes.Hit(enemyRow, shield.damage, push, slot) != DamageResult.Missed)
            {
                Vector3 chest = position + CHEST;
                EmitSized(glint, chest, isEvolved ? BASTION_AZURE : SHIELD_GOLD, HIT_GLINT_SIZE * fxScale);
                hitSparks.Play(chest, FxSizes.METRE_UNIT, fxScale, SHIELD_GOLD);
                PlayAt("aegis_bonk", position);
            }
        }

        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            if (!isClearing)
            {
                EmitSized(glint, position, KnightLooks.HOLY, FADE_GLINT_SIZE * fxScale);
                fadeMotes.Play(position, FxSizes.METRE_UNIT, fxScale, KnightLooks.HOLY);
            }
        }
    }
}

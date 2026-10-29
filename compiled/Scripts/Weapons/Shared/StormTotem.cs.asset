using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Storm Totem (shared, Rare): Count + Amount totems planted at the hero (the others SubRange away, 120 deg apart)
    /// for Duration (Duration stat) zap the nearest enemy within Range (Area) every Interval for Damage; the bolt
    /// chains on SubCount times to enemies within SubRadius it has not struck.
    /// Evolution Thunderspire (+ Quickening Hourglass): a floating rod circles the hero for good and zaps faster,
    /// further and wider.
    /// Look: a bolt from the sky plants each totem (flash, ground ring, static, sparks, dust); its tip crackles and
    /// glows and static crawls around its foot. Bolts are doubled lightning strands drawn segment by segment along a
    /// jagged line over a soft glow (LightningFx), and every target flashes with a ring, crackles and sparks.
    /// Thunderspire's rod crackles violet-white (SharedLooks); its bolts are tripled, violet-white and forked, and every
    /// few zaps a thunderclap strikes the first target from the sky.
    /// </summary>
    public sealed class StormTotem : Weapon
    {
        private const int MAX_TOTEMS = 6;
        private const int MAX_CHAIN = 8;
        private const float PLACE_SEARCH = 12.0f;
        /// <summary>Totems after the first stand this many degrees apart around the hero.</summary>
        private const float PLACE_SPREAD_DEGREES = 120.0f;
        private const float TOTEM_TIP_HEIGHT = 1.6f;
        private const float TARGET_HEIGHT = 1.0f;
        private const float ROD_ORBIT_RADIUS = 1.0f;
        private const float ROD_ORBIT_SPEED = 1.2f;
        /// <summary>Bolt segment length (m); the strand texture adds the fine jags.</summary>
        private const float BOLT_STEP = 1.1f;
        private const int BOLT_SEGMENTS_MAX = 10;
        private const float BOLT_JITTER = 0.3f;
        private const float BOLT_SECONDS = 0.16f;
        private const float THUNDER_SECONDS = 0.18f;
        private const int BOLT_STRANDS = 2;
        private const int THUNDER_STRANDS = 3;
        private const float FORK_CHANCE = 0.45f;
        private const float FORK_LENGTH_MIN = 0.5f;
        private const float FORK_LENGTH_MAX = 1.0f;
        private const float FORK_FORWARD = 0.6f;
        private const float FORK_DROP = 0.4f;
        /// <summary>Every this many rod zaps the first target is struck from the sky.</summary>
        private const int THUNDERCLAP_EVERY = 4;
        private const float CRACKLE_INTERVAL = 0.1f;
        private const float TIP_WEB_SIZE = 0.8f;
        private const float TIP_GLOW_SIZE = 0.9f;
        private const float BASE_STATIC_SIZE = 1.4f;
        private const float SOURCE_WEB_SIZE = 0.7f;
        private const float PLACE_HEIGHT = 0.8f;
        private const float PITCH_JITTER = 0.1f;

        /// <summary>The totems' static and glow: bluer than the bolts' core.</summary>
        private static readonly Color LIGHTNING = new Color(0.6f, 0.9f, 1.0f, 1.0f);

        private struct Totem
        {
            public int row;
            public Vector3 position;
            public float zapTimer;
            public float endTime;
        }

        private readonly Totem[] totems = new Totem[MAX_TOTEMS];
        private readonly Enemy[] chained = new Enemy[MAX_CHAIN + 1];
        private readonly int totemVisual;
        private readonly int spireVisual;
        private readonly int web;
        private readonly int tipGlow;
        private readonly int crawl;
        /// <summary>A totem's zap, and Thunderspire's tripled, forked violet-white bolt.</summary>
        private readonly BoltStyle zapBolt;
        private readonly BoltStyle thunderBolt;
        private readonly FxComposite place;
        private readonly FxComposite zapHit;
        private readonly FxComposite thunderHit;
        private readonly FxComposite thunderclap;
        private int totemCount;
        private int rod = -1;
        private float rodTimer;
        private float crackleTimer;
        private int rodZaps;

        public StormTotem(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            totemVisual = context.FindVisual("StormTotem");
            spireVisual = context.FindVisual("Thunderspire");
            zapBolt = new BoltStyle
            {
                strandEffect = effects.Find("SharedStormBolt"),
                glowEffect = effects.Find("SharedStormBoltGlow"),
                strands = BOLT_STRANDS,
                core = SharedLooks.LIGHTNING_CORE,
                glow = SharedLooks.LIGHTNING_GLOW,
                seconds = BOLT_SECONDS,
                step = BOLT_STEP,
                maxSegments = BOLT_SEGMENTS_MAX,
                jitter = BOLT_JITTER,
            };
            thunderBolt = zapBolt;
            thunderBolt.strands = THUNDER_STRANDS;
            thunderBolt.core = SharedLooks.THUNDER;
            thunderBolt.glow = SharedLooks.THUNDER_GLOW;
            thunderBolt.seconds = THUNDER_SECONDS;
            thunderBolt.branchChance = FORK_CHANCE;
            thunderBolt.branchForward = FORK_FORWARD;
            thunderBolt.branchLengthMin = FORK_LENGTH_MIN;
            thunderBolt.branchLengthMax = FORK_LENGTH_MAX;
            thunderBolt.branchDrop = FORK_DROP;
            web = effects.Find("SharedStormWeb");
            tipGlow = effects.Find("SharedStormGlow");
            crawl = effects.Find("SharedStormStatic");
            place = Composite(new FxLayer("SharedStormStrike", 3, 6.0f, true, true), new FxLayer("SharedFlash", 1, 1.8f),
                              FxLayer.Ground("SharedGroundRing", 3.0f), new FxLayer("SharedStormStatic", 2, 2.2f, true, true),
                              new FxLayer("SharedSparks", 16), new FxLayer("MeteorDust", 2, 0.5f, false));
            zapHit = Composite(new FxLayer("SharedFlash", 1, 1.0f), new FxLayer("SharedPing", 1, 1.0f), new FxLayer("SharedStormWeb", 1, 1.3f),
                               new FxLayer("SharedSparks", 8));
            thunderHit = Composite(new FxLayer("SharedFlash", 1, 0.85f), new FxLayer("SharedStormWeb", 2, 1.3f), new FxLayer("SharedSparks", 8));
            thunderclap = Composite(new FxLayer("SharedStormStrike", 2, 5.0f, true, true), FxLayer.Ground("SharedGroundRing", 2.4f),
                                    new FxLayer("SharedStormStatic", 1, 2.0f, true, true), new FxLayer("MeteorDust", 2, 0.45f, false));
        }

        protected override bool usesCooldown => !isEvolved;

        protected override bool Cast(in CastInfo cast)
        {
            if (!TryNearest(PLACE_SEARCH, out _))
            {
                return false;
            }
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_TOTEMS);
            float start = Ground.RandomYaw();
            for (int k = 0; k < count; k++)
            {
                Vector3 position = k == 0 ? heroPosition : heroPosition + Ground.FromYaw(start + PLACE_SPREAD_DEGREES * k) * level[WeaponField.SubRange];
                Place(Ground.Flat(position));
            }
            RequestAttack("CastRaise");
            PlayAt("totem_place", heroPosition);
            return true;
        }

        private void Place(Vector3 position)
        {
            int index;
            if (totemCount < MAX_TOTEMS)
            {
                index = totemCount++;
            }
            else
            {
                index = FindOldest();
                ExpireOwned(ref totems[index].row, ctx.visualOnly, totemVisual);
            }
            float duration = DurationOf(level[WeaponField.Duration]);
            totems[index] = new Totem
            {
                row = SpawnVisual(totemVisual, ProjectileMotion.Static, position, Vector3.zero, duration),
                position = position,
                zapTimer = Random.Range(0.0f, level[WeaponField.Interval]),
                endTime = clock + duration,
            };
            place.Play(position + Ground.Up(PLACE_HEIGHT), fxScale, fxScale, LIGHTNING);
        }

        protected override void Update(float dt)
        {
            crackleTimer -= dt;
            bool isCrackling = crackleTimer <= 0.0f;
            if (isCrackling)
            {
                crackleTimer = CRACKLE_INTERVAL;
            }
            for (int i = totemCount - 1; i >= 0; i--)
            {
                ref Totem totem = ref totems[i];
                if (clock >= totem.endTime)
                {
                    totems[i] = totems[--totemCount];
                    continue;
                }
                if (isCrackling)
                {
                    Crackle(totem.position);
                }
                totem.zapTimer -= dt;
                if (totem.zapTimer <= 0.0f)
                {
                    totem.zapTimer += level[WeaponField.Interval];
                    Zap(totem.position + Ground.Up(TOTEM_TIP_HEIGHT), false);
                }
            }
            if (isEvolved)
            {
                StepRod(dt);
            }
        }

        /// <summary>A standing totem: its tip crackles and glows, static crawls around its foot.</summary>
        private void Crackle(Vector3 position)
        {
            Vector3 tip = position + Ground.Up(TOTEM_TIP_HEIGHT);
            EmitSized(web, tip, SharedLooks.LIGHTNING_CORE, TIP_WEB_SIZE * fxScale);
            EmitSized(tipGlow, tip, LIGHTNING, TIP_GLOW_SIZE * fxScale);
            EmitSized(crawl, position + Ground.Up(FxSizes.GROUND_LIFT), LIGHTNING, BASE_STATIC_SIZE * fxScale);
        }

        private void StepRod(float dt)
        {
            if (!IsOwned(rod, ctx.visualOnly, spireVisual))
            {
                ProjectileSpawn spawn = NewProjectile(spireVisual, ProjectileMotion.Orbit, heroPosition, Vector3.zero, PERMANENT);
                spawn.orbitRadius = ROD_ORBIT_RADIUS;
                spawn.orbitSpeed = ROD_ORBIT_SPEED;
                spawn.handler = ctx.visualOnly;
                rod = Spawn(spawn);
                if (rod < 0)
                {
                    return;
                }
            }
            rodTimer -= dt;
            if (rodTimer <= 0.0f)
            {
                rodTimer += level[WeaponField.Interval];
                Zap(projectiles.Get(rod).position, true);
            }
        }

        /// <summary>A bolt from <paramref name="origin"/> to the nearest enemy, then chained jumps to unhit ones.</summary>
        private void Zap(Vector3 origin, bool isThunder)
        {
            if (!enemies.QueryNearest(origin, AreaOf(level[WeaponField.Range]), out int row))
            {
                return;
            }
            int jumps = Math.Min(level.GetCount(WeaponField.SubCount), MAX_CHAIN);
            float damage = DamageOf(WeaponField.Damage, CastInfo.normal);
            float knockback = KnockbackOf(level[WeaponField.Knockback]);
            bool isClap = isThunder && ++rodZaps % THUNDERCLAP_EVERY == 0;
            Color core = isThunder ? SharedLooks.THUNDER : SharedLooks.LIGHTNING_CORE;
            EmitSized(web, origin, core, SOURCE_WEB_SIZE * fxScale);
            Vector3 from = origin;
            int count = 0;
            while (true)
            {
                Vector3 to = enemies.GetPosition(row) + Ground.Up(TARGET_HEIGHT);
                chained[count++] = enemies.GetEnemy(row);
                LightningFx.DrawBolt(effects, from, to, isThunder ? thunderBolt : zapBolt);
                strikes.Hit(row, damage, Combat.GetPushFrom(from, to, knockback), slot);
                (isThunder ? thunderHit : zapHit).Play(to, fxScale, fxScale, core);
                if (isClap && count == 1)
                {
                    thunderclap.Play(Ground.Flat(to) + Ground.Up(PLACE_HEIGHT), fxScale, fxScale, SharedLooks.THUNDER);
                }
                if (count > jumps || !strikes.FindNearestExcept(to, level[WeaponField.SubRadius], chained.AsSpan(0, count), out row))
                {
                    break;
                }
                from = to;
            }
            PlayAt("totem_zap", origin, 1.0f, Random.Range(1.0f - PITCH_JITTER, 1.0f + PITCH_JITTER));
        }

        private int FindOldest()
        {
            int oldest = 0;
            for (int i = 1; i < totemCount; i++)
            {
                if (totems[i].endTime < totems[oldest].endTime)
                {
                    oldest = i;
                }
            }
            return oldest;
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Arc Lash (Mage, Uncommon): bolts (SubCount + Amount) leap from the mage's hand to the nearest enemies within
    /// Range and jump on from enemy to enemy (Count jumps after the first strike, each within SubRange), one jump every
    /// Interval (faster with Projectile Speed) so the chain visibly travels. Frozen enemies take +Bonus damage and
    /// shatter (they stay frozen); a burning enemy passes a burn (Burn per BurnTick for SubDuration, Duration stat) on
    /// to the next jump, which carries it on down the chain.
    /// Evolution Tempest Crown (+ Echo Quiver): a storm crown circles the mage's head and hurls a long bolt every
    /// Cooldown from the crown point facing its target; the bolt forks in two at every frozen or burning enemy (at most
    /// <see cref="MAX_STRIKES"/> strikes per bolt).
    /// Look: jagged bolts drawn segment by segment from doubled branching lightning strands over a soft glow, arching
    /// over the crowd, with short side branches (LightningFx); every strike flashes, crackles, throws sparks and leaves
    /// static clinging to the body and crawling on the ground. Shatters burst in ice shards; burn-carrying jumps run
    /// orange and spit fire. The crown is a ring of gold sparks over a band of light, crackling arcs between its
    /// points. Echo casts strike in violet.
    /// </summary>
    public sealed class ArcLash : Weapon
    {
        private const int STRIKE = 0;
        private const int MAX_BOLTS = 6;
        private const int MAX_CHAINS = 32;
        /// <summary>Strikes one bolt can make, forks included.</summary>
        private const int MAX_STRIKES = 20;
        private const int FORKS = 2;
        private const float LAUNCH_HEIGHT = 1.3f;
        private const float LAUNCH_FORWARD = 0.5f;
        /// <summary>Delay between the bolts of one cast.</summary>
        private const float BOLT_STAGGER = 0.07f;
        /// <summary>Bolts strike this share of an enemy's body height.</summary>
        private const float BODY_SHARE = 0.9f;
        private const float KNOCKBACK = 0.15f;
        /// <summary>The strand emitters' stretch_speed_scale: a strand's length (m) is its speed (m/s) x this.</summary>
        private const float STRETCH = 20.0f;
        /// <summary>Bolt segment length (m); the strand texture adds the fine jags and branches.</summary>
        private const float BOLT_STEP = 0.85f;
        private const int BOLT_SEGMENTS_MAX = 10;
        private const float BOLT_JITTER = 0.3f;
        private const float BOLT_SECONDS = 0.26f;
        /// <summary>A strand's width as a share of its segment's length (the strand tiles are 1:4).</summary>
        private const float STRAND_WIDTH = 0.7f;
        private const float GLOW_WIDTH = 0.6f;
        /// <summary>Legs arch over the crowd: their middle rises this share of their length (at most ARCH_MAX m).</summary>
        private const float ARCH_SHARE = 0.18f;
        private const float ARCH_MAX = 0.9f;
        private const int STRANDS = 2;
        private const int GRAND_STRANDS = 3;
        private const float BRANCH_CHANCE = 0.4f;
        private const float GRAND_BRANCH_CHANCE = 0.65f;
        private const float BRANCH_LENGTH_MIN = 0.4f;
        private const float BRANCH_LENGTH_MAX = 0.9f;
        private const float BRANCH_FORWARD = 0.7f;
        private const float BRANCH_DROP = 0.35f;
        private const float MUZZLE_WEB_SIZE = 0.6f;
        private const float MUZZLE_FLASH_SIZE = 0.7f;
        private const float NODE_SIZE = 0.9f;
        private const float PITCH_JITTER = 0.08f;
        private const float BOLT_PITCH = 1.15f;
        private const float CROWN_PITCH = 1.3f;
        private const float CROWN_VOLUME = 0.7f;
        private const float SHATTER_VOLUME = 0.8f;
        // Tempest Crown.
        private const int CROWN_POINTS = 5;
        private const float CROWN_HEIGHT = 2.35f;
        private const float CROWN_RADIUS = 0.5f;
        /// <summary>Degrees per second the crown turns.</summary>
        private const float CROWN_TURN_SPEED = 220.0f;
        private const float CROWN_BOB = 0.06f;
        private const float CROWN_BOB_SPEED = 7.0f;
        private const float CROWN_SPARK_SIZE = 0.32f;
        private const float CROWN_HALO_SHARE = 2.3f;
        private const float CROWN_HALO_SECONDS = 0.05f;
        private const float CROWN_ARC_INTERVAL = 0.07f;
        private const float CROWN_ARC_SECONDS = 0.09f;
        private const float CROWN_ARC_WIDTH = 0.18f;

        private static readonly Color ECHO_CORE = new Color(0.88f, 0.78f, 1.0f, 1.0f);
        private static readonly Color BURN_CORE = new Color(1.0f, 0.88f, 0.65f, 1.0f);
        private static readonly Color BURN_GLOW = new Color(1.0f, 0.45f, 0.12f, 1.0f);
        private static readonly Color STORM_GLOW = new Color(0.35f, 0.45f, 1.0f, 1.0f);
        private static readonly Color CROWN_GOLD = new Color(1.0f, 0.85f, 0.5f, 1.0f);
        private static readonly Color ICE = new Color(0.75f, 0.93f, 1.0f, 1.0f);

        /// <summary>One bolt's memory: the enemies it struck or is about to strike, and its jumps in flight.</summary>
        private sealed class Chain
        {
            public readonly Enemy[] claimed = new Enemy[MAX_STRIKES];
            public int count;
            public int pending;
            public bool isActive;
        }

        private readonly Chain[] chains = new Chain[MAX_CHAINS];
        private readonly int[] firstTargets = new int[MAX_BOLTS];
        private readonly int node;
        private readonly int flash;
        private readonly int web;
        private readonly int crownSpark;
        private readonly int crownHalo;
        /// <summary>The bolts' strands and glow; the colours, jag and widths are set per strike.</summary>
        private readonly BoltStyle boltStyle;
        private readonly FxComposite strikeHit;
        private readonly FxComposite shatter;
        private readonly FxComposite ignite;
        private float crownAngle;
        private float crownArcTimer;

        public ArcLash(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            for (int i = 0; i < MAX_CHAINS; i++)
            {
                chains[i] = new Chain();
            }
            node = effects.Find("MageArcNode");
            flash = effects.Find("MageArcFlash");
            web = effects.Find("MageArcWeb");
            crownSpark = effects.Find("MageCrownSpark");
            crownHalo = effects.Find("MageCrownHalo");
            boltStyle = new BoltStyle
            {
                strandEffect = effects.Find("MageArcBolt"),
                glowEffect = effects.Find("MageArcGlow"),
                stretch = STRETCH,
                seconds = BOLT_SECONDS,
                step = BOLT_STEP,
                maxSegments = BOLT_SEGMENTS_MAX,
                archShare = ARCH_SHARE,
                archMax = ARCH_MAX,
                branchForward = BRANCH_FORWARD,
                branchDrop = BRANCH_DROP,
            };
            strikeHit = Composite(new FxLayer("MageArcFlash", 1, 0.8f), new FxLayer("MageArcWeb", 1, 1.25f), new FxLayer("MageArcSparks", 7),
                                  new FxLayer("MageArcCling", 2), FxLayer.Ground("MageArcStatic", 1.4f));
            shatter = Composite(new FxLayer("MageArcFlash", 1, 1.4f), new FxLayer("MageArcShatter", 10), new FxLayer("IceShatter", 5, 0.0f, false),
                                new FxLayer("FrostSnow", 4));
            ignite = Composite(new FxLayer("FireSparks", 6), new FxLayer("FireEmbers", 3));
        }

        protected override bool Cast(in CastInfo cast)
        {
            int bolts = Math.Min(level.GetCount(WeaponField.SubCount) + amount, MAX_BOLTS);
            int found = FindNearest(level[WeaponField.Range], firstTargets, bolts);
            if (found == 0)
            {
                return false;
            }
            Color core = CoreColor(cast, false);
            for (int b = 0; b < bolts; b++)
            {
                int row = firstTargets[b % found];
                if (b >= found && TryRandomInView(level[WeaponField.Range], out int spare))
                {
                    row = spare;
                }
                Vector3 origin = isEvolved ? CrownPointToward(enemies.GetPosition(row)) : HandPoint();
                if (StartBolt(origin, row, cast, BOLT_STAGGER * b))
                {
                    EmitSized(isEvolved ? node : web, origin, isEvolved ? CROWN_GOLD : core, (isEvolved ? NODE_SIZE : MUZZLE_WEB_SIZE) * fxScale);
                }
            }
            if (isEvolved)
            {
                PlayAt("totem_zap", heroPosition, CROWN_VOLUME, CROWN_PITCH + Random.Range(-PITCH_JITTER, PITCH_JITTER));
            }
            else
            {
                EmitSized(flash, HandPoint(), core, MUZZLE_FLASH_SIZE * fxScale);
                PlayAt("totem_zap", heroPosition, 1.0f, BOLT_PITCH + Random.Range(-PITCH_JITTER, PITCH_JITTER));
                RequestAttack("CastShoot");
            }
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == STRIKE)
            {
                Strike(e);
            }
        }

        /// <summary>Tempest Crown: the crown circles the head every frame, crackling between its points.</summary>
        protected override void Update(float dt)
        {
            if (!isEvolved || !hero.isAlive)
            {
                return;
            }
            crownAngle = (crownAngle + CROWN_TURN_SPEED * dt) % Ground.FULL_TURN;
            Vector3 center = CrownCenter();
            float bob = MathF.Sin(clock * CROWN_BOB_SPEED) * CROWN_BOB;
            for (int k = 0; k < CROWN_POINTS; k++)
            {
                EmitSized(crownSpark, CrownPoint(center, k, bob), CROWN_GOLD, CROWN_SPARK_SIZE * fxScale);
            }
            EmitShape(crownHalo, center, MageLooks.LIGHTNING_GLOW, CROWN_RADIUS * CROWN_HALO_SHARE, CROWN_HALO_SECONDS);
            crownArcTimer -= dt;
            if (crownArcTimer <= 0.0f)
            {
                crownArcTimer = CROWN_ARC_INTERVAL;
                int k = Random.Range(0, CROWN_POINTS);
                LightningFx.DrawSegment(effects, CrownPoint(center, k, bob), CrownPoint(center, (k + 1) % CROWN_POINTS, -bob), GetCrownArcStyle(), 1);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Chains
        // -------------------------------------------------------------------------------------------------------

        /// <summary>A jump of bolt <paramref name="chain"/> from <paramref name="from"/> onto <paramref name="target"/>, carrying a burn (per second, 0 for none).</summary>
        private static WeaponEvent StrikeEvent(int chain, Vector3 from, Enemy target, int jumpsLeft, float burnPerSecond, in CastInfo cast)
        {
            return new WeaponEvent { kind = STRIKE, index = chain, origin = from, target = target, count = jumpsLeft, value = burnPerSecond, cast = cast };
        }

        private bool StartBolt(Vector3 origin, int row, in CastInfo cast, float delay)
        {
            int index = AllocateChain();
            if (index < 0)
            {
                return false;
            }
            Chain chain = chains[index];
            chain.claimed[chain.count++] = enemies.GetEnemy(row);
            if (!QueueStrike(chain, StrikeEvent(index, origin, enemies.GetEnemy(row), level.GetCount(WeaponField.Count), 0.0f, cast), delay))
            {
                chain.isActive = false;
                return false;
            }
            return true;
        }

        /// <summary>
        /// A jump lands: the enemy is struck (frozen ones shatter for more), takes any burn the bolt carries, and the
        /// bolt jumps on, carrying a burn when this enemy burned and forking at frozen or burning enemies when evolved.
        /// A target that died meanwhile is replaced by the nearest enemy the bolt has not claimed.
        /// </summary>
        private void Strike(in WeaponEvent e)
        {
            Chain chain = chains[e.index];
            chain.pending--;
            int row = enemies.GetRow(e.target);
            if (row < 0 && !TryClaimNearest(chain, e.origin, out row))
            {
                ReleaseIfDone(chain);
                return;
            }
            Vector3 to = BodyPoint(row);
            bool isFrozen = enemies.IsFrozen(row);
            bool isBurning = enemies.IsBurning(row);
            float carriedBurn = e.value;
            bool isHot = carriedBurn > 0.0f;
            LightningFx.DrawBolt(effects, e.origin, to, GetBoltStyle(e.cast, isHot));
            float damage = DamageOf(WeaponField.Damage, e.cast) * (isFrozen ? 1.0f + level[WeaponField.Bonus] : 1.0f);
            var effect = new HitEffect { numberKind = NumberKind(e.cast, isFrozen ? DamageKind.Frost : DamageKind.Normal) };
            strikes.Hit(row, damage, Combat.GetPushFrom(e.origin, to, KnockbackOf(KNOCKBACK)), slot, effect);
            if (isHot && enemies.IsTargetable(row))
            {
                enemies.Burn(row, carriedBurn, DurationOf(level[WeaponField.SubDuration]), slot);
            }
            PlayStrike(to, e.cast, isFrozen, isHot);
            float passOn = isBurning || isHot ? BurnPerSecondOf(e.cast) : 0.0f;
            if (e.count > 0)
            {
                Jump(chain, e, to, e.count - 1, passOn, isEvolved && (isFrozen || isBurning));
            }
            ReleaseIfDone(chain);
        }

        private void Jump(Chain chain, in WeaponEvent from, Vector3 at, int jumpsLeft, float burn, bool forks)
        {
            int branches = forks ? FORKS : 1;
            int launched = 0;
            float delay = level[WeaponField.Interval] / SpeedOf(1.0f);
            for (int f = 0; f < branches; f++)
            {
                if (!TryClaimNearest(chain, at, out int next))
                {
                    break;
                }
                if (QueueStrike(chain, StrikeEvent(from.index, at, enemies.GetEnemy(next), jumpsLeft, burn, from.cast), delay))
                {
                    launched++;
                }
            }
            if (launched > 1)
            {
                EmitSized(node, at, Tint(from.cast, CROWN_GOLD), NODE_SIZE * fxScale);
            }
        }

        private bool QueueStrike(Chain chain, in WeaponEvent strike, float delay)
        {
            bool isQueued = Schedule(delay, strike);
            if (isQueued)
            {
                chain.pending++;
            }
            return isQueued;
        }

        /// <summary>The nearest enemy within jump range the bolt has not claimed yet, claimed now.</summary>
        private bool TryClaimNearest(Chain chain, Vector3 at, out int row)
        {
            row = -1;
            if (chain.count >= MAX_STRIKES)
            {
                return false;
            }
            if (!strikes.FindNearestExcept(at, level[WeaponField.SubRange], chain.claimed.AsSpan(0, chain.count), out row))
            {
                return false;
            }
            chain.claimed[chain.count++] = enemies.GetEnemy(row);
            return true;
        }

        private int AllocateChain()
        {
            for (int i = 0; i < MAX_CHAINS; i++)
            {
                Chain chain = chains[i];
                if (!chain.isActive)
                {
                    chain.isActive = true;
                    chain.count = 0;
                    chain.pending = 0;
                    return i;
                }
            }
            return -1;
        }

        private static void ReleaseIfDone(Chain chain)
        {
            if (chain.pending <= 0)
            {
                chain.isActive = false;
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Look
        // -------------------------------------------------------------------------------------------------------

        private void PlayStrike(Vector3 at, in CastInfo cast, bool isFrozen, bool isHot)
        {
            strikeHit.Play(at, fxScale, fxScale, CoreColor(cast, isHot));
            if (isFrozen)
            {
                shatter.Play(at, fxScale, fxScale, ICE);
                PlayAt("frost_shatter", at, SHATTER_VOLUME);
            }
            if (isHot)
            {
                ignite.Play(at, fxScale, fxScale, MageLooks.FIRE);
            }
        }

        /// <summary>A strike's bolt: its colours (echo, burn, storm), Tempest Crown's extra strand and branches, all grown by fxScale.</summary>
        private BoltStyle GetBoltStyle(in CastInfo cast, bool isHot)
        {
            BoltStyle style = boltStyle;
            style.core = CoreColor(cast, isHot);
            style.glow = GlowColor(cast, isHot);
            style.strands = isEvolved ? GRAND_STRANDS : STRANDS;
            style.branchChance = isEvolved ? GRAND_BRANCH_CHANCE : BRANCH_CHANCE;
            style.jitter = BOLT_JITTER * fxScale;
            style.branchLengthMin = BRANCH_LENGTH_MIN * fxScale;
            style.branchLengthMax = BRANCH_LENGTH_MAX * fxScale;
            style.strandWidthShare = STRAND_WIDTH * fxScale;
            style.glowWidth = GLOW_WIDTH * fxScale;
            return style;
        }

        /// <summary>The crown's crackle: one thin strand between two points.</summary>
        private BoltStyle GetCrownArcStyle()
        {
            BoltStyle style = boltStyle;
            style.core = MageLooks.LIGHTNING_CORE;
            style.glow = MageLooks.LIGHTNING_GLOW;
            style.seconds = CROWN_ARC_SECONDS;
            style.strandWidth = CROWN_ARC_WIDTH;
            style.glowWidth = GLOW_WIDTH * fxScale;
            return style;
        }

        private Color CoreColor(in CastInfo cast, bool isHot) => cast.isEcho ? ECHO_CORE : isHot ? BURN_CORE : MageLooks.LIGHTNING_CORE;

        private Color GlowColor(in CastInfo cast, bool isHot) => cast.isEcho ? ECHO_TINT : isHot ? BURN_GLOW : isEvolved ? STORM_GLOW : MageLooks.LIGHTNING_GLOW;

        private Vector3 BodyPoint(int row) => enemies.GetPosition(row) + Ground.Up(enemies.GetHeight(row) * BODY_SHARE);

        private Vector3 HandPoint() => heroPosition + facing * LAUNCH_FORWARD + Ground.Up(LAUNCH_HEIGHT);

        private Vector3 CrownCenter() => heroPosition + Ground.Up(CROWN_HEIGHT);

        private Vector3 CrownPoint(Vector3 center, int k, float bob)
        {
            Vector3 offset = Ground.RingDirection(crownAngle, k, CROWN_POINTS) * CROWN_RADIUS;
            return center + offset + Ground.Up(k % 2 == 0 ? bob : -bob);
        }

        /// <summary>The point of the crown's ring that faces a target.</summary>
        private Vector3 CrownPointToward(Vector3 target)
        {
            Vector3 center = CrownCenter();
            return center + Ground.Direction(center, target, facing) * CROWN_RADIUS;
        }
    }
}

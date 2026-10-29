using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Dawn Hammer (Knight, Rare): spinning golden hammers (Count + Amount) are thrown 0.08 s apart at the nearest
    /// enemies, fly out Range m at Speed (Projectile Speed) and come back to the knight, hitting and stunning (Stun,
    /// Duration stat) everything they pass on both ways, each enemy once per way. A hammer that strikes an enemy that is
    /// already stunned (by Shield Rush, a Verdict slam, another hammer or its own way out) calls a thunderclap onto it
    /// 0.12 s later: SubDamage in SubRadius (Area). Each hammer calls at most one thunderclap on its way out and one on
    /// its way back.
    /// Evolution Skybreaker (+ Lucky Knucklebone): more hammers; any hit may also call the thunder (Chance plus half
    /// the Luck, in percent), and every thunderclap chains to SubCount more enemies beyond it, each struck by its own
    /// smaller bolt.
    /// Look: the knight throws with a glint and a flash at his hand. Each hammer is a glowing gold maul head and its
    /// gold haft (a visual twin carried on the head) spinning flat together, inside a halo, a golden whirl and falling
    /// motes (KnightLooks). A hit flashes a star and steel sparks and leaves a golden daze swirl over the stunned enemy.
    /// A coming thunderclap marks its spot: a ring closing in, crackling light and a star high above. The clap: two
    /// strands of lightning from the sky over a glowing column, a flash that lights the moor, a shockwave, a scorch and
    /// crawling lightning on the ground, sparks, rubble, dust and motes, with a camera shake and a short hit stop. The
    /// hammer is caught with a glint. Skybreaker's hammers are larger and paler, crackling with storm light in an azure
    /// halo; its thunder turns white-azure with a third strand, and its chains jump as forked lightning to the next
    /// enemies, each struck by a narrower bolt.
    /// </summary>
    public sealed class DawnHammer : Weapon
    {
        private const int THROW = 0;
        private const int THUNDER = 1;
        private const int CHAIN = 2;
        private const int MAX_HAMMERS = 10;
        private const int OUT_PASS = 0;
        private const int BACK_PASS = 1;
        private const int STRUCK_CAPACITY = 256;
        private const int CHAIN_CANDIDATES = 24;
        /// <summary>Hammers aim at enemies a little beyond the turning point too.</summary>
        private const float TARGET_REACH = 2.0f;
        private const float THROW_STAGGER = 0.08f;
        /// <summary>Spare hammers (more hammers than targets) fan out from the first one, this far apart.</summary>
        private const float SPREAD_DEGREES = 30.0f;
        /// <summary>A hammer flies its range out and the same back.</summary>
        private const float OUT_AND_BACK = 2.0f;
        private const float MIN_SPEED = 1e-3f;
        private const float HAMMER_RADIUS = 0.6f;
        private const float THROW_HEIGHT = 1.0f;
        private const float THROW_OUT = 0.5f;
        private const float THUNDER_DELAY = 0.12f;
        private const float THUNDER_KNOCKBACK = 1.0f;
        private const float LUCK_CHANCE_PER_POINT = 0.005f;
        private const float CHAIN_DELAY = 0.09f;
        private const float CHAIN_RANGE = 7.0f;
        /// <summary>A chain jumps to an enemy at least this share of the clap radius away from the strikes before it.</summary>
        private const float CHAIN_GAP_SHARE = 0.8f;
        private const float CHAIN_RADIUS_SHARE = 0.65f;
        private const float SHAKE_GAP = 0.2f;
        private const float THUNDER_TRAUMA = 0.22f;
        private const float CHAIN_TRAUMA = 0.1f;
        private const float HIT_STOP_SECONDS = 0.035f;
        private const float HIT_STOP_GAP = 1.2f;

        private const float HAND_HEIGHT = 1.3f;
        private const float HAND_FORWARD = 0.4f;
        private const float THROW_GLINT_SIZE = 1.1f;
        private const float THROW_FLASH_SIZE = 0.8f;
        /// <summary>The haft only shows: it touches nothing.</summary>
        private const float HAFT_RADIUS = 0.01f;
        /// <summary>The golden whirl around the head (twirl_01, its ring at 0.42 of the size) reaches the haft's end.</summary>
        private const float SWIRL_SIZE = 2.2f;
        /// <summary>Skybreaker's larger hammer whirls wider.</summary>
        private const float SKY_SWIRL_SCALE = 1.15f;
        private const float SWIRL_SECONDS = 0.06f;
        /// <summary>A slow drift that turns the flat whirl to the spin's angle (align_to_direction).</summary>
        private const float SWIRL_DRIFT = 0.05f;
        /// <summary>Turns the whirl's bright head onto the haft, so its tail trails behind the spin.</summary>
        private const float SWIRL_PHASE = 180.0f;
        /// <summary>
        /// The whirl is re-drawn every frame and overlaps for its short life: its alpha follows the frame time against
        /// this rate, so it reads the same at any frame rate or time scale.
        /// </summary>
        private const float REDRAW_RATE = 60.0f;
        private const float HIT_GLINT_SIZE = 0.8f;
        private const float DAZE_SIZE = 0.75f;
        private const float DAZE_ABOVE = 0.15f;
        private const float SWARM_HEIGHT = 0.3f;
        private const float RING_SECONDS = 0.4f;
        private const float MARK_LINGER = 0.03f;
        private const float CHARGE_CRACKLE_SIZE = 1.3f;
        /// <summary>Where the bolt comes from: a star high above the mark.</summary>
        private const float SKY_HEIGHT = 9.0f;
        private const float SKY_STAR_SIZE = 1.8f;
        private const float SKY_FLARE_SIZE = 3.0f;
        private const float BOLT_HEIGHT = 14.0f;
        private const float CHAIN_BOLT_HEIGHT = 11.0f;
        private const int BOLT_STRANDS = 2;
        private const int SKY_BOLT_STRANDS = 3;
        private const int CHAIN_BOLT_STRANDS = 1;
        private const float FLASH_PER_RADIUS = 0.8f;
        private const float SCORCH_PER_RADIUS = 1.5f;
        private const float SCORCH_SECONDS = 2.5f;
        private const float WEB_PER_RADIUS = 1.9f;
        /// <summary>Chain lightning between two strikes, at this height: segment length (m), jag, strands and life.</summary>
        private const float ARC_HEIGHT = 1.0f;
        private const float ARC_STEP = 1.1f;
        private const int ARC_SEGMENTS_MAX = 10;
        private const float ARC_JITTER = 0.3f;
        private const float ARC_SECONDS = 0.2f;
        private const int ARC_STRANDS = 2;
        private const float CATCH_GLINT_SIZE = 0.9f;
        private const float THROW_VOLUME = 0.75f;
        private const float THROW_PITCH = 0.8f;
        private const float HIT_VOLUME = 0.8f;
        private const float HIT_PITCH = 0.75f;
        private const float CRACK_PITCH = 0.8f;
        private const float BOOM_VOLUME = 0.75f;
        private const float CHAIN_VOLUME = 0.7f;
        private const float CHAIN_PITCH = 1.05f;

        private static readonly Color SWIRL = new Color(1.0f, 0.84f, 0.5f, 1.0f);
        private static readonly Color SKY_SWIRL = new Color(0.9f, 0.93f, 1.0f, 1.0f);
        private static readonly Color THUNDER_CORE = new Color(1.0f, 0.95f, 0.84f, 1.0f);
        private static readonly Color THUNDER_GLOW = new Color(1.0f, 0.76f, 0.38f, 1.0f);
        /// <summary>Skybreaker's accent: white-azure storm light.</summary>
        private static readonly Color STORM_CORE = new Color(0.86f, 0.94f, 1.0f, 1.0f);
        private static readonly Color STORM_GLOW = new Color(0.52f, 0.76f, 1.0f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.GROUND_LIFT);
        private static readonly Vector3 CHEST = Ground.Up(KnightLooks.CHEST_HEIGHT);

        private readonly int[] hammerRows = new int[MAX_HAMMERS];
        private readonly int[] hammerVisuals = new int[MAX_HAMMERS];
        /// <summary>Per hammer, its haft: a visual-only twin carried on the head (same spin).</summary>
        private readonly int[] haftRows = new int[MAX_HAMMERS];
        private readonly int[] haftVisuals = new int[MAX_HAMMERS];
        private readonly float[] turnAges = new float[MAX_HAMMERS];
        private readonly int[] passes = new int[MAX_HAMMERS];
        /// <summary>Per hammer, a bit per way (out, back) that has already called its thunderclap.</summary>
        private readonly int[] thunderPasses = new int[MAX_HAMMERS];
        /// <summary>Per hammer, the enemies it hit on its current way (each is hit once per way).</summary>
        private readonly HashSet<Enemy>[] struck = new HashSet<Enemy>[MAX_HAMMERS];
        private readonly int[] targetRows = new int[MAX_HAMMERS];
        private readonly int[] chainRows = new int[CHAIN_CANDIDATES];
        private readonly int hammerLook;
        private readonly int skyHammerLook;
        private readonly int haftLook;
        private readonly int skyHaftLook;
        private readonly int swirl;
        private readonly int daze;
        private readonly int mark;
        private readonly int crackle;
        private readonly int bolt;
        private readonly int boltGlow;
        private readonly int web;
        private readonly int scorch;
        private readonly int flash;
        private readonly int glint;
        private readonly int shockRing;
        private readonly BoltStyle chainArc;
        private readonly FxComposite hitSparks;
        private readonly FxComposite thunderBurst;
        private readonly FxComposite chainBurst;
        private readonly FxComposite catchMotes;
        private Throttle shakeThrottle = new Throttle(SHAKE_GAP);
        private Throttle hitStopThrottle = new Throttle(HIT_STOP_GAP);

        public DawnHammer(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            hammerLook = context.FindVisual("KnightDawnHammer");
            skyHammerLook = context.FindVisual("KnightSkyHammer");
            haftLook = context.FindVisual("KnightDawnHaft");
            skyHaftLook = context.FindVisual("KnightSkyHaft");
            swirl = effects.Find("KnightHammerSwirl");
            daze = effects.Find("KnightDaze");
            mark = effects.Find("KnightThunderMark");
            crackle = effects.Find("KnightHammerCrackle");
            bolt = effects.Find("KnightThunderBolt");
            boltGlow = effects.Find("KnightThunderGlow");
            web = effects.Find("KnightThunderWeb");
            scorch = effects.Find("ScorchMark");
            flash = effects.Find("KnightFlash");
            glint = effects.Find("KnightGlint");
            shockRing = effects.Find("KnightShockRing");
            chainArc = new BoltStyle
            {
                strandEffect = effects.Find("SharedStormBolt"),
                glowEffect = effects.Find("SharedStormBoltGlow"),
                strands = ARC_STRANDS,
                core = STORM_CORE,
                glow = STORM_GLOW,
                seconds = ARC_SECONDS,
                step = ARC_STEP,
                maxSegments = ARC_SEGMENTS_MAX,
                jitter = ARC_JITTER,
            };
            hitSparks = Composite(new FxLayer("KnightSteelSparks", 6));
            thunderBurst = Composite(new FxLayer("KnightThunderSparks", 14), new FxLayer("KnightSteelSparks", 10),
                                     new FxLayer("KnightDust", 3, 0.35f, false, true), new FxLayer("MeteorRocks", 6, 0.0f, false),
                                     new FxLayer("KnightMotes", 8));
            chainBurst = Composite(new FxLayer("KnightThunderSparks", 8), new FxLayer("KnightDust", 2, 0.45f, false, true),
                                   new FxLayer("KnightMotes", 4));
            catchMotes = Composite(new FxLayer("KnightMotes", 5));
            for (int i = 0; i < MAX_HAMMERS; i++)
            {
                hammerRows[i] = -1;
                haftRows[i] = -1;
                struck[i] = new HashSet<Enemy>(STRUCK_CAPACITY);
            }
        }

        protected override bool Cast(in CastInfo cast)
        {
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_HAMMERS);
            int found = FindNearest(level[WeaponField.Range] + TARGET_REACH, targetRows, count);
            if (found == 0)
            {
                return false;
            }
            Vector3 first = Ground.Direction(heroPosition, enemies.GetPosition(targetRows[0]), facing);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = i < found
                    ? Ground.Direction(heroPosition, enemies.GetPosition(targetRows[i]), first)
                    : Ground.Fan(first, i - found + 1, SPREAD_DEGREES);
                Schedule(THROW_STAGGER * i, new WeaponEvent { kind = THROW, direction = direction, cast = cast });
            }
            RequestAttack("Throw");
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            switch (e.kind)
            {
                case THROW:
                    Throw(e);
                    break;
                case THUNDER:
                    Thunderclap(e);
                    break;
                case CHAIN:
                    ChainStrike(e);
                    break;
            }
        }

        protected override void Update(float dt)
        {
            float alpha = Math.Clamp(dt * REDRAW_RATE, 0.0f, 1.0f);
            for (int i = 0; i < MAX_HAMMERS; i++)
            {
                if (hammerRows[i] < 0)
                {
                    continue;
                }
                if (!IsOwned(hammerRows[i], this, hammerVisuals[i]))
                {
                    hammerRows[i] = -1;
                    ExpireOwned(ref haftRows[i], ctx.visualOnly, haftVisuals[i]);
                    continue;
                }
                FollowHead(i);
                DrawWhirl(hammerRows[i], hammerVisuals[i] == skyHammerLook, alpha);
            }
        }

        /// <summary>Carries the haft on its head.</summary>
        private void FollowHead(int index)
        {
            if (!IsOwned(haftRows[index], ctx.visualOnly, haftVisuals[index]))
            {
                return;
            }
            ref ProjectileSpawn haft = ref projectiles.Get(haftRows[index]);
            haft.position = projectiles.Get(hammerRows[index]).position;
        }

        /// <summary>The weapon leaves the loadout: its hammers in flight leave with it (the base removes their permanent hafts).</summary>
        protected override void OnDispose()
        {
            for (int i = 0; i < MAX_HAMMERS; i++)
            {
                if (IsOwned(hammerRows[i], this, hammerVisuals[i]))
                {
                    projectiles.Remove(hammerRows[i]);
                }
                hammerRows[i] = -1;
            }
        }

        private void Throw(in WeaponEvent e)
        {
            int index = FindFreeHammer();
            if (index < 0)
            {
                return;
            }
            float speed = SpeedOf(level[WeaponField.Speed]);
            float lifetime = OUT_AND_BACK * level[WeaponField.Range] / MathF.Max(speed, MIN_SPEED);
            int look = isEvolved ? skyHammerLook : hammerLook;
            Vector3 origin = heroPosition + e.direction * THROW_OUT + Ground.Up(THROW_HEIGHT);
            ProjectileSpawn spawn = NewProjectile(look, ProjectileMotion.Boomerang, origin, e.direction * speed, lifetime);
            spawn.damage = MightOf(level[WeaponField.Damage], e.cast);
            spawn.radius = HAMMER_RADIUS;
            spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
            spawn.handler = this;
            spawn.userValue = index;
            int row = Spawn(spawn);
            if (row < 0)
            {
                return;
            }
            hammerRows[index] = row;
            hammerVisuals[index] = look;
            SpawnHaft(index, spawn);
            turnAges[index] = lifetime / OUT_AND_BACK;
            passes[index] = OUT_PASS;
            thunderPasses[index] = 0;
            struck[index].Clear();
            PlayThrow(e.direction);
        }

        /// <summary>The haft: a visual-only twin of the head, spawned with it (same spin) and carried on it every frame.</summary>
        private void SpawnHaft(int index, ProjectileSpawn head)
        {
            ExpireOwned(ref haftRows[index], ctx.visualOnly, haftVisuals[index]);
            int look = isEvolved ? skyHaftLook : haftLook;
            head.visual = look;
            head.motion = ProjectileMotion.Static;
            head.lifetime = PERMANENT;
            head.handler = ctx.visualOnly;
            head.damage = 0.0f;
            head.radius = HAFT_RADIUS;
            head.knockback = 0.0f;
            head.headScale = 0.0f;
            haftRows[index] = Spawn(head);
            haftVisuals[index] = look;
        }

        private int FindFreeHammer()
        {
            for (int i = 0; i < MAX_HAMMERS; i++)
            {
                if (hammerRows[i] < 0 || !IsOwned(hammerRows[i], this, hammerVisuals[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn hammer = ref world.Get(projectile);
            int index = hammer.userValue;
            if (index < 0 || index >= MAX_HAMMERS)
            {
                return;
            }
            int pass = world.GetAge(projectile) >= turnAges[index] ? BACK_PASS : OUT_PASS;
            if (pass != passes[index])
            {
                passes[index] = pass;
                struck[index].Clear();
            }
            if (!struck[index].Add(enemies.GetEnemy(enemyRow)))
            {
                return;
            }
            bool wasStunned = enemies.IsStunned(enemyRow);
            Vector3 position = enemies.GetPosition(enemyRow);
            float above = enemies.GetHeight(enemyRow) + DAZE_ABOVE;
            var effect = new HitEffect { stunSeconds = DurationOf(level[WeaponField.Stun]) };
            if (HitAlongFlight(enemyRow, hammer, effect) == DamageResult.Missed)
            {
                return;
            }
            PlayHit(position, above, effect.stunSeconds);
            TryCallThunder(index, pass, enemyRow, position, wasStunned);
        }

        /// <summary>A hammer is back in the knight's hand: it is caught and its haft goes with it.</summary>
        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            int index = world.Get(projectile).userValue;
            if (index >= 0 && index < MAX_HAMMERS && hammerRows[index] == projectile)
            {
                hammerRows[index] = -1;
                ExpireOwned(ref haftRows[index], ctx.visualOnly, haftVisuals[index]);
                PlayCatch();
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Thunder
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A stunned enemy struck (or, for Skybreaker, any enemy on a lucky roll) calls a thunderclap onto it, once per
        /// hammer and way.
        /// </summary>
        private void TryCallThunder(int index, int pass, int row, Vector3 position, bool wasStunned)
        {
            int bit = 1 << pass;
            if ((thunderPasses[index] & bit) != 0)
            {
                return;
            }
            float chance = level[WeaponField.Chance];
            bool isLucky = chance > 0.0f && Random.Float() < chance + stats[StatId.Luck] * LUCK_CHANCE_PER_POINT;
            if (!wasStunned && !isLucky)
            {
                return;
            }
            float radius = AreaOf(level[WeaponField.SubRadius]);
            WeaponEvent thunder = ThunderEvent(position, enemies.GetEnemy(row), radius, MightOf(level[WeaponField.SubDamage]),
                                               level.GetCount(WeaponField.SubCount));
            if (Schedule(THUNDER_DELAY, thunder))
            {
                thunderPasses[index] |= bit;
                PlayCharge(position, radius);
            }
        }

        /// <summary>A thunderclap on <paramref name="target"/> (else where it stood) that chains on to <paramref name="links"/> more enemies.</summary>
        private static WeaponEvent ThunderEvent(Vector3 position, Enemy target, float radius, float damage, int links)
        {
            return new WeaponEvent { kind = THUNDER, position = position, target = target, radius = radius, value = damage, count = links };
        }

        /// <summary>A chain strike on <paramref name="target"/>, its lightning leaping from <paramref name="from"/>.</summary>
        private static WeaponEvent ChainEvent(Vector3 from, Vector3 position, Enemy target, float radius, float damage)
        {
            return new WeaponEvent { kind = CHAIN, origin = from, position = position, target = target, radius = radius, value = damage };
        }

        private void Thunderclap(in WeaponEvent e)
        {
            Vector3 at = GetStrikePoint(e);
            Strike(at, e.radius, e.value, false);
            if (e.count > 0)
            {
                QueueChain(at, e);
            }
        }

        /// <summary>Picks the chain's next enemies now (each beyond the claps before it) and strikes them one by one.</summary>
        private void QueueChain(Vector3 origin, in WeaponEvent thunder)
        {
            float gap = thunder.radius * CHAIN_GAP_SHARE;
            Vector3 previous = origin;
            for (int link = 0; link < thunder.count; link++)
            {
                if (!TryFindChainTarget(previous, origin, gap, out int row))
                {
                    return;
                }
                Vector3 next = enemies.GetPosition(row);
                Schedule(CHAIN_DELAY * (link + 1), ChainEvent(previous, next, enemies.GetEnemy(row), thunder.radius * CHAIN_RADIUS_SHARE, thunder.value));
                previous = next;
            }
        }

        /// <summary>The enemy nearest to <paramref name="previous"/> that stands clear of both the last and the first clap.</summary>
        private bool TryFindChainTarget(Vector3 previous, Vector3 origin, float gap, out int row)
        {
            int found = strikes.FindNearest(previous, CHAIN_RANGE, chainRows, CHAIN_CANDIDATES);
            float gapSquared = gap * gap;
            for (int i = 0; i < found; i++)
            {
                Vector3 position = enemies.GetPosition(chainRows[i]);
                if (Ground.DistanceSquared(position, previous) >= gapSquared && Ground.DistanceSquared(position, origin) >= gapSquared)
                {
                    row = chainRows[i];
                    return true;
                }
            }
            row = -1;
            return false;
        }

        private void ChainStrike(in WeaponEvent e)
        {
            Vector3 at = GetStrikePoint(e);
            LightningFx.DrawBolt(effects, e.origin + Ground.Up(ARC_HEIGHT), at + Ground.Up(ARC_HEIGHT), chainArc);
            Strike(at, e.radius, e.value, true);
        }

        /// <summary>The target where it stands now, or where it stood when the strike was called.</summary>
        private Vector3 GetStrikePoint(in WeaponEvent e)
        {
            int row = enemies.GetRow(e.target);
            return row >= 0 && enemies.IsTargetable(row) ? enemies.GetPosition(row) : e.position;
        }

        private void Strike(Vector3 at, float radius, float damage, bool isChain)
        {
            strikes.Circle(at, radius, damage, KnockbackOf(THUNDER_KNOCKBACK), slot);
            PlayThunder(at, radius, isChain);
            if (isChain)
            {
                PlayAt("lightning_crack", at, CHAIN_VOLUME, CHAIN_PITCH);
            }
            else
            {
                PlayAt("lightning_crack", at, 1.0f, CRACK_PITCH);
                PlayAt("godspear_boom", at, BOOM_VOLUME);
            }
            if (shakeThrottle.TryPass(clock))
            {
                ctx.feel.Shake(isChain ? CHAIN_TRAUMA : THUNDER_TRAUMA);
            }
            if (!isChain)
            {
                TryHitStop(ref hitStopThrottle, HIT_STOP_SECONDS);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Looks
        // -------------------------------------------------------------------------------------------------------

        /// <summary>The golden whirl trailing the spinning haft, at the angle the hammer is drawn at this frame.</summary>
        private void DrawWhirl(int row, bool isSkybreaker, float alpha)
        {
            float angle = projectiles.GetAge(row) * KnightLooks.HAMMER_SPIN + SWIRL_PHASE;
            Vector3 whirl = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward;
            Color color = WithAlpha(isSkybreaker ? SKY_SWIRL : SWIRL, alpha);
            float size = isSkybreaker ? SWIRL_SIZE * SKY_SWIRL_SCALE : SWIRL_SIZE;
            effects.EmitMoving(swirl, projectiles.Get(row).position, whirl * SWIRL_DRIFT, 1, color, size, SWIRL_SECONDS);
        }

        private void PlayThrow(Vector3 direction)
        {
            Vector3 hand = heroPosition + direction * HAND_FORWARD + Ground.Up(HAND_HEIGHT);
            EmitSized(glint, hand, KnightLooks.HOLY, THROW_GLINT_SIZE * fxScale);
            EmitSized(flash, hand, KnightLooks.GOLD, THROW_FLASH_SIZE);
            PlayAt("javelin_throw", heroPosition, THROW_VOLUME, THROW_PITCH);
        }

        /// <summary>A hammer strikes: a star and steel sparks at the chest, a golden daze swirl over the head while it is stunned.</summary>
        private void PlayHit(Vector3 position, float above, float stunSeconds)
        {
            Vector3 chest = position + CHEST;
            Color color = isEvolved ? STORM_CORE : KnightLooks.GOLD;
            EmitSized(glint, chest, color, HIT_GLINT_SIZE * fxScale);
            hitSparks.Play(chest, FxSizes.METRE_UNIT, fxScale, KnightLooks.GOLD);
            EmitShape(daze, position + Ground.Up(above), KnightLooks.GOLD, DAZE_SIZE, stunSeconds);
            PlayAt("aegis_bonk", position, HIT_VOLUME, HIT_PITCH);
        }

        /// <summary>A thunderclap is coming: a ring closes in on the spot, light crackles there and a star kindles high above.</summary>
        private void PlayCharge(Vector3 position, float radius)
        {
            Color color = isEvolved ? STORM_CORE : THUNDER_CORE;
            EmitShape(mark, position + LIFT, color, radius * FxSizes.RING_SIZE_PER_RADIUS, THUNDER_DELAY + MARK_LINGER);
            EmitSized(crackle, position + CHEST, color, CHARGE_CRACKLE_SIZE);
            EmitSized(glint, position + Ground.Up(SKY_HEIGHT), color, SKY_STAR_SIZE);
        }

        /// <summary>
        /// The clap: lightning strands from the sky over a glowing column, a flash, a shockwave, a scorch and crawling
        /// lightning on the ground, sparks, rubble, dust and motes; a chain strike is a single narrower bolt.
        /// </summary>
        private void PlayThunder(Vector3 at, float radius, bool isChain)
        {
            Vector3 ground = at + LIFT;
            Color core = isEvolved ? STORM_CORE : THUNDER_CORE;
            Color glow = isEvolved ? STORM_GLOW : THUNDER_GLOW;
            int strands = isChain ? CHAIN_BOLT_STRANDS : isEvolved ? SKY_BOLT_STRANDS : BOLT_STRANDS;
            float height = isChain ? CHAIN_BOLT_HEIGHT : BOLT_HEIGHT;
            effects.EmitSized(bolt, ground, strands, core, height);
            EmitSized(boltGlow, ground, glow, height);
            EmitSized(flash, at + CHEST, core, radius * FLASH_PER_RADIUS);
            effects.Emit(shockRing, ground, 1, isEvolved ? STORM_GLOW : KnightLooks.GOLD, radius * FxSizes.RING_SIZE_PER_RADIUS, RING_SECONDS);
            EmitShape(scorch, ground, WHITE, radius * SCORCH_PER_RADIUS, SCORCH_SECONDS);
            EmitSized(web, ground, core, radius * WEB_PER_RADIUS);
            Vector3 swarm = at + Ground.Up(SWARM_HEIGHT);
            if (isChain)
            {
                chainBurst.Play(swarm, radius, fxScale, core);
                return;
            }
            EmitSized(glint, at + Ground.Up(SKY_HEIGHT), core, SKY_FLARE_SIZE);
            thunderBurst.Play(swarm, radius, fxScale, core);
        }

        /// <summary>The hammer is back in the knight's hand: a glint and a few motes.</summary>
        private void PlayCatch()
        {
            Vector3 hand = heroPosition + Ground.Up(HAND_HEIGHT);
            EmitSized(glint, hand, KnightLooks.GOLD, CATCH_GLINT_SIZE * fxScale);
            catchMotes.Play(hand, FxSizes.METRE_UNIT, fxScale, KnightLooks.GOLD);
        }
    }
}

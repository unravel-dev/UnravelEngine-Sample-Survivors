using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Knight. Active Shield Rush: an invulnerable dash that rams enemies sideways and stuns them while the knight glows
    /// gold. Signature Bulwark: the block chance is a class stat; each block heals and releases a shield bash shockwave
    /// (at most one per <see cref="BASH_GAP"/>), and the knight gains Armor as he levels. <see cref="ACTIVE_TEXT"/> and
    /// <see cref="SIGNATURE_TEXT"/> state the numbers.
    /// Look: the rush pushes off with a flash, a ground shockwave, dust and sparks; on the way a golden bow wave rides
    /// ahead of the shield, wind lines stream past and dust trails behind, and every enemy rammed flashes a star; it
    /// ends in a flash, a wider shockwave, a last bow flare, sparks, dust and motes. The Bulwark bash flashes the holy
    /// cross over the knight inside a gold shockwave at its radius, with steel sparks, dust and motes.
    /// Consecrated ground (Consecration's glyph, see <see cref="Consecrate"/>): the knight has its Armor bonus and his
    /// bash reaches twice as far; such a bash also stands rays of light around its ring and lifts motes.
    /// </summary>
    public sealed class KnightKit : ClassKit
    {
        /// <summary>Bulwark: the block chance the knight starts with (a class stat).</summary>
        public const float BLOCK_CHANCE = 0.20f;
        private const float RUSH_COOLDOWN = 10.0f;
        private const float RUSH_DISTANCE = 6.0f;
        private const float RUSH_SECONDS = 0.30f;
        private const float RUSH_HALF_WIDTH = 0.8f;
        private const float RUSH_DAMAGE = 40.0f;
        private const float RUSH_DAMAGE_PER_LEVEL = 4.0f;
        private const float RUSH_KNOCKBACK = 3.0f;
        private const float RUSH_STUN = 0.8f;
        private const int RUSH_HIT_STOP_TARGETS = 5;
        private const float RUSH_HIT_STOP = 0.06f;
        private const float RUSH_TRAUMA = 0.2f;
        private const float RUSH_TRAIL_INTERVAL = 0.03f;
        private const float RUSH_GLOW_INTENSITY = 1.6f;
        private const float RUSH_GLOW_EXTRA_SECONDS = 0.15f;
        private const float BASH_RADIUS = 2.0f;
        private const float BASH_DAMAGE = 15.0f;
        private const float BASH_DAMAGE_PER_LEVEL = 1.0f;
        private const float BASH_KNOCKBACK = 1.5f;
        private const float BASH_GAP = 0.5f;
        /// <summary>Every block heals this share of max health.</summary>
        private const float BLOCK_HEAL = 0.025f;
        private const int LEVELS_PER_ARMOR = 5;
        private const int MAX_BONUS_ARMOR = 8;

        private const float SWARM_HEIGHT = 0.3f;
        private const float RUSH_START_FLASH = 1.4f;
        private const float RUSH_START_RING_RADIUS = 1.2f;
        private const float RUSH_END_FLASH = 1.8f;
        private const float RUSH_END_RING_RADIUS = 1.6f;
        private const float RUSH_END_RING_SECONDS = 0.4f;
        /// <summary>Bow wave crescent (slash_04 pivoted on its centre): its middle rides 1 m ahead of the knight.</summary>
        private const float BOW_SIZE = 4.0f;
        private const float BOW_HEIGHT = 0.8f;
        private const float BOW_SECONDS = 0.09f;
        /// <summary>Bow waves move on at this share of the rush's speed, so they ride ahead of the knight and trail a little.</summary>
        private const float BOW_FOLLOW = 0.7f;
        private const float BOW_END_SHARE = 1.3f;
        private const float BOW_END_SECONDS = 0.22f;
        /// <summary>The slowest a bow crescent drifts: enough to turn it along the rush.</summary>
        private const float BOW_DRIFT = 2.0f;
        private const float WIND_SIDE = 0.7f;
        private const float WIND_LOW = 0.6f;
        private const float WIND_HIGH = 1.4f;
        private const float WIND_SPEED = 4.0f;
        private const float WIND_WIDTH = 0.6f;
        private const float WIND_SECONDS = 0.18f;
        private const int MAX_RAM_GLINTS = 4;
        private const float RAM_GLINT_SIZE = 1.0f;
        private const float BASH_FLASH = 1.6f;
        private const float BASH_CROSS = 2.2f;
        private const float BASH_RING_SECONDS = 0.35f;
        private const int CONSECRATED_RAYS = 10;
        private const float CONSECRATED_RAY_HEIGHT = 2.0f;
        private const float CONSECRATED_CROSS_SHARE = 1.3f;

        private static readonly Color GOLD = new Color(1.0f, 0.82f, 0.4f, 1.0f);
        private static readonly Color WHITE = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        private static readonly Color HOLY = new Color(1.0f, 0.96f, 0.86f, 1.0f);
        private static readonly Color WIND = new Color(1.0f, 0.95f, 0.82f, 0.6f);
        private static readonly Vector3 CHEST = new Vector3(0.0f, 1.0f, 0.0f);
        private static readonly Vector3 DECAL_LIFT = new Vector3(0.0f, FxSizes.GROUND_LIFT, 0.0f);

        /// <summary>Bulwark, for the hero select and details screens.</summary>
        public static readonly string SIGNATURE_TEXT =
            $"{WeaponText.Percent(BLOCK_CHANCE)} chance to block any hit. A block heals {WeaponText.Percent(BLOCK_HEAL)} of max health and " +
            $"releases a shield bash shockwave ({WeaponText.Number(BASH_RADIUS)} m, {WeaponText.PlusPerLevel(BASH_DAMAGE, BASH_DAMAGE_PER_LEVEL)} damage). " +
            $"+1 Armor every {LEVELS_PER_ARMOR} levels.";

        /// <summary>Shield Rush, for the hero select and details screens.</summary>
        public static readonly string ACTIVE_TEXT =
            $"Dash {WeaponText.Number(RUSH_DISTANCE)} m, invulnerable, ramming enemies aside for " +
            $"{WeaponText.PlusPerLevel(RUSH_DAMAGE, RUSH_DAMAGE_PER_LEVEL)} damage and a {WeaponText.Number(RUSH_STUN)} s stun. " +
            $"Cooldown {WeaponText.Number(RUSH_COOLDOWN)} s.";

        private readonly HashSet<Enemy> rushMemory = new HashSet<Enemy>();
        private readonly int rushDust;
        private readonly int flash;
        private readonly int glint;
        private readonly int crossFlare;
        private readonly int shockRing;
        private readonly int arcGhost;
        private readonly int slashArc;
        private readonly int windStreak;
        private readonly FxComposite pushOff;
        private readonly FxComposite rushEnd;
        private readonly FxComposite bashBurst;
        private readonly int rays;
        private readonly FxComposite consecratedBurst;
        private float rushTime;
        private Vector3 rushPrevious;
        private Vector3 rushDirection;
        private float rushSpeed;
        private int rushHits;
        private float trailTimer;
        private int trailCount;
        private bool isBlockPending;
        private float lastBash = -10.0f;
        private int bonusArmor;
        private StatBonus levelArmor = new StatBonus(StatId.Armor, false);
        private float requestedArmor;
        private float requestedBashScale = 1.0f;
        private StatBonus consecratedArmor = new StatBonus(StatId.Armor, false);
        private float bashRadiusScale = 1.0f;

        public KnightKit(WeaponContext context) : base(context, HeroClass.Knight)
        {
            rushDust = effects.Find("RushDust");
            flash = effects.Find("KnightFlash");
            glint = effects.Find("KnightGlint");
            crossFlare = effects.Find("KnightCrossFlare");
            shockRing = effects.Find("KnightShockRing");
            arcGhost = effects.Find("KnightArcGhost");
            slashArc = effects.Find("KnightSlashArc");
            windStreak = effects.Find("KnightSweepStreak");
            pushOff = new FxComposite(effects, new FxLayer("KnightSteelSparks", 8), new FxLayer("KnightDust", 4, 0.6f, false, true));
            rushEnd = new FxComposite(effects, new FxLayer("KnightSteelSparks", 14), new FxLayer("KnightDust", 5, 0.7f, false, true),
                                      new FxLayer("KnightMotes", 8));
            bashBurst = new FxComposite(effects, new FxLayer("KnightSteelSparks", 14), new FxLayer("KnightDust", 3, 0.5f, false, true),
                                        new FxLayer("KnightMotes", 4));
            rays = effects.Find("KnightRays");
            consecratedBurst = new FxComposite(effects, new FxLayer("KnightMotes", 10), new FxLayer("KnightConsecrateMotes", 12));
            hero.blocked += OnBlocked;
        }

        protected override float baseActiveCooldown => RUSH_COOLDOWN;
        public override int signatureStacks => bonusArmor;

        /// <summary>Whether the knight stood on consecrated ground last frame (its Armor bonus holds, bashes are wider).</summary>
        public bool isConsecrated => bashRadiusScale > 1.0f || consecratedArmor.value > 0.0f;

        /// <summary>
        /// Consecrated ground under the knight (Consecration): <paramref name="armor"/> flat Armor and a Bulwark bash
        /// radius times <paramref name="bashScale"/>. The weapon renews it every frame the knight stands in its glyph
        /// (the strongest request of a frame wins). The kit adds the Armor once when it starts, moves it by the
        /// difference when the request changes, and takes it back once on the first frame it is not renewed or when the
        /// run ends, so the bonus never outlives the glyph.
        /// </summary>
        public void Consecrate(float armor, float bashScale)
        {
            requestedArmor = MathF.Max(requestedArmor, armor);
            requestedBashScale = MathF.Max(requestedBashScale, bashScale);
        }

        public override void Dispose()
        {
            hero.blocked -= OnBlocked;
            consecratedArmor.Set(stats, 0.0f);
        }

        protected override bool Activate()
        {
            Vector3 direction = dashDirection;
            float distance = ctx.GetTravel(hero.position, direction, RUSH_DISTANCE);
            hero.Dash(direction, distance, RUSH_SECONDS, true);
            rushTime = RUSH_SECONDS;
            rushPrevious = hero.position;
            rushDirection = direction;
            rushSpeed = distance / RUSH_SECONDS;
            rushHits = 0;
            trailTimer = 0.0f;
            rushMemory.Clear();
            ctx.feel.RequestAttack("Block", WeaponFeel.PRIORITY_ACTIVE);
            hero.SetGlow(GOLD, RUSH_GLOW_INTENSITY, RUSH_SECONDS + RUSH_GLOW_EXTRA_SECONDS);
            sounds.PlayAt("shield_rush", hero.position);
            PlayPushOff(hero.position);
            return true;
        }

        /// <summary>The rush starts: a flash at the shield, a small ground shockwave, dust and sparks.</summary>
        private void PlayPushOff(Vector3 position)
        {
            effects.EmitSized(flash, position + CHEST, 1, GOLD, RUSH_START_FLASH);
            effects.EmitSized(shockRing, position + DECAL_LIFT, 1, GOLD, RUSH_START_RING_RADIUS * FxSizes.RING_SIZE_PER_RADIUS);
            pushOff.Play(position + Ground.Up(SWARM_HEIGHT), FxSizes.METRE_UNIT, 1.0f, GOLD);
        }

        protected override void Step(float dt)
        {
            if (rushTime > 0.0f)
            {
                StepRush(dt);
            }
            ApplyConsecration();
            if (isBlockPending && ctx.clock - lastBash >= BASH_GAP)
            {
                Bash();
            }
            isBlockPending = false;
            UpdateArmor();
        }

        private void StepRush(float dt)
        {
            rushTime -= dt;
            Vector3 current = hero.position;
            var effect = new HitEffect { stunSeconds = RUSH_STUN };
            float damage = ctx.MightOf(RUSH_DAMAGE + RUSH_DAMAGE_PER_LEVEL * level);
            rushHits += strikes.Capsule(rushPrevious, current, RUSH_HALF_WIDTH, damage, Strikes.KnockbackOf(RUSH_KNOCKBACK), RunRecord.OTHER_SOURCE, effect,
                                        PushMode.Sideways, default, rushMemory);
            GlintRammed();
            rushPrevious = current;
            trailTimer -= dt;
            if (trailTimer <= 0.0f)
            {
                trailTimer = RUSH_TRAIL_INTERVAL;
                effects.Emit(rushDust, current, 3, WHITE);
                EmitRushWake(current);
            }
            if (rushTime <= 0.0f)
            {
                EndRush(current);
            }
        }

        /// <summary>A star on each enemy the last stretch of the rush rammed (the first few).</summary>
        private void GlintRammed()
        {
            ReadOnlySpan<int> rows = strikes.lastHits;
            int count = Math.Min(rows.Length, MAX_RAM_GLINTS);
            for (int i = 0; i < count; i++)
            {
                effects.EmitSized(glint, ctx.enemies.GetPosition(rows[i]) + CHEST, 1, GOLD, RAM_GLINT_SIZE);
            }
        }

        /// <summary>The golden bow wave ahead of the shield and wind lines streaming past, alternating sides.</summary>
        private void EmitRushWake(Vector3 position)
        {
            float speed = MathF.Max(rushSpeed * BOW_FOLLOW, BOW_DRIFT);
            effects.EmitMoving(arcGhost, position + Ground.Up(BOW_HEIGHT), rushDirection * speed, 1, GOLD, BOW_SIZE, BOW_SECONDS);
            float side = trailCount % 2 == 0 ? WIND_SIDE : -WIND_SIDE;
            float height = trailCount % 3 == 0 ? WIND_HIGH : WIND_LOW;
            trailCount++;
            Vector3 at = position + Ground.Perpendicular(rushDirection) * side + Ground.Up(height);
            effects.EmitMoving(windStreak, at, rushDirection * -WIND_SPEED, 1, WIND, WIND_WIDTH, WIND_SECONDS);
        }

        private void EndRush(Vector3 position)
        {
            PlayRushEnd(position);
            sounds.PlayAt("shield_rush_boom", position);
            ctx.feel.Shake(RUSH_TRAUMA);
            if (rushHits >= RUSH_HIT_STOP_TARGETS)
            {
                ctx.feel.HitStop(RUSH_HIT_STOP);
            }
        }

        /// <summary>The rush lands: a flash, a wider ground shockwave, a last bow flare, sparks, dust and motes.</summary>
        private void PlayRushEnd(Vector3 position)
        {
            effects.EmitSized(flash, position + CHEST, 1, GOLD, RUSH_END_FLASH);
            effects.Emit(shockRing, position + DECAL_LIFT, 1, GOLD, RUSH_END_RING_RADIUS * FxSizes.RING_SIZE_PER_RADIUS, RUSH_END_RING_SECONDS);
            effects.EmitMoving(slashArc, position + Ground.Up(BOW_HEIGHT), rushDirection * BOW_DRIFT, 1, HOLY, BOW_SIZE * BOW_END_SHARE,
                               BOW_END_SECONDS);
            rushEnd.Play(position + Ground.Up(SWARM_HEIGHT), FxSizes.METRE_UNIT, 1.0f, GOLD);
        }

        private void OnBlocked(Vector3 from)
        {
            ctx.HealHero(hero.maxHealth * BLOCK_HEAL, HealSource.Class);
            // The hit is still being resolved by the enemy systems: the shockwave waits for the loadout's tick.
            isBlockPending = true;
        }

        private void Bash()
        {
            lastBash = ctx.clock;
            Vector3 center = hero.position;
            float radius = BASH_RADIUS * bashRadiusScale;
            float damage = ctx.MightOf(BASH_DAMAGE + BASH_DAMAGE_PER_LEVEL * level);
            strikes.Circle(center, radius, damage, Strikes.KnockbackOf(BASH_KNOCKBACK), RunRecord.OTHER_SOURCE);
            PlayBash(center, radius);
            sounds.PlayAt("bulwark_clang", center);
        }

        /// <summary>The Bulwark bash: the holy cross flares over the knight inside a gold shockwave at the bash radius.</summary>
        private void PlayBash(Vector3 center, float radius)
        {
            bool isWide = bashRadiusScale > 1.0f;
            effects.EmitSized(flash, center + CHEST, 1, GOLD, BASH_FLASH);
            effects.EmitSized(crossFlare, center + CHEST, 1, HOLY, isWide ? BASH_CROSS * CONSECRATED_CROSS_SHARE : BASH_CROSS);
            effects.Emit(shockRing, center + DECAL_LIFT, 1, GOLD, radius * FxSizes.RING_SIZE_PER_RADIUS, BASH_RING_SECONDS);
            bashBurst.Play(center + Ground.Up(SWARM_HEIGHT), FxSizes.METRE_UNIT, 1.0f, GOLD);
            if (isWide)
            {
                PlayConsecratedBash(center, radius);
            }
        }

        /// <summary>A bash on consecrated ground: rays of light stand up around its wider ring and holy motes rise.</summary>
        private void PlayConsecratedBash(Vector3 center, float radius)
        {
            for (int k = 0; k < CONSECRATED_RAYS; k++)
            {
                Vector3 edge = center + Ground.RingDirection(0.0f, k, CONSECRATED_RAYS) * radius + DECAL_LIFT;
                effects.EmitSized(rays, edge, 1, GOLD, CONSECRATED_RAY_HEIGHT);
            }
            consecratedBurst.Play(center + Ground.Up(SWARM_HEIGHT), radius, 1.0f, GOLD);
        }

        /// <summary>Takes this frame's consecration request (none means the knight left the glyph) and resets it.</summary>
        private void ApplyConsecration()
        {
            consecratedArmor.Set(stats, requestedArmor);
            bashRadiusScale = requestedBashScale;
            requestedArmor = 0.0f;
            requestedBashScale = 1.0f;
        }

        /// <summary>Bulwark's level Armor: +1 every few levels, up to the cap.</summary>
        private void UpdateArmor()
        {
            bonusArmor = Math.Min(level / LEVELS_PER_ARMOR, MAX_BONUS_ARMOR);
            levelArmor.Set(stats, bonusArmor);
        }
    }
}

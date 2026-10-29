using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Barbarian. Active Earthshaker Leap: an invulnerable leap that lands with a stunning shockwave. Signature
    /// Bloodlust: each kill grants Fury, each Fury adds Might and cuts Cooldown, and Fury decays a moment after the last
    /// kill; at full Fury kills heal (within a per-second budget), the barbarian glows red and embers burn in his eyes.
    /// Blood Price: a landing with enough Fury spends it to heal. <see cref="ACTIVE_TEXT"/> and
    /// <see cref="SIGNATURE_TEXT"/> state the numbers.
    /// Class weapons feed Fury too: War Cry adds it per enemy hit (<see cref="AddFury"/>), Skullsplitter's fissure
    /// kills grant more than a kill's own (<see cref="AddKillFury"/>), and an enemy that dies bloodied by Butcher's
    /// Chains grants <see cref="bloodiedKillFury"/> on top of its kill.
    /// Look: the leap kicks off in a dust ring and trails dust and wind streaks; the landing flashes, throws a shockwave
    /// with a rolling dust ring over a radial crack, boulders, rocks, clods and sparks, and raises a ring of rock spikes
    /// at the edge of the hit. Reaching full Fury bursts in blood-red flame and sparks; while full, red flames lick
    /// around the body, embers rise from the eyes and a faint red glow beats on the body.
    /// </summary>
    public sealed class BarbarianKit : ClassKit
    {
        private const float LEAP_COOLDOWN = 12.0f;
        private const float LEAP_DISTANCE = 7.0f;
        private const float LEAP_SECONDS = 0.55f;
        private const float LEAP_RECOVERY = 0.2f;
        private const float LAND_RADIUS = 4.0f;
        private const float LAND_DAMAGE = 60.0f;
        private const float LAND_DAMAGE_PER_LEVEL = 6.0f;
        private const float LAND_KNOCKBACK = 4.0f;
        private const float LAND_STUN = 1.0f;
        private const float LAND_HIT_STOP = 0.08f;
        private const float LAND_TRAUMA = 0.35f;
        /// <summary>Fury a kill grants (a weapon may add more for its own kills, see <see cref="AddKillFury"/>).</summary>
        public const float FURY_PER_KILL = 1.0f;
        private const float FURY_MAX = 50.0f;
        private const float FURY_GRACE = 2.0f;
        private const float FURY_DECAY = 8.0f;
        private const float MIGHT_PER_FURY = 0.008f;
        private const float COOLDOWN_PER_FURY = -0.003f;
        private const float FULL_FURY_HEAL = 0.25f;
        /// <summary>Full Fury heals at most this share of max health per second, however fast the kills come.</summary>
        private const float FURY_HEAL_SHARE_PER_SECOND = 0.01f;
        /// <summary>Blood Price: the Fury a landing spends and the share of max health it heals.</summary>
        private const float BLOOD_PRICE_FURY = 30.0f;
        private const float BLOOD_PRICE_HEAL = 0.05f;
        private const float FURY_EMBER_INTERVAL = 0.2f;
        private const int FURY_EMBER_COUNT = 2;
        /// <summary>
        /// Full Fury's body glow beats like a slow heart between these intensities: low enough that the model's detail
        /// stays readable; the flames and eye embers carry the look.
        /// </summary>
        private const float FURY_GLOW_LOW = 0.1f;
        private const float FURY_GLOW_HIGH = 0.32f;
        private const float FURY_PULSE_SECONDS = 1.1f;
        /// <summary>The glow is refreshed every frame and outlasts it a little, so it ends as soon as Fury drops.</summary>
        private const float FURY_GLOW_SECONDS = 0.1f;
        /// <summary>Dev fill: Fury holds this long before the usual grace and decay (effect captures).</summary>
        private const float DEV_FURY_HOLD_SECONDS = 10.0f;

        private const float TAKEOFF_HEIGHT = 0.3f;
        private const float LAND_BURST_HEIGHT = 0.5f;
        private const float LAND_CRACK_SHARE = 1.6f;
        private const float LAND_CRACK_SECONDS = 2.5f;
        private const int LAND_SPIKES = 10;
        /// <summary>Spikes stand at this share of the landing radius, jittered by the spread.</summary>
        private const float LAND_SPIKE_RING = 0.8f;
        private const float LAND_SPIKE_JITTER = 0.4f;
        private const float LAND_SPIKE_MIN = 1.3f;
        private const float LAND_SPIKE_MAX = 1.9f;
        private const float TRAIL_DUST_HEIGHT = 0.7f;
        private const float TRAIL_DUST_SIZE = 0.55f;
        private const float TRAIL_DUST_SECONDS = 0.5f;
        private const float TRAIL_STREAK_HEIGHT = 1.0f;
        private const float TRAIL_STREAK_SPEED = 4.0f;
        private const float TRAIL_STREAK_SIZE = 0.6f;
        private const float TRAIL_STREAK_SECONDS = 0.18f;
        private const int FURY_FLAMES = 3;
        private const float FURY_FLAME_SPREAD = 0.35f;
        private const float FURY_FLAME_LOW = 0.3f;
        private const float FURY_FLAME_HIGH = 1.4f;
        private const float FURY_FLAME_MIN = 0.4f;
        private const float FURY_FLAME_MAX = 0.6f;

        private static readonly Vector3 EYES = new Vector3(0.0f, 1.75f, 0.0f);
        private static readonly Vector3 CHEST = new Vector3(0.0f, 1.1f, 0.0f);

        /// <summary>Bloodlust, for the hero select and details screens.</summary>
        public static readonly string SIGNATURE_TEXT =
            $"Each kill grants {WeaponText.Number(FURY_PER_KILL)} Fury (max {WeaponText.Number(FURY_MAX)}): +{WeaponText.Percent(MIGHT_PER_FURY)} Might and " +
            $"{WeaponText.Percent(COOLDOWN_PER_FURY)} Cooldown per Fury. Fury fades {WeaponText.Number(FURY_GRACE)} s after the last kill. " +
            $"At {WeaponText.Number(FURY_MAX)} Fury kills heal, up to {WeaponText.Percent(FURY_HEAL_SHARE_PER_SECOND)} of max health per second.";

        /// <summary>Earthshaker Leap, for the hero select and details screens.</summary>
        public static readonly string ACTIVE_TEXT =
            $"Leap up to {WeaponText.Number(LEAP_DISTANCE)} m, invulnerable in the air, and land with a {WeaponText.Number(LAND_RADIUS)} m shockwave: " +
            $"{WeaponText.PlusPerLevel(LAND_DAMAGE, LAND_DAMAGE_PER_LEVEL)} damage and a {WeaponText.Number(LAND_STUN)} s stun. " +
            $"Landing with {WeaponText.Number(BLOOD_PRICE_FURY)} Fury or more spends it to heal {WeaponText.Percent(BLOOD_PRICE_HEAL)} of max health. " +
            $"Cooldown {WeaponText.Number(LEAP_COOLDOWN)} s.";

        private readonly int dust;
        private readonly int streak;
        private readonly int crack;
        private readonly int spike;
        private readonly int furyFlame;
        private readonly int furyEmbers;
        private readonly FxComposite takeoff;
        private readonly FxComposite landing;
        private readonly FxComposite furyBurst;
        private float landingAt = -1.0f;
        private Vector3 leapDirection;
        private float fury;
        private float lastKill = -10.0f;
        private StatBonus furyMight = new StatBonus(StatId.Might, true);
        private StatBonus furyCooldown = new StatBonus(StatId.Cooldown, true);
        private bool wasFull;
        private float emberTimer;
        private HealBudget furyHeal = new HealBudget(FURY_HEAL_SHARE_PER_SECOND);

        /// <summary>Fury an enemy that dies bloodied grants on top of its kill (Butcher's Chains sets it from its rank).</summary>
        public float bloodiedKillFury;

        public BarbarianKit(WeaponContext context) : base(context, HeroClass.Barbarian)
        {
            dust = effects.Find("BarbarianDust");
            streak = effects.Find("BarbarianSweepStreak");
            crack = effects.Find("BarbarianCrack");
            spike = effects.Find("BarbarianRockSpike");
            furyFlame = effects.Find("BarbarianFuryFlame");
            furyEmbers = effects.Find("BarbarianFuryEmbers");
            // Size unit = 1 m.
            takeoff = new FxComposite(effects, FxLayer.Ground("BarbarianDustRing", 3.2f, false), new FxLayer("BarbarianDust", 6, 0.7f, false),
                                      new FxLayer("BarbarianDirtSpray", 5, 0.0f, false), new FxLayer("BarbarianGrit", 6));
            // Size unit = landing radius.
            landing = new FxComposite(effects, new FxLayer("BarbarianFlash", 1, 0.6f), FxLayer.Ground("BarbarianShockRing", 2.2f, false),
                                      FxLayer.Ground("BarbarianDustRing", 2.4f, false), new FxLayer("BarbarianDust", 10, 0.22f, false),
                                      new FxLayer("BarbarianBoulders", 6, 0.0f, false), new FxLayer("BarbarianRocks", 12, 0.0f, false),
                                      new FxLayer("BarbarianDirtSpray", 8, 0.0f, false), new FxLayer("BarbarianGrit", 18));
            // Size unit = 1 m.
            furyBurst = new FxComposite(effects, new FxLayer("BarbarianFlash", 1, 2.4f), new FxLayer("BarbarianFuryFlame", 10, 0.9f),
                                        new FxLayer("BarbarianGrit", 18), new FxLayer("BarbarianFuryEmbers", 12));
        }

        protected override float baseActiveCooldown => LEAP_COOLDOWN;
        public override int signatureStacks => (int)fury;

        /// <summary>Full Fury: the music's heartbeat layer and red axe trails follow this.</summary>
        public bool isFuryFull => fury >= FURY_MAX;

        /// <summary>Adds Fury without a kill (War Cry hits): capped at 50, and like a kill it restarts the grace before decay.</summary>
        public void AddFury(float amount)
        {
            if (amount <= 0.0f)
            {
                return;
            }
            fury = MathF.Min(FURY_MAX, fury + amount);
            lastKill = MathF.Max(lastKill, ctx.clock);
        }

        /// <summary>
        /// Extra Fury for one of a weapon's own kills, on top of the kill's <see cref="FURY_PER_KILL"/> (weapons get kills
        /// after the kit): like a kill's Fury, it heals when it is what fills the meter.
        /// </summary>
        public void AddKillFury(float extra)
        {
            bool wasFull = isFuryFull;
            AddFury(extra);
            if (!wasFull && isFuryFull)
            {
                furyHeal.Heal(hero, FULL_FURY_HEAL, HealSource.Class);
            }
        }

        /// <summary>Dev (effect reviews): full Fury at once, held 10 s before the usual 2 s grace and decay.</summary>
        public override void DevFillSignature()
        {
            fury = FURY_MAX;
            lastKill = ctx.clock + DEV_FURY_HOLD_SECONDS;
        }

        protected override bool Activate()
        {
            Vector3 direction = dashDirection;
            Vector3 origin = hero.position;
            float distance = ctx.GetTravel(origin, direction, LEAP_DISTANCE);
            hero.Dash(direction, distance, LEAP_SECONDS, true);
            hero.PlayFullBody("Leap", LEAP_SECONDS + LEAP_RECOVERY);
            landingAt = ctx.clock + LEAP_SECONDS;
            leapDirection = direction;
            takeoff.Play(origin + Ground.Up(TAKEOFF_HEIGHT), FxSizes.METRE_UNIT, 1.0f, BarbarianLooks.DUST_GOLD);
            sounds.PlayAt("leap_takeoff", origin);
            return true;
        }

        public override void OnKill(in EnemyDeath death)
        {
            float gain = FURY_PER_KILL + (death.wasBloodied ? bloodiedKillFury : 0.0f);
            fury = MathF.Min(FURY_MAX, fury + gain);
            lastKill = ctx.clock;
            if (fury >= FURY_MAX)
            {
                furyHeal.Heal(hero, FULL_FURY_HEAL, HealSource.Class);
            }
        }

        protected override void Step(float dt)
        {
            if (landingAt >= 0.0f && ctx.clock >= landingAt)
            {
                landingAt = -1.0f;
                Land();
            }
            else if (landingAt >= 0.0f)
            {
                DrawLeapTrail();
            }
            furyHeal.Refill(hero.maxHealth, dt);
            if (ctx.clock - lastKill > FURY_GRACE && fury > 0.0f)
            {
                fury = MathF.Max(0.0f, fury - FURY_DECAY * dt);
            }
            ApplyFury();
            ShowFury(dt);
        }

        /// <summary>In the air: dust shed behind the hero and wind streaks along the leap, every frame.</summary>
        private void DrawLeapTrail()
        {
            Vector3 position = hero.position;
            effects.EmitMoving(dust, position + Ground.Up(TRAIL_DUST_HEIGHT), Vector3.zero, 1, TRAIL_DUST_SIZE, TRAIL_DUST_SECONDS);
            effects.EmitMoving(streak, position + Ground.Up(TRAIL_STREAK_HEIGHT), leapDirection * TRAIL_STREAK_SPEED, 1, BarbarianLooks.STEEL,
                               TRAIL_STREAK_SIZE, TRAIL_STREAK_SECONDS);
        }

        private void Land()
        {
            Vector3 center = hero.position;
            var effect = new HitEffect { stunSeconds = LAND_STUN };
            float damage = ctx.MightOf(LAND_DAMAGE + LAND_DAMAGE_PER_LEVEL * level);
            strikes.Circle(center, LAND_RADIUS, damage, Strikes.KnockbackOf(LAND_KNOCKBACK), RunRecord.OTHER_SOURCE, effect);
            DrawLanding(center);
            PayBloodPrice(center);
            sounds.PlayAt("leap_land", center);
            ctx.feel.HitStop(LAND_HIT_STOP);
            ctx.feel.Shake(LAND_TRAUMA);
        }

        /// <summary>Blood Price: with enough Fury the landing spends some of it to heal, in a burst of red flame.</summary>
        private void PayBloodPrice(Vector3 center)
        {
            if (fury < BLOOD_PRICE_FURY)
            {
                return;
            }
            fury -= BLOOD_PRICE_FURY;
            ctx.HealHero(hero.maxHealth * BLOOD_PRICE_HEAL, HealSource.Class);
            furyBurst.Play(center + CHEST, FxSizes.METRE_UNIT, 1.0f, BarbarianLooks.FURY_RED);
            sounds.Play("heal");
        }

        /// <summary>The landing: flash, shockwave, dust ring, debris and a radial crack ringed by rock spikes at the hit's edge.</summary>
        private void DrawLanding(Vector3 center)
        {
            Vector3 ground = new Vector3(center.x, FxSizes.GROUND_LIFT, center.z);
            landing.Play(ground + Ground.Up(LAND_BURST_HEIGHT), LAND_RADIUS, 1.0f, BarbarianLooks.DUST_GOLD);
            effects.EmitMoving(crack, ground, Ground.RandomDirection() * FxSizes.HEADING_SPEED, 1, Color.white, LAND_RADIUS * LAND_CRACK_SHARE,
                               LAND_CRACK_SECONDS);
            float start = Ground.RandomYaw();
            for (int k = 0; k < LAND_SPIKES; k++)
            {
                float distance = LAND_RADIUS * LAND_SPIKE_RING + Random.Range(-LAND_SPIKE_JITTER, LAND_SPIKE_JITTER);
                Vector3 root = Ground.Flat(center) + Ground.RingDirection(start, k, LAND_SPIKES) * distance;
                effects.EmitSized(spike, root, 1, Color.white, Random.Range(LAND_SPIKE_MIN, LAND_SPIKE_MAX));
            }
        }

        /// <summary>Moves the Might and Cooldown bonuses to the whole Fury, once per frame.</summary>
        private void ApplyFury()
        {
            int whole = (int)fury;
            furyMight.Set(stats, MIGHT_PER_FURY * whole);
            furyCooldown.Set(stats, COOLDOWN_PER_FURY * whole);
        }

        private void ShowFury(float dt)
        {
            bool isFull = isFuryFull;
            if (isFull && !wasFull)
            {
                sounds.Play("fury_max");
                furyBurst.Play(hero.position + CHEST, FxSizes.METRE_UNIT, 1.0f, BarbarianLooks.FURY_RED);
            }
            wasFull = isFull;
            if (!isFull)
            {
                return;
            }
            emberTimer -= dt;
            if (emberTimer <= 0.0f)
            {
                emberTimer = FURY_EMBER_INTERVAL;
                DrawFury(hero.position);
            }
            hero.SetGlow(BarbarianLooks.FURY_RED, GetFuryGlow(), FURY_GLOW_SECONDS);
        }

        /// <summary>The body glow's intensity now: a smooth beat from low to high and back every pulse.</summary>
        private float GetFuryGlow()
        {
            float beat = 0.5f - 0.5f * MathF.Cos(2.0f * MathF.PI * ctx.clock / FURY_PULSE_SECONDS);
            return Mathf.Lerp(FURY_GLOW_LOW, FURY_GLOW_HIGH, beat * beat);
        }

        /// <summary>Full Fury: embers rise from the eyes and blood-red flames lick at random points around the body.</summary>
        private void DrawFury(Vector3 position)
        {
            effects.Emit(furyEmbers, position + EYES, FURY_EMBER_COUNT, BarbarianLooks.FURY_RED);
            for (int k = 0; k < FURY_FLAMES; k++)
            {
                Vector3 at = position + Ground.RandomInCircle(FURY_FLAME_SPREAD) + Ground.Up(Random.Range(FURY_FLAME_LOW, FURY_FLAME_HIGH));
                effects.EmitSized(furyFlame, at, 1, BarbarianLooks.FURY_RED, Random.Range(FURY_FLAME_MIN, FURY_FLAME_MAX));
            }
        }
    }
}

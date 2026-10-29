using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Mage. Active Blink: an instant teleport with a short invulnerability and a ward, leaving an arcane rift that
    /// detonates a moment later. Signature Arcane Echo: each cast of a Mage weapon may cast again as an echo
    /// (<see cref="RollEcho"/>), more often as she levels. <see cref="ACTIVE_TEXT"/> and <see cref="SIGNATURE_TEXT"/>
    /// state the numbers.
    /// Arcane Ward look: while the ward holds, a faint shell surrounds the mage, one mote circles her per charge left
    /// and a rune circle turns under her feet; a charge that absorbs a hit bursts in violet shards (bigger for the
    /// last one), and a ward that runs out fades in a few motes.
    /// </summary>
    public sealed class MageKit : ClassKit
    {
        private const float BLINK_COOLDOWN = 8.0f;
        private const float BLINK_DISTANCE = 7.0f;
        private const float BLINK_INVULNERABLE = 0.3f;
        /// <summary>Arcane Ward: hits a blink absorbs, and for how long.</summary>
        private const int WARD_HITS = 3;
        private const float WARD_SECONDS = 3.0f;
        private const float RIFT_DELAY = 0.4f;
        private const float RIFT_RADIUS = 3.0f;
        private const float RIFT_DAMAGE = 50.0f;
        private const float RIFT_DAMAGE_PER_LEVEL = 5.0f;
        private const float RIFT_KNOCKBACK = 1.5f;
        private const float RIFT_TRAUMA = 0.15f;
        /// <summary>The rift's rune circle and its twirl above it, sized by the rift radius; both outlive the detonation a little.</summary>
        private const float RIFT_SIGIL_LIFT = 0.06f;
        private const float RIFT_SIGIL_SIZE_PER_RADIUS = 2.0f;
        private const float RIFT_SIGIL_LINGER = 0.15f;
        private const float RIFT_TWIRL_LIFT = 0.04f;
        private const int RIFT_TWIRL_COUNT = 2;
        private const float RIFT_TWIRL_SIZE_PER_RADIUS = 1.6f;
        private const float RIFT_TWIRL_LINGER = 0.1f;
        private const float ECHO_BASE = 0.12f;
        private const float ECHO_PER_STEP = 0.01f;
        private const int ECHO_LEVELS_PER_STEP = 5;
        private const float ECHO_MAX = 0.20f;
        private const float WARD_SHELL_SIZE = 3.0f;
        private const float WARD_SHELL_SECONDS = 0.05f;
        private const float WARD_MOTE_SIZE = 0.5f;
        private const float WARD_MOTE_RADIUS = 1.35f;
        private const float WARD_MOTE_BOB = 0.12f;
        private const float WARD_MOTE_BOB_SPEED = 5.0f;
        /// <summary>Degrees per second the ward's motes circle (the rune circle turns at a third of it).</summary>
        private const float WARD_SPIN_SPEED = 200.0f;
        private const float WARD_GLYPH_SHARE = 0.33f;
        private const float WARD_GLYPH_SIZE = 3.2f;
        private const float WARD_GLYPH_SECONDS = 0.05f;
        private const float WARD_GLYPH_DRIFT = 0.01f;
        private const float WARD_LAST_SCALE = 1.4f;
        private const float WARD_VOLUME = 0.9f;
        private const float WARD_PITCH = 1.2f;

        private static readonly Color ARCANE = new Color(0.65f, 0.4f, 1.0f, 1.0f);
        private static readonly Vector3 BODY = new Vector3(0.0f, 1.0f, 0.0f);
        private static readonly Color WARD_PALE = new Color(0.85f, 0.75f, 1.0f, 1.0f);
        /// <summary>The ward shows around the mage's shoulders, clear of her wide hat.</summary>
        private static readonly Vector3 WARD_HEIGHT = new Vector3(0.0f, 1.3f, 0.0f);

        /// <summary>Arcane Echo, for the hero select and details screens.</summary>
        public static readonly string SIGNATURE_TEXT =
            $"{WeaponText.Percent(ECHO_BASE)} chance that a Mage spell casts twice; the echo hits for {WeaponText.Percent(Weapon.ECHO_DAMAGE)}. " +
            $"+{WeaponText.Percent(ECHO_PER_STEP)} every {ECHO_LEVELS_PER_STEP} levels (max {WeaponText.Percent(ECHO_MAX)}).";

        /// <summary>Blink, for the hero select and details screens.</summary>
        public static readonly string ACTIVE_TEXT =
            $"Teleport {WeaponText.Number(BLINK_DISTANCE)} m and ward yourself: the next {WARD_HITS} hits within {WeaponText.Number(WARD_SECONDS)} s are absorbed. " +
            $"The rift left behind detonates after {WeaponText.Number(RIFT_DELAY)} s for {WeaponText.PlusPerLevel(RIFT_DAMAGE, RIFT_DAMAGE_PER_LEVEL)} damage. " +
            $"Cooldown {WeaponText.Number(BLINK_COOLDOWN)} s.";

        private readonly FxComposite blinkOut;
        private readonly FxComposite blinkIn;
        private readonly FxComposite riftBurst;
        private readonly int riftSigil;
        private readonly int riftTwirl;
        private readonly int wardShell;
        private readonly int wardMote;
        private readonly int wardGlyph;
        private readonly FxComposite wardBreak;
        private readonly FxComposite wardFade;
        private float riftAt = -1.0f;
        private Vector3 riftPosition;
        private float wardSpin;
        private int wardShown;

        public MageKit(WeaponContext context) : base(context, HeroClass.Mage)
        {
            blinkOut = new FxComposite(effects, new FxLayer("ArcaneFlash", 1, 2.2f), new FxLayer("ArcaneMotes", 16), new FxLayer("ArcaneStars", 6));
            blinkIn = new FxComposite(effects, new FxLayer("ArcaneFlash", 1, 2.6f), new FxLayer("ArcaneStars", 14),
                                      FxLayer.Ground("FireShock", 1.6f), new FxLayer("FrostSnow", 6));
            riftBurst = new FxComposite(effects, new FxLayer("ArcaneFlash", 1, 1.4f), new FxLayer("SolarRays", 1, 1.6f), new FxLayer("ArcaneStars", 20),
                                        FxLayer.Ground("FireShock", 2.0f), new FxLayer("FireSparks", 10));
            riftSigil = effects.Find("ArcaneSigil");
            riftTwirl = effects.Find("ArcaneTwirl");
            wardShell = effects.Find("MageWardShell");
            wardMote = effects.Find("MageWardMote");
            wardGlyph = effects.Find("MageWardGlyph");
            wardBreak = new FxComposite(effects, new FxLayer("ArcaneFlash", 1, 0.9f), new FxLayer("MageWardBurst", 1, 4.6f),
                                        new FxLayer("MageWardShards", 18), new FxLayer("ArcaneStars", 10));
            wardFade = new FxComposite(effects, new FxLayer("ArcaneMotes", 10), new FxLayer("ArcaneStars", 4));
            hero.warded += OnWarded;
        }

        public override void Dispose()
        {
            hero.warded -= OnWarded;
        }

        protected override float baseActiveCooldown => BLINK_COOLDOWN;

        /// <summary>Chance that a cast of a Mage weapon echoes at the hero's level.</summary>
        private float echoChance => MathF.Min(ECHO_MAX, ECHO_BASE + ECHO_PER_STEP * (level / ECHO_LEVELS_PER_STEP));

        /// <summary>Arcane Echo: a cast of a Mage weapon casts again as an echo.</summary>
        public override bool RollEcho() => Random.Float() < echoChance;

        /// <summary>An echo chimes at the mage.</summary>
        public override void OnEcho()
        {
            sounds.PlayAt("arcane_echo", hero.position);
        }

        protected override bool Activate()
        {
            Vector3 origin = hero.position;
            Vector3 direction = dashDirection;
            Vector3 target = origin + direction * ctx.GetTravel(origin, direction, BLINK_DISTANCE);
            blinkOut.Play(origin + BODY, FxSizes.METRE_UNIT, 1.0f, ARCANE);
            hero.Teleport(target);
            hero.SetInvulnerable(BLINK_INVULNERABLE);
            hero.Ward(WARD_HITS, WARD_SECONDS);
            blinkIn.Play(target + BODY, FxSizes.METRE_UNIT, 1.0f, ARCANE);
            Vector3 ground = origin + Ground.Up(RIFT_SIGIL_LIFT);
            effects.Emit(riftSigil, ground, 1, ARCANE, RIFT_RADIUS * RIFT_SIGIL_SIZE_PER_RADIUS, RIFT_DELAY + RIFT_SIGIL_LINGER);
            effects.Emit(riftTwirl, ground + Ground.Up(RIFT_TWIRL_LIFT), RIFT_TWIRL_COUNT, ARCANE, RIFT_RADIUS * RIFT_TWIRL_SIZE_PER_RADIUS,
                         RIFT_DELAY + RIFT_TWIRL_LINGER);
            ctx.feel.RequestAttack("CastShoot", WeaponFeel.PRIORITY_ACTIVE);
            sounds.PlayAt("blink", target);
            riftPosition = origin;
            riftAt = ctx.clock + RIFT_DELAY;
            return true;
        }

        protected override void Step(float dt)
        {
            DrawWard(dt);
            if (riftAt < 0.0f || ctx.clock < riftAt)
            {
                return;
            }
            riftAt = -1.0f;
            float damage = ctx.MightOf(RIFT_DAMAGE + RIFT_DAMAGE_PER_LEVEL * level);
            strikes.Circle(riftPosition, RIFT_RADIUS, damage, Strikes.KnockbackOf(RIFT_KNOCKBACK), RunRecord.OTHER_SOURCE);
            riftBurst.Play(riftPosition + BODY, RIFT_RADIUS, 1.0f, ARCANE);
            sounds.PlayAt("blink_rift", riftPosition);
            ctx.feel.Shake(RIFT_TRAUMA);
        }

        /// <summary>The ward while it holds: a faint shell, one circling mote per charge, a turning rune circle.</summary>
        private void DrawWard(float dt)
        {
            int charges = hero.wardHits;
            if (charges <= 0)
            {
                if (wardShown > 0)
                {
                    wardFade.Play(hero.position + WARD_HEIGHT, FxSizes.METRE_UNIT, 1.0f, ARCANE);
                }
                wardShown = 0;
                return;
            }
            wardShown = charges;
            wardSpin = (wardSpin + WARD_SPIN_SPEED * dt) % Ground.FULL_TURN;
            Vector3 body = hero.position + WARD_HEIGHT;
            effects.Emit(wardShell, body, 1, ARCANE, WARD_SHELL_SIZE, WARD_SHELL_SECONDS);
            float bob = MathF.Sin(ctx.clock * WARD_MOTE_BOB_SPEED) * WARD_MOTE_BOB;
            for (int k = 0; k < charges; k++)
            {
                Vector3 offset = Ground.RingDirection(wardSpin, k, charges) * WARD_MOTE_RADIUS;
                effects.EmitSized(wardMote, body + offset + Ground.Up(k % 2 == 0 ? bob : -bob), 1, WARD_PALE, WARD_MOTE_SIZE);
            }
            Vector3 floor = new Vector3(hero.position.x, Tuning.TELEGRAPH_HEIGHT, hero.position.z);
            Vector3 turn = Ground.FromYaw(wardSpin * WARD_GLYPH_SHARE) * WARD_GLYPH_DRIFT;
            effects.EmitMoving(wardGlyph, floor, turn, 1, ARCANE, WARD_GLYPH_SIZE, WARD_GLYPH_SECONDS);
        }

        /// <summary>A charge absorbed a hit: it bursts in shards, the last one bigger (runs inside the hit: effects only).</summary>
        private void OnWarded(Vector3 from)
        {
            bool isLast = hero.wardHits <= 0;
            float scale = isLast ? WARD_LAST_SCALE : 1.0f;
            wardBreak.Play(hero.position + WARD_HEIGHT, scale, scale, ARCANE);
            sounds.PlayAt("frost_shatter", hero.position, WARD_VOLUME, WARD_PITCH);
            wardShown = hero.wardHits;
        }
    }
}

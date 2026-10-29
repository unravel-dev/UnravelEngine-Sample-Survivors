using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Consecration (Knight, Uncommon): when the dead come near, a golden glyph of Radius (Area) is planted under the
    /// knight for Duration (Duration stat). Every Interval it deals holy Damage to everything inside and judges it
    /// (exposed: +Bonus damage taken from everything for SubDuration, so the hammers, swords and shields hit harder
    /// there). While the knight stands in any glyph he has +Armor and his Bulwark bashes reach twice as far
    /// (KnightKit.Consecrate, renewed every frame, so the bonus ends with the glyph, the evolution or the run).
    /// Overlapping glyphs each tick.
    /// Evolution Eternal Sanctum (+ Everburn Wick): one glyph that never fades, replanted where the knight stands every
    /// Cooldown, with a holy pulse every SubInterval (SubDamage in the whole glyph, Knockback outward).
    /// Look: the knight raises his sword; a flash and the holy cross flare over him, a gold shockwave runs out to the
    /// edge and pillars of light stand up around it while the glyph unfolds: a turning rune circle, the holy compass star
    /// under him, a crisp white-gold ring edge and a soft light on the moor. While it lasts gold motes, shafts of light
    /// and runes rise from it; every tick it breathes a faint ring and a flicker of light rises from each judged enemy;
    /// the knight on it glows with rising motes (a cross flare when he steps on). A fading glyph releases its light as
    /// motes. Eternal Sanctum is paler and holier: a crown of white-gold candle flames burns along its edge, and every
    /// pulse flashes the cross, sends a wave and light streaks out to the edge and stands rays of light around it.
    /// </summary>
    public sealed class Consecration : Weapon
    {
        private const int MAX_GLYPHS = 6;
        /// <summary>A glyph is planted when an enemy is within its radius plus this much of the knight.</summary>
        private const float PLANT_REACH = 4.0f;
        private const float BASH_SCALE = 2.0f;
        private const float MIN_INTERVAL = 0.05f;
        /// <summary>Ticks below this damage show no number (no number spam from a glyph).</summary>
        private const float QUIET_TICK_DAMAGE = 10.0f;

        private const float CROSS_HEIGHT = 1.5f;
        private const float SWARM_HEIGHT = 0.3f;
        private const float LIGHT_HEIGHT = 0.6f;
        /// <summary>ring_energy draws its thin line at 0.42 of the particle size: the glyph's edge.</summary>
        private const float EDGE_SIZE_PER_RADIUS = 2.39f;
        /// <summary>circle_03's double ring sits at 0.36 of the particle size.</summary>
        private const float PULSE_SIZE_PER_RADIUS = 2.78f;
        /// <summary>The holy compass star's points reach about half the radius.</summary>
        private const float STAR_SIZE_PER_RADIUS = 1.15f;
        private const float LIGHT_SIZE_PER_RADIUS = 1.1f;
        /// <summary>shock_ring's line is at 0.47 of the particle size.</summary>
        private const float WAVE_SIZE_PER_RADIUS = 2.12f;
        /// <summary>The eternal glyph's decals outlive its replant by this much: the old one fades as the new one unfolds.</summary>
        private const float REPLANT_OVERLAP = 0.45f;
        private const float PLANT_FLASH_SIZE = 1.6f;
        private const float PLANT_CROSS_SIZE = 2.0f;
        private const float PLANT_RING_SECONDS = 0.45f;
        private const int PLANT_RAYS = 10;
        private const float PLANT_RAY_HEIGHT = 2.4f;
        /// <summary>Ambient streams per metre of radius per second (x fxScale).</summary>
        private const float MOTES_PER_METRE = 8.0f;
        private const float SHAFTS_PER_METRE = 2.5f;
        private const float RUNES_PER_METRE = 1.6f;
        private const float AMBIENT_SPREAD = 0.92f;
        private const int MAX_EMITS_PER_FRAME = 6;
        private const float CANDLE_SPACING = 1.0f;
        private const int MIN_CANDLES = 8;
        /// <summary>Every candle flame is re-lit this often (its flame lives a little longer, so the candles never go out).</summary>
        private const float CANDLE_REFRESH = 0.22f;
        private const float CANDLE_HEIGHT = 0.75f;
        private const float CANDLE_JITTER = 0.15f;
        private const int MAX_JUDGE_FLARES = 16;
        private const float JUDGE_FLARE_HEIGHT = 1.2f;
        private const float BLESS_GAP = 1.0f;
        private const float BLESS_CROSS_SIZE = 1.2f;
        private const float BLESS_GLINT_SIZE = 1.0f;
        private const float BLESS_RING_RADIUS = 0.9f;
        private const float BLESS_RING_SECONDS = 0.3f;
        private const float BLESS_MOTE_INTERVAL = 0.1f;
        private const float BLESS_MOTE_SPREAD = 0.45f;
        private const float PULSE_FLASH_SIZE = 2.0f;
        private const float PULSE_CROSS_SIZE = 2.4f;
        private const float WAVE_SECONDS = 0.55f;
        private const int PULSE_STREAKS = 14;
        private const float PULSE_STREAK_HEIGHT = 0.4f;
        private const float PULSE_STREAK_WIDTH = 0.8f;
        private const float PULSE_STREAK_SECONDS = 0.35f;
        private const int PULSE_RAYS = 12;
        private const float PULSE_RAY_HEIGHT = 2.0f;
        private const int FADE_SHAFTS = 6;
        private const float PLANT_VOLUME = 0.8f;
        private const float PLANT_PITCH = 0.85f;
        private const float PULSE_VOLUME = 0.6f;

        /// <summary>The glyph's gold: a touch deeper than KnightLooks.GOLD; its crisp edge is paler.</summary>
        private static readonly Color GLYPH_GOLD = new Color(1.0f, 0.8f, 0.38f, 1.0f);
        private static readonly Color EDGE = new Color(1.0f, 0.9f, 0.62f, 1.0f);
        private static readonly Color GROUND_GLOW = new Color(1.0f, 0.82f, 0.48f, 1.0f);
        /// <summary>Eternal Sanctum's accent: a paler, holier white-gold.</summary>
        private static readonly Color SANCTUM = new Color(1.0f, 0.88f, 0.6f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.LOW_GROUND_LIFT);
        private static readonly Vector3 CHEST = Ground.Up(KnightLooks.CHEST_HEIGHT);

        /// <summary>One planted glyph and the timers of its ticks, pulse and ambient streams.</summary>
        private struct Glyph
        {
            public Vector3 center;
            public float radius;
            public float life;
            public float tickTimer;
            public float pulseTimer;
            public float moteTimer;
            public float shaftTimer;
            public float runeTimer;
            public float candleTimer;
            public int candleCursor;
            public bool isEternal;
        }

        private readonly Glyph[] glyphs = new Glyph[MAX_GLYPHS];
        private readonly KnightKit knightKit;
        private readonly int glyphDecal;
        private readonly int edgeRing;
        private readonly int compassStar;
        private readonly int groundLight;
        private readonly int tickPulse;
        private readonly int motes;
        private readonly int shafts;
        private readonly int runes;
        private readonly int judgeFlare;
        private readonly int candle;
        private readonly int wave;
        private readonly int flash;
        private readonly int glint;
        private readonly int crossFlare;
        private readonly int shockRing;
        private readonly int rays;
        private readonly int streak;
        private readonly FxComposite plantBurst;
        private readonly FxComposite pulseBurst;
        private readonly FxComposite fadeBurst;
        private int glyphCount;
        private bool wasInside;
        private Throttle blessThrottle = new Throttle(BLESS_GAP);
        private float blessTimer;

        public Consecration(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            knightKit = context.KitAs<KnightKit>();
            glyphDecal = effects.Find("KnightConsecrateGlyph");
            edgeRing = effects.Find("KnightConsecrateEdge");
            compassStar = effects.Find("KnightConsecrateStar");
            groundLight = effects.Find("KnightConsecrateLight");
            tickPulse = effects.Find("KnightConsecratePulse");
            motes = effects.Find("KnightConsecrateMotes");
            shafts = effects.Find("KnightConsecrateShaft");
            runes = effects.Find("KnightConsecrateRune");
            judgeFlare = effects.Find("KnightJudgeFlare");
            candle = effects.Find("KnightSanctumCandle");
            wave = effects.Find("KnightSanctumWave");
            flash = effects.Find("KnightFlash");
            glint = effects.Find("KnightGlint");
            crossFlare = effects.Find("KnightCrossFlare");
            shockRing = effects.Find("KnightShockRing");
            rays = effects.Find("KnightRays");
            streak = effects.Find("KnightSweepStreak");
            plantBurst = Composite(new FxLayer("KnightMotes", 14), new FxLayer("KnightConsecrateMotes", 10),
                                   new FxLayer("KnightDust", 3, 0.4f, false, true));
            pulseBurst = Composite(new FxLayer("KnightMotes", 12), new FxLayer("KnightDust", 4, 0.3f, false, true));
            fadeBurst = Composite(new FxLayer("KnightMotes", 10), new FxLayer("KnightConsecrateMotes", 12));
        }

        /// <summary>Eternal Sanctum's glyph never fades (its Duration reads permanent on the card).</summary>
        private bool isEternal => isEvolved;

        protected override bool usesCooldown => !isEternal;

        protected override bool Cast(in CastInfo cast)
        {
            float radius = AreaOf(level[WeaponField.Radius]);
            if (!TryNearest(radius + PLANT_REACH, out _))
            {
                return false;
            }
            int index = glyphCount < MAX_GLYPHS ? glyphCount++ : FindOldest();
            glyphs[index] = NewGlyph(radius, DurationOf(level[WeaponField.Duration]), false);
            PlayPlant(glyphs[index]);
            return true;
        }

        protected override void Update(float dt)
        {
            if (isEternal && !HasEternal() && glyphCount < MAX_GLYPHS)
            {
                int index = glyphCount++;
                glyphs[index] = NewGlyph(AreaOf(level[WeaponField.Radius]), cooldown, true);
                PlayPlant(glyphs[index]);
            }
            bool isInside = false;
            int i = 0;
            while (i < glyphCount)
            {
                ref Glyph glyph = ref glyphs[i];
                glyph.life -= dt;
                if (glyph.life <= 0.0f && !glyph.isEternal)
                {
                    PlayFade(glyph);
                    glyphs[i] = glyphs[--glyphCount];
                    continue;
                }
                if (glyph.life <= 0.0f)
                {
                    Replant(ref glyph);
                }
                StepGlyph(ref glyph, dt);
                isInside |= Ground.DistanceSquared(heroPosition, glyph.center) <= glyph.radius * glyph.radius;
                i++;
            }
            UpdateBlessing(isInside && hero.isAlive, dt);
        }

        /// <summary>A glyph under the knight; it ticks at once and Eternal Sanctum's first pulse comes a full interval later.</summary>
        private Glyph NewGlyph(float radius, float life, bool eternal)
        {
            return new Glyph
            {
                center = heroPosition,
                radius = radius,
                life = life,
                pulseTimer = level[WeaponField.SubInterval],
                isEternal = eternal,
            };
        }

        private bool HasEternal()
        {
            for (int i = 0; i < glyphCount; i++)
            {
                if (glyphs[i].isEternal)
                {
                    return true;
                }
            }
            return false;
        }

        private int FindOldest()
        {
            int oldest = -1;
            for (int i = 0; i < glyphCount; i++)
            {
                if (!glyphs[i].isEternal && (oldest < 0 || glyphs[i].life < glyphs[oldest].life))
                {
                    oldest = i;
                }
            }
            return Math.Max(oldest, 0);
        }

        /// <summary>Eternal Sanctum moves its glyph under the knight: the old one lifts away as light, the new one unfolds.</summary>
        private void Replant(ref Glyph glyph)
        {
            PlayFade(glyph);
            glyph.center = heroPosition;
            glyph.radius = AreaOf(level[WeaponField.Radius]);
            glyph.life = cooldown;
            PlayPlant(glyph);
        }

        private void StepGlyph(ref Glyph glyph, float dt)
        {
            glyph.tickTimer -= dt;
            if (glyph.tickTimer <= 0.0f)
            {
                glyph.tickTimer += MathF.Max(level[WeaponField.Interval], MIN_INTERVAL);
                Judge(glyph);
            }
            if (glyph.isEternal)
            {
                glyph.pulseTimer -= dt;
                if (glyph.pulseTimer <= 0.0f)
                {
                    glyph.pulseTimer += MathF.Max(level[WeaponField.SubInterval], MIN_INTERVAL);
                    Pulse(glyph);
                }
            }
            EmitAmbient(ref glyph, dt);
        }

        /// <summary>Holy damage to everything inside; each one hit is judged (exposed) and a flicker of light rises from it.</summary>
        private void Judge(in Glyph glyph)
        {
            float damage = DamageOf(WeaponField.Damage, CastInfo.normal);
            var effect = new HitEffect { hideNumber = damage < QUIET_TICK_DAMAGE };
            strikes.Circle(glyph.center, glyph.radius, damage, 0.0f, slot, effect, PushMode.None);
            ReadOnlySpan<int> rows = strikes.lastHits;
            float bonus = level[WeaponField.Bonus];
            float seconds = level[WeaponField.SubDuration];
            Color color = glyph.isEternal ? SANCTUM : GLYPH_GOLD;
            for (int k = 0; k < rows.Length; k++)
            {
                enemies.Expose(rows[k], bonus, seconds);
                if (k < MAX_JUDGE_FLARES)
                {
                    EmitSized(judgeFlare, enemies.GetPosition(rows[k]) + LIFT, color, JUDGE_FLARE_HEIGHT);
                }
            }
            EmitSized(tickPulse, glyph.center + LIFT, color, glyph.radius * PULSE_SIZE_PER_RADIUS);
        }

        /// <summary>Eternal Sanctum's holy pulse: damage in the whole glyph, everything pushed outward.</summary>
        private void Pulse(in Glyph glyph)
        {
            float damage = DamageOf(WeaponField.SubDamage, CastInfo.normal);
            strikes.Circle(glyph.center, glyph.radius, damage, KnockbackOf(level[WeaponField.Knockback]), slot);
            PlayPulse(glyph);
            PlayAt("soul_nova_ring", glyph.center, PULSE_VOLUME);
        }

        /// <summary>
        /// The knight on consecrated ground: the kit's Armor and wide bash for this frame, a cross flare when he steps on,
        /// and motes rising around him while he stays.
        /// </summary>
        private void UpdateBlessing(bool isInside, float dt)
        {
            if (!isInside)
            {
                wasInside = false;
                return;
            }
            knightKit?.Consecrate(level[WeaponField.Armor], BASH_SCALE);
            if (!wasInside && blessThrottle.TryPass(clock))
            {
                PlayBlessing();
            }
            wasInside = true;
            blessTimer -= dt;
            if (blessTimer <= 0.0f)
            {
                blessTimer = BLESS_MOTE_INTERVAL;
                Emit(motes, heroPosition + Ground.RandomInCircle(BLESS_MOTE_SPREAD) + LIFT, 1, isEternal ? SANCTUM : GLYPH_GOLD);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Looks
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The glyph is planted: its decals (rune circle, compass star, ring edge, ground light) for its whole life, and
        /// the moment itself: flash, cross flare, shockwave to the edge, pillars of light around it, motes and dust.
        /// </summary>
        private void PlayPlant(in Glyph glyph)
        {
            Vector3 ground = glyph.center + LIFT;
            float radius = glyph.radius;
            float life = glyph.isEternal ? glyph.life + REPLANT_OVERLAP : glyph.life;
            Color main = glyph.isEternal ? SANCTUM : GLYPH_GOLD;
            EmitShape(glyphDecal, ground, main, radius * FxSizes.SIGIL_SIZE_PER_RADIUS, life);
            EmitShape(compassStar, ground, main, radius * STAR_SIZE_PER_RADIUS, life);
            EmitShape(edgeRing, ground, EDGE, radius * EDGE_SIZE_PER_RADIUS, life);
            EmitShape(groundLight, glyph.center + Ground.Up(LIGHT_HEIGHT), GROUND_GLOW, radius * LIGHT_SIZE_PER_RADIUS, life);
            EmitSized(flash, glyph.center + CHEST, GLYPH_GOLD, PLANT_FLASH_SIZE);
            EmitSized(crossFlare, glyph.center + Ground.Up(CROSS_HEIGHT), KnightLooks.HOLY, PLANT_CROSS_SIZE);
            effects.Emit(shockRing, ground, 1, main, radius * FxSizes.RING_SIZE_PER_RADIUS, PLANT_RING_SECONDS);
            int count = (int)MathF.Round(PLANT_RAYS * fxScale);
            for (int k = 0; k < count; k++)
            {
                Vector3 edge = glyph.center + Ground.RingDirection(0.0f, k, count) * radius + LIFT;
                EmitSized(rays, edge, main, PLANT_RAY_HEIGHT * fxScale);
            }
            plantBurst.Play(glyph.center + Ground.Up(SWARM_HEIGHT), radius, fxScale, main);
            RequestAttack("CastRaise");
            PlayAt("verdict_summon", glyph.center, PLANT_VOLUME, PLANT_PITCH);
        }

        /// <summary>
        /// The glyph's life: gold motes, shafts of light and runes rising from inside it; Eternal Sanctum keeps a crown of
        /// candle flames burning along its edge, re-lit candle by candle.
        /// </summary>
        private void EmitAmbient(ref Glyph glyph, float dt)
        {
            float scale = glyph.radius * fxScale;
            Color color = glyph.isEternal ? SANCTUM : GLYPH_GOLD;
            int due = Countdown(ref glyph.moteTimer, dt, 1.0f / (MOTES_PER_METRE * scale));
            for (int k = 0; k < due; k++)
            {
                Emit(motes, RandomPointIn(glyph), 1, color);
            }
            due = Countdown(ref glyph.shaftTimer, dt, 1.0f / (SHAFTS_PER_METRE * scale));
            for (int k = 0; k < due; k++)
            {
                Emit(shafts, RandomPointIn(glyph), 1, color);
            }
            due = Countdown(ref glyph.runeTimer, dt, 1.0f / (RUNES_PER_METRE * scale));
            for (int k = 0; k < due; k++)
            {
                Emit(runes, RandomPointIn(glyph), 1, color);
            }
            if (glyph.isEternal)
            {
                EmitCandles(ref glyph, dt);
            }
        }

        private void EmitCandles(ref Glyph glyph, float dt)
        {
            int candles = Math.Max(MIN_CANDLES, (int)(MathF.PI * 2.0f * glyph.radius / CANDLE_SPACING));
            int due = Countdown(ref glyph.candleTimer, dt, CANDLE_REFRESH / candles);
            for (int k = 0; k < due; k++)
            {
                glyph.candleCursor = (glyph.candleCursor + 1) % candles;
                Vector3 edge = glyph.center + Ground.RingDirection(0.0f, glyph.candleCursor, candles) * glyph.radius + LIFT;
                EmitSized(candle, edge, WHITE, CANDLE_HEIGHT * (1.0f + Random.Range(-CANDLE_JITTER, CANDLE_JITTER)));
            }
        }

        /// <summary>Eternal Sanctum's pulse: a flash and the cross over the glyph, a wave and light streaks out to the edge, rays standing there.</summary>
        private void PlayPulse(in Glyph glyph)
        {
            Vector3 ground = glyph.center + LIFT;
            float radius = glyph.radius;
            EmitShape(wave, ground, SANCTUM, radius * WAVE_SIZE_PER_RADIUS, WAVE_SECONDS);
            EmitSized(flash, glyph.center + CHEST, SANCTUM, PULSE_FLASH_SIZE);
            EmitSized(crossFlare, glyph.center + Ground.Up(CROSS_HEIGHT), KnightLooks.HOLY, PULSE_CROSS_SIZE);
            Vector3 streaks = glyph.center + Ground.Up(PULSE_STREAK_HEIGHT);
            float speed = radius / PULSE_STREAK_SECONDS;
            for (int k = 0; k < PULSE_STREAKS; k++)
            {
                Vector3 outward = Ground.FromYaw(Ground.FULL_TURN * (k + 0.5f) / PULSE_STREAKS);
                effects.EmitMoving(streak, streaks, outward * speed, 1, SANCTUM, PULSE_STREAK_WIDTH, PULSE_STREAK_SECONDS);
            }
            for (int k = 0; k < PULSE_RAYS; k++)
            {
                EmitSized(rays, glyph.center + Ground.RingDirection(0.0f, k, PULSE_RAYS) * radius + LIFT, SANCTUM, PULSE_RAY_HEIGHT);
            }
            pulseBurst.Play(glyph.center + Ground.Up(SWARM_HEIGHT), radius, fxScale, SANCTUM);
        }

        /// <summary>The knight steps onto the glyph: the holy cross flares on him, a glint above and a small ring at his feet.</summary>
        private void PlayBlessing()
        {
            Color color = isEternal ? SANCTUM : GLYPH_GOLD;
            EmitSized(crossFlare, heroPosition + CHEST, KnightLooks.HOLY, BLESS_CROSS_SIZE);
            EmitSized(glint, heroPosition + Ground.Up(CROSS_HEIGHT), color, BLESS_GLINT_SIZE);
            effects.Emit(shockRing, heroPosition + LIFT, 1, color, BLESS_RING_RADIUS * FxSizes.RING_SIZE_PER_RADIUS, BLESS_RING_SECONDS);
        }

        /// <summary>A glyph ends (or Eternal Sanctum moves on): its light lifts away as motes and a few last shafts.</summary>
        private void PlayFade(in Glyph glyph)
        {
            Color color = glyph.isEternal ? SANCTUM : GLYPH_GOLD;
            fadeBurst.Play(glyph.center + Ground.Up(SWARM_HEIGHT), glyph.radius, fxScale, color);
            for (int k = 0; k < FADE_SHAFTS; k++)
            {
                Emit(shafts, RandomPointIn(glyph), 1, color);
            }
        }

        private static Vector3 RandomPointIn(in Glyph glyph)
        {
            return glyph.center + Ground.RandomInCircle(glyph.radius * AMBIENT_SPREAD) + LIFT;
        }

        /// <summary>How many of a steady stream are due this frame; a long hitch drops its backlog instead of bursting.</summary>
        private static int Countdown(ref float timer, float dt, float interval)
        {
            timer -= dt;
            int due = 0;
            while (timer <= 0.0f && due < MAX_EMITS_PER_FRAME)
            {
                timer += interval;
                due++;
            }
            if (timer <= 0.0f)
            {
                timer = interval;
            }
            return due;
        }
    }
}

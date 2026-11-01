using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// War Cry (Barbarian, Uncommon): the barbarian throws his head back (upper-body CastRaise) and, a beat later,
    /// roars: Damage within Radius (Area) with a shove outward (Knockback); every enemy hit is feared for Fear seconds
    /// (Confuse: it wanders and stops attacking), exposed (+Bonus damage taken for Duration seconds; both after the
    /// Duration stat) and feeds Fury per hit into Bloodlust. While the fear lasts the roar keeps pushing the dead away
    /// from the barbarian, so they turn and run. The cast waits until an enemy is inside the radius.
    /// Evolution Warlord's Bellow (+ Plate Pauldron): wider and more often, longer fear, more damage taken, and the
    /// barbarian gains +Armor for SubDuration seconds (flat, through the run's stats; a new bellow refreshes it, it is
    /// removed once when it ends).
    /// Look: wind streaks rush in to the barbarian as he draws breath; the roar bursts out as rings of red-tinged air
    /// (a pale crest over a dark trough, like shimmering air) over a ground shockwave and a rolling dust ring, dust
    /// blown out across the radius and wind streaks flying off him; skeletons hop back with fright lines over their
    /// skulls and dust at their feet. Full Fury turns the air blood red. The Bellow adds a wider echo ring, Fury flames
    /// and embers rising around him, the ground trembling in rings of dust and stones as the roar passes, and rock
    /// shards flying up from the ground to clad him in stone: a warm stone glow and stone sparks while the armor holds,
    /// shards crumbling off when it ends.
    /// </summary>
    public sealed class WarCry : Weapon
    {
        private const int ROAR = 0;
        private const int ECHO = 1;
        private const int TREMOR = 2;
        /// <summary>The roar bursts this long after the clip starts, as the head comes up.</summary>
        private const float ROAR_DELAY = 0.16f;
        private const float FLEE_INTERVAL = 0.1f;
        /// <summary>Outward push per flee tick: against the enemy world's knockback damping the feared drift away at ~2 m/s.</summary>
        private const float FLEE_IMPULSE = 2.0f;
        private const float STAGGER_HEIGHT = 0.35f;
        private const float ROAR_TRAUMA = 0.12f;
        private const float BELLOW_TRAUMA = 0.28f;
        private const int SCRATCH_CAPACITY = 1024;
        /// <summary>At most this many enemies show their fright per roar (late-game crowds).</summary>
        private const int MAX_REACTIONS = 40;
        private const float ROAR_PITCH = 1.05f;
        private const float BELLOW_PITCH = 0.88f;
        private const float ARMOR_CLANG_VOLUME = 0.8f;
        private const float ARMOR_CLANG_PITCH = 0.75f;

        private const float DUST_RING_PER_RADIUS = 2.0f;
        private const float WAVE_LOW_HEIGHT = 0.45f;
        private const float WAVE_HIGH_HEIGHT = 1.35f;
        private const float WAVE_HIGH_SHARE = 0.85f;
        private const float WAVE_SECONDS = 0.45f;
        private const float WAVE_HIGH_SECONDS = 0.38f;
        private const float SHADE_HEIGHT = 0.9f;
        private const float SHADE_SHARE = 0.92f;
        private const float SHADE_SECONDS = 0.5f;
        private const float ECHO_DELAY = 0.14f;
        private const float ECHO_HEIGHT = 1.0f;
        private const float ECHO_SHARE = 1.12f;
        private const float ECHO_SECONDS = 0.55f;
        private const int INHALE_STREAKS = 8;
        private const float INHALE_RADIUS = 2.2f;
        private const float INHALE_SPEED = 13.0f;
        private const float INHALE_HEIGHT = 1.0f;
        private const float STREAK_SIZE = 0.7f;
        private const float STREAK_SECONDS = 0.2f;
        private const int ROAR_STREAKS = 14;
        private const float ROAR_STREAK_SPEED = 22.0f;
        private const float ROAR_STREAK_START = 0.5f;
        private const float STREAK_HEIGHT_MIN = 0.5f;
        private const float STREAK_HEIGHT_MAX = 1.9f;
        private const int ROAR_DUST_PUFFS = 12;
        private const float ROAR_DUST_START = 0.8f;
        /// <summary>Launch speed of the blown dust per metre of radius: with its drag it settles near the rim.</summary>
        private const float ROAR_DUST_SPEED_PER_METRE = 2.6f;
        private const float ROAR_DUST_SIZE = 0.75f;
        private const float ROAR_DUST_SECONDS = 1.0f;
        private const float ROAR_DUST_HEIGHT = 0.3f;
        private const float ANGLE_JITTER = 10.0f;
        private const int FRIGHT_LINES = 3;
        private const float FRIGHT_HEIGHT = 0.25f;
        private const float SCUFF_HEIGHT = 0.1f;
        private const float SCUFF_SIZE = 0.35f;
        private const float GLOW_INTENSITY = 0.4f;
        private const float GLOW_SECONDS = 0.3f;
        private const int TREMOR_BANDS = 3;
        private const float TREMOR_BAND_DELAY = 0.07f;
        private const int TREMOR_PUFFS = 7;
        /// <summary>Every this many tremor puffs, a stone hops out of the ground too.</summary>
        private const int TREMOR_STONE_EVERY = 3;
        private const float TREMOR_DUST_SIZE = 0.55f;
        private const int PLUME_FLAMES = 8;
        private const float PLUME_RADIUS = 0.7f;
        private const float PLUME_LOW = 0.2f;
        private const float PLUME_HIGH = 1.6f;
        private const float PLUME_SIZE_MIN = 0.5f;
        private const float PLUME_SIZE_MAX = 0.8f;
        private const int PLUME_EMBERS = 14;
        private const int STONE_SHARDS = 10;
        private const float STONE_FROM_RADIUS = 1.8f;
        private const float STONE_FROM_HEIGHT = 0.1f;
        private const float STONE_BODY_SPREAD = 0.3f;
        private const float STONE_SECONDS = 0.28f;
        private const float STONE_SIZE_MIN = 0.2f;
        private const float STONE_SIZE_MAX = 0.32f;
        private const float ARMOR_GLOW = 0.09f;
        private const float ARMOR_GLOW_SECONDS = 0.1f;
        private const float ARMOR_SPARK_INTERVAL = 0.3f;
        private const int ARMOR_SPARKS = 2;
        private const int CRUMBLE_SHARDS = 6;
        private const float CRUMBLE_SPEED = 1.6f;
        private const float CRUMBLE_FALL = 1.0f;
        private const float CRUMBLE_SECONDS = 0.45f;
        private const float CRUMBLE_DUST_SIZE = 0.5f;

        private static readonly Color PALE = new Color(1.0f, 0.92f, 0.84f, 1.0f);
        private static readonly Color FURY_AIR = new Color(1.0f, 0.42f, 0.34f, 1.0f);
        /// <summary>The Bellow's echo: a dusty rose, so the ring at the rim never reads as an enemy telegraph.</summary>
        private static readonly Color ECHO_AIR = new Color(1.0f, 0.66f, 0.58f, 1.0f);
        private static readonly Color STONE_SPARK = new Color(1.0f, 0.82f, 0.62f, 1.0f);
        private static readonly Color STONE_GLOW = new Color(0.95f, 0.72f, 0.48f, 1.0f);
        private static readonly Vector3 CHEST = new Vector3(0.0f, 1.1f, 0.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.GROUND_LIFT);

        private readonly int[] scratch = new int[SCRATCH_CAPACITY];
        /// <summary>The Barbarian's kit (Fury), or null for another class.</summary>
        private readonly BarbarianKit barbarian;
        private readonly int roarWave;
        private readonly int roarShade;
        private readonly int roarDust;
        private readonly int streak;
        private readonly int fright;
        private readonly int dust;
        private readonly int rocks;
        private readonly int grit;
        private readonly int shockRing;
        private readonly int dustRing;
        private readonly int furyFlame;
        private readonly int furyEmbers;
        private readonly int stoneSkin;
        private float fleeUntil = -1.0f;
        private float fleeRadius;
        private float fleeTimer;
        /// <summary>The Bellow's Armor in the run's stats (0 when none): removed exactly once.</summary>
        private StatBonus armorBonus = new StatBonus(StatId.Armor, false);
        private float armorUntil;
        private float armorSparkTimer;

        public WarCry(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            barbarian = context.KitAs<BarbarianKit>();
            roarWave = effects.Find("BarbarianRoarWave");
            roarShade = effects.Find("BarbarianRoarShade");
            roarDust = effects.Find("BarbarianRoarDust");
            streak = effects.Find("BarbarianSweepStreak");
            fright = effects.Find("BarbarianFright");
            dust = effects.Find("BarbarianDust");
            rocks = effects.Find("BarbarianRocks");
            grit = effects.Find("BarbarianGrit");
            shockRing = effects.Find("BarbarianShockRing");
            dustRing = effects.Find("BarbarianDustRing");
            furyFlame = effects.Find("BarbarianFuryFlame");
            furyEmbers = effects.Find("BarbarianFuryEmbers");
            stoneSkin = effects.Find("BarbarianStoneSkin");
        }

        private bool isFuryFull => barbarian != null && barbarian.isFuryFull;

        private bool hasArmor => armorBonus.value > 0.0f;

        /// <summary>Leaving the loadout takes the Bellow's armor with it.</summary>
        protected override void OnDispose()
        {
            armorBonus.Set(stats, 0.0f);
            fleeUntil = -1.0f;
        }

        protected override bool Cast(in CastInfo cast)
        {
            if (!TryNearest(AreaOf(level[WeaponField.Radius]), out _))
            {
                return false;
            }
            RequestAttack("CastRaise");
            PlayAt("war_roar", heroPosition, 1.0f, isEvolved ? BELLOW_PITCH : ROAR_PITCH);
            DrawInhale(heroPosition);
            Schedule(ROAR_DELAY, new WeaponEvent { kind = ROAR, cast = cast });
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == ROAR)
            {
                Roar(e.cast);
            }
            else if (e.kind == ECHO)
            {
                DrawEcho(e.radius);
            }
            else if (e.kind == TREMOR)
            {
                DrawTremor(e.radius);
            }
        }

        /// <summary>Keeps the feared fleeing and holds (then drops) the Bellow's armor.</summary>
        protected override void Update(float dt)
        {
            if (clock < fleeUntil)
            {
                fleeTimer -= dt;
                if (fleeTimer <= 0.0f)
                {
                    fleeTimer += FLEE_INTERVAL;
                    enemies.PushAway(heroPosition, fleeRadius, FLEE_IMPULSE, scratch);
                }
            }
            if (!hasArmor)
            {
                return;
            }
            if (clock >= armorUntil)
            {
                DropArmor();
                return;
            }
            HoldArmor(dt);
        }

        private void Roar(in CastInfo cast)
        {
            Vector3 center = heroPosition;
            float radius = AreaOf(level[WeaponField.Radius]);
            float fear = DurationOf(level[WeaponField.Fear]);
            int hits = strikes.Circle(center, radius, DamageOf(WeaponField.Damage, cast), KnockbackOf(level[WeaponField.Knockback]), slot);
            Frighten(strikes.lastHits, fear, DurationOf(level[WeaponField.Duration]), RoarTint());
            barbarian?.AddFury(hits * level[WeaponField.Fury]);
            fleeUntil = clock + fear;
            fleeRadius = radius;
            fleeTimer = FLEE_INTERVAL;
            DrawRoar(center, radius);
            if (isEvolved)
            {
                Bellow(center, radius);
            }
            PlayAt(isEvolved ? "phase_shockwave" : "fault_stomp", center);
            ctx.feel.Shake(isEvolved ? BELLOW_TRAUMA : ROAR_TRAUMA);
        }

        /// <summary>Fears, exposes and staggers every enemy the roar hit; the first few show fright lines and a scuff.</summary>
        private void Frighten(ReadOnlySpan<int> hit, float fear, float exposed, Color tint)
        {
            float bonus = level[WeaponField.Bonus];
            int shown = 0;
            for (int i = 0; i < hit.Length; i++)
            {
                int row = hit[i];
                if (!enemies.IsTargetable(row))
                {
                    continue;
                }
                enemies.Confuse(row, fear);
                enemies.Expose(row, bonus, exposed);
                enemies.Launch(row, STAGGER_HEIGHT);
                if (shown++ < MAX_REACTIONS)
                {
                    DrawFright(row, tint);
                }
            }
        }

        /// <summary>Warlord's Bellow: stone armor, an echo ring, the ground trembling in bands and a plume of Fury.</summary>
        private void Bellow(Vector3 center, float radius)
        {
            RaiseArmor(center);
            Schedule(ECHO_DELAY, new WeaponEvent { kind = ECHO, radius = radius });
            for (int band = 1; band <= TREMOR_BANDS; band++)
            {
                Schedule(TREMOR_BAND_DELAY * band, new WeaponEvent { kind = TREMOR, radius = radius * band / TREMOR_BANDS });
            }
            DrawPlume(center);
        }

        private void RaiseArmor(Vector3 center)
        {
            float armor = level[WeaponField.Armor];
            if (armor <= 0.0f)
            {
                return;
            }
            if (!hasArmor)
            {
                armorBonus.Set(stats, armor);
                armorSparkTimer = ARMOR_SPARK_INTERVAL;
                PlayAt("bulwark_clang", center, ARMOR_CLANG_VOLUME, ARMOR_CLANG_PITCH);
            }
            armorUntil = clock + level[WeaponField.SubDuration];
            DrawStoneSkin(center);
        }

        private void DropArmor()
        {
            armorBonus.Set(stats, 0.0f);
            DrawCrumble(heroPosition);
        }

        /// <summary>While the stone armor holds: a warm stone glow (full Fury's red glow wins) and stone sparks off the body.</summary>
        private void HoldArmor(float dt)
        {
            if (!isFuryFull)
            {
                hero.SetGlow(STONE_GLOW, ARMOR_GLOW, ARMOR_GLOW_SECONDS);
            }
            armorSparkTimer -= dt;
            if (armorSparkTimer > 0.0f)
            {
                return;
            }
            armorSparkTimer += ARMOR_SPARK_INTERVAL;
            Emit(grit, heroPosition + CHEST + Ground.RandomInCircle(STONE_BODY_SPREAD), ARMOR_SPARKS, STONE_SPARK);
        }

        /// <summary>The roar's air: pale, blood red at full Fury.</summary>
        private Color RoarTint() => isFuryFull ? FURY_AIR : PALE;

        /// <summary>He draws breath: wind streaks rush in to him from a ring around his feet.</summary>
        private void DrawInhale(Vector3 center)
        {
            float start = Ground.RandomYaw();
            for (int k = 0; k < INHALE_STREAKS; k++)
            {
                Vector3 outward = Ground.RingDirection(start, k, INHALE_STREAKS);
                effects.EmitMoving(streak, center + outward * INHALE_RADIUS + Ground.Up(INHALE_HEIGHT), -outward * INHALE_SPEED, 1, PALE, STREAK_SIZE,
                                   STREAK_SECONDS);
            }
        }

        /// <summary>
        /// The roar: a ground shockwave and a rolling dust ring at the radius, two rings of air (low and at the chest)
        /// over a dark trough, dust blown out across the radius and wind streaks flying off the barbarian.
        /// </summary>
        private void DrawRoar(Vector3 center, float radius)
        {
            Color air = RoarTint();
            Vector3 ground = Ground.Flat(center) + LIFT;
            float ring = radius * FxSizes.SHOCK_RING_SIZE_PER_RADIUS;
            EmitSized(shockRing, ground, WHITE, ring);
            EmitSized(dustRing, ground, WHITE, radius * DUST_RING_PER_RADIUS);
            EmitShape(roarWave, ground + Ground.Up(WAVE_LOW_HEIGHT), air, ring, WAVE_SECONDS);
            EmitShape(roarWave, ground + Ground.Up(WAVE_HIGH_HEIGHT), air, ring * WAVE_HIGH_SHARE, WAVE_HIGH_SECONDS);
            EmitShape(roarShade, ground + Ground.Up(SHADE_HEIGHT), WHITE, ring * SHADE_SHARE, SHADE_SECONDS);
            BlowDust(ground, radius);
            BlowStreaks(center, air);
            if (!isFuryFull)
            {
                hero.SetGlow(BarbarianLooks.FURY_RED, GLOW_INTENSITY, GLOW_SECONDS);
            }
        }

        private void BlowDust(Vector3 ground, float radius)
        {
            int puffs = (int)MathF.Round(ROAR_DUST_PUFFS * fxScale);
            float start = Ground.RandomYaw();
            float speed = radius * ROAR_DUST_SPEED_PER_METRE;
            for (int k = 0; k < puffs; k++)
            {
                Vector3 outward = Ground.FromYaw(start + Ground.FULL_TURN * k / puffs + Random.Range(-ANGLE_JITTER, ANGLE_JITTER));
                effects.EmitMoving(roarDust, ground + outward * ROAR_DUST_START + Ground.Up(ROAR_DUST_HEIGHT), outward * speed, 1,
                                   ROAR_DUST_SIZE * fxScale, ROAR_DUST_SECONDS);
            }
        }

        private void BlowStreaks(Vector3 center, Color air)
        {
            int count = (int)MathF.Round(ROAR_STREAKS * fxScale);
            float start = Ground.RandomYaw();
            for (int k = 0; k < count; k++)
            {
                Vector3 outward = Ground.FromYaw(start + Ground.FULL_TURN * k / count + Random.Range(-ANGLE_JITTER, ANGLE_JITTER));
                Vector3 at = center + outward * ROAR_STREAK_START + Ground.Up(Random.Range(STREAK_HEIGHT_MIN, STREAK_HEIGHT_MAX));
                effects.EmitMoving(streak, at, outward * ROAR_STREAK_SPEED, 1, air, STREAK_SIZE, STREAK_SECONDS);
            }
        }

        /// <summary>A feared skeleton: fright lines flick up over its skull and dust scuffs at its feet.</summary>
        private void DrawFright(int row, Color tint)
        {
            Vector3 position = enemies.GetPosition(row);
            Emit(fright, position + Ground.Up(enemies.GetHeight(row) + FRIGHT_HEIGHT), FRIGHT_LINES, tint);
            EmitSized(dust, Ground.Flat(position) + Ground.Up(SCUFF_HEIGHT), WHITE, SCUFF_SIZE);
        }

        /// <summary>The Bellow's echo: a wider ring of red air and a second shockwave.</summary>
        private void DrawEcho(float radius)
        {
            Vector3 ground = Ground.Flat(heroPosition) + LIFT;
            float ring = radius * FxSizes.SHOCK_RING_SIZE_PER_RADIUS * ECHO_SHARE;
            EmitShape(roarWave, ground + Ground.Up(ECHO_HEIGHT), ECHO_AIR, ring, ECHO_SECONDS);
            EmitSized(shockRing, ground, WHITE, ring);
        }

        /// <summary>The ground trembles where the Bellow passes: a ring of dust puffs, every third throwing a stone.</summary>
        private void DrawTremor(float radius)
        {
            Vector3 ground = Ground.Flat(heroPosition) + LIFT;
            int puffs = (int)MathF.Round(TREMOR_PUFFS * fxScale);
            float start = Ground.RandomYaw();
            for (int k = 0; k < puffs; k++)
            {
                Vector3 at = ground + Ground.FromYaw(start + Ground.FULL_TURN * k / puffs + Random.Range(-ANGLE_JITTER, ANGLE_JITTER)) * radius;
                EmitSized(dust, at, WHITE, TREMOR_DUST_SIZE);
                if (k % TREMOR_STONE_EVERY == 0)
                {
                    Emit(rocks, at, 1, WHITE);
                }
            }
        }

        /// <summary>A plume of Fury around the barbarian: blood-red flames licking up and embers rising.</summary>
        private void DrawPlume(Vector3 center)
        {
            for (int k = 0; k < PLUME_FLAMES; k++)
            {
                Vector3 at = center + Ground.RingDirection(0.0f, k, PLUME_FLAMES) * PLUME_RADIUS + Ground.Up(Random.Range(PLUME_LOW, PLUME_HIGH));
                EmitSized(furyFlame, at, BarbarianLooks.FURY_RED, Random.Range(PLUME_SIZE_MIN, PLUME_SIZE_MAX));
            }
            Emit(furyEmbers, center + CHEST, PLUME_EMBERS, BarbarianLooks.FURY_RED);
        }

        /// <summary>Shards of rock tear out of the ground around him and fly to his body.</summary>
        private void DrawStoneSkin(Vector3 center)
        {
            int shards = (int)MathF.Round(STONE_SHARDS * fxScale);
            float start = Ground.RandomYaw();
            for (int k = 0; k < shards; k++)
            {
                Vector3 from = Ground.Flat(center) + Ground.RingDirection(start, k, shards) * STONE_FROM_RADIUS + Ground.Up(STONE_FROM_HEIGHT);
                Vector3 to = center + CHEST + Ground.RandomInCircle(STONE_BODY_SPREAD);
                effects.EmitMoving(stoneSkin, from, (to - from) / STONE_SECONDS, 1, Random.Range(STONE_SIZE_MIN, STONE_SIZE_MAX), STONE_SECONDS);
            }
        }

        /// <summary>The armor ends: its shards crumble off the body in a puff of dust.</summary>
        private void DrawCrumble(Vector3 center)
        {
            for (int k = 0; k < CRUMBLE_SHARDS; k++)
            {
                Vector3 outward = Ground.RandomDirection();
                Vector3 velocity = outward * CRUMBLE_SPEED + Ground.Up(-CRUMBLE_FALL);
                effects.EmitMoving(stoneSkin, center + CHEST + outward * STONE_BODY_SPREAD, velocity, 1, Random.Range(STONE_SIZE_MIN, STONE_SIZE_MAX),
                                   CRUMBLE_SECONDS);
            }
            EmitSized(dust, Ground.Flat(center) + Ground.Up(SCUFF_HEIGHT), WHITE, CRUMBLE_DUST_SIZE);
        }
    }
}

using System;
using System.Collections.Generic;
using F = Survivors.WeaponField;

namespace Survivors
{
    /// <summary>
    /// Every weapon, evolution, passive, boon and fallback card of the run. A rank table reads like the card: one row
    /// per rank, one column per card column. Numbers that do not change with the rank are constants applied to every
    /// rank. An evolution starts from rank 8 and overrides some of its numbers. Tuning lives here; behaviour lives in the
    /// weapon classes, which each entry builds through its factory. The tables are checked once, on first use.
    /// </summary>
    public static partial class WeaponCatalog
    {
        private static readonly WeaponColumn DAMAGE = new WeaponColumn(F.Damage, "Damage");
        private static readonly WeaponColumn COOLDOWN = new WeaponColumn(F.Cooldown, "Cooldown");
        private static readonly WeaponColumn PIERCE = new WeaponColumn(F.Pierce, "Pierce");
        private static readonly WeaponColumn KNOCKBACK = new WeaponColumn(F.Knockback, "Knockback");
        private static readonly WeaponColumn DURATION = new WeaponColumn(F.Duration, "Duration");
        private static readonly WeaponColumn STUN = new WeaponColumn(F.Stun, "Stun");
        private static readonly WeaponColumn RANGE = new WeaponColumn(F.Range, "Range");

        private static WeaponColumn Column(F field, string label) => new WeaponColumn(field, label);

        private static (F, float) Set(F field, float value) => (field, value);

        /// <summary>The card note of a rank (from 1).</summary>
        private static (int, string) Note(int rank, string text) => (rank, text);

        /// <summary>The first three class weapons of each class, and the shared ones.</summary>
        private static readonly WeaponDef[] coreWeapons =
        {
            // ---------------------------------------------------------------------------------------------- Knight
            Define(new WeaponSpec
            {
                id = "oathblade_arc", name = "Oathblade Arc", rarity = Rarity.Common, owner = HeroClass.Knight,
                tags = WeaponTags.Melee,
                create = (context, def, slot) => new OathbladeArc(context, def, slot),
                description = "A wide sword slice toward the nearest foe that throws skeletons back.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Radius"), Column(F.Count, "Swings"), Column(F.Angle, "Arc"), KNOCKBACK },
                rows = new float[,]
                {
                    { 14, 1.10f, 2.8f, 1, 140, 1.2f },
                    { 18, 1.10f, 2.8f, 1, 140, 1.2f },
                    { 18, 1.10f, 3.2f, 1, 140, 1.2f },
                    { 18, 1.10f, 3.2f, 2, 140, 1.2f },
                    { 24, 1.00f, 3.2f, 2, 140, 1.2f },
                    { 24, 1.00f, 3.6f, 2, 180, 1.2f },
                    { 30, 1.00f, 3.6f, 2, 180, 1.2f },
                    { 36, 0.90f, 3.6f, 2, 180, 1.6f },
                },
                constants = new[] { Set(F.Range, 6), Set(F.Stagger, 0.2f) },
                notes = new[] { Note(4, "Adds a backhand swing behind.") },
                evolution = new EvolutionSpec
                {
                    id = "dawnbreaker", name = "Dawnbreaker", passiveId = PassiveIds.IRON_HEART,
                    description = "A full 360 deg sweep: 48 damage, 0.85 s, radius 4.2 m. Every 3rd swing launches a golden crescent wave (48 damage, 4 m wide, 10 m). Heals 1 HP per 10 enemies hit (max 3 HP/s).",
                    overrides = new[]
                    {
                        Set(F.Damage, 48), Set(F.Cooldown, 0.85f), Set(F.Radius, 4.2f), Set(F.Angle, 360),
                        Set(F.Every, 3), Set(F.SubDamage, 48), Set(F.Width, 4), Set(F.SubSpeed, 14), Set(F.SubRange, 10),
                        Set(F.Heal, 0.1f), Set(F.HealCap, 3),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "aegis_orbit", name = "Aegis Orbit", rarity = Rarity.Uncommon, owner = HeroClass.Knight,
                tags = WeaponTags.Melee | WeaponTags.Lingering,
                create = (context, def, slot) => new AegisOrbit(context, def, slot),
                description = "Spectral shields circle the knight, batter skeletons outward and destroy enemy projectiles they touch.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Orbit"), Column(F.Count, "Shields"), DURATION, Column(F.Size, "Shield size") },
                rows = new float[,]
                {
                    { 10, 4.0f, 2.2f, 2, 3.0f, 0.6f },
                    { 10, 4.0f, 2.2f, 3, 3.0f, 0.6f },
                    { 14, 4.0f, 2.5f, 3, 3.0f, 0.6f },
                    { 14, 4.0f, 2.5f, 3, 4.0f, 0.6f },
                    { 14, 4.0f, 2.5f, 4, 4.0f, 0.6f },
                    { 18, 4.0f, 2.5f, 4, 4.0f, 0.75f },
                    { 18, 3.5f, 2.5f, 4, 5.0f, 0.75f },
                    { 22, 3.5f, 2.8f, 5, 5.0f, 0.75f },
                },
                constants = new[] { Set(F.Knockback, 2.0f), Set(F.Speed, 200), Set(F.Interval, 0.5f) },
                notes = new[] { Note(6, "Bigger shields.") },
                evolution = new EvolutionSpec
                {
                    id = "bastion_halo", name = "Bastion Halo", passiveId = PassiveIds.PLATE_PAULDRON,
                    description = "6 shields, 26 damage, orbit 3.0 m, permanent. Destroyed enemy projectiles are reflected at the nearest enemy for 30 damage.",
                    overrides = new[] { Set(F.Count, 6), Set(F.Damage, 26), Set(F.Radius, 3.0f), Set(F.Duration, -1), Set(F.SubDamage, 30), Set(F.SubSpeed, 18) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "verdict", name = "Verdict", rarity = Rarity.Rare, owner = HeroClass.Knight,
                tags = WeaponTags.Area,
                create = (context, def, slot) => new Verdict(context, def, slot),
                description = "A giant spectral sword slams down on the densest crowd, stunning and scattering it.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Radius"), Column(F.Count, "Swords"), STUN },
                rows = new float[,]
                {
                    { 45, 5.0f, 2.2f, 1, 0.5f },
                    { 60, 5.0f, 2.2f, 1, 0.5f },
                    { 60, 5.0f, 2.6f, 1, 0.5f },
                    { 60, 5.0f, 2.6f, 2, 0.5f },
                    { 80, 4.5f, 2.6f, 2, 0.5f },
                    { 80, 4.5f, 3.0f, 2, 0.8f },
                    { 80, 4.5f, 3.0f, 3, 0.8f },
                    { 110, 4.0f, 3.0f, 3, 0.8f },
                },
                constants = new[]
                {
                    Set(F.Knockback, 2.5f), Set(F.Range, 12), Set(F.Interval, 0.35f), Set(F.Stagger, 0.15f),
                    Set(F.SubDuration, 1.0f),
                },
                notes = new[] { Note(4, "A 2nd sword strikes the next crowd.") },
                evolution = new EvolutionSpec
                {
                    id = "final_verdict", name = "Final Verdict", passiveId = PassiveIds.WIDE_SIGIL,
                    description = "5 swords, 130 damage, radius 3.4 m. Each embedded sword releases a holy shockwave 1 s after landing (60 damage, radius 4.5 m).",
                    overrides = new[] { Set(F.Count, 5), Set(F.Damage, 130), Set(F.Radius, 3.4f), Set(F.SubDamage, 60), Set(F.SubRadius, 4.5f) },
                },
            }),

            // ------------------------------------------------------------------------------------------- Barbarian
            Define(new WeaponSpec
            {
                id = "ravager_chop", name = "Ravager Chop", rarity = Rarity.Common, owner = HeroClass.Barbarian,
                tags = WeaponTags.Melee,
                create = (context, def, slot) => new RavagerChop(context, def, slot),
                description = "A two-handed overhead chop in a cone with enormous knockback.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Length"), Column(F.Count, "Chops"), Column(F.Angle, "Cone"), KNOCKBACK, STUN },
                rows = new float[,]
                {
                    { 24, 1.60f, 3.2f, 1, 100, 2.5f, 0 },
                    { 30, 1.60f, 3.2f, 1, 100, 2.5f, 0 },
                    { 30, 1.60f, 3.6f, 1, 120, 2.5f, 0 },
                    { 30, 1.60f, 3.6f, 2, 120, 2.5f, 0 },
                    { 38, 1.45f, 3.6f, 2, 120, 2.5f, 0 },
                    { 38, 1.45f, 4.0f, 2, 120, 2.5f, 0 },
                    { 46, 1.45f, 4.0f, 2, 120, 3.0f, 0 },
                    { 55, 1.30f, 4.0f, 2, 120, 3.0f, 0.3f },
                },
                constants = new[] { Set(F.Range, 5), Set(F.Stagger, 0.25f) },
                notes = new[] { Note(4, "A 2nd axe chops 0.25 s later.") },
                evolution = new EvolutionSpec
                {
                    id = "skullsplitter", name = "Skullsplitter", passiveId = PassiveIds.WHETSTONE,
                    description = "70 damage, 1.20 s. Each chop splits the ground in a fissure 7 m forward (1.4 m wide, 70 damage); fissure kills grant 2 Fury.",
                    overrides = new[] { Set(F.Damage, 70), Set(F.Cooldown, 1.20f), Set(F.SubDamage, 70), Set(F.SubRange, 7), Set(F.Width, 1.4f), Set(F.FuryPerKill, 2) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "whirlwind", name = "Whirlwind", rarity = Rarity.Uncommon, owner = HeroClass.Barbarian,
                tags = WeaponTags.Melee | WeaponTags.Lingering,
                create = (context, def, slot) => new Whirlwind(context, def, slot),
                description = "The Barbarian spins, hitting everything around him every tick and moving 15% faster.",
                columns = new[] { Column(F.Damage, "Damage/tick"), COOLDOWN, Column(F.Radius, "Radius"), Column(F.Interval, "Tick"), DURATION },
                rows = new float[,]
                {
                    { 8, 6.0f, 2.4f, 0.25f, 1.5f },
                    { 10, 6.0f, 2.4f, 0.25f, 1.5f },
                    { 10, 6.0f, 2.4f, 0.25f, 2.0f },
                    { 10, 6.0f, 2.8f, 0.25f, 2.0f },
                    { 13, 5.5f, 2.8f, 0.25f, 2.0f },
                    { 13, 5.5f, 2.8f, 0.20f, 2.5f },
                    { 13, 5.5f, 3.2f, 0.20f, 2.5f },
                    { 16, 5.0f, 3.2f, 0.20f, 2.5f },
                },
                constants = new[] { Set(F.Knockback, 0.3f), Set(F.MoveSpeed, 0.15f), Set(F.AmountScale, 0.2f) },
                evolution = new EvolutionSpec
                {
                    id = "maelstrom", name = "Maelstrom", passiveId = PassiveIds.WINDSTRIDE_BOOTS,
                    description = "20 damage per 0.20 s, radius 3.6 m, 4 s spin every 4 s. Pulls enemies within 5 m inward; +30% move speed while spinning.",
                    overrides = new[]
                    {
                        Set(F.Damage, 20), Set(F.Interval, 0.20f), Set(F.Radius, 3.6f), Set(F.Duration, 4.0f), Set(F.Cooldown, 4.0f),
                        Set(F.Pull, 1.5f), Set(F.SubRange, 5), Set(F.MoveSpeed, 0.30f),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "fault_line", name = "Fault Line", rarity = Rarity.Rare, owner = HeroClass.Barbarian,
                tags = WeaponTags.Area,
                create = (context, def, slot) => new FaultLine(context, def, slot),
                description = "A stomp sends fissures at the nearest enemies; rock spikes erupt along each line and stun.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Length"), Column(F.Count, "Fissures"), STUN, Column(F.Width, "Width") },
                rows = new float[,]
                {
                    { 30, 4.0f, 6, 1, 0.4f, 1.2f },
                    { 30, 4.0f, 6, 2, 0.4f, 1.2f },
                    { 40, 4.0f, 6, 2, 0.4f, 1.2f },
                    { 40, 4.0f, 8, 2, 0.4f, 1.2f },
                    { 40, 3.6f, 8, 3, 0.4f, 1.2f },
                    { 52, 3.6f, 8, 3, 0.5f, 1.2f },
                    { 52, 3.6f, 10, 3, 0.5f, 1.6f },
                    { 65, 3.6f, 10, 4, 0.5f, 1.6f },
                },
                constants = new[] { Set(F.Range, 12), Set(F.Interval, 0.3f), Set(F.Knockback, 0.5f) },
                evolution = new EvolutionSpec
                {
                    id = "tectonic_rift", name = "Tectonic Rift", passiveId = PassiveIds.PLATE_PAULDRON,
                    description = "6 fissures in a star aimed at the nearest enemy, 80 damage, 12 m. They leave glowing lava cracks for 3 s (12 damage per 0.5 s).",
                    overrides = new[] { Set(F.Count, 6), Set(F.Damage, 80), Set(F.Radius, 12), Set(F.SubDuration, 3), Set(F.SubDamage, 12), Set(F.SubInterval, 0.5f) },
                },
            }),

            // ------------------------------------------------------------------------------------------------ Mage
            Define(new WeaponSpec
            {
                id = "ember_orb", name = "Ember Orb", rarity = Rarity.Common, owner = HeroClass.Mage,
                tags = WeaponTags.Projectile | WeaponTags.Area,
                create = (context, def, slot) => new EmberOrb(context, def, slot),
                description = "A fireball that explodes on the first enemy and leaves it burning.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Blast"), Column(F.Count, "Orbs"), Column(F.Burn, "Burn/tick") },
                rows = new float[,]
                {
                    { 16, 1.20f, 1.6f, 1, 3 },
                    { 16, 1.20f, 1.6f, 2, 3 },
                    { 22, 1.20f, 1.6f, 2, 3 },
                    { 22, 1.20f, 2.0f, 2, 3 },
                    { 22, 1.10f, 2.0f, 3, 3 },
                    { 28, 1.10f, 2.0f, 3, 5 },
                    { 28, 1.10f, 2.4f, 3, 5 },
                    { 34, 1.00f, 2.4f, 4, 5 },
                },
                constants = new[]
                {
                    Set(F.Speed, 10), Set(F.Range, 12), Set(F.BurnTick, 0.5f), Set(F.SubDuration, 2.0f),
                    Set(F.Knockback, 0.6f), Set(F.Stagger, 0.06f), Set(F.Turn, 240),
                },
                evolution = new EvolutionSpec
                {
                    id = "sunfire_orb", name = "Sunfire Orb", passiveId = PassiveIds.QUICKENING_HOURGLASS,
                    description = "5 orbs, 40 damage, blast 2.6 m, burn 8 per 0.5 s. Each blast throws 3 embers 3 m outward (15 damage, radius 1 m); a burn that would finish its target consumes it at once.",
                    overrides = new[]
                    {
                        Set(F.Count, 5), Set(F.Damage, 40), Set(F.Radius, 2.6f), Set(F.Burn, 8),
                        Set(F.SubCount, 3), Set(F.SubRange, 3), Set(F.SubDamage, 15), Set(F.SubRadius, 1.0f),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "starfall", name = "Starfall", rarity = Rarity.Uncommon, owner = HeroClass.Mage,
                tags = WeaponTags.Area | WeaponTags.Lingering,
                create = (context, def, slot) => new Starfall(context, def, slot),
                description = "Meteors fall on the crowds in view and leave burning craters.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Radius"), Column(F.Count, "Meteors"), Column(F.SubDuration, "Crater"), Column(F.SubDamage, "Crater/0.5 s") },
                rows = new float[,]
                {
                    { 38, 6.0f, 2.5f, 1, 2, 3 },
                    { 49, 6.0f, 2.5f, 1, 2, 3 },
                    { 49, 6.0f, 2.5f, 2, 2, 3 },
                    { 49, 6.0f, 3.0f, 2, 2, 3 },
                    { 64, 5.5f, 3.0f, 2, 2, 3 },
                    { 64, 5.5f, 3.0f, 3, 3, 4.5f },
                    { 64, 5.5f, 3.5f, 3, 3, 4.5f },
                    { 82, 5.0f, 3.5f, 4, 3, 4.5f },
                },
                constants = new[]
                {
                    Set(F.Range, 16), Set(F.Interval, 0.6f), Set(F.Stagger, 0.2f), Set(F.Knockback, 2.0f),
                    Set(F.SubInterval, 0.5f),
                },
                notes = new[] { Note(3, "Meteors fall 0.2 s apart.") },
                evolution = new EvolutionSpec
                {
                    id = "cataclysm", name = "Cataclysm", passiveId = PassiveIds.WIDE_SIGIL,
                    description = "Every 7 s a great meteor strikes the densest crowd (225 damage, radius 7 m) ringed by 6 meteors (82 damage). Craters burn 4 s for 8 per 0.5 s.",
                    overrides = new[]
                    {
                        Set(F.Cooldown, 7), Set(F.Count, 6), Set(F.Damage, 82), Set(F.SubDuration, 4), Set(F.SubDamage, 8),
                        Set(F.MajorDamage, 225), Set(F.MajorRadius, 7), Set(F.MajorDelay, 0.9f), Set(F.Stagger, 0.08f),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "rime_nova", name = "Rime Nova", rarity = Rarity.Rare, owner = HeroClass.Mage,
                tags = WeaponTags.Area,
                create = (context, def, slot) => new RimeNova(context, def, slot),
                description = "A frost ring bursts from the mage, chilling enemies (-40% speed) and sometimes freezing them.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Radius"), Column(F.Count, "Novas"), Column(F.Chance, "Freeze chance") },
                rows = new float[,]
                {
                    { 12, 5.0f, 4.0f, 1, 0 },
                    { 16, 5.0f, 4.0f, 1, 0 },
                    { 16, 5.0f, 4.6f, 1, 0 },
                    { 16, 5.0f, 4.6f, 1, 0.15f },
                    { 22, 4.5f, 4.6f, 1, 0.15f },
                    { 22, 4.5f, 5.2f, 1, 0.25f },
                    { 28, 4.5f, 5.2f, 1, 0.25f },
                    { 28, 4.0f, 5.8f, 1, 0.35f },
                },
                constants = new[]
                {
                    Set(F.Slow, 0.4f), Set(F.SubDuration, 2.0f), Set(F.Freeze, 1.5f), Set(F.Interval, 0.4f),
                    Set(F.Stagger, 0.5f), Set(F.AmountScale, 0.7f), Set(F.Knockback, 0.3f),
                },
                notes = new[] { Note(1, "Chill only.") },
                evolution = new EvolutionSpec
                {
                    id = "absolute_zero", name = "Absolute Zero", passiveId = PassiveIds.EVERBURN_WICK,
                    description = "Radius 7 m, 36 damage, 5 s. Freezes every enemy (elites 2 s, bosses are slowed 50%); frozen enemies that die shatter for 40 damage in 1.5 m.",
                    overrides = new[]
                    {
                        Set(F.Radius, 7.0f), Set(F.Damage, 36), Set(F.Cooldown, 5.0f), Set(F.Chance, 1.0f), Set(F.Freeze, 2.0f),
                        Set(F.SubDamage, 40), Set(F.SubRadius, 1.5f),
                    },
                },
            }),

            // ----------------------------------------------------------------------------------------------- Rogue
            Define(new WeaponSpec
            {
                id = "repeater_crossbow", name = "Repeater Crossbow", rarity = Rarity.Common, owner = HeroClass.Rogue,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new RepeaterCrossbow(context, def, slot),
                description = "Fast bolts in a burst at the nearest enemy, piercing through.",
                columns = new[] { DAMAGE, COOLDOWN, RANGE, Column(F.Count, "Bolts"), PIERCE, Column(F.CritDamage, "Crit damage") },
                rows = new float[,]
                {
                    { 11, 0.75f, 16, 1, 2, 0 },
                    { 11, 0.75f, 16, 2, 2, 0 },
                    { 14, 0.75f, 16, 2, 2, 0 },
                    { 14, 0.75f, 16, 2, 3, 0 },
                    { 14, 0.65f, 16, 3, 3, 0 },
                    { 18, 0.65f, 16, 3, 3, 0 },
                    { 18, 0.65f, 16, 3, 4, 0.25f },
                    { 22, 0.55f, 16, 4, 4, 0.25f },
                },
                constants = new[] { Set(F.Speed, 24), Set(F.Interval, 0.08f), Set(F.Knockback, 0.4f) },
                evolution = new EvolutionSpec
                {
                    id = "widowmaker", name = "Widowmaker", passiveId = PassiveIds.KEEN_EYE,
                    description = "24 damage, 0.50 s, 5 bolts, pierce all, +15% crit chance. Crits ricochet to 1 more enemy within 5 m.",
                    overrides = new[] { Set(F.Damage, 24), Set(F.Cooldown, 0.50f), Set(F.Count, 5), Set(F.Pierce, -1), Set(F.CritChance, 0.15f), Set(F.SubRange, 5) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "fan_of_knives", name = "Fan of Knives", rarity = Rarity.Uncommon, owner = HeroClass.Rogue,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new FanOfKnives(context, def, slot),
                description = "Knives fly out in every direction; the ring turns 15 deg each volley.",
                columns = new[] { DAMAGE, COOLDOWN, RANGE, Column(F.Count, "Knives"), PIERCE },
                rows = new float[,]
                {
                    { 10, 1.6f, 9, 6, 1 },
                    { 10, 1.6f, 9, 8, 1 },
                    { 13, 1.6f, 9, 8, 1 },
                    { 13, 1.6f, 9, 8, 2 },
                    { 13, 1.4f, 10, 10, 2 },
                    { 16, 1.4f, 10, 10, 2 },
                    { 16, 1.4f, 10, 12, 3 },
                    { 20, 1.2f, 10, 12, 3 },
                },
                constants = new[] { Set(F.Speed, 16), Set(F.Angle, 15), Set(F.AmountScale, 2), Set(F.SubDuration, 0.5f), Set(F.SubCount, 1), Set(F.Knockback, 0.3f) },
                notes = new[] { Note(8, "Knives return to the rogue.") },
                evolution = new EvolutionSpec
                {
                    id = "storm_of_steel", name = "Storm of Steel", passiveId = PassiveIds.ECHO_QUIVER,
                    description = "2 volleys 0.15 s apart, 16 knives each, 24 damage, pierce 4. Knives always return.",
                    overrides = new[] { Set(F.SubCount, 2), Set(F.Stagger, 0.15f), Set(F.Count, 16), Set(F.Damage, 24), Set(F.Pierce, 4), Set(F.Returns, 1) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "assassins_mark", name = "Assassin's Mark", rarity = Rarity.Rare, owner = HeroClass.Rogue,
                tags = WeaponTags.SingleTarget,
                create = (context, def, slot) => new AssassinsMark(context, def, slot),
                description = "Marks the strongest enemies: they take more damage, a shadow clone stabs them, and they burst into seeking daggers on death.",
                columns = new[] { Column(F.Damage, "Strike"), COOLDOWN, RANGE, Column(F.Count, "Marks"), Column(F.Bonus, "Bonus vs mark"), Column(F.SubCount, "Daggers"), Column(F.SubDamage, "Dagger damage") },
                rows = new float[,]
                {
                    { 40, 3.0f, 14, 1, 0.20f, 4, 12 },
                    { 55, 3.0f, 14, 1, 0.20f, 4, 12 },
                    { 55, 3.0f, 14, 1, 0.20f, 6, 12 },
                    { 55, 3.0f, 14, 2, 0.20f, 6, 12 },
                    { 75, 2.6f, 14, 2, 0.30f, 6, 16 },
                    { 75, 2.6f, 14, 2, 0.30f, 8, 16 },
                    { 75, 2.6f, 16, 3, 0.30f, 8, 16 },
                    { 100, 2.2f, 16, 3, 0.40f, 8, 20 },
                },
                constants = new[] { Set(F.Stagger, 0.15f), Set(F.SubSpeed, 14), Set(F.Turn, 400), Set(F.Knockback, 0.3f) },
                evolution = new EvolutionSpec
                {
                    id = "death_sentence", name = "Death Sentence", passiveId = PassiveIds.LUCKY_KNUCKLEBONE,
                    description = "4 marks, strike 130 that always crits. Marks below 15% health are executed at once (bosses excepted). Daggers 10 x 24.",
                    overrides = new[] { Set(F.Count, 4), Set(F.Damage, 130), Set(F.CritChance, 1), Set(F.Execute, 0.15f), Set(F.SubCount, 10), Set(F.SubDamage, 24) },
                },
            }),

            // ---------------------------------------------------------------------------------------------- Shared
            Define(new WeaponSpec
            {
                id = "spirit_darts", name = "Spirit Darts", rarity = Rarity.Common,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new SpiritDarts(context, def, slot),
                description = "Homing darts that hunt the nearest enemies.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Count, "Darts"), PIERCE, Column(F.Speed, "Speed") },
                rows = new float[,]
                {
                    { 10, 1.20f, 1, 1, 12 },
                    { 10, 1.20f, 2, 1, 12 },
                    { 13, 1.20f, 2, 1, 12 },
                    { 13, 1.10f, 3, 1, 12 },
                    { 13, 1.10f, 3, 2, 12 },
                    { 17, 1.10f, 4, 2, 12 },
                    { 17, 1.00f, 4, 2, 14 },
                    { 21, 1.00f, 5, 2, 14 },
                },
                constants = new[] { Set(F.Turn, 360), Set(F.Range, 14), Set(F.Stagger, 0.06f), Set(F.Knockback, 0.3f) },
                evolution = new EvolutionSpec
                {
                    id = "soul_hail", name = "Soul Hail", passiveId = PassiveIds.TOME_OF_INSIGHT,
                    description = "A continuous stream: 1 dart per 0.12 s, 24 damage, pierce 3. Kills by darts give +1 XP.",
                    overrides = new[] { Set(F.Cooldown, 0.12f), Set(F.Count, 1), Set(F.Damage, 24), Set(F.Pierce, 3), Set(F.ExperiencePerKill, 1), Set(F.AmountScale, 0.5f) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "ricochet_disc", name = "Ricochet Disc", rarity = Rarity.Uncommon,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new RicochetDisc(context, def, slot),
                description = "A disc that bounces from enemy to enemy, pinging higher with every bounce.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.SubRange, "Bounce range"), Column(F.Count, "Discs"), Column(F.SubCount, "Bounces") },
                rows = new float[,]
                {
                    { 12, 1.60f, 5, 1, 3 },
                    { 12, 1.60f, 5, 1, 4 },
                    { 16, 1.60f, 5, 1, 4 },
                    { 16, 1.60f, 5, 2, 4 },
                    { 16, 1.45f, 6, 2, 6 },
                    { 20, 1.45f, 6, 2, 6 },
                    { 20, 1.45f, 6, 3, 6 },
                    { 25, 1.30f, 6, 3, 8 },
                },
                constants = new[] { Set(F.Speed, 14), Set(F.Range, 12), Set(F.Stagger, 0.1f), Set(F.Knockback, 0.4f) },
                evolution = new EvolutionSpec
                {
                    id = "eclipse_disc", name = "Eclipse Disc", passiveId = PassiveIds.EVERBURN_WICK,
                    description = "3 discs bounce without limit for 4 s, 30 damage, +5% damage per bounce (max +100%).",
                    overrides = new[] { Set(F.Count, 3), Set(F.SubCount, -1), Set(F.Duration, 4), Set(F.Damage, 30), Set(F.Bonus, 0.05f), Set(F.BonusCap, 1.0f) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "bramble_halo", name = "Bramble Halo", rarity = Rarity.Common,
                tags = WeaponTags.Area,
                create = (context, def, slot) => new BrambleHalo(context, def, slot),
                description = "A ring of thorns around the hero that hurts and slows whatever stands in it.",
                columns = new[] { Column(F.Damage, "Damage/tick"), Column(F.Interval, "Tick"), Column(F.Radius, "Radius"), Column(F.Slow, "Slow") },
                rows = new float[,]
                {
                    { 4, 0.50f, 1.8f, 0.15f },
                    { 4, 0.50f, 2.1f, 0.15f },
                    { 6, 0.50f, 2.1f, 0.15f },
                    { 6, 0.50f, 2.1f, 0.25f },
                    { 8, 0.50f, 2.5f, 0.25f },
                    { 8, 0.40f, 2.5f, 0.25f },
                    { 10, 0.40f, 2.9f, 0.25f },
                    { 13, 0.40f, 2.9f, 0.35f },
                },
                evolution = new EvolutionSpec
                {
                    id = "briar_sanctum", name = "Briar Sanctum", passiveId = PassiveIds.IRON_HEART,
                    description = "16 damage per 0.35 s, radius 3.6 m, slow 40%. Enemies inside take +10% damage from everything; heals 0.5 HP per tick that hits.",
                    overrides = new[] { Set(F.Damage, 16), Set(F.Interval, 0.35f), Set(F.Radius, 3.6f), Set(F.Slow, 0.40f), Set(F.Bonus, 0.10f), Set(F.Heal, 0.5f) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "skewer_javelin", name = "Skewer Javelin", rarity = Rarity.Common,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new SkewerJavelin(context, def, slot),
                description = "A heavy javelin thrown where the hero walks; it pierces everything and drags enemies along.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Size, "Size"), Column(F.Count, "Javelins"), KNOCKBACK },
                rows = new float[,]
                {
                    { 18, 1.80f, 1.00f, 1, 1.5f },
                    { 24, 1.80f, 1.00f, 1, 1.5f },
                    { 24, 1.80f, 1.00f, 2, 1.5f },
                    { 24, 1.80f, 1.25f, 2, 1.5f },
                    { 32, 1.60f, 1.25f, 2, 1.5f },
                    { 32, 1.60f, 1.25f, 3, 1.5f },
                    { 40, 1.60f, 1.25f, 3, 2.5f },
                    { 48, 1.40f, 1.25f, 4, 2.5f },
                },
                constants = new[] { Set(F.Speed, 18), Set(F.Range, 14), Set(F.Width, 0.6f), Set(F.Angle, 12) },
                notes = new[] { Note(3, "Javelins fan out 12 deg apart.") },
                evolution = new EvolutionSpec
                {
                    id = "godspear", name = "Godspear", passiveId = PassiveIds.WHETSTONE,
                    description = "5 javelins, 60 damage. Each detonates at max range (radius 2.5 m, 60 damage).",
                    overrides = new[] { Set(F.Count, 5), Set(F.Damage, 60), Set(F.SubRadius, 2.5f), Set(F.SubDamage, 60) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "fire_flask", name = "Fire Flask", rarity = Rarity.Uncommon,
                tags = WeaponTags.Area | WeaponTags.Lingering,
                create = (context, def, slot) => new FireFlask(context, def, slot),
                description = "Lobbed flasks shatter into burning pools.",
                columns = new[] { Column(F.Damage, "Damage/tick"), COOLDOWN, Column(F.Radius, "Pool"), Column(F.Count, "Flasks"), DURATION },
                rows = new float[,]
                {
                    { 6, 3.5f, 1.5f, 1, 2.5f },
                    { 6, 3.5f, 1.5f, 2, 2.5f },
                    { 8, 3.5f, 1.5f, 2, 2.5f },
                    { 8, 3.5f, 1.8f, 2, 2.5f },
                    { 8, 3.5f, 1.8f, 3, 3.0f },
                    { 11, 3.5f, 1.8f, 3, 3.0f },
                    { 11, 3.0f, 2.1f, 3, 3.0f },
                    { 14, 3.0f, 2.1f, 4, 3.5f },
                },
                constants = new[] { Set(F.Interval, 0.4f), Set(F.Range, 9), Set(F.SubInterval, 0.6f), Set(F.Stagger, 0.08f) },
                evolution = new EvolutionSpec
                {
                    id = "hellfire_cask", name = "Hellfire Cask", passiveId = PassiveIds.WIDE_SIGIL,
                    description = "6 pools of radius 3 m in a ring 5 m around the hero, 18 damage per 0.4 s for 4 s.",
                    overrides = new[] { Set(F.Count, 6), Set(F.Radius, 3.0f), Set(F.SubRange, 5), Set(F.Damage, 18), Set(F.Duration, 4) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "storm_totem", name = "Storm Totem", rarity = Rarity.Rare,
                tags = WeaponTags.Summon | WeaponTags.Lingering,
                create = (context, def, slot) => new StormTotem(context, def, slot),
                description = "A totem planted at the hero zaps nearby enemies every 0.5 s with chaining lightning.",
                columns = new[] { DAMAGE, COOLDOWN, RANGE, Column(F.Count, "Totems"), Column(F.SubCount, "Chain"), DURATION },
                rows = new float[,]
                {
                    { 9, 7.0f, 6.0f, 1, 1, 5 },
                    { 12, 7.0f, 6.0f, 1, 1, 5 },
                    { 12, 7.0f, 6.0f, 1, 2, 5 },
                    { 12, 7.0f, 6.0f, 1, 2, 6 },
                    { 12, 6.5f, 6.0f, 2, 2, 6 },
                    { 16, 6.5f, 6.0f, 2, 2, 6 },
                    { 16, 6.5f, 7.5f, 2, 3, 6 },
                    { 20, 6.0f, 7.5f, 3, 3, 6 },
                },
                constants = new[] { Set(F.Interval, 0.5f), Set(F.SubRange, 1.5f), Set(F.SubRadius, 4.0f), Set(F.Knockback, 0.2f) },
                notes = new[] { Note(5, "Totems after the first stand 1.5 m away.") },
                evolution = new EvolutionSpec
                {
                    id = "thunderspire", name = "Thunderspire", passiveId = PassiveIds.QUICKENING_HOURGLASS,
                    description = "A floating rod follows the hero for good and zaps every 0.25 s: 22 damage, chain 5, range 8 m.",
                    overrides = new[] { Set(F.Interval, 0.25f), Set(F.Damage, 22), Set(F.SubCount, 5), Set(F.Range, 8), Set(F.Duration, -1) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "hunting_wisps", name = "Hunting Wisps", rarity = Rarity.Rare,
                tags = WeaponTags.Summon,
                create = (context, def, slot) => new HuntingWisps(context, def, slot),
                description = "Spirit wisps circle the hero and dive at random enemies within 10 m.",
                columns = new[] { DAMAGE, Column(F.Cooldown, "Dive interval"), Column(F.SubRadius, "Dive radius"), Column(F.Count, "Wisps") },
                rows = new float[,]
                {
                    { 12, 1.4f, 0.8f, 2 },
                    { 12, 1.4f, 0.8f, 3 },
                    { 16, 1.4f, 0.8f, 3 },
                    { 16, 1.2f, 0.8f, 3 },
                    { 16, 1.2f, 0.8f, 4 },
                    { 21, 1.2f, 0.8f, 4 },
                    { 21, 1.2f, 1.2f, 5 },
                    { 27, 1.0f, 1.2f, 5 },
                },
                constants = new[] { Set(F.Range, 10), Set(F.Speed, 18), Set(F.Knockback, 0.4f) },
                evolution = new EvolutionSpec
                {
                    id = "wisp_choir", name = "Wisp Choir", passiveId = PassiveIds.LODESTONE_CHARM,
                    description = "8 wisps, 30 damage, a dive every 0.8 s. The wisps carry gems within 15 m back to the hero.",
                    overrides = new[] { Set(F.Count, 8), Set(F.Damage, 30), Set(F.Cooldown, 0.8f), Set(F.SubRange, 15) },
                },
            }),
        };

        /// <summary>
        /// Every weapon: the core ones above, then the rest of each class's weapons (Knight, Barbarian, Mage, Rogue),
        /// defined beside their classes (KnightCatalog.cs, ...).
        /// </summary>
        public static readonly WeaponDef[] weapons = Combine(coreWeapons, KnightWeapons(), BarbarianWeapons(), MageWeapons(), RogueWeapons());

        /// <summary>The passive items, linear per rank.</summary>
        public static readonly PassiveDef[] passives =
        {
            Passive(PassiveIds.IRON_HEART, "Iron Heart", Rarity.Common, 5,
                    new PassiveEffect(StatId.MaxHealth, true, 0.10f, "Max Health"),
                    new PassiveEffect(StatId.Regeneration, false, 0.2f, "Regen", " HP/s")),
            Passive(PassiveIds.PLATE_PAULDRON, "Plate Pauldron", Rarity.Common, 5, new PassiveEffect(StatId.Armor, false, 2.0f, "Armor")),
            Passive(PassiveIds.WINDSTRIDE_BOOTS, "Windstride Boots", Rarity.Common, 5, new PassiveEffect(StatId.MoveSpeed, true, 0.07f, "Move Speed")),
            Passive(PassiveIds.WHETSTONE, "Whetstone", Rarity.Common, 5, new PassiveEffect(StatId.Might, true, 0.08f, "Might")),
            Passive(PassiveIds.LODESTONE_CHARM, "Lodestone Charm", Rarity.Common, 5, new PassiveEffect(StatId.PickupRadius, true, 0.25f, "Pickup Radius")),
            Passive(PassiveIds.WIDE_SIGIL, "Wide Sigil", Rarity.Uncommon, 5, new PassiveEffect(StatId.Area, true, 0.08f, "Area")),
            Passive(PassiveIds.EVERBURN_WICK, "Everburn Wick", Rarity.Uncommon, 5,
                    new PassiveEffect(StatId.Duration, true, 0.10f, "Duration"),
                    new PassiveEffect(StatId.ProjectileSpeed, true, 0.10f, "Projectile Speed")),
            Passive(PassiveIds.TOME_OF_INSIGHT, "Tome of Insight", Rarity.Uncommon, 5, new PassiveEffect(StatId.Growth, true, 0.08f, "Growth")),
            Passive(PassiveIds.KEEN_EYE, "Keen Eye", Rarity.Uncommon, 5,
                    new PassiveEffect(StatId.CritChance, false, 0.04f, "Crit Chance", "", true),
                    new PassiveEffect(StatId.CritDamage, false, 0.10f, "Crit Damage", "", true)),
            Passive(PassiveIds.LUCKY_KNUCKLEBONE, "Lucky Knucklebone", Rarity.Uncommon, 5, new PassiveEffect(StatId.Luck, false, 10.0f, "Luck")),
            Passive(PassiveIds.QUICKENING_HOURGLASS, "Quickening Hourglass", Rarity.Rare, 5, new PassiveEffect(StatId.Cooldown, true, -0.06f, "Cooldown")),
            Passive(PassiveIds.ECHO_QUIVER, "Echo Quiver", Rarity.Epic, 2, new PassiveEffect(StatId.Amount, false, 1.0f, "Amount")),
        };

        /// <summary>The slotless boons, first-pick values.</summary>
        public static readonly BoonDef[] boons =
        {
            Boon("boon_fleetfoot", "Fleetfoot", new PassiveEffect(StatId.MoveSpeed, true, 0.03f, "Move Speed")),
            Boon("boon_magnetism", "Magnetism", new PassiveEffect(StatId.PickupRadius, true, 0.10f, "Pickup Radius")),
            Boon("boon_vitality", "Vitality", new PassiveEffect(StatId.MaxHealth, true, 0.04f, "Max Health")),
            Boon("boon_mending", "Mending", new PassiveEffect(StatId.Regeneration, false, 0.1f, "Regen", " HP/s")),
            Boon("boon_hide", "Hide", new PassiveEffect(StatId.Armor, false, 0.6f, "Armor")),
            Boon("boon_edge", "Edge", new PassiveEffect(StatId.Might, true, 0.025f, "Might")),
            Boon("boon_alacrity", "Alacrity", new PassiveEffect(StatId.Cooldown, true, -0.015f, "Cooldown")),
            Boon("boon_breadth", "Breadth", new PassiveEffect(StatId.Area, true, 0.025f, "Area")),
            Boon("boon_persistence", "Persistence",
                 new PassiveEffect(StatId.Duration, true, 0.03f, "Duration"),
                 new PassiveEffect(StatId.ProjectileSpeed, true, 0.03f, "Projectile Speed")),
            Boon("boon_precision", "Precision", new PassiveEffect(StatId.CritChance, false, 0.012f, "Crit Chance", "", true)),
            Boon("boon_insight", "Insight", new PassiveEffect(StatId.Growth, true, 0.03f, "Growth")),
            Boon("boon_plunder", "Plunder", new PassiveEffect(StatId.Greed, true, 0.04f, "Greed")),
            Boon("boon_clover", "Clover", new PassiveEffect(StatId.Luck, false, 2.0f, "Luck")),
        };

        /// <summary>Share of level-ups that show one boon card while item cards are still available.</summary>
        public const float BOON_CARD_CHANCE = 0.35f;
        /// <summary>Below this share of max health a level-up may offer Hearty Meal, with this chance.</summary>
        public const float HEARTY_MEAL_HEALTH_SHARE = 0.5f;
        public const float HEARTY_MEAL_CHANCE = 0.6f;
        /// <summary>Hearty Meal heals this share of max health.</summary>
        public const float HEARTY_MEAL_HEAL = 0.30f;
        public const int COIN_PURSE_GOLD = 20;
        /// <summary>Battle Hymn's Might bonus for the rest of the run.</summary>
        public const float BATTLE_HYMN_MIGHT = 0.02f;
        /// <summary>The chest's gold item, before the Greed stat.</summary>
        public const int CHEST_GOLD_PER_ITEM = 25;
        /// <summary>Where the UI keeps one icon per item id.</summary>
        public const string ICON_ROOT = "app:/data/UI/Icons/";

        /// <summary>Hearty Meal: offered below half health, and the card a banish falls back to.</summary>
        public static readonly FallbackDef heartyMeal = Fallback("hearty_meal", "Hearty Meal", FallbackKind.Heal, HEARTY_MEAL_HEAL);

        /// <summary>The fallback cards, offered when no item or boon is left.</summary>
        public static readonly FallbackDef[] fallbacks =
        {
            heartyMeal,
            Fallback("coin_purse", "Coin Purse", FallbackKind.Gold, COIN_PURSE_GOLD),
            Fallback("battle_hymn", "Battle Hymn", FallbackKind.Might, BATTLE_HYMN_MIGHT),
        };

        /// <summary>The chest item when every owned item is maxed.</summary>
        public static readonly FallbackDef chestGold = Fallback("gold_hoard", "Gold Hoard", FallbackKind.GreedGold, CHEST_GOLD_PER_ITEM);

        static WeaponCatalog()
        {
            Validate();
        }

        /// <summary>The icon asset key of an item id (weapon, evolution, passive, active, hero).</summary>
        public static string IconKey(string id) => string.IsNullOrEmpty(id) ? "" : ICON_ROOT + id + ".png";

        public static WeaponDef FindWeapon(string id)
        {
            foreach (WeaponDef def in weapons)
            {
                if (def.id == id)
                {
                    return def;
                }
            }
            return null;
        }

        /// <summary>The weapon an evolution id belongs to, or null.</summary>
        public static WeaponDef FindWeaponByEvolution(string evolutionId)
        {
            foreach (WeaponDef def in weapons)
            {
                if (def.evolution != null && def.evolution.id == evolutionId)
                {
                    return def;
                }
            }
            return null;
        }

        public static int FindBoonIndex(string id)
        {
            for (int i = 0; i < boons.Length; i++)
            {
                if (boons[i].id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        public static BoonDef FindBoon(string id)
        {
            int index = FindBoonIndex(id);
            return index >= 0 ? boons[index] : null;
        }

        public static PassiveDef FindPassive(string id)
        {
            foreach (PassiveDef def in passives)
            {
                if (def.id == id)
                {
                    return def;
                }
            }
            return null;
        }

        public static FallbackDef FindFallback(string id)
        {
            foreach (FallbackDef def in fallbacks)
            {
                if (def.id == id)
                {
                    return def;
                }
            }
            return id == chestGold.id ? chestGold : null;
        }

        // -------------------------------------------------------------------------------------------------------
        // Builders
        // -------------------------------------------------------------------------------------------------------

        /// <summary>A weapon as written above, before its levels are built.</summary>
        private sealed class WeaponSpec
        {
            public string id;
            public string name;
            public Rarity rarity;
            /// <summary>The class of a class weapon; null for a shared weapon.</summary>
            public HeroClass? owner;
            public WeaponTags tags;
            /// <summary>Builds the weapon's behaviour class.</summary>
            public WeaponFactory create;
            public string description;
            public WeaponColumn[] columns;
            /// <summary>One row per rank, one value per column.</summary>
            public float[,] rows;
            /// <summary>Numbers every rank shares.</summary>
            public (F field, float value)[] constants = Array.Empty<(F, float)>();
            /// <summary>The card notes of some ranks.</summary>
            public (int rank, string text)[] notes = Array.Empty<(int, string)>();
            public EvolutionSpec evolution;
        }

        private sealed class EvolutionSpec
        {
            public string id;
            public string name;
            public string passiveId;
            public string description;
            /// <summary>The numbers that change from rank 8.</summary>
            public (F field, float value)[] overrides;
        }

        private static WeaponDef Define(WeaponSpec spec)
        {
            WeaponLevel[] levels = BuildLevels(spec);
            return new WeaponDef
            {
                id = spec.id,
                name = spec.name,
                description = spec.description,
                rarity = spec.rarity,
                isClassWeapon = spec.owner.HasValue,
                heroClass = spec.owner.GetValueOrDefault(),
                tags = spec.tags,
                columns = spec.columns,
                levels = levels,
                notes = BuildNotes(spec),
                evolution = spec.evolution != null ? BuildEvolution(spec.evolution, levels[levels.Length - 1]) : null,
                factory = spec.create,
            };
        }

        /// <summary>One level per row of the rank table: the constants, then the row's columns.</summary>
        private static WeaponLevel[] BuildLevels(WeaponSpec spec)
        {
            if (spec.rows.GetLength(0) != WeaponDef.MAX_RANK || spec.rows.GetLength(1) != spec.columns.Length)
            {
                throw new InvalidOperationException($"Weapon {spec.id}: the rank table must have {WeaponDef.MAX_RANK} rows of {spec.columns.Length} columns.");
            }
            var levels = new WeaponLevel[WeaponDef.MAX_RANK];
            for (int rank = 0; rank < levels.Length; rank++)
            {
                var level = new WeaponLevel();
                foreach ((F field, float value) in spec.constants)
                {
                    level.Set(field, value);
                }
                for (int column = 0; column < spec.columns.Length; column++)
                {
                    level.Set(spec.columns[column].field, spec.rows[rank, column]);
                }
                levels[rank] = level;
            }
            return levels;
        }

        /// <summary>A note per rank (index 0..7), empty where the spec has none.</summary>
        private static string[] BuildNotes(WeaponSpec spec)
        {
            var notes = new string[WeaponDef.MAX_RANK];
            Array.Fill(notes, "");
            foreach ((int rank, string text) in spec.notes)
            {
                if (rank < 1 || rank > WeaponDef.MAX_RANK)
                {
                    throw new InvalidOperationException($"Weapon {spec.id}: a note names rank {rank}.");
                }
                notes[rank - 1] = text;
            }
            return notes;
        }

        /// <summary>An evolution: rank 8's numbers with the spec's overrides.</summary>
        private static EvolutionDef BuildEvolution(EvolutionSpec spec, WeaponLevel maxRank)
        {
            WeaponLevel evolved = maxRank.Clone();
            foreach ((F field, float value) in spec.overrides)
            {
                evolved.Set(field, value);
            }
            return new EvolutionDef { id = spec.id, name = spec.name, passiveId = spec.passiveId, description = spec.description, level = evolved };
        }

        private static WeaponDef[] Combine(params WeaponDef[][] lists)
        {
            int total = 0;
            foreach (WeaponDef[] list in lists)
            {
                total += list.Length;
            }
            var combined = new WeaponDef[total];
            int next = 0;
            foreach (WeaponDef[] list in lists)
            {
                list.CopyTo(combined, next);
                next += list.Length;
            }
            return combined;
        }

        private static BoonDef Boon(string id, string name, params PassiveEffect[] effects)
        {
            return new BoonDef { id = id, name = name, effects = effects };
        }

        private static PassiveDef Passive(string id, string name, Rarity rarity, int maxRank, params PassiveEffect[] effects)
        {
            return new PassiveDef { id = id, name = name, rarity = rarity, maxRank = maxRank, effects = effects };
        }

        private static FallbackDef Fallback(string id, string name, FallbackKind kind, float amount)
        {
            return new FallbackDef { id = id, name = name, kind = kind, amount = amount, description = WeaponText.DescribeFallback(kind, amount) };
        }

        // -------------------------------------------------------------------------------------------------------
        // Validation
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Checks the tables on first use, so a broken entry fails at once instead of when it is offered mid-run: ids are
        /// unique across every kind of item (offers, banishes and icons share them), every weapon has its behaviour and
        /// 8 ranks, and every evolution names a passive that exists.
        /// </summary>
        private static void Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            foreach (WeaponDef def in weapons)
            {
                ValidateWeapon(def, ids, problems);
            }
            foreach (PassiveDef def in passives)
            {
                AddId(def.id, ids, problems);
            }
            foreach (BoonDef def in boons)
            {
                AddId(def.id, ids, problems);
            }
            foreach (FallbackDef def in fallbacks)
            {
                AddId(def.id, ids, problems);
            }
            AddId(chestGold.id, ids, problems);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException("WeaponCatalog: " + string.Join(" ", problems));
            }
        }

        private static void ValidateWeapon(WeaponDef def, HashSet<string> ids, List<string> problems)
        {
            AddId(def.id, ids, problems);
            if (def.factory == null)
            {
                problems.Add($"Weapon {def.id} has no factory.");
            }
            if (def.levels.Length != WeaponDef.MAX_RANK)
            {
                problems.Add($"Weapon {def.id} has {def.levels.Length} ranks.");
            }
            if (def.evolution == null)
            {
                return;
            }
            AddId(def.evolution.id, ids, problems);
            if (FindPassive(def.evolution.passiveId) == null)
            {
                problems.Add($"Evolution {def.evolution.id} names the unknown passive {def.evolution.passiveId}.");
            }
        }

        private static void AddId(string id, HashSet<string> ids, List<string> problems)
        {
            if (string.IsNullOrEmpty(id) || !ids.Add(id))
            {
                problems.Add($"The item id '{id}' is empty or used twice.");
            }
        }
    }
}

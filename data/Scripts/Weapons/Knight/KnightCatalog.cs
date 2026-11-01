using F = Survivors.WeaponField;

namespace Survivors
{
    public static partial class WeaponCatalog
    {
        /// <summary>
        /// The rest of the Knight's class weapons: Consecration, Dawn Hammer, Lance of Dawn and Phalanx. Lance of Dawn: Damage on
        /// its flight, SubDamage per enemy in the cross of SubCount arms, Radius long, SubRadius wide. Phalanx: Width across,
        /// Range long, Damage once, SubDamage + Bonus per enemy carried (up to BonusCap) when it stops, SubCount walls.
        /// </summary>
        private static WeaponDef[] KnightWeapons() => new[]
        {
            Define(new WeaponSpec
            {
                id = "consecration", name = "Consecration", rarity = Rarity.Uncommon, owner = HeroClass.Knight,
                tags = WeaponTags.Area | WeaponTags.Lingering,
                create = (context, def, slot) => new Consecration(context, def, slot),
                description = "A holy glyph burns the dead and judges them (+10% damage taken). In it the knight has Armor and wider bashes.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Radius"), DURATION, Column(F.Interval, "Tick"), Column(F.Armor, "Armor") },
                rows = new float[,]
                {
                    { 8, 6.0f, 2.6f, 4.0f, 0.5f, 2 },
                    { 10, 6.0f, 2.6f, 4.0f, 0.5f, 2 },
                    { 10, 6.0f, 3.0f, 4.5f, 0.5f, 3 },
                    { 12, 5.5f, 3.0f, 4.5f, 0.5f, 3 },
                    { 12, 5.5f, 3.4f, 5.0f, 0.4f, 4 },
                    { 15, 5.5f, 3.4f, 5.0f, 0.4f, 4 },
                    { 15, 5.0f, 3.8f, 5.5f, 0.4f, 5 },
                    { 18, 5.0f, 3.8f, 6.0f, 0.4f, 6 },
                },
                constants = new[] { Set(F.Bonus, 0.10f), Set(F.SubDuration, 1.0f) },
                notes = new[] { Note(5, "Ticks every 0.4 s.") },
                evolution = new EvolutionSpec
                {
                    id = "eternal_sanctum", name = "Eternal Sanctum", passiveId = PassiveIds.EVERBURN_WICK,
                    description = "The glyph never fades and is replanted under the knight every 6 s. A holy pulse every 2 s hurls the dead back.",
                    overrides = new[]
                    {
                        Set(F.Damage, 18), Set(F.Interval, 0.4f), Set(F.Radius, 4.6f), Set(F.Duration, -1), Set(F.Armor, 8),
                        Set(F.Cooldown, 6.0f), Set(F.SubInterval, 2.0f), Set(F.SubDamage, 40), Set(F.Knockback, 2.0f),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "dawn_hammer", name = "Dawn Hammer", rarity = Rarity.Rare, owner = HeroClass.Knight,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new DawnHammer(context, def, slot),
                description = "Spinning golden hammers fly out and back, stunning what they hit. A stunned enemy struck calls down thunder.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Count, "Hammers"), RANGE, STUN, Column(F.SubDamage, "Thunder") },
                rows = new float[,]
                {
                    { 20, 3.0f, 1, 8, 0.4f, 20 },
                    { 26, 3.0f, 1, 8, 0.4f, 26 },
                    { 26, 3.0f, 2, 8, 0.4f, 26 },
                    { 26, 2.7f, 2, 9, 0.5f, 34 },
                    { 34, 2.7f, 2, 9, 0.5f, 34 },
                    { 34, 2.7f, 3, 9, 0.5f, 44 },
                    { 42, 2.4f, 3, 10, 0.6f, 44 },
                    { 50, 2.2f, 3, 10, 0.6f, 56 },
                },
                constants = new[] { Set(F.Speed, 12), Set(F.SubRadius, 2.0f), Set(F.Knockback, 1.0f) },
                notes = new[] { Note(3, "A 2nd hammer."), Note(6, "A 3rd hammer.") },
                evolution = new EvolutionSpec
                {
                    id = "skybreaker", name = "Skybreaker", passiveId = PassiveIds.LUCKY_KNUCKLEBONE,
                    description = "4 hammers. Any hit may call the thunder (10% + half the Luck), and every thunderclap chains to 2 more enemies.",
                    overrides = new[]
                    {
                        Set(F.Count, 4), Set(F.Damage, 60), Set(F.SubDamage, 70), Set(F.SubRadius, 2.6f), Set(F.Chance, 0.10f),
                        Set(F.SubCount, 2),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "lance_of_dawn", name = "Lance of Dawn", rarity = Rarity.Rare, owner = HeroClass.Knight,
                tags = WeaponTags.Projectile | WeaponTags.Area,
                create = (context, def, slot) => new LanceOfDawn(context, def, slot),
                description = "A spear of light pierces into the densest crowd and bursts there into a cross of stunning beams.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Count, "Lances"), Column(F.SubDamage, "Cross"), Column(F.Radius, "Arm length") },
                rows = new float[,]
                {
                    { 20, 4.5f, 1, 28, 5.0f },
                    { 26, 4.5f, 1, 36, 5.0f },
                    { 26, 4.5f, 1, 36, 6.0f },
                    { 26, 4.0f, 2, 36, 6.0f },
                    { 32, 4.0f, 2, 46, 6.0f },
                    { 32, 4.0f, 2, 46, 7.0f },
                    { 32, 3.5f, 3, 46, 7.0f },
                    { 40, 3.5f, 3, 60, 7.5f },
                },
                constants = new[]
                {
                    Set(F.Range, 16), Set(F.Speed, 30), Set(F.Width, 1.6f), Set(F.SubRadius, 1.0f), Set(F.SubCount, 4), Set(F.Stun, 0.6f),
                    Set(F.Stagger, 0.18f), Set(F.Knockback, 1.0f),
                },
                notes = new[] { Note(4, "A 2nd lance."), Note(7, "A 3rd lance.") },
                evolution = new EvolutionSpec
                {
                    id = "solar_cross", name = "Solar Cross", passiveId = PassiveIds.TOME_OF_INSIGHT,
                    description = "4 lances, each bursting into eight beams around a blazing sun.",
                    overrides = new[] { Set(F.Count, 4), Set(F.Damage, 54), Set(F.SubDamage, 80), Set(F.SubCount, 8), Set(F.Radius, 8.5f) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "phalanx", name = "Phalanx", rarity = Rarity.Uncommon, owner = HeroClass.Knight,
                tags = WeaponTags.Area,
                create = (context, def, slot) => new Phalanx(context, def, slot),
                description = "A shield wall bulldozes the crowd and crushes all it carries; the fuller, the harder.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Width, "Width"), RANGE, Column(F.SubDamage, "Crush") },
                rows = new float[,]
                {
                    { 10, 7.0f, 8.0f, 8, 22 },
                    { 13, 7.0f, 8.0f, 8, 28 },
                    { 13, 7.0f, 9.0f, 9, 28 },
                    { 16, 6.5f, 9.0f, 9, 36 },
                    { 16, 6.5f, 10.0f, 10, 36 },
                    { 20, 6.0f, 10.0f, 10, 46 },
                    { 20, 6.0f, 11.0f, 11, 46 },
                    { 26, 5.5f, 12.0f, 12, 60 },
                },
                constants = new[] { Set(F.Speed, 9), Set(F.Stun, 0.8f), Set(F.Bonus, 0.05f), Set(F.BonusCap, 1.5f), Set(F.SubCount, 1) },
                evolution = new EvolutionSpec
                {
                    id = "immortal_legion", name = "Immortal Legion", passiveId = PassiveIds.IRON_HEART,
                    description = "Four walls march out to every side, and each crushes what it carries.",
                    overrides = new[] { Set(F.SubCount, 4), Set(F.Damage, 20), Set(F.SubDamage, 50) },
                },
            }),
        };
    }
}

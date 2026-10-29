using F = Survivors.WeaponField;

namespace Survivors
{
    public static partial class WeaponCatalog
    {
        /// <summary>The rest of the Knight's class weapons: Consecration and Dawn Hammer.</summary>
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
        };
    }
}

using F = Survivors.WeaponField;

namespace Survivors
{
    public static partial class WeaponCatalog
    {
        /// <summary>The rest of the Barbarian's class weapons: War Cry and Butcher's Chains.</summary>
        private static WeaponDef[] BarbarianWeapons() => new[]
        {
            Define(new WeaponSpec
            {
                id = "war_cry", name = "War Cry", rarity = Rarity.Uncommon, owner = HeroClass.Barbarian,
                tags = WeaponTags.Area,
                create = (context, def, slot) => new WarCry(context, def, slot),
                description = "A roar that scatters the dead, exposes them to more damage, and feeds Fury.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Radius, "Radius"), Column(F.Fear, "Fear"), Column(F.Bonus, "Damage taken"), Column(F.Fury, "Fury per hit") },
                rows = new float[,]
                {
                    { 15, 8.0f, 5.0f, 1.0f, 0.10f, 0.5f },
                    { 20, 8.0f, 5.0f, 1.0f, 0.10f, 0.5f },
                    { 20, 8.0f, 5.5f, 1.2f, 0.15f, 0.5f },
                    { 26, 7.5f, 5.5f, 1.2f, 0.15f, 0.75f },
                    { 26, 7.5f, 6.0f, 1.4f, 0.15f, 0.75f },
                    { 34, 7.0f, 6.0f, 1.4f, 0.20f, 0.75f },
                    { 34, 7.0f, 6.5f, 1.6f, 0.20f, 1.0f },
                    { 44, 6.5f, 7.0f, 1.8f, 0.25f, 1.0f },
                },
                constants = new[] { Set(F.Knockback, 1.5f), Set(F.Duration, 3.0f) },
                evolution = new EvolutionSpec
                {
                    id = "warlords_bellow", name = "Warlord's Bellow", passiveId = PassiveIds.PLATE_PAULDRON,
                    description = "Every 5 s, 8 m: fear 2 s, +30% damage taken, and +10 Armor for 3 s.",
                    overrides = new[]
                    {
                        Set(F.Cooldown, 5.0f), Set(F.Radius, 8.0f), Set(F.Damage, 60), Set(F.Fear, 2.0f), Set(F.Bonus, 0.30f),
                        Set(F.Armor, 10), Set(F.SubDuration, 3.0f),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "butchers_chains", name = "Butcher's Chains", rarity = Rarity.Rare, owner = HeroClass.Barbarian,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new ButchersChains(context, def, slot),
                description = "Hooked chains yank the crowd to the axes; bloodied foes take more damage and feed Fury.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Count, "Chains"), RANGE, Column(F.Pull, "Pull"), Column(F.Width, "Width") },
                rows = new float[,]
                {
                    { 18, 4.0f, 1, 9, 4, 1.2f },
                    { 24, 4.0f, 1, 9, 4, 1.2f },
                    { 24, 4.0f, 2, 9, 5, 1.2f },
                    { 24, 3.6f, 2, 10, 5, 1.4f },
                    { 32, 3.6f, 2, 10, 6, 1.4f },
                    { 32, 3.6f, 3, 10, 6, 1.4f },
                    { 40, 3.2f, 3, 11, 7, 1.6f },
                    { 48, 3.0f, 4, 11, 8, 1.6f },
                },
                constants = new[] { Set(F.Speed, 22), Set(F.Bonus, 0.40f), Set(F.SubDuration, 3.0f), Set(F.Fury, 1.0f) },
                evolution = new EvolutionSpec
                {
                    id = "maw_of_the_pit", name = "Maw of the Pit", passiveId = PassiveIds.LODESTONE_CHARM,
                    description = "Every 5 s a blood vortex drags all within 10 m in, then the chains slam for 80.",
                    overrides = new[]
                    {
                        Set(F.Cooldown, 5.0f), Set(F.Count, 6), Set(F.Radius, 10.0f), Set(F.MajorDamage, 80), Set(F.MajorRadius, 3.0f),
                        Set(F.MajorDelay, 1.0f),
                    },
                },
            }),
        };
    }
}

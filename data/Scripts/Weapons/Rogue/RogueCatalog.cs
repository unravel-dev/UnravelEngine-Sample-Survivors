using F = Survivors.WeaponField;

namespace Survivors
{
    public static partial class WeaponCatalog
    {
        /// <summary>
        /// The rest of the Rogue's class weapons: Venom Shivs and Shadow Dancers. Venom Shivs: Every = stacks a rogue
        /// crit needs to rupture, Multiplier = rupture damage per venom damage left, Stacks = stack cap (-1 none),
        /// SubRadius = splash radius, SubDuration = Nightshade's cloud. Shadow Dancers: Stacks = Momentum needed,
        /// SubRange = metres per clone, SubDamage = fade burst in SubRadius, CritChance = share of the rogue's crit
        /// chance (Legion), MajorCount = clones Smoke Roll leaves (Legion).
        /// </summary>
        private static WeaponDef[] RogueWeapons() => new[]
        {
            Define(new WeaponSpec
            {
                id = "venom_shivs", name = "Venom Shivs", rarity = Rarity.Uncommon, owner = HeroClass.Rogue,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new VenomShivs(context, def, slot),
                description = "Shivs stack venom. A Rogue crit on 3+ stacks ruptures it: triple the venom at once, half the stacks splash.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Count, "Shivs"), Column(F.SubDamage, "Venom per stack"), Column(F.Stacks, "Max stacks"), PIERCE },
                rows = new float[,]
                {
                    { 9, 1.4f, 2, 3, 5, 0 },
                    { 9, 1.4f, 3, 3, 5, 0 },
                    { 12, 1.4f, 3, 3, 6, 0 },
                    { 12, 1.3f, 3, 4.5f, 6, 1 },
                    { 12, 1.3f, 4, 4.5f, 7, 1 },
                    { 15, 1.2f, 4, 4.5f, 8, 1 },
                    { 15, 1.2f, 5, 6, 9, 1 },
                    { 18, 1.0f, 5, 6, 10, 2 },
                },
                constants = new[]
                {
                    Set(F.Range, 9), Set(F.Speed, 18), Set(F.Duration, 4.0f), Set(F.SubRadius, 2.5f), Set(F.Every, 3),
                    Set(F.Multiplier, 3),
                },
                evolution = new EvolutionSpec
                {
                    id = "nightshade_plague", name = "Nightshade Plague", passiveId = PassiveIds.KEEN_EYE,
                    description = "6 shivs, no stack cap. Splashed foes at 3+ stacks rupture in turn; each rupture leaves a 3 s poison cloud.",
                    overrides = new[] { Set(F.Count, 6), Set(F.Damage, 21), Set(F.Stacks, -1), Set(F.SubDuration, 3.0f) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "shadow_dancers", name = "Shadow Dancers", rarity = Rarity.Rare, owner = HeroClass.Rogue,
                tags = WeaponTags.Summon,
                create = (context, def, slot) => new ShadowDancers(context, def, slot),
                description = "With 3+ Momentum, running leaves shadow clones that throw knives and burst when they fade.",
                columns = new[]
                {
                    DAMAGE, Column(F.SubRange, "Clone every"), Column(F.Count, "Clones"), DURATION, Column(F.SubCount, "Knives"), Column(F.Interval, "Throw"),
                    Column(F.SubDamage, "Burst"),
                },
                rows = new float[,]
                {
                    { 11, 3.0f, 2, 3.0f, 1, 1.0f, 17 },
                    { 14, 3.0f, 2, 3.0f, 1, 1.0f, 17 },
                    { 14, 2.6f, 3, 3.0f, 1, 0.9f, 20 },
                    { 14, 2.6f, 3, 3.5f, 1, 0.9f, 22 },
                    { 15, 2.2f, 4, 3.5f, 2, 0.8f, 25 },
                    { 15, 2.2f, 4, 3.5f, 2, 0.8f, 28 },
                    { 17, 1.8f, 5, 4.0f, 2, 0.7f, 31 },
                    { 17, 1.5f, 6, 4.0f, 2, 0.7f, 35 },
                },
                constants = new[]
                {
                    Set(F.Range, 10), Set(F.Speed, 16), Set(F.SubRadius, 1.5f), Set(F.Stacks, 3), Set(F.Turn, 360),
                    Set(F.Knockback, 0.3f),
                },
                evolution = new EvolutionSpec
                {
                    id = "legion_of_shadows", name = "Legion of Shadows", passiveId = PassiveIds.WINDSTRIDE_BOOTS,
                    description = "A clone every metre, up to 10, lasting 5 s with half your crit chance. Smoke Roll leaves 4 clones.",
                    overrides = new[] { Set(F.SubRange, 1.0f), Set(F.Count, 10), Set(F.Duration, 5.0f), Set(F.CritChance, 0.5f), Set(F.MajorCount, 4) },
                },
            }),
        };
    }
}

using F = Survivors.WeaponField;

namespace Survivors
{
    public static partial class WeaponCatalog
    {
        /// <summary>
        /// The rest of the Rogue's class weapons: Venom Shivs, Shadow Dancers, Thousand Cuts and Powder Trail. Thousand Cuts:
        /// Damage per cut, MajorDamage per mark and SubDamage in SubRadius around it after MajorDelay. Powder Trail: Count piles
        /// SubRange apart, SubInterval seconds of fuse per pile, Damage in Radius; Inferno Road burns SubDamage every Interval in
        /// SubRadius for SubDuration. Shadow Dancers: Venom Shivs: Every = stacks a rogue
        /// crit needs to rupture, Multiplier = rupture damage per venom damage left, Stacks = stack cap (-1 none),
        /// SubRadius = splash radius, SubDuration = Nightshade's cloud. Shadow Dancers: Stacks = Momentum needed,
        /// SubRange = metres per clone, Width = blink cut width, SubDamage = fade burst in SubRadius, CritChance = share of
        /// the rogue's crit chance (Legion), MajorCount = clones Smoke Roll leaves.
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
                description = "Running leaves shadow clones that blink through the crowd, cutting all in their path.",
                columns = new[]
                {
                    DAMAGE, Column(F.SubRange, "Clone every"), Column(F.Count, "Clones"), DURATION, Column(F.Interval, "Blink"), Column(F.SubDamage, "Burst"),
                },
                rows = new float[,]
                {
                    { 22, 3.0f, 2, 4.0f, 0.9f, 30 },
                    { 28, 3.0f, 2, 4.0f, 0.9f, 38 },
                    { 28, 2.6f, 3, 4.0f, 0.8f, 38 },
                    { 34, 2.6f, 3, 4.5f, 0.8f, 46 },
                    { 34, 2.2f, 4, 4.5f, 0.7f, 46 },
                    { 40, 2.2f, 4, 4.5f, 0.7f, 56 },
                    { 40, 1.8f, 5, 5.0f, 0.6f, 56 },
                    { 48, 1.5f, 6, 5.0f, 0.55f, 70 },
                },
                constants = new[]
                {
                    Set(F.Range, 9), Set(F.Width, 1.1f), Set(F.SubRadius, 2.5f), Set(F.Stacks, 1), Set(F.Knockback, 0.3f), Set(F.MajorCount, 1),
                },
                evolution = new EvolutionSpec
                {
                    id = "legion_of_shadows", name = "Legion of Shadows", passiveId = PassiveIds.WINDSTRIDE_BOOTS,
                    description = "A clone every metre, up to 10, lasting 6 s with half your crit chance. Smoke Roll leaves 4 clones.",
                    overrides = new[]
                    {
                        Set(F.Damage, 60), Set(F.SubDamage, 90), Set(F.SubRange, 1.0f), Set(F.Count, 10), Set(F.Duration, 6.0f), Set(F.CritChance, 0.5f),
                        Set(F.MajorCount, 4),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "thousand_cuts", name = "Thousand Cuts", rarity = Rarity.Rare, owner = HeroClass.Rogue,
                tags = WeaponTags.Area,
                create = (context, def, slot) => new ThousandCuts(context, def, slot),
                description = "A shadow blade marks every foe it cuts; a heartbeat later, all the marks burst at once.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Count, "Cuts"), Column(F.MajorDamage, "Burst"), Column(F.SubRadius, "Burst radius") },
                rows = new float[,]
                {
                    { 10, 6.0f, 5, 40, 1.2f },
                    { 14, 6.0f, 6, 52, 1.2f },
                    { 14, 6.0f, 7, 52, 1.4f },
                    { 18, 5.5f, 8, 66, 1.4f },
                    { 18, 5.5f, 9, 66, 1.6f },
                    { 24, 5.0f, 10, 84, 1.6f },
                    { 24, 5.0f, 12, 84, 1.8f },
                    { 30, 4.5f, 14, 110, 2.0f },
                },
                constants = new[] { Set(F.Range, 11), Set(F.SubRange, 7), Set(F.Interval, 0.05f), Set(F.MajorDelay, 0.35f), Set(F.SubDamage, 20) },
                evolution = new EvolutionSpec
                {
                    id = "death_blossom", name = "Death Blossom", passiveId = PassiveIds.KEEN_EYE,
                    description = "20 cuts, and every foe that dies in the burst blooms once more where it fell.",
                    overrides = new[] { Set(F.Count, 20), Set(F.Damage, 30), Set(F.MajorDamage, 110), Set(F.SubDamage, 22), Set(F.SubRadius, 2.2f) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "powder_trail", name = "Powder Trail", rarity = Rarity.Rare, owner = HeroClass.Rogue,
                tags = WeaponTags.Area | WeaponTags.Lingering,
                create = (context, def, slot) => new PowderTrail(context, def, slot),
                description = "You spill powder as you run. Fire races along it and the horde behind you goes up.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Count, "Length"), Column(F.Radius, "Blast radius") },
                rows = new float[,]
                {
                    { 26, 4.0f, 10, 1.6f },
                    { 34, 4.0f, 10, 1.6f },
                    { 34, 4.0f, 12, 1.8f },
                    { 44, 3.6f, 12, 1.8f },
                    { 44, 3.6f, 14, 2.0f },
                    { 56, 3.2f, 14, 2.0f },
                    { 56, 3.2f, 16, 2.2f },
                    { 72, 2.8f, 18, 2.4f },
                },
                constants = new[] { Set(F.SubRange, 1.0f), Set(F.SubInterval, 0.045f), Set(F.Knockback, 1.0f) },
                evolution = new EvolutionSpec
                {
                    id = "inferno_road", name = "Inferno Road", passiveId = PassiveIds.EVERBURN_WICK,
                    description = "A far longer trail with wider blasts, and the road burns on behind for 3 s.",
                    overrides = new[]
                    {
                        Set(F.Count, 28), Set(F.Damage, 90), Set(F.Radius, 2.8f), Set(F.SubDamage, 16), Set(F.SubRadius, 1.6f), Set(F.Interval, 0.4f),
                        Set(F.SubDuration, 3.0f),
                    },
                },
            }),
        };
    }
}

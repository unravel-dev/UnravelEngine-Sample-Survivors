using F = Survivors.WeaponField;

namespace Survivors
{
    public static partial class WeaponCatalog
    {
        /// <summary>
        /// The rest of the Mage's class weapons: Arc Lash, Prismatic Ray, Singularity and Firebird. Singularity: Pull in Radius,
        /// SubDamage every Interval for Duration, then MajorDamage in MajorRadius; Bonus = Event Horizon's mark for SubDuration.
        /// Firebird: Damage once and Burn per BurnTick for SubDuration under wings Width across, Angle between birds. Arc Lash: Count = jumps after the first
        /// strike, Bonus = extra damage to frozen enemies, Burn per BurnTick for SubDuration = the burn a bolt passes on.
        /// Prismatic Ray: Damage per beam crossing, Burn per BurnTick and the chill (Slow) last SubDuration; thermal
        /// shock = SubDamage x (1 + 0.1 x level) in SubRadius, once per SubInterval per enemy.
        /// </summary>
        private static WeaponDef[] MageWeapons() => new[]
        {
            Define(new WeaponSpec
            {
                id = "arc_lash", name = "Arc Lash", rarity = Rarity.Uncommon, owner = HeroClass.Mage,
                tags = WeaponTags.Projectile,
                create = (context, def, slot) => new ArcLash(context, def, slot),
                description = "A bolt jumps from enemy to enemy. Frozen ones take double damage; burning ones pass on their fire.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Count, "Jumps"), Column(F.SubRange, "Jump range"), Column(F.SubCount, "Bolts") },
                rows = new float[,]
                {
                    { 15, 1.6f, 3, 5.0f, 1 },
                    { 15, 1.6f, 4, 5.0f, 1 },
                    { 19, 1.6f, 4, 5.0f, 1 },
                    { 19, 1.5f, 5, 5.5f, 1 },
                    { 19, 1.5f, 5, 5.5f, 2 },
                    { 25, 1.4f, 6, 6.0f, 2 },
                    { 25, 1.4f, 7, 6.0f, 2 },
                    { 31, 1.3f, 8, 6.5f, 2 },
                },
                constants = new[]
                {
                    Set(F.Range, 10), Set(F.Bonus, 1.0f), Set(F.Interval, 0.05f), Set(F.Burn, 3), Set(F.BurnTick, 0.5f),
                    Set(F.SubDuration, 2.0f),
                },
                notes = new[] { Note(5, "A second bolt.") },
                evolution = new EvolutionSpec
                {
                    id = "tempest_crown", name = "Tempest Crown", passiveId = PassiveIds.ECHO_QUIVER,
                    description = "A storm crown hurls a 10-jump bolt every 0.5 s that forks at every frozen or burning enemy.",
                    overrides = new[] { Set(F.Cooldown, 0.5f), Set(F.Count, 10), Set(F.Damage, 36), Set(F.SubCount, 1) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "prismatic_ray", name = "Prismatic Ray", rarity = Rarity.Rare, owner = HeroClass.Mage,
                tags = WeaponTags.Area | WeaponTags.Lingering,
                create = (context, def, slot) => new PrismaticRay(context, def, slot),
                description = "Beams sweep around the mage, burning and chilling. Burning, chilled enemies burst in thermal shock.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Range, "Length"), Column(F.Count, "Beams"), Column(F.Duration, "Sweep"), Column(F.Burn, "Burn/0.5 s") },
                rows = new float[,]
                {
                    { 5, 7.0f, 7, 1, 1.6f, 2 },
                    { 6, 7.0f, 7, 1, 1.6f, 2 },
                    { 6, 7.0f, 8, 1, 1.8f, 2 },
                    { 6, 6.5f, 8, 2, 1.8f, 3 },
                    { 8, 6.5f, 8, 2, 1.8f, 3 },
                    { 8, 6.0f, 9, 2, 2.0f, 3 },
                    { 10, 6.0f, 9, 3, 2.0f, 3 },
                    { 10, 5.0f, 10, 3, 2.0f, 4 },
                },
                constants = new[]
                {
                    Set(F.Interval, 0.1f), Set(F.Slow, 0.4f), Set(F.Width, 0.8f), Set(F.BurnTick, 0.5f), Set(F.SubDamage, 30),
                    Set(F.SubRadius, 1.6f), Set(F.SubInterval, 2.0f), Set(F.SubDuration, 2.0f),
                },
                notes = new[] { Note(1, "Thermal shock: 30 + 3 x level damage.") },
                evolution = new EvolutionSpec
                {
                    id = "aurora_lance", name = "Aurora Lance", passiveId = PassiveIds.TOME_OF_INSIGHT,
                    description = "6 beams turn in a double helix and never stop; thermal shock deals double damage.",
                    overrides = new[] { Set(F.Count, 6), Set(F.Duration, -1), Set(F.Damage, 13), Set(F.SubDamage, 60) },
                },
            }),
            Define(new WeaponSpec
            {
                id = "singularity", name = "Singularity", rarity = Rarity.Rare, owner = HeroClass.Mage,
                tags = WeaponTags.Area,
                create = (context, def, slot) => new Singularity(context, def, slot),
                description = "A tear in the world drags the crowd in, grinds it, then collapses in a stunning blast.",
                columns = new[] { Column(F.MajorDamage, "Blast"), COOLDOWN, Column(F.Radius, "Pull radius"), DURATION, Column(F.SubDamage, "Grind"), Column(F.Pull, "Pull") },
                rows = new float[,]
                {
                    { 50, 8.0f, 5.0f, 2.2f, 4, 7 },
                    { 64, 8.0f, 5.0f, 2.2f, 5, 7 },
                    { 64, 8.0f, 5.5f, 2.2f, 5, 8 },
                    { 82, 7.5f, 5.5f, 2.4f, 7, 8 },
                    { 82, 7.5f, 6.0f, 2.4f, 7, 9 },
                    { 104, 7.0f, 6.0f, 2.6f, 9, 9 },
                    { 104, 7.0f, 6.5f, 2.6f, 9, 10 },
                    { 136, 6.0f, 7.0f, 2.8f, 11, 11 },
                },
                constants = new[]
                {
                    Set(F.Range, 14), Set(F.Count, 1), Set(F.Interval, 0.25f), Set(F.MajorRadius, 4.0f), Set(F.Stun, 0.8f), Set(F.Knockback, 4.0f),
                },
                evolution = new EvolutionSpec
                {
                    id = "event_horizon", name = "Event Horizon", passiveId = PassiveIds.LODESTONE_CHARM,
                    description = "Two wider tears, and whatever they drag in takes +25% damage for 3 s.",
                    overrides = new[]
                    {
                        Set(F.Count, 2), Set(F.MajorDamage, 150), Set(F.MajorRadius, 5.0f), Set(F.Radius, 8.0f), Set(F.Pull, 13), Set(F.Bonus, 0.25f),
                        Set(F.SubDuration, 3.0f),
                    },
                },
            }),
            Define(new WeaponSpec
            {
                id = "firebird", name = "Firebird", rarity = Rarity.Uncommon, owner = HeroClass.Mage,
                tags = WeaponTags.Projectile | WeaponTags.Area,
                create = (context, def, slot) => new Firebird(context, def, slot),
                description = "A bird of fire sweeps over the field in a long wave, burning everything under its wings.",
                columns = new[] { DAMAGE, COOLDOWN, Column(F.Range, "Flight"), Column(F.Width, "Wingspan"), Column(F.Burn, "Burn/0.5 s") },
                rows = new float[,]
                {
                    { 26, 5.0f, 14, 2.4f, 4 },
                    { 34, 5.0f, 14, 2.4f, 4 },
                    { 34, 5.0f, 16, 2.4f, 5 },
                    { 44, 4.5f, 16, 2.8f, 5 },
                    { 44, 4.5f, 18, 2.8f, 6 },
                    { 56, 4.0f, 18, 3.2f, 6 },
                    { 56, 4.0f, 20, 3.2f, 8 },
                    { 72, 3.5f, 22, 3.6f, 8 },
                },
                constants = new[] { Set(F.Count, 1), Set(F.Speed, 20), Set(F.BurnTick, 0.5f), Set(F.SubDuration, 3.0f), Set(F.Angle, 40) },
                evolution = new EvolutionSpec
                {
                    id = "phoenix_ascendant", name = "Phoenix Ascendant", passiveId = PassiveIds.EVERBURN_WICK,
                    description = "Two birds that wheel at the end of their flight and sweep back to you, burning everything again.",
                    overrides = new[] { Set(F.Count, 2), Set(F.Damage, 90), Set(F.Burn, 10), Set(F.Width, 4.0f) },
                },
            }),
        };
    }
}

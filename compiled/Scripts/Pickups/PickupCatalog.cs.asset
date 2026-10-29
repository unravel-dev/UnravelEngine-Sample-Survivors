using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The looks of everything the hero picks up: faceted gems glowing in their tier colour, coins, food, the
    /// lodestone, the holy bell, the frost hourglass and chests. Gems pulse their glow once per second. The pickup
    /// world owns the material instances made here.
    /// </summary>
    public static class PickupCatalog
    {
        private const string GEM_MESH = "app:/data/Models/Pickups/gem.glb";
        private const string GEM_MATERIAL = "app:/data/Materials/Gem.mat";
        private const float GEM_GLOW = 2.4f;
        /// <summary>The gem glow rises this share at the top of each pulse.</summary>
        private const float GEM_PULSE = 0.3f;
        private const float GEM_SPIN = 90.0f;
        /// <summary>A gem hovers this high plus half its size, so bigger tiers float higher.</summary>
        private const float GEM_HOVER = 0.35f;
        private const float GEM_BOB = 0.08f;

        /// <summary>Gem tier colours: azure, verdant, amber, violet, crimson.</summary>
        private static readonly Color[] GEM_COLORS =
        {
            new Color(0.247f, 0.663f, 1.0f, 1.0f),
            new Color(0.224f, 0.878f, 0.42f, 1.0f),
            new Color(1.0f, 0.71f, 0.18f, 1.0f),
            new Color(0.69f, 0.36f, 1.0f, 1.0f),
            new Color(1.0f, 0.23f, 0.306f, 1.0f),
        };

        /// <summary>Gem heights (m) by tier; bigger tiers read at a glance.</summary>
        private static readonly float[] GEM_SIZES = { 0.38f, 0.48f, 0.58f, 0.72f, 0.95f };

        /// <summary>Gives every pickup kind its mesh and look; the world owns the material instances it creates.</summary>
        public static void Register(PickupWorld world)
        {
            Mesh gem = Assets.GetAsset<Mesh>(GEM_MESH);
            Material gemMaterial = Assets.GetAsset<Material>(GEM_MATERIAL);
            bool hasGemMaterial = AssetChecks.IsLoaded(gemMaterial);
            for (int tier = 0; tier < GEM_COLORS.Length; tier++)
            {
                world.SetVisual(PickupKind.GemSmall + tier, new PickupVisual
                {
                    mesh = gem,
                    material = gemMaterial,
                    materialInstance = hasGemMaterial ? new MaterialInstance(gemMaterial) { baseColor = GEM_COLORS[tier], emissiveColor = GEM_COLORS[tier], emissiveIntensity = GEM_GLOW } : null,
                    scale = GEM_SIZES[tier],
                    spin = GEM_SPIN,
                    height = GEM_HOVER + GEM_SIZES[tier] * 0.5f,
                    bob = GEM_BOB,
                    glow = GEM_GLOW,
                    glowPulse = GEM_PULSE,
                });
            }
            world.SetVisual(PickupKind.Gold, new PickupVisual
            {
                mesh = Assets.GetAsset<Mesh>("app:/data/Environment/dungeon/coin.obj"),
                scale = 1.7f,
                spin = 220.0f,
                height = 0.45f,
                bob = 0.08f,
                meshRotation = new Vector3(90.0f, 0.0f, 0.0f),
            });
            world.SetVisual(PickupKind.Food, new PickupVisual
            {
                mesh = Assets.GetAsset<Mesh>("app:/data/Environment/dungeon/plate_food_A.obj"),
                scale = 0.9f,
                spin = 35.0f,
                height = 0.15f,
                bob = 0.05f,
            });
            world.SetVisual(PickupKind.Magnet, new PickupVisual
            {
                mesh = Assets.GetAsset<Mesh>("engine:/embedded/dodecahedron"),
                material = gemMaterial,
                materialInstance = hasGemMaterial ? new MaterialInstance(gemMaterial) { baseColor = new Color(0.12f, 0.14f, 0.2f, 1.0f), emissiveColor = new Color(0.35f, 0.75f, 1.0f, 1.0f), emissiveIntensity = 1.6f } : null,
                scale = 0.7f,
                spin = 120.0f,
                height = 0.8f,
                bob = 0.15f,
            });
            world.SetVisual(PickupKind.Bomb, new PickupVisual
            {
                mesh = Assets.GetAsset<Mesh>("app:/data/Environment/Halloween/gltf/lantern_standing.gltf"),
                scale = 1.3f,
                spin = 60.0f,
                height = 0.25f,
                bob = 0.12f,
            });
            Material ice = Assets.GetAsset<Material>("app:/data/Models/Pickups/ice_cluster_a_[0] Ice.mat");
            world.SetVisual(PickupKind.Freeze, new PickupVisual
            {
                mesh = Assets.GetAsset<Mesh>("app:/data/Models/Pickups/ice_cluster_a.glb"),
                material = ice,
                materialInstance = AssetChecks.IsLoaded(ice) ? new MaterialInstance(ice) { emissiveColor = new Color(0.45f, 0.85f, 1.0f, 1.0f), emissiveIntensity = 1.2f } : null,
                scale = 1.1f,
                spin = 70.0f,
                height = 0.2f,
                bob = 0.1f,
            });
            world.SetVisual(PickupKind.Chest, new PickupVisual
            {
                mesh = Assets.GetAsset<Mesh>("app:/data/Models/Pickups/chest_gold.glb"),
                scale = 0.95f,
                spin = 0.0f,
                height = 0.0f,
                bob = 0.0f,
            });
        }
    }
}

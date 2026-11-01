using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The looks of everything the hero picks up: faceted gems glowing in their tier colour, coins, food, the
    /// lodestone (a horseshoe magnet), the holy bell, the frost shard and chests. Gems and the rare power-ups pulse their
    /// glow once per second. The power-ups stand out by kind: each has a colour for its beacon, and a marker. The pickup
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
        /// <summary>The rare power-ups glow hard and hover high, turning over slowly above the horde's feet.</summary>
        private const float POWER_UP_GLOW = 0.9f;
        private const float POWER_UP_PULSE = 0.8f;
        private const float POWER_UP_SPIN = 110.0f;
        private const float POWER_UP_HEIGHT = 0.7f;
        private const float POWER_UP_BOB = 0.16f;

        /// <summary>Gem tier colours: azure, verdant, amber, violet, crimson.</summary>
        private static readonly Color[] GEM_COLORS =
        {
            new Color(0.247f, 0.663f, 1.0f, 1.0f),
            new Color(0.224f, 0.878f, 0.42f, 1.0f),
            new Color(1.0f, 0.71f, 0.18f, 1.0f),
            new Color(0.69f, 0.36f, 1.0f, 1.0f),
            new Color(1.0f, 0.23f, 0.306f, 1.0f),
        };

        private static readonly Color FOOD_COLOR = new Color(1.0f, 0.36f, 0.42f, 1.0f);
        private static readonly Color MAGNET_COLOR = new Color(0.3f, 0.72f, 1.0f, 1.0f);
        private static readonly Color BELL_COLOR = new Color(1.0f, 0.82f, 0.32f, 1.0f);
        private static readonly Color FROST_COLOR = new Color(0.5f, 0.92f, 1.0f, 1.0f);

        /// <summary>Gem heights (m) by tier; bigger tiers read at a glance.</summary>
        private static readonly float[] GEM_SIZES = { 0.38f, 0.48f, 0.58f, 0.72f, 0.95f };

        /// <summary>Whether a kind gets a beacon and a marker: chests and the rare power-ups.</summary>
        public static bool HasBeacon(PickupKind kind) => kind >= PickupKind.Food && kind <= PickupKind.Chest;

        /// <summary>The colour of a power-up's beacon.</summary>
        public static Color GetColor(PickupKind kind) => kind switch
        {
            PickupKind.Food => FOOD_COLOR,
            PickupKind.Magnet => MAGNET_COLOR,
            PickupKind.Bomb => BELL_COLOR,
            _ => FROST_COLOR,
        };

        /// <summary>The marker of a chest or power-up.</summary>
        public static MarkerKind GetMarker(PickupKind kind) => kind switch
        {
            PickupKind.Food => MarkerKind.Food,
            PickupKind.Magnet => MarkerKind.Magnet,
            PickupKind.Bomb => MarkerKind.Bell,
            PickupKind.Freeze => MarkerKind.Frost,
            _ => MarkerKind.Chest,
        };

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
                scale = 1.5f,
                spin = 50.0f,
                height = 0.45f,
                bob = 0.1f,
            });
            world.SetVisual(PickupKind.Magnet, MakePowerUp("app:/data/Meshes/pickup_magnet.gltf", gemMaterial, hasGemMaterial, MAGNET_COLOR, 1.15f));
            world.SetVisual(PickupKind.Bomb, MakePowerUp("app:/data/Meshes/pickup_bell.gltf", gemMaterial, hasGemMaterial, BELL_COLOR, 1.25f));
            Material ice = Assets.GetAsset<Material>("app:/data/Models/Pickups/ice_cluster_a_[0] Ice.mat");
            world.SetVisual(PickupKind.Freeze, new PickupVisual
            {
                mesh = Assets.GetAsset<Mesh>("app:/data/Models/Pickups/ice_cluster_a.glb"),
                material = ice,
                materialInstance = AssetChecks.IsLoaded(ice) ? new MaterialInstance(ice) { emissiveColor = FROST_COLOR, emissiveIntensity = POWER_UP_GLOW } : null,
                scale = 1.7f,
                spin = 70.0f,
                height = 0.4f,
                bob = 0.12f,
                glow = POWER_UP_GLOW,
                glowPulse = POWER_UP_PULSE,
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

        /// <summary>A power-up drawn as one glowing mesh in its colour.</summary>
        private static PickupVisual MakePowerUp(string meshKey, Material material, bool hasMaterial, Color color, float scale)
        {
            return new PickupVisual
            {
                mesh = Assets.GetAsset<Mesh>(meshKey),
                material = material,
                materialInstance = hasMaterial ? new MaterialInstance(material) { baseColor = color, emissiveColor = color, emissiveIntensity = POWER_UP_GLOW } : null,
                scale = scale,
                spin = POWER_UP_SPIN,
                height = POWER_UP_HEIGHT,
                bob = POWER_UP_BOB,
                glow = POWER_UP_GLOW,
                glowPulse = POWER_UP_PULSE,
            };
        }
    }
}

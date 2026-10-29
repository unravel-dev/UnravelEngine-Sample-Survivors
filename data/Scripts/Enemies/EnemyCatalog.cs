using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The six regular enemies.</summary>
    public enum Archetype
    {
        Shambler,
        Runner,
        Archer,
        Shieldbearer,
        CinderSkull,
        HexCaster,
        Count,
    }

    /// <summary>Tint tiers by spawn minute: the health curve made visible.</summary>
    public enum Tint
    {
        Bone,
        Ash,
        Blood,
        Void,
        Count,
    }

    /// <summary>What the director needs to know about an archetype.</summary>
    public sealed class ArchetypeInfo
    {
        public Archetype archetype;
        public string displayName;
        /// <summary>Threat points one costs.</summary>
        public float cost;
        /// <summary>Most alive at once (0 = no cap).</summary>
        public int cap;
    }

    /// <summary>
    /// Every enemy type of the game, registered once per run start: each archetype in four tint tiers (24 types sharing
    /// six bakes), the Grave Lantern, the bone pyre and the boss proxies (rows with no body, driven by their owners).
    /// </summary>
    public static class EnemyCatalog
    {
        private const string SKELETONS = "app:/data/Characters/Skeletons";
        private const string MATERIALS = "app:/data/Materials/Enemies";
        private const string LANTERN_PREFAB = "app:/data/Prefabs/Props/GraveLantern.pfb";
        private const string BODY_GLOW_MAP = "app:/data/Textures/white.png";
        /// <summary>Each tint tier is this much bigger than the last: the crowd reads older and tougher as the run goes on.</summary>
        private const float TINT_SCALE_STEP = 0.05f;
        /// <summary>Proxies do not budge when pushed.</summary>
        private const float PROXY_WEIGHT = 1000.0f;
        /// <summary>What every proxy shrugs off: its owner drives the body, so only damage, slows and stuns reach it.</summary>
        private const StatusImmunity PROXY_IMMUNITIES = StatusImmunity.Freeze | StatusImmunity.Ward | StatusImmunity.Confuse | StatusImmunity.Launch;

        private static readonly int[,] types = new int[(int)Archetype.Count, (int)Tint.Count];
        private static readonly ArchetypeInfo[] infos = new ArchetypeInfo[(int)Archetype.Count];
        private static readonly string[] TINT_NAMES = { "Bone", "Ash", "Blood", "Void" };

        /// <summary>The Grave Lantern's proxy row type (1 HP, never moves).</summary>
        public static int lanternType = -1;
        public static Prefab lanternPrefab;
        /// <summary>A bone pyre's proxy row type (map events): never moves, health set at spawn.</summary>
        public static int pyreType = -1;
        /// <summary>Proxy types of the bosses by <see cref="BossKind"/> (-1 until registered); each boss sets its health.</summary>
        public static readonly int[] bossTypes = CreateUnregistered((int)BossKind.Count);

        public static int Get(Archetype archetype, Tint tint) => types[(int)archetype, (int)tint];

        public static ArchetypeInfo Info(Archetype archetype) => infos[(int)archetype];

        public static void Register(EnemyWorld world)
        {
            for (int a = 0; a < (int)Archetype.Count; a++)
            {
                var archetype = (Archetype)a;
                for (int t = 0; t < (int)Tint.Count; t++)
                {
                    types[a, t] = world.Register(Create(archetype, (Tint)t));
                }
                infos[a] = CreateInfo(archetype, world.registeredTypes[types[a, 0]].displayName);
            }
            lanternType = world.Register(MakeProxy("GraveLantern", "Grave Lantern", 0.5f));
            lanternPrefab = Assets.GetAsset<Prefab>(LANTERN_PREFAB);
            pyreType = world.Register(MakeProxy("BonePyre", "Bone Pyre", 0.6f));
            bossTypes[(int)BossKind.Gravewarden] = world.Register(MakeProxy("Gravewarden", "Gravewarden", 1.1f));
            bossTypes[(int)BossKind.HollowArchmage] = world.Register(MakeProxy("HollowArchmage", "Hollow Archmage", 1.0f));
            bossTypes[(int)BossKind.HollowKing] = world.Register(MakeProxy("HollowKing", "The Hollow King", 2.0f));
        }

        /// <summary>A row type with no body of its own: 1 HP unless its owner sets the health at spawn, never pushed.</summary>
        private static EnemyType MakeProxy(string name, string displayName, float radius)
        {
            return new EnemyType
            {
                name = name,
                displayName = displayName,
                isProxy = true,
                immunities = PROXY_IMMUNITIES,
                health = 1.0f,
                radius = radius,
                weight = PROXY_WEIGHT,
                eliteHealth = 1.0f,
                eliteScale = 1.0f,
                experience = 0.0f,
            };
        }

        private static int[] CreateUnregistered(int count)
        {
            var array = new int[count];
            Array.Fill(array, -1);
            return array;
        }

        private static EnemyType Create(Archetype archetype, Tint tint)
        {
            EnemyType type = archetype switch
            {
                Archetype.Shambler => Shambler(),
                Archetype.Runner => Runner(),
                Archetype.Archer => Archer(),
                Archetype.Shieldbearer => Shieldbearer(),
                Archetype.CinderSkull => CinderSkull(),
                // Raise calls Shamblers of the caster's own tier (registered first: they come first in Archetype).
                Archetype.HexCaster => HexCaster(Get(Archetype.Shambler, tint)),
                _ => throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Not an enemy archetype."),
            };
            type.name = $"{archetype}_{TINT_NAMES[(int)tint]}";
            type.scale *= 1.0f + TINT_SCALE_STEP * (int)tint;
            string materialKey = $"{MATERIALS}/{GetMaterialFamily(archetype)}_{TINT_NAMES[(int)tint]}.mat";
            type.material = Assets.GetAsset<Material>(materialKey);
            if (!AssetChecks.IsLoaded(type.material))
            {
                Log.Warning($"EnemyCatalog: {type.name} has no material {materialKey}; the mesh's own is used.");
            }
            return type;
        }

        private static ArchetypeInfo CreateInfo(Archetype archetype, string displayName)
        {
            return new ArchetypeInfo { archetype = archetype, displayName = displayName, cost = GetCost(archetype), cap = GetCap(archetype) };
        }

        /// <summary>The material set an archetype wears in each tint: burning skulls, ghosts, or bone.</summary>
        private static string GetMaterialFamily(Archetype archetype) => archetype switch
        {
            Archetype.CinderSkull => "Cinder",
            Archetype.Runner => "Ghost",
            Archetype.Shambler => "Skeleton",
            Archetype.Archer => "Skeleton",
            Archetype.Shieldbearer => "Skeleton",
            Archetype.HexCaster => "Skeleton",
            _ => throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Not an enemy archetype."),
        };

        private static float GetCost(Archetype archetype) => archetype switch
        {
            Archetype.Shambler => 1.0f,
            Archetype.Runner => 1.0f,
            Archetype.Archer => 2.0f,
            Archetype.Shieldbearer => 3.0f,
            Archetype.CinderSkull => 2.0f,
            Archetype.HexCaster => 5.0f,
            _ => throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Not an enemy archetype."),
        };

        /// <summary>Most of an archetype alive at once; 0 = no cap.</summary>
        private static int GetCap(Archetype archetype) => archetype switch
        {
            Archetype.Shambler => 0,
            Archetype.Runner => 0,
            Archetype.Archer => 120,
            Archetype.Shieldbearer => 0,
            Archetype.CinderSkull => 150,
            Archetype.HexCaster => 40,
            _ => throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Not an enemy archetype."),
        };

        // ---------------------------------------------------------------------------------------------------------
        // Archetypes
        // ---------------------------------------------------------------------------------------------------------

        private static EnemyType Shambler()
        {
            EnemyType type = Body("Skeleton_Minion");
            type.displayName = "Shambler";
            type.health = 10.0f;
            type.speed = 2.6f;
            type.radius = 0.35f;
            type.experience = 1.0f;
            type.abilities = new[]
            {
                new EnemyAbility { effect = EnemyEffect.Melee, clip = EnemyClip.Attack, range = 1.0f, radius = 1.2f, spread = 90.0f,
                                   hitTime = 0.35f, duration = 0.8f, cooldown = 1.2f, damage = 6.0f },
            };
            return type;
        }

        private static EnemyType Runner()
        {
            EnemyType type = Body("Skeleton_Minion");
            type.displayName = "Grave Runner";
            type.scale = 0.85f;
            type.health = 6.0f;
            type.speed = 4.4f;
            type.acceleration = 20.0f;
            type.radius = 0.3f;
            type.experience = 1.0f;
            type.clips[(int)EnemyClip.Walk] = type.clips[(int)EnemyClip.Run];
            type.abilities = new[]
            {
                new EnemyAbility { effect = EnemyEffect.Charge, clip = EnemyClip.AttackHeavy, range = 2.6f, radius = 0.8f,
                                   hitTime = 0.25f, chargeTime = 0.15f, speed = 13.0f, duration = 1.0f, cooldown = 1.5f, damage = 5.0f },
            };
            return type;
        }

        private static EnemyType Archer()
        {
            EnemyType type = Body("Skeleton_Rogue");
            type.displayName = "Bone Archer";
            type.health = 14.0f;
            type.speed = 2.4f;
            type.radius = 0.35f;
            type.experience = 2.0f;
            type.movement = EnemyMovement.KeepRange;
            type.holdMin = 8.0f;
            type.holdMax = 13.0f;
            type.abilities = new[]
            {
                new EnemyAbility { effect = EnemyEffect.Projectile, clip = EnemyClip.Shoot, minRange = 6.0f, range = 13.0f,
                                   hitTime = 0.8f, duration = 3.0f, cooldown = 3.0f, damage = 10.0f, speed = 14.0f, count = 1,
                                   visual = "EnemyBolt", castSound = "bolt_draw" },
            };
            return type;
        }

        private static EnemyType Shieldbearer()
        {
            EnemyType type = Body("Skeleton_Warrior");
            type.displayName = "Shieldbearer";
            type.health = 45.0f;
            type.speed = 2.0f;
            type.radius = 0.5f;
            type.weight = 3.3f;
            type.experience = 4.0f;
            type.abilities = new[]
            {
                new EnemyAbility { effect = EnemyEffect.Melee, clip = EnemyClip.Attack, clipSpeed = 0.85f, range = 1.4f, radius = 1.6f, spread = 120.0f,
                                   hitTime = 0.6f, duration = 1.3f, cooldown = 2.0f, damage = 12.0f, telegraph = true },
            };
            return type;
        }

        private static EnemyType CinderSkull()
        {
            EnemyType type = Body("Skeleton_Minion");
            type.displayName = "Cinder Skull";
            type.health = 12.0f;
            type.speed = 3.6f;
            type.radius = 0.35f;
            type.experience = 2.0f;
            type.detonatesOnDeath = true;
            type.eliteGlow = new Color(1.0f, 0.5f, 0.1f, 1.0f);
            type.abilities = new[]
            {
                new EnemyAbility { effect = EnemyEffect.Explode, clip = EnemyClip.Idle, range = 1.8f, radius = 2.5f,
                                   hitTime = 0.8f, duration = 1.0f, cooldown = 99.0f, cooldownJitter = 0.0f, damage = 25.0f, facesTarget = false },
            };
            return type;
        }

        /// <param name="raisedType">The type its Raise calls up.</param>
        private static EnemyType HexCaster(int raisedType)
        {
            EnemyType type = Body("Skeleton_Mage");
            type.displayName = "Hex Caster";
            type.health = 30.0f;
            type.speed = 2.0f;
            type.radius = 0.4f;
            type.weight = 1.25f;
            type.experience = 6.0f;
            type.movement = EnemyMovement.KeepRange;
            type.holdMin = 7.0f;
            type.holdMax = 11.0f;
            type.abilities = new[]
            {
                new EnemyAbility { effect = EnemyEffect.GroundBurst, clip = EnemyClip.Cast, minRange = 5.0f, range = 11.0f,
                                   radius = 2.0f, hitTime = 2.1f, duration = 2.9f, cooldown = 6.0f, damage = 18.0f, castSound = "hex_cast" },
                new EnemyAbility { effect = EnemyEffect.Summon, clip = EnemyClip.Cast, minRange = 0.0f, range = 20.0f,
                                   radius = 1.5f, hitTime = 1.5f, duration = 2.0f, cooldown = 12.0f, count = 4, maxAllies = 5, allyRadius = 6.0f,
                                   summonType = raisedType, summonLimit = 2500, castSound = "raise_chant" },
                new EnemyAbility { effect = EnemyEffect.Ward, clip = EnemyClip.Cast, minRange = 0.0f, range = 20.0f,
                                   radius = 6.0f, hitTime = 0.8f, duration = 1.3f, cooldown = 12.0f, chargeTime = 5.0f, minAllies = 10,
                                   allyRadius = 6.0f },
            };
            return type;
        }

        /// <summary>A skeleton body: its merged mesh and its clips by role.</summary>
        private static EnemyType Body(string model)
        {
            var type = new EnemyType
            {
                mesh = Assets.GetAsset<Mesh>($"{SKELETONS}/{model}/{model}.gltf"),
                bodyGlowMap = Assets.GetAsset<Texture>(BODY_GLOW_MAP),
                walkClipPerSpeed = 0.0f,
                eliteGlow = new Color(1.0f, 0.78f, 0.25f, 1.0f),
                eliteScale = 1.6f,
                eliteHealth = 15.0f,
            };
            type.clips[(int)EnemyClip.Walk] = Clip(model, ClipNames.WALK);
            type.clips[(int)EnemyClip.Run] = Clip(model, ClipNames.RUN);
            type.clips[(int)EnemyClip.Idle] = Clip(model, ClipNames.IDLE);
            type.clips[(int)EnemyClip.Attack] = Clip(model, ClipNames.ATTACK);
            type.clips[(int)EnemyClip.AttackHeavy] = Clip(model, ClipNames.ATTACK_HEAVY);
            type.clips[(int)EnemyClip.Cast] = Clip(model, ClipNames.CAST);
            type.clips[(int)EnemyClip.Shoot] = Clip(model, ClipNames.SHOOT);
            type.clips[(int)EnemyClip.Hit] = Clip(model, ClipNames.HIT);
            type.clips[(int)EnemyClip.Death] = Clip(model, ClipNames.DEATH);
            type.clips[(int)EnemyClip.DeathAlt] = Clip(model, ClipNames.DEATH_ALT);
            type.clips[(int)EnemyClip.Rise] = Clip(model, ClipNames.RISE);
            return type;
        }

        private static AnimationClip Clip(string model, string clip)
        {
            if (string.IsNullOrEmpty(clip))
            {
                return null;
            }
            return Assets.GetAsset<AnimationClip>($"{SKELETONS}/{model}/{model}_{clip}.anim");
        }

        /// <summary>Clip names inside the skeleton models.</summary>
        private static class ClipNames
        {
            public const string WALK = "Walking_D_Skeletons";
            public const string RUN = "Running_C";
            public const string IDLE = "Idle_Combat";
            public const string ATTACK = "1H_Melee_Attack_Chop";
            public const string ATTACK_HEAVY = "1H_Melee_Attack_Stab";
            public const string CAST = "Spellcast_Shoot";
            public const string SHOOT = "1H_Ranged_Shoot";
            public const string HIT = "Hit_A";
            public const string DEATH = "Death_C_Skeletons";
            public const string DEATH_ALT = "Death_A";
            public const string RISE = "Skeletons_Awaken_Floor";
        }
    }
}

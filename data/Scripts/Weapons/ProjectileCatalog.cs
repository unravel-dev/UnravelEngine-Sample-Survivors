using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Every projectile look of the game, hero and enemy, registered by name. Props use their own KayKit material;
    /// spells use the shared SpellGlow material tinted per look by a MaterialInstance. A look whose mesh is missing
    /// still registers (it just draws nothing), so gameplay never depends on art being present. Particle heads come with
    /// the look, or from the class head tables (KnightLooks, MageLooks, RogueLooks, SharedLooks) by look name.
    /// Enemy looks the enemies use: "EnemyBolt" (archer bolt), "HexOrb" (caster and boss orbs); both draw a white-hot core,
    /// a red flame, a wide halo and a danger ring on the ground under the shot.
    /// </summary>
    public static class ProjectileCatalog
    {
        public const string SPELL_GLOW_MATERIAL = "app:/data/Materials/SpellGlow.mat";
        private const string PROPS = "app:/data/Props/Adventurers/gltf/";
        private const string PRIMITIVES = "engine:/embedded/";
        /// <summary>Meshes generated for this game.</summary>
        private const string GENERATED = "app:/data/Meshes/";
        private const float DEFAULT_TRAIL_INTERVAL = 0.03f;

        /// <summary>One look: mesh, optional glow tint, size, orientation, trail and particle heads.</summary>
        private sealed class Look
        {
            public string name;
            public string mesh;
            /// <summary>Drawn when <see cref="mesh"/> is missing; null for none.</summary>
            public string fallbackMesh;
            /// <summary>Alpha 0 (the default) keeps the mesh's own material; otherwise the SpellGlow tint.</summary>
            public Color glow;
            public float intensity;
            public float scale = 1.0f;
            /// <summary>Degrees per second about the flight axis.</summary>
            public float spin;
            /// <summary>Euler degrees that turn the mesh into its flight pose.</summary>
            public Vector3 rotation;
            public float height = 1.0f;
            /// <summary>Particle effect shed along the flight, or null.</summary>
            public string trail;
            public float trailInterval = DEFAULT_TRAIL_INTERVAL;
            /// <summary>Particle heads drawn every frame (Combat/ProjectileWorld.cs): a core, flames or sparkles, a halo.</summary>
            public ProjectileHead[] heads = Array.Empty<ProjectileHead>();
            /// <summary>The heads alone draw the projectile.</summary>
            public bool hideMesh;
        }

        private static readonly Color GOLD = new Color(1.0f, 0.78f, 0.32f, 1.0f);
        private static readonly Color PALE_GOLD = new Color(1.0f, 0.9f, 0.62f, 1.0f);
        private static readonly Color FIRE = new Color(1.0f, 0.45f, 0.1f, 1.0f);
        private static readonly Color SUN = new Color(1.0f, 0.82f, 0.45f, 1.0f);
        private static readonly Color VIOLET = new Color(0.6f, 0.35f, 1.0f, 1.0f);
        private static readonly Color SHADOW = new Color(0.45f, 0.2f, 0.7f, 1.0f);
        private static readonly Color PALE_BLUE = new Color(0.62f, 0.85f, 1.0f, 1.0f);
        private static readonly Color CYAN = new Color(0.45f, 0.95f, 1.0f, 1.0f);
        private static readonly Color THORN = new Color(0.35f, 0.62f, 0.22f, 1.0f);
        private static readonly Color BRIAR = new Color(0.62f, 0.85f, 0.3f, 1.0f);
        private static readonly Color WISP = new Color(0.55f, 1.0f, 0.85f, 1.0f);
        private static readonly Color CHOIR = new Color(1.0f, 0.95f, 0.75f, 1.0f);
        private static readonly Color ENEMY_ORB = new Color(1.0f, 0.28f, 0.08f, 1.0f);
        private static readonly Color VENOM = new Color(0.45f, 1.0f, 0.42f, 1.0f);
        private static readonly Color WHITE_HOT = new Color(1.0f, 0.93f, 0.8f, 1.0f);
        private static readonly Color HEX_RED = new Color(1.0f, 0.22f, 0.07f, 1.0f);
        /// <summary>The Dawn Hammer's glowing head and haft, and Skybreaker's paler gold.</summary>
        private static readonly Color HAMMER_GOLD = new Color(1.0f, 0.66f, 0.2f, 1.0f);
        private static readonly Color HAFT_GOLD = new Color(0.95f, 0.62f, 0.26f, 1.0f);
        private static readonly Color SKY_HAMMER_GOLD = new Color(1.0f, 0.82f, 0.5f, 1.0f);
        private static readonly Vector3 LAID_FLAT = new Vector3(90.0f, 0.0f, 0.0f);
        private static readonly Vector3 STANDING = new Vector3(-90.0f, 0.0f, 0.0f);
        /// <summary>Lays the cylinder's axis across the hammer's haft, which runs along the look's -Z.</summary>
        private static readonly Vector3 ACROSS_HAFT = new Vector3(0.0f, 0.0f, 90.0f);
        /// <summary>Lays the wand's long end back along -Z, its short tip forward through the hammer's head.</summary>
        private static readonly Vector3 LAID_BACK = new Vector3(-90.0f, 0.0f, 0.0f);

        internal static ProjectileHead Core(float size) => new ProjectileHead("HeadCore", size, WHITE_HOT, false);
        internal static ProjectileHead Core(float size, Color color) => new ProjectileHead("HeadCore", size, color, false);
        internal static ProjectileHead Flame(float size, Color color) => new ProjectileHead("HeadFlame", size, color);
        internal static ProjectileHead Halo(float size, Color color) => new ProjectileHead("HeadHalo", size, color);
        /// <summary>The danger ring every enemy shot draws on the ground under itself.</summary>
        private static ProjectileHead ShotMark(float size) => new ProjectileHead("EnemyShotMark", size, Tuning.DANGER_COLOR, false, true);
        internal static ProjectileHead Corona(float size, Color color) => new ProjectileHead("HeadCorona", size, color);
        internal static ProjectileHead Star(float size, Color color) => new ProjectileHead("HeadStar", size, color);

        private static readonly Look[] looks =
        {
            // Hero, Knight.
            new Look { name = "OathCrescent", mesh = GENERATED + "crescent.gltf", fallbackMesh = PRIMITIVES + "torus", glow = GOLD, intensity = 4.0f, scale = 2.0f },
            new Look
            {
                name = "AegisShield", mesh = PROPS + "shield_badge.gltf", fallbackMesh = PRIMITIVES + "cylinder", scale = 1.1f,
                rotation = new Vector3(0.0f, 90.0f, 0.0f), height = 1.1f,
            },
            new Look { name = "AegisReflect", mesh = PRIMITIVES + "icosphere2", glow = GOLD, intensity = 5.0f, scale = 0.3f, trail = "ReflectTrail" },
            new Look
            {
                name = "VerdictSword", mesh = PROPS + "sword_2handed.gltf", fallbackMesh = PRIMITIVES + "cube", glow = PALE_GOLD, intensity = 3.0f, scale = 3.0f,
                rotation = LAID_FLAT, height = 0.0f,
            },
            new Look
            {
                name = "DawnLance", mesh = PROPS + "sword_2handed.gltf", fallbackMesh = PRIMITIVES + "cube", glow = PALE_GOLD, intensity = 1.8f, scale = 1.9f,
                rotation = LAID_FLAT, height = 1.4f, trail = "KnightSteelSparks", trailInterval = 0.02f,
                heads = new[] { Core(0.7f, PALE_GOLD), Halo(2.6f, GOLD) },
            },
            new Look
            {
                name = "PhalanxShield", mesh = PROPS + "shield_square.gltf", fallbackMesh = PRIMITIVES + "cube", glow = HAMMER_GOLD, intensity = 0.55f, scale = 1.9f,
                height = 0.0f,
            },
            new Look { name = "Boulder", mesh = GENERATED + "boulder.gltf", fallbackMesh = PRIMITIVES + "dodecahedron", scale = 0.85f, spin = 170.0f, height = 0.0f },
            new Look { name = "BoulderShard", mesh = GENERATED + "boulder.gltf", fallbackMesh = PRIMITIVES + "dodecahedron", scale = 0.3f, spin = 500.0f, height = 0.8f },
            // The Dawn Hammer is a head (a cylinder laid across the haft) and a haft (a wand pivoted at its grip) spinning together.
            new Look
            {
                name = "KnightDawnHammer", mesh = PRIMITIVES + "cylinder", glow = HAMMER_GOLD, intensity = 1.6f, scale = 0.42f,
                spin = KnightLooks.HAMMER_SPIN, rotation = ACROSS_HAFT,
            },
            new Look
            {
                name = "KnightSkyHammer", mesh = PRIMITIVES + "cylinder", glow = SKY_HAMMER_GOLD, intensity = 2.2f, scale = 0.48f,
                spin = KnightLooks.HAMMER_SPIN, rotation = ACROSS_HAFT,
            },
            new Look
            {
                name = "KnightDawnHaft", mesh = PROPS + "wand.gltf", glow = HAFT_GOLD, intensity = 1.1f, scale = 1.25f,
                spin = KnightLooks.HAMMER_SPIN, rotation = LAID_BACK,
            },
            new Look
            {
                name = "KnightSkyHaft", mesh = PROPS + "wand.gltf", glow = SKY_HAMMER_GOLD, intensity = 1.1f, scale = 1.4f,
                spin = KnightLooks.HAMMER_SPIN, rotation = LAID_BACK,
            },
            // Hero, Barbarian: no projectiles (melee and ground effects).
            // Hero, Mage.
            new Look
            {
                name = "EmberOrb", mesh = PRIMITIVES + "icosphere2", glow = FIRE, intensity = 6.0f, scale = 0.45f, height = 1.2f, trail = "EmberOrbTrail",
                trailInterval = 0.05f,
            },
            new Look
            {
                name = "SunfireOrb", mesh = PRIMITIVES + "icosphere2", glow = SUN, intensity = 8.0f, scale = 0.6f, height = 1.2f, trail = "SunfireOrbTrail",
                trailInterval = 0.04f,
            },
            new Look
            {
                name = "Ember", mesh = PRIMITIVES + "icosphere1", glow = FIRE, intensity = 6.0f, scale = 0.2f, height = 0.5f, trail = "EmberSparks",
                trailInterval = 0.05f,
            },
            new Look
            {
                name = "Meteor", mesh = PRIMITIVES + "dodecahedron", glow = FIRE, intensity = 3.0f, scale = 0.9f, height = 0.0f, trail = "MeteorTrail",
            },
            new Look
            {
                name = "GreatMeteor", mesh = PRIMITIVES + "dodecahedron", glow = FIRE, intensity = 4.0f, scale = 2.4f, height = 0.0f, trail = "MeteorTrail",
                trailInterval = 0.02f,
            },
            // Hero, Rogue.
            new Look { name = "CrossbowBolt", mesh = PROPS + "arrow.gltf", height = 1.2f },
            new Look { name = "WidowBolt", mesh = PROPS + "arrow.gltf", glow = VENOM, intensity = 3.0f, scale = 1.15f, height = 1.2f },
            new Look { name = "ThrownKnife", mesh = PROPS + "dagger.gltf", rotation = LAID_FLAT },
            new Look
            {
                name = "ShadowDagger", mesh = PROPS + "dagger.gltf", glow = SHADOW, intensity = 3.0f, rotation = LAID_FLAT, trail = "ShadowDaggerTrail",
                trailInterval = 0.04f,
            },
            // Hero, shared.
            new Look { name = "SpiritDart", mesh = PRIMITIVES + "icosphere1", glow = PALE_BLUE, intensity = 5.0f, scale = 0.25f, height = 1.2f },
            new Look
            {
                name = "RicochetDisc", mesh = PROPS + "shield_round.gltf", fallbackMesh = PRIMITIVES + "cylinder", glow = CYAN, intensity = 2.5f, scale = 0.75f,
                spin = 1080.0f, rotation = LAID_FLAT, trail = "DiscTrail", trailInterval = 0.04f,
            },
            new Look
            {
                name = "EclipseDisc", mesh = PROPS + "shield_round.gltf", fallbackMesh = PRIMITIVES + "cylinder", glow = VIOLET, intensity = 3.5f, scale = 0.95f,
                spin = 1440.0f, rotation = LAID_FLAT, trail = "EclipseTrail",
            },
            new Look
            {
                name = "BrambleRing", mesh = GENERATED + "thorn_ring.gltf", fallbackMesh = PRIMITIVES + "torus", glow = THORN, intensity = 0.8f, spin = 15.0f,
                height = 0.05f,
            },
            new Look
            {
                name = "BriarRing", mesh = GENERATED + "thorn_ring.gltf", fallbackMesh = PRIMITIVES + "torus", glow = BRIAR, intensity = 1.6f, spin = -20.0f,
                height = 0.05f,
            },
            new Look { name = "SkewerJavelin", mesh = PROPS + "arrow.gltf", scale = 2.4f, height = 1.2f, trail = "JavelinTrail", trailInterval = 0.05f },
            new Look { name = "Godspear", mesh = PROPS + "arrow.gltf", glow = GOLD, intensity = 4.0f, scale = 3.0f, height = 1.2f, trail = "GodspearTrail" },
            new Look
            {
                name = "FireFlask", mesh = PROPS + "smokebomb.gltf", fallbackMesh = PRIMITIVES + "icosphere1", scale = 1.2f, spin = 720.0f, height = 1.2f,
                trail = "FlaskSmoke", trailInterval = 0.06f,
            },
            new Look
            {
                name = "HellfireCask", mesh = PROPS + "smokebomb.gltf", fallbackMesh = PRIMITIVES + "icosphere1", scale = 1.8f, spin = 540.0f, height = 1.2f,
                trail = "FlaskSmoke", trailInterval = 0.04f,
            },
            new Look
            {
                name = "StormTotem", mesh = PROPS + "staff.gltf", fallbackMesh = PRIMITIVES + "capsule_1m", scale = 1.5f, rotation = STANDING, height = 0.0f,
            },
            new Look
            {
                name = "Thunderspire", mesh = PROPS + "staff.gltf", fallbackMesh = PRIMITIVES + "capsule_1m", glow = CYAN, intensity = 3.0f, scale = 1.3f,
                spin = 90.0f, rotation = STANDING, height = 2.4f, trail = "SpireSparks", trailInterval = 0.08f,
            },
            new Look { name = "Wisp", mesh = PRIMITIVES + "icosphere1", glow = WISP, intensity = 6.0f, scale = 0.32f, height = 1.8f },
            new Look { name = "ChoirWisp", mesh = PRIMITIVES + "icosphere1", glow = CHOIR, intensity = 7.0f, scale = 0.36f, height = 1.8f },
            // Enemy.
            // Enemy: one hot look whatever flies, so a shot reads as danger over the horde and over the hero's own spells.
            new Look
            {
                name = "EnemyBolt", mesh = PROPS + "arrow.gltf", scale = 1.7f, height = 1.2f, trail = "EnemyBoltTrail", trailInterval = 0.03f,
                heads = new[] { Core(0.7f), Flame(1.1f, HEX_RED), Halo(1.8f, HEX_RED), ShotMark(2.0f) },
            },
            new Look
            {
                name = "HexOrb", mesh = PRIMITIVES + "icosphere2", glow = ENEMY_ORB, intensity = 5.0f, scale = 0.55f, height = 1.2f, trail = "HexOrbTrail",
                trailInterval = 0.04f, hideMesh = true, heads = new[] { Core(0.8f), Flame(1.4f, HEX_RED), Halo(2.4f, HEX_RED), ShotMark(3.6f) },
            },
        };

        /// <summary>Registers every look with the world, with the class tables' particle heads.</summary>
        public static void Register(ProjectileWorld world)
        {
            Material glow = LoadMaterial(SPELL_GLOW_MATERIAL);
            var classHeads = new[] { KnightLooks.heads, MageLooks.heads, RogueLooks.heads, SharedLooks.heads };
            WarnUnmatchedHeads(classHeads);
            foreach (Look look in looks)
            {
                ProjectileVisual visual = Build(look, glow);
                ApplyClassHeads(visual, classHeads);
                world.RegisterVisual(visual);
            }
        }

        private static ProjectileVisual Build(Look look, Material glow)
        {
            var visual = new ProjectileVisual
            {
                name = look.name,
                mesh = LoadMesh(look.mesh) ?? LoadMesh(look.fallbackMesh),
                scale = look.scale,
                spin = look.spin,
                meshRotation = look.rotation,
                height = look.height,
                trail = look.trail,
                trailInterval = look.trailInterval,
                castShadows = false,
                heads = look.heads,
                hideMesh = look.hideMesh,
            };
            bool isGlowing = look.glow.a > 0.0f && glow != null;
            if (isGlowing)
            {
                var instance = new MaterialInstance(glow);
                instance.baseColor = look.glow;
                instance.emissiveColor = look.glow;
                instance.emissiveIntensity = look.intensity;
                visual.material = glow;
                visual.materialInstance = instance;
            }
            return visual;
        }

        /// <summary>A class table's heads for this look replace the look's own (the last table that names it wins).</summary>
        private static void ApplyClassHeads(ProjectileVisual visual, (string look, bool particleOnly, ProjectileHead[] heads)[][] tables)
        {
            foreach (var table in tables)
            {
                foreach ((string name, bool particleOnly, ProjectileHead[] layers) in table)
                {
                    if (name == visual.name)
                    {
                        visual.heads = layers;
                        visual.hideMesh = particleOnly;
                    }
                }
            }
        }

        /// <summary>Logs class head entries that name no look: a renamed look would otherwise lose its heads silently.</summary>
        private static void WarnUnmatchedHeads((string look, bool particleOnly, ProjectileHead[] heads)[][] tables)
        {
            foreach (var table in tables)
            {
                foreach ((string name, bool _, ProjectileHead[] _) in table)
                {
                    if (FindLook(name) == null)
                    {
                        Log.Warning($"ProjectileCatalog: particle heads name {name}, which is no projectile look.");
                    }
                }
            }
        }

        private static Look FindLook(string name)
        {
            foreach (Look look in looks)
            {
                if (look.name == name)
                {
                    return look;
                }
            }
            return null;
        }

        private static Mesh LoadMesh(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            Mesh mesh = Assets.GetAsset<Mesh>(key);
            return mesh != null && mesh.uid != Guid.Empty ? mesh : null;
        }

        private static Material LoadMaterial(string key)
        {
            Material material = Assets.GetAsset<Material>(key);
            if (material == null || material.uid == Guid.Empty)
            {
                Log.Warning($"ProjectileCatalog: {key} is missing; spell projectiles draw with their mesh's own material.");
                return null;
            }
            return material;
        }
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// One layer of a layered effect: an emitter, how many particles, and the particle size as a multiple of the size
    /// unit the effect is played with (0 keeps the emitter's own sizes). Tinted layers multiply their gradient by the
    /// caller's tint; the others keep their own colours.
    /// </summary>
    public readonly struct FxLayer
    {
        public readonly string effect;
        public readonly int count;
        public readonly float size;
        public readonly bool isTinted;
        /// <summary>Drawn on the ground under the effect's point (decals, shockwaves), not at its height.</summary>
        public readonly bool onGround;

        public FxLayer(string effect, int count, float size = 0.0f, bool isTinted = true, bool onGround = false)
        {
            this.effect = effect;
            this.count = count;
            this.size = size;
            this.isTinted = isTinted;
            this.onGround = onGround;
        }

        /// <summary>A ground layer (decal or shockwave) sized by the size unit.</summary>
        public static FxLayer Ground(string effect, float size, bool isTinted = true) => new FxLayer(effect, 1, size, isTinted, true);
    }

    /// <summary>
    /// A layered effect fired as one (flash + shockwave + sparks + debris + smoke + scorch). The size unit (usually the
    /// hit radius) sizes the layers; the intensity multiplies the particle counts, so a higher rank or an evolution
    /// reads denser without new emitters. Main thread.
    /// </summary>
    public sealed class FxComposite
    {
        /// <summary>Ground layers sit just above the ground so they never fight it in depth.</summary>
        private const float GROUND_HEIGHT = 0.05f;

        private readonly Effects effects;
        private readonly FxLayer[] layers;
        private readonly int[] ids;

        public FxComposite(Effects effects, params FxLayer[] layers)
        {
            this.effects = effects;
            this.layers = layers;
            ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                ids[i] = effects.Find(layers[i].effect);
            }
        }

        /// <summary>Fires every layer at a position: sizes are layer.size x <paramref name="sizeUnit"/> metres, counts
        /// layer.count x <paramref name="intensity"/>.</summary>
        public void Play(Vector3 position, float sizeUnit, float intensity, Color tint)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                FxLayer layer = layers[i];
                int count = Math.Max(1, (int)MathF.Round(layer.count * MathF.Max(intensity, 0.25f)));
                Color color = layer.isTinted ? tint : Color.white;
                Vector3 at = layer.onGround ? new Vector3(position.x, GROUND_HEIGHT, position.z) : position;
                if (layer.size > 0.0f)
                {
                    effects.EmitSized(ids[i], at, count, color, layer.size * sizeUnit);
                }
                else
                {
                    effects.Emit(ids[i], at, count, color);
                }
            }
        }
    }

    /// <summary>
    /// The run's particle effects: world-space emitters placed once in the scene under one root, found by entity
    /// name, and fired at positions with Emit. One emitter serves every burst of its kind, so a hundred hits cost a
    /// hundred Emit calls and no entities. Main thread.
    /// </summary>
    public sealed class Effects
    {
        /// <summary>An effect id not looked up yet (callers that resolve lazily).</summary>
        public const int UNRESOLVED = -2;
        /// <summary>The id <see cref="Find"/> returns for a name the scene has no emitter for; emitting it does nothing.</summary>
        public const int MISSING = -1;

        /// <summary>Groups under the effects root that hold the hero's own effects (faded by the opacity setting).</summary>
        private static readonly HashSet<string> HERO_GROUPS = new HashSet<string>
        {
            "Hero", "Knight", "Barbarian", "Mage", "Rogue", "Weapons", "Trails", "MageFx", "KnightFx", "BarbarianFx", "RogueFx", "SharedFx",
            "MageFx2", "KnightFx2", "BarbarianFx2", "RogueFx2", "MageFx3", "KnightFx3", "BarbarianFx3", "RogueFx3",
        };

        private readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        private readonly List<ParticleEmitterComponent> emitters = new List<ParticleEmitterComponent>();
        private readonly List<bool> isHeroEffect = new List<bool>();
        private readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>Player setting (GameUI.ApplySettings): opacity of the hero's own effects; enemy effects and
        /// telegraphs stay whole.</summary>
        public float heroOpacity = 1.0f;

        /// <summary>Registers every emitter under <paramref name="root"/> by its entity name.</summary>
        public void Collect(Entity root)
        {
            if (!root.IsValid())
            {
                return;
            }
            foreach (ParticleEmitterComponent emitter in root.GetComponentsInChildren<ParticleEmitterComponent>())
            {
                string name = emitter.owner.name;
                if (ids.ContainsKey(name))
                {
                    Log.Warning($"Effects: two emitters are named {name}; the first one is used.");
                    continue;
                }
                ids[name] = emitters.Count;
                emitters.Add(emitter);
                Entity group = emitter.owner.transform.parent;
                isHeroEffect.Add(group.IsValid() && HERO_GROUPS.Contains(group.name));
            }
        }

        /// <summary>
        /// The id of an effect by name, or <see cref="MISSING"/> (logged once) when the scene has none. Look ids up once
        /// (when a system is built, or lazily from <see cref="UNRESOLVED"/>) and emit by id: the lookup is a dictionary
        /// search too slow for every frame.
        /// </summary>
        public int Find(string name)
        {
            if (name != null && ids.TryGetValue(name, out int id))
            {
                return id;
            }
            if (name != null && missing.Add(name))
            {
                Log.Warning($"Effects: no emitter named {name}.");
            }
            return MISSING;
        }

        /// <summary>A burst in the emitter's own colours.</summary>
        public void Emit(int id, Vector3 position, int count)
        {
            if (id < 0 || count <= 0)
            {
                return;
            }
            var emit = new EmitParams { position = position };
            if (IsFaded(id))
            {
                emit.color = Fade(id, Color.white);
            }
            emitters[id].Emit(count, emit);
        }

        /// <summary>A burst tinted by <paramref name="color"/> (multiplies the emitter's gradient).</summary>
        public void Emit(int id, Vector3 position, int count, Color color)
        {
            if (id < 0 || count <= 0)
            {
                return;
            }
            emitters[id].Emit(count, new EmitParams { position = position, color = Fade(id, color) });
        }

        /// <summary>A burst with a size (m) and lifetime of its own: rings, telegraphs, shockwaves.</summary>
        public void Emit(int id, Vector3 position, int count, Color color, float size, float lifetime)
        {
            if (id < 0 || count <= 0)
            {
                return;
            }
            emitters[id].Emit(count, new EmitParams { position = position, color = Fade(id, color), size = size, lifetime = lifetime });
        }

        /// <summary>A tinted burst with a size (m) of its own and the emitter's lifetime (projectile heads, layers).</summary>
        public void EmitSized(int id, Vector3 position, int count, Color color, float size)
        {
            if (id < 0 || count <= 0)
            {
                return;
            }
            emitters[id].Emit(count, new EmitParams { position = position, color = Fade(id, color), size = size });
        }

        /// <summary>A burst moving with a velocity (projectile sparks, thrown debris).</summary>
        public void EmitMoving(int id, Vector3 position, Vector3 velocity, int count, float size, float lifetime)
        {
            if (id < 0 || count <= 0)
            {
                return;
            }
            var emit = new EmitParams { position = position, velocity = velocity, size = size, lifetime = lifetime };
            if (IsFaded(id))
            {
                emit.color = Fade(id, Color.white);
            }
            emitters[id].Emit(count, emit);
        }

        /// <summary>A tinted burst moving with a velocity (oriented decals and swings that also take a color).</summary>
        public void EmitMoving(int id, Vector3 position, Vector3 velocity, int count, Color color, float size, float lifetime)
        {
            if (id < 0 || count <= 0)
            {
                return;
            }
            emitters[id].Emit(count, new EmitParams { position = position, velocity = velocity, color = Fade(id, color), size = size, lifetime = lifetime });
        }

        private bool IsFaded(int id) => heroOpacity < 1.0f && isHeroEffect[id];

        private Color Fade(int id, Color color) => IsFaded(id) ? new Color(color.r, color.g, color.b, color.a * heroOpacity) : color;

        /// <summary>Stops and clears every effect (run restart).</summary>
        public void ResetAll()
        {
            foreach (ParticleEmitterComponent emitter in emitters)
            {
                emitter.ResetEmitter();
            }
        }
    }
}

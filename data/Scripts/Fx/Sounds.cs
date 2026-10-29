using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>One named sound: variants picked at random, a pitch range and limits against spam.</summary>
    public sealed class SoundDef
    {
        public string name;
        public AudioClip[] variants = Array.Empty<AudioClip>();
        public float volume = 1.0f;
        public float pitchMin = 0.95f;
        public float pitchMax = 1.05f;
        /// <summary>Most voices of this sound at once, all variants together; a request over it is dropped.</summary>
        public int limit = 4;
        /// <summary>Random volume change per instance, +- this many dB, so repeats do not sound mechanical.</summary>
        public float volumeJitterDb = 2.0f;
        /// <summary>Seconds between two starts of this sound.</summary>
        public float minInterval = 0.03f;
        /// <summary>Plays at its world position (falls off with distance) instead of flat.</summary>
        public bool spatial = false;
        public AudioBus bus = AudioBus.Sfx;
        public int priority = 0;
        internal float lastPlayed = -1.0f;
        internal AudioVoice[] voices = Array.Empty<AudioVoice>();
    }

    /// <summary>
    /// The run's sound bank. Sounds are registered by name from asset keys; playing picks a variant and a pitch and
    /// drops the request when the sound played too recently or too often. Main thread.
    /// </summary>
    public sealed class Sounds
    {
        private const float SPATIAL_MIN_DISTANCE = 6.0f;
        private const float SPATIAL_MAX_DISTANCE = 60.0f;

        private readonly Dictionary<string, SoundDef> sounds = new Dictionary<string, SoundDef>();
        private readonly HashSet<string> missing = new HashSet<string>();
        private readonly System.Random random = new System.Random(1);

        /// <summary>Registers a sound from clip keys; clips that fail to load are skipped with a warning.</summary>
        public SoundDef Register(string name, string[] keys, float volume = 1.0f, float pitchJitter = 0.05f, int limit = 4, float minInterval = 0.03f, bool spatial = false, AudioBus bus = AudioBus.Sfx, int priority = 0)
        {
            var clips = new List<AudioClip>();
            foreach (string key in keys)
            {
                AudioClip clip = Assets.GetAsset<AudioClip>(key);
                if (!AssetChecks.IsLoaded(clip))
                {
                    Log.Warning($"Sounds: {name} has no clip {key}.");
                    continue;
                }
                Audio.SetClipLimit(clip, limit, minInterval);
                clips.Add(clip);
            }
            var def = new SoundDef
            {
                name = name,
                variants = clips.ToArray(),
                volume = volume,
                pitchMin = 1.0f - pitchJitter,
                pitchMax = 1.0f + pitchJitter,
                limit = limit,
                minInterval = minInterval,
                spatial = spatial,
                bus = bus,
                priority = priority,
                voices = new AudioVoice[Math.Max(1, limit)],
            };
            sounds[name] = def;
            return def;
        }

        /// <summary>Plays a sound flat (UI, the hero's own sounds).</summary>
        public AudioVoice Play(string name, float volumeScale = 1.0f, float pitchScale = 1.0f)
        {
            SoundDef def = Pick(name, out AudioClip clip);
            if (def == null)
            {
                return default;
            }
            return Track(def, Audio.PlayOneShot(clip, GetVolume(def) * volumeScale, GetPitch(def) * pitchScale, def.bus, def.priority));
        }

        /// <summary>Plays a sound at a world position (flat unless the sound is spatial).</summary>
        public AudioVoice PlayAt(string name, Vector3 position, float volumeScale = 1.0f, float pitchScale = 1.0f)
        {
            SoundDef def = Pick(name, out AudioClip clip);
            if (def == null)
            {
                return default;
            }
            if (!def.spatial)
            {
                return Track(def, Audio.PlayOneShot(clip, GetVolume(def) * volumeScale, GetPitch(def) * pitchScale, def.bus, def.priority));
            }
            AudioVoice voice = Audio.PlayOneShot(clip, position, GetVolume(def) * volumeScale, GetPitch(def) * pitchScale, def.bus, def.priority);
            voice.SetDistance(SPATIAL_MIN_DISTANCE, SPATIAL_MAX_DISTANCE);
            return Track(def, voice);
        }

        private SoundDef Pick(string name, out AudioClip clip)
        {
            clip = null;
            if (!sounds.TryGetValue(name, out SoundDef def))
            {
                if (missing.Add(name))
                {
                    Log.Warning($"Sounds: no sound named {name}.");
                }
                return null;
            }
            if (def.variants.Length == 0)
            {
                return null;
            }
            float now = Time.realtimeSinceStartup;
            if (def.lastPlayed >= 0.0f && now - def.lastPlayed < def.minInterval)
            {
                return null;
            }
            if (FindFreeVoice(def) < 0)
            {
                return null;
            }
            def.lastPlayed = now;
            clip = def.variants[random.Next(def.variants.Length)];
            return def;
        }

        private float GetPitch(SoundDef def) => def.pitchMin + (float)random.NextDouble() * (def.pitchMax - def.pitchMin);

        private float GetVolume(SoundDef def)
        {
            float db = ((float)random.NextDouble() * 2.0f - 1.0f) * def.volumeJitterDb;
            return def.volume * MathF.Pow(10.0f, db / 20.0f);
        }

        /// <summary>The slot of a voice of this sound that no longer plays, or -1 when the sound is at its limit.</summary>
        private static int FindFreeVoice(SoundDef def)
        {
            for (int i = 0; i < def.voices.Length; i++)
            {
                if (!def.voices[i].isPlaying)
                {
                    return i;
                }
            }
            return -1;
        }

        private static AudioVoice Track(SoundDef def, AudioVoice voice)
        {
            int slot = FindFreeVoice(def);
            if (slot >= 0 && voice.started)
            {
                def.voices[slot] = voice;
            }
            return voice;
        }
    }
}

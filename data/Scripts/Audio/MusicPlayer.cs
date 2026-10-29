using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Music: three looping tracks on 2D AudioSource entities (Music bus) that crossfade, one-shot victory and defeat
    /// stingers, and a wind bed under everything. The run creates it when play starts and ticks it with unscaled time,
    /// so menus, hit stops and the end slow motion do not slow it. Each tick plays the menu music whenever the title is
    /// up, swells the battle track with the number of enemies alive, ducks the SFX bus while a level-up or a chest holds
    /// the game, and beats a heart at low health. Main thread.
    /// </summary>
    public sealed class MusicPlayer
    {
        public enum Track
        {
            Menu,
            Battle,
            Boss,
            Victory,
            Defeat,
        }

        private const string VICTORY_KEY = SoundCatalog.AUDIO_ROOT + "music/victory_stinger.wav";
        private const string DEFEAT_KEY = SoundCatalog.AUDIO_ROOT + "music/defeat_stinger.wav";
        private const string AMBIENCE_KEY = SoundCatalog.AUDIO_ROOT + "ambience/moor_wind.wav";
        private const int LOOP_COUNT = 3;
        private const float CROSSFADE_SECONDS = 0.8f;
        private const float STINGER_FADE_SECONDS = 0.6f;
        private const float STINGER_VOLUME = 0.85f;
        private const float AMBIENCE_VOLUME = 0.45f;
        private const int MUSIC_PRIORITY = 100;
        private const int AMBIENCE_PRIORITY = 90;

        // Intensity: one stereo mix, so the battle track swells between 200 and 800 enemies alive instead of adding layers.
        private const float CALM_ALIVE = 200.0f;
        private const float FULL_ALIVE = 800.0f;
        private const float CALM_SHARE = 0.82f;
        private const float INTENSITY_RATE = 1.5f;
        private const float VOLUME_EPSILON = 0.002f;

        // Low health: a heartbeat at 1 Hz under 30% health, 1.6 Hz under 15%; the music backs off under 15%.
        private const float LOW_HEALTH = 0.30f;
        private const float CRITICAL_HEALTH = 0.15f;
        private const float HEARTBEAT_SECONDS = 1.0f;
        private const float CRITICAL_HEARTBEAT_SECONDS = 1.0f / 1.6f;
        private const float LOW_HEARTBEAT_VOLUME = 0.7f;
        private const float CRITICAL_MUSIC_SHARE = 0.7f;

        // Duck: -6 dB on the SFX bus while a level-up or a chest holds the game.
        private const float DUCK_GAIN = 0.5f;
        private const float DUCK_SECONDS = 0.15f;
        private const float RESTORE_SECONDS = 0.3f;

        private static readonly string[] LOOP_KEYS =
        {
            SoundCatalog.AUDIO_ROOT + "music/menu_loop.flac",
            SoundCatalog.AUDIO_ROOT + "music/battle_loop.flac",
            SoundCatalog.AUDIO_ROOT + "music/boss_loop.ogg",
        };

        /// <summary>Volume of each loop (menu, battle at full intensity, boss); balanced against their loudness.</summary>
        private static readonly float[] LOOP_VOLUMES = { 0.5f, 0.62f, 0.55f };

        private readonly Entity root;
        private readonly AudioSourceComponent[] loops = new AudioSourceComponent[LOOP_COUNT];
        private readonly AudioSourceComponent ambience;
        private readonly AudioClip victory;
        private readonly AudioClip defeat;
        private bool hasTrack;
        private Track track;
        private float fadeEndsAt;
        private float battleVolume;
        private bool isDucked;
        private float duckRestore = 1.0f;
        private float restoreEndsAt;
        private float heartbeatTimer;

        /// <summary>Creates the "Music" entity with the loops and the wind bed, and loads the stingers.</summary>
        public MusicPlayer()
        {
            root = Scene.CreateEntity("Music");
            for (int i = 0; i < LOOP_COUNT; i++)
            {
                loops[i] = CreateSource("Music " + (Track)i, LOOP_KEYS[i], AudioBus.Music, MUSIC_PRIORITY);
            }
            ambience = CreateSource("Ambience", AMBIENCE_KEY, AudioBus.Sfx, AMBIENCE_PRIORITY);
            victory = LoadClip(VICTORY_KEY);
            defeat = LoadClip(DEFEAT_KEY);
        }

        /// <summary>Plays a track on the run's player; for callers that hold the run but not the player.</summary>
        public static void Play(Run run, Track next) => run?.music?.Play(next);

        /// <summary>Crossfades to a looping track, or plays the victory / defeat stinger once over fading music.</summary>
        public void Play(Track next)
        {
            if (next == Track.Victory || next == Track.Defeat)
            {
                PlayStinger(next);
                return;
            }
            int index = (int)next;
            if (hasTrack && track == next && loops[index].isPlaying)
            {
                return;
            }
            FadeOutLoops(CROSSFADE_SECONDS, index);
            AudioSourceComponent source = loops[index];
            if (!source.isPlaying)
            {
                source.volume = 0.0f;
                source.Play();
            }
            float target = next == Track.Battle ? LOOP_VOLUMES[index] * CALM_SHARE : LOOP_VOLUMES[index];
            battleVolume = target;
            source.FadeTo(target, CROSSFADE_SECONDS);
            fadeEndsAt = Time.realtimeSinceStartup + CROSSFADE_SECONDS;
            hasTrack = true;
            track = next;
            StartAmbience();
        }

        /// <summary>The per-frame part; the run calls it once per frame with unscaled time.</summary>
        public void Tick(Run run, float unscaledDt)
        {
            if (!root.IsValid())
            {
                return;
            }
            if (run.phase == RunPhase.Menu && (!hasTrack || track != Track.Menu))
            {
                Play(Track.Menu);
            }
            UpdateIntensity(run, unscaledDt);
            UpdateDuck(run);
            UpdateHeartbeat(run, unscaledDt);
        }

        private AudioSourceComponent CreateSource(string name, string key, AudioBus bus, int priority)
        {
            Entity entity = Scene.CreateEntity(name);
            entity.transform.SetParent(root, false);
            AudioSourceComponent source = entity.AddComponent<AudioSourceComponent>();
            source.clip = LoadClip(key);
            source.loop = true;
            source.spatial = false;
            source.bus = bus;
            source.priority = priority;
            source.volume = 0.0f;
            return source;
        }

        private static AudioClip LoadClip(string key)
        {
            AudioClip clip = Assets.GetAsset<AudioClip>(key);
            if (!AssetChecks.IsLoaded(clip))
            {
                Log.Warning($"MusicPlayer: no clip {key}.");
                return null;
            }
            return clip;
        }

        /// <summary>Fades out every playing loop but <paramref name="keep"/> (-1 fades them all).</summary>
        private void FadeOutLoops(float seconds, int keep = -1)
        {
            for (int i = 0; i < LOOP_COUNT; i++)
            {
                if (i != keep && loops[i].isPlaying)
                {
                    loops[i].FadeTo(0.0f, seconds, true);
                }
            }
        }

        private void PlayStinger(Track stinger)
        {
            FadeOutLoops(STINGER_FADE_SECONDS);
            hasTrack = true;
            track = stinger;
            AudioClip clip = stinger == Track.Victory ? victory : defeat;
            if (clip != null)
            {
                Audio.PlayOneShot(clip, STINGER_VOLUME, 1.0f, AudioBus.Music, MUSIC_PRIORITY);
            }
        }

        private void StartAmbience()
        {
            if (ambience.clip != null && !ambience.isPlaying)
            {
                ambience.volume = 0.0f;
                ambience.Play();
                ambience.FadeTo(AMBIENCE_VOLUME, CROSSFADE_SECONDS);
            }
        }

        /// <summary>The battle track swells from 82% to full between 200 and 800 enemies alive, and backs off at low health.</summary>
        private void UpdateIntensity(Run run, float dt)
        {
            if (!hasTrack || track != Track.Battle || run.phase != RunPhase.Playing || Time.realtimeSinceStartup < fadeEndsAt)
            {
                return;
            }
            int index = (int)Track.Battle;
            float alive = run.enemies != null ? run.enemies.count : 0.0f;
            float swell = Math.Clamp((alive - CALM_ALIVE) / (FULL_ALIVE - CALM_ALIVE), 0.0f, 1.0f);
            float target = LOOP_VOLUMES[index] * (CALM_SHARE + (1.0f - CALM_SHARE) * swell);
            if (run.hero != null && run.hero.isAlive && run.hero.healthFraction < CRITICAL_HEALTH)
            {
                target *= CRITICAL_MUSIC_SHARE;
            }
            float next = battleVolume + (target - battleVolume) * MathF.Min(1.0f, dt * INTENSITY_RATE);
            if (MathF.Abs(next - battleVolume) > VOLUME_EPSILON)
            {
                battleVolume = next;
                loops[index].volume = battleVolume;
            }
        }

        private void UpdateDuck(Run run)
        {
            bool holds = run.phase == RunPhase.LevelUp || run.phase == RunPhase.Chest;
            float now = Time.realtimeSinceStartup;
            if (holds && !isDucked)
            {
                // A restore still fading would hand back a lowered level: keep the level from before the last duck.
                if (now >= restoreEndsAt)
                {
                    duckRestore = Audio.GetBusVolume(AudioBus.Sfx);
                }
                Audio.FadeBusVolume(AudioBus.Sfx, duckRestore * DUCK_GAIN, DUCK_SECONDS);
                isDucked = true;
            }
            else if (!holds && isDucked)
            {
                Audio.FadeBusVolume(AudioBus.Sfx, duckRestore, RESTORE_SECONDS);
                restoreEndsAt = now + RESTORE_SECONDS;
                isDucked = false;
            }
        }

        private void UpdateHeartbeat(Run run, float dt)
        {
            HeroController hero = run.hero;
            if (run.phase != RunPhase.Playing || hero == null || !hero.isAlive || hero.healthFraction >= LOW_HEALTH || run.sounds == null)
            {
                heartbeatTimer = 0.0f;
                return;
            }
            heartbeatTimer -= dt;
            if (heartbeatTimer > 0.0f)
            {
                return;
            }
            bool isCritical = hero.healthFraction < CRITICAL_HEALTH;
            run.sounds.Play("heartbeat", isCritical ? 1.0f : LOW_HEARTBEAT_VOLUME);
            heartbeatTimer = isCritical ? CRITICAL_HEARTBEAT_SECONDS : HEARTBEAT_SECONDS;
        }
    }
}

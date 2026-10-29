using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Every sound of the game by name. Clips live under app:/data/Audio/[folder]/[name].wav or [name]_1..N.wav and were
    /// mastered offline (peak -1 dBFS, loudness per category), so the volumes here only trim the mix. Rules: pitch
    /// jitter +-6% unless a sound is tuned (gem chain, harp darts, disc ping chain); voices at once: enemy hit 8, enemy
    /// death 6, gem 4, coin 3, weapon fire 4 per weapon; 30 ms minimum retrigger. Spatial only where the sound happens
    /// at an enemy (hits, deaths, enemy casts, weapon impacts on targets): the camera is ~21 m from the hero, so those
    /// use Sounds' 6-60 m range and carry a little more volume. The hero's own weapons, pickups, bosses, stingers and
    /// UI play flat. Each row names only what differs from its preset: one variant, one voice, the 30 ms retrigger
    /// gap, +-6% pitch and the Sfx bus. Main thread.
    /// </summary>
    public static class SoundCatalog
    {
        /// <summary>Root of every audio asset key (sounds and music).</summary>
        internal const string AUDIO_ROOT = "app:/data/Audio/";

        private const float JITTER = 0.06f;
        private const float RETRIGGER = 0.03f;
        /// <summary>Stingers (one-off announcements) restart at most every half second and keep their pitch.</summary>
        private const float STING_GAP = 0.5f;
        private const float STING_JITTER = 0.0f;
        private const float BOSS_GAP = 0.1f;
        private const float BOSS_JITTER = 0.03f;
        private const int MENU_VOICES = 2;
        private const float MENU_JITTER = 0.03f;

        private const int PRIORITY_AMBIENT = 0;
        private const int PRIORITY_HIT = 1;
        private const int PRIORITY_DEATH = 2;
        private const int PRIORITY_WEAPON = 4;
        private const int PRIORITY_IMPACT = 5;
        private const int PRIORITY_PICKUP = 5;
        private const int PRIORITY_DANGER = 7;
        private const int PRIORITY_HEAVY = 8;
        private const int PRIORITY_HERO = 9;
        private const int PRIORITY_STINGER = 10;
        private const int PRIORITY_BOSS = 11;
        private const int PRIORITY_UI = 12;

        /// <summary>Registers every sound of the game in <paramref name="sounds"/>.</summary>
        public static void Register(Sounds sounds)
        {
            var catalog = new Registrar(sounds);
            RegisterCombat(catalog);
            RegisterHero(catalog);
            RegisterPickups(catalog);
            RegisterDirector(catalog);
            RegisterEnemies(catalog);
            RegisterBosses(catalog);
            RegisterUi(catalog);
            RegisterKnight(catalog);
            RegisterBarbarian(catalog);
            RegisterMage(catalog);
            RegisterRogue(catalog);
            RegisterShared(catalog);
            RegisterAliases(catalog);
        }

        private static void RegisterCombat(Registrar c)
        {
            c.Spatial("combat", "hit", PRIORITY_HIT, volume: 0.5f, variants: 6, limit: 8);
            c.Spatial("combat", "hit_crit", PRIORITY_IMPACT, volume: 0.7f, variants: 4, limit: 4, jitter: 0.05f);
            c.Spatial("combat", "enemy_death", PRIORITY_DEATH, volume: 0.6f, variants: 6, limit: 6);
            c.Flat("combat", "elite_death", PRIORITY_STINGER, volume: 0.95f, variants: 2, limit: 2, gap: 0.1f);
        }

        private static void RegisterHero(Registrar c)
        {
            // One grunt per half second at most.
            c.Flat("hero", "hero_hurt", PRIORITY_HERO, volume: 0.85f, variants: 4, gap: 0.5f);
            c.Sting("hero", "revive", volume: 1.0f);
            c.Flat("hero", "heartbeat", PRIORITY_HERO, volume: 0.7f, gap: 0.3f, jitter: 0.02f);
            c.Sting("hero", "run_start", volume: 0.9f);
            // Level-up, chest and evolution play while a menu ducks the SFX bus: they ride the UI bus.
            c.Flat("hero", "level_up", PRIORITY_UI, volume: 0.9f, gap: 0.2f, jitter: 0.0f, bus: AudioBus.Ui);
            c.Flat("hero", "evolve", PRIORITY_UI, volume: 0.95f, gap: STING_GAP, jitter: 0.0f, bus: AudioBus.Ui);
            c.Flat("pickups", "chest_open", PRIORITY_UI, volume: 0.95f, gap: STING_GAP, jitter: 0.0f, bus: AudioBus.Ui);
            c.Flat("ui", "card_pick", PRIORITY_UI, volume: 0.8f, gap: 0.1f, jitter: 0.02f, bus: AudioBus.Ui);
            c.Flat("ui", "active_ready", PRIORITY_UI, volume: 0.7f, gap: 0.2f, jitter: 0.0f, bus: AudioBus.Ui);
            c.Flat("barbarian", "fury_max", PRIORITY_HERO, volume: 0.9f, gap: STING_GAP, jitter: 0.03f);
            c.Flat("rogue", "momentum_max", PRIORITY_HERO, volume: 0.7f, gap: STING_GAP, jitter: 0.03f);
        }

        private static void RegisterPickups(Registrar c)
        {
            // The gem chain climbs a semitone per gem (Drops), so the clip itself stays in tune.
            c.Flat("pickups", "gem", PRIORITY_PICKUP, volume: 0.45f, limit: 4, jitter: 0.01f);
            c.Flat("pickups", "coin", PRIORITY_PICKUP, volume: 0.5f, variants: 3, limit: 3, gap: 0.04f);
            c.Flat("pickups", "heal", PRIORITY_PICKUP, volume: 0.8f, gap: 0.1f, jitter: 0.03f);
            c.Sting("pickups", "lodestone", volume: 0.9f);
            c.Sting("pickups", "holy_bell", volume: 1.0f);
            c.Sting("pickups", "frost_hourglass", volume: 0.95f);
            c.Spatial("pickups", "lantern_break", PRIORITY_IMPACT, volume: 0.9f, limit: 2, gap: 0.05f);
        }

        private static void RegisterDirector(Registrar c)
        {
            c.Sting("director", "ring_rattle", volume: 0.9f);
            c.Sting("director", "last_tide", volume: 1.0f);
            c.Sting("director", "bell_toll", volume: 1.0f);
            c.Sting("director", "rush_rumble", volume: 0.95f);
            c.Sting("director", "elite_horn", volume: 0.9f, jitter: 0.02f);
        }

        private static void RegisterEnemies(Registrar c)
        {
            c.Spatial("enemies", "cinder_fuse", PRIORITY_DANGER, volume: 0.75f, limit: 4, gap: 0.05f);
            c.Spatial("enemies", "cinder_blast", PRIORITY_HEAVY, volume: 0.95f, variants: 3, limit: 4, gap: 0.05f);
            c.Spatial("enemies", "grave_burst", PRIORITY_DANGER, volume: 0.85f, variants: 2, limit: 4, gap: 0.05f);
            c.Spatial("enemies", "bolt_draw", PRIORITY_DANGER, volume: 0.6f, variants: 2, limit: 4, gap: 0.06f);
            c.Spatial("enemies", "bolt_fire", PRIORITY_DANGER, volume: 0.7f, variants: 3, limit: 4, gap: 0.05f);
            c.Spatial("enemies", "hex_cast", PRIORITY_DANGER, volume: 0.75f, limit: 3, gap: 0.1f, jitter: 0.03f);
            c.Spatial("enemies", "raise_chant", PRIORITY_DANGER, volume: 0.8f, limit: 2, gap: 0.2f, jitter: 0.03f);
            c.Spatial("enemies", "summon", PRIORITY_DANGER, volume: 0.85f, variants: 2, limit: 3, gap: 0.1f);
            c.Spatial("enemies", "ward", PRIORITY_DANGER, volume: 0.6f, limit: 2, gap: 0.1f);
        }

        private static void RegisterBosses(Registrar c)
        {
            // Flat (heard off screen), high priority, 1-2 voices: a boss's stings must be unmistakable.
            c.Boss("boss_rise", 1, 1.0f);
            c.Boss("king_rise", 1, 1.0f);
            c.Boss("warden_charge_sting", 2, 0.95f);
            c.Boss("warden_charge_go", 1, 0.9f);
            c.Boss("warden_charge_hit", 2, 1.0f, 2);
            c.Boss("warden_charge_stop", 1, 0.95f);
            c.Boss("warden_cleave_sting", 2, 0.95f);
            c.Boss("warden_cleave_hit", 2, 1.0f, 2);
            c.Boss("warden_guard_up", 1, 0.85f);
            c.Flat("boss", "guard_tink", PRIORITY_BOSS, volume: 0.7f, variants: 4, limit: 3, gap: 0.1f, jitter: 0.08f);
            c.Boss("boss_dazed", 1, 0.85f);
            c.Boss("archmage_spiral_sting", 1, 0.95f);
            c.Flat("boss", "archmage_orb", PRIORITY_BOSS, volume: 0.5f, variants: 3, limit: 2, gap: 0.06f);
            c.Boss("archmage_field_sting", 1, 0.95f);
            c.Boss("archmage_summon_sting", 1, 0.95f);
            c.Boss("archmage_blink", 2, 0.85f);
            c.Boss("king_cleave_sting", 2, 1.0f);
            c.Boss("king_cleave_hit", 2, 1.0f, 2);
            c.Boss("king_slam_sting", 1, 1.0f);
            c.Boss("king_slam_hit", 1, 1.0f, 2);
            c.Flat("boss", "bone_spike", PRIORITY_BOSS, volume: 0.8f, variants: 3, limit: 4, gap: 0.05f);
            c.Boss("king_rain_sting", 1, 1.0f);
            c.Flat("boss", "bone_rain_impact", PRIORITY_BOSS, volume: 0.75f, variants: 4, limit: 6, gap: 0.05f);
            c.Boss("king_raise_sting", 1, 1.0f);
            c.Boss("king_spin_sting", 1, 1.0f);
            c.Flat("boss", "king_spin_whoosh", PRIORITY_BOSS, volume: 0.85f, variants: 3, limit: 2, gap: 0.1f);
            c.Boss("king_nova_sting", 1, 1.0f);
            c.Flat("boss", "soul_nova_ring", PRIORITY_BOSS, volume: 0.95f, variants: 2, limit: 3, gap: 0.1f, jitter: 0.03f);
            c.Boss("king_phase_break", 1, 1.0f);
            c.Boss("king_roar", 1, 1.0f);
            c.Boss("phase_shockwave", 1, 1.0f);
            c.Boss("king_enrage", 1, 1.0f);
            c.Boss("boss_death", 1, 1.0f);
            c.Boss("king_death", 1, 1.0f);
            c.Boss("death_wave", 1, 1.0f);
            c.Boss("gold_shower", 1, 0.85f);
            c.Boss("boss_chest_drop", 1, 0.95f);
        }

        private static void RegisterUi(Registrar c)
        {
            // Menus ride the Ui bus unless noted.
            c.Flat("ui", "ui_hover", PRIORITY_UI, volume: 0.5f, variants: 2, gap: 0.06f, jitter: 0.04f, bus: AudioBus.Ui);
            c.Ui("ui_click", 2, 0.7f);
            c.Ui("ui_confirm", 1, 0.8f);
            c.Ui("ui_back", 1, 0.7f);
            c.Ui("ui_error", 1, 0.8f);
            c.Ui("ui_select", 1, 0.7f);
            c.Ui("ui_toggle", 1, 0.7f);
            c.Flat("ui", "ui_tick", PRIORITY_UI, volume: 0.5f, limit: 2, gap: 0.04f, jitter: 0.03f, bus: AudioBus.Ui);
            c.Flat("ui", "card_flip", PRIORITY_UI, volume: 0.7f, variants: 3, limit: 3, bus: AudioBus.Ui);
            c.Ui("card_select", 1, 0.75f);
            c.Ui("card_reroll", 1, 0.8f);
            c.Ui("card_banish", 1, 0.8f);
            c.Ui("card_skip", 1, 0.75f);
            c.Flat("ui", "reel_tick", PRIORITY_UI, volume: 0.55f, variants: 3, limit: 2, gap: 0.06f, jitter: 0.04f, bus: AudioBus.Ui);
            c.Ui("reel_land", 2, 0.8f);
            c.Ui("reel_land_evolution", 1, 0.95f);
            c.Ui("chest_gold", 1, 0.75f);
            c.Ui("buy", 2, 0.8f);
            c.Ui("refund", 1, 0.8f);
            c.Ui("banner", 1, 0.75f);
            c.Ui("banner_warning", 1, 0.9f);
            c.Sting("ui", "danger_warning", volume: 0.85f);
            c.Ui("boss_bar_appear", 1, 0.9f);
            c.Ui("boss_bar_shatter", 1, 0.9f);
            c.Ui("results_victory", 1, 1.0f);
            c.Ui("results_defeat", 1, 1.0f);
            c.Ui("results_tally", 1, 0.8f);
        }

        private static void RegisterKnight(Registrar c)
        {
            c.Flat("knight", "oathblade_swing", PRIORITY_WEAPON, volume: 0.75f, variants: 3, limit: 3, gap: 0.08f, jitter: 0.05f);
            c.Spatial("knight", "oathblade_hit", PRIORITY_IMPACT, volume: 0.85f, variants: 3, limit: 3);
            c.Flat("knight", "dawn_crescent", PRIORITY_WEAPON, volume: 0.8f, variants: 2, limit: 2, gap: 0.08f);
            c.Flat("knight", "aegis_summon", PRIORITY_WEAPON, volume: 0.6f, variants: 2, limit: 2, gap: 0.1f, jitter: 0.03f);
            c.Spatial("knight", "aegis_bonk", PRIORITY_IMPACT, volume: 0.75f, variants: 4, limit: 4, gap: 0.05f, jitter: 0.08f);
            c.Spatial("knight", "aegis_block", PRIORITY_IMPACT, volume: 0.8f, variants: 3, limit: 3);
            c.Spatial("knight", "verdict_summon", PRIORITY_WEAPON, volume: 0.75f, variants: 2, limit: 2);
            c.Spatial("knight", "verdict_slam", PRIORITY_HEAVY, volume: 1.0f, variants: 3, limit: 3);
            c.Spatial("knight", "verdict_shockwave", PRIORITY_HEAVY, volume: 1.0f, variants: 2, limit: 2);
            c.Flat("knight", "shield_rush", PRIORITY_WEAPON, volume: 0.85f, variants: 2, gap: 0.1f);
            c.Flat("knight", "shield_rush_boom", PRIORITY_HEAVY, volume: 1.0f, variants: 2, limit: 2, gap: 0.1f);
            c.Flat("knight", "bulwark_clang", PRIORITY_IMPACT, volume: 0.9f, variants: 3, limit: 3, gap: 0.05f);
        }

        private static void RegisterBarbarian(Registrar c)
        {
            c.Flat("barbarian", "ravager_chop", PRIORITY_WEAPON, volume: 0.8f, variants: 3, limit: 3, gap: 0.05f);
            c.Spatial("barbarian", "ravager_hit", PRIORITY_HEAVY, volume: 0.95f, variants: 4, limit: 3);
            c.Spatial("barbarian", "skullsplitter_crack", PRIORITY_HEAVY, volume: 1.0f, variants: 2, limit: 2, gap: 0.05f);
            // The script restarts the loop when it ends and ramps its pitch: no retrigger gap, no jitter.
            c.Flat("barbarian", "whirlwind_loop", PRIORITY_HEAVY, volume: 0.75f, gap: 0.0f, jitter: 0.0f);
            c.Flat("barbarian", "whirlwind_stop", PRIORITY_WEAPON, volume: 0.8f, variants: 2, limit: 2, gap: 0.1f);
            c.Flat("barbarian", "fault_stomp", PRIORITY_HEAVY, volume: 0.9f, variants: 2, limit: 2, gap: 0.05f);
            c.Spatial("barbarian", "fault_crack", PRIORITY_IMPACT, volume: 0.9f, variants: 2, limit: 4, gap: 0.05f);
            c.Flat("barbarian", "leap_takeoff", PRIORITY_HERO, volume: 0.85f, variants: 2, gap: 0.2f);
            c.Flat("barbarian", "leap_land", PRIORITY_HEAVY, volume: 1.0f, variants: 2, gap: 0.2f);
        }

        private static void RegisterMage(Registrar c)
        {
            c.Flat("mage", "ember_whoosh", PRIORITY_WEAPON, volume: 0.7f, variants: 3, limit: 4, gap: 0.04f);
            c.Spatial("mage", "ember_boom", PRIORITY_IMPACT, volume: 0.95f, variants: 4, limit: 4, gap: 0.04f);
            c.Spatial("mage", "starfall_whistle", PRIORITY_WEAPON, volume: 0.7f, variants: 2, limit: 4, gap: 0.05f);
            c.Spatial("mage", "starfall_impact", PRIORITY_HEAVY, volume: 1.0f, variants: 3, limit: 4, gap: 0.05f);
            c.Spatial("mage", "cataclysm_whistle", PRIORITY_HEAVY, volume: 0.85f, gap: 0.1f, jitter: 0.03f);
            c.Spatial("mage", "cataclysm_impact", PRIORITY_STINGER, volume: 1.0f, gap: 0.1f, jitter: 0.04f);
            c.Flat("mage", "rime_nova", PRIORITY_IMPACT, volume: 0.85f, variants: 3, limit: 3, gap: 0.05f);
            c.Spatial("mage", "frost_shatter", PRIORITY_IMPACT, volume: 0.8f, variants: 4, limit: 3, gap: 0.04f);
            c.Flat("mage", "arcane_echo", PRIORITY_WEAPON, volume: 0.6f, variants: 2, limit: 2, gap: 0.08f, jitter: 0.03f);
            c.Flat("mage", "blink", PRIORITY_HERO, volume: 0.8f, variants: 2, gap: 0.1f);
            c.Spatial("mage", "blink_rift", PRIORITY_HEAVY, volume: 0.95f, variants: 2, limit: 2, gap: 0.1f);
        }

        private static void RegisterRogue(Registrar c)
        {
            c.Flat("rogue", "crossbow_twang", PRIORITY_WEAPON, volume: 0.7f, variants: 4, limit: 4, gap: 0.04f, jitter: 0.05f);
            c.Spatial("rogue", "widow_ricochet", PRIORITY_IMPACT, volume: 0.85f, variants: 3, limit: 3, gap: 0.04f);
            c.Flat("rogue", "knife_fan", PRIORITY_WEAPON, volume: 0.8f, variants: 3, limit: 3, gap: 0.05f);
            c.Spatial("rogue", "mark_apply", PRIORITY_IMPACT, volume: 0.8f, variants: 2, limit: 2, gap: 0.08f);
            c.Spatial("rogue", "mark_stab", PRIORITY_IMPACT, volume: 0.9f, variants: 3, limit: 3);
            c.Spatial("rogue", "mark_daggers", PRIORITY_IMPACT, volume: 0.85f, variants: 2, limit: 2, gap: 0.08f);
            c.Spatial("rogue", "execute", PRIORITY_HEAVY, volume: 1.0f, variants: 2, limit: 2, gap: 0.08f);
            c.Flat("rogue", "smoke_poof", PRIORITY_WEAPON, volume: 0.8f, variants: 2, limit: 2, gap: 0.1f);
            c.Flat("rogue", "roll_whoosh", PRIORITY_HERO, volume: 0.8f, variants: 2, gap: 0.1f);
        }

        private static void RegisterShared(Registrar c)
        {
            // Spirit darts are harp notes of one pentatonic set: tiny jitter keeps them in tune.
            c.Flat("shared", "spirit_dart", PRIORITY_WEAPON, volume: 0.45f, variants: 4, limit: 4, gap: 0.04f, jitter: 0.015f);
            c.Flat("shared", "disc_throw", PRIORITY_WEAPON, volume: 0.7f, variants: 2, limit: 2, gap: 0.05f);
            // The disc climbs a semitone per bounce: +-2% only.
            c.Spatial("shared", "disc_ping", PRIORITY_IMPACT, volume: 0.8f, variants: 2, limit: 3, jitter: 0.02f);
            c.Flat("shared", "bramble_rustle", PRIORITY_AMBIENT, volume: 0.55f, variants: 3, gap: 0.1f);
            c.Flat("shared", "javelin_throw", PRIORITY_WEAPON, volume: 0.8f, variants: 2, limit: 2, gap: 0.05f);
            c.Spatial("shared", "godspear_boom", PRIORITY_HEAVY, volume: 1.0f, variants: 2, limit: 2, gap: 0.05f);
            c.Flat("shared", "flask_throw", PRIORITY_WEAPON, volume: 0.65f, variants: 2, limit: 2, gap: 0.05f);
            c.Spatial("shared", "flask_shatter", PRIORITY_IMPACT, volume: 0.9f, variants: 3, limit: 3, gap: 0.05f);
            c.Spatial("shared", "totem_place", PRIORITY_WEAPON, volume: 0.8f, variants: 2, limit: 2, gap: 0.1f);
            c.Spatial("shared", "totem_zap", PRIORITY_IMPACT, volume: 0.75f, variants: 4, limit: 4, gap: 0.04f);
            c.Spatial("shared", "wisp_whistle", PRIORITY_WEAPON, volume: 0.6f, variants: 3, limit: 3, gap: 0.05f);
            c.Spatial("shared", "wisp_splash", PRIORITY_IMPACT, volume: 0.7f, variants: 3, limit: 3, gap: 0.05f);
        }

        /// <summary>Generic names some scripts play, sharing a clip set above (same volume, limits and routing).</summary>
        private static void RegisterAliases(Registrar c)
        {
            c.Alias("war_roar", "fury_max");
            c.Alias("lightning_crack", "totem_zap");
            c.Alias("knife_throw", "knife_fan");
            c.Alias("shadow_whoosh", "roll_whoosh");
        }

        /// <summary>One sound's clip keys and playback rules.</summary>
        private readonly struct Spec
        {
            public readonly string[] keys;
            public readonly float volume;
            public readonly float pitchJitter;
            public readonly int limit;
            public readonly float minInterval;
            public readonly bool spatial;
            public readonly AudioBus bus;
            public readonly int priority;

            public Spec(string[] keys, float volume, float pitchJitter, int limit, float minInterval, bool spatial, AudioBus bus, int priority)
            {
                this.keys = keys;
                this.volume = volume;
                this.pitchJitter = pitchJitter;
                this.limit = limit;
                this.minInterval = minInterval;
                this.spatial = spatial;
                this.bus = bus;
                this.priority = priority;
            }
        }

        /// <summary>Builds clip keys and registers them; remembers each spec so aliases share it.</summary>
        private sealed class Registrar
        {
            private readonly Sounds sounds;
            private readonly Dictionary<string, Spec> specs = new Dictionary<string, Spec>();

            public Registrar(Sounds sounds)
            {
                this.sounds = sounds;
            }

            /// <summary>
            /// A sound played flat (no distance falloff) from [folder]/[name].wav, or [folder]/[name]_1..N.wav when it has
            /// several <paramref name="variants"/>.
            /// </summary>
            /// <param name="priority">Voice priority when the mixer runs out of voices (PRIORITY_*).</param>
            /// <param name="limit">Voices of the whole sound at once.</param>
            /// <param name="gap">Seconds before the sound can start again.</param>
            /// <param name="jitter">Pitch change per play, +- this share.</param>
            public void Flat(string folder, string name, int priority, float volume, int variants = 1, int limit = 1,
                             float gap = RETRIGGER, float jitter = JITTER, AudioBus bus = AudioBus.Sfx)
            {
                Add(name, new Spec(BuildKeys(folder, name, variants), volume, jitter, limit, gap, false, bus, priority));
            }

            /// <summary>A sound placed in the world at an enemy (falls off with distance); otherwise like <see cref="Flat"/>.</summary>
            public void Spatial(string folder, string name, int priority, float volume, int variants = 1, int limit = 1,
                                float gap = RETRIGGER, float jitter = JITTER)
            {
                Add(name, new Spec(BuildKeys(folder, name, variants), volume, jitter, limit, gap, true, AudioBus.Sfx, priority));
            }

            /// <summary>A stinger: one clip, one voice, flat, stinger priority, the half-second gap and no pitch change.</summary>
            public void Sting(string folder, string name, float volume, float jitter = STING_JITTER)
            {
                Flat(folder, name, PRIORITY_STINGER, volume, gap: STING_GAP, jitter: jitter);
            }

            /// <summary>A boss sound: flat, boss priority, one voice unless more are asked for.</summary>
            public void Boss(string name, int variants, float volume, int limit = 1)
            {
                Flat("boss", name, PRIORITY_BOSS, volume, variants, limit, BOSS_GAP, BOSS_JITTER);
            }

            /// <summary>A menu sound: Ui bus, two voices.</summary>
            public void Ui(string name, int variants, float volume)
            {
                Flat("ui", name, PRIORITY_UI, volume, variants, MENU_VOICES, RETRIGGER, MENU_JITTER, AudioBus.Ui);
            }

            /// <summary>Registers <paramref name="alias"/> with the clips and rules of <paramref name="original"/>.</summary>
            public void Alias(string alias, string original)
            {
                if (!specs.TryGetValue(original, out Spec spec))
                {
                    Log.Warning($"SoundCatalog: alias {alias} has no sound {original}.");
                    return;
                }
                Add(alias, spec);
            }

            private void Add(string name, in Spec spec)
            {
                specs[name] = spec;
                Apply(name, spec);
            }

            private void Apply(string name, in Spec spec)
            {
                sounds.Register(name, spec.keys, spec.volume, spec.pitchJitter, spec.limit, spec.minInterval, spec.spatial, spec.bus, spec.priority);
            }

            private static string[] BuildKeys(string folder, string name, int variants)
            {
                string stem = AUDIO_ROOT + folder + "/" + name;
                if (variants <= 1)
                {
                    return new[] { stem + ".wav" };
                }
                var keys = new string[variants];
                for (int i = 0; i < variants; i++)
                {
                    keys[i] = stem + "_" + (i + 1) + ".wav";
                }
                return keys;
            }
        }
    }
}

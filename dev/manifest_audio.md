# Barrowlight - audio module manifest

Library: `S\src\Audio\` -> import as `app:/data/Audio/...` (keys `app:/data/Audio/<folder>/<name>[_N].wav`). 299 files, 29.74 MB (13.30 MB music). Built by `S\audio_work\tools\build_audio.py` (numpy only; Ogg sources decoded with the engine's own stb_vorbis through `oggdec.exe`), checked by `qa.py` (format, edges, DC, peak, loop seams, sub-bass of heavy hits) and `verify_catalog.py` (every catalog key exists, every name the scripts play and every manifest id is registered, every file is referenced).

## 1. Formats and processing

- SFX: 16-bit PCM WAV, 44.1 kHz, mono; stereo only for level_up, evolve, ring_rattle (bones circling) and the two music stingers.
- Every SFX: DC + rumble removed (28 Hz 4th-order high-pass), leading silence cut, tail cut once its 10 ms RMS stays 42 dB under the peak (cosine fade over 10% of the length), 3 ms in / 5 ms out fades, sample peak -1 dBFS, then loudness brought to the category target below (attenuated when louder; a look-ahead limiter adds up to the category's push when quieter), length capped per category / per sound, TPDF-dithered to 16 bit. No file peaks above -1 dBFS; no file starts or ends off zero.
- Layering: impacts are transient (0-30 ms: bone click, crack, metal tick) + body (50-200 ms punch / thump) + tail (ring, debris, reverb); heavy weapons and boss hits add a sub-bass sine thump under 80 Hz (`sub_boom`, falling 90 -> 30 Hz).
- Music: `battle_loop.flac` and `menu_loop.flac` are lossless FLAC (the engine imports .flac; encoder in `audio_work\tools\flac.py`, each file verified bit-exact through the engine's own dr_flac decoder at build time). `boss_loop.ogg` is the original Ogg copied unchanged. As 16-bit WAV the two loops would add 9.6 MB and push the library past 30 MB.

## 2. Loudness by category (momentary loudness, K-weighted, 100 ms window, dB LUFS-like)

| Category | Target | Max limiter push | Length cap s | Measured (min .. max) | Files |
|---|---|---|---|---|---|
| impact_heavy | -9 | +6 dB | 1.3 | -14.6 .. -9.6 | 36 |
| impact | -11 | +6 dB | 0.6 | -16.9 .. -11.0 | 70 |
| weapon | -12 | +5 dB | 0.75 | -16.5 .. -12.0 | 70 |
| enemy | -13 | +4 dB | 2.3 | -15.6 .. -13.0 | 25 |
| voice | -12 | +4 dB | 1.0 | -13.4 .. -12.0 | 7 |
| stinger | -11 | +3 dB | 2.6 | -14.0 .. -11.0 | 49 |
| pickup | -15 | +3 dB | 1.2 | -21.2 .. -15.0 | 8 |
| ui | -17 | +2 dB | 0.8 | -19.7 .. -17.0 | 23 |
| ui_tick | -20 | +2 dB | 0.08 | -20.0 .. -20.0 | 6 |
| loop | -15 | +3 dB | 4.0 | -15.0 .. -15.0 | 1 |
| ambience | -26 | +0 dB | 9.0 | -26.0 .. -26.0 | 1 |

Categories: impact_heavy = heavy weapon / boss hits; impact = hits, deaths, light weapon impacts; weapon = swings, casts, launches; enemy = enemy telegraphs and casts; voice = hero grunts; stinger = events, boss stings, big pickups; pickup = gems, coins, heal; ui / ui_tick = menus; loop = whirlwind; ambience = wind bed. Short clicks read lower than their category on a 100 ms meter. The volumes in SoundCatalog trim this: hits 0.5, deaths 0.6, gem 0.45, coins 0.5, weapon fires 0.6-0.85, heavy hits / stingers / bosses 0.9-1.0, UI 0.5-0.8; spatial sounds sit a little higher because the listener is ~21 m away.

## 3. Sounds (name -> files)

Routing columns: vol = SoundDef volume, lim = voices of the sound at once (split over its variants per clip), gap = minimum retrigger s, 3D = spatial (6-60 m), jit = pitch jitter +-.

### Combat (core)

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| hit | combat/hit_1..6.wav | 6 | 0.14-0.15 | bone click transient + punch body (60-170 ms) + bone crack | kenney_impact_sounds: impactPunch_medium_000.ogg, impactPunch_medium_001.ogg, impactPunch_medium_002.ogg, impactPunch_medium_003.ogg, impactPunch_medium_004.ogg; sfx_bone_breaks: Deep Break 1.wav, Deep Break 2.wav, Deep Break 3.wav, Deep Break 4.wav, Deep Break 6.wav, Deep Break 8.wav; sfx_bones_rattle: 0.ogg, 1.ogg, 2.ogg, 3.ogg, 4.ogg, 5.ogg | 0.5 | 8 | 0.03 | yes | Sfx | 0.06 |
| hit_crit | combat/hit_crit_1..4.wav | 4 | 0.17-0.18 | sharp metal tick + steel ring tail + heavy punch body + bone click | kenney_impact_sounds: impactMetal_light_000.ogg, impactMetal_light_001.ogg, impactMetal_light_002.ogg, impactMetal_light_004.ogg, impactPunch_heavy_000.ogg, impactPunch_heavy_001.ogg, impactPunch_heavy_002.ogg, impactPunch_heavy_003.ogg; sfx_80_rpg: blade_01.ogg, blade_02.ogg, blade_03.ogg; sfx_bones_rattle: 5.ogg, 6.ogg, 7.ogg, 8.ogg | 0.7 | 4 | 0.03 | yes | Sfx | 0.05 |
| enemy_death | combat/enemy_death_1..6.wav | 6 | 0.38-0.47 | bone break crunch + low thump + two bone rattles (scatter) | kenney_impact_sounds: impactSoft_medium_000.ogg, impactSoft_medium_001.ogg, impactSoft_medium_002.ogg, impactSoft_medium_003.ogg, impactSoft_medium_004.ogg; sfx_bone_breaks: Deep Break 1.wav, Deep Break 2.wav, Deep Break 3.wav, Deep Break 4.wav, Deep Break 6.wav, Deep Break 8.wav; sfx_bones_rattle: 0.ogg, 1.ogg, 2.ogg, 3.ogg, 4.ogg, 5.ogg, 6.ogg, 7.ogg, 8.ogg | 0.6 | 6 | 0.03 | yes | Sfx | 0.06 |
| elite_death | combat/elite_death_1..2.wav | 2 | 1.40-1.40 | two bone breaks (pitched down) + creature death groan + noise boom + sub < 80 Hz + coin glint | sfx_80_rpg: creature_die_01.ogg, item_coins_02.ogg; sfx_bone_breaks: Deep Break 1.wav, Deep Break 4.wav, Deep Break 6.wav, Deep Break 8.wav | 0.95 | 2 | 0.1 |  | Sfx | 0.06 |

### Hero and core events

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| hero_hurt | hero/hero_hurt_1..4.wav | 4 | 0.33-0.39 | body punch + armor rattle + grunt (voice pitched up) | kenney_impact_sounds: impactPunch_heavy_000.ogg, impactPunch_heavy_001.ogg, impactPunch_heavy_002.ogg, impactPunch_heavy_003.ogg; sfx_rpg_sound_pack: armor-light.wav, chainmail1.wav, chainmail2.wav, giant1.wav, giant3.wav, giant4.wav, giant5.wav | 0.85 | 1 | 0.5 |  | Sfx | 0.06 |
| revive | hero/revive.wav | 1 | 2.20 | riser + D major choir swell + rising whoosh, then chime arpeggio and sub boom at 0.85 s | sfx_soundpack01: Rise02.aif | 1.0 | 1 | 0.5 |  | Sfx | 0.0 |
| level_up | hero/level_up.wav | 1 | 2.00 st | rising D major chime arpeggio panned L->R + riser + choir pad + gem sparkle | sfx_gem: gem-gather-stereo.wav; sfx_soundpack01: Rise03.aif | 0.9 | 1 | 0.2 |  | Ui | 0.0 |
| heartbeat | hero/heartbeat.wav | 1 | 0.50 | lub-dub: two low sine thumps (70/62 Hz) with soft knocks | generated | 0.7 | 1 | 0.3 |  | Sfx | 0.02 |
| run_start | hero/run_start.wav | 1 | 2.20 | sword drawn + drum + church bell (D3) + sub boom | kenney_impact_sounds: impactSoft_heavy_001.ogg; sfx_rpg_sound_pack: sword-unsheathe.wav | 0.9 | 1 | 0.5 |  | Sfx | 0.0 |
| evolve | hero/evolve.wav | 1 | 1.60 st | D major brass chord + choir burst + major bell + chime run + sub | generated | 0.95 | 1 | 0.5 |  | Ui | 0.0 |

### Pickups

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| chest_open | pickups/chest_open.wav | 1 | 1.80 | lock click + latch + lid creak + coins + G major chime run + choir | kenney_rpg_audio: doorOpen_1.ogg, handleCoins.ogg, metalLatch.ogg; sfx_80_rpg: lock_02.ogg | 0.95 | 1 | 0.5 |  | Ui | 0.0 |
| gem | pickups/gem.wav | 1 | 0.30 | glass ting at A5 (+ fifth) + gem sparkle; the code climbs a semitone per chained gem | sfx_gem: gem-gather-stereo.wav | 0.45 | 4 | 0.03 |  | Sfx | 0.01 |
| coin | pickups/coin_1..3.wav | 3 | 0.23-0.32 | coin clink | sfx_80_rpg: item_coins_01.ogg, item_coins_03.ogg; sfx_rpg_sound_pack: coin3.wav | 0.5 | 3 | 0.04 |  | Sfx | 0.06 |
| heal | pickups/heal.wav | 1 | 1.20 | warm C major chime run + gulp + soft 'oh' choir | sfx_rpg_sound_pack: bubble2.wav | 0.8 | 1 | 0.1 |  | Sfx | 0.03 |
| lodestone | pickups/lodestone.wav | 1 | 0.99 | magnetic hum + riser + rising whoosh | kenney_scifi_sounds: forceField_000.ogg; sfx_soundpack01: Rise03.aif | 0.9 | 1 | 0.5 |  | Sfx | 0.0 |
| holy_bell | pickups/holy_bell.wav | 1 | 2.40 | heavy bell (pitched down) + synth church bell D3 + shock whoosh + sub | kenney_impact_sounds: impactBell_heavy_001.ogg | 1.0 | 1 | 0.5 |  | Sfx | 0.0 |
| frost_hourglass | pickups/frost_hourglass.wav | 1 | 2.38 | inhaling whoosh, then glass hit + ice crackle + glass chimes + soft sub | kenney_impact_sounds: impactGlass_medium_001.ogg; kenney_interface_sounds: glass_004.ogg | 0.95 | 1 | 0.5 |  | Sfx | 0.0 |
| lantern_break | pickups/lantern_break.wav | 1 | 0.60 | glass smash + glass ring + fire puff + crackles | kenney_impact_sounds: impactGlass_heavy_001.ogg; kenney_interface_sounds: glass_004.ogg | 0.9 | 2 | 0.05 | yes | Sfx | 0.06 |

### Director events

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ring_rattle | director/ring_rattle.wav | 1 | 2.20 st | 20 bone rattles circling L->R->L + low 'oo' drone | sfx_bones_rattle: 0.ogg, 1.ogg, 2.ogg, 3.ogg, 4.ogg, 5.ogg, 6.ogg, 7.ogg, 8.ogg, 9.ogg | 0.9 | 1 | 0.5 |  | Sfx | 0.0 |
| last_tide | director/last_tide.wav | 1 | 3.20 | D minor choir swell + low drone + bell toll (D2) + sub + rumble | generated | 1.0 | 1 | 0.5 |  | Sfx | 0.0 |
| bell_toll | director/bell_toll.wav | 1 | 3.50 | deep church bell (D2, synth) + heavy bell pitched down an octave | kenney_impact_sounds: impactBell_heavy_000.ogg | 1.0 | 1 | 0.5 |  | Sfx | 0.0 |
| rush_rumble | director/rush_rumble.wav | 1 | 2.33 | rising low rumble + four drum hits + a crowd of bone rattles | kenney_impact_sounds: impactSoft_heavy_000.ogg, impactSoft_heavy_001.ogg, impactSoft_heavy_002.ogg, impactSoft_heavy_003.ogg; kenney_scifi_sounds: spaceEngineLow_000.ogg; sfx_bones_rattle: 0.ogg, 1.ogg, 2.ogg, 3.ogg, 4.ogg, 5.ogg, 6.ogg, 7.ogg | 0.95 | 1 | 0.5 |  | Sfx | 0.0 |
| elite_horn | director/elite_horn.wav | 1 | 2.00 | war horn (D2/A2/D3 brass) + low roar + drum | kenney_impact_sounds: impactSoft_heavy_000.ogg; sfx_80_rpg: creature_roar_02.ogg | 0.9 | 1 | 0.5 |  | Sfx | 0.02 |

### Enemies

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| skeleton_rattle | enemies/skeleton_rattle_1..4.wav | 4 | 0.15-0.26 | 3-4 bone rattles | sfx_bones_rattle: 1.ogg, 2.ogg, 3.ogg, 4.ogg, 5.ogg, 6.ogg, 8.ogg, 9.ogg | 0.4 | 4 | 0.08 | yes | Sfx | 0.06 |
| cinder_fuse | enemies/cinder_fuse.wav | 1 | 0.85 | fuse sizzle growing over 0.8 s + crackles + rising whistle | kenney_scifi_sounds: thrusterFire_002.ogg | 0.75 | 4 | 0.05 | yes | Sfx | 0.06 |
| cinder_blast | enemies/cinder_blast_1..3.wav | 3 | 0.60-0.60 | explosion crunch + noise boom + sub + fire crackles | kenney_scifi_sounds: explosionCrunch_000.ogg, explosionCrunch_001.ogg, explosionCrunch_002.ogg | 0.95 | 4 | 0.05 | yes | Sfx | 0.06 |
| grave_burst | enemies/grave_burst_1..2.wav | 2 | 0.48-0.49 | earth burst: stones + ground thud + dark whoosh + rattles | kenney_impact_sounds: impactSoft_heavy_002.ogg, impactSoft_heavy_003.ogg; sfx_80_rpg: stones_01.ogg, stones_03.ogg; sfx_bones_rattle: 0.ogg, 3.ogg, 4.ogg, 5.ogg, 9.ogg | 0.85 | 4 | 0.05 | yes | Sfx | 0.06 |
| hex_orb_cast | enemies/hex_orb_cast_1..2.wav | 2 | 0.46-0.47 | dark spell (pitched down) + falling whoosh | sfx_80_rpg: spell_02.ogg | 0.7 | 4 | 0.06 | yes | Sfx | 0.06 |
| bolt_draw | enemies/bolt_draw_1..2.wav | 2 | 0.28-0.28 | bow string creak + cloth | kenney_rpg_audio: cloth2.ogg, creak3.ogg | 0.6 | 4 | 0.06 | yes | Sfx | 0.06 |
| bolt_fire | enemies/bolt_fire_1..3.wav | 3 | 0.30-0.30 | bow string twang (plucked string) + arrow swish | sfx_swishes: swish-3.wav, swish-7.wav, swish-9.wav | 0.7 | 4 | 0.05 | yes | Sfx | 0.06 |
| hex_cast | enemies/hex_cast.wav | 1 | 2.17 | ghostly voice (pitched down) + diminished 'oo' choir crescendo + rising whoosh into the 2.1 s hit | sfx_rpg_sound_pack: shade9.wav | 0.75 | 3 | 0.1 | yes | Sfx | 0.03 |
| raise_chant | enemies/raise_chant.wav | 1 | 1.66 | ghostly voice + low A minor 'oh' chant + rattles as the dead rise | sfx_bones_rattle: 1.ogg, 7.ogg, 8.ogg, 9.ogg; sfx_rpg_sound_pack: shade12.wav | 0.8 | 2 | 0.2 | yes | Sfx | 0.03 |
| summon | enemies/summon_1..2.wav | 2 | 0.68-0.68 | dirt burst + thud + low magic hum + rising whoosh + rattles | kenney_impact_sounds: impactSoft_heavy_000.ogg, impactSoft_heavy_001.ogg; kenney_scifi_sounds: forceField_003.ogg; sfx_80_rpg: stones_02.ogg, stones_04.ogg; sfx_bones_rattle: 2.ogg, 3.ogg, 4.ogg, 6.ogg | 0.85 | 3 | 0.1 | yes | Sfx | 0.06 |
| ward | enemies/ward.wav | 1 | 0.80 | force field hum + dissonant glass dyad (G#5 + A5) | kenney_scifi_sounds: forceField_002.ogg | 0.6 | 2 | 0.1 | yes | Sfx | 0.06 |

### Bosses (manifest_bosses.md)

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| boss_rise | boss/boss_rise.wav | 1 | 2.70 | deep ground rumble crescendo + three bone cracks + stones + distant moan (ghost voice, low-passed) + sub | sfx_80_rpg: stones_03.ogg; sfx_bone_breaks: Deep Break 1.wav, Deep Break 6.wav, Deep Break 8.wav; sfx_rpg_sound_pack: shade12.wav | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| king_rise | boss/king_rise.wav | 1 | 3.30 | rumble crescendo + stone cracking (pitched down) + low 'oo' choir swell + bone grinding (slow rattles) + sub | kenney_impact_sounds: impactMining_000.ogg, impactMining_001.ogg, impactMining_003.ogg; sfx_80_rpg: stones_03.ogg; sfx_bones_rattle: 0.ogg, 1.ogg, 2.ogg, 3.ogg, 4.ogg, 5.ogg, 6.ogg, 7.ogg, 8.ogg, 9.ogg | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| warden_charge_sting | boss/warden_charge_sting_1..2.wav | 2 | 0.78-0.78 | shield scraping on stone (steel slowed + grind noise) + low war growl | sfx_80_rpg: creature_monster_03.ogg, creature_monster_04.ogg; sfx_rpg_sound_pack: sword-unsheathe2.wav, sword-unsheathe4.wav | 0.95 | 1 | 0.1 |  | Sfx | 0.03 |
| warden_charge_go | boss/warden_charge_go.wav | 1 | 0.48 | burst of heavy footfalls (slowed) + ground thud + whoosh | kenney_impact_sounds: impactSoft_heavy_001.ogg; kenney_rpg_audio: footstep00.ogg, footstep03.ogg, footstep07.ogg | 0.9 | 1 | 0.1 |  | Sfx | 0.03 |
| warden_charge_hit | boss/warden_charge_hit_1..2.wav | 2 | 0.52-0.53 | metal shield clang + body thud + sub | kenney_impact_sounds: impactPlate_heavy_000.ogg, impactPlate_heavy_002.ogg, impactPunch_heavy_001.ogg, impactPunch_heavy_003.ogg | 1.0 | 2 | 0.1 |  | Sfx | 0.03 |
| warden_charge_stop | boss/warden_charge_stop.wav | 1 | 0.62 | boom + sub + gravel | sfx_80_rpg: stones_02.ogg, stones_04.ogg | 0.95 | 1 | 0.1 |  | Sfx | 0.03 |
| warden_cleave_sting | boss/warden_cleave_sting_1..2.wav | 2 | 0.60-0.60 | blade scrape + whoosh rising into the swing | sfx_rpg_sound_pack: sword-unsheathe4.wav, sword-unsheathe5.wav | 0.95 | 1 | 0.1 |  | Sfx | 0.03 |
| warden_cleave_hit | boss/warden_cleave_hit_1..2.wav | 2 | 0.42-0.43 | thud + earth crunch + metal ring + sub | kenney_impact_sounds: impactMetal_medium_000.ogg, impactMetal_medium_003.ogg, impactPunch_heavy_000.ogg, impactPunch_heavy_004.ogg; sfx_80_rpg: stones_01.ogg, stones_03.ogg | 1.0 | 2 | 0.1 |  | Sfx | 0.03 |
| warden_guard_up | boss/warden_guard_up.wav | 1 | 0.39 | metal clank + bone creak + chain rattle | kenney_impact_sounds: impactPlate_medium_002.ogg; kenney_rpg_audio: creak1.ogg; sfx_rpg_sound_pack: chainmail2.wav | 0.85 | 1 | 0.1 |  | Sfx | 0.03 |
| guard_tink | boss/guard_tink_1..4.wav | 4 | 0.07-0.12 | bright metal tink (light metal hit pitched up) | kenney_impact_sounds: impactMetal_light_000.ogg, impactMetal_light_001.ogg, impactMetal_light_003.ogg, impactMetal_light_004.ogg | 0.7 | 3 | 0.1 |  | Sfx | 0.08 |
| boss_dazed | boss/boss_dazed.wav | 1 | 1.00 | ringing tinnitus tone (3.15 kHz) + three falling chirps + small chime | generated | 0.85 | 1 | 0.1 |  | Sfx | 0.03 |
| archmage_spiral_sting | boss/archmage_spiral_sting.wav | 1 | 0.69 | violet hum swelling (D3 + G#3 saws, filter opening) + whispered ghost syllables | sfx_rpg_sound_pack: shade3.wav | 0.95 | 1 | 0.1 |  | Sfx | 0.03 |
| archmage_orb | boss/archmage_orb_1..3.wav | 3 | 0.25-0.25 | soft magic pop (falling sine) + small air puff + faint glass tone | generated | 0.5 | 2 | 0.06 |  | Sfx | 0.06 |
| archmage_field_sting | boss/archmage_field_sting.wav | 1 | 0.80 | earth whispers (flickering breath) + grave hiss rising + low rumble | generated | 0.95 | 1 | 0.1 |  | Sfx | 0.03 |
| archmage_summon_sting | boss/archmage_summon_sting.wav | 1 | 2.10 | dark low chant (A minor 'oh' voices pulsing in syllables) + bass voices + ghost voice | sfx_rpg_sound_pack: shade12.wav | 0.95 | 1 | 0.1 |  | Sfx | 0.03 |
| archmage_blink | boss/archmage_blink_1..2.wav | 2 | 0.60-0.60 | falling whoosh + crystalline chime | kenney_interface_sounds: glass_002.ogg, glass_005.ogg | 0.85 | 1 | 0.1 |  | Sfx | 0.03 |
| king_cleave_sting | boss/king_cleave_sting_1..2.wav | 2 | 0.77-0.78 | huge axe scraped across stone: steel slowed far down + chains + stone grind | sfx_80_rpg: chain_01.ogg, chain_03.ogg; sfx_rpg_sound_pack: sword-unsheathe.wav, sword-unsheathe3.wav | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| king_cleave_hit | boss/king_cleave_hit_1..2.wav | 2 | 0.82-0.82 | giant whoosh, then impact thud + sub < 80 Hz + grit | kenney_impact_sounds: impactPunch_heavy_000.ogg, impactPunch_heavy_002.ogg; sfx_80_rpg: stones_02.ogg | 1.0 | 2 | 0.1 |  | Sfx | 0.03 |
| king_slam_sting | boss/king_slam_sting.wav | 1 | 0.92 | deep giant inhale (rising band noise) + low growl + rattling ribs | sfx_80_rpg: creature_monster_03.ogg; sfx_bones_rattle: 1.ogg, 2.ogg, 3.ogg, 5.ogg, 6.ogg, 7.ogg, 8.ogg, 9.ogg | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| king_slam_hit | boss/king_slam_hit.wav | 1 | 1.40 | heavy punch + stone crack + air boom + sub < 60 Hz + debris tail | kenney_impact_sounds: impactMining_000.ogg, impactPunch_heavy_000.ogg; sfx_80_rpg: stones_03.ogg | 1.0 | 2 | 0.1 |  | Sfx | 0.03 |
| bone_spike | boss/bone_spike_1..3.wav | 3 | 0.14-0.15 | bone snap + crack + crackle + stone tick | sfx_80_rpg: stones_04.ogg; sfx_bone_breaks: Deep Break 2.wav, Deep Break 4.wav, Deep Break 9.wav | 0.8 | 4 | 0.05 |  | Sfx | 0.06 |
| king_rain_sting | boss/king_rain_sting.wav | 1 | 0.89 | many bones clattering + falling whistle | sfx_bones_rattle: 0.ogg, 1.ogg, 2.ogg, 4.ogg, 5.ogg, 6.ogg, 8.ogg | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| bone_rain_impact | boss/bone_rain_impact_1..4.wav | 4 | 0.27-0.29 | bone shatter + soft thud + scattering rattles | kenney_impact_sounds: impactSoft_medium_000.ogg, impactSoft_medium_001.ogg, impactSoft_medium_002.ogg, impactSoft_medium_003.ogg; sfx_bone_breaks: Deep Break 10.wav, Deep Break 3.wav, Deep Break 5.wav, Deep Break 7.wav; sfx_bones_rattle: 1.ogg, 4.ogg, 5.ogg, 6.ogg, 7.ogg, 8.ogg, 9.ogg | 0.75 | 6 | 0.05 |  | Sfx | 0.06 |
| king_raise_sting | boss/king_raise_sting.wav | 1 | 1.60 | low 'oo' choir + earth groan (growl far down) + shifting stones | sfx_80_rpg: creature_monster_04.ogg, stones_01.ogg | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| king_spin_sting | boss/king_spin_sting.wav | 1 | 0.68 | whoosh rising in pitch while its spin speeds up (4 to 14 passes per second) | generated | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| king_spin_whoosh | boss/king_spin_whoosh_1..3.wav | 3 | 0.43-0.44 | heavy axe whoosh (swish an octave down + low air) | sfx_swishes: swish-3.wav, swish-7.wav, swish-9.wav | 0.85 | 2 | 0.1 |  | Sfx | 0.06 |
| king_nova_sting | boss/king_nova_sting.wav | 1 | 0.98 | D harmonic-minor choir swell + rising high shimmer | generated | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| soul_nova_ring | boss/soul_nova_ring_1..2.wav | 2 | 1.20-1.20 | deep whoom pulse (sub + dark air) + ghostly wail (gliding down) + ghost voice | sfx_rpg_sound_pack: shade1.wav, shade10.wav | 0.95 | 3 | 0.1 |  | Sfx | 0.03 |
| king_phase_break | boss/king_phase_break.wav | 1 | 0.75 | bone crack + glassy shatter + heavy kneel thud + sub | kenney_impact_sounds: impactGlass_heavy_000.ogg, impactSoft_heavy_002.ogg; sfx_bone_breaks: Deep Break 1.wav | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| king_roar | boss/king_roar.wav | 1 | 1.90 | two creature roars pitched far down, distorted, + sub, long reverb | sfx_80_rpg: creature_roar_01.ogg, creature_roar_02.ogg, creature_roar_03.ogg | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| phase_shockwave | boss/phase_shockwave.wav | 1 | 0.83 | air boom + sub + outward falling whoosh | generated | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| king_enrage | boss/king_enrage.wav | 1 | 2.20 | distorted roar + bell toll (D2) + dissonant choir stab (D/G#) + sub | sfx_80_rpg: creature_roar_01.ogg, creature_roar_03.ogg | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| boss_death | boss/boss_death.wav | 1 | 1.68 | fading groan + bone break + shield clang and staff clatter + 14 bones clattering | kenney_impact_sounds: impactPlate_medium_003.ogg, impactWood_medium_002.ogg; sfx_80_rpg: creature_die_01.ogg, metal_01.ogg; sfx_bone_breaks: Deep Break 6.wav; sfx_bones_rattle: 0.ogg, 1.ogg, 2.ogg, 3.ogg, 4.ogg, 5.ogg, 8.ogg, 9.ogg | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| king_death | boss/king_death.wav | 1 | 3.60 | long roar fading into a D major choir chord + major bell + sub + bones raining | sfx_80_rpg: creature_roar_03.ogg; sfx_bones_rattle: 0.ogg, 1.ogg, 3.ogg, 4.ogg, 6.ogg, 7.ogg, 8.ogg, 9.ogg | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| death_wave | boss/death_wave.wav | 1 | 1.90 | spectral wind + rising whoosh + airy 'oo' voices | generated | 1.0 | 1 | 0.1 |  | Sfx | 0.03 |
| gold_shower | boss/gold_shower.wav | 1 | 1.40 | coin cascade (coin pour + two handfuls) + light chime run | kenney_rpg_audio: handleCoins.ogg; sfx_80_rpg: item_coins_04.ogg | 0.85 | 1 | 0.1 |  | Sfx | 0.03 |
| boss_chest_drop | boss/boss_chest_drop.wav | 1 | 0.82 | heavy chest thud + latch rattle + sub + sparkle chime | kenney_impact_sounds: impactSoft_heavy_003.ogg, impactWood_heavy_004.ogg; kenney_rpg_audio: metalLatch.ogg | 0.95 | 1 | 0.1 |  | Sfx | 0.03 |

### UI (manifest_ui.md)

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| active_ready | ui/active_ready.wav | 1 | 0.40 | soft two-note chime (A5, D6) | generated | 0.7 | 1 | 0.2 |  | Ui | 0.0 |
| card_pick | ui/card_pick.wav | 1 | 0.63 | bright confirm blip + D6/A6 chime + upward whoosh | kenney_interface_sounds: maximize_009.ogg | 0.8 | 1 | 0.1 |  | Ui | 0.02 |
| ui_hover | ui/ui_hover_1..2.wav | 2 | 0.04-0.04 | soft wooden tick | kenney_impact_sounds: impactWood_light_000.ogg, impactWood_light_002.ogg | 0.5 | 1 | 0.06 |  | Ui | 0.04 |
| ui_click | ui/ui_click_1..2.wav | 2 | 0.07-0.07 | click + low thump | kenney_impact_sounds: impactSoft_medium_001.ogg, impactSoft_medium_003.ogg; kenney_interface_sounds: click_003.ogg, click_005.ogg | 0.7 | 2 | 0.03 |  | Ui | 0.03 |
| ui_confirm | ui/ui_confirm.wav | 1 | 0.26 | click + bright E6/B6 chime | kenney_interface_sounds: click_002.ogg | 0.8 | 2 | 0.03 |  | Ui | 0.03 |
| ui_back | ui/ui_back.wav | 1 | 0.09 | reversed soft thump into a tiny click | kenney_impact_sounds: impactSoft_medium_002.ogg; kenney_interface_sounds: click_004.ogg | 0.7 | 2 | 0.03 |  | Ui | 0.03 |
| ui_error | ui/ui_error.wav | 1 | 0.18 | dull double wooden knock | kenney_impact_sounds: impactWood_medium_001.ogg | 0.8 | 2 | 0.03 |  | Ui | 0.03 |
| ui_select | ui/ui_select.wav | 1 | 0.16 | short metal ring + blade shimmer | kenney_impact_sounds: impactMetal_light_003.ogg; sfx_80_rpg: blade_03.ogg | 0.7 | 2 | 0.03 |  | Ui | 0.03 |
| ui_toggle | ui/ui_toggle.wav | 1 | 0.08 | latch click | kenney_rpg_audio: metalLatch.ogg | 0.7 | 2 | 0.03 |  | Ui | 0.03 |
| ui_tick | ui/ui_tick.wav | 1 | 0.02 | tiny tick | kenney_interface_sounds: tick_002.ogg | 0.5 | 2 | 0.04 |  | Ui | 0.03 |
| card_flip | ui/card_flip_1..3.wav | 3 | 0.13-0.13 | paper flip + air swish | kenney_rpg_audio: bookFlip3.ogg; sfx_swishes: swish-10.wav, swish-12.wav, swish-13.wav | 0.7 | 3 | 0.03 |  | Ui | 0.06 |
| card_select | ui/card_select.wav | 1 | 0.29 | upward whoosh + tick | kenney_interface_sounds: select_007.ogg | 0.75 | 2 | 0.03 |  | Ui | 0.03 |
| card_reroll | ui/card_reroll.wav | 1 | 0.39 | card riffle + four quick paper flicks | kenney_rpg_audio: bookFlip1.ogg; sfx_swishes: swish-13.wav | 0.8 | 2 | 0.03 |  | Ui | 0.03 |
| card_banish | ui/card_banish.wav | 1 | 0.43 | torn paper scratch + low burn whoomp + crackle | kenney_interface_sounds: scratch_005.ogg | 0.8 | 2 | 0.03 |  | Ui | 0.03 |
| card_skip | ui/card_skip.wav | 1 | 0.24 | coin drop | sfx_rpg_sound_pack: coin3.wav | 0.75 | 2 | 0.03 |  | Ui | 0.03 |
| reel_tick | ui/reel_tick_1..3.wav | 3 | 0.03-0.03 | dry wooden click | kenney_impact_sounds: impactWood_light_001.ogg, impactWood_light_003.ogg, impactWood_light_004.ogg | 0.55 | 2 | 0.06 |  | Ui | 0.04 |
| reel_land | ui/reel_land_1..2.wav | 2 | 0.31-0.31 | wooden thunk + small chime | kenney_impact_sounds: impactWood_heavy_001.ogg, impactWood_heavy_003.ogg | 0.8 | 2 | 0.03 |  | Ui | 0.03 |
| reel_land_evolution | ui/reel_land_evolution.wav | 1 | 1.20 | wooden thunk + rising D major choir chord + chime | kenney_impact_sounds: impactWood_heavy_001.ogg | 0.95 | 2 | 0.03 |  | Ui | 0.03 |
| chest_gold | ui/chest_gold.wav | 1 | 0.47 | coin cascade | kenney_rpg_audio: handleCoins.ogg; sfx_80_rpg: item_coins_04.ogg | 0.75 | 2 | 0.03 |  | Ui | 0.03 |
| buy | ui/buy_1..2.wav | 2 | 0.50-0.50 | anvil strike (heavy metal hit + ring) + coins | kenney_impact_sounds: impactMetal_heavy_001.ogg, impactMetal_heavy_003.ogg; sfx_80_rpg: item_coins_01.ogg, item_coins_03.ogg | 0.8 | 2 | 0.03 |  | Ui | 0.03 |
| refund | ui/refund.wav | 1 | 0.62 | coin pour | kenney_rpg_audio: handleCoins.ogg; sfx_80_rpg: item_coins_04.ogg | 0.8 | 2 | 0.03 |  | Ui | 0.03 |
| banner | ui/banner.wav | 1 | 1.30 | distant horn call (D3/A3, wet hall) | generated | 0.75 | 2 | 0.03 |  | Ui | 0.03 |
| banner_warning | ui/banner_warning.wav | 1 | 1.60 | low dissonant brass swell + church bell D3 at 0.6 s | generated | 0.9 | 2 | 0.03 |  | Ui | 0.03 |
| danger_warning | ui/danger_warning.wav | 1 | 1.60 | rumble rising over 1.2 s + low drone + rising dark whoosh + tritone stab | generated | 0.85 | 1 | 0.5 |  | Sfx | 0.0 |
| boss_bar_appear | ui/boss_bar_appear.wav | 1 | 0.90 | deep drum hit + sub | kenney_impact_sounds: impactPunch_heavy_002.ogg, impactSoft_heavy_001.ogg | 0.9 | 2 | 0.03 |  | Ui | 0.03 |
| boss_bar_shatter | ui/boss_bar_shatter.wav | 1 | 0.64 | glass smash + bone break + sub + shard crackles | kenney_impact_sounds: impactGlass_heavy_000.ogg; sfx_bone_breaks: Deep Break 1.wav | 0.9 | 2 | 0.03 |  | Ui | 0.03 |
| results_victory | ui/results_victory.wav | 1 | 2.60 | D major choir (two voicings summed) + brass sting + major bell + sub | generated | 1.0 | 2 | 0.03 |  | Ui | 0.03 |
| results_defeat | ui/results_defeat.wav | 1 | 2.60 | low D minor strings (two voicings summed) + church bell D3 + sub | generated | 1.0 | 2 | 0.03 |  | Ui | 0.03 |
| results_tally | ui/results_tally.wav | 1 | 0.45 | coins + ding (D6) | sfx_80_rpg: item_coins_01.ogg | 0.8 | 2 | 0.03 |  | Ui | 0.03 |

### Knight

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| oathblade_swing | knight/oathblade_swing_1..3.wav | 3 | 0.15-0.21 | air swish + steel 'shing' ring tail (2-4 kHz) | sfx_80_rpg: blade_01.ogg, blade_02.ogg, blade_03.ogg; sfx_swishes: swish-3.wav, swish-7.wav, swish-9.wav | 0.75 | 3 | 0.08 |  | Sfx | 0.05 |
| oathblade_hit | knight/oathblade_hit_1..3.wav | 3 | 0.20-0.22 | low thump (160->110 Hz) + punch + bone crunch transient | kenney_impact_sounds: impactPunch_medium_000.ogg, impactPunch_medium_001.ogg, impactPunch_medium_002.ogg; sfx_bone_breaks: Deep Break 1.wav, Deep Break 6.wav, Deep Break 8.wav | 0.85 | 3 | 0.03 | yes | Sfx | 0.06 |
| dawn_crescent | knight/dawn_crescent_1..2.wav | 2 | 0.74-0.74 | rising holy whoosh + bell shimmer + faint choir | generated | 0.8 | 2 | 0.08 |  | Sfx | 0.06 |
| aegis_summon | knight/aegis_summon_1..2.wav | 2 | 0.59-0.60 | glass/bell tone (880 + 1320 Hz partials) swelling in | kenney_impact_sounds: impactPlate_light_000.ogg | 0.6 | 2 | 0.1 |  | Sfx | 0.03 |
| aegis_bonk | knight/aegis_bonk_1..4.wav | 4 | 0.16-0.19 | metal-wood 'bonk' (shield plate + wood) with a short ring | kenney_impact_sounds: impactPlate_medium_000.ogg, impactPlate_medium_001.ogg, impactPlate_medium_003.ogg, impactPlate_medium_004.ogg, impactWood_medium_000.ogg, impactWood_medium_001.ogg, impactWood_medium_002.ogg, impactWood_medium_003.ogg | 0.75 | 4 | 0.05 | yes | Sfx | 0.08 |
| aegis_block | knight/aegis_block_1..3.wav | 3 | 0.17-0.18 | crisp metallic ping + spark crackle | kenney_impact_sounds: impactMetal_light_001.ogg, impactMetal_light_003.ogg, impactMetal_light_004.ogg | 0.8 | 3 | 0.03 | yes | Sfx | 0.06 |
| verdict_summon | knight/verdict_summon_1..2.wav | 2 | 0.40-0.40 | short choir swell + rising whoosh into the slam | generated | 0.75 | 2 | 0.03 | yes | Sfx | 0.06 |
| verdict_slam | knight/verdict_slam_1..3.wav | 3 | 1.20-1.20 | deep bell (C2) + stone crack transient + punch + sub < 80 Hz thump, bell tail | kenney_impact_sounds: impactMining_000.ogg, impactMining_001.ogg, impactMining_002.ogg, impactPunch_heavy_000.ogg, impactPunch_heavy_002.ogg, impactPunch_heavy_004.ogg; sfx_80_rpg: stones_01.ogg, stones_02.ogg, stones_04.ogg | 1.0 | 3 | 0.03 | yes | Sfx | 0.06 |
| verdict_shockwave | knight/verdict_shockwave_1..2.wav | 2 | 0.90-0.90 | airy holy boom + outward whoosh + chime + sub | generated | 1.0 | 2 | 0.03 | yes | Sfx | 0.06 |
| shield_rush | knight/shield_rush_1..2.wav | 2 | 0.35-0.35 | heavy whoosh + armor rattle | sfx_rpg_sound_pack: chainmail1.wav, chainmail2.wav | 0.85 | 1 | 0.1 |  | Sfx | 0.06 |
| shield_rush_boom | knight/shield_rush_boom_1..2.wav | 2 | 0.52-0.52 | shield plate impact + low punch + sub thump | kenney_impact_sounds: impactPlate_heavy_001.ogg, impactPlate_heavy_003.ogg, impactPunch_heavy_002.ogg, impactPunch_heavy_004.ogg | 1.0 | 2 | 0.1 |  | Sfx | 0.06 |
| bulwark_clang | knight/bulwark_clang_1..3.wav | 3 | 0.32-0.32 | metal clang (1-4 kHz) + low thump (100 Hz, 150 ms) + bell ring | kenney_impact_sounds: impactBell_heavy_002.ogg, impactMetal_medium_000.ogg, impactMetal_medium_001.ogg, impactMetal_medium_003.ogg | 0.9 | 3 | 0.05 |  | Sfx | 0.06 |

### Barbarian

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| fury_max | barbarian/fury_max.wav | 1 | 1.00 | war drum hit + sub + war roar (voice) with a growl edge | kenney_impact_sounds: impactPunch_heavy_002.ogg, impactSoft_heavy_001.ogg; sfx_80_rpg: creature_roar_01.ogg; sfx_rpg_sound_pack: giant2.wav | 0.9 | 1 | 0.5 |  | Sfx | 0.03 |
| ravager_chop | barbarian/ravager_chop_1..3.wav | 3 | 0.34-0.35 | heavy swish (slowed) + low air whoosh + cloth | kenney_rpg_audio: cloth1.ogg; sfx_swishes: swish-3.wav, swish-7.wav, swish-8.wav | 0.8 | 3 | 0.05 |  | Sfx | 0.06 |
| ravager_hit | barbarian/ravager_hit_1..4.wav | 4 | 0.23-0.27 | wet axe chop + heavy punch + bone crack + sub | kenney_impact_sounds: impactPunch_heavy_000.ogg, impactPunch_heavy_001.ogg, impactPunch_heavy_003.ogg, impactPunch_heavy_004.ogg; kenney_rpg_audio: chop.ogg; sfx_bone_breaks: Deep Break 1.wav, Deep Break 4.wav, Deep Break 6.wav, Deep Break 8.wav | 0.95 | 3 | 0.03 | yes | Sfx | 0.06 |
| skullsplitter_crack | barbarian/skullsplitter_crack_1..2.wav | 2 | 0.63-0.63 | rock tearing crack + stones + magma hiss + crackle + sub | kenney_impact_sounds: impactMining_000.ogg, impactMining_003.ogg; sfx_80_rpg: stones_03.ogg | 1.0 | 2 | 0.05 | yes | Sfx | 0.06 |
| whirlwind_loop | barbarian/whirlwind_loop.wav | 1 | 3.50 | spinning blade whoosh: band noise swept per pass at 4.5 Hz + low whum + faint steel ring; seam crossfaded | generated | 0.75 | 1 | 0.0 |  | Sfx | 0.0 |
| whirlwind_stop | barbarian/whirlwind_stop_1..2.wav | 2 | 0.37-0.43 | axe ring + boot scuff + grunt | kenney_rpg_audio: footstep00.ogg, footstep03.ogg; sfx_80_rpg: metal_02.ogg, metal_03.ogg; sfx_rpg_sound_pack: giant3.wav, giant5.wav | 0.8 | 2 | 0.1 |  | Sfx | 0.06 |
| fault_stomp | barbarian/fault_stomp_1..2.wav | 2 | 0.41-0.41 | low ground thump (130->80 Hz) + boot impact + dust | kenney_impact_sounds: impactSoft_heavy_000.ogg, impactSoft_heavy_003.ogg | 0.9 | 2 | 0.05 |  | Sfx | 0.06 |
| fault_crack | barbarian/fault_crack_1..2.wav | 2 | 0.57-0.57 | rolling crackle climbing in pitch (travels) + stones + rumble | sfx_80_rpg: stones_01.ogg, stones_03.ogg | 0.9 | 4 | 0.05 | yes | Sfx | 0.06 |
| leap_takeoff | barbarian/leap_takeoff_1..2.wav | 2 | 0.39-0.40 | grunt + rising whoosh | sfx_rpg_sound_pack: giant1.wav, giant5.wav | 0.85 | 1 | 0.2 |  | Sfx | 0.06 |
| leap_land | barbarian/leap_land_1..2.wav | 2 | 1.03-1.03 | low boom + sub < 60 Hz + rock crack + crumbling stones tail | kenney_impact_sounds: impactMining_002.ogg, impactMining_004.ogg; kenney_scifi_sounds: lowFrequency_explosion_000.ogg, lowFrequency_explosion_001.ogg; sfx_80_rpg: stones_01.ogg, stones_02.ogg | 1.0 | 1 | 0.2 |  | Sfx | 0.06 |

### Mage

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ember_whoosh | mage/ember_whoosh_1..3.wav | 3 | 0.30-0.30 | fire 'fwoomp' (closing noise burst + flickering roar) + fire spell texture | sfx_80_rpg: spell_fire_01.ogg, spell_fire_06.ogg, spell_fire_07.ogg | 0.7 | 4 | 0.04 |  | Sfx | 0.06 |
| ember_boom | mage/ember_boom_1..4.wav | 4 | 0.46-0.51 | crack transient + 150 ms body boom + explosion crunch + crackle tail + sub | kenney_scifi_sounds: explosionCrunch_000.ogg, explosionCrunch_001.ogg, explosionCrunch_002.ogg, explosionCrunch_003.ogg | 0.95 | 4 | 0.04 | yes | Sfx | 0.06 |
| starfall_whistle | mage/starfall_whistle_1..2.wav | 2 | 0.59-0.59 | whistle rising over 0.6 s (sine + band noise) | generated | 0.7 | 4 | 0.05 | yes | Sfx | 0.06 |
| starfall_impact | mage/starfall_impact_1..3.wav | 3 | 0.82-0.83 | air boom + sub + rock crack + debris + crackles | kenney_impact_sounds: impactMining_001.ogg, impactMining_003.ogg, impactMining_004.ogg; sfx_80_rpg: stones_01.ogg, stones_02.ogg, stones_04.ogg | 1.0 | 4 | 0.05 | yes | Sfx | 0.06 |
| cataclysm_whistle | mage/cataclysm_whistle.wav | 1 | 0.75 | deep whistle rising over 0.9 s + rumble | generated | 0.85 | 1 | 0.1 | yes | Sfx | 0.03 |
| cataclysm_impact | mage/cataclysm_impact.wav | 1 | 2.00 | massive: crack + closing air blast + deep boom + low explosion + fire roar + debris rain + sub < 60 Hz | kenney_scifi_sounds: lowFrequency_explosion_000.ogg | 1.0 | 1 | 0.1 | yes | Sfx | 0.04 |
| rime_nova | mage/rime_nova_1..3.wav | 3 | 0.52-0.52 | glassy crackle sweep (ice crystallizing) + airy whoosh + high glass shimmer | kenney_interface_sounds: glass_002.ogg, glass_003.ogg, glass_005.ogg | 0.85 | 3 | 0.05 |  | Sfx | 0.06 |
| frost_shatter | mage/frost_shatter_1..4.wav | 4 | 0.21-0.22 | ice shatter (glass hit + tinkle) + fine crackle | kenney_impact_sounds: impactGlass_heavy_000.ogg, impactGlass_heavy_001.ogg, impactGlass_heavy_002.ogg, impactGlass_heavy_003.ogg, impactGlass_light_000.ogg, impactGlass_light_001.ogg, impactGlass_light_002.ogg, impactGlass_light_003.ogg | 0.8 | 3 | 0.04 | yes | Sfx | 0.06 |
| arcane_echo | mage/arcane_echo_1..2.wav | 2 | 0.34-0.34 | reversed chime swelling into a click | kenney_interface_sounds: click_003.ogg | 0.6 | 2 | 0.08 |  | Sfx | 0.03 |
| blink | mage/blink_1..2.wav | 2 | 0.24-0.24 | implosion: reverse-shaped whoosh into a pop | generated | 0.8 | 1 | 0.1 |  | Sfx | 0.06 |
| blink_rift | mage/blink_rift_1..2.wav | 2 | 0.60-0.60 | violet boom + sub + glassy overtone dyad + spell texture | sfx_80_rpg: spell_02.ogg | 0.95 | 2 | 0.1 | yes | Sfx | 0.06 |

### Rogue

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| momentum_max | rogue/momentum_max.wav | 1 | 0.75 | wind gust: wide band whoosh + faint whistle | generated | 0.7 | 1 | 0.5 |  | Sfx | 0.03 |
| crossbow_twang | rogue/crossbow_twang_1..4.wav | 4 | 0.20-0.20 | tight string twang (plucked, A2) + bolt hiss + trigger click | kenney_rpg_audio: metalLatch.ogg | 0.7 | 4 | 0.04 |  | Sfx | 0.05 |
| widow_ricochet | rogue/widow_ricochet_1..3.wav | 3 | 0.26-0.26 | high metal ping + falling ricochet whizz | kenney_impact_sounds: impactMetal_light_001.ogg, impactMetal_light_002.ogg, impactMetal_light_004.ogg | 0.85 | 3 | 0.04 | yes | Sfx | 0.06 |
| knife_fan | rogue/knife_fan_1..3.wav | 3 | 0.27-0.27 | four quick blade swishes 35 ms apart + knife slice | kenney_rpg_audio: knifeSlice2.ogg; sfx_swishes: swish-10.wav, swish-11.wav, swish-12.wav, swish-13.wav, swish-2.wav, swish-5.wav | 0.8 | 3 | 0.05 |  | Sfx | 0.06 |
| mark_apply | rogue/mark_apply_1..2.wav | 2 | 0.57-0.57 | whisper (shaped breath + ghost voice) + heartbeat thump | sfx_rpg_sound_pack: shade1.wav, shade14.wav | 0.8 | 2 | 0.08 | yes | Sfx | 0.06 |
| mark_stab | rogue/mark_stab_1..3.wav | 3 | 0.23-0.24 | clean blade stab + small punch + smoky whoosh | kenney_impact_sounds: impactPunch_medium_000.ogg, impactPunch_medium_001.ogg, impactPunch_medium_002.ogg; kenney_rpg_audio: knifeSlice.ogg, knifeSlice2.ogg | 0.9 | 3 | 0.03 | yes | Sfx | 0.06 |
| mark_daggers | rogue/mark_daggers_1..2.wav | 2 | 0.48-0.48 | falling hiss + blade swish | sfx_swishes: swish-12.wav | 0.85 | 2 | 0.08 | yes | Sfx | 0.06 |
| execute | rogue/execute_1..2.wav | 2 | 0.60-0.60 | heavy slash + bell toll cut short + bone break + sub | kenney_rpg_audio: knifeSlice2.ogg; sfx_bone_breaks: Deep Break 6.wav; sfx_swishes: swish-7.wav | 1.0 | 2 | 0.08 | yes | Sfx | 0.06 |
| smoke_poof | rogue/smoke_poof_1..2.wav | 2 | 0.40-0.40 | soft low 'poof' + hiss + cloth | kenney_rpg_audio: cloth1.ogg, cloth3.ogg | 0.8 | 2 | 0.1 |  | Sfx | 0.06 |
| roll_whoosh | rogue/roll_whoosh_1..2.wav | 2 | 0.35-0.35 | cloth rustle + whoosh | kenney_rpg_audio: cloth1.ogg, cloth3.ogg | 0.8 | 1 | 0.1 |  | Sfx | 0.06 |

### Shared weapons

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| spirit_dart | shared/spirit_dart_1..4.wav | 4 | 0.25-0.25 | soft harp-like pluck (D5, pentatonic set) + air | generated | 0.45 | 4 | 0.04 |  | Sfx | 0.015 |
| disc_throw | shared/disc_throw_1..2.wav | 2 | 0.30-0.30 | whirring throw: band noise chopped at 18-22 Hz + whoosh | generated | 0.7 | 2 | 0.05 |  | Sfx | 0.06 |
| disc_ping | shared/disc_ping_1..2.wav | 2 | 0.25-0.25 | tuned metallic ping (E6/D6 chime) + metal tick; the script climbs a semitone per bounce | kenney_impact_sounds: impactMetal_light_002.ogg | 0.8 | 3 | 0.03 | yes | Sfx | 0.02 |
| bramble_rustle | shared/bramble_rustle_1..3.wav | 3 | 0.38-0.42 | dry grass rustle + twig crackles + wood creak | kenney_impact_sounds: footstep_grass_000.ogg, footstep_grass_002.ogg, footstep_grass_004.ogg; sfx_80_rpg: wood_04.ogg | 0.55 | 1 | 0.1 |  | Sfx | 0.06 |
| javelin_throw | shared/javelin_throw_1..2.wav | 2 | 0.46-0.47 | heavy swish (slowed) + falling whoosh | sfx_swishes: swish-7.wav, swish-9.wav | 0.8 | 2 | 0.05 |  | Sfx | 0.06 |
| godspear_boom | shared/godspear_boom_1..2.wav | 2 | 1.00-1.00 | holy explosion: air boom + sub + choir burst + major bell | generated | 1.0 | 2 | 0.05 | yes | Sfx | 0.06 |
| flask_throw | shared/flask_throw_1..2.wav | 2 | 0.17-0.17 | swish + glass clink + bottle slosh | kenney_interface_sounds: glass_005.ogg, glass_006.ogg; sfx_rpg_sound_pack: bottle.wav; sfx_swishes: swish-5.wav, swish-6.wav | 0.65 | 2 | 0.05 |  | Sfx | 0.06 |
| flask_shatter | shared/flask_shatter_1..3.wav | 3 | 0.60-0.60 | glass shatter + 'whoomp' ignition + fire crackle | kenney_impact_sounds: impactGlass_heavy_001.ogg, impactGlass_heavy_002.ogg, impactGlass_heavy_004.ogg | 0.9 | 3 | 0.05 | yes | Sfx | 0.06 |
| totem_place | shared/totem_place_1..2.wav | 2 | 0.45-0.45 | wooden thunk + static buzz hum starting | kenney_impact_sounds: impactWood_heavy_000.ogg, impactWood_heavy_002.ogg | 0.8 | 2 | 0.1 | yes | Sfx | 0.06 |
| totem_zap | shared/totem_zap_1..4.wav | 4 | 0.22-0.23 | electric snap + falling buzz + sizzle crackle | generated | 0.75 | 4 | 0.04 | yes | Sfx | 0.06 |
| wisp_whistle | shared/wisp_whistle_1..3.wav | 3 | 0.39-0.40 | falling whistle (sine + band noise) | generated | 0.6 | 3 | 0.05 | yes | Sfx | 0.06 |
| wisp_splash | shared/wisp_splash_1..3.wav | 3 | 0.40-0.40 | airy glass chime + soft bright burst | generated | 0.7 | 3 | 0.05 | yes | Sfx | 0.06 |

### Ambience

| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |
|---|---|---|---|---|---|---|---|---|---|---|---|
| moor_wind | ambience/moor_wind.wav | 1 | 5.00 | moor wind bed: gusting low-passed noise + faint howl; 5 s seamless loop | generated | - | - | - |  | - | - |

### Aliases (same clips and rules as the original)

bone_break -> enemy_death, skeleton_rise -> summon, hex_summon -> summon, hex_ward -> ward, archer_draw -> bolt_draw, archer_fire -> bolt_fire, sword_swing -> oathblade_swing, sword_hit -> oathblade_hit, shield_bash -> shield_rush_boom, axe_swing -> ravager_chop, axe_hit -> ravager_hit, ground_slam -> leap_land, war_roar -> fury_max, fire_cast -> ember_whoosh, fire_whoosh -> ember_whoosh, fire_explosion -> ember_boom, frost_cast -> rime_nova, lightning_crack -> totem_zap, arcane_bolt -> blink, crossbow_fire -> crossbow_twang, knife_throw -> knife_fan, knife_hit -> mark_stab, shadow_whoosh -> roll_whoosh, bell_chime -> active_ready, boss_roar -> king_roar, boss_slam -> king_slam_hit, boss_metal_scrape -> king_cleave_sting, boss_inhale -> king_slam_sting, boss_choir_swell -> king_nova_sting, ui_buy -> buy

## 4. Music (MusicPlayer)

| Track | File | Length s | Peak dBFS | Loudest 3 s | Player volume | Made of |
|---|---|---|---|---|---|---|
| music_menu | music/menu_loop.flac | 52.439 | -1.0 | -16.2 | 0.5 | decoded; trailing digital silence cut, the 3.5 s fade-out tail overlapped onto the fade-in head (seamless), peak -1 dBFS, 16-bit dithered, lossless FLAC |
| music_battle | music/battle_loop.flac | 54.0 | -2.9 | -16.4 | 0.51-0.62 (intensity) | first 54.0 s of the 108 s loop, lossless FLAC (bit-exact through the engine's dr_flac); its last 14 s equal the track's end, so the wrap is the composer's own seam |
| music_boss | music/boss_loop.ogg | 108.0 | -1.0 | -14.6 | 0.55 | original Ogg Vorbis copied unchanged (seam step 0.026 is within the music's own sample steps) |
| victory_stinger | music/victory_stinger.wav | 1.8 | -1.0 | -12.7 | 0.85 | Kenney orchestral hit jingle (HIT15) + D major choir (wide) + major bell + sub |
| defeat_stinger | music/defeat_stinger.wav | 2.0 | -1.0 | -13.1 | 0.85 | Kenney descending pizzicato jingle (PIZZI07, F-E-D#-D) + low D minor strings falling to Bb + bell toll D2 + sub |
| moor_wind | ambience/moor_wind.wav | 5.0 | -14.57 | -26.0 | 0.45 (Sfx bus) | moor wind bed: gusting low-passed noise + faint howl; 5 s seamless loop |

- Loops play on 2D AudioSource entities created at play start under a "Music" entity (Music bus, loop, priority 100); track changes crossfade over 0.8 s; Victory / Defeat fade the loops out over 0.6 s and play their stinger once (Audio.PlayOneShot on the Music bus). The wind bed loops on the Sfx bus at 0.45 from the first track on.
- `MusicDriver` (script added to the Music entity) every frame, unscaled time: plays Menu whenever `run.phase == Menu` (the title has music without a core change), swells the battle track from 82% to 100% between 200 and 800 enemies alive and drops it to 70% under 15% hero health (stand-in for the 800 Hz low-pass), ducks the SFX bus -6 dB while a level-up or chest holds the game (level_up, chest_open, evolve, card_pick ride the Ui bus so they stay full), and plays `heartbeat` at 1 Hz under 30% health and 1.6 Hz under 15% (design 10.4).
- Loop seams: battle = first 54.000 s of the 108 s file; its last 14 s are sample-identical to the file's end, so the wrap is the composer's own seam (checked: residual -64 dB). Menu = the 3.5 s fade-out tail overlapped onto the fade-in head, trailing digital silence removed. Boss = original; the wrap step (0.026) is within the music's own sample steps. Wind bed and whirlwind loop are crossfaded internally (qa.py seam check).

## 5. Generated vs from packs

Fully generated (no pack audio): active_ready, archmage_field_sting, archmage_orb, banner, banner_warning, blink, boss_dazed, cataclysm_whistle, danger_warning, dawn_crescent, death_wave, disc_throw, evolve, godspear_boom, heartbeat, king_nova_sting, king_spin_sting, last_tide, momentum_max, moor_wind, phase_shockwave, results_defeat, results_victory, spirit_dart, starfall_whistle, totem_zap, verdict_shockwave, verdict_summon, whirlwind_loop, wisp_splash, wisp_whistle.

Generated layers used throughout (tools/dsp.py): sub-bass thumps, air booms (closing low-pass noise), whooshes (swept band noise), additive bells (church / chime / glass partials), choir and brass and string sections (detuned saws through formant or low-pass filters), plucked strings (Karplus-Strong: crossbow, bows, harp darts), crackles, reverb tails.

## 6. Licenses and credits (all CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/)

| Pack | Title | Author | URL | Used for |
|---|---|---|---|---|
| sfx_80_rpg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | blink_rift, bone_spike, boss_death, boss_rise, bramble_rustle, buy, chest_gold, chest_open, coin, elite_death, elite_horn, ember_whoosh, fault_crack, fury_max, gold_shower, grave_burst, hex_orb_cast, hit_crit, king_cleave_hit, king_cleave_sting, king_death, king_enrage, king_raise_sting, king_rise, king_roar, king_slam_hit, king_slam_sting, leap_land, oathblade_swing, refund, results_tally, skullsplitter_crack, starfall_impact, summon, ui_select, verdict_slam, warden_charge_sting, warden_charge_stop, warden_cleave_hit, whirlwind_stop |
| sfx_rpg_sound_pack | RPG Sound Pack | artisticdude | https://opengameart.org/content/rpg-sound-pack | archmage_spiral_sting, archmage_summon_sting, boss_rise, card_skip, coin, flask_throw, fury_max, heal, hero_hurt, hex_cast, king_cleave_sting, leap_takeoff, mark_apply, raise_chant, run_start, shield_rush, soul_nova_ring, warden_charge_sting, warden_cleave_sting, warden_guard_up, whirlwind_stop |
| sfx_swishes | Swishes Sound Pack | artisticdude | https://opengameart.org/content/swishes-sound-pack | bolt_fire, card_flip, card_reroll, execute, flask_throw, javelin_throw, king_spin_whoosh, knife_fan, mark_daggers, oathblade_swing, ravager_chop |
| sfx_bone_breaks | Deep Bone Crack/Break SFX | Zane Little Music | https://opengameart.org/content/deep-bone-crackbreak-sfx | bone_rain_impact, bone_spike, boss_bar_shatter, boss_death, boss_rise, elite_death, enemy_death, execute, hit, king_phase_break, oathblade_hit, ravager_hit |
| sfx_bones_rattle | Bones rattle | congusbongus | https://opengameart.org/content/bones-rattle | bone_rain_impact, boss_death, enemy_death, grave_burst, hit, hit_crit, king_death, king_rain_sting, king_rise, king_slam_sting, raise_chant, ring_rattle, rush_rumble, skeleton_rattle, summon |
| sfx_gem | Gem collect SFX | Bobjt | https://opengameart.org/content/gem-collect-sfx | gem, level_up |
| sfx_soundpack01 | Level up, power up, Coin get (13 Sounds) | wobbleboxx | https://opengameart.org/content/level-up-power-up-coin-get-13-sounds | level_up, lodestone, revive |
| kenney_impact_sounds | Impact Sounds | Kenney (kenney.nl) | https://kenney.nl/assets/impact-sounds | aegis_block, aegis_bonk, aegis_summon, bell_toll, bone_rain_impact, boss_bar_appear, boss_bar_shatter, boss_chest_drop, boss_death, bramble_rustle, bulwark_clang, buy, disc_ping, elite_horn, enemy_death, fault_stomp, flask_shatter, frost_hourglass, frost_shatter, fury_max, grave_burst, guard_tink, hero_hurt, hit, hit_crit, holy_bell, king_cleave_hit, king_phase_break, king_rise, king_slam_hit, lantern_break, leap_land, mark_stab, oathblade_hit, ravager_hit, reel_land, reel_land_evolution, reel_tick, run_start, rush_rumble, shield_rush_boom, skullsplitter_crack, starfall_impact, summon, totem_place, ui_back, ui_click, ui_error, ui_hover, ui_select, verdict_slam, warden_charge_go, warden_charge_hit, warden_cleave_hit, warden_guard_up, widow_ricochet |
| kenney_interface_sounds | Interface Sounds | Kenney (kenney.nl) | https://kenney.nl/assets/interface-sounds | arcane_echo, archmage_blink, card_banish, card_pick, card_select, flask_throw, frost_hourglass, lantern_break, rime_nova, ui_back, ui_click, ui_confirm, ui_tick |
| kenney_scifi_sounds | Sci-fi Sounds | Kenney (kenney.nl) | https://kenney.nl/assets/sci-fi-sounds | cataclysm_impact, cinder_blast, cinder_fuse, ember_boom, leap_land, lodestone, rush_rumble, summon, ward |
| kenney_music_jingles | Music Jingles | Kenney (kenney.nl) | https://kenney.nl/assets/music-jingles | defeat_stinger, victory_stinger |
| kenney_rpg_audio | RPG Audio | Kenney (kenney.nl) | https://kenney.nl/assets/rpg-audio | bolt_draw, boss_chest_drop, card_flip, card_reroll, chest_gold, chest_open, crossbow_twang, execute, gold_shower, knife_fan, mark_stab, ravager_chop, ravager_hit, refund, roll_whoosh, smoke_poof, ui_toggle, warden_charge_go, warden_guard_up, whirlwind_stop |
| music_battle | Determined Pursuit (epic orchestra loop) | Emma_MA | https://opengameart.org/content/determined-pursuit-epic-orchestra-loop | music_battle |
| music_boss | Heavy Boss Battle 1 | MintoDog | https://opengameart.org/content/heavy-boss-battle-1 | music_boss |
| music_menu | Dark Shrine Loop | qubodup | https://opengameart.org/content/dark-shrine-loop | music_menu |

CC0 needs no attribution; the credits are listed as a courtesy (a credits screen may reuse this table).


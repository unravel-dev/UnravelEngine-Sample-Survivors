"""Writes survivors/manifest_audio.md from records.json (build output) and SoundCatalog.cs (routing)."""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dsp  # noqa: E402

SURV = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
AUDIO = os.path.join(SURV, "..", "data", "Audio")
records = json.load(open(os.path.join(SURV, "audio_work", "records.json"), encoding="utf-8"))
catalog = open(os.path.join(SURV, "..", "data", "Scripts", "Audio", "SoundCatalog.cs"), encoding="utf-8").read()

route = {}
for m in re.finditer(r'c\.Add\("(\w+)", "(\w+)", (\d+), ([\d.]+)f, (\d+), ([\w.]+), (true|false), AudioBus\.(\w+), (\w+)(?:, ([\d.]+)f)?\)', catalog):
    jitter = m.group(10) or "0.06"
    interval = {"RETRIGGER": "0.03", "STING_GAP": "0.5"}.get(m.group(6), m.group(6).rstrip("f"))
    route[m.group(2)] = dict(vol=m.group(4), limit=m.group(5), gap=interval, spatial=m.group(7) == "true", bus=m.group(8), jitter=jitter)
for m in re.finditer(r'c\.Boss\("(\w+)", (\d+), ([\d.]+)f(?:, (\d+))?\)', catalog):
    route[m.group(1)] = dict(vol=m.group(3), limit=m.group(4) or "1", gap="0.1", spatial=False, bus="Sfx", jitter="0.03")
for m in re.finditer(r'c\.Ui\("(\w+)", (\d+), ([\d.]+)f\)', catalog):
    route[m.group(1)] = dict(vol=m.group(3), limit="2", gap="0.03", spatial=False, bus="Ui", jitter="0.03")
aliases = re.findall(r'c\.Alias\("(\w+)", "(\w+)"\)', catalog)

by_name = {}
for rel, rec in records.items():
    by_name.setdefault(rec["name"], []).append(rec)

total = sum(os.path.getsize(os.path.join(r, f)) for r, _, fs in os.walk(AUDIO) for f in fs)
count = sum(len(fs) for _, _, fs in os.walk(AUDIO))
music_bytes = sum(os.path.getsize(os.path.join(AUDIO, "music", f)) for f in os.listdir(os.path.join(AUDIO, "music")))

GROUPS = [("Combat (core)", "combat"), ("Hero and core events", "hero"), ("Pickups", "pickups"), ("Director events", "director"),
          ("Enemies", "enemies"), ("Bosses (manifest_bosses.md)", "boss"), ("UI (manifest_ui.md)", "ui"), ("Knight", "knight"),
          ("Barbarian", "barbarian"), ("Mage", "mage"), ("Rogue", "rogue"), ("Shared weapons", "shared"), ("Ambience", "ambience")]


def short_sources(recs):
    items = sorted({s for rec in recs for s in rec["sources"]})
    if not items:
        return "generated"
    packs = {}
    for s in items:
        pack, _, file = s.partition(":")
        packs.setdefault(pack, []).append(file)
    return "; ".join(f"{p}: {', '.join(sorted(set(f)))}" for p, f in sorted(packs.items()))


lines = []
w = lines.append
w("# Barrowlight - audio module manifest")
w("")
w(f"Library: `S\\src\\Audio\\` -> import as `app:/data/Audio/...` (keys `app:/data/Audio/<folder>/<name>[_N].wav`). "
  f"{count} files, {total / 1048576:.2f} MB ({music_bytes / 1048576:.2f} MB music). Built by "
  "`S\\audio_work\\tools\\build_audio.py` (numpy only; Ogg sources decoded with the engine's own stb_vorbis through "
  "`oggdec.exe`), checked by `qa.py` (format, edges, DC, peak, loop seams, sub-bass of heavy hits) and `verify_catalog.py` "
  "(every catalog key exists, every name the scripts play and every manifest id is registered, every file is referenced).")
w("")
w("## 1. Formats and processing")
w("")
w("- SFX: 16-bit PCM WAV, 44.1 kHz, mono; stereo only for level_up, evolve, ring_rattle (bones circling) and the two music stingers.")
w("- Every SFX: DC + rumble removed (28 Hz 4th-order high-pass), leading silence cut, tail cut once its 10 ms RMS stays 42 dB under "
  "the peak (cosine fade over 10% of the length), 3 ms in / 5 ms out fades, sample peak -1 dBFS, then loudness brought to the "
  "category target below (attenuated when louder; a look-ahead limiter adds up to the category's push when quieter), length "
  "capped per category / per sound, TPDF-dithered to 16 bit. No file peaks above -1 dBFS; no file starts or ends off zero.")
w("- Layering: impacts are transient (0-30 ms: bone click, crack, metal tick) + body (50-200 ms punch / thump) + tail (ring, "
  "debris, reverb); heavy weapons and boss hits add a sub-bass sine thump under 80 Hz (`sub_boom`, falling 90 -> 30 Hz).")
w("- Music: `battle_loop.flac` and `menu_loop.flac` are lossless FLAC (the engine imports .flac; encoder in "
  "`audio_work\\tools\\flac.py`, each file verified bit-exact through the engine's own dr_flac decoder at build time). "
  "`boss_loop.ogg` is the original Ogg copied unchanged. As 16-bit WAV the two loops would add 9.6 MB and push the library "
  "past 30 MB.")
w("")
w("## 2. Loudness by category (momentary loudness, K-weighted, 100 ms window, dB LUFS-like)")
w("")
w("| Category | Target | Max limiter push | Length cap s | Measured (min .. max) | Files |")
w("|---|---|---|---|---|---|")
import build_audio  # noqa: E402
for cat, (target, push) in dsp.TARGETS.items():
    recs = [r for r in records.values() if r["category"] == cat]
    if not recs:
        continue
    lo = min(r["loudness"] for r in recs)
    hi = max(r["loudness"] for r in recs)
    w(f"| {cat} | {target:.0f} | +{push:.0f} dB | {build_audio.MAX_SECONDS[cat]} | {lo:.1f} .. {hi:.1f} | {len(recs)} |")
w("")
w("Categories: impact_heavy = heavy weapon / boss hits; impact = hits, deaths, light weapon impacts; weapon = swings, casts, "
  "launches; enemy = enemy telegraphs and casts; voice = hero grunts; stinger = events, boss stings, big pickups; pickup = gems, "
  "coins, heal; ui / ui_tick = menus; loop = whirlwind; ambience = wind bed. Short clicks read lower than their category on a "
  "100 ms meter. The volumes in SoundCatalog trim this: hits 0.5, deaths 0.6, gem 0.45, coins 0.5, weapon fires 0.6-0.85, "
  "heavy hits / stingers / bosses 0.9-1.0, UI 0.5-0.8; spatial sounds sit a little higher because the listener is ~21 m away.")
w("")
w("## 3. Sounds (name -> files)")
w("")
w("Routing columns: vol = SoundDef volume, lim = voices of the sound at once (split over its variants per clip), gap = "
  "minimum retrigger s, 3D = spatial (6-60 m), jit = pitch jitter +-.")
for title, folder in GROUPS:
    names = [n for n, recs in by_name.items() if recs[0]["file"].startswith(folder + "/")]
    if not names:
        continue
    w("")
    w(f"### {title}")
    w("")
    w("| Name | Files | Var | Length s | Made of | Sources | vol | lim | gap | 3D | bus | jit |")
    w("|---|---|---|---|---|---|---|---|---|---|---|---|")
    for name in names:
        recs = sorted(by_name[name], key=lambda r: r["file"])
        files = recs[0]["file"] if len(recs) == 1 else recs[0]["file"].rsplit("_", 1)[0] + f"_1..{len(recs)}.wav"
        secs = [r["seconds"] for r in recs]
        length = f"{secs[0]:.2f}" if len(recs) == 1 else f"{min(secs):.2f}-{max(secs):.2f}"
        if recs[0]["channels"] == 2:
            length += " st"
        rt = route.get(name, {})
        w(f"| {name} | {files} | {len(recs)} | {length} | {recs[0]['desc']} | {short_sources(recs)} | {rt.get('vol', '-')} | "
          f"{rt.get('limit', '-')} | {rt.get('gap', '-')} | {'yes' if rt.get('spatial') else ''} | {rt.get('bus', '-')} | {rt.get('jitter', '-')} |")
w("")
w("### Aliases (same clips and rules as the original)")
w("")
w(", ".join(f"{a} -> {o}" for a, o in aliases))
w("")
w("## 4. Music (MusicPlayer)")
w("")
w("| Track | File | Length s | Peak dBFS | Loudest 3 s | Player volume | Made of |")
w("|---|---|---|---|---|---|---|")
vols = {"music_menu": "0.5", "music_battle": "0.51-0.62 (intensity)", "music_boss": "0.55", "victory_stinger": "0.85", "defeat_stinger": "0.85"}
for key in ("music/menu_loop.flac", "music/battle_loop.flac", "music/boss_loop.ogg", "music/victory_stinger.wav", "music/defeat_stinger.wav", "ambience/moor_wind.wav"):
    rec = records[key]
    w(f"| {rec['name']} | {key} | {rec['seconds']} | {rec['peak_db']} | {rec['loudness']} | {vols.get(rec['name'], '0.45 (Sfx bus)')} | {rec['desc']} |")
w("")
w("- Loops play on 2D AudioSource entities created at play start under a \"Music\" entity (Music bus, loop, priority 100); "
  "track changes crossfade over 0.8 s; Victory / Defeat fade the loops out over 0.6 s and play their stinger once "
  "(Audio.PlayOneShot on the Music bus). The wind bed loops on the Sfx bus at 0.45 from the first track on.")
w("- `MusicDriver` (script added to the Music entity) every frame, unscaled time: plays Menu whenever `run.phase == Menu` "
  "(the title has music without a core change), swells the battle track from 82% to 100% between 200 and 800 enemies alive "
  "and drops it to 70% under 15% hero health (stand-in for the 800 Hz low-pass), ducks the SFX bus -6 dB while a level-up "
  "or chest holds the game (level_up, chest_open, evolve, card_pick ride the Ui bus so they stay full), and plays "
  "`heartbeat` at 1 Hz under 30% health and 1.6 Hz under 15% (design 10.4).")
w("- Loop seams: battle = first 54.000 s of the 108 s file; its last 14 s are sample-identical to the file's end, so the wrap "
  "is the composer's own seam (checked: residual -64 dB). Menu = the 3.5 s fade-out tail overlapped onto the fade-in head, "
  "trailing digital silence removed. Boss = original; the wrap step (0.026) is within the music's own sample steps. Wind bed "
  "and whirlwind loop are crossfaded internally (qa.py seam check).")
w("")
w("## 5. Generated vs from packs")
w("")
gen = sorted(n for n, recs in by_name.items() if all(not r["sources"] for r in recs))
w(f"Fully generated (no pack audio): {', '.join(gen)}.")
w("")
w("Generated layers used throughout (tools/dsp.py): sub-bass thumps, air booms (closing low-pass noise), whooshes (swept "
  "band noise), additive bells (church / chime / glass partials), choir and brass and string sections (detuned saws through "
  "formant or low-pass filters), plucked strings (Karplus-Strong: crossbow, bows, harp darts), crackles, reverb tails.")
w("")
w("## 6. Licenses and credits (all CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/)")
w("")
w("| Pack | Title | Author | URL | Used for |")
w("|---|---|---|---|---|")
packs = [("sfx_80_rpg", "80 CC0 RPG SFX", "rubberduck", "https://opengameart.org/content/80-cc0-rpg-sfx"),
         ("sfx_rpg_sound_pack", "RPG Sound Pack", "artisticdude", "https://opengameart.org/content/rpg-sound-pack"),
         ("sfx_swishes", "Swishes Sound Pack", "artisticdude", "https://opengameart.org/content/swishes-sound-pack"),
         ("sfx_bone_breaks", "Deep Bone Crack/Break SFX", "Zane Little Music", "https://opengameart.org/content/deep-bone-crackbreak-sfx"),
         ("sfx_bones_rattle", "Bones rattle", "congusbongus", "https://opengameart.org/content/bones-rattle"),
         ("sfx_gem", "Gem collect SFX", "Bobjt", "https://opengameart.org/content/gem-collect-sfx"),
         ("sfx_soundpack01", "Level up, power up, Coin get (13 Sounds)", "wobbleboxx", "https://opengameart.org/content/level-up-power-up-coin-get-13-sounds"),
         ("kenney_impact_sounds", "Impact Sounds", "Kenney (kenney.nl)", "https://kenney.nl/assets/impact-sounds"),
         ("kenney_interface_sounds", "Interface Sounds", "Kenney (kenney.nl)", "https://kenney.nl/assets/interface-sounds"),
         ("kenney_scifi_sounds", "Sci-fi Sounds", "Kenney (kenney.nl)", "https://kenney.nl/assets/sci-fi-sounds"),
         ("kenney_music_jingles", "Music Jingles", "Kenney (kenney.nl)", "https://kenney.nl/assets/music-jingles"),
         ("kenney_rpg_audio", "RPG Audio", "Kenney (kenney.nl)", "https://kenney.nl/assets/rpg-audio"),
         ("music_battle", "Determined Pursuit (epic orchestra loop)", "Emma_MA", "https://opengameart.org/content/determined-pursuit-epic-orchestra-loop"),
         ("music_boss", "Heavy Boss Battle 1", "MintoDog", "https://opengameart.org/content/heavy-boss-battle-1"),
         ("music_menu", "Dark Shrine Loop", "qubodup", "https://opengameart.org/content/dark-shrine-loop")]
for pack, title, author, url in packs:
    users = sorted({r["name"] for r in records.values() if any(s.startswith(pack + ":") for s in r["sources"])})
    w(f"| {pack} | {title} | {author} | {url} | {', '.join(users) if users else '(not used)'} |")
w("")
w("CC0 needs no attribution; the credits are listed as a courtesy (a credits screen may reuse this table).")
w("")
with open(os.path.join(SURV, "manifest_audio.md"), "w", encoding="ascii", errors="strict", newline="\n") as f:
    f.write("\n".join(lines) + "\n")
print("manifest_audio.md written:", len(lines), "lines")

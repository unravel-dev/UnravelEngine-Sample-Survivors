# Survivors sample - free asset sources

Researched 2026-10-28. Nothing was downloaded into the workspace. Sizes come from HTTP HEAD
`Content-Length`, from the host page (OpenGameArt `length=` attribute, itch.io file list,
Google Drive file page), or from a headers-only probe (see "Verification log"). Every pack
below is CC0, CC-BY or OFL. Anything NC, ND, "no redistribution" or unclear was rejected
(see "Rejected").

## Key findings first

1. **KayKit rig (from the local files).** `Knight.glb` (Adventurers 1.0) has 1 skin with 41
   joints: `root, hips, spine, chest, upperarm.l/.r, lowerarm.l/.r, wrist.l/.r, hand.l/.r,
   handslot.l/.r, head, upperleg/lowerleg/foot/toes .l/.r` and IK/control helpers
   (`kneeIK.l`, `IK-foot.l`, `handIK.r`, ...). It embeds 76 clips (`Idle`, `Walking_A`,
   `Running_A`, `1H_Melee_Attack_Chop`, `Hit_A`, `Death_A`, `Spellcast_Shoot`, ...). The
   character GLBs embed their texture in a bufferView, and the same PNG also sits next to each
   GLB (`knight_texture.png`, ...).
2. **The local "KayKit Character Animations 1.2" does not fit this rig.** Its
   `KayKit_AnimatedCharacter_v1.2.glb` has a 6-joint legacy rig (`Body, Head, armLeft,
   handSlotLeft, armRight, handSlotRight`, the old "Prototype Pete" figure) with 30 clips. It
   is the 2021 legacy pack from OpenGameArt, not the current one.
3. **There are two KayKit generations. Do not mix them.**
   - GitHub, 1.0 era: Adventurers 1.0 (your local copy) and Skeletons 1.0. Each character GLB
     embeds its own clips, so no separate animation pack is needed.
   - itch.io, current: Adventurers **2.0**, Skeletons **1.1** and Character Animations **1.1**
     (Dec 2025) all use **Rig_Medium**. The character files carry no clips; all clips come
     from Character Animations 1.1, including a "Special" skeleton set (`Skeletons_Walking`,
     `Skeletons_Awaken_*`, `Skeletons_Death`, `Skeletons_Taunt`, `Skeletons_Spawn_Ground`, per
     a third-party repo that ships them). These downloads are itch-only.
   - **Recommendation:** use GitHub Skeletons 1.0 next to your local Adventurers 1.0. You get
     direct URLs and every GLB carries its own clips.
4. **GitHub archive zips exclude FBX files and sample images.** Each KayKit repo's
   `.gitattributes` marks `Assets/fbx`, `Characters/fbx`, `Samples` and the top-level files
   `export-ignore`, so the archive is small. Proof: your local `kaykit_adventurers.zip`
   (6,141,833 bytes) holds exactly the 18,751,016 bytes that these rules keep.

## Recommended download list

"Attr" means attribution is legally required. CC0 items need none, but a CREDITS line is still
recommended. OFL fonts must ship with their `OFL.txt` (license notice, not an on-screen
credit).

| # | Name | URL | Size | License | Attr | Purpose |
|---|------|-----|------|---------|------|---------|
| 1 | KayKit Character Pack: Skeletons 1.0 (GitHub, pinned commit) | https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0/archive/15b62b9bad122f72926c10fb14d622c73819fa54.zip | ~6.4 MB zip (est.); 19,633,386 B unpacked | CC0 | n | Minion, Warrior, Rogue, Mage skeleton enemies + 13 weapons/shields (glTF) |
| 2 | Quaternius Ultimate Monsters - `Big/glTF/Demon.gltf` | https://drive.usercontent.google.com/download?id=1XhBLnR6tjqIrFy0AUfRlqKf-hYmVwIR4&export=download | 1,264,925 B | CC0 | n | Boss (animated, big) |
| 3 | Quaternius Ultimate Monsters - `Big/glTF/Atlas_Monsters.png` | https://drive.usercontent.google.com/download?id=1fX6w5W_wuiak8-Cm2ocef3VRzJdLzA-U&export=download | 9,007 B | CC0 | n | Shared palette texture for the boss |
| 4 | KayKit Halloween Bits 1.0 (GitHub) | https://github.com/KayKit-Game-Assets/KayKit-Halloween-Bits-1.0/archive/6dc69bf6b2fa766a985754f35ec6a0324090e6c6.zip | ~1.2 MB zip (est.); 3,269,808 B unpacked | CC0 | n | PRIMARY arena kit: graveyard (graves, crypt, fences, dead trees, lanterns, bones) |
| 5 | KayKit Dungeon Remastered 1.0 (GitHub) | https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0/archive/b0ca9bd96a8072ab36a3a5464f00ed1e06a16d07.zip | ~5 MB zip (est.); 17,013,515 B unpacked | CC0 | n | SECONDARY: ruined-crypt pieces (broken walls, pillars, rubble, torches, floors) |
| 6 | 80 CC0 RPG SFX (rubberduck) | https://opengameart.org/sites/default/files/80-CC0-RPG-SFX_0.zip | 1,845,114 B | CC0 | n | Creature die/hurt/roar, gem/coin items, blade, fire spell |
| 7 | RPG Sound Pack (artisticdude) | https://opengameart.org/sites/default/files/rpg_sound_pack.zip | 12,465,564 B | CC0 | n | Boss roars (giant/ogre/beast), swooshes, sword clashes |
| 8 | Swishes Sound Pack (artisticdude) | https://opengameart.org/sites/default/files/swishes.zip | 385,150 B | CC0 | n | Sword/axe swings, arrow whoosh (13 swishes) |
| 9 | Deep Bone Crack/Break SFX (Zane Little Music) | https://opengameart.org/sites/default/files/deep_breaks.zip | 2,610,684 B | CC0 | n | Bone crunch on skeleton hit/death (10 cracks) |
| 10 | Bones rattle (congusbongus) | https://opengameart.org/sites/default/files/bones_rattle.zip | 49,966 B | CC0 | n | Skeleton walk/collapse rattle (9 sounds) |
| 11 | Gem collect SFX (Bobjt) | https://opengameart.org/sites/default/files/gem-gather-stereo.wav | 50,828 B | CC0 | n | Gem pickup chime |
| 12 | Level up, power up, Coin get (wobbleboxx) | https://opengameart.org/sites/default/files/SoundPack01.zip | 6,780,792 B | CC0 | n | Level-up / power-up / pickup stingers (13) |
| 13 | Kenney Impact Sounds | https://kenney.nl/assets/impact-sounds (download button -> `kenney_impact-sounds.zip`) | 800,850 B | CC0 | n | Footsteps, metal/wood/punch hits (130 files) |
| 14 | Kenney Interface Sounds | https://kenney.nl/assets/interface-sounds (-> `kenney_interface-sounds.zip`) | 834,536 B | CC0 | n | UI clicks, confirm, error, open/close (100 files) |
| 15 | Kenney Sci-fi Sounds | https://kenney.nl/assets/sci-fi-sounds (-> `kenney_sci-fi-sounds.zip`) | 5,875,104 B | CC0 | n | Explosions (70 files: engines, explosions, lasers) |
| 16 | Kenney Music Jingles | https://kenney.nl/assets/music-jingles (-> `kenney_music-jingles.zip`) | 1,239,525 B | CC0 | n | Level-up fanfare, victory, game-over jingles (85) |
| 17 | Kenney RPG Audio | https://kenney.nl/assets/rpg-audio (-> `kenney_rpg-audio.zip`) | 964,837 B | CC0 | n | Footsteps, knife/blade, coins, cloth, metal (50) |
| 18 | Basic Spell Impacts (lentikula) | https://lentikula.itch.io/freecc0-basic-spell-impacts-sfx (itch only) | 13 MB (itch listing) | CC0 | n | Fire, ice, lightning, water spell impacts (5 each, 48 kHz/24-bit WAV) |
| 19 | Determined Pursuit (epic orchestra loop), Emma_MA | https://opengameart.org/sites/default/files/determined_pursuit_loop.wav | 19,052,830 B (WAV) | CC0 | n | Battle loop. About 1:48 (est., see Music) |
| 20 | Heavy Boss Battle 1, MintoDog | https://opengameart.org/sites/default/files/heavy_boss_battle_1_bpm200_0.ogg | 3,360,172 B (OGG) | CC0 | n | Boss loop, 200 BPM, "loopable" |
| 21 | Dark Shrine Loop, qubodup (remix of yd "Shrine") | https://opengameart.org/sites/default/files/qubodup-yd-DarkShrineLoop-OpenGameArt.ogg | 678,630 B (OGG) | CC0 | n | Menu loop, dark/calm/mystery. About 0:30-0:45 (est.) |
| 22 | Kenney Particle Pack | https://kenney.nl/assets/particle-pack (-> `kenney_particle-pack.zip`) | 15,001,764 B | CC0 | n | 512x512 VFX sprites: slashes, magic, sparks, flames, circles/symbols, trails |
| 23 | Kenney Smoke Particles | https://kenney.nl/assets/smoke-particles (-> `kenney_smoke-particles.zip`) | 6,019,666 B | CC0 | n | Frame sequences: explosion, flash, smoke, puff |
| 24 | Lightning animation (Calinou) | https://opengameart.org/sites/default/files/lightning_1.zip | 377,510 B | CC0 | n | Lightning bolt frames (black background) |
| 25 | Pirata One (Google Fonts) | https://raw.githubusercontent.com/google/fonts/main/ofl/pirataone/PirataOne-Regular.ttf + https://raw.githubusercontent.com/google/fonts/main/ofl/pirataone/OFL.txt | 56,316 B + 4,427 B | OFL 1.1 | ship OFL.txt | Title / logo font |
| 26 | Lilita One (Google Fonts) | https://raw.githubusercontent.com/google/fonts/main/ofl/lilitaone/LilitaOne-Regular.ttf + https://raw.githubusercontent.com/google/fonts/main/ofl/lilitaone/OFL.txt | 28,092 B + 4,392 B | OFL 1.1 | ship OFL.txt | Damage numbers, HUD counters |

### Total size

- Exact (rows 2-3, 6-17, 19-26): 79,760,681 B, about **79.8 MB**.
- Estimated (rows 1, 4, 5 GitHub zips about 12.6 MB; row 18 itch 13 MB): about 25.6 MB.
- **Download total: about 105 MB (about 100 MiB).**
- In the repo it will be much smaller. Commit only the files the game uses, convert the WAV
  music and the 24-bit spell WAVs to OGG, and take a subset of the Particle Pack. Expect
  about 30-40 MB, of which about 19.3 MB is the 4 Skeleton GLBs (each carries 90+ embedded
  clips).

## Optional / alternatives (all verified, not in the total)

| Name | URL | Size | License | Attr | Use |
|------|-----|------|---------|------|-----|
| Quaternius `Big/glTF/Orc_Skull.gltf` | https://drive.usercontent.google.com/download?id=13wbbztVj_2eYyF5lavumLvK9JyCfQEhI&export=download | 1,286,559 B | CC0 | n | Alternate boss (skull-helmed orc) |
| Quaternius `Flying/glTF/Ghost_Skull.gltf` (+ Flying atlas id 1I77ztJm_byEkqBVMmLc4SaVhngxdoB7p, 9,007 B) | https://drive.usercontent.google.com/download?id=1JIw8lx6H5IIhf_3Z5FprEu_yCoMcoRN7&export=download | 682,385 B | CC0 | n | Flying elite / ghost enemy |
| Quaternius Ultimate Monsters, whole pack (Drive folder) | https://drive.google.com/drive/folders/18m4KpzpEzhC9wl7jzr6dUc0N8Jozr79C | about 215 MB (third-party figure) | CC0 | n | All 50 monsters (Big / Blob / Flying x glTF/FBX/OBJ/Blend) |
| Kenney Graveyard Kit 5.0 | https://kenney.nl/assets/graveyard-kit (-> `kenney_graveyard-kit_5.0.zip`) | 3,623,408 B | CC0 | n | Alternate graveyard kit (90 files, "Animation" feature; Kenney style, not KayKit) |
| KayKit Forest Nature Pack (free tier) | https://kaylousberg.itch.io/kaykit-forest (itch only) | 6.1 MB | CC0 | n | Haunted-forest edge: trees, rocks, bushes, grass; one 1024 gradient atlas |
| KayKit Dungeon Pack 1.1 (renamed Dungeon Remastered 1.1) | https://kaylousberg.itch.io/kaykit-dungeon-pack (itch only) | 31 MB | CC0 | n | Same as row 5 but .glb split into .gltf/.bin, +8 pieces |
| KayKit Adventurers 2.0 / Skeletons 1.1 / Character Animations 1.1 | https://kaylousberg.itch.io/kaykit-adventurers , https://kaylousberg.itch.io/kaykit-skeletons , https://kaylousberg.itch.io/kaykit-character-animations | 12 MB / 7.7 MB / 14 MB | CC0 | n | Rig_Medium path (replace the local 1.0 heroes too) |
| Kenney UI Audio | https://kenney.nl/assets/ui-audio (-> `kenney_ui-audio.zip`) | 411,949 B | CC0 | n | More click/rollover/switch sounds |
| Kenney Digital Audio | https://kenney.nl/assets/digital-audio (-> `kenney_digital-audio.zip`) | 990,367 B | CC0 | n | Synth power-ups (alt level-up / pickup) |
| Kenney Splat Pack | https://kenney.nl/assets/splat-pack (-> `kenney_splat-pack.zip`) | 351,719 B | CC0 | n | Ground splat decals |
| Freeze Spell (artisticdude) | https://opengameart.org/sites/default/files/freeze.wav | 613,544 B | CC0 | n | Frost spell, direct URL (if itch row 18 is skipped) |
| Ice spells (bart) | https://opengameart.org/sites/default/files/icespells.zip | 269,661 B | CC0 | n | Frost spell variants |
| Spell sounds, electric (Augmentality) | https://opengameart.org/sites/default/files/electricspell.ogg , https://opengameart.org/sites/default/files/electricspell2.ogg | 341,572 B + 215,995 B | CC0 (dual CC0 / OGA-BY 3.0; take CC0) | n | Lightning spell, direct URL |
| Magic Spell SFX (JaggedStone) | https://opengameart.org/content/magic-spell-sfx (7 OGGs, e.g. https://opengameart.org/sites/default/files/magical_1_0.ogg 59,071 B) | 7 files, about 0.37 MB | CC0 | n | Generic cast / magic sparkle |
| Crossbow Shot (spookymodem) | https://opengameart.org/sites/default/files/Crossbow%20Shot.wav | 357,508 B | CC-BY 3.0 | **y** | Crossbow release + impact |
| Medieval sound effects - Weapon Textures (Jaszczak & Nelson) | https://opengameart.org/sites/default/files/medieval_sfx_textures_1_of_2.7z + `_2_of_2.7z` | 89,924,031 B + 86,084,389 B | CC0 | n | Best bow/crossbow/blade foley, but 176 MB; cherry-pick only |
| Boss Battle #2 [Symphonic Metal] (nene) | https://opengameart.org/sites/default/files/boss_battle_%232_metal_pack.zip | 11,010,042 B | CC0 | n | Alt boss: intro + loop parts (zip file names are swapped, per the page) |
| Battle Theme A (cynicmusic) | https://opengameart.org/sites/default/files/battleThemeA.mp3 | 3,289,143 B | CC0 | n | Alt battle: strings + horns |
| Dark Theme (JaggedStone) | https://opengameart.org/sites/default/files/theme_0.ogg | 1,006,663 B | CC0 | n | Alt menu: dark/spooky |
| Kevin MacLeod - "Ossuary 1 - A Beginning" (3:08) | https://incompetech.com/music/royalty-free/mp3-royaltyfree/Ossuary%201%20-%20A%20Beginning.mp3 | 6,020,064 B | CC-BY 4.0 | **y** | Alt menu: dark, eerie, calm |
| Kevin MacLeod - "Unholy Knight" (2:34) | https://incompetech.com/music/royalty-free/mp3-royaltyfree/Unholy%20Knight.mp3 | 4,941,701 B | CC-BY 4.0 | **y** | Alt battle: organ + choir action, 140 BPM |
| Kevin MacLeod - "Black Vortex" (2:50) | https://incompetech.com/music/royalty-free/mp3-royaltyfree/Black%20Vortex.mp3 | 6,790,920 B | CC-BY 4.0 | **y** | Alt boss: aggressive orchestral, 163 BPM |
| Kevin MacLeod - "Crypto" (3:24) / "Five Armies" (2:36) | https://incompetech.com/music/royalty-free/mp3-royaltyfree/Crypto.mp3 , https://incompetech.com/music/royalty-free/mp3-royaltyfree/Five%20Armies.mp3 | 6,532,455 B / 6,235,982 B | CC-BY 4.0 | **y** | Alt menu / alt battle |
| 4 summoning circles (Luke.RUSTLTD) | https://opengameart.org/sites/default/files/summoning_circles.zip | 4,306,421 B | CC0 | n | Magic-circle decals (.ai/.svg; export PNG) |
| More Explosions (StumpyStrust) | https://opengameart.org/sites/default/files/exports_0.zip | 2,792,412 B | CC0 | n | Extra explosion sheets (small frames, about 100x100) |
| Cinzel Decorative Bold (Google Fonts) | https://raw.githubusercontent.com/google/fonts/main/ofl/cinzeldecorative/CinzelDecorative-Bold.ttf (+ OFL.txt 4,393 B) | 62,300 B | OFL 1.1 | ship OFL.txt | Alt title font (classic serif caps) |
| New Rocker / Bangers (Google Fonts) | https://raw.githubusercontent.com/google/fonts/main/ofl/newrocker/NewRocker-Regular.ttf , https://raw.githubusercontent.com/google/fonts/main/ofl/bangers/Bangers-Regular.ttf | 173,224 B / 93,148 B | OFL 1.1 | ship OFL.txt | Alt title (gothic) / alt damage-number font |

## Per-item notes

### 1. Monsters on the KayKit rig: KayKit Skeletons

- Repo: https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0 . It is a
  user account, not an org; there are 10 repos in total. HEAD is commit 15b62b9b (2024-02-02),
  the same commit the Godot Asset Library pins (asset 2566). The moving URL
  `.../archive/refs/heads/main.zip` also resolves (302 -> codeload 200).
- The archive contains `addons/kaykit_character_pack_skeletons/`:
  - `Characters/gltf/Skeleton_Minion.glb` (4,814,296 B), `Skeleton_Warrior.glb`
    (4,863,620 B), `Skeleton_Rogue.glb` (4,827,024 B), `Skeleton_Mage.glb` (4,761,048 B),
    and `skeleton_texture.png` (17,037 B).
  - `Assets/gltf/*.gltf + *.bin + skeleton_texture.png`. These 13 props are Blade, Axe,
    Crossbow, Staff, Quiver, Shield_Large_A/B, Shield_Small_A/B, Arrow,
    Arrow_Broken(_Half), Arrow_Half. Verified: `Skeleton_Blade.gltf` points at
    `images[0].uri = "skeleton_texture.png"` and `buffers[0].uri = "Skeleton_Blade.bin"`.
  - `Textures/skeleton_texture.png` and `LICENSE.txt` (CC0, Kay Lousberg).
  - The FBX files (22 MB each) and Samples are excluded by `export-ignore`.
- Rig and clips:
  - The README says "fully rigged and animated ... 90+ animations".
  - Same 1.0 generation as your Adventurers 1.0: same packaging, GLBs with embedded clips,
    and a palette texture beside each GLB. 90+ is consistent with the Adventurers' 76 clips
    plus a skeleton-specific set (awaken, spawn from ground, taunt, skeleton walk/death).
  - **Not byte-verified** (that needs the file). After download, compare joint lists:
    `python -I <scratchpad>/glbinfo.py Knight.glb Skeleton_Minion.glb`. The script is at
    C:\Users\User\AppData\Local\Temp\claude\C--Workspace-github-UnravelEngine-UnravelEngine\4da809e6-fd2c-4f39-9b7b-211eeff32938\scratchpad\glbinfo.py
    and prints joints, images and clip names.
  - Even if the rigs differ, every Skeleton GLB carries its own clips, so the game still works
    without shared clips.
- Import concern: the character GLBs embed the texture. The engine will not wire it
  automatically, so assign one shared material using `skeleton_texture.png` (the same
  approach as the Adventurers). Props are proper .gltf + .bin + external PNG and import
  cleanly. Weapons attach to the `handslot.l` / `handslot.r` joints.
- Other free CC0 KayKit character packs on the same rig: **none beyond these two**.
  - GitHub has only Adventurers 1.0 and Skeletons 1.0 as character packs; the other 8 repos
    are environment "Bits"/kits.
  - On itch the free character content is Adventurers 2.0, Skeletons 1.1 and Character
    Animations 1.1 (Rig_Medium 100+ clips; Rig_Large 25+ clips, but no free Rig_Large
    character exists).
  - Paid only: Skeleton Golem and Necromancer (Skeletons EXTRA), 3 extra adventurers
    (Adventurers EXTRA), and Mystery Monthly Series 4-6.
  - Legacy packs on the old blocky rig, not compatible: "(Legacy) Character Pack: Skeletons"
    (https://kaylousberg.itch.io/kaykit-skeleton-pack) and "(Legacy) Character Animations"
    (= your local 1.2 copy).
- Cheap boss fallback with zero downloads: `Skeleton_Warrior` scaled 2.5-3x with a tinted
  material instance. It shares every clip.

### 2. Boss: Quaternius Ultimate Monsters (Oct 2022, 50 monsters)

- Page: https://quaternius.com/packs/ultimatemonsters.html . It links CC0
  (creativecommons.org/publicdomain/zero/1.0). poly.pizza lists the bundle as "Public Domain
  (CC0)": https://poly.pizza/bundle/Ultimate-Monsters-Bundle-5oyGWAmOB6 . The site's "Just
  give me the Download" button opens the Drive folder
  https://drive.google.com/drive/folders/18m4KpzpEzhC9wl7jzr6dUc0N8Jozr79C , which has
  subfolders `Big`, `Blob`, `Flying` (each with Blends/FBX/glTF/OBJ), plus `Atlas_Monsters.png`,
  `Color Guide.png`, `License.txt` and `Preview.jpg`.
- Big glTF folder: https://drive.google.com/drive/folders/1sOXLt5U3ofaujPlQRL11s4ub2UsqN8V8 .
  It holds Alien, Birb, BlueDemon, Bunny, Cactoro, Demon, Dino, Fish, Frog, Monkroose,
  MushroomKing, Ninja, Orc, Orc_Skull, Tribal and Yeti `.gltf`, plus `Atlas_Monsters.png`.
  - File sizes from Drive file pages: Demon 1,264,925 B, Orc_Skull 1,286,559 B, Yeti
    1,227,684 B, MushroomKing 1,219,690 B. Big FBX: Demon.fbx 3,463,660 B (FBX folder
    https://drive.google.com/drive/folders/1JvPqInPVfD3HW-iHpSQJtVAM-XIjB9Aa).
- Flying glTF folder: https://drive.google.com/drive/folders/1RDoNgoRYBNacHO-uRPfSxEgtc4U-EmaB .
  It holds Dragon (427,100 B), Dragon_Evolved (991,335 B), Ghost (643,180 B), Ghost_Skull
  (682,385 B), Demon, Hywirl and others.
- Format:
  - `.gltf` with embedded (base64) buffers and NO .bin.
  - One external 9 KB palette texture, `Atlas_Monsters.png`. Keep it next to the .gltf. Big
    and Flying ship identical-size copies.
  - Because the texture is external, it wires like the KayKit props. The rig is Quaternius's
    own (not KayKit's); clips are embedded per model.
  - FBX via assimp is the fallback.
- Clips: no Quaternius page lists them. A public repo that commits these exact glTFs documents
  them (https://github.com/Han1989/tgt-td-cld/pull/112, README
  `tools/blender/assets/quaternius/monsters/README.md`):
  - Big (Demon, BlueDemon, MushroomKing, Dino, Cactoro): `Death, Duck, HitReact, Idle, Jump,
    Punch, Run, Walk, Weapon`, and the README also uses `Jump_Land` and `Wave`. This covers
    idle, walk, run, two attacks (Punch, Weapon), hit and death; Jump + Jump_Land works as a
    ground slam.
  - Flying (Ghost, Hywirl): `Death, Fast_Flying, Flying_Idle, Headbutt, HitReact, Punch`.
  - Blob (Mushnub): `Bite_Front, Death, HitRecieve, Idle, Jump, Walk`.
  - Names may carry an armature prefix (e.g. `CharacterArmature|Idle`) in the file. Check
    with glbinfo.py.
- Pick: **Demon** (red horned brute) for the boss; Orc_Skull or MushroomKing as alternates;
  Ghost_Skull as a flying elite that fits the graveyard theme.
- Rejected: Quaternius "Bestiary - Dungeon Monsters Kit" (Aug 2026) uses the "QAL" license,
  which forbids redistributing the assets ("may not extract, repackage, sublicense, sell, or
  otherwise redistribute the Assets"), so it cannot go in a public repo.

### 3. Arena environment

- **Primary: KayKit Halloween Bits 1.0** (https://github.com/KayKit-Game-Assets/KayKit-Halloween-Bits-1.0).
  - The archive keeps `addons/kaykit_halloween_bits/Assets/{gltf, obj, textures, LICENSE.txt}`
    (FBX excluded).
  - `gltf/` holds 63 models as `.gltf + .bin` with ONE shared `halloweenbits_texture.png`
    (15,093 B). Verified on `grave_A.gltf`: `uri: "halloweenbits_texture.png"`, material
    `HalloweenBits`. This is the best import format for this engine.
  - Models: arch, arch_gate, bench(_decorated), bone_A/B/C, candle (melted/thin/triple),
    coffin(_decorated), crypt, fence (broken/gate/pillar/seperate), floor_dirt(_grave/_small),
    grave_A(_destroyed)/B, gravemarker_A/B, gravestone, lantern_hanging/standing, path_A-D,
    pillar, plaque(_candles), post(_lantern/_skull), pumpkins (+jack-o-lanterns), ribcage,
    shrine(_candles), skull(_candle), tree_dead_large/medium/small(+decorated), and autumn
    pine trees (orange/yellow, 3 sizes).
  - The itch version (https://kaylousberg.itch.io/halloween-bits, free 5.3 MB, "60+") matches
    the GitHub content.
- **Secondary: KayKit Dungeon Remastered 1.0** (https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0),
  for a ruined-crypt ring or edge.
  - Contents: 203 models: walls (broken, cracked, arched, half, sloped, scaffold), pillars,
    columns, barrier, rubble_half/large, floor tiles (broken, weeds, grates, dirt), stairs,
    torches (lit/mounted), candles, banners, chests, coins, barrels, crates and tables.
  - Format concern: in 1.0 the `gltf/` folder is **binary `*.gltf.glb`**, which this engine
    will not texture automatically. Two fixes:
    - (a) Import the **OBJ** folder instead: each `.mtl` has `map_Kd dungeon_texture.png`
      (verified on `wall.mtl`) and the PNG sits beside it.
    - (b) Import the GLBs and assign one material with `Assets/texture/dungeon_texture.png`
      (15,794 B).
  - The itch "Dungeon Pack 1.1" (31 MB) splits these into .gltf/.bin, but it is itch-only.
- Both KayKit kits use a single 1024 gradient-atlas texture, the same art style as the
  Adventurers and Skeletons.
- Kenney Graveyard Kit 5.0 (optional) is a valid CC0 alternative, but its art style is
  Kenney's, not KayKit's. The pack page does not state its file layout; Kenney 3D kits usually
  ship GLB/FBX/OBJ with a shared colormap PNG, so check whether its GLBs embed or reference the
  texture.

### 4. Sound effects - coverage

| Need | Covered by (row #) |
|------|--------------------|
| Sword swings | 8 Swishes (13), 7 RPG Sound Pack (3 swoosh), 6 80 CC0 RPG (3 blade) |
| Sword / axe hits | 7 RPG Sound Pack (5 unsheathe/clash), 13 Kenney Impact (metal/wood/punch impacts), 17 Kenney RPG Audio (knife/chop) |
| Bone crunch / skeleton death | 9 Deep Bone Cracks (10), 10 Bones rattle (9) |
| Arrow / crossbow shots | 8 Swishes (tagged arrow/whoosh) + 13 Kenney Impact (wood thunk on hit); optional Crossbow Shot (CC-BY 3.0) or the 176 MB CC0 Medieval weapon library |
| Fire / frost / lightning spells | 18 Basic Spell Impacts (5 each + water); direct-URL fallbacks: Freeze Spell, Ice spells, electricspell.ogg; 6 80 CC0 RPG (9 spell incl. fire) |
| Explosions | 15 Kenney Sci-fi (explosions); 6 80 CC0 RPG (misc) |
| Footsteps | 13 Kenney Impact (footsteps on several surfaces), 17 Kenney RPG Audio (footsteps) |
| Gem pickup chime | 11 Gem collect SFX (+ reverb variant for rare gems), 6 80 CC0 RPG (item: gem), 14 Kenney Interface (confirmation/glass) |
| Level-up fanfare | 16 Kenney Music Jingles (85 short jingles), 12 wobbleboxx Level up pack |
| UI clicks | 14 Kenney Interface Sounds, optional Kenney UI Audio |
| Boss roar | 7 RPG Sound Pack (15 guttural beast, 5 giant, 5 ogre), 6 80 CC0 RPG (creature roar/monster) |

- Content lists for the OGA packs come from their pages. The Kenney pages list no file names;
  category coverage comes from Kenney's own OGA descriptions ("engines, explosions, lasers";
  "button clicks, snaps, minimize, maximize, confirmation") and the pack tags. Audition the
  sounds after download.
- Formats: Kenney ships OGG. The OGA packs are WAV/OGG (`dark_magic.7z`, the optional
  Medieval weapon pack, is 7z). The engine imports `.ogg .wav .flac .mp3`. Convert WAVs to
  OGG before committing.
- lentikula "Basic Spell Impacts" is itch-only ("Name your own price", $0 allowed). Its page
  states CC0 and links the CC0 1.0 deed.
- "Spell sounds" (Augmentality) is dual-licensed CC0 / OGA-BY 3.0. Use it under CC0.

### 5. Music

| Slot | Track | Format / size | Duration | License |
|------|-------|---------------|----------|---------|
| Menu | Dark Shrine Loop - qubodup (remix of yd's "Shrine") | OGG 678,630 B (also FLAC 2,997,779 B, MP3 905,717 B) | about 0:30-0:45, est. from FLAC/OGG size | CC0 |
| Battle loop | Determined Pursuit (epic orchestra loop) - Emma_MA | WAV 19,052,830 B | about 1:48 (bytes / 176,400 assuming 44.1 kHz 16-bit stereo) | CC0 ("public domain as of January 2017") |
| Boss loop | Heavy Boss Battle 1 - MintoDog (Jan 2026) | OGG 3,360,172 B (MP3 4,322,869 B: https://opengameart.org/sites/default/files/heavy_boss_battle_1_bpm200.mp3) | about 1:50-3:00, est. (bitrate unknown) | CC0 |

- Durations: OpenGameArt does not publish durations, and reading the audio headers would mean
  downloading. The WAV figure is exact if the file is 44.1 kHz/16-bit/stereo (99 s if 48 kHz);
  the OGG figures are estimates. Run `ffprobe` after download.
- All three pages are CC0. Loopability: Determined Pursuit says "loop" and suggests "battle
  theme, boss fight"; Heavy Boss Battle 1 states loopable; Dark Shrine Loop says loop in its
  title and tags.
- CC-BY alternates with exact durations from incompetech's catalog (`pieces.json`), all
  Kevin MacLeod, CC-BY 4.0: Ossuary 1 - A Beginning 3:08, Crypto 3:24, Unholy Knight 2:34,
  Five Armies 2:36, Black Vortex 2:50. These are full compositions, not seamless loops.
- Attribution text (exact incompetech template), per track:
  `"Unholy Knight" Kevin MacLeod (incompetech.com)` /
  `Licensed under Creative Commons: By Attribution 4.0 License` /
  `http://creativecommons.org/licenses/by/4.0/`

### 6. VFX textures

- **Kenney Particle Pack** (80 files, page tile size 512x512, CC0, v1.0 2018).
  - Single-frame additive sprites. The usual set is slash, magic, spark, flame/fire, smoke,
    star, circle, symbol (rune circles), twirl, trace (trails), muzzle, scorch, light, flare,
    dirt and scratch. The page does not list the sprites, so verify the names after download.
  - Covers slashes/swooshes and magic circles.
- **Kenney Smoke Particles** (70 files, CC0).
  - The usual layout is frame sequences for explosion, flash, black smoke and white puff. The
    page does not list them; verify after download.
  - Covers explosion and smoke flipbooks (pack the frames into an atlas at import).
- **Lightning animation** (Calinou, CC0). PNG lightning frames (OGA frame pages show
  1024x512) on black: use additive blend, or key out black.
- Optional: 4 summoning circles (CC0 vector), More Explosions (CC0, small frames), and the
  StarsteelGaming spell effects (CC0, pixel art; poor fit for 3D).

### 7. Fonts

- **Pirata One**: Copyright (c) 2012 Rodrigo Fuenzalida, Nicolas Massi, with Reserved Font
  Name 'Pirata'. A blackletter fantasy display face for titles.
- **Lilita One**: Copyright (c) 2011 Juan Montoreano, with Reserved Font Name Lilita. A heavy,
  round, very legible face for damage numbers.
- Alternates: Cinzel Decorative (Natanael Gama), New Rocker (Pablo Impallari, Brenda Gallo),
  Bangers (The Bangers Project Authors).
- All are OFL 1.1. Ship each `OFL.txt` next to its `.ttf`, and do not rename the font if you
  modify it (Reserved Font Name). The engine imports `.ttf/.otf`.
- Your local Kenney Fonts remain an option for HUD text.

## Credits block (CC0 credits are voluntary; CC-BY lines only if those alternates are used)

```
Characters: KayKit Adventurers / Skeletons, Halloween Bits, Dungeon Remastered by Kay Lousberg (www.kaylousberg.com) - CC0
Boss: Ultimate Monsters by Quaternius (quaternius.com) - CC0
Sounds: Kenney (www.kenney.nl) - CC0; rubberduck, artisticdude, Zane Little Music, congusbongus, Bobjt,
        WobbleBoxx Workshop, lentikula (OpenGameArt / itch.io) - CC0
Music: "Determined Pursuit" by Emma_MA, "Heavy Boss Battle 1" by MintoDog, "Dark Shrine Loop" by qubodup
       (remix of "Shrine" by yd) - CC0
VFX: Kenney Particle Pack / Smoke Particles - CC0; "Lightning animation" by Calinou - CC0
Fonts: Pirata One, Lilita One - SIL Open Font License 1.1
[if used] "<Title>" Kevin MacLeod (incompetech.com) Licensed under Creative Commons: By Attribution 4.0 License http://creativecommons.org/licenses/by/4.0/
[if used] "Crossbow Shot" by spookymodem (opengameart.org) - CC-BY 3.0
```

## Rejected

- Quaternius "Bestiary - Dungeon Monsters Kit": QAL license with no redistribution of the
  assets.
- Paid tiers (KayKit EXTRA/SOURCE, Mystery Monthly): not free. They are also CC0, but out of
  scope.
- CC-BY-SA or GPL-only items: "Spell 4 (fire)" (Bart K.: CC-BY / CC-BY-SA / GPL / OGA-BY)
  was skipped in favour of CC0 options. "Bow & Arrow Shot" (dorkster, CC-BY-SA 3.0) was
  skipped (share-alike).
- "Crystal Cave + Mysterious Ambience (seamless loop)": CC-BY / CC-BY-SA / GPL only, no CC0.
- "Fire & Evil Spell" (artisticdude), "Ice & Electricity Magic" (qubodup), "Thunder"
  (Jerimee), "Electric Sound Effects Library" (Little Robot Sound Factory, 84.7 MB): CC-BY
  3.0. They are acceptable but not needed, since CC0 options cover these.
- KayKit legacy packs and the local Character Animations 1.2: legacy rig, not compatible.

## Verification log (2026-10-28)

- GitHub archives (Skeletons pinned + main, Halloween, Dungeon Remastered, Adventurers):
  HEAD -> 302 -> codeload 200 `application/zip` with the expected `content-disposition`.
  - Codeload streams archives without `Content-Length`, so zip sizes are estimates. Unpacked
    sizes are exact: git tree API blob sizes filtered by each repo's `.gitattributes`
    export-ignore rules.
  - Compression estimate: ratio 0.3275, measured on your local Adventurers archive, applied to
    Skeletons (same content type). Halloween and Dungeon contain OBJ text that compresses
    better, so their estimate is about 0.35.
- `raw.githubusercontent.com` files (Skeleton GLBs, texture, props, LICENSE, Google Fonts
  TTF + OFL.txt): HEAD 200, sizes from `Content-Length`. The New Rocker, Cinzel Decorative and
  Bangers files also returned 200.
- OpenGameArt file URLs (every row and alternate above): HEAD 200 with `Content-Length` equal
  to the size shown on each page. Licenses were read from each page's License(s) field.
- incompetech MP3s: HEAD 200 with `Content-Length`. Durations come from incompetech
  `pieces.json`. CC-BY 4.0 is confirmed in the licenses page and a track page.
- Kenney:
  - kenney.nl no longer has stable file links. The Download button calls
    `https://kenney.nl/download-link/<hex of "assets/<slug>">` (needs Referer and the page
    cookie), which returns a signed URL that expires.
  - HEAD on that signed URL returns 404, so I used a GET aborted before the body
    (`curl --max-filesize 1`, headers only, nothing saved). Every pack listed returned 200 with
    `Content-Length` and the zip filename shown.
  - Use the page URL as the stable reference.
- itch.io pages (KayKit packs, lentikula): GET 200. Sizes are as listed in each page's
  download section. There are no direct file URLs; itch issues signed links after "Download"
  -> "No thanks, just take me to the downloads".
- Google Drive (Quaternius):
  - Folder listings were read via `embeddedfolderview`, and file sizes from each file's
    `/file/d/<id>/view` page. HEAD on `drive.usercontent.google.com/download?id=...` returned
    200 but no length.
  - Note: one status-check pass used a plain GET with output discarded to /dev/null on 5 small
    Drive file URLs (Demon, Orc_Skull, Ghost_Skull and 2 atlases, at most about 3.3 MB).
    Nothing was written to disk.

## Post-download checklist

1. Compare joints: `glbinfo.py Knight.glb Skeleton_Minion.glb` should show the same 41 joint
   names. List the Skeleton clip names.
2. For the Quaternius Demon, list the clips and confirm the `Atlas_Monsters.png` uri.
3. Run `ffprobe` on the three music files to get exact durations. Trim or set loop points if
   needed.
4. Convert WAV to OGG (music and spell impacts). Commit only the used subset, with each pack's
   LICENSE/License.txt.

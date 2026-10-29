# Barrowlight development tools

Python tools that built and tune the sample through the running editor's MCP server (`http://127.0.0.1:27182`).
The game's sources live only in `../data` (scripts, UI, audio); nothing here is part of the game or its deploy.
The editor watches `data/` and recompiles scripts on its own when a `.cs` file changes.

Run every tool from this folder with the editor open on this project (`python open_project.py` opens it).

## Daily loop

| Task | Command |
|---|---|
| Wait for the editor's script build and print its errors | `python push.py scripts` |
| Reload UI documents after editing `.rhtml` / `.rcss` | `python push.py ui [name filter...]` |
| Compile the scripts offline (no editor) | `cd check && dotnet build -nologo -v q` |
| Autopilot balance run of one class | `python balance_sim.py CLASS GAME_SECONDS SPEED TAG [inv]` (appends to `shots/balance.log`) |
| Pressure at a Dread level (enemies alive per minute) | `python difficulty_sim.py CLASS DREAD GAME_SECONDS SPEED TAG [inv]` (appends to `shots/difficulty.log`) |
| Smoke test: every class, every evolved weapon, every map event | `python smoke_all.py [SPEED] [SECONDS_PER_PASS] [classes...]` |
| Damage per second of single weapons (rank 8 vs evolved) | `python weapon_probe.py HERO SECONDS id...` |
| Review a map event (waiting, under way, end) | `python event_preview.py Shrine/Robber/Pyres/Moonwell [CLASS] [START_SECONDS]` |
| A map event without the horde | `python event_look.py KIND [SECONDS]` |
| Weapons and actives in slow motion | `python gallery.py HERO ids...`, `python actives.py` |
| Screens at a resolution | `python screens_preview.py`, `python chest_preview.py W H TAG`, `python armory_preview.py` |
| Deploy to the phone | `python deploy_s26.py` |

Screenshots and logs land in `shots/` (ignored). `editor_lock.py OWNER cmd...` serializes editor use between
several agents or scripts.

## Builders

The scene's content was assembled by these scripts; rerun one after changing it.

| Script | Builds |
|---|---|
| `build_arena.py` (+ `arena_components.py`, `arena_plan.json`) | The moor: ground, props, obstacles, lights |
| `build_effects.py`, `build_effects_weapons.py`, `build_effects_bosses.py` | Shared, weapon and boss particle effects |
| `build_effects_spells*.py`, `build_effects_core_v2.py`, `build_effects_events.py` | Class spells, chest beacons, traits, map events |
| `build_heroes.py`, `build_controllers.py`, `build_hero_marker.py`, `build_bosses.py`, `build_menu_stage.py` | Heroes, animators, boss prefabs, the title stage |
| `mood.py` | Lighting and sky settings |
| `tools/ui/build_ui_art.py` | UI icons, frames and glyphs into `../data/UI` |
| `tools/gen_*.py`, `tools/kaykit_merge.py`, `toolkit/` | Textures, telegraph meshes and merged characters (outputs in `tools/out`, ignored) |
| `audio_work/tools/build_audio.py` | The sound library from the downloads (`../../_downloads/survivors`) into `../data/Audio` |

Effect builders write particle emitters into the open scene; save it afterwards (`scene_save` over MCP).

## Folders

- `history/` - one-shot patch and import scripts that built the game step by step; already applied, kept as a record.
- `check/` - an offline compile project over `../data/Scripts`.
- `save_backup/` - copies of the player save taken before tools that change it (`Run.DevArmory`).
- Design notes: `design_brief.md` (the game), `design_update_2026-10-29.md` (class update), `design_events.md` (map
  events), the `manifest_*.md` and `*_BRIEF.md` files written for the module agents, `api_digest.md`.

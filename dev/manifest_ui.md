# Barrowlight - UI + meta module manifest

Files (S = scratchpad\survivors): `S\src\Scripts\UI\**`, `S\src\Scripts\Meta\**`, `S\src\UI\**` (maps to
`app:/data/UI/**`), tools in `S\tools\ui\build_ui_art.py` (re-run it to regenerate every PNG; contact sheet
`S\tools\ui\icons_sheet.png`, mockups `S\tools\ui\mockup_*.png`).

## 1. Scene setup

1. One entity for `Run.uiRoot` (suggested name `UI`). Under it, one child per document, named exactly as below, each
   with a **UI Document** component: `render_mode = screen_space_overlay`, `asset` = the key. Enabled state does not
   matter: GameUI hides every document at start and shows them itself. If a child is missing, GameUI creates it at
   runtime (logged), but then its render order is not under your control.
2. Draw order: set each UI Document's `sort_order` (higher draws on top and gets pointer / keys first):
   Overlay 10, Hud 20, Title 30, HeroSelect 30, Results 30, LevelUp 40, Chest 40, Pause 40, Details 50,
   Armory 60, Settings 60. Documents with the same order are never shown together.
3. Import settings: set compression **none** for `app:/data/UI/Icons/*.png` (pixel-art glyphs scaled 3x),
   `app:/data/UI/Art/*.png` and `app:/data/UI/Glyphs/*.png` (assets_set_import_settings quality.compression none).
4. Touch: add Unravel.Samples.SampleTouchJoystick on its own entity (Show = TouchScreen, Floating = true,
   Radius 80, DeadZone 0.1, SouthLabel / face labels empty: the HUD has its own active button bottom-right).
5. Optional turntable: `run.ui.heroPreviewChanged += heroClass => ...` (hero select focus changes).
6. Optional display font: `Pirata One` (OFL, asset_sources.md row 25) for titles: put it at
   `app:/data/UI/Fonts/PirataOne-Regular.ttf` (+ OFL.txt) and change the `GT Display` @font-face `src` in
   `app:/data/UI/theme.rcss` (one line). Shipped default: Inter Black (OFL, in `app:/data/UI/Fonts`).

| Entity | Document key | Data model | Purpose |
|---|---|---|---|
| Hud | app:/data/UI/Hud.rhtml | hud | XP bar + level, timer, kills, gold, pause button, 6+6 slots with pips, active + radial cooldown |
| Overlay | app:/data/UI/Overlay.rhtml | - | damage numbers (256 pooled), hero health bar, elite health bars (8 pooled), low-HP vignette, flash, markers, edge chevrons, boss bar, banners |
| Title | app:/data/UI/Title.rhtml | - | logo, Play / Armory / Settings / Quit, best run |
| HeroSelect | app:/data/UI/HeroSelect.rhtml | heroes | 4 hero cards, locked hints, Dread selector, Start |
| LevelUp | app:/data/UI/LevelUp.rhtml | - | 3 cards, Reroll / Banish / Skip, icon flight to the HUD |
| Chest | app:/data/UI/Chest.rhtml | - | 1/3/5 reels, gold, Continue |
| Pause | app:/data/UI/Pause.rhtml | pause | build, damage per weapon, stat sheet, Resume / Settings / Abandon |
| Results | app:/data/UI/Results.rhtml | results | banner, tiles, weapon table, unlocks / records, Retry / Hero select / Armory / Title |
| Armory | app:/data/UI/Armory.rhtml | armory | 14 meta upgrades, Buy, Refund all |
| Settings | app:/data/UI/Settings.rhtml | settings | volumes, shake, damage numbers, effects opacity, elite bars, joystick side, interface size |
| Details | app:/data/UI/Details.rhtml | - | item details over the paused game (tap / click a HUD slot or the active's ? badge) |

Shared style sheets: `app:/data/UI/theme.rcss` first, one `<Screen>.rcss` per document, and
`app:/data/UI/phone.rcss` last (phone layouts of every screen, all under `body.phone`). Every length is in `rem`;
GameUI sets the body font-size to `10dp x scale`:
- desktops / tablets: fit the 1920 x 1080 design (identical layout at 1280 x 720, 1080p and 4K);
- touch screens: at least 1rem = 1.15 mm from `Screen.dpi` (physical size, not resolution);
- phones (touch, short side < 3.6 in, e.g. S26 2.4 in): the phone layouts, about 110 x 53 rem on an S26;
- times the player's Interface size (80 / 100 / 120 / 140%), clamped so a phone keeps >= 100 x 48 rem and other
  screens >= 144 x 80 rem.
`.safe` layers follow `Screen.safeArea` (camera cutout). Body classes written by GameUI: `touch`, `phone`,
`compact` (< 172 rem wide), `short` (< 100 rem tall), `stick-right` (joystick on the right), `input-kbd |
input-pad | input-touch`, `show-focus` (keyboard / gamepad focus ring).

## 2. Input actions (project input settings)

Menus read the keyboard with key codes (no action needed) and the gamepad through these actions:

| Action | Gamepad | Keyboard (read directly) | Used for |
|---|---|---|---|
| UISubmit | South (A) | Enter, Keypad Enter, Space | press focused button / pick focused card |
| UICancel | East (B) | Escape, Backspace | back, resume from pause |
| UIUp / UIDown / UILeft / UIRight | D-pad | (WASD / arrows via Horizontal / Vertical) | move focus, change a setting |
| UIReroll | North (Y) | R | level-up reroll |
| UIBanish | West (X) | X | banish the focused card |
| UISkip | Back / View | Q | level-up skip (+5 gold) |
| Pause (core) | Start | Escape, P | open pause (core), close pause (UI) |
| Horizontal / Vertical (default) | left stick | WASD, arrows | focus moves with repeat (0.38 s, then 0.11 s) |

Level-up also takes keys 1 / 2 / 3 (and keypad). Active (core) stays Space / gamepad South in play; the HUD active
button is also tappable.

## 3. Sounds (all on the Ui bus unless noted; register in SoundCatalog)

| Name | Description | Layers | Length | Variants |
|---|---|---|---|---|
| ui_hover | focus moves / pointer enters a button | soft wooden tick | 40 ms | 2, limit 1, min interval 0.06 s |
| ui_click | button press | tick + low thump | 80 ms | 2 |
| ui_confirm | start run, continue, retry | click + bright bell | 250 ms | 1 |
| ui_back | back / close / resume | reversed soft click | 120 ms | 1 |
| ui_error | refused (no charges, no gold, locked) | dull double knock | 200 ms | 1 |
| ui_select | hero card selected | metal ring, short | 200 ms | 1 |
| ui_toggle | segmented option changed | latch click | 80 ms | 1 |
| ui_tick | slider step by pad / keys | tiny tick | 30 ms | 1, min interval 0.04 s |
| ui_open | details panel opens | soft page turn + low tick | 200 ms | 1 |
| card_flip | each level-up card lands (3 x 0.06 s apart) | paper flip + air | 150 ms | 3, pitch +-6% |
| card_select | a card is picked (core then plays card_pick) | whoosh up | 300 ms | 1 |
| card_reroll | reroll | shuffle of cards | 400 ms | 1 |
| card_banish | banish | torn paper + low burn | 400 ms | 1 |
| card_skip | skip | coin drop | 300 ms | 1 |
| reel_tick | chest reel step | dry wooden click | 30 ms | 3, limit 2, min interval 0.06 s |
| reel_land | reel lands | thunk + small chime | 300 ms | 2 |
| reel_land_evolution | reel lands on an evolution | thunk + rising choir chord | 900 ms | 1 |
| chest_gold | chest gold counts up | coin cascade | 600 ms | 1 |
| buy | Armory purchase | anvil strike + coins | 500 ms | 2 |
| refund | Refund all | coin pour | 800 ms | 1 |
| banner | event banner | distant horn | 1.2 s | 1 |
| banner_warning | warning banner ("The ground splits...") | low brass swell + bell | 1.5 s | 1 |
| danger_warning | edge chevrons appear | rumble rising (Sfx bus) | 1.5 s | 1 |
| boss_bar_appear | boss bar slides in | deep drum hit | 800 ms | 1 |
| boss_bar_shatter | phase pip breaks | glass/bone shatter + sub | 700 ms | 1 |
| active_ready | active cooldown finished | soft chime (design 3.0) | 300 ms | 1, Sfx bus ok |
| results_victory | victory screen | choir + brass sting | 3 s | 1 |
| results_defeat | game-over screen | low strings + bell | 3 s | 1 |
| results_tally | result counters finished | coins + ding | 400 ms | 1 |

Core sounds the UI relies on (already in CONTRACTS): level_up, card_pick, chest_open, run_start.

## 4. Public API added (beyond the stubs)

- HUD: filled weapon / passive slots and the active's "?" badge take taps and clicks and open the Details
  panel. It pauses through the run's own `Run.Pause()` (GameUI.ShowPause then shows the details instead of the
  pause menu) and resumes with `Run.Resume()`; no Run hook is needed. On touch the HUD active icon is the touch
  button: it fires `run.loadout.TryActivate()` on press (mousedown), 14 rem (about 16 mm) at the bottom right
  inside the safe area, bottom left when `joystickOnRight`.
- Ownership tags (`UIText.GetOwnership`): class weapons and their evolutions "<HERO> UNIQUE" in the class
  colour with the hero badge (Knight silver, Barbarian red, Mage violet, Rogue teal), shared weapons "SHARED",
  passives "SHARED PASSIVE", fallback cards "BLESSING". Shown on level-up cards, chest reels, the details
  panel, the pause build list and the results table.
- `GameSettings.uiScale` (0.8 / 1.0 / 1.2 / 1.4), set in Settings > Interface size; applies at once.
- `GameUI.OpenWeaponDetails(slot)`, `OpenPassiveDetails(slot)`, `OpenActiveDetails()`: the details panel from code,
  self-contained: while playing they pause first (Run.Pause), while paused they replace the pause menu; closing
  resumes. HUD taps only act while `run.phase == Playing`.

- `GameUI.ShowEliteBar(Vector3 worldHead, float healthFraction)` (per frame, in StepPlaying before `ui.Update`, max 8;
  hidden when Settings > Elite health bars is off or the head is off screen),
  `GameUI.ShowMarker(Vector3 world, MarkerKind kind)` (per frame), `GameUI.FlashScreen(bool isDanger)`,
  `GameUI.heroPreviewChanged`, `enum MarkerKind { Elite, Chest, Boss, Treasure }`.
- `DamageNumbers.Show(Vector3, float, DamageKind)`, `DamageNumbers.ShowText(Vector3, string, DamageKind)` (words such as
  "EXECUTED": same pool and motion, no budget, no merging), `DamageNumbers.Clear()`, `enum DamageKind`.
- `SaveData`: `RecordBossDefeated(id)`, `GetUnlockProgress`, `GetHero`, `GetRank` / `SetRank`, `selectedDread`,
  `lastHero`, `goldSpent`, `isDreadUnlocked`, `newlyUnlocked`, `newRecords`, `GameSettings.effectsOpacity |
  showEliteBars | joystickOnRight`.
- `MetaShop.upgrades`, `GetPrice`, `CanBuy`, `Buy`, `RefundAll`, `GetTotalRanks`, `Find`, `MetaUpgrade.Describe`.
- Core requests: `S\CORE_REQUESTS.md`, section "UI + meta module".

## 5. QA checklist (play mode; use ui_query_elements / ui_click_element / ui_get_model)

Title (T)
- T1 Play leads to hero select; Armory and Settings open and Back returns to the title; Quit hidden on touch.
- T2 Arrow keys / D-pad move a gold focus ring; Enter / A presses; the mouse hides the ring.
- T3 Gold total and best run text read from the save.

Hero select (S)
- S1 Four cards with portraits, HP / Armor / Speed bars, weapon, signature, active (HeroCatalog texts).
- S2 With `unlockAllHeroes = false` and a fresh save: Barbarian, Mage, Rogue grey with lock and hint; Start says
  LOCKED and errors.
- S3 Left / right selects (heroPreviewChanged fires), Enter / A or a second click starts the run.
- S4 After a victory: Dread - / + appears, value stored in the save.

HUD (H)
- H1 XP bar eases, fills and flashes white on each level-up; level number updates.
- H2 Timer MM:SS, kills pop every 10 kills, gold pops on gain.
- H3 Weapon slots fill in order with 8 pips (passives 5, Echo Quiver 2); evolved slot gold frame + "E"; an
  evolution-ready slot pulses gold.
- H4 Active icon bottom center (bottom right on touch, 96 px) with a clockwise dark sweep; cyan flash + active_ready
  when ready; tapping it fires the active.
- H5 Pause button top right (hidden during level-up / chest); Escape / Start pauses (core).
- H6 Hero health bar above the head (green > 50%, amber > 25%, red).
- H6a Click a weapon / passive slot or the active's "?" badge: play pauses and the details panel shows icon,
  name, ownership tag, rank pips (or EVOLVED), description, every number of the rank, the next rank's changes and
  the evolution recipe with its status; Escape / B / the X / Continue / a click outside resumes.
- H6b Elites show a violet bar with a gold rim over their heads (max 8); Settings > Elite health bars Off hides them.
- H7 Under 30% HP the red vignette grows with a 1 Hz heartbeat, 1.6 Hz under 15%; each hit pulses it.
- H8 Damage numbers: white, yellow "!" crits (bigger), red hero hits that shake, green "+N" heals; rise and fade
  in 0.8 s; rapid hits on one spot merge and re-pop; Settings Off / Big hits / All; no stutter with 3000 enemies.
- H9 Level-up / chest / pause draw over the HUD (render order) and the HUD buttons ignore clicks behind them.

Level-up (L)
- L1 Three cards fly in from below 0.06 s apart (card_flip x3); rarity frame colors (grey / green / blue / violet /
  gold); NEW or RANK a > b tag; description numbers "old > new" in green; evolution hint row with icon.
- L2 Hover / focus lifts a card; click, keys 1-3, Enter / A pick it: flash, icon flies to its HUD slot, then the
  run resumes (or the next level-up opens).
- L3 Reroll (R / Y) only with charges (count shown; refused with a shake + ui_error at 0); new cards fly in.
- L4 Banish (X on the card, X key / gamepad X on the focused card) only with charges; replaces that card.
- L5 Skip (Q / View) closes and adds 5 gold.

Chest (C)
- C1 1 / 3 / 5 reels spin 1.5 / 2.0 / 3.0 s, land left to right with flash in the rarity color, name and tag;
  evolution lands gold with reel_land_evolution.
- C2 Any button while spinning lands all at once; then gold counts up and Continue resumes.

Pause (P)
- P1 Weapons with rank, damage, DPS, kills; passives with effect lines; stat sheet with armor "% less damage";
  boosted stats green.
- P2 Escape / Start / B resumes; Settings returns to pause; Abandon needs two presses and lands on the title.

Results (R)
- R1 FALLEN (red, cause) or VICTORY (gold glow); tiles count up; greed bonus in the gold label.
- R2 Weapon table sorted by damage with share bars; extra line (highest hit, damage taken, chests, elites).
- R3 New unlocks / records line; Retry restarts the same hero; Hero select, Armory (Back returns here), Title.

Armory (A)
- A1 14 upgrades with pips, current bonus, next rank, price = base x rank + 5 x ranks owned (Vigor first rank 70).
- A2 Buying deducts gold, flashes the card, saves at once; too poor: shake + message; maxed shows MAX.
- A3 Refund all (two presses) returns every coin spent; ranks reset.
- A4 A new run gets the bonuses (pause stat sheet shows them).

Settings (G)
- G1 Sliders change Audio.masterVolume and the Music / Sfx / Ui buses live; left / right on a focused row change it.
- G2 Shake 0 disables shake; damage number mode applies at once; values persist after restarting play.

Save (V)
- V1 `Application.persistentDataPath/barrowlight_save.json` written after a run, a purchase, closing settings.
- V2 Corrupt the file by hand: the next start logs a warning, keeps `barrowlight_save.corrupt-<time>.json`, starts fresh.

Phone (F) - Game view preset "Phone 19.5:9" landscape (touch on, phone dpi, 4% left cutout), and on the S26
- F1 Every screen fits inside the safe area with nothing clipped or overlapping: title (3 buttons, stats line
  bottom right, no keyboard hints), hero select (4 cards, Back / Dread / Begin at the bottom), level-up (3 cards
  between title and buttons), chest (5 reels, Continue at the bottom), pause (3 columns; build and stats scroll),
  results (tiles, scrolling table, buttons at the bottom), armory (2 columns of rows, scrolling), settings
  (2 columns of rows, Done at the bottom), details (fills the screen, body scrolls).
- F2 Text reads at about 2 mm or more; buttons are about 7.5 mm tall; the active button about 16 mm.
- F3 HUD: weapon and passive slots in two rows at the top left (pips visible), none in the joystick's bottom-left
  corner; the boss bar sits right of them; the active button is bottom right (bottom left with the joystick on
  the right) and fires on press.
- F4 Tapping a slot pauses and opens its details; tapping outside the panel, the X or Continue resumes; the tap
  does not move the hero or start the joystick.
- F5 Settings > Interface size 80-140% rescales every screen at once (a phone stops growing where the layout
  would no longer fit).
- F6 Ownership tags on cards, reels, details, pause rows and results rows: "KNIGHT UNIQUE" etc. in the class
  colour, "SHARED", "SHARED PASSIVE".

## 6. Known limits

- Mockups are approximations drawn with PIL; RmlUi only renders in the editor, so nothing here was seen in engine.
- No frame-rate cap, vibration or language settings: the engine has no C# API for them.
- The optional "Later" (defer a level-up) is not implemented: it needs the run to resume with a pending pick.
- Damage numbers merge by position, not enemy id (the core API passes no id).

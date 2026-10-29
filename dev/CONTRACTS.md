# Barrowlight - module contracts (read before writing code)

Sample game "Barrowlight" (survivors-like) for UnravelEngine, built through the editor MCP by the lead
agent. Module agents write code and data only; the lead integrates it into the project and the editor.

Root: S = C:\Users\User\AppData\Local\Temp\claude\C--Workspace-github-UnravelEngine-UnravelEngine\4da809e6-fd2c-4f39-9b7b-211eeff32938\scratchpad\survivors

- Design (what to build, numbers): S\design_brief.md
- Engine C# API (exact signatures): S\api_digest.md; engine sources (read freely):
  C:\Workspace\github\UnravelEngine\UnravelEngine\engine_data\data\scripts\**.cs
- Game sources: S\src\Scripts\** (C#), S\src\UI\** (RmlUi rhtml/rcss/png/ttf for the UI module).
- Compile check (must pass with 0 errors before you report): `cd S\check && dotnet build -nologo -v q`
  (compiles every .cs under S\src against the engine's engine-script.dll).
- The project (do NOT write into it; the lead pushes files): C:\Workspace\github\UnravelEngine\UnravelEngine-Sample-Survivors
- Do NOT call the editor MCP server (127.0.0.1:27182) and do not start/kill editor processes: the lead drives it.

## Ownership (edit only your own files)

| Module | Owner | Files |
|---|---|---|
| Core | lead | Core/*, Combat/*, Fx/*, Hero/HeroController.cs, Enemies/EnemyWorld.cs, EnemyTypes.cs, Pickups/PickupWorld.cs |
| Enemies, director, bosses, drops | lead | Enemies/* (other), Pickups/Drops.cs, PickupCatalog.cs |
| Weapons | weapons agent | Weapons/** (Loadout.cs, HeroCatalog.cs, ProjectileCatalog.cs + any new files) |
| UI + meta | UI agent | UI/**, Meta/** (SaveData.cs, MetaShop.cs + new), S\src\UI\** |
| Audio | audio agent (later) | Audio/** |

Stub files in your folders define the PUBLIC API the core already calls: keep every public member the
stub has (you may add members). If you need something from a core file, do not edit it: add a line to
S\CORE_REQUESTS.md (what, why, proposed signature) and work around it meanwhile.

## Conventions

- C# like the engine samples: PascalCase types/methods, camelCase fields, XML doc comments on public
  types and members, `using System; using Unravel.Core;` explicitly (ImplicitUsings is off), namespace
  `Survivors`. ASCII only in every file (no unicode dashes/arrows/quotes; build special chars with
  (char)NN). Comments say what the code does now (no history, no dates).
- `Random` is the engine's per-thread static (Random.Range / Random.Float / insideUnitCircle); use the
  run's `System.Random rng` (Run.current.rng) for anything that must be reproducible per seed
  (offers, chests).
- No engine calls on worker threads. No per-frame allocations on hot paths (weapons tick every frame;
  preallocate buffers; no LINQ in Tick).
- Units: metres, seconds, m/s. A design knockback of "N m" = push speed N * Tuning.KNOCKBACK_SPEED_PER_METRE.
- Time: gameplay uses Time.deltaTime (scaled: 0 while menus are open); UI animation uses unscaled time.

## Core API you build on (read the files)

- `Run.current` (Core/Run.cs): stats (PlayerStats), enemies (EnemyWorld), projectiles (ProjectileWorld),
  pickups, effects (Effects), sounds (Sounds), feedback (Feedback: HitStop(s), Shake(strength, s)),
  cameraRig, gameCamera (CameraComponent), record (RunRecord), hero (HeroController), loadout, director,
  ui (GameUI), save (SaveData), rng, heroClass, phase, runTime, minutes, level, experience,
  experienceFraction, gold, AddGold, AddExperience, StartRun(HeroClass), AbandonRun, Pause, Resume,
  IsUnlocked(HeroClass), ShowTitle, OpenChest.
- Stats (Core/Stats.cs): StatId list = design 1.1; stats[StatId.X]; AddFlat / AddPercent; GetCount;
  GetDamageTaken; armorReduction; activeCooldownFactor. Cooldown stat is a MULTIPLIER (1 = base,
  passives AddPercent(StatId.Cooldown, -0.06)).
- Damage (Combat/Combat.cs): weapons compute `baseDamage * stats[StatId.Might]` and call
  Combat.DamageEnemy(row, damage, pushVelocity, source) / DamageCircle / DamageCone / DamageCapsule.
  Crits, target multipliers (frozen, marked), numbers, sparks and hit sounds happen inside.
  `source` = the weapon's slot (0..5) for the results table; actives/passives/explosions use
  RunRecord.OTHER_SOURCE.
- Enemies (Enemies/EnemyWorld.cs, public section): QueryCircle / QueryCapsule / QueryCone /
  QueryNearest / QueryRandom / FindCluster, GetPosition / GetRadius / GetHeight / IsElite /
  IsTargetable / GetHealthFraction, Damage (prefer Combat), Slow, Freeze, Stun, Burn, Mark,
  PushAway, GetEnemy / GetRow (stable handles across frames: store `Enemy`, resolve with GetRow).
  Rows are only valid during the frame you got them.
- Projectiles (Combat/ProjectileWorld.cs): register looks in ProjectileCatalog (weapons module owns it),
  spawn with ProjectileSpawn (motions Linear, Homing, Boomerang, Orbit, Arc, Static; pierce, rehit
  interval, handler for custom hit/expire logic: implement IProjectileHandler on your weapon).
- Effects (Fx/Effects.cs): particle emitters found by entity NAME; resolve ids once with
  effects.Find("Name") in your constructor, then effects.Emit(id, position, count[, color[, size,
  lifetime]]) / EmitMoving. The lead builds every emitter you list in your manifest.
- Sounds (Fx/Sounds.cs): sounds.Play("name") / PlayAt("name", position); throttled inside. The audio
  module registers every name you list in your manifest.
- Hero (Hero/HeroController.cs): position, facingDirection, lastMoveDirection, currentVelocity,
  isMoving, PlayAttack(upperState), PlayFullBody(state, seconds, stopsWalking), Dash(direction,
  distance, seconds, invulnerable), Teleport, SetInvulnerable, Heal, events damaged / blocked / died.
- Hero animation controller states (the lead builds them per class with these exact names):
  - Upper layer (upper body, legs keep walking): Slash (1H slice horizontal), SlashDiagonal, Chop (1H chop),
    Stab (1H stab), HeavyChop (2H chop), HeavySlice (2H slice), DualChop, DualSlice (dual-wield),
    Shoot (1H ranged shoot), Throw, CastShoot (spellcast shoot), CastRaise (spellcast raise),
    CastLong (spellcast long), Block (block hit).
  - Base layer, full body: Locomotion (idle/walk/run blend), Dodge (dodge forward), Spin (2H spinning,
    loops), Leap (jump), Cheer, Death, Hit.

## Shared naming

- Item ids: snake_case of the design's English name, apostrophes dropped: "Oathblade Arc" ->
  oathblade_arc, "Assassin's Mark" -> assassins_mark, "Iron Heart" -> iron_heart; evolutions too
  ("Final Verdict" -> final_verdict); actives (shield_rush, earthshaker_leap, blink, smoke_roll),
  signature passives (bulwark, bloodlust, arcane_echo, momentum), fallbacks (hearty_meal, coin_purse,
  battle_hymn), heroes (knight, barbarian, mage, rogue).
- Icons: app:/data/UI/Icons/<id>.png (the UI module makes one per id). UpgradeOffer.icon holds that key.
- Effects: PascalCase emitter names (e.g. "OathbladeArcSlash", "EmberOrbExplosion"). Core already uses:
  HitSpark, CritSpark, BoneBurst, LevelUpRing, ReviveBurst.
- Sounds: snake_case (e.g. "oathblade_swing", "ember_orb_boom"). Core already uses: hit, hit_crit,
  enemy_death, level_up, card_pick, chest_open, run_start, hero_hurt, revive.
- Projectile looks: PascalCase names in ProjectileCatalog; meshes from KayKit props
  (app:/data/Props/Adventurers/gltf/<file>.gltf: arrow, arrow_bundle, axe_1handed, axe_2handed,
  crossbow_1handed, crossbow_2handed, dagger, mug_empty, mug_full, quiver, shield_badge(_color),
  shield_round(_color|_barbarian), shield_spikes(_color), shield_square(_color), smokebomb,
  spellbook_closed, spellbook_open, staff, sword_1handed, sword_2handed(_color), wand), loaded with
  Assets.GetAsset<Mesh>(key), or engine primitives Assets.GetAsset<Mesh>("engine:/embedded/<name>")
  with name in: sphere, cube, cube_rounded, plane, cylinder, cone, torus, capsule_1m, icosahedron,
  dodecahedron, icosphere0..icosphere19. Other shapes: ask the lead for a generated mesh (manifest). Glowing spell
  meshes use a MaterialInstance with emissive color on a shared material the lead provides:
  app:/data/Materials/SpellGlow.mat (white, emissive 1) - tint per look with MaterialInstance.
  Enemy looks the enemy module uses: "EnemyBolt", "HexOrb" (register them too).

## Manifest (every module delivers S\manifest_<module>.md)

Tables of what the lead must create, one row each:
- Effects: name, what it shows, look (blend additive/alpha, texture idea, color, size m, lifetime s,
  count per emit, motion, render mode billboard/horizontal/stretched).
- Sounds: name, description, layers, length, variants.
- Projectile looks / meshes: name, mesh, scale, material tint, spin/align.
- Hero states used per class.
- Anything else (UI documents and their entity names, input actions, scene entities).

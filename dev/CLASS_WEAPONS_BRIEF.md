# Barrowlight class update - brief for class agents (one agent per class)

S = C:\Users\User\AppData\Local\Temp\claude\C--Workspace-github-UnravelEngine-UnravelEngine\4da809e6-fd2c-4f39-9b7b-211eeff32938\scratchpad\survivors

You own ONE class (Knight, Barbarian, Mage or Rogue) and build its two new class weapons, each with its evolution,
complete: gameplay, synergies with the class, and great visuals. The user asked for class abilities that "feel
impactful and very fun to play, with great visuals and synergies". Think like the game designer of this game: every
moment needs anticipation, impact, reaction and aftermath, and the new weapons must make the class's existing kit
(signature passive, active, other class weapons) better and more interesting together.

## Read first

1. `S\design_update_2026-10-29.md`: the update. Section 5 = your class's two weapons; section 2 = healing rules
   (do NOT add new healing beyond what the design says).
2. `S\VFX_BRIEF.md` sections "What already exists" and "Art direction" (the measured intensity limits, layer recipe,
   palettes, budgets). Its "Workflow and rules" section is REPLACED by this brief.
3. Your class's existing code as the pattern to match: `S\src\Scripts\Weapons\<Class>\*.cs` (weapons, <Class>Looks.cs,
   <Class>Catalog.cs), `S\src\Scripts\Weapons\Kits\<Class>Kit.cs`, and `S\build_effects_spells_<class>.py` (Mage: its
   effects are in `S\build_effects_spells.py` group MageFx). Framework: `S\src\Scripts\Weapons\Framework\Weapon.cs`,
   `Strikes.cs`, `GroundZones.cs`, `WeaponFeel.cs`, `S\src\Scripts\Fx\Effects.cs`, `S\src\Scripts\Combat\*.cs`.

## What is already in place (by the lead)

- Catalog entries with rank tables and evolutions: `Weapons\<Class>\<Class>Catalog.cs` (you own this file: tune numbers
  and descriptions; keep 8 rank rows; columns are what the level-up card shows).
- Stub weapon classes (`Weapons\<Class>\<Name>.cs`, Cast returns false) already registered in WeaponFactory.
- Icons for both weapons and evolutions (app:/data/UI/Icons/<id>.png).
- Enemy statuses in EnemyWorld you can use: Expose(row, bonus, seconds) (+damage taken, no mark look),
  AddVenom(row, stacks, damagePerStack, seconds, maxStacks, source) / GetVenomStacks / ConsumeVenom, Bloody(row,
  seconds) / IsBloodied (EnemyDeath.wasBloodied on death), plus the existing Stun, Freeze, Slow, Burn, Mark, Confuse
  (wander, no attacks), PushAway, Pull, Launch, IsBurning, IsFrozen, GetSlowFactor, QueryCircle/Capsule/Cone/Nearest.
- `run.combatEvents.hit` (Combat/CombatEvents.cs): every hero hit with (row, source, amount, isCritical). Handlers run
  INSIDE the damage call: only change state there; queue any damage you want to cause and deal it in your weapon's
  Update/OnEvent next tick (otherwise you recurse). Subscribe in the weapon constructor; runs clear the event.
- Class kits: you may ADD small public members to your class kit (e.g. `AddFury(float)`, a bash radius scale, an
  event when the smoke cloud drops). Do not change the existing healing code there (Fury heal cap, Blood Price, block
  heal, Arcane Ward, Bloodletting) or other behaviour.

## Files you own (write only these)

- `S\src\Scripts\Weapons\<Class>\<YourWeapon1>.cs`, `<YourWeapon2>.cs`, `<Class>Catalog.cs`, `<Class>Looks.cs`, and new
  helper files you add under `S\src\Scripts\Weapons\<Class>\`.
- `S\src\Scripts\Weapons\Kits\<Class>Kit.cs`: additive changes only, as above.
- A NEW effects builder `S\build_effects_spells_<class>_v2.py` (knight, barbarian, mage, rogue) with ONE registry group
  `<Class>Fx2` (KnightFx2, BarbarianFx2, MageFx2, RogueFx2); every new emitter name starts with your class name
  (Knight..., Barbarian..., Mage..., Rogue...). Reuse existing emitters by name where they already look right.
- Everything else (EnemyWorld, Combat, Run, HeroController, Loadout, WeaponCatalog.cs, Weapon.cs, Strikes.cs, other
  classes' folders, UI) belongs to the lead or other agents who are editing them right now. Do not edit them. If you
  truly need a core change, work around it inside your files if at all possible, and list it in your final report.
- Sounds: reuse existing sound ids from `S\src\Scripts\Audio\SoundCatalog.cs` (no new audio files).

## The editor (shared by 4 agents and the lead)

One editor runs the project (MCP on http://127.0.0.1:27182). EVERY command that talks to it goes through the lock:

    cd S
    python editor_lock.py <class> python push.py scripts
    python editor_lock.py <class> python build_effects_spells_<class>_v2.py build
    python editor_lock.py <class> python gallery.py <Hero> <weapon_id> [...]

- `push.py scripts` pushes ALL game scripts and waits for the compile. Before pushing, run the offline compile check
  `cd S\check && dotnet build -nologo -v q` (0 errors). If the check fails in a file you do not own, someone else is
  mid-edit: wait a minute and retry; never edit their file.
- `gallery.py Hero id` captures rank 1 and evolved in slow motion into `S\shots\gallery\<id>.png`; look at it (Read the
  png) and iterate until it looks great. It uses Run.DevWeapon(id, rank, evolve), DevCrowd, DevTimeScale.
- Keep each locked command short; never leave play mode running; never save or open scenes yourself (the builder
  saves what it creates). Other agents' scripts may be in any state: judge only your own weapon in the gallery.

## Quality bar

- Gameplay: implement exactly the mechanic and synergies of the design (section 5), with numbers in the ballpark of the
  class's existing weapons of the same rarity (Uncommon ~ the class's #2 weapon, Rare ~ its #3). Evolutions must be a
  clear power spike and change the behaviour, not only numbers. Amount (Echo Quiver / boons), Area, Duration,
  Projectile Speed, Cooldown and Might must apply the way the tags say (Weapon helpers: MightOf, AreaOf, DurationOf,
  SpeedOf, amount, KnockbackOf, cooldown). Arcane Echo applies automatically to Mage class weapons.
- Visuals: follow the art direction; rank 1 already reads clearly; ranks grow with fxScale; the evolution adds new
  signature layers and accents. Use RequestAttack for an upper-body animation where it fits. Respect the particle
  budgets and the intensity limits (no white-out under the bloom).
- Performance: no allocations per frame (pre-size arrays, reuse scratch spans), late game has 1000+ enemies.
- Code style: match the existing C#: constants ALL_CAPS at the top, doc comments describe the current design (no
  history), full words, ASCII only, LF line endings. UI text (descriptions) are full sentences ending with '.'.
- Check before you finish: offline compile 0 errors; builder lists and builds without error; galleries of both
  weapons (rank 1 and evolved) look great; a quick functional sanity check that synergies trigger (e.g. log a count
  once, then remove the log).

## Final report (your last message)

Per weapon and evolution: what it does now (mechanics + synergies), the visual layers per moment, emitter names, the
final rank table if you changed it, kit changes, the gallery image paths, anything you could not do, and core requests.

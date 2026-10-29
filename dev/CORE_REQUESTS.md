# Core requests

Status (lead): every item below is done except where noted here.
- UI 5: effectsOpacity fades the hero's effect groups (Effects.heroOpacity), showEliteBars via GameUI.ShowEliteBar,
  joystickOnRight picks the SampleTouchJoystick side (both sticks move the hero).
- Weapons 13: not done as loops; the fury_max / momentum_max one-shot stings play when the meters fill.
- Bosses 4: Effects.EmitMoving with a color exists; BossFx keeps its own lookup (fine as is).
- Bosses 5: not done; KingVictory's onCollect swap stays (it restores in Clear).
- Audio 4: not needed (MusicDriver plays the menu track). Audio 6: no engine filter; the volume drop stays.


## UI + meta module (GameUI, DamageNumbers, SaveData, MetaShop)

The UI works without these (it degrades gracefully); each line makes a screen or rule complete.

1. **Boss bars** (design 7.3): call `run.ui.SetBossBar(name, title, healthFraction, phaseMarks)` every frame while
   the Hollow King (marks `{0.65f, 0.30f}`) or a mini-boss (marks empty array or null) is alive, and
   `run.ui.HideBossBar()` when it dies. Pass the same `float[]` instance every frame (a new array rebuilds the pips).
   Example: `ui.SetBossBar("Hollow King", "The Hollow King", hp / maxHp, BOSS_MARKS)`; mini-boss:
   `ui.SetBossBar("Gravewarden", "Mini-boss", f, null)`.
2. **Off-screen markers** (design 11.3): `run.ui.ShowMarker(worldPosition, MarkerKind.Elite | Chest | Boss | Treasure)`
   every frame in `StepPlaying` for each alive elite, each chest on the ground, the boss and the Crimson Cluster
   (8 per frame max; targets on screen draw nothing).
3. **Unlocks** (design 9): when they die, call `run.save.RecordBossDefeated(SaveData.BOSS_GRAVEWARDEN)`,
   `SaveData.BOSS_ARCHMAGE`, `SaveData.BOSS_HOLLOW_KING`. The run end (`save.OnRunEnded`) then unlocks Barbarian /
   Rogue; Mage unlocks from `run.level >= 20` with no hook.
4. **Dread** (design 1.2, 9): `run.save.selectedDread` (0-5, already 0 until the first victory) is set by hero select
   before `StartRun`; the director / enemy health should read it at `StartRun` (`dread_k`).
5. **Settings the UI cannot apply itself** (`run.save.settings`, `GameSettings`):
   - `screenShake` is 0..1; Feedback only has `isShakeEnabled` (the UI sets it to `screenShake > 0`). Proposed:
     `public float shakeScale = 1;` on Feedback multiplying `strength` in `Shake`; GameUI.ApplySettings would set it.
   - `effectsOpacity` (1 / 0.6 / 0.3): hero VFX opacity (design 10.8), for Effects / projectile looks.
   - `showEliteBars` (bool): elite health bars over heads.
   - `joystickOnRight` (bool): side of the touch joystick (SampleTouchJoystick area / position).
   Re-read them when `GameUI.settings` closes (or every StartRun).
6. **Reroll / Banish charges** (weapons module, design 2.8): GameUI spends them with
   `stats.AddFlat(StatId.Reroll, -1)` / `stats.AddFlat(StatId.Banish, -1)` around `loadout.RollLevelUp` and
   `loadout.Banish`. Loadout must NOT also spend them. The mini-boss reroll refund is `stats.AddFlat(StatId.Reroll, 1)`.
   `Loadout.Banish` may return null when the pool is empty; the card is then removed.
7. **Damage number kinds** (design 10.3): `DamageNumbers.Show(world, amount, DamageKind.Burn | Frost | Echo)` exists
   next to `Show(world, amount, isCritical)`; Combat could pick the kind (burn tick source, frozen target, echo cast).
   Hits are merged by position (0.9 m, 0.25 s) since no enemy id is passed.
8. **Hero preview** (design 11.2, optional): `run.ui.heroPreviewChanged += heroClass => ...` fires when hero select
   focuses a class, for a turntable model behind the cards.
9. **Screen flash** (optional): `run.ui.FlashScreen(false)` white (Holy Bell, revive), `FlashScreen(true)` red.
10. **Edge warnings**: `run.ui.ShowEdgeWarning(directionFromHero, 2.0f)` 2 s before Swarm Rushes / archer volleys
    (design 6.4); it plays `danger_warning`.

## Weapons module (Weapons/**)

Every item works today with the workaround named; each request removes a workaround or completes a design line.
All weapon damage goes through `Strikes.Hit` (Weapons/Framework/Strikes.cs), so items 1-2 change one file.

1. **Hit options on Combat.DamageEnemy** (design 3.4, 10.1, 10.3, S3). Proposed:
   `public struct HitOptions { public bool canCrit; public bool forceCrit; public float critChanceBonus; public float critDamageBonus; public DamageKind numberKind; public bool hideNumber; }`
   and `public static DamageResult DamageEnemy(int row, float damage, Vector3 push, int source, in HitOptions options, out bool isCritical)`.
   Why: Smoke Roll's 3 auto-crits and Death Sentence's always-crit (the CritChance stat caps at 75%), Widowmaker
   +15% crit chance, Repeater Crossbow R7+ +25% crit damage, crit-only effects (20 ms freeze, Widowmaker ricochet),
   Bramble Halo ticks under 10 hide their numbers, echo hits violet / burn orange / frozen cyan (DamageNumbers already
   has DamageKind). Workaround: Strikes rolls those crits itself and calls DamageEnemy(canCrit: false), adding CritSpark
   and hit_crit (the number stays white); plain hits detect Combat's crit from the record's damage delta.
2. **EnemyDeath.wasFrozen / wasMarked** (design 3.3 M3, Absolute Zero). BeginDeath clears freeze and mark before
   onDeath, so a death cannot tell it was frozen. Workaround: Strikes remembers the rows the hero froze (generation +
   end time); Frost Hourglass freezes do not shatter.
3. **ProjectileWorld enemy-projectile query and silent removal** (design 3.1 K2 Aegis Orbit / Bastion Halo).
   Proposed: `public int QueryCircle(Team team, Vector3 center, float radius, Span<int> rows)` (or `public int rowCount`)
   and `public void Remove(int row)` (ends a projectile without its handler's OnExpire). Workaround: shields scan rows
   0..4095 with IsAlive every frame while shields exist and call Expire, which runs the enemy handler's OnExpire:
   please make sure a blocked HexOrb / EnemyBolt does not detonate or hurt the hero from OnExpire.
4. **PickupWorld.AttractWithin(Vector3 center, float radius)** (Wisp Choir: wisps carry gems within 15 m to the hero).
   Workaround: +3 m Pickup Radius while Wisp Choir is owned (stat cap 6 m).
5. **EnemyWorld.Confuse(int row, float seconds)**: no target, wander, no ability casts (Smoke Roll cloud, design 3.4).
   Workaround: March(row, per-row random direction, false, 1.4 m/s, 0.5 s) every 0.25 s while inside; enemies can
   still cast if the hero stands in range.
6. **EnemyWorld.Pull(Vector3 center, float radius, float speed)** with falloff lerp(1, 0.3, d / r) (Maelstrom).
   Workaround: three nested PushAway calls with negative speed (0.3 / 0.35 / 0.35 shares of the impulse).
7. **EnemyWorld.Launch(int row, float height)** (Fault Line pops enemies 0.5 m up, design 3.2 B3). Workaround: stun only.
8. **DamageNumbers.ShowText(Vector3 world, string text, DamageKind kind)** for "EXECUTED" (Death Sentence).
   Workaround: ExecuteSlash effect + execute sound.
9. **HeroController hop and glow**: `Dash(direction, distance, seconds, invulnerable, float arcHeight)` (a visual model
   hop for Earthshaker Leap) and `SetGlow(Color color, float intensity, float seconds)` (Shield Rush gold shield glow,
   Bloodlust red eyes at 50 Fury). Workaround: particles (RushGlow, FuryEmbers).
10. **Feedback hit-stop gap** (design 10.1 "max one global hit stop per 0.30 s"): weapons and actives gate theirs in
    WeaponFeel; core stops (elite death, boss phases) are not gated with them. Proposal: move the 0.30 s gate into
    Feedback.HitStop (with a bypass for boss phase changes).
11. **Run.StartRun**: call `loadout?.Dispose()` before building the new loadout (KnightKit unsubscribes hero.blocked).
    Harmless today because the hero entity is respawned every run.
12. **Hero controller (optional)**: an Upper state "Reload" (crossbow reload every 4th volley, cosmetic) and an upper
    spin flourish for Fan of Knives (design names Melee_Attack_Spin on the upper body; it uses DualSlice now).
13. **Audio hooks (for the audio module, not core)**: `run.loadout.isFuryFull` (Bloodlust heartbeat drum layer) and
    `run.loadout.signatureStacks == 10` on a Rogue (Momentum wind loop).

## Bosses module (Enemies/BossManager.cs, Bosses/**)

Every boss works today with the workaround named; each line removes a workaround or completes a design line.

1. **EnemyWorld: proxy rows never thaw** (bug). `StepRows` `continue`s in the `kind.isProxy` branch before the
   `freezeTime -= dt; stunTime -= dt;` countdown, so after the first Freeze a boss (or lantern) row stays frozen
   forever: `IsFrozen` is always true and `GetDamageTakenMultiplier` keeps the x1.25 frozen bonus. Fix: count both
   timers down in the proxy branch too (e.g. move them into `TickStatus`). Workaround: Boss divides its damage-taken
   scale by `Tuning.FROZEN_DAMAGE_TAKEN` while the row reads frozen, and only the first freeze becomes the design's
   30% / 1 s slow (each later freeze needs the row to thaw first).
2. **EnemyWorld.GetSlowFactor(int row)** => `slowTime[row] > 0 ? slowFactor[row] : 1` (HIGH: the main freeze path).
   Why: weapons turn every freeze on a boss into `Slow(bossRow, 0.5, 1.0)` (Strikes.Freeze), Frost Hourglass calls
   `Slow(bossRow, 0.5, 5)` ("bosses slowed 50%", design 8) and Bramble Halo slows, but a proxy never moves itself and
   the boss cannot read the slow. Once it exists, Boss.speedFactor (Bosses/Boss.cs) multiplies it in (one line).
   Workaround: none, slows do nothing to bosses; only direct `Freeze` calls become the 30% / 1 s slow (see 1).
3. **EnemyWorld: burn ignores the damage-taken scale**. `TickStatus` subtracts `burnDps * dt` directly, so a burning
   Hollow King loses health through his 2.5 s phase-change invulnerability (`SetDamageTakenScale(row, 0)`). Fix:
   multiply the tick by `damageTakenScale[row]` (or `GetDamageTakenMultiplier(row)`). Workaround: none (small).
4. **Effects.EmitMoving with a color**: `EmitMoving(int id, Vector3 position, Vector3 velocity, int count, Color color,
   float size, float lifetime)`. Why: oriented danger decals (cone, lane, arc, gapped rings) need a heading AND a tint
   (they turn violet in the enrage, design 7.1). Workaround: Bosses/BossFx.cs finds the emitters under
   `run.effectsRoot` by name itself and fills `EmitParams` directly; it could route through Effects once this exists.
5. **Run: hold level-ups during the victory** (design 7.4 "all gems vacuum", then the victory screen after 4 s).
   Proposed: `public bool holdLevelUps;` checked next to `pendingLevelUps > 0` in `StepPlaying`. Workaround:
   Bosses/KingVictory.cs swaps `run.pickups.onCollect` for its own handler while the sequence plays (gems only chime,
   every other pickup goes to the previous handler) and restores it before `OnBossDefeated` (and in `Clear`).
6. **ProjectileSpawn.sourceName** (string) for enemy projectiles, set into `director.lastDamageSource` when it hurts
   the hero. Why: an Archmage orb kill names whatever hurt the hero before. Workaround: none.
7. **SaveData (UI/meta owner): boss unlocks never reach `newlyUnlocked`**. `RecordBossDefeated` sets
   `gravewardenDefeated` / `archmageDefeated` at once, and `OnRunEnded` snapshots `wasUnlocked` from those same
   fields, so Barbarian / Rogue unlocked during the run are already "unlocked before". Fix: snapshot at `StartRun`, or
   keep the kills in a pending set that `OnRunEnded` applies after its snapshot. Bosses call `RecordBossDefeated`.
8. **Asset checks (lead files)**: `Assets.GetAsset<T>` never returns null (a missing key gives `uid == Guid.Empty`),
   so `EnemyCatalog` `type.material == null` and `WaveDirector` `lanternPrefab == null` never fire. Bosses test
   `prefab.uid != Guid.Empty`.

## Audio module (Audio/**: SoundCatalog, MusicPlayer, MusicDriver)

Everything plays today with the workaround named; each line completes a design 10.7 / 10.4 rule.

1. **Sounds: volume jitter +-2 dB per instance** (design 10.7; Fx/Sounds.cs). Proposed in `Play` / `PlayAt`:
   `volumeScale *= MathF.Pow(10.0f, ((float)random.NextDouble() * 4.0f - 2.0f) / 20.0f);` (skip it for UI and the gem
   chain if wanted: a `volumeJitterDb` field on SoundDef, default 2). Workaround: none (pitch jitter only).
2. **Sounds: one voice budget per sound, not per clip**. `Register` passes `limit` to `Audio.SetClipLimit` for every
   variant, so a 6-variant "hit" with limit 8 could reach 48 voices. Workaround: SoundCatalog passes
   ceil(limit / variants) as the per-clip limit. Proposed: keep a small ring of `AudioVoice` per SoundDef and `Stop()` the
   oldest when `def.limit` voices still play.
3. **Sounds.Register: missing clips are not reported**. `Assets.GetAsset<AudioClip>` never returns null (a missing key has
   `uid == Guid.Empty`), so `clip == null` never fires and a typo'd key registers a silent clip. Proposed:
   `if (clip == null || clip.uid == Guid.Empty)`. verify_catalog.py checks every key the catalog builds exists today.
4. **Run.ShowTitle (optional)**: `MusicPlayer.Play(this, MusicPlayer.Track.Menu);`. Not needed: MusicDriver starts the
   menu track whenever `run.phase == RunPhase.Menu`; the call would only make it explicit.
5. **Import**: the music loops are `app:/data/Audio/music/battle_loop.flac`, `menu_loop.flac` (lossless FLAC, verified
   through the engine's dr_flac) and `boss_loop.ogg`; the importer takes .flac / .ogg like .wav.
6. **Low-health music low-pass** (design 10.4, 800 Hz under 15%): the engine has no bus or source filter. Workaround:
   MusicDriver lowers the battle track to 70% under 15% health and plays the heartbeat at 1 / 1.6 Hz itself.

# Barrowlight - bosses module manifest

Everything the bosses module (src/Scripts/Enemies/BossManager.cs, src/Scripts/Bosses/**) needs the lead to build.
Names are exact. Effects are resolved once by entity name under `run.effectsRoot` (Bosses/BossFx.cs, at the first
boss spawn: a missing name logs one warning and does nothing), sounds go through `run.sounds.PlayAt/Play(name)`, the
orb look through `projectiles.FindVisual(name)`. The game runs before the art lands.

No scene entities and no prefab changes are needed: bosses instantiate `app:/data/Prefabs/Bosses/Boss_*.pfb`, tint
them at runtime with per-renderer MaterialInstances (body slot 0 base color, eye slot 1 emissive, held props base
color) and drive their proxy rows (EnemyCatalog.bossTypes, already registered).

Conventions below (same as build_effects.py):
- Every effect is a WORLD-space burst emitter (`burst(...)`: emission_rate 0, always_simulate) under the Effects root.
- "Tinted" = the script passes a color: keep the gradient WHITE with the alpha shape given. Danger decals get
  `Tuning.DANGER_COLOR` (red-orange) and VIOLET (0.62, 0.25, 1.0) while the Hollow King is enraged (design 7.1), so
  every boss telegraph gradient must stay white.
- "Untinted" = the script passes white or no color: the gradient carries the color.
- "Scripted size / life" = EmitParams size (m, the DIAMETER for rings and decals; the LENGTH for the lane) and
  lifetime (s, the wind-up for telegraphs): size curves are normalised, lifetime range 1.0 (overridden).
- "Oriented" = fired with velocity = heading x 0.001 (`align_to_direction=True`, `velocity_gradient=span((1,1))` as
  TelegraphCone): the texture's +V points along the heading.
- Design 7.3: boss danger decals read at 45% alpha vs 35% for the crowd. The boss-only telegraphs below (Lane, Arc,
  ArcEdge, SoulNovaWarn, SoulNovaRing) are specified about 30% more opaque than TelegraphFill / TelegraphRing.
- Telegraph fills end with the alpha spike at 0.97 like TelegraphFill: the "white flash at the hit moment".

## 1. Effects

### 1.1 Existing emitters the bosses reuse (no change needed unless noted)

| Name | Boss use | How it is fired |
|---|---|---|
| TelegraphRing + TelegraphFill | Bone Rain (r 1.8), Grave Field (r 2.5), Death Spiral wind-up (r 3.5); TelegraphRing alone follows the spinning King (r 3.5, life 0.2 s, 10 Hz) | tinted danger / violet, scripted size + life. Peak alive: ~40 each (16 rain circles + enrage extras + spin trail): max 256 is fine |
| TelegraphCone | Gravewarden Overhead Cleave (120 deg, 3.5 m, 0.9 s) | oriented, UNTINTED (its gradient is already DANGER), size 7 m |
| EnemySwing | Gravewarden cleave swing | oriented, white, size 7 m, life 0.2 s |
| GraveBurst | Hollow Archmage Grave Field impacts (5 at once) | tinted (1, 0.35, 0.2) like the Hex Casters, size 5.5 m, life 0.6 s |
| SummonMotes | Archmage Summon Guard (8 points x 4 motes / 0.5 s), King Raise the Legion (30-38 points x 3 motes / 0.5 s) | plain bursts at each point. Please raise max_particles 512 -> 1024 (a Raise alone keeps ~320 alive next to the Hex Casters') |
| BoneBurst | shards at every hazard impact (5), every slam spike (3), every death-wave kill (6, max 40 kills per frame), every add the phase shockwave kills (6) | plain bursts. max 2048 may saturate during the victory wave (excess is just dropped) |
| EliteBeam | mini-boss chest drop, the King's chest at the hero's feet | plain burst |

### 1.2 New telegraphs (group "Telegraphs")

| Name | Shows | Blend | Texture | Color | Size (m) | Life (s) | Count / emit | Motion | Render, sort |
|---|---|---|---|---|---|---|---|---|---|
| TelegraphLane | Rectangle 2 x 10 m (Gravewarden Shield Charge) / 3 x 14 m (King Grave Slam) that fills from its near end to its far end over the wind-up | additive, intensity 1.1 | lane_fill_4x4.png (new, below), texture_sheet_tiles [4, 4], texture_sheet_cycles 1 (frame 0 at birth, frame 15 at death) | tinted; alpha keys (0: 0.6), (0.9: 0.8), (0.97: 1.0), (1.0: 0.0) | scripted = lane LENGTH (10 / 14); size curve constant 1 | scripted (1.0 charge; 1.2 / 1.08 / 0.96 / 0.72 slam) | 1 | none (oriented, fired at the lane's center) | horizontal_billboard, align_to_direction, max 32, sort -13 |
| TelegraphArc | Fill of the King's Cleave Sweep: 200 deg sector, radius 6.5 m, growing from the King outward | additive, intensity 0.9 | cone_200.png (exists in gen_telegraphs.py) | tinted; alpha keys as TelegraphFill: (0: 0.35), (0.9: 0.65), (0.97: 1.0), (1.0: 0.0) | scripted = 13 (diameter, apex at the quad center); size curve span((0.1, 0.1), (1.0, 1.0)) | scripted (1.0 / 0.9 / 0.8 / 0.6) | 1 | none (oriented) | horizontal_billboard, align_to_direction, max 16, sort -13 |
| TelegraphArcEdge | Outline of the same arc at full reach from the first frame | additive, intensity 2.2 | cone_200_edge.png (new) | tinted; alpha keys as TelegraphRing: (0: 0.0), (0.08: 1.0), (0.92: 1.0), (1.0: 0.0) | scripted = 13; constant | scripted, same as TelegraphArc | 1 | none (oriented) | horizontal_billboard, align_to_direction, max 16, sort -12 |
| SoulNovaWarn | The three Soul Nova rings during the wind-up (radii 4 / 5.5 / 7 m), each with two opposite 60 deg gaps; ring k turned 30 deg from ring k-1 so the player reads the safe lanes | additive, intensity 2.2 | ring_gapped_60.png (new) | tinted; alpha keys (0: 0.0), (0.1: 1.0), (0.92: 1.0), (1.0: 0.0) | scripted (8 / 11 / 14); constant | scripted (1.25 x scale: 1.0 in phase 3, 0.75 enraged) | 1 per ring | none (oriented: +V = gap center) | horizontal_billboard, align_to_direction, max 16, sort -12 |
| SoulNovaRing | One SEGMENT of an expanding Soul Nova ring. The script launches ~21 segments per arc (2 arcs per ring, 3 rings) radially at 6 m/s from the King, so the band keeps its 1.2 m thickness and the 60 deg gaps stay exact while it grows to 20 m | additive, intensity 2.5 | nova_segment.png (new) | tinted; alpha keys (0: 0.0), (0.03: 0.9), (0.85: 0.8), (1.0: 0.0) | scripted = 1.2 (the band thickness, V); initial_scale_3d (size_axes) [1.75, 1, 1] so U = 2.1 m along the ring | scripted 3.33 (20 m / 6 m/s) | 1 per segment (126 per Nova) | launched by the script: velocity = radial x 6 m/s; speed_over_life constant 1, no drag, no gravity | horizontal_billboard, align_to_direction (V = radial, so the bar lies along the ring), max 256, sort -11 |

New telegraph textures (white masks, same generator style as tools/gen_telegraphs.py, 256 px unless noted):
- `lane_fill_4x4.png` (1024 x 1024, 16 tiles of 256): in every tile a rectangle centered horizontally, width 3/14 of
  the tile (21.4%), full tile height; a crisp 2 px edge on all four sides in every frame (alpha 1); the interior filled
  at alpha 0.45 from the BOTTOM edge up to (k + 1) / 16 of the height in tile k (row-major), tile 15 fully filled. The
  bottom of the tile (-V) is the near end at the boss.
- `cone_200_edge.png`: only the rim arc and the two side edges of cone_200.png (the `rim` and `side` terms of
  `cone()`), no body fill.
- `ring_gapped_60.png`: ring_thin.png's band with two gaps of 60 deg centered on +V and -V (soft 3 deg gap ends).
- `nova_segment.png`: a soft bar: alpha 1 across the full height with a 10% soft edge at top and bottom, a 20% soft
  fade at the left and right ends, a 1.3x brighter center line (blend the overlapping segments into one band).

### 1.3 New boss effects (group "Bosses")

| Name | Shows | Blend | Texture | Color | Size (m) | Life (s) | Count / emit | Motion | Render, sort |
|---|---|---|---|---|---|---|---|---|---|
| BossSlamDust | Dust shock ring of heavy impacts (charge trail every 0.08 s and stop, cleave impacts, the slam) | normal, lighting surface | {V}/smoke_puffs_4x4.png, tiles [4, 4] randomized | untinted dust (0.82, 0.76, 0.66); alpha (0: 0.0), (0.1: 0.7), (1.0: 0.0) | scripted (2.2 charge trail, 3.3 charge stop, 3 warden cleave, 6.5 King cleave, 7 slam); size curve punch((0.3, 0.3), (1.0, 1.0)) | scripted (0.5 - 1.0) | 1 | none; start_rotation 0-360 | horizontal_billboard, max 64, sort -8 |
| BossRiseDust | Dust and rubble cloud while a boss climbs out of the ground (every 0.6 s during the rise) and when a corpse finishes sinking | normal, lighting volumetric, soft_fade 1.0 | {V}/smoke_billow_4x4.png, tiles [4, 4] randomized | untinted dust; alpha (0: 0.0), (0.15: 0.55), (1.0: 0.0) | scripted 3 x body radius (3.3 warden, 3.0 archmage, 6.0 King); size curve 0.6 -> 1.2 | scripted (1.0 - 3.0) | 1 | none; start_rotation 0-360, spin 5-12 deg/s | billboard, max 32, sort -5 |
| BossCleaveTrail | The King's 200 deg axe sweep at the hit moment | additive, intensity 3.0 | {V}/arc_8x1.png, tiles [8, 1] randomized (as EnemySwing) | untinted (1.0, 0.8, 0.7); alpha (0: 0.0), (0.15: 0.85), (1.0: 0.0) | scripted 13; size curve 0.8 -> 1.0 | scripted 0.3 | 1 | none (oriented); rotation [90, 90] as EnemySwing | horizontal_billboard, align_to_direction, max 8, sort 1 |
| BoneSpike | Bone spikes bursting out of the ground along the Grave Slam lane, one per metre at 25 m/s | normal, lighting surface, alpha cutoff | bone_spikes.png (new: 3-4 jagged bone spikes rising from the bottom edge, bone white with a dark 2 px outline) | untinted bone (0.95, 0.9, 0.78); alpha (0: 1.0), (0.8: 1.0), (1.0: 0.0) | scripted 2.2; size keys (0: 0.1), (0.12: 1.0), (0.7: 1.0), (1.0: 0.0) (erupt in 0.08 s, sink at the end) | scripted 0.7 | 1 | none | vertical_billboard, pivot [0.5, 1.0] (bottom on the ground, as EliteBeam), random flip U, max 64, sort 0 |
| BoneRainFall | A bone spear dropping onto a Bone Rain circle 0.28 s before it hits | normal, lighting surface | {P}/shard.png (or a new bone_spear.png pointing +V) | untinted bone; alpha (0: 0.3), (0.2: 1.0), (1.0: 1.0) | scripted 1.4; initial_scale_3d [1, 4, 1] (long) | scripted 0.28 | 1 | launched by the script: (0, -32 m/s, 0) from 9 m up; no gravity | billboard, align_to_direction, max 64, sort 1 |
| BoneRainImpact | A Bone Rain circle landing | additive, intensity 3.0 | {V}/ring_energy.png, start_rotation 0-360 | untinted (0.9, 1.0, 0.85) -> (0.6, 1.0, 0.6); alpha (0: 1.0), (0.5: 0.6), (1.0: 0.0) | scripted 3.96; size curve punch((0.4, 0.4), (1.0, 1.0)) (as GraveBurst) | scripted 0.6 | 1 | none | horizontal_billboard, max 64, sort -9 |
| PhaseShockwave | The King's phase-change shockwave (pushes the hero 4 m) | additive, intensity 8 | {V}/ring_energy.png, start_rotation 0-360 | untinted: (0: (1, 1, 1, 1)), (0.4: (0.5, 1.0, 0.6, 0.7)), (1.0: alpha 0) | scripted 28; size curve punch((0.1, 0.1), (1.0, 1.0), 0.35, 0.85) | scripted 0.5 | 1 | none | horizontal_billboard, max 8, sort -9 |
| BossSpinWind | Dust swirl around the spinning King (10 Hz) | normal | {V}/arc_8x1.png, tiles [8, 1] randomized | untinted dust (0.85, 0.8, 0.72); alpha (0: 0.0), (0.2: 0.5), (1.0: 0.0) | scripted 7 | scripted 0.3 | 1 | none; start_rotation 0-360, spin_speed 900-1100 deg/s, one direction | horizontal_billboard, max 64, sort 0 |
| BossDazed | Stars circling over a dazed head (Gravewarden after a charge, King after Death Spiral) | additive, intensity 3 | dazed_stars.png (new: five small four-point stars evenly on a circle of 80% of the tile radius) | untinted (1.0, 0.95, 0.6); alpha (0: 0.0), (0.1: 1.0), (0.85: 1.0), (1.0: 0.0) | scripted (1.56 warden, 2.25 King) | scripted (1.2 / 1.5) | 1 | none; spin_speed 240 deg/s, one direction | horizontal_billboard, max 8, sort 3 |
| BossCoreGlow | The Hollow King's green core in the rib cage, re-emitted every 0.1 s at chest height: bigger in recoveries (x1.5), late phases (x1.3), Raise the Legion (x2.5) | additive, intensity 5, soft_fade 0.5 | {P}/glow.png | tinted (green (0.3, 1, 0.45); (0.45, 1, 0.3) while raising); alpha (0: 0.0), (0.3: 0.9), (1.0: 0.0) | scripted 1.6 - 4.0 | scripted 0.25 | 1 | none | billboard, max 32, sort 3. Optional: light_count 1, light_range 6 m (green light on the ground) |
| KingCrackPulse | Phase 3 / enrage: glowing ground cracks pulsing under the King every 1.6 s | additive, intensity 3 | crack_radial.png (new, also asked for by the weapons manifest) | tinted (green; violet when enraged); alpha (0: 0.0), (0.25: 0.8), (1.0: 0.0) | scripted 12 | scripted 1.6 | 1 | none; start_rotation 0-360 | horizontal_billboard, max 8, sort -10 |
| BossGuardGlow | Bone Guard shimmer in front of the Gravewarden's raised shield (every 0.3 s for 3 s) | additive, intensity 2.5 | {P}/glow.png | tinted pale gold (1, 0.9, 0.6) by the script; alpha (0: 0.0), (0.3: 0.55), (1.0: 0.0) | scripted 2.6; size curve 0.9 -> 1.05 | scripted 0.45 | 1 | none | billboard, max 16, sort 3 |
| GuardSpark | A hit the shield turned away (throttled 0.15 s) | additive, intensity 8 | {P}/spark.png | untinted gold (1, 0.9, 0.5) -> alpha 0 | 0.12-0.2 -> 0.05 | 0.3 | 6 | sphere, 4-8 m/s, drag 3 | billboard, align_to_direction (as SPARK), max 128, sort 2 |
| ArchmageSigil | Violet rune circle under the Archmage for the Bone Spiral wind-up and the spiral (design 5.5) | additive, intensity 3 | sigil_circle.png (new: two concentric thin rings with a band of runes between them and a five-point star inside, white) | tinted violet (0.66, 0.3, 1.0) by the script; alpha (0: 0.0), (0.06: 0.9), (0.9: 0.8), (1.0: 0.0) | scripted 6; size keys (0: 0.6), (0.06: 1.0), (1.0: 1.0) | scripted 3.8 | 1 | none; spin_speed 30 deg/s | horizontal_billboard, max 8, sort -12 |
| ArchmageGhost | The 1.5 s afterimage the Archmage leaves where it blinked from | additive, intensity 2 | ghost_mage.png (new: white silhouette of the hooded Skeleton_Mage with its staff, front view, 2 px blurred edge; a white-mask render of the model is ideal) | tinted violet (0.72, 0.35, 1.0); alpha (0: 0.7), (1.0: 0.0) | scripted 4.7 (tall); size curve 1.0 -> 1.1 | scripted 1.5 | 1 (emitted at chest height 2.9 m) | up 0.3-0.5 m/s | billboard (upright, camera facing), max 4, sort 2 |
| BlinkFlash | Violet flash at both ends of a blink | additive, intensity 6 | {P}/glow.png | tinted violet; alpha 1 -> 0 | scripted 3.1; size curve punch((0.3, 0.3), (1.0, 1.0)) | scripted 0.35 | 1 | none | billboard, max 8, sort 3 |
| BossBoneBurst | Bone fragments of a boss: 60 at a King phase break, 120 at a mini-boss death, 300 at the King's death | normal, lighting surface | {P}/shard.png | untinted bone; alpha (0: 1.0), (0.8: 1.0), (1.0: 0.0) | 0.25-0.45 -> 0.2-0.35 | 1.6 | scripted (60 / 120 / 300) | hemisphere up, 4-12 m/s, gravity_scale 2.4, start_rotation 0-360, spin 360-720 random direction, collision ground plane, restitution 0.3, max_bounces 2 | billboard, max 1024, sort 1 |
| BossDeathBurst | White-gold flash ring when a boss dies (at chest height) | additive, intensity 8 | {V}/ring_energy.png | untinted: (0: (1, 1, 0.9, 1)), (0.5: (1, 0.8, 0.38, 0.7)), (1.0: alpha 0) | scripted (6 mini-boss, 12 King); size curve punch((0.2, 0.2), (1.0, 1.0)) | scripted (1.0 / 1.2) | 1 | none | billboard (camera facing), max 8, sort 3 |
| DeathWave | The King's death wave: re-emitted EVERY FRAME at its current radius (0 -> 34 m at 20 m/s of real time), so it keeps its speed through the slow motion | additive, intensity 5 | {V}/ring_energy.png | tinted pale green (0.55, 1.0, 0.65) by the script; alpha (0: 0.9), (1.0: 0.0) | scripted 0 -> 68; constant curve | scripted 0.12 | 1 | none | horizontal_billboard, max 256, sort -9 |
| GoldShower | 200 gold paid at the King's death: coins burst up from the body and rain down | normal, lighting surface | coin.png (new or reuse the gold pickup sprite: a gold coin, 64 px) | untinted gold (1.0, 0.85, 0.35); alpha (0: 1.0), (0.8: 1.0), (1.0: 0.0) | 0.22-0.32 | 1.6 | 60 | cone up 40 deg, 6-10 m/s, gravity_scale 2.0, collision ground plane, restitution 0.4, spin 360-720 | billboard, max 128, sort 2 |

## 2. Sounds

All boss sounds are big moments: spatial off (flat, they must be heard off screen), priority high, limit 1-2 unless
noted. "Sting" = design 7.3: plays 0.2 s into a wind-up so the player can react without looking; each must be
unmistakable.

| Name | Description | Layers | Length | Variants | Notes |
|---|---|---|---|---|---|
| boss_rise | A mini-boss climbs out of the ground | deep ground rumble + bone cracks + distant moan | 2.6 s | 1 | |
| king_rise | The Hollow King rises at 14:00 | rumble crescendo + stone cracking + low choir swell + bone grinding | 3.2 s | 1 | music switches at the same moment (director) |
| warden_charge_sting | Shield Charge wind-up | shield scraping on stone + low war growl | 0.8 s | 2 | sting |
| warden_charge_go | The charge launches | heavy footfalls burst + whoosh | 0.5 s | 1 | |
| warden_charge_hit | The shield hits the hero | metal clang + body thud + sub | 0.5 s | 2 | |
| warden_charge_stop | The charge ends, shield slams down | boom + gravel | 0.7 s | 1 | |
| warden_cleave_sting | Overhead Cleave wind-up | rising blade scrape | 0.6 s | 2 | sting |
| warden_cleave_hit | The blade lands | thud + earth crunch + metal ring | 0.5 s | 2 | |
| warden_guard_up | Bone Guard: the great shield rises | metal clank + bone creak | 0.5 s | 1 | |
| guard_tink | A hit deflected by the raised shield | bright metal tink | 0.15 s | 4 | limit 3, min interval 0.1 (the script throttles 0.15 s) |
| boss_dazed | A boss reels (after a charge, after Death Spiral) | ringing tinnitus bell + chirps | 1.0 s | 1 | |
| archmage_spiral_sting | Bone Spiral wind-up | violet hum swelling + whispered syllables | 0.7 s | 1 | sting |
| archmage_orb | One spiral volley (5 per second for 3 s) | soft magical pop + small whoosh | 0.25 s | 3 | volume 0.5, limit 2 |
| archmage_field_sting | Grave Field cast | earth whispers + grave hiss rising | 0.8 s | 1 | sting |
| archmage_summon_sting | Summon Guard channel starts | dark chant, low voices | 2.0 s | 1 | |
| archmage_blink | The Archmage blinks away | whoosh + crystalline chime | 0.6 s | 2 | |
| king_cleave_sting | Cleave Sweep wind-up | huge axe scraped across stone (design: metal scrape) | 0.8 s | 2 | sting |
| king_cleave_hit | The sweep lands | giant whoosh + impact thud + sub-bass < 80 Hz | 0.8 s | 2 | |
| king_slam_sting | Grave Slam wind-up | deep giant inhale, rattling ribs (design: deep inhale) | 0.9 s | 1 | sting |
| king_slam_hit | The axe hits the ground | boom + stone crack + sub-bass + debris tail | 1.2 s | 1 | |
| bone_spike | Spikes bursting along the slam lane (every 4 m) | crackle + snap | 0.3 s | 3 | limit 4 |
| king_rain_sting | Bone Rain cast | many bones clattering overhead + falling whistle | 0.9 s | 1 | sting |
| bone_rain_impact | One Bone Rain circle lands | bone shatter + scatter | 0.4 s | 4 | limit 6, min interval 0.05 |
| king_raise_sting | Raise the Legion channel | low choir + earth groan | 1.5 s | 1 | sting |
| king_spin_sting | Death Spiral spin-up | whoosh rising in pitch | 0.7 s | 1 | sting |
| king_spin_whoosh | Each half second of the spin | heavy axe whoosh | 0.45 s | 3 | limit 2 |
| king_nova_sting | Soul Nova wind-up | choir swell (design) | 1.0 s | 1 | sting |
| soul_nova_ring | Each of the three nova rings leaves | deep whoom pulse + ghostly wail | 1.0 s | 2 | limit 3 |
| king_phase_break | Phase change: the King breaks to one knee, a boss bar pip shatters | bone crack + glassy shatter + heavy kneel thud | 0.8 s | 1 | |
| king_roar | The King roars before the shockwave | giant skeletal roar, distorted, long reverb | 1.4 s | 1 | |
| phase_shockwave | The shockwave | boom + outward whoosh | 1.0 s | 1 | |
| king_enrage | 16:30 hard enrage | roar layered with a bell toll and a choir stab | 2.0 s | 1 | |
| boss_death | A mini-boss collapses | bones clattering + fading groan + shield / staff clatter | 2.0 s | 1 | |
| king_death | The Hollow King falls (in 0.25 slow motion) | long roar fading into a choir chord + bones raining | 3.5 s | 1 | |
| death_wave | The death wave rushes out | spectral wind + rising whoosh | 1.8 s | 1 | |
| gold_shower | 200 gold paid | coin cascade | 1.6 s | 1 | |
| boss_chest_drop | A boss chest lands | heavy chest thud + sparkle chime | 0.8 s | 1 | |

Also used (already in other manifests / the core): `grave_burst`, `summon`, `enemy_death`, `gem`.

## 3. Projectile looks (ProjectileCatalog, weapons module or lead)

| Name | Mesh | Scale | Material | Spin / align | Notes |
|---|---|---|---|---|---|
| ArchmageOrb | engine:/embedded/icosphere2 | 0.45 (the code multiplies by 1.3) | SpellGlow.mat instance, emissive violet (0.75, 0.35, 1.0) intensity 3 | spin 180 | height 1.6 m (the staff), optional trail of violet sparkles. OPTIONAL: without it the bosses fire "HexOrb" |

## 4. Boss controller states used (build_bosses.py names, base layer)

- Every boss: Locomotion ("Speed" = world speed / model scale), Rise (stretched to 2.6 s / 3.0 s), Death, Hit.
- Gravewarden: Block (charge wind-up, Bone Guard; loops), Attack (Overhead Cleave), run cycle for the charge.
- Hollow Archmage: CastRaise (spiral wind-up), CastLong (stretched over the 3 s spiral), Cast (Grave Field),
  Summon (channel).
- Hollow King: HeavySlice (Cleave Sweep), HeavyChop (Grave Slam; the pose holds while the axe is stuck), CastRaise
  (Bone Rain, Soul Nova, the phase roar), Summon (Raise the Legion), Spin (Death Spiral, 0.35x during the spin-up),
  Kneel (phase break).
- Unused: AttackSlice, JumpChop, Taunt.
- Swings are re-timed so their impact frame lands on the design's hit moment: BossAnimator.GetImpactShare holds the
  impact share of each clip, measured on the KayKit clips by forward kinematics of hand.r (2H chop 0.5, 2H slice
  0.38, 1H chop 0.62, Spellcast_Raise 0.32, Spellcast_Summon 0.7). Tune there by eye if a blow looks early or late.

## 5. Integration notes

- Nothing to wire: WaveDirector already calls Spawn / Tick / Clear / OnEnemyDied / isMiniBossAlive. New public
  members: `isBossAlive` (King rising or fighting), `isVictoryPlaying`.
- Quick test without playing 14 minutes: `Run.current.director.bosses.Spawn(BossKind.HollowKing, heroPosition +
  forward * 12)` from any debug hook (the King's health uses `run.level`, 40000 at low levels); set `run.runTime` to
  990 to see the enrage.
- Looks are set at runtime: Gravewarden body bone (0.91, 0.88, 0.8) with gold props (1.0, 0.8, 0.42) and gold eyes;
  Archmage ash (0.55, 0.59, 0.66) with violet eyes; King void (0.36, 0.27, 0.47) with a green core (eyes +
  BossCoreGlow). Hits flash the body white (0.08 s, at most every 0.12 s); a freeze turned slow tints it frost blue.
- Victory (design 7.4): 0.25 time scale for 1.5 s, zoom 0.9, death wave, all gems vacuum (they only chime: no level
  -up cards over the victory, see CORE_REQUESTS bosses #5), 200 x Greed gold paid at once with the GoldShower
  picture, the 5-item chest lands at the hero's feet and opens its reel, then `run.OnBossDefeated()` at 1.9 s of
  sequence time (the results follow Run's own 2.2 s: 4.1 s plus the reel).
- Mini-boss kill: chest (3 / 5 items) + 30 / 60 gold pickups + a Lodestone at the body, +1 Reroll (max +2 per run),
  `save.RecordBossDefeated`, hit stop 0.1 s (bypassGap), white screen flash, banner "<name> falls  +1 Reroll".
- The boss bar shows the King when he is up, else the newest mini-boss; titles: "Warden of the Barrow", "Voice of the
  Barrow", "Lord of the Barrowlight". Every live boss gets a MarkerKind.Boss off-screen arrow.
- Open core items that change boss behaviour: CORE_REQUESTS.md "Bosses module" (proxy rows never thaw; slows are
  invisible to bosses, which includes every weapon freeze; burn through phase invulnerability; tinted EmitMoving;
  level-up hold for the victory; death cause for enemy projectiles; SaveData unlock snapshot).

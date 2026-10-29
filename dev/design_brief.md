# BARROWLIGHT - Design Brief (3D survivors-like sample)

Working title: **Barrowlight**. A 15-minute run on a haunted moor. The hero walks, weapons fire on
their own, the bone tide grows from 20 skeletons to 3000, and at 14:00 the Hollow King rises.
All names, items and numbers below are original to this brief.

## 0. Pillars and conventions

Pillars:
1. Readable at a glance: one screenshot tells the stakes (hero, health ring, horde, timer).
2. Power fantasy with a debt: the player out-scales enemies per hit, but the director out-scales
   them in numbers. Death comes from density and from telegraphed heavy hits, never from noise.
3. A reward every ~20 seconds (level-up, chest, pickup, event), a big spike every ~2.5 minutes.
4. Every weapon has a body: anticipation, impact, reaction, aftermath (see section 10).

Conventions used everywhere:
- Distances in meters (m), speeds in m/s, time in seconds (s). `t` = run time in minutes (float).
- Hero base move speed is 5.0 m/s. Camera shows about 32 m x 18 m of ground at default zoom.
- "Rank" = item level. Weapons rank 1-8, passives rank 1-5, evolved weapons are rank "E".
- Damage numbers in tables are base values before Might, crit and target modifiers.
- Weapon "Area" scales radius / length (linear), not surface. "Amount" adds count.
- All percentages in the stat sheet are additive with each other inside one stat unless a
  formula says otherwise.

---

## 1. Player stat sheet

### 1.1 Stats

| Stat | Meaning | Base | Per passive rank | Per meta rank | Cap |
|---|---|---|---|---|---|
| Max Health | Hit points | 100 (class 85-150) | +10% (Iron Heart) | +5% | none |
| Regen | HP restored per second | 0 HP/s | +0.2 HP/s (Iron Heart) | +0.1 HP/s | 6 HP/s |
| Armor | Damage reduction, see 1.2 | 0 | +2 (Plate Pauldron) | +1 | 45 (= 75% reduction) |
| Move Speed | Walk speed multiplier | 100% (5.0 m/s) | +7% (Windstride Boots) | +3% | 160% (8.0 m/s) |
| Might | Damage multiplier | 100% | +8% (Whetstone) | +4% | none |
| Area | Weapon radius / length multiplier | 100% | +8% (Wide Sigil) | +5% | 200% |
| Cooldown | Weapon cooldown multiplier | 100% | -6% (Quickening Hourglass) | -2.5% | 50% (min) |
| Amount | Extra projectiles / swings / summons | +0 | +1 (Echo Quiver, 2 ranks) | none | +4 |
| Projectile Speed | Projectile travel speed | 100% | +10% (Everburn Wick) | none | 200% |
| Duration | Lingering effect time (pools, orbit, spin, stun) | 100% | +10% (Everburn Wick) | none | 200% |
| Pickup Radius | Gem / gold magnet radius | 2.0 m | +25% (Lodestone Charm) | +15% | 6.0 m |
| Luck | Better rarity weights, chest tiers, drop rates | 0 | +10 (Lucky Knucklebone) | +5 | 100 |
| Growth | XP gain multiplier | 100% | +8% (Tome of Insight) | +4% | 200% |
| Greed | Gold gain multiplier | 100% | none | +8% | 200% |
| Crit Chance | Chance a hit crits | 5% | +4% (Keen Eye) | none | 75% |
| Crit Damage | Crit multiplier | 150% | +10% (Keen Eye) | none | 300% |
| Knockback | Multiplier on weapon knockback | 100% | none | none | 200% |
| Block Chance | Knight only: chance to negate a hit | 0% | none | none | 40% |
| Revival | Extra lives per run | 0 | none | +1 (1 rank) | 2 |
| Reroll | Re-roll the 3 cards | 0 | none | +1 | 5 |
| Banish | Remove an item from the run pool | 0 | none | +1 | 3 |
| Skip | Close the picker for 5 gold | unlimited | - | - | - |
| Dread | Optional difficulty after first win | 0 | none | toggle 0-5 | 5 |

Not upgradeable but fixed: player invulnerability after taking a hit = 0.25 s; pickup collect
distance 0.5 m; body radius 0.45 m.

### 1.2 Formulas

```
damage_dealt   = base_damage * (might) * (crit ? crit_damage : 1) * target_mods
                 target_mods: frozen x1.25, marked x(1 + mark_bonus), Briar Sanctum x1.10
crit           = roll per hit: random01() < crit_chance
cooldown_final = base_cooldown * max(0.50, cooldown_mult)        (actives use half: 1 - (1-cdm)/2)
damage_taken   = max(1, raw * 15 / (15 + min(armor, 45)))         (armor 15 = 50%, 45 = 75%)
dread_k        = 1 + 0.15 * dread (enemy HP), 1 + 0.10 * dread (spawn budget, XP, gold)
revival        = on death: revive at 50% max HP, 3.0 s invulnerable, blast radius 6 m 200 dmg, KB 6 m
body blocking  = hero speed x (1 - 0.08 * overlapping_enemies), floor 0.40;
                 Shieldbearers and bosses are hard blockers (no overlap)
```

Display rule: show armor as "% reduction" in the pause screen, computed from the formula.

### 1.3 Class starting stats (bold = class bonus)

| Stat | Knight | Barbarian | Mage | Rogue |
|---|---|---|---|---|
| Max Health | **130** | **150** | 85 | 95 |
| Regen | **0.2** | 0 | 0 | 0 |
| Armor | **4** | **2** | 0 | 0 |
| Move Speed | 4.7 m/s | 4.9 m/s | 5.0 m/s | **5.5 m/s** |
| Might | 100% | **110%** | 100% | 100% |
| Area | 100% | **110%** | **110%** | 100% |
| Cooldown | 100% | 100% | **92%** | 100% |
| Crit Chance / Damage | 5% / 150% | 5% / 150% | 5% / 150% | **15% / 175%** |
| Luck | 0 | 0 | **10** | **10** |
| Pickup Radius | 2.0 m | 2.0 m | **2.6 m** | 2.0 m |
| Projectile Speed | 100% | 100% | 100% | **110%** |
| Knockback | 100% | **130%** | 100% | 100% |
| Block Chance | **15%** | 0 | 0 | 0 |

---

## 2. Leveling

### 2.1 XP curve

```
xp_to_next(L) = round(5 + 9 * (L - 1) + 2.4 * (L - 1)^2)
```

Fitted against the spawn model in section 6 so that a player with no Growth reaches level 5 at
1:00, 13 at 5:00, 24 at 10:00, 30 at 13:00 and 32 when the boss rises at 14:00 (+20% Growth is
worth about one minute). Level 2 needs 5 kills (hook in 3-5 s).

| L | to next | cumul. | L | to next | cumul. | L | to next | cumul. | L | to next | cumul. |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 5 | 0 | 16 | 680 | 3456 | 31 | 2435 | 24597 | 46 | 5270 | 79623 |
| 2 | 16 | 5 | 17 | 763 | 4136 | 32 | 2590 | 27032 | 47 | 5497 | 84893 |
| 3 | 33 | 21 | 18 | 852 | 4899 | 33 | 2751 | 29622 | 48 | 5730 | 90390 |
| 4 | 54 | 54 | 19 | 945 | 5751 | 34 | 2916 | 32373 | 49 | 5967 | 96120 |
| 5 | 79 | 108 | 20 | 1042 | 6696 | 35 | 3085 | 35289 | 50 | 6208 | 102087 |
| 6 | 110 | 187 | 21 | 1145 | 7738 | 36 | 3260 | 38374 | 51 | 6455 | 108295 |
| 7 | 145 | 297 | 22 | 1252 | 8883 | 37 | 3439 | 41634 | 52 | 6706 | 114750 |
| 8 | 186 | 442 | 23 | 1365 | 10135 | 38 | 3624 | 45073 | 53 | 6963 | 121456 |
| 9 | 231 | 628 | 24 | 1482 | 11500 | 39 | 3813 | 48697 | 54 | 7224 | 128419 |
| 10 | 280 | 859 | 25 | 1603 | 12982 | 40 | 4006 | 52510 | 55 | 7489 | 135643 |
| 11 | 335 | 1139 | 26 | 1730 | 14585 | 41 | 4205 | 56516 | 56 | 7760 | 143132 |
| 12 | 394 | 1474 | 27 | 1861 | 16315 | 42 | 4408 | 60721 | 57 | 8035 | 150892 |
| 13 | 459 | 1868 | 28 | 1998 | 18176 | 43 | 4617 | 65129 | 58 | 8316 | 158927 |
| 14 | 528 | 2327 | 29 | 2139 | 20174 | 44 | 4830 | 69746 | 59 | 8601 | 167243 |
| 15 | 601 | 2855 | 30 | 2284 | 22313 | 45 | 5047 | 74576 | 60 | 8890 | 175844 |

"cumul." = total XP needed to reach that level. Level 60 is the display cap (XP bar fills, extra
level-ups keep coming with fallback cards).

### 2.2 XP income

```
gem_xp(enemy, t) = enemy.base_xp * (1 + 0.10 * t) * growth * dread_k
                   stochastic rounding: floor(x) + (random01() < frac(x) ? 1 : 0)
```

Base XP: Shambler 1, Runner 1, Archer 2, Exploder 2, Shieldbearer 4, Hex Caster 6, elite 25,
Gravewarden 200, Hollow Archmage 250. Summoned enemies (by casters / boss) give 0 XP and no drops.
Expected XP per minute is listed in section 12.

### 2.3 Gem tiers

| Tier | Name | XP value range | Color (sRGB) | Mesh scale | Pickup sound pitch base |
|---|---|---|---|---|---|
| 1 | Azure Shard | 1-4 | #3FA9FF | 0.18 m | 1.00 |
| 2 | Verdant Shard | 5-19 | #39E06B | 0.24 m | 0.94 |
| 3 | Amber Shard | 20-99 | #FFB52E | 0.30 m | 0.89 |
| 4 | Violet Shard | 100-499 | #B05CFF | 0.38 m | 0.84 |
| 5 | Crimson Cluster | 500+ (merged overflow) | #FF3B4E | 0.50 m | 0.79 |

Gems idle-bob 0.08 m at 1.5 Hz and spin 90 deg/s; emissive pulses +30% at 1 Hz. Gems never despawn.

### 2.4 Gem merging

- Hard cap: 600 gems on the ground (readability and pickup cost).
- Every 2.0 s, merge pass: gems farther than 28 m from the hero are bucketed in a 4 m grid; each
  bucket collapses into one gem (value = sum, tier recomputed, position = highest-value gem).
- If a new gem would exceed the cap after the pass, its value is added to the single
  **Crimson Cluster** of the run (created on first overflow at the far edge of the screen,
  marked with an off-screen arrow). Picking it up resets it.
- Gems within 28 m are never merged (the player sees exactly what dropped).

### 2.5 Magnet and vacuum

- A gem inside Pickup Radius enters vacuum: it first hops 0.25 m away from the hero over 0.10 s
  (ease-out), then flies toward the hero with `speed = 4 + 40 * s^2` m/s (s = seconds since the
  hop), capped at 30 m/s, and is collected at 0.5 m.
- Lodestone pickup (section 8) puts every gem on the map into vacuum with a 0.3 s random stagger.
- Gold coins use the same curve; food does not vacuum (must be walked over).

### 2.6 Level-up offer rules

- Every level-up pauses the game and shows exactly **3 cards** (unique items, no duplicates).
- Slots: **6 weapons** (starting weapon included) and **6 passives**.
- Max ranks: weapons 8, passives 5 (Echo Quiver 2). A maxed item leaves the pool.
- Rarity weights: Common 100, Uncommon 70, Rare 45, Epic 20.
- Luck shifts weights toward rarer items: `w = base_w * (1 + luck/100 * k)`,
  k = 0 (Common), 0.5 (Uncommon), 1.0 (Rare), 1.5 (Epic).
- Owned, non-maxed items get `w *= 1.6` (builds converge, as in the genre).
- Class-exclusive weapons only appear for their class.

Generation (deterministic per seed):

```
pool = owned items with rank < max
     + unowned weapons   if weapon_slots_free > 0
     + unowned passives  if passive_slots_free > 0
     - banished items
if pool.empty: offer fallback cards (Hearty Meal, Coin Purse, Battle Hymn)
cards = []
if level in {2, 3} or no_new_weapon_streak >= 5 (and weapon slot free):
    cards += weighted_pick(unowned weapons)                    # pity / onboarding
if any owned non-maxed item and cards has no owned item:
    cards += weighted_pick(owned non-maxed items)               # keep the build moving
while cards.size < 3 and pool not exhausted:
    cards += weighted_pick(pool - cards)
shuffle(cards)
```

Fallback cards (when nothing else is eligible, also fill missing slots):
- Hearty Meal: heal 30% max HP.
- Coin Purse: +20 gold.
- Battle Hymn: +2% Might for the rest of the run (stacks without limit).

After the pick: 0.8 s invulnerability, golden ring flash, game resumes with a 0.25 s ramp from
time scale 0.3 to 1.0.

### 2.7 Evolutions

- Condition: weapon at rank 8 **and** its paired passive owned at any rank.
- Delivery: the next chest (elite, mini-boss, boss) evolves it as its first item. 5-item chests
  can evolve up to 2 weapons. The HUD weapon icon gets a pulsing gold frame when eligible.
- Elites spawn at least every 90 s from 3:00, so an eligible player waits at most 90 s.
- The evolved weapon keeps its slot, is shown with a gold frame and "E" rank, cannot rank further.
- The paired passive stays and keeps ranking.

### 2.8 Reroll, Banish, Skip

- Reroll: replaces all 3 cards with a fresh roll (pity rules re-applied). Charges from meta (0-5).
- Banish: an X on each card. Removes that item from the pool for the rest of the run and
  replaces only that card. Charges from meta (0-3).
- Skip: always available, closes the picker and grants 5 gold.
- Rerolls are also refunded +1 when a mini-boss dies (max +2 per run) so even a fresh save sees
  the feature.

---

## 3. Heroes and weapons

### 3.0 Shared rules

Targeting modes (all weapons use one):
- NEAREST: nearest living enemy within the weapon range (spatial hash query).
- FACING: last non-zero movement direction (works with stick and touch).
- CLUSTER: center of the 2 m cell with the most enemies within 12 m (precomputed once per frame
  from the spatial hash: top 8 cells).
- RANDOM_VIEW: random enemy inside the camera rectangle.
- SELF: centered on the hero. ORBIT: around the hero.

Active button (recommended, one per class):
- Keyboard Space, gamepad South button or Right Bumper, touch: 96 px button bottom-right.
- Radial cooldown sweep on the HUD icon, ready flash + soft chime. Cooldown stat applies at half.
- Every active grants invulnerability for its movement part: it is the "oh no" button.

Animation layering on the hero (KayKit humanoid rig):
- Legs always play locomotion (Idle / Walking / Running by speed: <0.2, <3.5, else Running).
- Weapon attacks play on an upper-body layer (mask from the spine up) when available; otherwise a
  one-shot override capped at 1 attack clip per 0.4 s with priority: active > evolved > class
  weapon > shared. Weapons that fire while the body is busy still spawn their VFX and damage.
- Full-body clips: Spin (Whirlwind), Dodge/Roll (Rogue active), Jump (Barbarian active).

Weapon stats used by tag: [melee] Area, Might, Cooldown, Amount=extra swings; [projectile] +
Projectile Speed, Amount=extra projectiles; [lingering] + Duration; [summon] Amount=extra summons.

Asset notes: projectiles reuse KayKit weapon props (bolts, daggers, axes, shields, swords) scaled
and tinted; spells are simple emissive meshes + particles. No new characters are needed.

### 3.1 Knight - "The Oath That Holds"

Vibe: the wall in the storm. Slow, armored, every swing is a heavy steel arc that throws skeletons
back; the knight wins by standing where others would flee. Sound is steel, bells and stone. Gold
and white VFX. Best for new players: high armor, block, a dash that is also a battering ram.

- Starting weapon: Oathblade Arc.
- Signature passive **Bulwark**: 15% chance to block any hit (0 damage). A block plays the Block
  clip on the upper body, a shield clang (2 layers: metal transient + low thump), and releases a
  Shield Bash shockwave: radius 2.0 m, damage 15 + level, knockback 1.5 m (max 1 per 0.5 s).
  +1 Armor every 5 levels (max +8 at level 40).
- Active **Shield Rush** (cooldown 10 s): dash 6 m in the move direction over 0.30 s,
  invulnerable, path width 1.6 m; enemies hit take 40 + 4 x level damage, are knocked sideways
  3 m and stunned 0.8 s. Feel: 60 ms hit stop if >= 5 enemies hit, camera trauma 0.2, dust trail,
  shield glows gold during the dash, final "boom" on stop.

#### K1. Oathblade Arc (start) - [melee], FACING toward NEAREST within 6 m, else move direction
A wide sword slice in front. Knockback 1.2 m (R8: 1.6 m). Amount: +1 swing (alternating side).

| R | Dmg | CD s | Radius m | Swings | Pierce | Arc | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 14 | 1.10 | 2.8 | 1 | all | 140 deg | |
| 2 | 18 | 1.10 | 2.8 | 1 | all | 140 deg | |
| 3 | 18 | 1.10 | 3.2 | 1 | all | 140 deg | |
| 4 | 18 | 1.10 | 3.2 | 2 | all | 140 deg | backhand swing behind, +0.2 s |
| 5 | 24 | 1.00 | 3.2 | 2 | all | 140 deg | |
| 6 | 24 | 1.00 | 3.6 | 2 | all | 180 deg | |
| 7 | 30 | 1.00 | 3.6 | 2 | all | 180 deg | |
| 8 | 36 | 0.90 | 3.6 | 2 | all | 180 deg | KB 1.6 m |

Feel: white-gold ribbon trail 0.12 s, "shing" + body thud, 30 ms global hit stop when one swing
hits >= 6 enemies (max once per 0.3 s), struck skeletons flash and stagger back.
Evolution **Dawnbreaker** (+ Iron Heart): 48 dmg, 0.85 s, radius 4.2 m, full 360 deg sweep;
every 3rd swing launches a golden crescent wave (48 dmg, 4 m wide, 14 m/s, travels 10 m, pierces
all); heals 1 HP per 10 enemies hit (max 3 HP/s).

#### K2. Aegis Orbit - [melee, lingering], ORBIT
Spectral shields circle the knight at 200 deg/s, each enemy re-hit at most every 0.5 s.
Shields destroy enemy projectiles they touch. Knockback 2.0 m. Shield radius 0.6 m (R6+: 0.75 m).

| R | Dmg | CD s | Orbit r m | Shields | Pierce | Dur s | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 10 | 4.0 | 2.2 | 2 | all | 3.0 | |
| 2 | 10 | 4.0 | 2.2 | 3 | all | 3.0 | |
| 3 | 14 | 4.0 | 2.5 | 3 | all | 3.0 | |
| 4 | 14 | 4.0 | 2.5 | 3 | all | 4.0 | |
| 5 | 14 | 4.0 | 2.5 | 4 | all | 4.0 | |
| 6 | 18 | 4.0 | 2.5 | 4 | all | 4.0 | bigger shields |
| 7 | 18 | 3.5 | 2.5 | 4 | all | 5.0 | |
| 8 | 22 | 3.5 | 2.8 | 5 | all | 5.0 | |

Feel: shields fade in with a ringing tone, each hit is a short "bonk" (pitch +-8%), knocked
skeletons tumble outward, blocked bolts burst into sparks.
Evolution **Bastion Halo** (+ Plate Pauldron): 6 shields, 26 dmg, orbit 3.0 m, permanent (no
downtime); destroyed enemy projectiles are reflected at the nearest enemy for 30 dmg.

#### K3. Verdict - [area], CLUSTER within 12 m
A giant spectral sword (3 m) appears above the densest cluster and slams down. Telegraph 0.35 s
(gold ring decal + glowing sword), knockback 2.5 m outward, embedded 1.0 s then dissolves.

| R | Dmg | CD s | Radius m | Swords | Pierce | Stun s | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 45 | 5.0 | 2.2 | 1 | all | 0.5 | |
| 2 | 60 | 5.0 | 2.2 | 1 | all | 0.5 | |
| 3 | 60 | 5.0 | 2.6 | 1 | all | 0.5 | |
| 4 | 60 | 5.0 | 2.6 | 2 | all | 0.5 | 2nd sword on next cluster, +0.15 s |
| 5 | 80 | 4.5 | 2.6 | 2 | all | 0.5 | |
| 6 | 80 | 4.5 | 3.0 | 2 | all | 0.8 | |
| 7 | 80 | 4.5 | 3.0 | 3 | all | 0.8 | |
| 8 | 110 | 4.0 | 3.0 | 3 | all | 0.8 | |

Feel: deep bell + stone crack, camera trauma 0.15 per slam, 40 ms hit stop on the first slam per
second, radial crack decal fading over 2 s, dust ring and bone shards flung outward.
Evolution **Final Verdict** (+ Wide Sigil): 5 swords, 130 dmg, radius 3.4 m; each embedded sword
emits a holy shockwave ring 1.0 s after landing (60 dmg, radius 4.5 m).

### 3.2 Barbarian - "The Red Storm"

Vibe: a walking avalanche with two axes. Short range, huge cleaves, enormous knockback; gets
stronger the more he kills, so he wants to be in the thick of it. Sound is meaty chops, grunts,
war drums. Red and ember-orange VFX, chunky debris.

- Starting weapon: Ravager Chop.
- Signature passive **Bloodlust**: each kill grants 1 Fury (max 50). Each Fury: +1% Might and
  -0.4% Cooldown. Fury decays 5 per second after 2.0 s without a kill. At 50 Fury: eyes glow red,
  axe trails turn red, a heartbeat drum layer joins the music, kills heal 0.5 HP.
- Active **Earthshaker Leap** (cooldown 12 s): Jump clip, leap up to 7 m in the move direction,
  airtime 0.55 s invulnerable; landing shockwave radius 4.0 m, 60 + 6 x level damage, knockback
  4 m radial, stun 1.0 s. Feel: 80 ms hit stop, camera trauma 0.35, ground crack decal, rock
  chunks, low boom with sub-bass.

#### B1. Ravager Chop (start) - [melee], FACING toward NEAREST within 5 m
Two-handed overhead chop in a cone. Knockback 2.5 m (R7+: 3.0 m). Amount: +1 chop.

| R | Dmg | CD s | Length m | Chops | Pierce | Cone | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 24 | 1.60 | 3.2 | 1 | all | 100 deg | |
| 2 | 30 | 1.60 | 3.2 | 1 | all | 100 deg | |
| 3 | 30 | 1.60 | 3.6 | 1 | all | 120 deg | |
| 4 | 30 | 1.60 | 3.6 | 2 | all | 120 deg | 2nd axe +0.25 s |
| 5 | 38 | 1.45 | 3.6 | 2 | all | 120 deg | |
| 6 | 38 | 1.45 | 4.0 | 2 | all | 120 deg | |
| 7 | 46 | 1.45 | 4.0 | 2 | all | 120 deg | KB 3.0 m |
| 8 | 55 | 1.30 | 4.0 | 2 | all | 120 deg | hits stagger 0.3 s |

Feel: 50 ms global hit stop when a chop hits >= 4 enemies, camera trauma 0.08 per chop, thick red
smear trail, wet chop + bone crack sound, overkill launches bodies 1-2 m up (see 10.2).
Evolution **Skullsplitter** (+ Whetstone): 70 dmg, 1.20 s; each chop also splits the ground in a
fissure 7 m forward, 1.4 m wide, 70 dmg; kills by the fissure grant 2 Fury.

#### B2. Whirlwind - [melee, lingering], SELF
The Barbarian spins (full-body Spin clip) for the duration, hitting everything in the radius each
tick; +15% move speed while spinning; 0.3 m knockback per tick keeps enemies swirling.
Amount: +20% duration per point.

| R | Dmg/tick | CD s | Radius m | Tick s | Pierce | Dur s | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 8 | 6.0 | 2.4 | 0.25 | all | 1.5 | |
| 2 | 10 | 6.0 | 2.4 | 0.25 | all | 1.5 | |
| 3 | 10 | 6.0 | 2.4 | 0.25 | all | 2.0 | |
| 4 | 10 | 6.0 | 2.8 | 0.25 | all | 2.0 | |
| 5 | 13 | 5.5 | 2.8 | 0.25 | all | 2.0 | |
| 6 | 13 | 5.5 | 2.8 | 0.20 | all | 2.5 | |
| 7 | 13 | 5.5 | 3.2 | 0.20 | all | 2.5 | |
| 8 | 16 | 5.0 | 3.2 | 0.20 | all | 2.5 | |

Feel: rising "whoosh" loop whose pitch climbs 0 -> +4 semitones over the duration, red ring
trail at blade height, sparks per hit (max 30 per second), final heavy stop sound.
Evolution **Maelstrom** (+ Windstride Boots): 20 dmg/0.20 s, radius 3.6 m, duration 4.0 s,
cooldown 4.0 s, pulls enemies within 5 m inward at 1.5 m/s, +30% move speed while spinning.

#### B3. Fault Line - [area], toward NEAREST enemies (one fissure per target)
A stomp sends fissures along the ground; rock spikes erupt sequentially along each line over
0.3 s, popping enemies 0.5 m into the air and stunning them.

| R | Dmg | CD s | Length m | Fissures | Pierce | Stun s | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 30 | 4.0 | 6 | 1 | all | 0.4 | width 1.2 m |
| 2 | 30 | 4.0 | 6 | 2 | all | 0.4 | |
| 3 | 40 | 4.0 | 6 | 2 | all | 0.4 | |
| 4 | 40 | 4.0 | 8 | 2 | all | 0.4 | |
| 5 | 40 | 3.6 | 8 | 3 | all | 0.4 | |
| 6 | 52 | 3.6 | 8 | 3 | all | 0.5 | |
| 7 | 52 | 3.6 | 10 | 3 | all | 0.5 | width 1.6 m |
| 8 | 65 | 3.6 | 10 | 4 | all | 0.5 | |

Feel: stomp thump on the hero, then a rolling crack that travels with the spikes, camera trauma
0.10, dust puffs per spike, launched skeletons spin.
Evolution **Tectonic Rift** (+ Plate Pauldron): 6 fissures in a star aimed at the nearest enemy,
80 dmg, length 12 m; leaves glowing lava cracks for 3 s (12 dmg per 0.5 s).

### 3.3 Mage - "The Last Candle"

Vibe: fragile, far-reaching, spectacular. The mage does not touch the horde; the sky does it for
her. Fire, ice and starlight; every big spell has a visible promise (a shadow, a ring) before it
lands. Sound is crackle, glassy chimes and deep impacts. Blue and violet VFX with orange fire.

- Starting weapon: Ember Orb.
- Signature passive **Arcane Echo**: 12% chance that a Mage-exclusive weapon casts twice; the echo
  fires 0.25 s later at 70% damage with a violet tint and a reversed-chime sound. +1% per 5
  levels (max 20%).
- Active **Blink** (cooldown 8 s): instant teleport 7 m in the move direction (0.1 s), 0.3 s
  invulnerable; leaves an arcane rift at the origin that detonates after 0.4 s: radius 3.0 m,
  50 + 5 x level damage. Feel: implosion sound, screen-space ripple 0.2 s, violet afterimage.

#### M1. Ember Orb (start) - [projectile, area], NEAREST within 12 m
A fireball (10 m/s) that explodes on the first enemy, leaving a burn (3 dmg per 0.5 s for 2 s,
refreshes, does not stack). Amount: +1 orb.

| R | Dmg | CD s | Blast r m | Orbs | Pierce | Burn/tick | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 16 | 1.20 | 1.6 | 1 | 0 | 3 | |
| 2 | 16 | 1.20 | 1.6 | 2 | 0 | 3 | |
| 3 | 22 | 1.20 | 1.6 | 2 | 0 | 3 | |
| 4 | 22 | 1.20 | 2.0 | 2 | 0 | 3 | |
| 5 | 22 | 1.10 | 2.0 | 3 | 0 | 3 | |
| 6 | 28 | 1.10 | 2.0 | 3 | 0 | 5 | |
| 7 | 28 | 1.10 | 2.4 | 3 | 0 | 5 | |
| 8 | 34 | 1.00 | 2.4 | 4 | 0 | 5 | |

Feel: Spellcast_Shoot on the upper body, whoosh on launch, punchy boom with random pitch +-6%,
orange flash sphere 0.1 s, burning skeletons emit small flames and orange damage numbers.
Evolution **Sunfire Orb** (+ Quickening Hourglass): 5 orbs, 40 dmg, blast 2.6 m, burn 8 per 0.5 s;
each blast throws 3 embers 3 m outward (15 dmg, radius 1.0 m).

#### M2. Starfall - [area, lingering], CLUSTER (weighted random among top 8 cells in view)
Meteors fall at a 60 deg angle; a shadow decal grows for 0.6 s, then impact and a burning crater.

| R | Dmg | CD s | Radius m | Meteors | Pierce | Crater | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 50 | 6.0 | 2.5 | 1 | all | 2 s, 4/0.5 s | |
| 2 | 65 | 6.0 | 2.5 | 1 | all | 2 s, 4/0.5 s | |
| 3 | 65 | 6.0 | 2.5 | 2 | all | 2 s, 4/0.5 s | 0.2 s stagger |
| 4 | 65 | 6.0 | 3.0 | 2 | all | 2 s, 4/0.5 s | |
| 5 | 85 | 5.5 | 3.0 | 2 | all | 2 s, 4/0.5 s | |
| 6 | 85 | 5.5 | 3.0 | 3 | all | 3 s, 6/0.5 s | |
| 7 | 85 | 5.5 | 3.5 | 3 | all | 3 s, 6/0.5 s | |
| 8 | 110 | 5.0 | 3.5 | 4 | all | 3 s, 6/0.5 s | |

Feel: Spellcast_Raise, rising whistle during the fall (pitch up 0.6 s), impact boom with sub-bass,
camera trauma 0.20, 30 ms hit stop on the first impact per second, flying rock debris, radial
knockback 2 m, scorched decal 4 s.
Evolution **Cataclysm** (+ Wide Sigil): every 7 s one great meteor on the densest cluster (300 dmg,
radius 7 m, telegraph 0.9 s) plus 6 normal meteors (110 dmg) ringing it; craters 4 s, 10/0.5 s;
camera trauma 0.6.

#### M3. Rime Nova - [area], SELF
A frost ring expands from the mage to full radius in 0.4 s; hit enemies are chilled (-40% speed,
2 s) and may freeze (1.5 s, ice-blue tint, animation paused). Frozen enemies take +25% damage.
Amount: +1 = a second nova 0.5 s later at 70% radius.

| R | Dmg | CD s | Radius m | Novas | Pierce | Freeze chance | Notes |
|---|---|---|---|---|---|---|---|
| 1 | 12 | 5.0 | 4.0 | 1 | all | 0% | chill only |
| 2 | 16 | 5.0 | 4.0 | 1 | all | 0% | |
| 3 | 16 | 5.0 | 4.6 | 1 | all | 0% | |
| 4 | 16 | 5.0 | 4.6 | 1 | all | 15% | |
| 5 | 22 | 4.5 | 4.6 | 1 | all | 15% | |
| 6 | 22 | 4.5 | 5.2 | 1 | all | 25% | |
| 7 | 28 | 4.5 | 5.2 | 1 | all | 25% | |
| 8 | 28 | 4.0 | 5.8 | 1 | all | 35% | |

Feel: glassy crackle sweep, frost ring mesh with refraction-like white edge, frozen enemies get a
crystalline shell overlay; frozen kills shatter into 6 ice shards with a tinkle.
Evolution **Absolute Zero** (+ Everburn Wick): radius 7.0 m, 36 dmg, cooldown 5.0 s, freeze 100%
(elites 2 s; bosses only slowed 50% for 1 s); frozen enemies that die shatter for 40 dmg in 1.5 m.

### 3.4 Rogue - "The Knife in the Fog"

Vibe: a hooded blur that never stands still. Crossbow bolts and thrown steel, crits everywhere,
and an executioner's mark for the big ones. Moving keeps her lethal; stopping makes her ordinary.
Sound is sharp twangs, whispers and clean stabs. Teal and smoke-grey VFX, yellow crit numbers.

- Starting weapon: Repeater Crossbow.
- Signature passive **Momentum**: every 0.5 s of continuous movement grants 1 Momentum (max 10);
  each gives +3% crit chance and +1% move speed. Standing still for 0.4 s resets to 0. At 10:
  teal footstep wisps and a subtle wind loop.
- Active **Smoke Roll** (cooldown 6 s): Dodge/Roll clip, 5 m over 0.35 s, 0.45 s invulnerable;
  drops a smoke cloud (radius 3 m, 3 s): enemies inside lose their target and wander, no attacks;
  the next 3 Rogue weapon hits auto-crit. Feel: soft "poof", grey puff particles, whoosh.

#### R1. Repeater Crossbow (start) - [projectile], NEAREST within 16 m
Fast bolts (24 m/s) in a burst, 0.08 s between bolts. Pierce = extra enemies passed through.

| R | Dmg | CD s | Range m | Bolts | Pierce | Notes |
|---|---|---|---|---|---|---|
| 1 | 9 | 0.75 | 16 | 1 | 1 | |
| 2 | 9 | 0.75 | 16 | 2 | 1 | |
| 3 | 12 | 0.75 | 16 | 2 | 1 | |
| 4 | 12 | 0.75 | 16 | 2 | 2 | |
| 5 | 12 | 0.65 | 16 | 3 | 2 | |
| 6 | 15 | 0.65 | 16 | 3 | 2 | |
| 7 | 15 | 0.65 | 16 | 3 | 3 | +25% crit damage on this weapon |
| 8 | 18 | 0.55 | 16 | 4 | 3 | |

Feel: Ranged_Shoot on the upper body (Reload clip every 4th volley, cosmetic), tight "twang" with
pitch +-5%, thin teal tracer, crits add a high "tick" layer and a 20 ms per-enemy freeze.
Evolution **Widowmaker** (+ Keen Eye): 24 dmg, 0.50 s, 5 bolts, pierce all, +15% crit chance on
this weapon; crits ricochet to 1 more enemy within 5 m.

#### R2. Fan of Knives - [projectile], SELF (360 deg, ring rotates 15 deg per volley)
Knives (16 m/s) in all directions. Amount: +2 knives.

| R | Dmg | CD s | Range m | Knives | Pierce | Notes |
|---|---|---|---|---|---|---|
| 1 | 8 | 2.0 | 9 | 6 | 1 | |
| 2 | 8 | 2.0 | 9 | 8 | 1 | |
| 3 | 11 | 2.0 | 9 | 8 | 1 | |
| 4 | 11 | 2.0 | 9 | 8 | 2 | |
| 5 | 11 | 1.8 | 10 | 10 | 2 | |
| 6 | 14 | 1.8 | 10 | 10 | 2 | |
| 7 | 14 | 1.8 | 10 | 12 | 3 | |
| 8 | 18 | 1.5 | 10 | 12 | 3 | knives return to the rogue |

Feel: Melee_Attack_Spin (upper body) flourish, layered "shhk" fan sound, steel glints, knives
stick in the ground for 0.5 s after max range (unless returning).
Evolution **Storm of Steel** (+ Echo Quiver): 2 volleys 0.15 s apart, 16 knives each, 20 dmg,
pierce 4, always return.

#### R3. Assassin's Mark - [single target], highest current HP within range (boss > elite > other)
Marks targets (red skull glyph above them). All damage to marked targets is increased. Every
cooldown, a shadow clone appears behind each mark (0.15 s) and stabs. When a marked enemy dies it
bursts into seeking shadow daggers.

| R | Strike dmg | CD s | Range m | Marks | Bonus vs mark | Death daggers |
|---|---|---|---|---|---|---|
| 1 | 40 | 3.0 | 14 | 1 | +20% | 4 x 12 |
| 2 | 55 | 3.0 | 14 | 1 | +20% | 4 x 12 |
| 3 | 55 | 3.0 | 14 | 1 | +20% | 6 x 12 |
| 4 | 55 | 3.0 | 14 | 2 | +20% | 6 x 12 |
| 5 | 75 | 2.6 | 14 | 2 | +30% | 6 x 16 |
| 6 | 75 | 2.6 | 14 | 2 | +30% | 8 x 16 |
| 7 | 75 | 2.6 | 16 | 3 | +30% | 8 x 16 |
| 8 | 100 | 2.2 | 16 | 3 | +40% | 8 x 20 |

Feel: whisper + heartbeat thump when a mark is applied, clone uses the Stab clip with a smoky
afterimage, strike has 30 ms hit stop on the target only, death daggers fan out with a hiss.
Evolution **Death Sentence** (+ Lucky Knucklebone): 4 marks, strike 130 and always crits; non-boss
marks below 15% HP are executed instantly ("EXECUTED" text, 80 ms hit stop); daggers 10 x 24.

### 3.5 Shared weapons (any class)

| Weapon | Rarity | Targeting | Evolution (+ passive) |
|---|---|---|---|
| Spirit Darts | Common | NEAREST | Soul Hail (+ Tome of Insight) |
| Ricochet Disc | Uncommon | NEAREST, then bounces | Eclipse Disc (+ Everburn Wick) |
| Bramble Halo | Common | SELF aura | Briar Sanctum (+ Iron Heart) |
| Skewer Javelin | Common | FACING | Godspear (+ Whetstone) |
| Fire Flask | Uncommon | RANDOM_VIEW within 9 m | Hellfire Cask (+ Wide Sigil) |
| Storm Totem | Rare | placed at hero | Thunderspire (+ Quickening Hourglass) |
| Hunting Wisps | Rare | summon, RANDOM within 10 m | Wisp Choir (+ Lodestone Charm) |

Class weapons: starting weapons are owned at start; exclusive #2 is Uncommon, exclusive #3 Rare.

#### S1. Spirit Darts - [projectile] homing darts, 12 m/s (R7+: 14 m/s), turn rate 360 deg/s

| R | Dmg | CD s | Darts | Pierce |
|---|---|---|---|---|
| 1 | 10 | 1.20 | 1 | 1 |
| 2 | 10 | 1.20 | 2 | 1 |
| 3 | 13 | 1.20 | 2 | 1 |
| 4 | 13 | 1.10 | 3 | 1 |
| 5 | 13 | 1.10 | 3 | 2 |
| 6 | 17 | 1.10 | 4 | 2 |
| 7 | 17 | 1.00 | 4 | 2 |
| 8 | 21 | 1.00 | 5 | 2 |

Feel: soft pluck per dart, pale-blue trail, small pop on hit. Evolution **Soul Hail**: continuous
stream (1 dart per 0.12 s), 24 dmg, pierce 3, kills by darts drop +1 XP.

#### S2. Ricochet Disc - [projectile] 14 m/s, bounces to the next nearest enemy within bounce range

| R | Dmg | CD s | Bounce range m | Discs | Bounces |
|---|---|---|---|---|---|
| 1 | 12 | 1.60 | 5 | 1 | 3 |
| 2 | 12 | 1.60 | 5 | 1 | 4 |
| 3 | 16 | 1.60 | 5 | 1 | 4 |
| 4 | 16 | 1.60 | 5 | 2 | 4 |
| 5 | 16 | 1.45 | 6 | 2 | 6 |
| 6 | 20 | 1.45 | 6 | 2 | 6 |
| 7 | 20 | 1.45 | 6 | 3 | 6 |
| 8 | 25 | 1.30 | 6 | 3 | 8 |

Feel: each bounce raises pitch +1 semitone ("ping-ping-ping"). Evolution **Eclipse Disc**: 3
discs bounce without limit for 4 s, 30 dmg, +5% damage per bounce (max +100%).

#### S3. Bramble Halo - [area] damaging aura, also slows

| R | Dmg/tick | Tick s | Radius m | Slow |
|---|---|---|---|---|
| 1 | 4 | 0.50 | 1.8 | 15% |
| 2 | 4 | 0.50 | 2.1 | 15% |
| 3 | 6 | 0.50 | 2.1 | 15% |
| 4 | 6 | 0.50 | 2.1 | 25% |
| 5 | 8 | 0.50 | 2.5 | 25% |
| 6 | 8 | 0.40 | 2.5 | 25% |
| 7 | 10 | 0.40 | 2.9 | 25% |
| 8 | 13 | 0.40 | 2.9 | 35% |

Feel: thorny ground ring, rustle loop, no damage numbers for ticks under 10 (avoids spam).
Evolution **Briar Sanctum**: 16 dmg per 0.35 s, radius 3.6 m, slow 40%, enemies inside take +10%
damage from all sources, heals 0.5 HP per tick that hits at least 1 enemy.

#### S4. Skewer Javelin - [projectile] 18 m/s, range 14 m, hitbox 1.6 x 0.6 m, pierce all

| R | Dmg | CD s | Size | Javelins | Knockback m |
|---|---|---|---|---|---|
| 1 | 18 | 1.80 | 1.00 | 1 | 1.5 |
| 2 | 24 | 1.80 | 1.00 | 1 | 1.5 |
| 3 | 24 | 1.80 | 1.00 | 2 (12 deg fan) | 1.5 |
| 4 | 24 | 1.80 | 1.25 | 2 | 1.5 |
| 5 | 32 | 1.60 | 1.25 | 2 | 1.5 |
| 6 | 32 | 1.60 | 1.25 | 3 | 1.5 |
| 7 | 40 | 1.60 | 1.25 | 3 | 2.5 |
| 8 | 48 | 1.40 | 1.25 | 4 | 2.5 |

Feel: heavy whoosh, skewered enemies are dragged 0.5 m along. Evolution **Godspear**: 5 javelins,
60 dmg, each detonates at max range (radius 2.5 m, 60 dmg).

#### S5. Fire Flask - [area, lingering] lobbed (0.6 s arc), burning pool ticks every 0.4 s

| R | Dmg/tick | CD s | Pool r m | Flasks | Dur s |
|---|---|---|---|---|---|
| 1 | 6 | 3.5 | 1.5 | 1 | 2.5 |
| 2 | 6 | 3.5 | 1.5 | 2 | 2.5 |
| 3 | 8 | 3.5 | 1.5 | 2 | 2.5 |
| 4 | 8 | 3.5 | 1.8 | 2 | 2.5 |
| 5 | 8 | 3.5 | 1.8 | 3 | 3.0 |
| 6 | 11 | 3.5 | 1.8 | 3 | 3.0 |
| 7 | 11 | 3.0 | 2.1 | 3 | 3.0 |
| 8 | 14 | 3.0 | 2.1 | 4 | 3.5 |

Feel: glass shatter + whoomp ignition, flames lick 0.5 m high. Evolution **Hellfire Cask**: 6
pools of radius 3.0 m in a ring 5 m around the hero, 18 dmg per 0.4 s, 4 s.

#### S6. Storm Totem - [summon, lingering] placed at the hero, zaps every 0.5 s, chains

| R | Dmg | CD s | Range m | Totems | Chain | Dur s |
|---|---|---|---|---|---|---|
| 1 | 9 | 7.0 | 6.0 | 1 | 1 | 5 |
| 2 | 12 | 7.0 | 6.0 | 1 | 1 | 5 |
| 3 | 12 | 7.0 | 6.0 | 1 | 2 | 5 |
| 4 | 12 | 7.0 | 6.0 | 1 | 2 | 6 |
| 5 | 12 | 6.5 | 6.0 | 2 | 2 | 6 |
| 6 | 16 | 6.5 | 6.0 | 2 | 2 | 6 |
| 7 | 16 | 6.5 | 7.5 | 2 | 3 | 6 |
| 8 | 20 | 6.0 | 7.5 | 3 | 3 | 6 |

Totems after the first are placed 1.5 m away at 120 deg steps. Feel: crackle with random pitch
+-10%, jagged bolt mesh lives 0.08 s. Evolution **Thunderspire**: a floating rod follows the hero
permanently, zaps every 0.25 s, 22 dmg, chain 5, range 8 m.

#### S7. Hunting Wisps - [summon] spirit wisps orbit loosely, each dives at a random enemy within 10 m

| R | Dmg | Dive interval s | Dive AoE r m | Wisps |
|---|---|---|---|---|
| 1 | 12 | 1.4 | 0.8 | 2 |
| 2 | 12 | 1.4 | 0.8 | 3 |
| 3 | 16 | 1.4 | 0.8 | 3 |
| 4 | 16 | 1.2 | 0.8 | 3 |
| 5 | 16 | 1.2 | 0.8 | 4 |
| 6 | 21 | 1.2 | 0.8 | 4 |
| 7 | 21 | 1.2 | 1.2 | 5 |
| 8 | 27 | 1.0 | 1.2 | 5 |

Feel: whistling dive, soft splash of light. Evolution **Wisp Choir**: 8 wisps, 30 dmg, interval
0.8 s; wisps also carry gems within 15 m back to the hero.

---

## 4. Passive items (12)

| Passive | Rarity | Effect per rank | R1 | R2 | R3 | R4 | R5 | Evolves |
|---|---|---|---|---|---|---|---|---|
| Iron Heart | Common | +10% max HP, +0.2 HP/s | 10% / 0.2 | 20% / 0.4 | 30% / 0.6 | 40% / 0.8 | 50% / 1.0 | Dawnbreaker, Briar Sanctum |
| Plate Pauldron | Common | +2 Armor | 2 | 4 | 6 | 8 | 10 | Bastion Halo, Tectonic Rift |
| Windstride Boots | Common | +7% move speed | 7% | 14% | 21% | 28% | 35% | Maelstrom |
| Whetstone | Common | +8% Might | 8% | 16% | 24% | 32% | 40% | Skullsplitter, Godspear |
| Lodestone Charm | Common | +25% pickup radius | 25% | 50% | 75% | 100% | 125% | Wisp Choir |
| Wide Sigil | Uncommon | +8% Area | 8% | 16% | 24% | 32% | 40% | Final Verdict, Cataclysm, Hellfire Cask |
| Everburn Wick | Uncommon | +10% Duration, +10% projectile speed | 10% | 20% | 30% | 40% | 50% | Absolute Zero, Eclipse Disc |
| Tome of Insight | Uncommon | +8% Growth | 8% | 16% | 24% | 32% | 40% | Soul Hail |
| Keen Eye | Uncommon | +4% crit chance, +10% crit damage | 4/10 | 8/20 | 12/30 | 16/40 | 20/50 | Widowmaker |
| Lucky Knucklebone | Uncommon | +10 Luck | 10 | 20 | 30 | 40 | 50 | Death Sentence |
| Quickening Hourglass | Rare | -6% Cooldown | 6% | 12% | 18% | 24% | 30% | Sunfire Orb, Thunderspire |
| Echo Quiver | Epic | +1 Amount (max rank 2) | +1 | +2 | - | - | - | Storm of Steel |

Card text format (always numbers, old -> new): `Whetstone  R2 -> R3   Might +16% -> +24%`.

---

## 5. Enemies

### 5.1 Archetypes (base values at t = 0)

| Enemy | KayKit model / tint | HP | Speed m/s | Dmg | Radius m | KB resist | XP | Cost | From |
|---|---|---|---|---|---|---|---|---|---|
| Shambler (melee fodder) | Skeleton Minion | 10 | 2.6 | 6 | 0.35 | 0% | 1 | 1 | 0:00 |
| Grave Runner (fast) | Minion x0.85, cyan ghost tint | 6 | 4.4 | 5 | 0.30 | 0% | 1 | 1 | 1:00 |
| Bone Archer (ranged) | Skeleton Rogue (crossbow) | 14 | 2.4 | 10 | 0.35 | 0% | 2 | 2 | 2:00 |
| Shieldbearer (tank) | Skeleton Warrior (shield) | 45 | 2.0 | 12 | 0.50 | 70% | 4 | 3 | 3:00 |
| Cinder Skull (exploder) | Minion, orange emissive skull | 12 | 3.6 | 25 | 0.35 | 0% | 2 | 2 | 4:00 |
| Hex Caster (caster) | Skeleton Mage (staff) | 30 | 2.0 | 18 | 0.40 | 20% | 6 | 5 | 5:00 |

Per-archetype caps on alive count: Archers 120, Hex Casters 40, Cinder Skulls 150, summoned 200.
Over cap, the director substitutes Shamblers.

### 5.2 Scaling with time (t = spawn time in minutes, values frozen at spawn)

```
hp(t)     = base_hp     * (1 + 0.15 t + 0.02 t^2) * dread_k      t=5: x2.25  t=10: x4.5  t=14: x7.0
damage(t) = base_damage * (1 + 0.07 t)                            t=14: x1.98
speed(t)  = base_speed  * min(1.15, 1 + 0.01 t)
xp(t)     = base_xp     * (1 + 0.10 t)
```

Shambler HP by minute: 10, 11.7, 13.8, 16.3, 19.2, 22.5, 26.2, 30.3, 34.8, 39.7, 45.0, 50.7, 56.8,
63.3, 70.2. Shieldbearer HP: 45, 53, 62, 73, 86, 101, 118, 136, 157, 179, 202, 228, 256, 285, 316.

Tint tiers show the HP curve on screen (all archetypes, +5% scale per tier):
- 0:00-3:59 Bone (ivory #E8E0CC), 4:00-7:59 Ash (blue-grey #8C97A8), 8:00-11:59 Blood (crimson
  #B83A3A), 12:00+ Void (black-violet #3A2350 with violet eye emissive).

### 5.3 Attacks and telegraphs

No passive contact damage: every enemy hit comes from an attack with a windup (counterplay, and
the attack clip reads in the crowd). Damage applies at the hit moment if the hero is inside the
hit shape at that instant. Decals only for heavy attacks (budget 64 enemy decals; farthest culled).

| Enemy | Trigger | Windup s | Hit shape | Recovery s | Cooldown s | Clip | Telegraph |
|---|---|---|---|---|---|---|---|
| Shambler | hero within 1.0 m | 0.35 | cone 90 deg, 1.2 m | 0.45 | 1.2 | 1H chop | arm raise only |
| Grave Runner | within 2.6 m | 0.25 crouch | lunge 2.0 m in 0.15 s, circle 0.8 m at end | 0.60 | 1.5 | 1H stab | crouch + cyan eye flare |
| Shieldbearer | within 1.4 m | 0.60 | cone 120 deg, 1.6 m | 0.70 | 2.0 | 1H chop (heavy) | red cone decal fills 0.6 s |
| Bone Archer | 8-13 m, every 3.0 s | 0.80 aim | bolt 14 m/s, r 0.25 m, 18 m | 2.20 reload | 3.0 | shoot + reload | thin red aim line; locks 0.2 s before fire |
| Cinder Skull | within 1.8 m | 0.80 fuse | circle 2.5 m | dies | - | Idle + swell | orange circle decal + flash 4 -> 12 Hz |
| Hex Caster: Grave Circle | 6-11 m | 0.9 cast + 1.2 fill | circle 2.0 m at hero position at cast start | 0.8 | 6.0 | spellcast shoot | red circle decal fills 1.2 s |
| Hex Caster: Raise | < 6 allies within 6 m | 1.5 channel | spawns 4 Shamblers in a 1.5 m ring | 0.5 | 12.0 | spellcast summon | green ground motes |
| Hex Caster: Dark Ward | >= 10 allies within 6 m | 0.8 | allies in 6 m: +30% speed, -25% damage taken, 5 s | 0.5 | 12.0 | spellcast raise | violet ring + aura on buffed |

Behavior notes:
- Steering: seek the hero, separation from neighbors via spatial hash (cell 1.0 m, push
  6 m/s x overlap, max 8 neighbors checked), knockback velocity `v0 = 10 x distance`, damping 10/s.
- Bone Archer: approach if > 13 m, back off at 0.8x speed if < 8 m, else strafe; Shieldbearers
  block 30% of projectiles from their front 90 deg (spark, "tink", 0 damage, Block clip).
- Hex Caster keeps 7-11 m. Global limits: max 8 Grave Circles active, max 1 Raise per 2 s
  across all casters.
- Cinder Skull: if killed before its fuse ends it detonates after 0.3 s and damages **enemies
  only** (60 x damage(t)/6), so chain reactions reward the player. Its own fuse hurts the hero
  (25 x damage multiplier) and enemies (60).
- Hit reaction: every enemy hit plays a 0.15 s hit pose blend (or freezes its baked animation
  for 50 ms, see 10.1). Death: section 10.2.

### 5.4 Elites

- Any archetype. Scale 1.6x, HP x15, damage x1.5, knockback and stun immune (freeze 0.5 s max),
  gold rim emissive, small HP bar above the head, off-screen arrow, horn sound on spawn.
- Drops: chest (100%), coin bag 10 gold, Roast 50%. XP 25 x xp(t).
- From 8:00 every elite rolls one affix:
  - Hasted: +40% speed, windups x0.75 (yellow streaks).
  - Splitter: on death spawns 5 Grave Runners (0 XP).
  - Warded: a bubble absorbing 30% of max HP; Frozen or crit hits deal x2 to the bubble.
  - Commander: allies within 6 m +20% speed (banner ring on the ground).

### 5.5 Mini-bosses

**Gravewarden** (5:00) - Skeleton Warrior x2.6, Bone tint with gold trim. HP 3000, speed 2.4 m/s,
radius 1.1 m, KB/stun immune, damage not time-scaled.
- Shield Charge: lane decal 2 m x 10 m fills 1.0 s, charges at 12 m/s, 25 dmg, KB 3 m. Recovery
  1.2 s (dazed, +30% damage taken).
- Overhead Cleave: cone 120 deg, 3.5 m, windup 0.9 s, 30 dmg. Recovery 0.8 s.
- Bone Guard: every 20 s raises the shield for 3 s (Block clip): -80% damage from the front
  180 deg. Teaches flanking.
- Drops: chest (3 items; 5 items at 20% x luck factor), 30 gold, Lodestone, +1 reroll.

**Hollow Archmage** (10:00) - Skeleton Mage x2.6, Ash tint with violet flames. HP 10000, speed
2.2 m/s, keeps 7 m, radius 1.0 m.
- Bone Spiral: 3 arms of slow orbs (6 m/s) rotating 40 deg/s for 3 s, 12 dmg each. Windup 0.8 s
  (violet sigil under the boss).
- Grave Field: 5 circles of radius 2.5 m on and around the hero, fill 1.2 s, 35 dmg.
- Summon Guard: channel 2.0 s, 8 Shieldbearers in a ring of 4 m around itself (0 XP). Takes +50%
  damage while channeling.
- Blink: if the hero is within 3 m, teleports 8 m away (cooldown 6 s), leaving a 1.5 s ghost.
- Drops: chest (3 items; 5 items at 25% x luck factor), 60 gold, Lodestone, +1 reroll.

---

## 6. Wave director

### 6.1 Formulas

```
budget_rate(t) = (1.2 + 1.1 t + 0.07 t^2) * dread_k        threat points per second
min_alive(t)   = min(800, 20 + 50 t)                        floor: top up instantly if below
max_alive(t)   = min(3000, 300 + 200 t)                     hard cap on regular spawns
```

- The director accrues points every frame and spends them every 0.5 s in **groups** of 3-8 of
  one archetype (groups read better than singles). Archetype pick = weighted by the minute mix
  (6.3), cost from 5.1. Unspent points carry over (cap 3 s of budget).
- Floor top-up spends no budget, max 20 enemies per 0.5 s pulse, Shamblers/Runners only.
- Event spawns (rings, rushes, elites, bosses) are free (outside the budget).
- During mini-boss fights the budget is x0.5 (until the mini-boss dies or 90 s pass).

### 6.2 Placement

- Spawn ring: just outside the camera rectangle + 3 m margin (about 22-26 m from the hero).
- While the hero moves (> 1 m/s), 50% of groups spawn within +-60 deg of the velocity direction
  so the player runs into the tide; the rest are uniform.
- Recycling: regular enemies (not elites, not ranged in range) farther than 40 m from the hero
  are teleported to the forward spawn arc (keeps density without new budget).
- Visible spawns (rings, elites, boss adds) use a rise-from-ground: start 1.2 m below, rise over
  0.4 s with a dust puff and bone rattle; enemies are untargetable for the first 0.2 s.

### 6.3 Minute-by-minute timeline

Mix = share of regular spawns by count: Sh Shambler, Ru Runner, Ar Archer, SB Shieldbearer,
Ex Cinder Skull, HC Hex Caster. Budget at the start of the minute.

| Min | Budget pts/s | Min alive | Max alive | Mix Sh/Ru/Ar/SB/Ex/HC % | Tint | Events |
|---|---|---|---|---|---|---|
| 0 | 1.2 | 20 | 300 | 100/0/0/0/0/0 | Bone | 0:00 20 Shamblers placed 14-18 m around; 0:30 first Grave Lantern |
| 1 | 2.4 | 70 | 500 | 85/15/0/0/0/0 | Bone | 1:30 Swarm Rush A: 1 line x 24 Runners |
| 2 | 3.7 | 120 | 700 | 70/15/15/0/0/0 | Bone | 2:30 Encircle Ring A: 40 Shamblers, r 15 m |
| 3 | 5.1 | 170 | 900 | 60/15/15/10/0/0 | Bone | 3:00 Elite #1 (Shambler); Shieldbearers join |
| 4 | 6.7 | 220 | 1100 | 55/15/10/10/10/0 | Ash | 4:30 Elite #2 (Shieldbearer); Swarm Rush B: 2 lines x 30 |
| 5 | 8.5 | 270 | 1300 | 50/15/10/10/10/5 | Ash | 5:00 Mini-boss Gravewarden (budget x0.5) |
| 6 | 10.3 | 320 | 1500 | 45/15/12/12/8/8 | Ash | 6:00 Elite #3 (Archer); 6:30 Ring B: 60 Shamblers r 15 + 8 Shieldbearers r 12 |
| 7 | 12.3 | 370 | 1700 | 40/20/12/12/8/8 | Ash | 7:00 Elite #4; 7:30 Swarm Rush C: 3 waves x 40 Runners, 2 s apart, one side |
| 8 | 14.5 | 420 | 1900 | 40/15/12/15/10/8 | Blood | 8:00 Elites #5 + #6 (affixes start); 8:30 Exploder Ring: 40 Cinder Skulls r 16 m |
| 9 | 16.8 | 470 | 2100 | 38/15/12/15/10/10 | Blood | 9:00 Elite #7 (Caster); Shieldwall Ring: 50 Shieldbearers r 16 m at 1.2 m/s; 9:30 Archer Line (below) |
| 10 | 19.2 | 520 | 2300 | 35/15/14/16/10/10 | Blood | 10:00 Mini-boss Hollow Archmage (budget x0.5); 10:30 Elite #8 |
| 11 | 21.8 | 570 | 2500 | 35/15/12/16/10/12 | Blood | 11:00 Elite #9; Swarm Rush D: 4 lines x 40 crossing; 11:45 Elite #10 |
| 12 | 24.5 | 620 | 2700 | 30/20/12/18/10/10 | Void | 12:00 Ring D: 120 Shamblers r 15 + 30 Cinder Skulls r 18; 12:30 Elites #11 + #12 |
| 13 | 27.3 | 670 | 2900 | 35/20/10/15/12/8 | Void | 13:00 Last Tide: budget x1.5 for 45 s + Swarm Rush E: 6 lines; 13:15 Elite #13; 13:45 spawns stop |
| 14 | 12.1 (x0.4) | 150 | 1200 | 55/20/0/15/10/0 | Void | 13:50 warning banner; 14:00 The Hollow King rises; 16:30 hard enrage |

### 6.4 Event definitions

- **Encircle Ring**: N enemies evenly spaced on a circle around the hero's position at spawn,
  visible edge of the screen. They walk toward the ring center (fixed point) at 1.6 m/s for 8 s
  without chasing, then switch to normal chase. 1.5 s before: ground fog ring + rattling chorus.
- **Swarm Rush**: a line of Runners 20 m wide spawns 26 m away on one side and runs straight
  across at 6.5 m/s (no homing), despawning after 60 m. Warning 2.0 s before: edge-of-screen
  red chevrons on that side + rumble that crescendos.
- **Archer Line** (9:30): 24 Archers in a ring of 13 m aim together (synchronized 1.2 s aim lines)
  and fire one volley, then behave normally. Teaches using the active as a dodge.
- **Elite**: spawned at 20 m in the current mix; spawn beam of light for 0.6 s + horn.
- **Boss entrance**: at 13:45 spawns stop; 13:50 banner "The ground splits..." + bell toll x3;
  at 14:00 every regular enemy within 8 m of the boss spawn point dies (drops gems), the boss rises
  12 m in front of the hero over 3.0 s (camera zooms out 15%, music switches).
- **Grave Lanterns** (destructible, see section 8): every 45 s at 14-20 m, max 3 alive.

---

## 7. Boss - The Hollow King

A giant skeleton king: KayKit Skeleton Warrior at scale 4.5 (about 7.5 m tall), Void tint, green
eye and rib-cage emissive (the "core"), wielding a 2H great axe prop scaled up.

- HP: `clamp(1700 x player_level_at_spawn, 40000, 75000)` -> level 30 = 51000. Target kill time
  75-100 s for a level-30 build with ~800 single-target DPS at 75% uptime.
- Body radius 2.0 m (capsule), hard blocker. Contact: 20 dmg per 0.5 s when inside the radius.
- Immune to knockback and stun. Freeze becomes 30% slow for 1 s (8 s immunity after).
- Movement 2.6 / 3.0 / 3.4 m/s in phases 1/2/3; walks to 4 m of the hero, then attacks.
- Gap between attacks (end of recovery -> next windup): 1.6 s / 1.2 s / 0.9 s.
- Selection: weighted random among the phase's attacks, never the same attack twice in a row.
- Adds: regular director at 40% budget (Shamblers, Runners, Shieldbearers, Cinder Skulls only).

### 7.1 Phases

| Phase | HP | Attacks available | Telegraph scale | Notes |
|---|---|---|---|---|
| 1 | 100-65% | Cleave Sweep, Grave Slam, Bone Rain | 1.00 | learning phase |
| 2 | 65-30% | + Raise the Legion, Death Spiral | 0.90 | faster walk, more adds |
| 3 | 30-0% | + Soul Nova | 0.80 | core glows brighter, ground cracks pulse |
| Enrage | at 16:30 run time | all | 0.60 | damage x2, extra Bone Rain every 5 s, decals turn violet |

Transitions (at 65% and 30%): 2.5 s invulnerable, boss kneels (hit reaction) then roars
(spellcast raise). Shockwave pushes the hero 4 m (15 dmg) and kills all adds within 10 m (their
gems drop: a gift). Camera trauma 0.6, 100 ms hit stop, boss bar pip shatters.

### 7.2 Attacks

| Attack | Phase | Telegraph | Windup s (P1 / P3) | Shape | Damage | Recovery s | Weight |
|---|---|---|---|---|---|---|---|
| Cleave Sweep | 1+ | red arc decal fills from the boss outward; boss draws the axe back (2H slice) | 1.0 / 0.8 | arc 200 deg in front, r 6.5 m | 45, KB 4 m | 1.2 | 3 |
| Grave Slam | 1+ | rectangle 3 x 14 m toward the hero fills near -> far; overhead raise (2H chop) | 1.2 / 0.96 | line 3 x 14 m; spikes erupt along it at 0.04 s per meter | 55 | 1.4, axe stuck: +50% damage taken | 3 |
| Bone Rain | 1+ | 10 circles r 1.8 m (P3: 16): 3 on the hero at 0.3 s intervals, rest random within 9 m; each fills 1.4 s | 1.4 / 1.12 | circles | 30 each | 0.6 (keeps walking) | 2 |
| Raise the Legion | 2+ | boss plants the axe, kneels, core glows green; spawn motes appear on 2 rings at r 10 m around the hero | 2.0 channel | spawn 24 Shamblers + 6 Shieldbearers (P3 + 8 Cinder Skulls), 0 XP | - | channel = +50% damage taken | 2, cooldown 20 s |
| Death Spiral | 2+ | red ring r 3.5 m around the boss, spin-up whoosh | 0.8 / 0.64 | spins for 4.0 s chasing at 3.2 m/s (P3 3.8), circle r 3.5 m | 20 per 0.5 s | 1.5 dizzy, +50% damage taken | 2 |
| Soul Nova | 3 | arms raised (spellcast raise); 3 concentric ring decals with 2 gaps of 60 deg each, gaps rotate 30 deg per ring | 1.0 | rings expand from the boss at 6 m/s, 0.7 s apart, thickness 1.2 m, to r 20 m | 35 per ring | 1.0 | 2, cooldown 12 s |

### 7.3 Readability

- Enemy danger decals are red-orange #FF4A2A at 35% alpha (boss 45%) with a 2 px bright edge;
  the inner disc grows from 0 to 100% over the windup and flashes white for 0.06 s exactly at the
  hit moment. Player effects never use red (gold, cyan, violet only).
- Boss bar: top center, 60% screen width, name + "THE HOLLOW KING", phase pips at 65% and 30%,
  a white "ghost" chunk trails damage for 0.4 s then drains. Off-screen arrow when not visible.
- Recovery windows glow the core brighter (+50% emissive) to signal "hit me now".
- Big attacks have a unique audio sting 0.2 s into the windup (cleave: metal scrape; slam: deep
  inhale; nova: choir swell) so the player can react without looking.

### 7.4 Victory

Boss death: time scale 0.25 for 1.5 s, camera zooms in 10%, 300 bone fragments burst, an
expanding death wave (20 m/s) kills every enemy it reaches (cascade of pops), all gems vacuum,
5-item chest drops, 200 gold shower, victory screen after 4.0 s.

---

## 8. Pickups and drops

| Source | Drop | Chance | Notes |
|---|---|---|---|
| Any kill | XP gem | 100% | value = xp(t), section 2.2 |
| Any kill | Coin (1 gold) | 4% | x Greed |
| Any kill | Roast Haunch (heal 25% max HP) | 0.05% | max 3 on the ground |
| Any kill | Lodestone (vacuum all gems) | 0.03% | max 1 on the ground; pity: forced if none for 150 s and > 150 gems on the ground |
| Any kill | Holy Bell (screen clear) | 0.02% | min 120 s between drops |
| Any kill | Frost Hourglass (freeze screen 5 s) | 0.02% | min 120 s between drops |
| Elite | Chest | 100% | + coin bag 10 gold, Roast 50% |
| Mini-boss | Chest (3 or 5) | 100% | + 30 / 60 gold, Lodestone, +1 reroll |
| Boss | Chest (5) | 100% | + 200 gold |
| Grave Lantern (1 HP, every 45 s) | Roast 40%, coin pile 5 gold 25%, Lodestone 12%, Holy Bell 8%, Frost Hourglass 8%, gem cache (25 x xp(t)) 7% | - | glowing lantern on a pole, shatter sound |

Luck multiplies non-gem drop chances: `p = p_base * (1 + luck/100)`.

Pickup effects:
- **Holy Bell**: every non-elite enemy inside the camera rectangle + 2 m dies (gems drop),
  elites and bosses take 200 damage. Bell toll, white flash 0.15 s, camera trauma 0.5.
- **Frost Hourglass**: all enemies in view freeze 5 s (bosses slowed 50%); frozen take +25%.
- **Lodestone**: section 2.5. **Roast**: heal 25% max HP + green "+N" number.

Chests:
- Elite chest item count: 5 items at `2% x (1 + luck/100)`, else 3 items at `15% x (1 + luck/100)`,
  else 1 item. Mini-boss: 5 items at 20% / 25% (x luck factor), else 3. Boss: 5.
- Items are rank-ups of **owned** non-maxed items (weighted like level-up cards); evolutions
  first (section 2.7). If everything is maxed: 25 gold per item.
- Gold inside: 1 item 15-30, 3 items 45-90, 5 items 100-200 (x Greed).
- Opening: the hero stops time (pause), chest lid bursts, icons spin like a reel for 1.5 s (3 items
  2.0 s, 5 items 3.0 s), each lands with a thunk + rarity color flash; any button skips the reel.
- Scripted: the first elite chest of a save always gives 3 items (sets expectations).

Expected per winning run (no Luck/Greed): ~8000 kills -> ~320 gold from coins, ~13 elite chests,
~17 Roasts total, ~4 Lodestones, ~1-2 Holy Bells, ~1000 gold total.

---

## 9. Meta progression (light)

Gold is kept on death and victory. The Armory screen sells permanent ranks.

```
price(next_rank r) = base * r + 5 * total_ranks_owned_across_all_upgrades
```

| Upgrade | Effect per rank | Max ranks | Base | Prices (before the shared fee) |
|---|---|---|---|---|
| Vigor | +5% Max Health | 5 | 70 | 70, 140, 210, 280, 350 |
| Might | +4% Might | 5 | 100 | 100, 200, 300, 400, 500 |
| Plating | +1 Armor | 3 | 140 | 140, 280, 420 |
| Mending | +0.1 HP/s Regen | 5 | 70 | 70, 140, 210, 280, 350 |
| Swiftness | +3% Move Speed | 3 | 100 | 100, 200, 300 |
| Haste | -2.5% Cooldown | 3 | 200 | 200, 400, 600 |
| Reach | +5% Area | 3 | 140 | 140, 280, 420 |
| Magnetism | +15% Pickup Radius | 3 | 60 | 60, 120, 180 |
| Fortune | +5 Luck | 4 | 140 | 140, 280, 420, 560 |
| Insight | +4% Growth | 5 | 100 | 100, 200, 300, 400, 500 |
| Avarice | +8% Greed | 5 | 60 | 60, 120, 180, 240, 300 |
| Second Wind | +1 Revival | 1 | 1000 | 1000 |
| Reroll | +1 Reroll charge | 5 | 100 | 100 ... 500 |
| Banish | +1 Banish charge | 3 | 140 | 140, 280, 420 |

Totals: 53 ranks, 14,580 base + 6,890 fees = **21,470 gold** (about 22 winning runs or 40 mixed
runs). A first run that dies at 8:00 earns ~250 gold = 2-3 cheap ranks. Full refund button
(free) so players can experiment.

Hero unlocks:
- Knight: unlocked. Barbarian: defeat the Gravewarden once. Mage: reach level 20 in one run.
  Rogue: defeat the Hollow Archmage once.
- Recommendation for the sample: ship with `unlock_all_heroes = true` in the game settings asset;
  conditions are implemented and shown on the hero select screen as "locked hint" text.
- Dread (0-5) unlocks after the first victory (formulas in 1.2); highest Dread cleared shows as
  skulls on the hero card.

---

## 10. Game feel checklist

### 10.1 Hits
- [ ] Enemy hit flash: per-instance emissive white overlay, intensity 1.0 -> 0 over 0.08 s
      (restart on re-hit). Requires a per-instance flash float in the instance data.
- [ ] Per-enemy hit freeze: baked animation time held for 50 ms on hit (cheap: skip time advance).
- [ ] Global hit stop (time scale 0): 30 ms melee multi-hit (>= 6 targets), 40-60 ms heavy
      weapons (Verdict, Starfall, Ravager Chop >= 4 targets), 80 ms actives / executes, 100 ms boss
      phase change. Max one global hit stop per 0.30 s; never while a menu is open.
- [ ] Knockback: impulse along hit direction, `v0 = 10 x distance` m/s, damping 10/s, scaled by
      (1 - KB resist) and hero Knockback stat. Max stacked knockback speed 15 m/s.
- [ ] Hit squash: enemy scale (1.15, 0.85, 1.15) -> 1.0 over 0.10 s (per-instance transform).
- [ ] Hit sparks: 3-6 particles per hit, bone-white chips; cap 400 hit particles per second.
- [ ] Crit: yellow number 1.3x size + "!", 20 ms freeze on that enemy, sharp "tick" sound layer.

### 10.2 Kills
- [ ] Death: play the death clip from 0 to 0.4 s, then shrink to 0 over 0.15 s with a dust puff
      and 6-10 bone fragments (GPU particles, 0.8 s life, gravity 12 m/s^2, bounce 0.3).
- [ ] Death-clip budget 300 simultaneous; beyond that, instant pop (fragments only).
- [ ] Overkill (damage > 2x remaining HP) or heavy weapons: launch the body 1-2 m up and 1-3 m
      away from the hit, spin 360-720 deg/s, ballistic for 0.6 s, then pop.
- [ ] Gem drops with a 0.25 s hop (0.4 m high) from the corpse.
- [ ] Kill counter on the HUD pops 1.1x scale for 0.06 s per 10 kills (not per kill).
- [ ] Elite death: 60 ms hit stop, gold burst, chest drops with a bounce and a light beam 2 s.

### 10.3 Damage numbers
- [ ] Pooled, max 256 alive, max 40 new per second (overflow merges into existing numbers).
- [ ] Motion: rise 0.9 m over 0.6 s ease-out, random x offset +-0.3 m, pop scale 1.4 -> 1.0 in
      0.08 s, fade during the last 0.2 s. Always drawn on top, outlined (2 px dark).
- [ ] Stacking: hits on the same enemy within 0.25 s merge into one number that counts up and
      re-pops at 1.2x.
- [ ] Colors: white normal, yellow crit, orange burn, cyan frost, violet echo, green heal,
      red damage taken by the hero (1.2x, shakes 2 px).
- [ ] Settings: Off / Crits and big hits only (>= 50) / All.

### 10.4 Hero damage and danger
- [ ] Hero hit: red rim flash 0.10 s, 0.25 s invulnerability with 10 Hz blink, grunt voice
      (max 1 per 0.5 s), push surrounding enemies 1.0 m away, camera trauma 0.25
      (0.45 if the hit was > 20% max HP).
- [ ] Low health (< 30% HP): red vignette alpha = (0.30 - hp%) / 0.30 x 0.6; heartbeat 1 Hz;
      under 15%: 1.6 Hz heartbeat and music low-pass at 800 Hz.
- [ ] Hero health ring under the feet + bar above head (both): green > 50%, amber > 25%, red.
- [ ] Off-screen danger chevrons for Swarm Rushes and incoming archer volleys.

### 10.5 Pickups and level-up
- [ ] Gem vacuum: hop 0.25 m back over 0.10 s, then `4 + 40 s^2` m/s, cap 30 m/s.
- [ ] Gem pickup sound chain: +1 semitone per gem within 0.3 s of the previous, max +12, resets
      after 0.5 s; max 4 gem voices.
- [ ] XP bar fill eases over 0.15 s with a bright leading edge; full bar flashes before level-up.
- [ ] Level-up: time scale 1.0 -> 0.3 over 0.15 s, golden ring burst from the hero (pushes
      enemies within 4 m back 3 m, 0 damage), rising chime + choir pad, then the card picker.
- [ ] Card picker: cards fly in from the bottom staggered 0.06 s, hover/focus scale 1.06,
      selected card flashes and the icon flies to its HUD slot (0.35 s).
- [ ] Chest reel: section 8.

### 10.6 Camera
- [ ] Follow with critically damped spring (frequency 6 Hz), look-ahead 1.5 m along velocity.
- [ ] Shake = trauma model: offset = max_offset x trauma^2 x noise(25 Hz); max translation
      0.35 m, max roll 1.5 deg; trauma decays 1.5 per second; trauma adds are listed per weapon.
- [ ] Global shake slider 0-100% (0 disables), applied to translation and roll.
- [ ] Boss: zoom out 15% during the fight; victory zoom in 10%.

### 10.7 Audio
- [ ] Random pitch +-6% and volume +-2 dB on every SFX instance.
- [ ] Voice limits per sound type: enemy hit 8, enemy death 6, gem 4, coin 3, weapon fire 4 per
      weapon; steal the oldest. Global SFX voices 48. Min retrigger 30 ms per sound type.
- [ ] Layering for impacts: transient (0-30 ms) + body (50-200 ms) + tail; heavy weapons add
      sub-bass under 80 Hz.
- [ ] Music intensity layers driven by alive count: 0-200 base, 200-800 + percussion,
      800+ + choir; boss track separate; 0.5 s crossfades.
- [ ] Ducking: level-up and chest open duck SFX by -6 dB.

### 10.8 Readability
- [ ] Enemy telegraphs red-orange only; player effects never red.
- [ ] Player VFX opacity slider (100% / 60% / 30%) for late-game clutter.
- [ ] Hero always drawn with a bright rim outline (visible through dense crowds).

---

## 11. UI screens

1. **Title**: logo, Play, Armory (meta shop), Settings, Quit; animated background: the hero idle
   on the moor with skeletons rising in the distance. Gamepad and touch navigable.
2. **Hero select**: 4 cards (model turntable, name, HP / Armor / Speed bars, signature passive
   text, active text, starting weapon icon). Locked heroes show the unlock hint. Dread selector
   (after the first win). Start button.
3. **HUD**:
   - Top-left: XP bar across the full top edge (2 px glow), level number at its left.
   - Top-center: run timer `MM:SS` (large), kill count with skull icon below.
   - Top-right: gold count with coin icon.
   - Left column: 6 weapon icons with rank pips (8 pips; gold frame if evolved, pulsing if evolution
     ready), below them 6 passive icons with rank pips (5).
   - Bottom-center (desktop) / bottom-right (touch): active ability icon with radial cooldown.
   - Hero: health ring under the feet + small bar above the head.
   - Boss bar (section 7.3), elite/chest off-screen arrows, event banners (2.5 s, center top).
   - Touch: floating joystick on the left half of the screen (radius 80 px, dead zone 10%).
4. **Level-up picker**: pause, dim the world 50%. 3 cards side by side (stacked on portrait phones):
   rarity frame color (Common grey #B8B8B8, Uncommon green #4CCB6A, Rare blue #4C8DFF, Epic violet
   #B05CFF, Evolution gold #FFC94C), item icon, name, "NEW" or "R3 -> R4" tag, description with
   numbers (old -> new), evolution hint line ("Evolves with Wide Sigil" + small icon). Buttons:
   Reroll (n), Banish X per card (n), Skip (+5 gold). Keys 1/2/3, D-pad, tap.
5. **Chest opening**: reel described in section 8, result list, Continue.
6. **Pause / build summary**: weapons and passives with ranks and a short stat line each; full stat
   sheet (all section 1.1 stats with current values and armor as % reduction); damage dealt per
   weapon so far; Resume, Settings, Abandon run.
7. **Game over / Victory**: result banner, time survived, level, kills, gold earned (with Greed
   bonus shown), table per weapon: total damage, DPS while owned, kills, time owned (sorted by
   damage); highest hit; damage taken; buttons Retry (same hero), Hero select, Armory.
8. **Armory (meta shop)**: grid of section 9 upgrades, rank pips, price, Buy; Refund all; gold total.
9. **Settings**: master / music / SFX volume (0-100), screen shake (0-100%), damage numbers
   (Off / Big only / All), player VFX opacity, show enemy HP bars on elites (on/off), vibration
   (on/off), touch joystick side (left/right), language placeholder, frame rate cap.

---

## 12. Balancing targets

From the spawn model in section 6 (steady state: kills = spawns, +15% for events).

| Min | Level (end of min) | Weapons owned | Avg weapon rank | Shambler HP | Shieldbearer HP | Kills/s | Alive (typical) | XP gained this min | AoE DPS needed | Single-target DPS | Hero max HP (Knight-like) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 5 | 2 | 1.5 | 10 | - | 1.8 | 20-60 | 130 | 22 | 15 | 130 |
| 2 | 7-8 | 2-3 | 2 | 12 | - | 3.0 | 70-120 | 240 | 41 | 25 | 130 |
| 3 | 9-10 | 3 | 2.5 | 14 | 62 | 3.8 | 120-200 | 380 | 66 | 40 | 140 |
| 4 | 11-12 | 3-4 | 3 | 16 | 73 | 4.4 | 170-260 | 620 | 120 | 60 | 140 |
| 5 | 13-14 | 4 | 3.5 | 19 | 86 | 5.4 | 220-350 | 840 | 175 | 90 | 150 |
| 6 | 16 | 4-5 | 4 | 23 | 101 | 5.9 | 270-450 | 1300 | 240 | 120 | 155 |
| 7 | 18 | 5 | 4.5 | 26 | 118 | 6.4 | 320-600 | 1460 | 330 | 160 | 160 |
| 8 | 20 | 5 | 5 | 30 | 136 | 7.6 | 370-800 | 1830 | 445 | 210 | 170 |
| 9 | 22 | 5-6 | 5.5 | 35 | 157 | 8.5 | 420-1000 | 2300 | 615 | 270 | 175 |
| 10 | 24 | 6 | 6 | 40 | 179 | 9.4 | 470-1200 | 2790 | 790 | 330 | 180 |
| 11 | 26 | 6 | 6.5 | 45 | 202 | 10.4 | 520-1500 | 3530 | 1020 | 420 | 190 |
| 12 | 28 | 6 (1 evolved) | 7 | 51 | 228 | 11.4 | 570-1900 | 3960 | 1280 | 520 | 200 |
| 13 | 30 | 6 (1-2 evolved) | 7.5 | 57 | 256 | 13.1 | 620-2500 | 4680 | 1640 | 650 | 210 |
| 14 | 32 | 6 (2-3 evolved) | 8 | 63 | 285 | 15.7 | 670-3000 | 5350 | 2000 | 800 | 220 |
| 15 (boss) | 33 | 6 | 8 | 70 | 316 | 8.8 | 150-1200 | 2380 | 1100 | 800 | 220 |

Time-to-kill rules:
- Fodder (Shambler, Runner): 1 hit from the main weapon at its expected rank at every minute; 2
  hits from an under-ranked weapon. Check: Knight R1 14 dmg vs Shambler 13.8 at 2:00; R5 24 vs
  22.5 at 5:00; R8 36 x 1.24 Might vs 45 at 10:00.
- Tanks (Shieldbearer): 4-6 hits of the main weapon (about 2-3 s of focused fire).
- Elites: 5-8 s of total single-target DPS (Shieldbearer elite at 8:00 = 2350 HP vs ~300 DPS).
- Mini-bosses: 20-30 s (Gravewarden 3000 vs ~120 DPS at 5:00; Archmage 10000 vs ~330 at 10:00).
- Boss: 75-100 s (51000 vs ~800 DPS at 75% uptime).

Per-weapon sanity check: at rank 8 with +30% Might, each weapon should deliver 300-600 AoE DPS
into a crowd of density 1 enemy per m^2. Two weapons below 200 = too weak; above 900 = too strong
(evolutions may reach 1200).

Player damage taken targets: a Shambler hit at 14:00 = 12 raw; with 10 Armor = 7. Fully
surrounded (4 hits per second through the 0.25 s window) = ~28 HP/s -> a 220 HP hero lasts ~8 s,
long enough to react with the active. Boss attacks: 30-55 raw, 3-5 mistakes = death.

Difficulty curve rationale:
- Three acts: 0-5 min onboarding (one new archetype per minute, single-hit kills, level-up every
  ~20 s), 5-10 min build-defining (casters, elites with chests, first evolutions become
  possible, events every 30-60 s), 10-15 min crescendo (Void tier, 2000-3000 alive, Last Tide,
  boss).
- Enemy HP grows ~7x by minute 14 while a focused build grows ~15-25x in AoE output, so the
  late game feels powerful; pressure is kept by the min-alive floor (a strong player always faces
  600+ enemies) and the 3000 max-alive cap (a weak build gets swamped by 11-13 min).
- Enemy damage only doubles, so death comes from density and telegraphed heavies, which are
  readable and dodgeable; armor and HP passives matter mostly for the boss.
- Saw-tooth pressure: each event spikes density for ~20 s, then gems and chests reward survival;
  the mini-bosses halve the budget so their fights are readable duels.
- First-run target: a new player dies between 8:00 and 12:00 (earning 250-600 gold); after ~5
  meta ranks and a few runs, the boss becomes reachable; an experienced player wins ~70%.

Telemetry to log per run (for tuning): level per minute, alive count per 10 s, damage per weapon,
damage taken per source, death cause and time, picks offered vs taken.

---

## 13. Implementation notes for this engine

- Enemies are per-member data in arrays (structure of arrays): position, velocity, knockback
  velocity, hp, max_hp, archetype, tint tier, elite affix, AI state, state timer, attack target
  point, anim clip, anim time, flash timer, freeze/chill timers, mark flag. One instanced baked
  animation draw per (archetype, tint) bucket; elites and bosses can be separate entities.
- Spatial hash grid (cell 1.0 m) rebuilt every frame; used for separation, weapon overlap
  queries, cluster targeting (top 8 cells) and nearest-enemy queries.
- Steering and AI stepping parallel over members; damage application and death spawning on the
  main thread from per-thread event lists (no engine calls from workers).
- Projectiles, gems, coins, damage numbers and decals are pooled arrays with instanced draws.
- Deterministic RNG per run seed (level-up offers, drops, director) for reproducible tuning.
- Data-driven: weapons, passives, enemies and the minute table live in one data file so tuning
  needs no recompilation.

---

## Sources

- Vampire Survivors wiki, Level up (XP steps, 3-4 options, rarity weights, 6 slots, reroll/skip/banish): https://vampire.survivors.wiki/w/Level_up
- Vampire Survivors wiki, Experience Gem (gem colors by value, 400-gem cap with a single red overflow gem): https://vampire.survivors.wiki/w/Experience_Gem
- Vampire Survivors wiki, PowerUp (meta ranks, per-rank bonuses, price formula with shared fee): https://vampire.survivors.wiki/w/PowerUp
- Vampire Survivors wiki, Mad Forest (minute-by-minute minimum enemies, spawn intervals, swarms, walls): https://vampire.survivors.wiki/w/Mad_Forest
- Vampire Survivors wiki, Treasure Chest (1/3/5 item chances, luck scaling, evolution via chests): https://vampire.survivors.wiki/w/Treasure_Chest
- Vampire Survivors wiki, Magic Wand (typical 8-rank weapon progression pattern): https://vampire.survivors.wiki/w/Magic_Wand
- Brotato wiki, Armor (hyperbolic armor formula 1 / (1 + A/15)): https://brotato.wiki.spellsandguns.com/Armor
- Brotato wiki, Luck (luck shifts tier odds, diminishing caps): https://brotato.wiki.spellsandguns.com/Luck
- The Secret Sauce of Vampire Survivors (reward cadence, multi-tier upgrades, scripted early chests, light meta): https://jboger.substack.com/p/the-secret-sauce-of-vampire-survivors
- Wayline, Game Feel and Juice (hit stop 50-100 ms on heavy hits): https://www.wayline.io/learn/game-feel/1
- Unity game feel primer (hit flash ~0.08 s, hit stop and shake practice): https://uhiyama-lab.com/en/notes/unity/unity-game-feel-hit-feedback/
- Prima Games, Death Must Die boss guide (telegraphed dash lines and spike patterns): https://primagames.com/tips/how-to-defeat-left-and-right-barons-hand-in-death-must-die
- Common Sense Gamer, Halls of Torment Swordsman (base stat sheet: block, crit, regen, pickup range): https://commonsensegamer.com/halls-of-torment-best-swordsman-build/

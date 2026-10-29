# Barrowlight update: Oaths, Boons and the Blood Price

Design pass on top of design_brief.md. Goals, in priority order:

1. Every class heals, each in its own way, and nobody heals for free. Survival comes from skill (moving,
   timing the active), not from an unbounded passive.
2. More class identity: two new class weapons per class, each feeding the class signature mechanic
   (Bulwark, Fury, Arcane Echo, Momentum) and the other class weapons (stun, fear, freeze/burn, crit).
3. Passive items matter: each gets a trait you can feel, and the evolutions it unlocks keep growing with it.
4. Slotless boons: small, unlimited, with diminishing returns, so late levels stay meaningful and nothing breaks.
5. A dropped chest is impossible to miss, even under 1000 skeletons.

## 1. Diagnosis

| Problem | Cause in the current build |
|---|---|
| Barbarian never dies | Full Fury heals 0.5 HP per kill with no cap. Late game kills run 50-150/s, so 25-75 HP/s against ~50 DPS when surrounded (hits land at most every 0.25 s). |
| Other classes cannot heal | Only Iron Heart regen (max 1 HP/s), Briar Sanctum, rare roasts and the Hearty Meal fallback. |
| Passives are invisible | Pure +x% stat sticks; once an evolution exists its passive no longer matters. |
| Late levels are empty | When items are maxed the cards are Coin Purse / Battle Hymn / Hearty Meal. |
| Chests vanish | The chest is a 1 m mesh at ground level; the arrow only shows off screen. |

## 2. Healing economy: "the Blood Price"

Rule of thumb: sustained self-healing per class stays at or below ~2% max HP per second, and big heals are
deliberate (a card, a pickup, a well-timed active).

| Class | Heal | Numbers | Skill expression |
|---|---|---|---|
| Barbarian | Bloodlust, full Fury | kills heal 0.25 HP each, **capped at 1% max HP/s** (was 0.5 HP per kill, no cap) | stay in the fight to keep Fury |
| Barbarian | Earthshaker Leap, Blood Price | landing with >= 30 Fury spends 30 Fury and heals 5% max HP; max health 150 -> 140 | trade damage for life, on demand |
| Knight | Bulwark | every block heals 2.5% max HP; block chance 15% -> 20%, regen 0.2 -> 0.4 HP/s | stand your ground |
| Mage | Blink, Arcane Ward | blinking wards her for 3 s: the next 3 hits are absorbed; max health 85 -> 95 | blink into safety, not just away |
| Rogue | Momentum, Bloodletting | at full Momentum, crits heal 0.2 HP, capped at 1.5% max HP/s; max health 95 -> 105, +1 Armor, Might +15%, crossbow pierce +1 | never stop moving |
| Anyone | Iron Heart: Heartbeat | every 10 s heal 1.5% max HP per rank | |
| Anyone | Lucky Knucklebone: Lucky Break | kills may drop a roast (25% max HP), at most one per 30 s | |
| Anyone | Hearty Meal card | below 50% health one level-up card may be Hearty Meal (heal 30%) | heal or power, your call |
| Anyone | Boons Vitality / Mending | small max HP / regen, unlimited | |

Fury also decays faster once it starts (5 -> 8 per second after the 2 s grace), so the barbarian has to keep
cutting to keep the cap, and each Fury gives +0.8% Might and -0.3% Cooldown (was +1% / -0.4%): full Fury is x1.65
damage output instead of x1.9.

## 3. Boons (slotless, unlimited)

Boons sit beside weapons and passives: no slot, no max rank, small numbers. Each pick gives a little less than
the one before, `value(n) = base / sqrt(n)` for the n-th pick, so the total grows like `2 x base x sqrt(N)`:
10 picks are worth ~5 bases, 25 picks ~8.7 bases. The stat caps of section 1.1 still apply (Area 200%,
Cooldown 50%, Move Speed 8 m/s, Pickup 6 m, ...), so nothing runs away.

| Boon | Per first pick | After 10 | After 25 |
|---|---|---|---|
| Fleetfoot | +3% Move Speed | +15% | +26% |
| Magnetism | +10% Pickup Radius | +50% | +87% |
| Vitality | +4% Max Health | +20% | +35% |
| Mending | +0.1 HP/s | +0.5 HP/s | +0.87 HP/s |
| Hide | +0.6 Armor | +3.0 | +5.2 |
| Edge | +2.5% Might | +12.5% | +22% |
| Alacrity | -1.5% Cooldown | -7.5% | -13% |
| Breadth | +2.5% Area | +12.5% | +22% |
| Persistence | +3% Duration and Projectile Speed | +15% | +26% |
| Precision | +1.2% Crit Chance | +6% | +10.5% |
| Insight | +3% Growth | +15% | +26% |
| Plunder | +4% Greed | +20% | +35% |
| Clover | +2 Luck | +10 | +17 |

Offer rules (replaces the fallback cards, design 2.6):
- While item cards exist, at most **one** card of three is a boon, shown on 35% of level-ups (always when fewer
  than 3 item cards exist).
- When the item pool is empty, all three cards are boons (Battle Hymn and Coin Purse retire: Edge and Plunder
  replace them).
- Below 50% health one card may be Hearty Meal instead.
- A chest item that finds nothing to rank up gives a boon pick instead of 25 gold.
- Boons have their own teal frame and a "BOON" tag; the card reads `Edge  +2.1% Might  (total +12.5%)`. The
  pause sheet lists every boon with its count and total.

## 4. Passive items: traits and bonds

Each passive keeps its stat and gains:
- a **trait**, scaled by rank, that helps any build but is themed on what it evolves;
- a **bond**: an evolution whose passive is ranked deals **+6% damage per rank** of that passive (R5 = +30%), so
  ranking the passive after evolving stays worth it.

| Passive | Stat (unchanged) | Trait (per rank) | Evolves |
|---|---|---|---|
| Iron Heart | +10% Max HP, +0.2 HP/s | **Heartbeat**: every 10 s heal 1.5% max HP | Dawnbreaker, Briar Sanctum |
| Plate Pauldron | +2 Armor | **Spiked Plate**: when struck, spikes burst for 12 + 3 x Armor damage in 2.5 m (once per 0.5 s) | Bastion Halo, Tectonic Rift, Warlord's Bellow |
| Windstride Boots | +7% Move Speed | **Tailwind**: 3% chance to dodge a hit (shows DODGE) | Maelstrom, Legion of Shadows |
| Whetstone | +8% Might | **Opening Strike**: +6% damage to enemies above 75% health | Skullsplitter, Godspear |
| Lodestone Charm | +25% Pickup Radius | **Soul Magnet**: every 65 - 9 x rank s (R5: 20 s) every gem on the map flies to you | Wisp Choir, Maw of the Pit |
| Wide Sigil | +8% Area | **Aftershock**: area hits (radius >= 1.5 m) pulse again 0.5 s later for 8% of their damage | Final Verdict, Cataclysm, Hellfire Cask |
| Everburn Wick | +10% Duration, +10% Proj. Speed | **Kindle**: burns, chills and freezes last 12% longer; burns tick 8% harder | Absolute Zero, Eclipse Disc, Eternal Sanctum |
| Tome of Insight | +8% Growth | **Epiphany**: each level-up releases a nova (7 m) for (15 + 3 x level) damage | Soul Hail, Aurora Lance |
| Keen Eye | +4% Crit, +10% Crit Dmg | **Exploit**: crits expose the enemy for 3 s, +4% damage taken | Widowmaker, Nightshade Plague |
| Lucky Knucklebone | +10 Luck | **Lucky Break**: kills drop a coin 0.8% of the time and a roast (once per 30 s) 0.5% of the time | Death Sentence, Skybreaker |
| Quickening Hourglass | -6% Cooldown | **Overdrive**: after your active, weapons recharge 25% faster for 0.6 s | Sunfire Orb, Thunderspire |
| Echo Quiver | +1 Amount (2 ranks) | (none: already the strongest stat) | Storm of Steel, Tempest Crown |

## 5. New class weapons

Each class gets an Uncommon and a Rare weapon (5 class weapons each, 12 weapons per class pool). Each one
feeds the class signature and the existing class weapons. Numbers are rank 1 / rank 8; the full tables live in
WeaponCatalog.

### Knight: hold the line
- **Consecration** (Uncommon, area, lingering): plants a golden glyph (2.6 -> 3.8 m) under the knight for 4 -> 6
  s; holy damage per 0.5 s (6 -> 16), enemies inside are judged (+10% damage taken), the knight gets +2 -> +6
  Armor while inside, and Bulwark bashes inside it are twice as large. Evolution **Eternal Sanctum** (+ Everburn
  Wick): the glyph never fades and is replanted where the knight stands every 6 s; 4.6 m, 22 per 0.4 s, +8
  Armor, a holy pulse every 2 s (40 damage, knockback).
- **Dawn Hammer** (Rare, projectile): spinning golden hammers fly out 8 -> 10 m and return, stunning 0.4 -> 0.6
  s. A hammer striking an already stunned enemy calls a thunderclap (2 m, 20 -> 56). Shield Rush and Verdict
  stun, so the knight's kit chains. Evolution **Skybreaker** (+ Lucky Knucklebone): 4 hammers, any hit may call
  the thunder (10% + Luck / 2 %), thunder chains to 2 more enemies.

### Barbarian: pull them in, break them
- **War Cry** (Uncommon, area): a roar every 8 -> 6.5 s (5 -> 7 m): damage, enemies flee for 1.0 -> 1.8 s and
  take +10% -> +25% damage, and every enemy hit feeds 0.5 -> 1 Fury. Survival through control, not healing.
  Evolution **Warlord's Bellow** (+ Plate Pauldron): every 5 s, 8 m, fear 2 s, +30% damage taken, and the
  barbarian gains +10 Armor for 3 s.
- **Butcher's Chains** (Rare, projectile, pull): hooked chains (1 -> 4) fly 9 -> 11 m into the crowd and yank
  everything along them 4 -> 8 m toward the barbarian, into the axes. Yanked enemies are bloodied: the next
  barbarian melee hit on them deals +40% and grants an extra Fury. Evolution **Maw of the Pit** (+ Lodestone
  Charm): every 5 s a blood vortex drags every enemy within 10 m to the barbarian, then the chains slam (80, 3 m).

### Mage: elements that talk to each other
- **Arc Lash** (Uncommon, chain): a bolt jumps between 3 -> 8 enemies. Frozen targets take double damage
  (shatter sparks), burning targets pass their burn on to the next jump. Echo can recast it. Evolution
  **Tempest Crown** (+ Echo Quiver): a storm crown orbits the mage; every 0.5 s a 10-jump bolt, forking in two
  at every frozen or burning enemy.
- **Prismatic Ray** (Rare, beam): a beam of light sweeps a full circle around the mage (7 -> 10 m, 1 -> 3
  beams), burning and chilling what it crosses. An enemy that is burning and chilled (or frozen) suffers
  **thermal shock**: it bursts for 40 + 4 x level damage in 1.6 m (once per 2 s per enemy). Ember Orb, Starfall,
  Rime Nova and the ray all feed it. Evolution **Aurora Lance** (+ Tome of Insight): 6 beams in a double helix,
  always on, thermal shock deals double.

### Rogue: the faster she moves, the deadlier
- **Venom Shivs** (Uncommon, projectile): 2 -> 5 poisoned shivs at nearby enemies; venom stacks (max 5 -> 10).
  A rogue crit on an enemy with 3+ stacks ruptures it: all remaining venom at once (x3) and half the stacks
  splash to enemies within 2.5 m. Momentum crits make it a chain reaction. Evolution **Nightshade Plague** (+
  Keen Eye): no stack cap, ruptures can trigger ruptures, a lingering toxic cloud.
- **Shadow Dancers** (Rare, summon): with 3+ Momentum, shadow clones are left behind every 3.0 -> 1.5 m of
  movement (max 2 -> 6, 3 -> 4 s); they throw knives at the nearest enemy and burst when they fade. Standing
  still makes none. Evolution **Legion of Shadows** (+ Windstride Boots): a clone every metre (max 10), 5 s,
  with half the rogue's crit chance; Smoke Roll leaves 4 clones around the cloud.

## 6. Chest beacon

- On drop: a light pillar (12 m, gold; violet for 3 items, prismatic for 5) shoots up and stays above the
  chest, readable over any crowd; a ground ring pulses every second; a bell chime plays.
- On screen: a bobbing chest icon above it in the overlay (drawn over the horde). Off screen: the existing
  edge arrow.

## 7. Verification

- Telemetry: healing per source and damage taken per minute in RunRecord, printed by DevReport.
- Autopilot runs per class (no invulnerability): survival time, minimum health, healing as a share of damage
  taken. Target: no class heals more than ~35% of what it takes after 8:00; all four classes survive similar
  times with the same autopilot.
- Captures of every new weapon, evolution and the beacon at the S26 and Full HD presets.

## 8. Phases

1. Healing economy, telemetry, chest beacon.
2. Boons, Hearty Meal rule, chest boons, UI (cards, pause sheet).
3. Passive traits and evolution bonds.
4. New class weapons (one agent per class: behaviour, looks, effects), catalog + icons by the lead.
5. Balance runs, tuning, captures.

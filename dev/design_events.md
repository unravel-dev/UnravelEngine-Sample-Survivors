# Map events (design update 2026-10-29)

Optional challenges between the director's waves. Each asks for a different verb than "walk in circles and let the
weapons fire": hold ground, chase, push into a fortified spot, or trade safety for health. Each pays with something
the player can see coming: the tracker's icon shows the prize.

## Rules shared by all events

- One event at a time. The first at 1:30, the next 70-100 s after the last one ended. None starts while a mini-boss
  fights or after 12:30 (the Last Tide and the Hollow King own the end): about 6-8 events per run.
- Placed 18-24 m from the hero on open ground (clear of arena obstacles, 10 m inside the edge); the robber 12 m away.
- Announced with a banner. A light pillar stands over a waiting event; a badge with the prize's icon floats over it
  on screen and an arrow points to it off screen.
- The tracker (right, under the gold) shows the title, a one-line goal, the clock and a bar. It turns green when won
  and red when lost, then leaves after 2.5 s.
- A waiting event fades after 40 s if ignored. The same kind never comes twice in a row. The Moonwell weighs 1.5
  while the hero is under 70% health and 0.35 otherwise; the others weigh 1.
- Waves come from the minute's enemy mix and tint, so events get harder with the run.

## The four events

| Event | Verb | Start | Rules | Win | Lose |
|---|---|---|---|---|---|
| Shrine of Vigil | Hold | Step into the 4.5 m ring | 10 s inside within 30 s; outside, the vigil drains at half speed. A ring of 10 + 2/min dead rises every 2.5 s and marches on the shrine. | Chest (1 item before 5:00, then 3) and a shove that clears the ring | The candles gutter out |
| Grave Robber | Chase | At once | A gilded elite runner with 260 + 120/min health flees for 20 s at 85% of the hero's speed (50% when 16 m ahead), weaving and turning back from the edge. Every 10% of his health spills a coin. | Elite loot (chest, coin bag, roast odds) and 20 + 3/min gold | He digs out |
| Bone Pyres | Break | Hero within 14 m | Three glowing skull pyres (150 x the minute's health scale each) on a 5.5 m circle, 30 s. Each raises 3 dead every 3.5 s and wards the dead within 7 m every second (faster, tougher). | A great gem worth a whole level | Every pyre still standing bursts into an elite |
| Moonwell | Risk | First sip while hurt | A 3.5 m pool holding 40% of max health heals 6%/s while the hero stands in it. While the hero drinks, a ring of 8 + 2/min dead rises every 3 s. Light fades 25 s after the first sip. | The healing drunk (the well runs dry or fades) | - |

Why these four:

- **Shrine**: the horde game's core fantasy, a last stand. It rewards area builds and tanky classes.
- **Robber**: rewards single-target and ranged builds plus mobility. That balances the shrine and gives the Rogue
  and the crossbow a moment to shine.
- **Pyres**: a fortified objective. The player must leave the kite loop and push in; it punishes ignoring it with
  elites.
- **Moonwell**: healing for every class at a price. This is the answer to "only the Barbarian can heal": the Knight,
  Mage and Rogue get a real but risky way to recover mid-run.

Healing from the Moonwell counts under its own heal source (`Event`) in the run report.

## Rogue tuning in the same pass

Over 8 invulnerable bot minutes, the Rogue dealt 193k-301k damage against about 670k for the Mage and the Barbarian,
with 5.2k-7.6k kills against 12.9k. Changes:

- **Repeater Crossbow**: a burst spreads over the nearest enemies (bolt k aims at the k-th nearest) instead of
  piling every bolt onto one target the first bolt already kills. Damage 9/12/15/18 becomes 11/14/18/22;
  Widowmaker 24 becomes 28.
- **Momentum**: each stack also gives +2% Might and -1.5% Cooldown (+20% and -15% at full), mirroring the
  Barbarian's Fury. Moving is the Rogue's damage.
- **Fan of Knives**: cooldown 2.0-1.5 s becomes 1.6-1.2 s and damage +2; Storm of Steel 20 becomes 24.
- **Venom Shivs**: damage and venom per stack +50% (Nightshade 14 becomes 21).
- **Shadow Dancers**: knife damage and fade burst about +40%.

Results (bot, invulnerable, 8 min; the picks differ every run):

| | Kills (runs) | Average kills | Average damage |
|---|---|---|---|
| Rogue before | 7.6k, 5.2k | 6.4k | 247k |
| Rogue after | 11.7k, 9.5k, 8.1k | 9.8k | 463k |
| Mage, Barbarian (one run each) | 12.9k, 12.9k | - | 666k, 674k |

Survival with map events on (one bot run each, 10 min cap): Knight 4:02, Barbarian 6:55, Mage 3:14, Rogue 4:06
(the Rogue's earlier average was 2:58). The bot ignores events unless it walks into one.

## Tools

- `python event_preview.py KIND [CLASS] [START]` starts an event 9 m ahead and captures it waiting, under way and at
  its end.
- `python event_look.py KIND` captures it without the horde.
- Dev hooks on Run: `DevEvent(kind)`, `DevGoToEvent(shortBy)`, `DevHurt(share)`; `DevReport` prints
  `event=Kind:State:progress:timeLeft`.

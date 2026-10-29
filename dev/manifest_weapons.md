# Barrowlight - weapons module manifest

Everything the weapons module (src/Scripts/Weapons/**) needs the lead to build. Names are exact; code resolves effects
with `effects.Find(name)` once, sounds with `sounds.Play/PlayAt(name)`, looks with `projectiles.FindVisual(name)`.
Missing names only log once and do nothing, so the game runs before the art lands.

Conventions used below:
- Every effect is a WORLD-space emitter under the effects root (positions are world positions), loop off, rate 0
  (scripted bursts only), like the existing Telegraph / Combat emitters in build_effects.py.
- "Tinted" = the script passes a color (EmitParams.color multiplies the gradient): keep the gradient WHITE with the
  alpha shape given, and let the script color it (gold / red / violet echo / ...).
- "Scripted size" = the script passes size (m, the DIAMETER for rings and decals) and lifetime: give the emitter a
  normalized size curve (0..1) as noted, and a lifetime range of 1.0 (overridden).
- "Untinted" = plain Emit without color (trails, EvolveBurst): the emitter's gradient carries the color.
- Player effects never use red-orange danger tones except the Barbarian's own red (design 10.8); enemy looks are
  red-orange.
- Textures named below exist in the lead's set (glow, spark, sparkle, ember, shard, ring_thin, ring_energy,
  disc_fill, light_shaft); "smoke" = a soft smoke puff (Kenney Smoke); two new ones are asked for: `crack_radial.png`
  (radial ground crack, white on transparent) and `slash_x.png` (two crossed slash strokes).

## 1. Effects

### Knight

| Name | Shows | Blend | Texture | Color | Size (m) | Life (s) | Count / emit | Motion | Render |
|---|---|---|---|---|---|---|---|---|---|
| OathbladeSlash | Sword ribbon: points along the slash arc (3-28 per swing), the leading end lives longest | additive, intensity 2.5 | glow (soft round) | tinted (white-gold, Dawnbreaker gold) | scripted (radius x 0.45, 1.3-1.9) ; size curve 1 -> 0.6 | scripted 0.08-0.14 | 1 | none | horizontal billboard |
| CrescentLaunch | Burst when Dawnbreaker's crescent leaves | additive | spark | tinted gold | 0.15-0.3 | 0.3 | 10 | 3-6 m/s sphere, drag 4 | stretched |
| CrescentSparks | Trail of the crescent wave (every 0.04 s) | additive | sparkle | untinted gold (1, 0.8, 0.35) | 0.3-0.5 | 0.3 | 1 | up 0.5 m/s | billboard |
| AegisSummon | Shields fade in: gold motes rising around the knight | additive | sparkle | tinted gold | 0.15-0.3 | 0.5 | 16 | sphere r 1.5, up 1 m/s | billboard |
| AegisFade | A shield fades out | additive | sparkle | tinted pale gold | 0.2 | 0.3 | 6 | sphere r 0.3, size -> 0 | billboard |
| AegisBlockSpark | An enemy bolt bursts on a shield (the bolt is removed without its own end effect) | additive, intensity 3 | spark | tinted gold | 0.1-0.2 | 0.25 | 10 | 4-8 m/s sphere | stretched |
| VerdictImpact | Holy flash where the sword lands | additive, intensity 4 | glow | tinted holy white-gold | scripted (2 x blast r); size 0.6 -> 1.1 | scripted 0.15 | 1 | none | horizontal billboard |
| VerdictCrack | Radial crack decal fading over 2 s | alpha (normal) | crack_radial (new) | tinted gold (dark center, bright cracks) | scripted (2 x blast r); size 1 | scripted 2.0; alpha 1 -> 0 over the last 60% | 1 | none | horizontal billboard |
| DustRing | Dust ring flung outward at a slam | alpha | smoke | tinted dust (0.82, 0.76, 0.66) | 0.6 -> 1.4 | 0.6-0.9 | 14 | ring r 0.5, outward 3-5 m/s, drag 3 | billboard |
| VerdictShockwave | Final Verdict's holy ring | additive, intensity 3 | ring_energy | tinted holy white | scripted (9 m x Area); size 0.2 -> 1.0 | scripted 0.4 | 1 | none | horizontal billboard |
| VerdictDissolve | Embedded sword dissolves into motes | additive | sparkle | tinted holy | 0.15-0.25 | 0.6 | 12 | box 0.3 x 3 x 0.3 (vertical), up 1 m/s | billboard |
| RushDust | Dust trail of Shield Rush (every 0.03 s) | alpha | smoke | tinted white (use a dusty gradient) | 0.5 -> 1.2 | 0.5 | 3 | sphere r 0.3, out 1-2 m/s | billboard |
| RushGlow | Gold motes around the rushing knight (the hero also glows gold via SetGlow) | additive | glow | tinted gold | 0.25-0.4 | 0.25 | 12 at start, 2 per 0.03 s | sphere r 0.6 | billboard |
| RushImpact | Final boom of the rush | additive, intensity 3 | ring_energy | tinted gold | 1 -> 3.5 | 0.3 | 1 | none | horizontal billboard |
| BulwarkShockwave | Shield bash ring on a block | additive | ring_energy | tinted gold | scripted (4 m); size 0.3 -> 1 | scripted 0.3 | 1 | none | horizontal billboard |
| TelegraphRing / TelegraphFill | EXISTING: Verdict and Starfall reuse them tinted gold / pale blue | - | - | - | - | - | - | - | - |

### Barbarian

| Name | Shows | Blend | Texture | Color | Size (m) | Life (s) | Count / emit | Motion | Render |
|---|---|---|---|---|---|---|---|---|---|
| RavagerSmear | Thick axe smear along the chop cone | additive, intensity 2 | glow | tinted red (0.95, 0.22, 0.1), crimson at full Fury | scripted (length x 0.55, ~2 m); size 1 -> 0.7 | scripted 0.1-0.18 | 1 per point | none | horizontal billboard |
| RavagerDebris | Chunky dirt and bone chips at the chop | alpha | shard | tinted dust | 0.15-0.3 | 0.7 | 6 | up/out 3-5 m/s, gravity 12, random spin | billboard |
| FissureSpike | Skullsplitter fissure: rock spikes with magma | additive core + alpha | shard | tinted magma (1, 0.45, 0.12) | 0.4-0.7 | 0.5 | 2 per 1 m segment | up 4-6 m/s, gravity 15 | stretched |
| DustPuff | Dust per spike (Fault Line, Skullsplitter) | alpha | smoke | tinted dust | 0.4 -> 1.0 | 0.6 | 2 | sphere r 0.3, up 0.5 m/s | billboard |
| FaultStomp | Ground shock under the stomp | alpha | ring_energy | tinted dust | 0.5 -> 4 | 0.35 | 1 | none | horizontal billboard |
| FaultSpike | Rock spikes erupting along a fissure | alpha | shard | tinted rock (0.72, 0.6, 0.48) | 0.4-0.8 | 0.5 | 2 per segment | up 5-7 m/s, gravity 18, spin | stretched |
| LavaCrack | Tectonic Rift lava cracks (every 0.1 s, random point on the line) | additive, intensity 3 | ember | tinted lava (1, 0.4, 0.08) | 0.5-0.8 | 0.5-0.8 | 1 | up 0.5 m/s | billboard |
| WhirlwindTrail | Red ring trail at blade height (2 blades, 1 per frame each) | additive, intensity 2 | glow | tinted red / storm orange | 0.6 | 0.18 | 1 | none | billboard |
| WhirlwindStop | Heavy stop burst | additive | spark | tinted red | 0.15-0.3 | 0.3 | 14 | 5-8 m/s horizontal ring | stretched |
| MaelstromSwirl | Wind dust drawn in at the pull edge (per 0.2 s tick) | alpha | smoke | tinted storm orange | 0.6 -> 1.2 | 0.6 | 2 | 2 m/s random | billboard |
| LeapDust | Takeoff dust ring | alpha | smoke | tinted dust | 0.6 -> 1.6 | 0.6 | 10 | ring, outward 3 m/s | billboard |
| LeapShockwave | Landing shockwave ring | alpha + additive edge | ring_energy | tinted dust | scripted (8 m); size 0.2 -> 1 | scripted 0.4 | 1 | none | horizontal billboard |
| LeapCrack | Ground crack decal at landing | alpha | crack_radial (new) | tinted dust (dark) | scripted (6.4 m) | scripted 2.5; alpha fades last 60% | 1 | none | horizontal billboard |
| LeapRocks | Rock chunks flung at landing | alpha | shard | tinted dust | 0.2-0.45 | 0.9 | 16 | up/out 4-8 m/s, gravity 15, spin | billboard |
| FuryEmbers | Red embers at eye height at 50 Fury (every 0.2 s; the hero also glows red via SetGlow) | additive | ember | tinted ember red | 0.12-0.2 | 0.5 | 2 | up 0.8 m/s | billboard |

### Mage

| Name | Shows | Blend | Texture | Color | Size (m) | Life (s) | Count / emit | Motion | Render |
|---|---|---|---|---|---|---|---|---|---|
| EmberOrbFlash | Orange flash sphere at the blast | additive, intensity 4 | glow | tinted fire / sun / violet echo | scripted (2 x blast r); size 0.5 -> 1 | scripted 0.1 | 1 | none | billboard |
| EmberOrbBurst | Embers flung out of the blast | additive | ember | tinted | 0.12-0.25 | 0.4-0.7 | 12 | 3-6 m/s sphere, gravity 4 | stretched |
| EmberFizzle | An orb that missed fizzles out | alpha | smoke | tinted | 0.3 -> 0.6 | 0.4 | 6 | up 0.5 m/s | billboard |
| EmberSplash | Sunfire ember lands | additive | ember | tinted sun | 0.15-0.3 | 0.35 | 6 | up 1-3 m/s cone | billboard |
| StarfallShadow | Meteor shadow growing on the ground | alpha (normal) | disc_fill | tinted near-black violet (0.1, 0.06, 0.14) | scripted (2 x r); size 0.2 -> 1.0; alpha 0.2 -> 0.6 | scripted 0.6 / 0.9 | 1 | none | horizontal billboard |
| StarfallImpact | Impact flash | additive, intensity 5 | glow | tinted fire / violet echo | scripted (2 x r); size 0.6 -> 1.2 | scripted 0.15 | 1 | none | horizontal billboard |
| StarfallDebris | Flying rock debris with embers (also Godspear, gold tint) | alpha + ember core | shard | tinted rock | 0.15-0.35 | 0.8 | 10 (great meteor 28) | up/out 5-9 m/s, gravity 15, spin | billboard |
| ScorchDecal | Scorched ground decal, 4 s | alpha (normal) | disc_fill (soft blotch) | tinted (0.18, 0.12, 0.1) | scripted (2 x r) | scripted 4.0; alpha 0.8 -> 0 over the last 50% | 1 | none | horizontal billboard |
| CraterFire | Flames in a burning crater (every 0.15 s) | additive | ember (or flame flipbook) | tinted fire / violet echo | 0.5 -> 0.2 | 0.5-0.8 | 1 (great 3) | up 1 m/s | billboard |
| RimeNovaRing | Frost ring expanding in 0.4 s, white edge | additive, intensity ~7 | ring_energy (8x8 flipbook frost-smoke ring if available) | tinted frost (0.6, 0.88, 1) / violet echo | scripted (2 x r, 8-14 m); size curve 0 -> 1 (the expansion) | scripted 0.45; alpha 1 -> 0 over the last 30% | 1 | none | horizontal billboard |
| FrostMist | Cold mist puffs at the mage | alpha | smoke | tinted frost | 0.6 -> 1.4 | 0.8 | 14 | horizontal ring out 2-4 m/s, drag 3 | billboard |
| FrostShatter | Frozen kill shatters into ice shards | additive | shard | tinted ice (0.8, 0.95, 1) | 0.12-0.25 | 0.5 | 6 | 3-6 m/s sphere, gravity 10, spin | billboard |
| BlinkOut | Violet afterimage at the origin (implodes) | additive | sparkle + glow | tinted arcane (0.65, 0.4, 1) | 0.2-0.4 | 0.35 | 14 | start on sphere r 1, velocity inward 3 m/s | billboard |
| BlinkIn | Arrival burst | additive | sparkle | tinted arcane | 0.2-0.35 | 0.35 | 14 | 2-4 m/s sphere | billboard |
| BlinkRift | Swirling rift waiting to detonate | additive, intensity 3 | ring_energy | tinted arcane | scripted (6 m); size 1 -> 0.6, spin 360 deg/s | scripted 0.5 | 1 | none | horizontal billboard |
| BlinkRiftBurst | Rift detonation ring | additive, intensity 4 | ring_energy | tinted arcane | scripted (6 m); size 0.3 -> 1 | scripted 0.35 | 1 | none | horizontal billboard |

### Rogue

| Name | Shows | Blend | Texture | Color | Size (m) | Life (s) | Count / emit | Motion | Render |
|---|---|---|---|---|---|---|---|---|---|
| CrossbowMuzzle | Teal sparks at the bolt release | additive | spark | tinted teal | 0.08-0.15 | 0.15 | 4 | 2-4 m/s sphere | stretched |
| KnifeGlint | Steel glints when the fan goes out | additive, intensity 3 | sparkle | tinted steel | 0.2-0.35 | 0.2 | 8 | sphere r 0.6 | billboard |
| MarkApply | Red sparks swirling over a newly marked head | additive | sparkle | tinted mark red | 0.15-0.25 | 0.4 | 10 | sphere r 0.3, up 1 m/s | billboard |
| ShadowClone | Smoky afterimage behind the mark (the clone) | alpha | smoke | tinted shadow violet (0.4, 0.25, 0.55) | 0.5 -> 0.9 | 0.35 | 12 | box 0.4 x 1.6 x 0.4, up 0.5 m/s | billboard |
| ShadowStab | The clone's stab | additive | spark | tinted mark red | 0.1-0.2 | 0.15 | 8 | 4-6 m/s cone | stretched |
| MarkBurst | Marked enemy bursts into daggers | alpha + additive sparks | smoke + spark | tinted shadow | 0.4 -> 0.9 | 0.5 | 14 | 3-5 m/s horizontal | billboard |
| ExecuteSlash | Big red slash flash on an execution | additive, intensity 4 | slash_x (new) | tinted mark red | 2.5 | 0.25 | 1 | none | billboard |
| SmokePoof | Smoke Roll puff at the origin | alpha | smoke | tinted smoke grey (0.62, 0.66, 0.68) | 0.8 -> 2.0 | 0.9 | 16 | 1-3 m/s sphere, drag 2 | billboard |
| SmokeCloud | The 3 s cloud (2 puffs per 0.12 s inside 3 m) | alpha, opacity 0.5 | smoke | tinted smoke grey | 1.5 -> 2.5 | 1.2 | 2 | 0.3 m/s random | billboard |
| MomentumWisps | Teal footstep wisps at 10 Momentum | additive | glow | tinted teal | 0.25 | 0.5 | 2 per 0.12 s | up 0.3 m/s | billboard |

### Shared weapons and common

| Name | Shows | Blend | Texture | Color | Size (m) | Life (s) | Count / emit | Motion | Render |
|---|---|---|---|---|---|---|---|---|---|
| SpiritDartPop | Small pop when a dart hits | additive | sparkle | tinted spirit blue | 0.15-0.25 | 0.25 | 5 | 1-2 m/s sphere | billboard |
| DiscFade | A disc ends | additive | sparkle | tinted cyan / violet | 0.2 | 0.3 | 6 | sphere r 0.3 | billboard |
| BrambleAura | Soft thorn ring under the hero, re-emitted every 0.12 s so it follows | additive, intensity ~7 x opacity 0.35 | ring_energy (8x8 smoke flipbook ring if available) | tinted thorn green / briar yellow-green | scripted (2 x aura r); size 1 | scripted 0.35; alpha 0 -> 0.6 -> 0 | 1 | none | horizontal billboard |
| BrambleThorns | Thorn bits popping on the ring (every 0.15 s) | alpha | shard | tinted thorn green | 0.15-0.3 | 0.5 | 2 | up 1-2 m/s, gravity 6 | billboard |
| GodspearBlast | Godspear detonation flash | additive, intensity 4 | ring_energy | tinted gold | scripted (5 m x Area); size 0.2 -> 1 | scripted 0.2 | 1 | none | horizontal billboard |
| FlaskShatter | Glass shards + fire burst | additive core | shard | tinted fire / hellfire | 0.1-0.25 | 0.4 | 14 | 3-5 m/s sphere, gravity 10 | billboard |
| FlaskFlames | Flames licking 0.5 m high in a pool (2 per 0.08 s) | additive | ember / flame flipbook | tinted fire | 0.4 -> 0.1 | 0.4-0.6 | 2 | up 1.2 m/s | billboard |
| FlaskPoolDecal | Burning pool glow under the flames | additive, intensity 1.2 | disc_fill | tinted fire | scripted (2 x pool r) | scripted = pool duration; alpha in 0.1 s, out over the last 20% | 1 | none | horizontal billboard |
| TotemPlace | Cyan spark burst when a totem lands | additive | spark | tinted lightning | 0.1-0.2 | 0.3 | 12 | 2-5 m/s hemisphere | stretched |
| LightningBolt | Jagged bolt drawn as overlapping dots | additive, intensity 6 | glow | tinted lightning (0.6, 0.9, 1) | scripted 0.55 | scripted 0.09 | 1 per point (3-15 per segment) | none | billboard |
| ZapImpact | Sparks at each zapped enemy | additive | spark | tinted lightning | 0.08-0.15 | 0.2 | 6 | 3-5 m/s sphere | stretched |
| WispSplash | Soft splash of light at a dive | additive | sparkle + glow | tinted wisp green / choir gold | 0.2-0.4 | 0.4 | 10 | 1-3 m/s sphere | billboard |
| EvolveBurst | Weapon evolved: gold column burst at the hero | additive, intensity 4 | sparkle + light_shaft | UNTINTED gold (1, 0.85, 0.4) | 0.3-0.6 | 1.2 | 30 | box 1 x 0.2 x 1, up 2-4 m/s | billboard |

### Projectile trails (named in ProjectileCatalog; emitted untinted every trailInterval)

| Name | Look that uses it | Look | Interval (s) |
|---|---|---|---|
| CrescentSparks | OathCrescent | see above | 0.04 |
| ReflectTrail | AegisReflect | gold glow dots, additive, 0.2 m, 0.2 s | 0.03 |
| EmberOrbTrail | EmberOrb | fire puff, additive, orange -> red, 0.35 -> 0 m, 0.25 s | 0.05 |
| SunfireOrbTrail | SunfireOrb | as EmberOrbTrail, gold-white, 0.5 m | 0.04 |
| EmberSparks | Ember | tiny orange sparks, 0.08 m, 0.25 s | 0.05 |
| MeteorTrail | Meteor, GreatMeteor | fire core (additive glow, orange) fading to grey smoke (alpha), 0.8 -> 1.6 m, 0.4 s | 0.03 / 0.02 |
| BoltTracer | CrossbowBolt, WidowBolt | thin teal glow dot, additive, 0.15 m, 0.12 s (dashed tracer) | 0.03 / 0.025 |
| ShadowDaggerTrail | ShadowDagger | dark violet smoke wisps, alpha, 0.3 -> 0.6 m, 0.3 s | 0.04 |
| SpiritDartTrail | SpiritDart | pale blue glow dots, additive, 0.2 -> 0 m, 0.25 s | 0.04 |
| DiscTrail | RicochetDisc | cyan glow, additive, 0.3 m, 0.2 s | 0.04 |
| EclipseTrail | EclipseDisc | violet glow with gold rim sparks, 0.35 m, 0.2 s | 0.03 |
| JavelinTrail | SkewerJavelin | faint dust wind streak, alpha, 0.3 m, 0.25 s | 0.05 |
| GodspearTrail | Godspear | gold sparks, additive, 0.25 m, 0.3 s | 0.03 |
| FlaskSmoke | FireFlask, HellfireCask | small grey smoke puffs, alpha, 0.3 -> 0.6 m, 0.4 s | 0.06 / 0.04 |
| SpireSparks | Thunderspire | small cyan sparks, additive, 0.1 m, 0.2 s | 0.08 |
| WispTrail | Wisp, ChoirWisp | soft pale green glow, additive, 0.25 -> 0 m, 0.35 s | 0.08 / 0.07 |
| EnemyBoltTrail | EnemyBolt (enemy) | thin red-orange streak dots, additive, 0.15 m, 0.15 s | 0.04 |
| HexOrbTrail | HexOrb (enemy) | red-orange glow puffs, additive, 0.35 -> 0 m, 0.3 s | 0.04 |

## 2. Sounds

Random pitch / volume jitter as the audio module applies to every SFX (design 10.7); "pitch" below is the extra
jitter wanted. Spatial unless noted. Limits = voices at once / min retrigger.

| Name | Description | Layers | Length | Variants | Notes |
|---|---|---|---|---|---|
| oathblade_swing | Steel "shing" of the wide slash | air whoosh 80-120 ms + metallic ring tail (2-4 kHz shimmer) | 0.4 s | 3 | pitch +-5%, limit 3 / 0.08 s |
| oathblade_hit | Body thud when the slash connects | low thump (120 Hz, 60-90 ms) + bone crunch transient | 0.3 s | 3 | limit 3 |
| dawn_crescent | Dawnbreaker crescent launch | rising holy whoosh + bell shimmer | 0.6 s | 2 | |
| aegis_summon | Shields fade in | glass/bell ringing tone (880 + 1320 Hz partials) swelling in | 0.6 s | 2 | flat |
| aegis_bonk | Shield hits a skeleton | short metal-wood "bonk" 80 ms + small ring 150 ms | 0.25 s | 4 | pitch +-8%, limit 4 / 0.05 s |
| aegis_block | Enemy bolt bursts on a shield | crisp metallic ping + spark crackle | 0.25 s | 3 | |
| verdict_summon | Holy hum as the sword appears | soft choir/hum swell rising into the slam | 0.35 s | 2 | |
| verdict_slam | Giant sword slam | deep bell (C2) + stone crack transient + sub-bass (<80 Hz) thump, bell tail | 1.2 s | 3 | priority high |
| verdict_shockwave | Final Verdict holy shockwave | airy holy boom + whoosh | 0.8 s | 2 | |
| shield_rush | Charge start | heavy whoosh + armor rattle | 0.35 s | 2 | flat |
| shield_rush_boom | Final "boom" on stop | shield impact low thump + metal clang | 0.6 s | 2 | |
| bulwark_clang | Block + shield bash | metal transient (clang 1-3 kHz, 40 ms attack) + low thump (100 Hz, 150 ms) | 0.5 s | 3 | |
| ravager_chop | Two-handed chop swing | heavy wet whoosh | 0.35 s | 3 | limit 3 |
| ravager_hit | Chop lands | wet chop impact + bone crack | 0.3 s | 4 | limit 3 |
| skullsplitter_crack | Ground split by Skullsplitter | rock tearing rumble + magma hiss + sub | 0.6 s | 2 | |
| whirlwind_loop | Rising whoosh while spinning | continuous blade whoosh (seamless), the script ramps pitch 1.0 -> 1.26 (+4 semitones) and restarts it when it ends | 3-4 s | 1 | flat, priority high |
| whirlwind_stop | Heavy stop | axe ring + boot scuff + grunt | 0.5 s | 2 | |
| fault_stomp | Stomp on the hero | low boot/ground thump (100 Hz) + dust | 0.4 s | 2 | |
| fault_crack | Rolling crack along the fissures | crackle sweeping left to right (travels) | 0.6 s | 2 | |
| leap_takeoff | Earthshaker takeoff | grunt + whoosh | 0.4 s | 2 | flat |
| leap_land | Earthshaker landing | low boom with sub-bass (<60 Hz) + rock crumble tail | 1.2 s | 2 | priority high |
| fury_max | 50 Fury reached | war drum hit + roar | 1.0 s | 1 | flat |
| ember_whoosh | Fireball launch | fire whoosh | 0.3 s | 3 | limit 4 |
| ember_boom | Fireball explosion | crack transient + body boom (150 ms) + crackle tail | 0.5 s | 4 | pitch +-6%, limit 4 |
| starfall_whistle | Falling meteor | whistle rising in pitch over 0.6 s | 0.6 s | 2 | |
| starfall_impact | Meteor impact | boom + sub-bass + rock debris | 1.0 s | 3 | |
| cataclysm_whistle | Great meteor falling | deeper, longer whistle + rumble | 0.9 s | 1 | |
| cataclysm_impact | Great meteor impact | massive boom, sub-bass, debris rain | 2.0 s | 1 | priority highest |
| rime_nova | Frost ring | glassy crackle sweep (ice crystallizing), high shimmer | 0.5 s | 3 | |
| frost_shatter | Frozen enemy shatters | ice tinkle shatter | 0.3 s | 4 | limit 3 / 0.04 s |
| arcane_echo | A Mage spell echoes | reversed chime (reverse bell swell into a click) | 0.35 s | 2 | flat |
| blink | Blink | implosion (reverse whoosh into a pop) | 0.25 s | 2 | flat |
| blink_rift | Rift detonation | violet boom with glassy overtone | 0.7 s | 2 | |
| crossbow_twang | Crossbow bolt | tight string twang + bolt hiss | 0.2 s | 4 | pitch +-5%, limit 4 / 0.04 s |
| widow_ricochet | Crit ricochet | high metallic ricochet ping | 0.25 s | 3 | |
| knife_fan | Fan of Knives | layered "shhk": 3-4 quick blade swishes | 0.35 s | 3 | |
| mark_apply | A mark lands | whisper + heartbeat thump | 0.6 s | 2 | |
| mark_stab | Clone stab | clean blade stab + smoky whoosh | 0.25 s | 3 | |
| mark_daggers | Death daggers fan out | hiss | 0.5 s | 2 | |
| execute | Death Sentence execution | heavy slash + cut bell toll | 0.6 s | 2 | priority high |
| smoke_poof | Smoke cloud | soft "poof" | 0.4 s | 2 | |
| roll_whoosh | Smoke Roll | cloth whoosh | 0.35 s | 2 | flat |
| momentum_max | 10 Momentum reached | subtle wind gust | 0.8 s | 1 | flat |
| spirit_dart | Dart launch | soft pluck (harp-like) | 0.2 s | 4 | limit 4 / 0.04 s (Soul Hail fires ~8 per second) |
| disc_throw | Disc throw | whirring throw | 0.3 s | 2 | |
| disc_ping | Disc bounce | metallic "ping"; the script raises pitch one semitone per bounce (up to +12) | 0.25 s | 1-2 | small extra jitter only (+-2%) |
| bramble_rustle | Thorns rustle (when the aura hits) | dry thorny rustle | 0.5 s | 3 | limit 1 |
| javelin_throw | Javelin throw | heavy whoosh | 0.5 s | 2 | |
| godspear_boom | Godspear detonation | holy explosion boom | 0.9 s | 2 | |
| flask_throw | Flask toss | bottle swish | 0.25 s | 2 | |
| flask_shatter | Flask lands | glass shatter + "whoomp" ignition | 0.7 s | 3 | |
| totem_place | Totem planted | wooden thunk + static hum start | 0.4 s | 2 | |
| totem_zap | Lightning zap | electric crackle; the script adds pitch +-10% | 0.25 s | 4 | limit 4 |
| wisp_whistle | Wisp dive | whistling dive | 0.4 s | 3 | limit 3 |
| wisp_splash | Wisp splash | soft light splash (airy chime + soft burst) | 0.4 s | 3 | limit 3 |
| active_ready | Active ready again | soft chime | 0.4 s | 1 | flat |
| evolve | Weapon evolved | triumphant chord + choir burst | 1.5 s | 1 | flat |

Core sounds only: hits, crits and kills sound through Combat / Feedback (`hit`, `hit_crit`, `enemy_death`).

## 3. Projectile looks (ProjectileCatalog.cs)

Material: `app:/data/Materials/SpellGlow.mat` (white, emissive 1) tinted per look with a MaterialInstance (base and
emissive = tint, intensity as listed); "own" = the mesh's own KayKit material. Spin = degrees per second about up
(0 = aligned: the mesh's +Z follows the travel direction). Rotation = Euler degrees applied in mesh space first: the
values are GUESSES of the KayKit prop axes; please check each in the editor and fix the row in `looks`.
Orbit height applies to orbiting looks only.

| Name | Mesh (fallback) | Material / tint | Scale | Spin | Rotation | Height | Trail | Notes |
|---|---|---|---|---|---|---|---|---|
| OathCrescent | app:/data/Meshes/crescent.gltf (engine torus) | glow gold, 4 | 2.0 | 0 | 0,0,0 | - | CrescentSparks | NEW MESH: flat crescent blade, 2 m wide along X, lying in XZ, convex side toward +Z |
| AegisShield | Props shield_badge (engine cylinder) | glow pale gold, 2.5 | 1.1 | 0 | 0,90,0 | 1.1 | - | face must point outward (travel is the orbit tangent) |
| AegisReflect | engine icosphere2 | glow gold, 5 | 0.3 | 0 | 0 | - | ReflectTrail | |
| VerdictSword | Props sword_2handed (engine cube) | glow pale gold, 3 | 3.0 | 0 | 90,0,0 | - | - | travel is straight down: blade must point along +Z (down); pivot ~ blade middle (script places it 1.1 m up when embedded) |
| EmberOrb | engine icosphere2 | glow fire (1, 0.45, 0.1), 6 | 0.45 | 0 | 0 | - | EmberOrbTrail | |
| SunfireOrb | engine icosphere2 | glow sun (1, 0.82, 0.45), 8 | 0.6 | 0 | 0 | - | SunfireOrbTrail | |
| Ember | engine icosphere1 | glow fire, 6 | 0.2 | 0 | 0 | - | EmberSparks | |
| Meteor | engine dodecahedron | glow fire, 3 | 0.9 | 0 | 0 | - | MeteorTrail | |
| GreatMeteor | engine dodecahedron | glow fire, 4 | 2.4 | 0 | 0 | - | MeteorTrail | |
| CrossbowBolt | Props arrow | own | 1.0 | 0 | 0 | - | BoltTracer | tip along +Z |
| WidowBolt | Props arrow | glow cyan, 3 | 1.15 | 0 | 0 | - | BoltTracer | |
| ThrownKnife | Props dagger | own | 1.0 | 0 | 90,0,0 | - | - | blade along +Z; also used for knives stuck in the ground (tilted down) |
| ShadowDagger | Props dagger | glow shadow violet, 3 | 1.0 | 0 | 90,0,0 | - | ShadowDaggerTrail | |
| MarkSigil | engine torus | glow mark red, 4 | 0.55 | 140 | 0 | - | - | floats over a marked head; a skull mesh would be better if one exists |
| SpiritDart | engine icosphere1 | glow pale blue, 5 | 0.25 | 0 | 0 | - | SpiritDartTrail | |
| RicochetDisc | Props shield_round (engine cylinder) | glow cyan, 2.5 | 0.75 | 1080 | 90,0,0 | - | DiscTrail | must lie flat (spinning about up) |
| EclipseDisc | Props shield_round (engine cylinder) | glow violet, 3.5 | 0.95 | 1440 | 90,0,0 | - | EclipseTrail | |
| BrambleRing | app:/data/Meshes/thorn_ring.gltf (engine torus) | glow thorn green, 0.8 | 1.0 x script | 15 | 0 | 0.05 | - | NEW MESH: thorny ring 1 m across in XZ; the script scales it to the aura diameter assuming 1 m across at scale 1 |
| BriarRing | as BrambleRing | glow briar yellow-green, 1.6 | 1.0 x script | -20 | 0 | 0.05 | - | |
| SkewerJavelin | Props arrow | own | 2.4 | 0 | 0 | - | JavelinTrail | |
| Godspear | Props arrow | glow gold, 4 | 3.0 | 0 | 0 | - | GodspearTrail | |
| FireFlask | Props smokebomb (engine icosphere1) | own | 1.2 | 720 | 0 | - | FlaskSmoke | |
| HellfireCask | Props smokebomb (engine icosphere1) | own | 1.8 | 540 | 0 | - | FlaskSmoke | |
| StormTotem | Props staff (engine capsule_1m) | own | 1.5 | 0 | -90,0,0 | - | - | must stand upright (static, no travel) |
| Thunderspire | Props staff (engine capsule_1m) | glow cyan, 3 | 1.3 | 90 | -90,0,0 | 2.4 | SpireSparks | floating rod orbiting the hero |
| Wisp | engine icosphere1 | glow wisp (0.55, 1, 0.85), 6 | 0.32 | 0 | 0 | 1.8 | WispTrail | |
| ChoirWisp | engine icosphere1 | glow choir (1, 0.95, 0.75), 7 | 0.36 | 0 | 0 | 1.8 | WispTrail | |
| EnemyBolt | Props arrow | own | 1.0 | 0 | 0 | - | EnemyBoltTrail | enemy (red-orange trail) |
| HexOrb | engine icosphere2 | glow enemy red-orange (1, 0.28, 0.08), 5 | 0.55 | 0 | 0 | - | HexOrbTrail | enemy |

New assets asked for: `app:/data/Materials/SpellGlow.mat` (already in CONTRACTS), `app:/data/Meshes/crescent.gltf`,
`app:/data/Meshes/thorn_ring.gltf` (both optional: torus fallbacks), textures `crack_radial.png`, `slash_x.png`.

## 4. Hero animation states used (exact controller state names)

Every class can own the shared weapons, so every class needs the shared states too.

| Class | Upper layer (via hero.PlayAttack) | Base layer (via hero.PlayFullBody) |
|---|---|---|
| Knight | Slash, SlashDiagonal (Oathblade Arc), CastRaise (Verdict), Block (Shield Rush; core also plays Block on Bulwark blocks) | - |
| Barbarian | HeavyChop, DualChop (Ravager Chop), Chop (Fault Line stomp) | Spin (Whirlwind, loops for the spin), Leap (Earthshaker Leap, 0.75 s) |
| Mage | CastShoot (Ember Orb, Blink), CastRaise (Starfall), CastLong (Rime Nova) | - |
| Rogue | Shoot (Repeater Crossbow), Reload (after every 4th crossbow volley), UpperSpin (Fan of Knives flourish) | Dodge (Smoke Roll, 0.35 s) |
| Shared, all classes | CastShoot (Spirit Darts), Throw (Ricochet Disc, Skewer Javelin, Fire Flask), CastRaise (Storm Totem) | - |

At most one upper clip per 0.4 s; within that window a higher priority interrupts (active > evolved > class weapon >
shared). Assassin's Mark plays no hero clip (the shadow clone strikes).

## 5. Icons (app:/data/UI/Icons/[id].png; UpgradeOffer.icon / SlotView.icon hold the full key)

- Weapons (19): oathblade_arc, aegis_orbit, verdict, ravager_chop, whirlwind, fault_line, ember_orb, starfall,
  rime_nova, repeater_crossbow, fan_of_knives, assassins_mark, spirit_darts, ricochet_disc, bramble_halo,
  skewer_javelin, fire_flask, storm_totem, hunting_wisps.
- Evolutions (19): dawnbreaker, bastion_halo, final_verdict, skullsplitter, maelstrom, tectonic_rift, sunfire_orb,
  cataclysm, absolute_zero, widowmaker, storm_of_steel, death_sentence, soul_hail, eclipse_disc, briar_sanctum,
  godspear, hellfire_cask, thunderspire, wisp_choir.
- Passives (12): iron_heart, plate_pauldron, windstride_boots, whetstone, lodestone_charm, wide_sigil,
  everburn_wick, tome_of_insight, keen_eye, lucky_knucklebone, quickening_hourglass, echo_quiver.
- Fallbacks and chest gold (4): hearty_meal, coin_purse, battle_hymn, gold_hoard.
- Actives (4): shield_rush, earthshaker_leap, blink, smoke_roll. Signature passives (4): bulwark, bloodlust,
  arcane_echo, momentum (Loadout.signatureIcon).
- Hero portraits (4): knight_portrait, barbarian_portrait, mage_portrait, rogue_portrait (HeroInfo.portraitIcon).

## 6. Other notes for integration

- Loadout API added for the UI/audio: rerollsLeft, banishesLeft, TryReroll(rng, out offers), TryBanish(offer,
  shown, rng, out replacement) (both spend a charge; RollLevelUp and Banish do not, as the level-up screen expects),
  activeId, signatureName / signatureIcon / signatureFraction / signatureStacks, isFuryFull, battleHymnCount,
  Dispose(); SlotView.summary (stat line); UpgradeOffer.isChestItem. Card descriptions put one "Label old -> new"
  per line (UIText.FormatDescription styles them).
- The level-up pity counts level-up PICKS without a new weapon (chest items, rerolls and skips do not count).
- Ground is assumed at y = 0 (lobbed flasks and embers land when y <= 0; meteors aim at y = 0).
- Heavy effects per second at worst: Fire Flask pools (12.5 emits/s per pool), Whirlwind (2 emits/frame while
  spinning), lightning bolts (up to ~15 dots per segment per zap).

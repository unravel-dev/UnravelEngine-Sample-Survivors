# Barrowlight spell VFX pass - brief for class agents

S = C:\Users\User\AppData\Local\Temp\claude\C--Workspace-github-UnravelEngine-UnravelEngine\4da809e6-fd2c-4f39-9b7b-211eeff32938\scratchpad\survivors

## Goal (from the user)
"Each spell and effect looks primitive, like spheres. I want better VFX, use particles. Effects should feel impactful
and high quality, and upgraded spells should feel more epic." Every weapon, its evolution and the class active must
look hand-crafted: layered, readable, punchy, and clearly grander at higher ranks and once evolved.

## What already exists (use it, do not rebuild it)
- Fx/Effects.cs: `FxLayer` + `FxComposite` (one moment = several emitters fired together). `FxLayer(effect, count,
  size, isTinted, onGround)`; `FxLayer.Ground(effect, size, isTinted)` for decals and shockwaves on the ground. `Play(position,
  sizeUnit, intensity, tint)`: sized layers get size = layer.size x sizeUnit (usually the hit radius), counts x intensity.
  `Effects.EmitSized(id, pos, count, color, size)` (emitter lifetime), `Emit(id, pos, count, color, size, lifetime)`
  (scripted lifetime), `EmitMoving(...)` (with velocity, optional color).
- Weapons/Framework/Weapon.cs: `fxScale` (1 at rank 1, 1.35 at rank 8, x1.3 more when evolved) - pass it as the
  intensity, and multiply sizes by it where a bigger look is wanted; `Composite(params FxLayer[])`, `EmitSized`,
  `EmitShape`, `EmitArc`, `NewProjectile` (sets `headScale = fxScale`), `ECHO_TINT`.
- Projectile heads (Combat/ProjectileWorld.cs `ProjectileHead`, `ProjectileVisual.heads/hideMesh`): every frame one
  particle per head layer is emitted at each projectile; the short lives fade behind it into a comet tail.
  Register heads for your looks in YOUR file `Weapons/<Class>/<Class>Looks.cs` (array of (look name, particleOnly,
  heads)); helpers `ProjectileCatalog.Core/Flame/Halo/Corona/Star(size, color)`. particleOnly hides the mesh (use it
  for orbs, darts, bolts of energy; keep meshes for real objects like knives, arrows, shields, flasks, but you can
  still add a glow/trail head to them). Look names are in Weapons/ProjectileCatalog.cs `looks`. `spawn.headTint`
  recolours the heads of one projectile (Arcane Echo style); `spawn.headScale` is set from fxScale already.
- Effect builders (Python, data only): build_effects.py (base + helpers), effect_helpers.py, build_effects_weapons.py
  (the current, first-pass weapon effects: you may stop using them; do not delete them), build_effects_spells.py (the
  NEW reference: read it fully before writing anything). It defines shared emitters you may fire from your scripts by
  name: heads HeadCore/HeadHalo/HeadFlame/HeadStar/HeadCorona; fire FireFlash/FireBall/FireShock/FireSparks/
  FireEmbers/FireSmoke/ScorchMark/FireMuzzle/SolarRays/SolarHalo; impact MeteorDust/MeteorRocks/MeteorPillar; frost
  FrostFlash/FrostRing/FrostSpikes/FrostFrost/FrostSnow/IceShatter; status BurnLick. Mage weapons
  (Weapons/Mage/EmberOrb.cs, Starfall.cs, RimeNova.cs) are the worked examples of composites in scripts.
- Textures (all white or neutral so tints work, unless noted):
  - Atlases `app:/data/Textures/Atlases/`: flames_4x2, smoke_4x2, sparks_2x2 (lightning webs), bolts_4x1 (lightning
    strands), stars_4x2, slashes_2x2 (sword arcs), traces_4x2 (streaks), scorch_2x2, magic_4x1 (magic circles/glyph
    stars), twirls_2x2 (swirls), dirt_2x2. Use texture_sheet_tiles + texture_sheet_randomize.
  - Singles `app:/data/Textures/Kenney/`: circle_01..05, dirt_01..03, fire_01..02, flame_01..06, flare_01, light_01..03,
    magic_01..05, muzzle_01..05, scorch_01..03, scratch_01, slash_01..04, smoke_01..10, spark_01..07, star_01..09,
    trace_01..07, twirl_01..03. A contact sheet of all of them: S\shots\kenney_sheet.png (look at it).
  - Older: `app:/data/Textures/Particles/` (glow, soft, spark, sparkle, ember, ring, rune_circle, shard, light_shaft,
    arc_1x4, bolt_4x1, fire_8x8, explosion_8x8, smoke_4x4, scorch), `app:/data/Textures/VFX/` (crystal_shard_2x2,
    ice_spike_4x1, frost_decal, snowflake_2x2, smoke_billow_4x4, explosion_fire_8x8 (coloured, blobby: avoid),
    glyphs_4x4, beam_4x1, bolt_8x1, arc_8x1, dust_ring, ring_energy, heal_plus), `app:/data/Textures/weapons/`
    (crack_radial, slash_x, rock_spike_2x2, rock_chunk_2x2, vortex, thorn_ring, shock_ring, fire_8x8_white).
- Particle system features worth using (property names as the builders write them): render_mode billboard /
  camera_facing / horizontal_billboard (ground decals, rings) / vertical_billboard / stretched (sparks and streaks
  along velocity: stretch_length_scale, stretch_speed_scale) / trail (a ribbon along ONE particle's own path over
  trail_length seconds: a single long-lived particle launched with the same velocity and gravity as an arcing or
  straight projectile draws its exact path); lighting unlit / volumetric (smoke) / surface (debris); particle lights
  light_count / light_intensity / light_range / light_color_from_particle (the brightest N particles light the scene:
  use on flashes and halos only, light_count <= 4, intensity 15-30, range 4-9); collision "bounce" with
  collision_planes GROUND for debris; turbulence_strength; drag; gravity_scale; frame_blending for flipbooks;
  pivot [0.5, 1] for things standing on the ground; soft_fade_distance for smoke near geometry. Do NOT use render_mode
  "ribbon" for bursts (it joins every particle of the emitter into one strip).
- Size rule (important, the engine does this): a size passed by a script is the particle's BIRTH size; if the
  size_over_life curve starts at 0, the passed size is its PEAK instead. Write scripted curves either starting at 1
  (birth = passed size) or starting at 0 and peaking at 1. Never start a scripted curve at e.g. 0.3 and grow to 1:
  it would end at 3.3x the asked size.

## Art direction
- World: dark moonlit graveyard (moor, blue-grey ground, warm candle/lantern light). Effects must pop against it but
  never white out the screen. Measured limits that look right here: color_intensity of glow/core layers <= 3.5, big
  soft glows <= 2.2, flashes 1.6-2.2 with a particle light; additive layers must not stack into white blobs - prefer
  textured shapes (flames, slashes, stars, shards) over soft glows; one short flash per impact, not several glows.
- Every attack moment gets 3-6 layers: (1) a cast/muzzle accent at the hero, (2) the body (projectile heads + trail,
  slash arcs, beams), (3) the impact: short bright flash with light, a SHAPE layer (flames / slash / shards / stars),
  radial streaks (stretched sparks or shards), something lingering (embers, smoke, dust, a ground decal that fades),
  (4) ground contact for heavy hits (shockwave ring on the ground, dust, cracks, scorch).
- Class palettes: Knight = holy gold and white with pale steel; Barbarian = earthy brown dust, rock, blood-red fury
  (red is fine for Fury only), lava orange for Tectonic Rift; Rogue = steel, poison green, shadow violet, crimson for
  the Mark; Mage = fire orange, arcane violet echo, frost cyan (done). Shared weapons each have their own colour as
  their code says (spirit pale blue, disc cyan, eclipse violet, bramble green, javelin steel/gold, flask fire, storm
  cyan-white lightning, wisps mint).
- Upgrades: ranks grow counts and sizes (fxScale). The EVOLUTION must change the look, not only its size: new
  palette accent + 1-3 extra signature layers (e.g. Dawnbreaker: sunrise rays and a golden afterimage; Final Verdict:
  a column of light from the sky; Maelstrom: a dark vortex with lightning; Tectonic Rift: lava cracks and magma
  spurts; Widowmaker: bolts leave crystal trails and burst in shards; Storm of Steel: a whirl of blades with metal
  glints; Death Sentence: a crimson execution sigil and a dark slash; and so on for the shared weapons).
- Readability: enemy telegraphs are red-orange on the ground; hero ground decals must not look like them (no
  red-orange filled circles from the hero). Keep the hero visible: no big opaque layers over the hero.
- Budget: per impact about 30-60 particles at rank 1 and up to ~150 evolved; per projectile at most 3 head layers;
  max_particles sized for the worst case (fast weapons fire often).

## Workflow and rules
- You CANNOT use the editor or its MCP (another session drives it). Work offline only. The lead builds your effects,
  pushes your scripts and captures a slow-motion gallery of each weapon (rank 1 and evolved), then sends you the image
  paths for review; you may then refine.
- Files you own (write only these): your class's weapon scripts and kit (Weapons/<Class>/** and Weapons/Kits/
  <Class>Kit.cs; shared agent: Weapons/Shared/**), Weapons/<Class>/<Class>Looks.cs, and a new builder
  S\build_effects_spells_<class>.py (lowercase class: knight, barbarian, rogue, shared). Do not edit any other file;
  if you need a core change, describe it in your report.
- Your builder imports from build_effects_spells (`from build_effects_spells import ...` for helpers/constants), uses
  `Registry` with ONE group named "<Class>Fx" (KnightFx, BarbarianFx, RogueFx, SharedFx), and every new emitter name
  starts with your class prefix (Knight..., Barbarian..., Rogue..., Shared...) so names never collide. Copy the
  `if __name__ == "__main__": REGISTRY.main(...)` pattern.
- Check: `cd S\check && dotnet build -nologo -v q` must give 0 errors; `python S\build_effects_spells_<class>.py`
  must list your effects without error (that also catches duplicate names).
- Code style: match the existing C# (PascalCase methods, constants in ALL_CAPS at the top, doc comments describe the
  current design, no history), ASCII only, LF line endings. Keep gameplay identical: never change damage, radius,
  timing or hit logic, only visuals.
- Final report: per weapon (base, evolved) and active: what each moment now shows (layers), the emitter names, any
  core requests, and the gallery command line to review them: `python gallery.py <Hero> <weapon ids...>` (heroes:
  Knight, Barbarian, Mage, Rogue; ids in Weapons/WeaponCatalog.cs).

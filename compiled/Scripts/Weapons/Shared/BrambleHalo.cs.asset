using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Bramble Halo (shared, Common): a ring of thorns of Radius (Area) on the ground around the hero that deals Damage
    /// to everything inside it every Interval (the Cooldown stat shortens the tick) and slows it (-Slow speed). The ring
    /// is an invisible projectile that follows the hero (an orbit of radius 0).
    /// Evolution Briar Sanctum (+ Iron Heart): wider and faster; enemies inside take +Bonus damage from everything, and
    /// each tick that hits heals Heal.
    /// Look: the bramble is drawn with particles (SharedLooks hides the ring's mesh): two rings of dark thorny vines
    /// turning opposite ways on a soft green band, thorns lashing up along the rim, leaves whirling around it and spores
    /// rising inside. A tick that hits tightens the ring and stabs thorns up through the enemies. Briar Sanctum's vines
    /// turn olive-gold over a golden sanctum circle, wild-rose petals drift inside, brighter thorns lash more often and
    /// the hero sparkles when it heals.
    /// </summary>
    public sealed class BrambleHalo : Weapon
    {
        /// <summary>Across-size of the ring mesh at scale 1 (the engine torus primitive is 1 m across).</summary>
        private const float RING_MESH_DIAMETER = 1.0f;
        private const float SLOW_EXTRA_SECONDS = 0.25f;
        private const float MARK_EXTRA_SECONDS = 0.15f;
        private const float THORN_INTERVAL = 0.12f;
        private const float BRIAR_THORN_INTERVAL = 0.08f;
        private const int THORN_COUNT = 3;
        /// <summary>Height (m) of a thorn spike at fxScale 1, varied by THORN_JITTER either way.</summary>
        private const float THORN_HEIGHT = 0.9f;
        private const float THORN_JITTER = 0.25f;
        /// <summary>Thorns and leaves spread this share of the radius around the rim.</summary>
        private const float RIM_SPREAD = 0.08f;
        /// <summary>Ground decals are re-emitted this often and live this long, so they follow the hero.</summary>
        private const float DECAL_INTERVAL = 0.03f;
        private const float DECAL_SECONDS = 0.09f;
        /// <summary>Vine ring diameters as shares of the halo's (thorn_ring.png draws its band at 0.915 of the quad).</summary>
        private const float OUTER_VINE_SHARE = 1.04f;
        private const float INNER_VINE_SHARE = 0.92f;
        private const float GLOW_SHARE = 1.05f;
        /// <summary>Vine turning speeds (deg/s); the inner ring turns the other way, offset by half a thorn.</summary>
        private const float OUTER_VINE_SPIN = 20.0f;
        private const float INNER_VINE_SPIN = 32.0f;
        private const float INNER_VINE_OFFSET = 6.4f;
        /// <summary>A vine copy drifts this slowly (m/s) along its angle: align_to_direction turns it, it barely moves.</summary>
        private const float VINE_DRIFT = 0.02f;
        /// <summary>The Briar sanctum circle sits just inside the thorns.</summary>
        private const float SIGIL_SHARE = 0.92f;
        private const float LEAF_INTERVAL = 0.09f;
        /// <summary>Leaves sweep along the rim (m/s) the way the outer vines turn.</summary>
        private const float LEAF_SPEED = 2.6f;
        private const float LEAF_RISE = 0.5f;
        private const float LEAF_SIZE = 0.2f;
        private const float LEAF_SECONDS = 0.9f;
        private const float LEAF_HEIGHT_MIN = 0.2f;
        private const float LEAF_HEIGHT_MAX = 1.1f;
        private const float SPORE_INTERVAL = 0.2f;
        private const float SPORE_HEIGHT = 0.3f;
        private const float PETAL_INTERVAL = 0.08f;
        private const float PETAL_HEIGHT_MIN = 1.2f;
        private const float PETAL_HEIGHT_MAX = 2.2f;
        /// <summary>The tightening ring starts just outside the thorns.</summary>
        private const float WAVE_OVERSHOOT = 1.08f;
        private const float WAVE_SECONDS = 0.3f;
        /// <summary>Enemies a tick stabs visibly (the rest are hit all the same).</summary>
        private const int MAX_STABS = 6;
        private const float STAB_HEIGHT = 0.3f;
        private const float HEAL_HEIGHT = 0.4f;
        private const float RUSTLE_GAP = 0.6f;
        /// <summary>Ticks below this damage show no number (no number spam from a ring that ticks this often).</summary>
        private const float QUIET_TICK_DAMAGE = 10.0f;

        private static readonly Color THORN_VINE = new Color(0.16f, 0.34f, 0.1f, 1.0f);
        private static readonly Color BRIAR_VINE = new Color(0.3f, 0.42f, 0.12f, 1.0f);
        private static readonly Color THORN_GLOW = new Color(0.25f, 0.55f, 0.18f, 1.0f);
        private static readonly Color BRIAR_GLOW = new Color(0.55f, 0.6f, 0.2f, 1.0f);
        private static readonly Color THORN_SPIKE = new Color(0.32f, 0.58f, 0.2f, 1.0f);
        private static readonly Color BRIAR_SPIKE = new Color(0.62f, 0.85f, 0.3f, 1.0f);
        private static readonly Color THORN_LEAF = new Color(0.4f, 0.68f, 0.25f, 1.0f);
        private static readonly Color BRIAR_LEAF = new Color(0.65f, 0.85f, 0.35f, 1.0f);
        private static readonly Color SPORE = new Color(0.6f, 1.0f, 0.45f, 1.0f);
        private static readonly Color BRIAR_GOLD = new Color(1.0f, 0.85f, 0.45f, 1.0f);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.GROUND_LIFT);

        private readonly int brambleVisual;
        private readonly int briarVisual;
        private readonly int vine;
        private readonly int glow;
        private readonly int sigil;
        private readonly int spike;
        private readonly int leaf;
        private readonly int spores;
        private readonly int petals;
        private readonly int wave;
        private readonly FxComposite stab;
        private readonly FxComposite healBloom;
        private int ring = -1;
        private int ringVisual = -1;
        private float tickTimer;
        private float thornTimer;
        private float decalTimer;
        private float leafTimer;
        private float bloomTimer;
        private Throttle rustleThrottle = new Throttle(RUSTLE_GAP);

        public BrambleHalo(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            brambleVisual = context.FindVisual("BrambleRing");
            briarVisual = context.FindVisual("BriarRing");
            vine = effects.Find("SharedBrambleVine");
            glow = effects.Find("SharedBrambleGlow");
            sigil = effects.Find("SharedBriarSigil");
            spike = effects.Find("SharedBrambleSpike");
            leaf = effects.Find("SharedBrambleLeaf");
            spores = effects.Find("SharedMotes");
            petals = effects.Find("SharedBriarPetals");
            wave = effects.Find("SharedBrambleWave");
            stab = Composite(new FxLayer("SharedThornStab", 3), new FxLayer("SharedSparks", 2));
            healBloom = Composite(new FxLayer("SharedGlints", 2), new FxLayer("SharedMotes", 2));
        }

        protected override bool usesCooldown => false;

        protected override bool Cast(in CastInfo cast) => false;

        protected override void Update(float dt)
        {
            float radius = AreaOf(level[WeaponField.Radius]);
            KeepRing(radius);
            tickTimer -= dt;
            if (tickTimer <= 0.0f)
            {
                tickTimer += level[WeaponField.Interval] * stats[StatId.Cooldown];
                Pulse(radius);
            }
            EmitBramble(radius, dt);
            EmitThorns(radius, dt);
            EmitLeaves(radius, dt);
            EmitBloom(radius, dt);
        }

        /// <summary>The ring mesh follows the hero; it is respawned when the look changes or the row was cleared.</summary>
        private void KeepRing(float radius)
        {
            int visual = isEvolved ? briarVisual : brambleVisual;
            if (ringVisual != visual || !IsOwned(ring, ctx.visualOnly, visual))
            {
                ExpireOwned(ref ring, ctx.visualOnly, ringVisual);
                ring = SpawnVisual(visual, ProjectileMotion.Orbit, heroPosition, Vector3.zero, PERMANENT);
                ringVisual = visual;
            }
            if (ring >= 0)
            {
                projectiles.Get(ring).scale = radius * 2.0f / RING_MESH_DIAMETER;
            }
        }

        private void Pulse(float radius)
        {
            float tick = level[WeaponField.Interval] * stats[StatId.Cooldown];
            float damage = DamageOf(WeaponField.Damage, CastInfo.normal);
            var effect = new HitEffect
            {
                hideNumber = damage < QUIET_TICK_DAMAGE,
                slowFactor = 1.0f - level[WeaponField.Slow],
                slowSeconds = tick + SLOW_EXTRA_SECONDS,
                markBonus = level[WeaponField.Bonus],
                markSeconds = tick + MARK_EXTRA_SECONDS,
            };
            int hits = strikes.Circle(heroPosition, radius, damage, 0.0f, slot, effect, PushMode.None);
            if (hits == 0)
            {
                return;
            }
            ShowTick(radius);
            float heal = level[WeaponField.Heal];
            if (heal > 0.0f)
            {
                hero.Heal(heal, HealSource.Weapon);
                healBloom.Play(heroPosition + Ground.Up(HEAL_HEIGHT), fxScale, fxScale, BRIAR_GOLD);
            }
            if (rustleThrottle.TryPass(clock))
            {
                PlayAt("bramble_rustle", heroPosition);
            }
        }

        /// <summary>A tick that hit: the ring tightens and thorns stab up through the first few enemies it hurt.</summary>
        private void ShowTick(float radius)
        {
            Vector3 ground = heroPosition + LIFT;
            EmitShape(wave, ground, isEvolved ? BRIAR_GOLD : THORN_GLOW, radius * 2.0f * WAVE_OVERSHOOT, WAVE_SECONDS);
            Color color = isEvolved ? BRIAR_SPIKE : THORN_SPIKE;
            ReadOnlySpan<int> hits = strikes.lastHits;
            int shown = 0;
            for (int i = 0; i < hits.Length && shown < MAX_STABS; i++)
            {
                int row = hits[i];
                if (!enemies.IsTargetable(row))
                {
                    continue;
                }
                stab.Play(enemies.GetPosition(row) + Ground.Up(STAB_HEIGHT), fxScale, fxScale, color);
                shown++;
            }
        }

        /// <summary>
        /// The bramble on the ground, re-emitted so it follows the hero: a soft green band under two rings of dark
        /// thorny vines turning opposite ways (and Briar's sanctum circle).
        /// </summary>
        private void EmitBramble(float radius, float dt)
        {
            decalTimer -= dt;
            if (decalTimer > 0.0f)
            {
                return;
            }
            decalTimer = DECAL_INTERVAL;
            Vector3 ground = heroPosition + LIFT;
            float diameter = radius * 2.0f;
            float turn = isEvolved ? -1.0f : 1.0f;
            Color vineColor = isEvolved ? BRIAR_VINE : THORN_VINE;
            EmitShape(glow, ground, isEvolved ? BRIAR_GLOW : THORN_GLOW, diameter * GLOW_SHARE, DECAL_SECONDS);
            EmitVine(ground, clock * OUTER_VINE_SPIN * turn, vineColor, diameter * OUTER_VINE_SHARE);
            EmitVine(ground, INNER_VINE_OFFSET - clock * INNER_VINE_SPIN * turn, vineColor, diameter * INNER_VINE_SHARE);
            if (isEvolved)
            {
                EmitShape(sigil, ground, BRIAR_GOLD, diameter * SIGIL_SHARE, DECAL_SECONDS);
            }
        }

        /// <summary>One copy of a vine ring turned to <paramref name="degrees"/> (its slow drift sets the angle).</summary>
        private void EmitVine(Vector3 ground, float degrees, Color color, float diameter)
        {
            effects.EmitMoving(vine, ground, Ground.FromYaw(degrees) * VINE_DRIFT, 1, color, diameter, DECAL_SECONDS);
        }

        /// <summary>Thorn spikes lashing up from the ground along the rim.</summary>
        private void EmitThorns(float radius, float dt)
        {
            thornTimer -= dt;
            if (thornTimer > 0.0f)
            {
                return;
            }
            thornTimer = isEvolved ? BRIAR_THORN_INTERVAL : THORN_INTERVAL;
            Color color = isEvolved ? BRIAR_SPIKE : THORN_SPIKE;
            for (int k = 0; k < THORN_COUNT; k++)
            {
                Vector3 at = Ground.Flat(heroPosition + Ground.RandomDirection() * (radius * Random.Range(1.0f - RIM_SPREAD, 1.0f + RIM_SPREAD)));
                EmitSized(spike, at, color, THORN_HEIGHT * fxScale * Random.Range(1.0f - THORN_JITTER, 1.0f + THORN_JITTER));
            }
        }

        /// <summary>Leaves swept along the rim in the ring's turning direction.</summary>
        private void EmitLeaves(float radius, float dt)
        {
            leafTimer -= dt;
            if (leafTimer > 0.0f)
            {
                return;
            }
            leafTimer = LEAF_INTERVAL;
            Vector3 outward = Ground.RandomDirection();
            float turn = isEvolved ? -1.0f : 1.0f;
            Vector3 at = Ground.Flat(heroPosition) + outward * (radius * Random.Range(1.0f - RIM_SPREAD, 1.0f + RIM_SPREAD))
                         + Ground.Up(Random.Range(LEAF_HEIGHT_MIN, LEAF_HEIGHT_MAX));
            Vector3 velocity = Ground.Perpendicular(outward) * (LEAF_SPEED * turn) + Ground.Up(LEAF_RISE);
            effects.EmitMoving(leaf, at, velocity, 1, isEvolved ? BRIAR_LEAF : THORN_LEAF, LEAF_SIZE * fxScale, LEAF_SECONDS);
        }

        /// <summary>Spores rising inside the ring; Briar Sanctum drifts rose petals instead.</summary>
        private void EmitBloom(float radius, float dt)
        {
            bloomTimer -= dt;
            if (bloomTimer > 0.0f)
            {
                return;
            }
            Vector3 inside = Ground.Flat(heroPosition) + Ground.RandomInCircle(radius);
            if (isEvolved)
            {
                bloomTimer = PETAL_INTERVAL;
                Emit(petals, inside + Ground.Up(Random.Range(PETAL_HEIGHT_MIN, PETAL_HEIGHT_MAX)), 1, WHITE);
                return;
            }
            bloomTimer = SPORE_INTERVAL;
            Emit(spores, inside + Ground.Up(SPORE_HEIGHT), 1, SPORE);
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Shadow Dancers (Rogue, Rare): while the rogue has at least Stacks Momentum, every SubRange metres she runs leave
    /// a shadow clone where she was, while fewer than Count + Amount are alive (no cooldown: standing still, or below
    /// the Momentum, makes none; Smoke Roll's dash counts as running). A clone lives Duration seconds (Duration stat);
    /// every Interval (Cooldown stat) it throws SubCount homing shadow daggers (Speed, Turn) at the enemy nearest to it
    /// within Range, Damage each (Might, Projectile Speed), and when it fades it bursts for SubDamage in SubRadius
    /// (Area). Clone damage never rolls its own crits, but spends Smoke Roll's loaded ones like any Rogue weapon.
    /// Evolution Legion of Shadows (+ Windstride Boots): clones come thicker, last longer and crit at CritChance times
    /// the rogue's crit chance (so clone crits rupture venom too), and every Smoke Roll leaves MajorCount clones in a
    /// ring around its cloud (RogueKit.smokeDropped), beyond the cap.
    /// Look: a clone is a violet afterimage of the rogue, about her height: it pops in with a bright pale violet rim, a
    /// small flash and a shadow puff, then stands as a hooded dark violet shade edged by a violet rim, with pale eyes on
    /// the hood, over a dark pool, shimmering (one sprite set refreshed every 0.2 s with a little jitter) while shadow
    /// wisps curl at its feet; it fades in, dissolves toward its burst, and thins out while the rogue is right next to it
    /// so a crowd of clones never hides her. Each throw flares its rim, swipes a violet
    /// crescent toward the target and glints at the hand; the daggers fly with a slim violet shadow flame and cut where
    /// they strike. The fade burst is a violet flash, shadow smoke, a shadow shockwave out to the burst radius and
    /// steel-violet glints. Legion clones stand taller with a brighter rim and violet shadow flames licking up around
    /// them, their crits glint steel-white, their bursts add a whirl of crescent blades and a rising violet shaft, and
    /// the Smoke Roll ring rises out of the smoke on violet pillars over a wide shadow shockwave.
    /// </summary>
    public sealed class ShadowDancers : Weapon
    {
        private const int SMOKE_CLONE = 0;
        private const int MAX_CLONES = 16;
        /// <summary>Movement in one frame beyond this is a teleport, not running.</summary>
        private const float MAX_STEP = 2.0f;
        /// <summary>A clone stands this far behind where the rogue is when it is left.</summary>
        private const float CLONE_BEHIND = 0.3f;
        /// <summary>The first throw comes this long after the clone appears (plus up to the jitter, so clones do not sync).</summary>
        private const float FIRST_THROW = 0.3f;
        private const float FIRST_THROW_JITTER = 0.15f;
        private const float THROW_RETRY = 0.15f;
        private const float KNIFE_HEIGHT = 1.1f;
        private const float KNIFE_OUT = 0.3f;
        private const float KNIFE_SPREAD = 9.0f;
        private const float KNIFE_RADIUS = 0.3f;
        private const float KNIFE_FLIGHT_MARGIN = 1.3f;
        /// <summary>Clone daggers keep a slimmer shadow flame than the mark's (head scale x fxScale).</summary>
        private const float KNIFE_HEAD_SCALE = 0.7f;
        /// <summary>The clone's sprites are refreshed this often and live a little longer, so they cross-fade (a shimmer).</summary>
        private const float REFRESH = 0.2f;
        private const float REFRESH_OVERLAP = 1.3f;
        private const float SHIMMER = 0.04f;
        /// <summary>The clone's sprite size (m): its shade fills about a third of it, standing about as tall as the rogue.</summary>
        private const float CLONE_HEIGHT = 4.6f;
        private const float LEGION_SCALE = 1.12f;
        private const float FADE_IN = 0.15f;
        private const float FADE_OUT = 0.35f;
        /// <summary>Clones close to the rogue thin out (down to the minimum within the inner distance) so she stays visible.</summary>
        private const float HERO_CLEAR_INNER = 0.5f;
        private const float HERO_CLEAR_OUTER = 1.6f;
        private const float HERO_CLEAR_MIN = 0.2f;
        /// <summary>The eyes: apart along the camera's right (+X), at this share of the sprite size above the feet (the hood).</summary>
        private const float EYE_GAP = 0.085f;
        private const float EYE_HEIGHT_SHARE = 0.37f;
        private const float EYE_SIZE = 0.15f;
        private const float POOL_SIZE = 1.3f;
        private const int LEGION_FLAMES = 2;
        /// <summary>The pop as a clone is left: a brighter, slightly larger rim that fades as the clone fades in.</summary>
        private const float POP_SCALE = 1.12f;
        private const float POP_SECONDS = 0.3f;
        /// <summary>Throw flourish: a brighter rim for a moment, a crescent swiped toward the target, a glint at the hand.</summary>
        private const float FLARE_SECONDS = 0.16f;
        /// <summary>The flared rim stands this much larger than the clone's own.</summary>
        private const float FLARE_SCALE = 1.04f;
        private const float SWIPE_OUT = 0.45f;
        private const float SWIPE_SPEED = 4.0f;
        private const float SWIPE_SIZE = 1.0f;
        private const float SWIPE_SECONDS = 0.14f;
        private const float HAND_SIDE = 0.25f;
        private const float GLINT_SIZE = 0.35f;
        private const float CUT_SIZE = 0.6f;
        private const float CRIT_GLINT_SIZE = 0.8f;
        /// <summary>Legion bursts: crescent blades sliding around the clone.</summary>
        private const int BURST_BLADES = 6;
        private const float BLADE_RADIUS = 0.8f;
        private const float BLADE_SPEED = 7.0f;
        private const float BLADE_SIZE = 1.0f;
        private const float BLADE_SECONDS = 0.22f;
        private const float BLADE_HEIGHT = 0.9f;
        /// <summary>Smoke Roll's Legion clones stand on a ring at this share of the cloud's radius, appearing in turn.</summary>
        private const float SMOKE_RING_SHARE = 0.85f;
        private const float SMOKE_CLONE_GAP = 0.06f;
        /// <summary>The Smoke Roll ring's shockwave: its size per metre of the cloud's radius.</summary>
        private const float SMOKE_SHOCK_PER_RADIUS = 2.6f;
        private const float PILLAR_SIZE = 2.6f;
        private const float BODY_HEIGHT = 1.0f;
        private const float WHOOSH_VOLUME = 0.35f;
        private const float THROW_VOLUME = 0.3f;
        private const float THROW_PITCH = 1.25f;
        private const float BURST_VOLUME = 0.45f;
        private const float BURST_PITCH = 1.3f;

        private static readonly Vector3 LIFT = Ground.Up(FxSizes.GROUND_LIFT);
        private static readonly Vector3 BODY = Ground.Up(BODY_HEIGHT);

        private struct Clone
        {
            public Vector3 position;
            public float age;
            public float life;
            public float throwTimer;
            public float refreshTimer;
            /// <summary>When the last throw flared the rim (game clock), for the flourish.</summary>
            public float flareUntil;
        }

        private readonly Clone[] clones = new Clone[MAX_CLONES];
        private readonly RogueKit rogue;
        private readonly int daggerVisual;
        private readonly int shade;
        private readonly int rim;
        private readonly int eyes;
        private readonly int pool;
        private readonly int wisp;
        private readonly int legionFlame;
        private readonly int swipe;
        private readonly int glint;
        private readonly int knifeCut;
        private readonly int shock;
        private readonly int blade;
        private readonly int pillar;
        private readonly FxComposite cloneArrive;
        private readonly FxComposite knifeHit;
        private readonly FxComposite knifeCrit;
        private readonly FxComposite burst;
        private readonly FxComposite legionArrive;
        private int cloneCount;
        private float travelled;
        private Vector3 lastHeroPosition;

        public ShadowDancers(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            daggerVisual = context.FindVisual("ShadowDagger");
            shade = effects.Find("RogueDancerShade");
            rim = effects.Find("RogueDancerRim");
            eyes = effects.Find("RogueDancerEyes");
            pool = effects.Find("RogueDancerPool");
            wisp = effects.Find("RogueMomentumWisp");
            legionFlame = effects.Find("RogueLegionFlame");
            swipe = effects.Find("RogueKnifeArc");
            glint = effects.Find("RogueGlint");
            knifeCut = effects.Find("RogueKnifeCut");
            shock = effects.Find("RogueShadowShock");
            blade = effects.Find("RogueStormBlade");
            pillar = effects.Find("RogueLegionPillar");
            cloneArrive = Composite(new FxLayer("RogueFlash", 1, 0.6f), new FxLayer("RogueShadowPuff", 3, 0.6f, false));
            knifeHit = Composite(new FxLayer("RogueSparks", 2));
            knifeCrit = Composite(new FxLayer("RogueFlash", 1, 0.5f), new FxLayer("RogueSparks", 5));
            burst = Composite(new FxLayer("RogueFlash", 1, 1.0f), new FxLayer("RogueShadowPuff", 5, 0.8f, false), new FxLayer("RogueStormGlints", 6));
            legionArrive = Composite(new FxLayer("RogueFlash", 1, 1.6f), new FxLayer("RogueShadowPuff", 6, 0.9f, false), new FxLayer("RogueStormGlints", 10));
            lastHeroPosition = heroPosition;
            rogue = context.KitAs<RogueKit>();
            if (rogue != null)
            {
                rogue.smokeDropped += OnSmokeDropped;
            }
        }

        protected override void OnDispose()
        {
            if (rogue != null)
            {
                rogue.smokeDropped -= OnSmokeDropped;
            }
        }

        protected override bool usesCooldown => false;

        protected override bool Cast(in CastInfo cast) => false;

        private int cloneLimit => Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_CLONES);

        /// <summary>Metres the rogue runs per clone she leaves.</summary>
        private float cloneSpacing => level[WeaponField.SubRange];

        /// <summary>Momentum the rogue needs before running leaves clones.</summary>
        private int momentumNeeded => level.GetCount(WeaponField.Stacks);

        /// <summary>Legion of Shadows: clones each Smoke Roll leaves around its cloud.</summary>
        private int smokeRollClones => level.GetCount(WeaponField.MajorCount);

        protected override void Update(float dt)
        {
            TrackRunning();
            StepClones(dt);
        }

        /// <summary>Counts the metres run with enough Momentum; every SubRange of them leaves a clone while there is room.</summary>
        private void TrackRunning()
        {
            Vector3 position = heroPosition;
            float step = Ground.Distance(lastHeroPosition, position);
            lastHeroPosition = position;
            if (!hero.isAlive || ctx.kit.signatureStacks < momentumNeeded)
            {
                travelled = 0.0f;
                return;
            }
            float every = cloneSpacing;
            if (step <= MAX_STEP)
            {
                travelled = MathF.Min(travelled + step, every);
            }
            if (travelled < every || cloneCount >= cloneLimit)
            {
                return;
            }
            travelled = 0.0f;
            AddClone(position - facing * CLONE_BEHIND);
            cloneArrive.Play(position + BODY, FxSizes.METRE_UNIT, fxScale, RogueLooks.SHADOW_VIOLET);
            PlayAt("shadow_whoosh", position, WHOOSH_VOLUME);
        }

        /// <summary>A new clone; a full list makes room by bursting the oldest.</summary>
        private void AddClone(Vector3 position)
        {
            if (cloneCount >= MAX_CLONES)
            {
                int oldest = FindOldest();
                Burst(clones[oldest]);
                clones[oldest] = clones[--cloneCount];
            }
            Vector3 foot = Ground.Flat(position);
            clones[cloneCount++] = new Clone
            {
                position = foot,
                life = DurationOf(level[WeaponField.Duration]),
                throwTimer = FIRST_THROW + Random.Range(0.0f, FIRST_THROW_JITTER),
            };
            float scale = isEvolved ? LEGION_SCALE : 1.0f;
            effects.Emit(rim, foot + LIFT, 1, RogueLooks.PALE_VIOLET, CLONE_HEIGHT * scale * POP_SCALE, POP_SECONDS);
        }

        private int FindOldest()
        {
            int oldest = 0;
            for (int i = 1; i < cloneCount; i++)
            {
                if (clones[i].age > clones[oldest].age)
                {
                    oldest = i;
                }
            }
            return oldest;
        }

        private void StepClones(float dt)
        {
            float interval = MathF.Max(level[WeaponField.Interval] * stats[StatId.Cooldown], THROW_RETRY);
            int i = 0;
            while (i < cloneCount)
            {
                ref Clone clone = ref clones[i];
                clone.age += dt;
                if (clone.age >= clone.life)
                {
                    Burst(clone);
                    clones[i] = clones[--cloneCount];
                    continue;
                }
                clone.throwTimer -= dt;
                if (clone.throwTimer <= 0.0f)
                {
                    clone.throwTimer = MathF.Max(clone.throwTimer, 0.0f) + (Throw(ref clone) ? interval : THROW_RETRY);
                }
                clone.refreshTimer -= dt;
                if (clone.refreshTimer <= 0.0f)
                {
                    clone.refreshTimer = MathF.Max(clone.refreshTimer + REFRESH, 0.0f);
                    ShowClone(clone);
                }
                i++;
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Throws, hits and bursts
        // -------------------------------------------------------------------------------------------------------

        private bool Throw(ref Clone clone)
        {
            float range = level[WeaponField.Range];
            if (!enemies.QueryNearest(clone.position, range, out int target))
            {
                return false;
            }
            Vector3 direction = Ground.Direction(clone.position, enemies.GetPosition(target), facing);
            Vector3 chest = clone.position + Ground.Up(KNIFE_HEIGHT);
            int knives = Math.Max(1, level.GetCount(WeaponField.SubCount));
            float speed = SpeedOf(level[WeaponField.Speed]);
            float flight = range * KNIFE_FLIGHT_MARGIN / speed;
            for (int k = 0; k < knives; k++)
            {
                Vector3 heading = Ground.Rotate(direction, (k - (knives - 1) * 0.5f) * KNIFE_SPREAD);
                ProjectileSpawn spawn = NewProjectile(daggerVisual, ProjectileMotion.Homing, chest + heading * KNIFE_OUT, heading * speed, flight);
                spawn.homing = level[WeaponField.Turn] * Ground.DEGREES_TO_RADIANS;
                spawn.damage = MightOf(level[WeaponField.Damage]);
                spawn.radius = KNIFE_RADIUS;
                spawn.pierce = 0;
                spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
                spawn.handler = this;
                spawn.headScale = KNIFE_HEAD_SCALE * fxScale;
                Spawn(spawn);
            }
            clone.flareUntil = clock + FLARE_SECONDS;
            ShowThrow(clone, chest, direction);
            return true;
        }

        /// <summary>Clone damage: no crits of its own; Legion crits at its share of the rogue's crit chance.</summary>
        private HitEffect GetCloneHit()
        {
            float share = isEvolved ? level[WeaponField.CritChance] : 0.0f;
            return new HitEffect { critChanceBonus = -(1.0f - share) * stats[StatId.CritChance] };
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn knife = ref world.Get(projectile);
            Vector3 impact = knife.position;
            if (HitAlongFlight(enemyRow, knife, WithAutoCrit(GetCloneHit())) == DamageResult.Missed)
            {
                return;
            }
            Color color = isEvolved ? RogueLooks.LEGION_RIM : RogueLooks.SHADOW_VIOLET;
            RogueLooks.EmitKnifeCut(effects, knifeCut, impact, FlightOf(knife), color, CUT_SIZE * fxScale);
            if (!strikes.lastWasCritical)
            {
                knifeHit.Play(impact, FxSizes.METRE_UNIT, fxScale, color);
                return;
            }
            knifeCrit.Play(impact, FxSizes.METRE_UNIT, fxScale, color);
            EmitSized(glint, impact, RogueLooks.STEEL_WHITE, CRIT_GLINT_SIZE * fxScale);
        }

        /// <summary>The clone fades: a shadow burst around it (Legion: a whirl of blades and a rising shaft).</summary>
        private void Burst(in Clone clone)
        {
            float radius = AreaOf(level[WeaponField.SubRadius]);
            strikes.Circle(clone.position, radius, MightOf(level[WeaponField.SubDamage]), KnockbackOf(level[WeaponField.Knockback]), slot, GetCloneHit(),
                           PushMode.Outward);
            Color color = isEvolved ? RogueLooks.LEGION_RIM : RogueLooks.DANCER_RIM;
            burst.Play(clone.position + BODY, FxSizes.METRE_UNIT, fxScale, color);
            EmitSized(shock, clone.position + LIFT, color, radius * 2.0f);
            if (isEvolved)
            {
                ShowBladeWhirl(clone.position, color);
                EmitSized(pillar, clone.position + LIFT, color, PILLAR_SIZE);
            }
            PlayAt("smoke_poof", clone.position, BURST_VOLUME, BURST_PITCH);
        }

        // -------------------------------------------------------------------------------------------------------
        // Smoke Roll (Legion of Shadows)
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Legion: the smoke cloud's ring of clones, appearing in turn (scheduled: this runs inside the activation).</summary>
        private void OnSmokeDropped(Vector3 center, float radius)
        {
            if (!isEvolved)
            {
                return;
            }
            int count = smokeRollClones;
            float start = Ground.RandomYaw();
            for (int k = 0; k < count; k++)
            {
                Vector3 at = Ground.Flat(center) + Ground.RingDirection(start, k, count) * (radius * SMOKE_RING_SHARE);
                Schedule(SMOKE_CLONE_GAP * (k + 1), new WeaponEvent { kind = SMOKE_CLONE, position = at });
            }
            EmitSized(shock, Ground.Flat(center) + LIFT, RogueLooks.LEGION_RIM, radius * SMOKE_SHOCK_PER_RADIUS);
            PlayAt("summon", center, WHOOSH_VOLUME);
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind != SMOKE_CLONE)
            {
                return;
            }
            AddClone(e.position);
            EmitSized(pillar, e.position + LIFT, RogueLooks.LEGION_RIM, PILLAR_SIZE);
            legionArrive.Play(e.position + BODY, FxSizes.METRE_UNIT, fxScale, RogueLooks.LEGION_RIM);
            PlayAt("shadow_whoosh", e.position, WHOOSH_VOLUME);
        }

        // -------------------------------------------------------------------------------------------------------
        // Looks
        // -------------------------------------------------------------------------------------------------------

        /// <summary>One refresh of a clone: shade, rim (flared after a throw), eyes, pool, wisps; Legion adds shadow flames.</summary>
        private void ShowClone(in Clone clone)
        {
            float scale = isEvolved ? LEGION_SCALE : 1.0f;
            float height = CLONE_HEIGHT * scale;
            float presence = MathF.Min(1.0f, clone.age / FADE_IN) * MathF.Min(1.0f, (clone.life - clone.age) / FADE_OUT) * GetHeroClearance(clone.position);
            float seconds = REFRESH * REFRESH_OVERLAP;
            Vector3 foot = clone.position + Ground.RandomInCircle(SHIMMER) + LIFT;
            var shadeColor = new Color(1.0f, 1.0f, 1.0f, presence);
            Color rimColor = isEvolved ? RogueLooks.LEGION_RIM : RogueLooks.DANCER_RIM;
            rimColor.a = presence;
            effects.Emit(shade, foot, 1, shadeColor, height, seconds);
            effects.Emit(rim, foot, 1, rimColor, height, seconds);
            if (clock < clone.flareUntil)
            {
                effects.Emit(rim, foot, 1, rimColor, height * FLARE_SCALE, FLARE_SECONDS);
            }
            Vector3 eyeLine = foot + Ground.Up(height * EYE_HEIGHT_SHARE);
            Color eyeColor = RogueLooks.DANCER_EYES;
            eyeColor.a = presence;
            effects.Emit(eyes, eyeLine + new Vector3(-EYE_GAP * scale, 0.0f, 0.0f), 1, eyeColor, EYE_SIZE * scale, seconds);
            effects.Emit(eyes, eyeLine + new Vector3(EYE_GAP * scale, 0.0f, 0.0f), 1, eyeColor, EYE_SIZE * scale, seconds);
            effects.Emit(pool, clone.position + LIFT, 1, shadeColor, POOL_SIZE * scale, seconds);
            effects.Emit(wisp, foot, 1, RogueLooks.SHADOW_VIOLET);
            if (isEvolved)
            {
                effects.Emit(legionFlame, foot, LEGION_FLAMES, RogueLooks.SHADOW_VIOLET);
            }
        }

        /// <summary>How much of a clone shows at its distance from the rogue (thin over her, whole from the outer distance).</summary>
        private float GetHeroClearance(Vector3 position)
        {
            float share = (Ground.Distance(position, heroPosition) - HERO_CLEAR_INNER) / (HERO_CLEAR_OUTER - HERO_CLEAR_INNER);
            return Math.Clamp(share, HERO_CLEAR_MIN, 1.0f);
        }

        /// <summary>The throw: a violet crescent swiped toward the target from the chest and a glint at the throwing hand.</summary>
        private void ShowThrow(in Clone clone, Vector3 chest, Vector3 direction)
        {
            Color color = isEvolved ? RogueLooks.LEGION_RIM : RogueLooks.PALE_VIOLET;
            effects.EmitMoving(swipe, chest + direction * SWIPE_OUT, direction * SWIPE_SPEED, 1, color, SWIPE_SIZE * fxScale, SWIPE_SECONDS);
            Vector3 hand = chest + Ground.Perpendicular(direction) * HAND_SIDE;
            EmitSized(glint, hand, RogueLooks.STEEL_WHITE, GLINT_SIZE * fxScale);
            PlayAt("knife_throw", clone.position, THROW_VOLUME, THROW_PITCH);
        }

        /// <summary>Legion bursts: crescent blades sliding around the clone (counter-clockwise from above).</summary>
        private void ShowBladeWhirl(Vector3 position, Color color)
        {
            float start = Ground.RandomYaw();
            Vector3 center = position + Ground.Up(BLADE_HEIGHT);
            for (int k = 0; k < BURST_BLADES; k++)
            {
                Vector3 outward = Ground.RingDirection(start, k, BURST_BLADES);
                Vector3 around = -Ground.Perpendicular(outward);
                effects.EmitMoving(blade, center + outward * BLADE_RADIUS, around * BLADE_SPEED, 1, color, BLADE_SIZE * fxScale, BLADE_SECONDS);
            }
        }
    }
}

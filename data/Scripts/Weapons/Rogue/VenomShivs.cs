using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Venom Shivs (Rogue, Uncommon): a quick volley of Count + Amount poisoned shivs, 0.05 s apart and fanned 3 deg,
    /// all at one nearby enemy (Pierce carries them on), at Speed (Projectile Speed). The focus is kept while it lives
    /// within Range below the stack cap; then the most poisoned of the nearest few below the cap takes over (a random
    /// one when none is poisoned), so venom builds fast where the other Rogue weapons strike. Every hit deals Damage and
    /// adds a venom stack: SubDamage per stack per second (Might) for Duration seconds (Duration stat) refreshed by each
    /// stack, at most Stacks. A critical hit from any Rogue class weapon (Momentum crits, Smoke Roll's loaded crits,
    /// the shivs themselves) on an enemy carrying at least Every stacks ruptures it (<see cref="VenomRuptures"/>):
    /// Multiplier times the venom damage it had left at once, and half its stacks splash onto the nearest enemies within
    /// SubRadius (Area).
    /// Evolution Nightshade Plague (+ Keen Eye): no stack cap for shivs and clouds (the focus moves on at
    /// <see cref="FOCUS_STACKS_MAX"/>); splashed neighbours that reach Every stacks rupture in turn, and every rupture
    /// leaves a toxic cloud for SubDuration seconds (Duration stat) that adds a venom stack to, and slows, what stands
    /// in it.
    /// Look: each shiv leaves the hand with a venom glint and a green crescent and flies as a knife with a green star
    /// glint, a thin venom tracer and drips falling off it; hits splash venom and cut across the path, and an enemy that
    /// reaches the rupture threshold pops a venom ring and bubbles. Poisoned enemies (a budgeted few per tick) waft a
    /// green toxic vapour, bubble and drip; primed ones (Every stacks or more) carry a larger vapour and smoke with
    /// sickly fumes. A rupture is a green-black splash pulled in front of the body: a venom flash that lights the
    /// ground, a near-black ink splat under a bright venom splatter, a flat splatter on the ground, venom droplets flung
    /// out, bubbles and lingering dark fumes, a faint venom shockwave out to the splash radius, a dark stain left behind,
    /// and venom globs arcing onto the splashed neighbours (all bigger with more stacks). Nightshade adds berry-violet
    /// shiv glints, a violet nightshade bloom and spores, chain ruptures that pop where the globs land (lighter: no flash
    /// or shockwave, so a chain never whites out), and the toxic clouds (<see cref="ToxicClouds"/>).
    /// </summary>
    public sealed class VenomShivs : Weapon
    {
        private const int THROW = 0;
        private const int MAX_VOLLEY = 16;
        private const float SHIV_STAGGER = 0.05f;
        /// <summary>A new focus is the most poisoned (below the cap) of this many nearest enemies.</summary>
        private const int FOCUS_POOL = 6;
        /// <summary>Without a stack cap (Nightshade) the focus still moves on once it carries this many stacks.</summary>
        private const float FOCUS_STACKS_MAX = 12.0f;
        /// <summary>The stack cap when the data says none (Stacks below 0).</summary>
        private const float NO_STACK_CAP = float.MaxValue;
        /// <summary>Degrees between the shivs of a volley.</summary>
        private const float SHIV_SPREAD = 3.0f;
        private const float SHIV_RADIUS = 0.3f;
        private const float SHIV_SCALE = 0.8f;
        private const float LAUNCH_HEIGHT = 1.0f;
        private const float LAUNCH_FORWARD = 0.35f;
        /// <summary>Shivs fly a little past the range, so a target at its edge is still reached.</summary>
        private const float FLIGHT_MARGIN = 1.15f;
        private const float TRACER_WIDTH = 0.035f;
        private const float TRACER_OVERSHOOT = 1.2f;
        /// <summary>Live shivs followed for their drips.</summary>
        private const int MAX_SHIVS = 48;
        private const float DRIP_INTERVAL = 0.035f;
        private const float DRIP_SIZE = 0.07f;
        private const float ARC_OUT = 0.5f;
        private const float ARC_SPEED_SHARE = 0.55f;
        private const float ARC_SIZE = 0.6f;
        private const float ARC_SECONDS = 0.12f;
        private const float GLINT_SIZE = 0.3f;
        private const float CUT_SIZE = 0.6f;
        /// <summary>Venom globs arcing onto splashed neighbours: this long in flight.</summary>
        private const float GLOB_SECONDS = 0.2f;
        /// <summary>Must match the glob emitter's gravity (gravity_scale 1).</summary>
        private const float GLOB_GRAVITY = 9.81f;
        private const float GLOB_WIDTH = 0.05f;
        private const float GLOB_START_HEIGHT = 1.0f;
        private const float GLOB_END_HEIGHT = 0.8f;
        /// <summary>A rupture reads bigger with more stacks: size unit = base + per stack, up to the cap.</summary>
        private const float RUPTURE_SCALE_BASE = 0.9f;
        private const float RUPTURE_SCALE_PER_STACK = 0.06f;
        private const float RUPTURE_SCALE_MAX = 1.5f;
        /// <summary>The shockwave shows the splash reach at this opacity (a thin line, never a filled telegraph).</summary>
        private const float SHOCK_ALPHA = 0.6f;
        /// <summary>The dark stain (m, times the rupture's scale) and how long it stays.</summary>
        private const float STAIN_SIZE = 2.0f;
        private const float STAIN_SECONDS = 1.6f;
        /// <summary>The flat splash under a rupture (m, times the rupture's scale): trigger and chain ruptures.</summary>
        private const float SPLAT_SIZE = 2.6f;
        private const float CHAIN_SPLAT_SIZE = 1.8f;
        /// <summary>A chain rupture's nightshade bloom, as a share of a trigger rupture's.</summary>
        private const float CHAIN_BLOOM_SHARE = 0.7f;
        private const float RUPTURE_VOLUME = 0.9f;
        private const float CHAIN_VOLUME = 0.5f;
        private const float RUPTURE_PITCH = 0.8f;
        /// <summary>Status look: every interval, up to the budget of poisoned enemies show their venom.</summary>
        private const float STATUS_INTERVAL = 0.08f;
        private const int STATUS_BUDGET = 24;
        private const float CHEST_SHARE = 0.55f;
        /// <summary>A rupture bursts at this share of the body's height, pulled toward the camera (see TOWARD_CAMERA).</summary>
        private const float BURST_HEIGHT_SHARE = 0.7f;
        private const float FUME_HEIGHT_SHARE = 0.85f;
        private const float FUME_SIZE_SHARE = 0.45f;
        /// <summary>Status looks sit this share of the burst's pull toward the camera, so the body does not hide them.</summary>
        private const float STATUS_TOWARD_CAMERA = 0.6f;
        /// <summary>The toxic aura's size as a share of the body's height: poisoned, and primed.</summary>
        private const float AURA_SIZE_SHARE = 0.65f;
        private const float PRIMED_AURA_SIZE_SHARE = 0.9f;
        private const int BUBBLES = 1;
        private const int PRIMED_BUBBLES = 2;
        /// <summary>Nightshade clouds: radius as a share of the splash radius, and what a tick does to those inside.</summary>
        private const float CLOUD_RADIUS_SHARE = 0.8f;
        private const float CLOUD_STACKS = 1.0f;
        private const float CLOUD_SLOW_FACTOR = 0.6f;
        private const float CLOUD_SLOW_SECONDS = 0.6f;
        private const float THROW_VOLUME = 0.45f;
        private const float THROW_PITCH = 1.2f;
        private const int QUERY_CAPACITY = 24;

        /// <summary>Toward the camera (CameraRig looks along +Z from above at yaw 0): splashes drawn in front of the body.</summary>
        private static readonly Vector3 TOWARD_CAMERA = new Vector3(0.0f, 0.3f, -0.5f);

        private readonly VenomLedger ledger;
        private readonly VenomRuptures ruptures;
        private readonly ToxicClouds clouds;
        private readonly int[] nearest = new int[FOCUS_POOL];
        private readonly int[] cloudRows = new int[QUERY_CAPACITY];
        private readonly int[] statusRows = new int[STATUS_BUDGET];
        private readonly int[] shivs = new int[MAX_SHIVS];
        private readonly int shivVisual;
        private readonly int tracer;
        private readonly int drip;
        private readonly int knifeArc;
        private readonly int glint;
        private readonly int knifeCut;
        private readonly int bubble;
        private readonly int venomDrip;
        private readonly int fume;
        private readonly int aura;
        private readonly int glob;
        private readonly int shock;
        private readonly int stain;
        private readonly int splat;
        private readonly FxComposite shivHit;
        private readonly FxComposite primed;
        private readonly FxComposite ruptureBurst;
        private readonly FxComposite chainBurst;
        private readonly FxComposite nightshadeBurst;
        private Enemy focus = Enemy.None;
        private int shivCount;
        private float dripTimer;
        private float statusTimer;

        public VenomShivs(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            ledger = new VenomLedger(enemies);
            ruptures = new VenomRuptures(this, context, ledger);
            clouds = new ToxicClouds(effects, PoisonCloud);
            shivVisual = context.FindVisual("ThrownKnife");
            tracer = effects.Find("RogueWidowTracer");
            drip = effects.Find("RogueShivDrip");
            knifeArc = effects.Find("RogueKnifeArc");
            glint = effects.Find("RogueGlint");
            knifeCut = effects.Find("RogueKnifeCut");
            bubble = effects.Find("RogueVenomBubble");
            venomDrip = effects.Find("RogueVenomDrip");
            fume = effects.Find("RogueVenomFume");
            aura = effects.Find("RogueVenomAura");
            glob = effects.Find("RogueRuptureGlob");
            shock = effects.Find("RogueShadowShock");
            stain = effects.Find("RogueVenomStain");
            splat = effects.Find("RogueRuptureSplat");
            shivHit = Composite(new FxLayer("RogueVenomSplat", 1, 0.55f), new FxLayer("RogueVenomDrops", 3, 0.0f, false));
            primed = Composite(new FxLayer("RogueVenomRing", 1, 1.0f), new FxLayer("RogueVenomBubble", 5));
            ruptureBurst = Composite(new FxLayer("RogueFlash", 1, 1.4f), new FxLayer("RogueRuptureInk", 1, 2.3f, false), new FxLayer("RogueRuptureSplash", 1, 1.6f),
                                     new FxLayer("RogueRuptureDroplets", 14), new FxLayer("RogueVenomBubble", 6), new FxLayer("RogueRuptureFume", 3, 0.0f, false));
            chainBurst = Composite(new FxLayer("RogueRuptureInk", 1, 1.8f, false), new FxLayer("RogueRuptureSplash", 1, 1.3f),
                                   new FxLayer("RogueRuptureDroplets", 8),
                                   new FxLayer("RogueVenomBubble", 3), new FxLayer("RogueRuptureFume", 2, 0.0f, false));
            nightshadeBurst = Composite(new FxLayer("RogueNightshadeBloom", 1, 1.7f), new FxLayer("RogueMarkMotes", 8));
        }

        /// <summary>The most stacks one enemy carries; no cap when the data says so (Stacks below 0).</summary>
        private float stackCap => level[WeaponField.Stacks] < 0.0f ? NO_STACK_CAP : level[WeaponField.Stacks];

        /// <summary>Stacks an enemy needs for a Rogue crit to rupture it.</summary>
        private float primedStacks => level[WeaponField.Every];

        private Color accent => isEvolved ? RogueLooks.NIGHTSHADE : RogueLooks.VENOM;

        private VenomRules rules => new VenomRules
        {
            primedStacks = primedStacks,
            multiplier = level[WeaponField.Multiplier],
            stackCap = stackCap,
            splashRadius = AreaOf(level[WeaponField.SubRadius]),
            canChain = isEvolved,
        };

        // -------------------------------------------------------------------------------------------------------
        // Shivs
        // -------------------------------------------------------------------------------------------------------

        protected override bool Cast(in CastInfo cast)
        {
            if (!RefreshFocus(level[WeaponField.Range]))
            {
                return false;
            }
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_VOLLEY);
            for (int k = 0; k < count; k++)
            {
                float spread = (k - (count - 1) * 0.5f) * SHIV_SPREAD;
                Schedule(SHIV_STAGGER * k, ThrowEvent(focus, spread, cast));
            }
            RequestAttack("Throw");
            return true;
        }

        /// <summary>One shiv of a volley at <paramref name="target"/>, turned <paramref name="spreadDegrees"/> off the line to it.</summary>
        private static WeaponEvent ThrowEvent(Enemy target, float spreadDegrees, in CastInfo cast)
        {
            return new WeaponEvent { kind = THROW, target = target, value = spreadDegrees, cast = cast };
        }

        /// <summary>Keeps the focus while it lives within range below the cap; else the most poisoned nearby enemy below it.</summary>
        private bool RefreshFocus(float range)
        {
            float cap = MathF.Min(stackCap, FOCUS_STACKS_MAX);
            int row = enemies.GetRow(focus);
            if (row >= 0 && enemies.IsTargetable(row) && Ground.Distance(heroPosition, enemies.GetPosition(row)) <= range &&
                enemies.GetVenomStacks(row) < cap)
            {
                return true;
            }
            int found = FindNearest(range, nearest, FOCUS_POOL);
            if (found == 0)
            {
                focus = Enemy.None;
                return false;
            }
            int start = Random.Range(0, found);
            int best = nearest[start];
            float bestStacks = -1.0f;
            for (int k = 0; k < found; k++)
            {
                int candidate = nearest[(start + k) % found];
                float stacks = enemies.GetVenomStacks(candidate);
                if (stacks < cap && stacks > bestStacks)
                {
                    best = candidate;
                    bestStacks = stacks;
                }
            }
            focus = enemies.GetEnemy(best);
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind != THROW)
            {
                return;
            }
            int row = enemies.GetRow(e.target);
            if (row < 0 && !TryRandomInView(level[WeaponField.Range], out row))
            {
                return;
            }
            Throw(enemies.GetPosition(row), e.value, e.cast);
        }

        private void Throw(Vector3 target, float spread, in CastInfo cast)
        {
            Vector3 direction = Ground.Rotate(Ground.Direction(heroPosition, target, facing), spread);
            Vector3 origin = heroPosition + direction * LAUNCH_FORWARD + Ground.Up(LAUNCH_HEIGHT);
            float speed = SpeedOf(level[WeaponField.Speed]);
            float flight = level[WeaponField.Range] * FLIGHT_MARGIN / speed;
            ProjectileSpawn spawn = NewProjectile(shivVisual, ProjectileMotion.Linear, origin, direction * speed, flight);
            spawn.damage = DamageOf(WeaponField.Damage, cast);
            spawn.radius = SHIV_RADIUS;
            spawn.pierce = level.GetCount(WeaponField.Pierce);
            spawn.scale = SHIV_SCALE;
            spawn.handler = this;
            spawn.headTint = isEvolved ? RogueLooks.NIGHTSHADE : RogueLooks.SHIV_GLINT;
            TrackShiv(Spawn(spawn));
            float reach = Ground.Distance(origin, target);
            float tracerSeconds = spawn.pierce > 0 ? flight : MathF.Min(flight, (reach + TRACER_OVERSHOOT) / speed);
            effects.EmitMoving(tracer, origin, spawn.velocity, 1, RogueLooks.VENOM, TRACER_WIDTH * fxScale, tracerSeconds);
            effects.EmitMoving(knifeArc, origin + direction * ARC_OUT, direction * (speed * ARC_SPEED_SHARE), 1, accent, ARC_SIZE * fxScale, ARC_SECONDS);
            EmitSized(glint, origin, RogueLooks.SHIV_GLINT, GLINT_SIZE * fxScale);
            PlayAt("knife_throw", origin, THROW_VOLUME, THROW_PITCH);
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn shiv = ref world.Get(projectile);
            Vector3 impact = shiv.position;
            float before = enemies.GetVenomStacks(enemyRow);
            // The stack lands first, so the shiv's own crit counts it.
            Poison(enemyRow, 1.0f, stackCap);
            float after = enemies.GetVenomStacks(enemyRow);
            Vector3 chest = enemies.GetPosition(enemyRow) + Ground.Up(enemies.GetHeight(enemyRow) * CHEST_SHARE) + TOWARD_CAMERA * STATUS_TOWARD_CAMERA;
            if (HitAlongFlight(enemyRow, shiv, WithAutoCrit(default)) == DamageResult.Missed)
            {
                return;
            }
            RogueLooks.EmitKnifeCut(effects, knifeCut, impact, FlightOf(shiv), accent, CUT_SIZE * fxScale);
            shivHit.Play(impact, FxSizes.METRE_UNIT, fxScale, RogueLooks.VENOM);
            if (before < primedStacks && after >= primedStacks)
            {
                primed.Play(chest, FxSizes.METRE_UNIT, fxScale, RogueLooks.VENOM);
            }
        }

        private void TrackShiv(int row)
        {
            if (row < 0 || shivCount >= MAX_SHIVS)
            {
                return;
            }
            for (int i = 0; i < shivCount; i++)
            {
                if (shivs[i] == row)
                {
                    return;
                }
            }
            shivs[shivCount++] = row;
        }

        // -------------------------------------------------------------------------------------------------------
        // Venom and ruptures
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Adds venom stacks up to a cap (Might on the per-stack damage, Duration on the seconds) and notes it.</summary>
        internal void Poison(int row, float stacks, float maxStacks)
        {
            float perStack = MightOf(level[WeaponField.SubDamage]);
            float seconds = DurationOf(level[WeaponField.Duration]);
            enemies.AddVenom(row, stacks, perStack, seconds, maxStacks, slot);
            if (enemies.IsTargetable(row))
            {
                ledger.Record(row, perStack, seconds, clock);
            }
        }

        /// <summary>A Rogue class weapon's crit on a primed enemy queues its rupture. Runs inside the damage call.</summary>
        public override void OnEnemyDamaged(int row, int source, float damage, bool isCritical)
        {
            if (isCritical && IsRogueWeapon(source))
            {
                ruptures.QueueTrigger(row, clock, rules);
            }
        }

        /// <summary>Whether a hit source is a slot holding a Rogue class weapon.</summary>
        private bool IsRogueWeapon(int source)
        {
            Weapon weapon = ctx.loadout.GetWeapon(source);
            return weapon != null && weapon.def.IsClassWeaponOf(HeroClass.Rogue);
        }

        protected override void Update(float dt)
        {
            ruptures.Resolve(clock, rules);
            clouds.Tick(dt);
            ShowShivs(dt);
            ShowPoisoned(dt);
        }

        /// <summary>Nightshade Plague: a rupture leaves a toxic cloud where it burst.</summary>
        internal void LeaveCloud(Vector3 position)
        {
            if (isEvolved)
            {
                clouds.Add(position, AreaOf(level[WeaponField.SubRadius]) * CLOUD_RADIUS_SHARE, DurationOf(level[WeaponField.SubDuration]));
            }
        }

        /// <summary>Nightshade's cloud tick: a venom stack and a slow for everything inside.</summary>
        private void PoisonCloud(Vector3 center, float radius)
        {
            int found = enemies.QueryCircle(center, radius, cloudRows);
            for (int i = 0; i < found; i++)
            {
                int row = cloudRows[i];
                Poison(row, CLOUD_STACKS, stackCap);
                enemies.Slow(row, CLOUD_SLOW_FACTOR, CLOUD_SLOW_SECONDS);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Looks
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The green-black splash of a rupture, sized by its stacks; a chain rupture is lighter (no flash, no shockwave).
        /// Nightshade blooms violet over it.
        /// </summary>
        internal void ShowRupture(Vector3 position, float height, float stacks, bool isTrigger)
        {
            float scale = Math.Clamp(RUPTURE_SCALE_BASE + RUPTURE_SCALE_PER_STACK * stacks, RUPTURE_SCALE_BASE, RUPTURE_SCALE_MAX);
            Vector3 body = position + Ground.Up(height * BURST_HEIGHT_SHARE) + TOWARD_CAMERA;
            Vector3 ground = Ground.Flat(position) + Ground.Up(FxSizes.GROUND_LIFT);
            (isTrigger ? ruptureBurst : chainBurst).Play(body, scale, fxScale, RogueLooks.VENOM);
            EmitShape(stain, ground, WHITE, STAIN_SIZE * scale, STAIN_SECONDS);
            EmitSized(splat, ground, RogueLooks.VENOM, (isTrigger ? SPLAT_SIZE : CHAIN_SPLAT_SIZE) * scale);
            if (isTrigger)
            {
                EmitSized(shock, ground, WithAlpha(RogueLooks.VENOM, SHOCK_ALPHA), AreaOf(level[WeaponField.SubRadius]) * 2.0f);
            }
            if (isEvolved)
            {
                nightshadeBurst.Play(body, isTrigger ? scale : scale * CHAIN_BLOOM_SHARE, isTrigger ? fxScale : 1.0f, RogueLooks.NIGHTSHADE);
            }
            PlayAt("wisp_splash", position, isTrigger ? RUPTURE_VOLUME : CHAIN_VOLUME, RUPTURE_PITCH);
        }

        /// <summary>A venom glob arcing from a rupture onto a neighbour (its trail draws the arc).</summary>
        internal void ShowGlob(Vector3 from, Vector3 to)
        {
            Vector3 start = from + Ground.Up(GLOB_START_HEIGHT);
            Vector3 end = to + Ground.Up(GLOB_END_HEIGHT);
            Vector3 velocity = (end - start) * (1.0f / GLOB_SECONDS) + Ground.Up(0.5f * GLOB_GRAVITY * GLOB_SECONDS);
            effects.EmitMoving(glob, start, velocity, 1, accent, GLOB_WIDTH * fxScale, GLOB_SECONDS);
        }

        /// <summary>Venom drips falling off every live shiv.</summary>
        private void ShowShivs(float dt)
        {
            dripTimer -= dt;
            bool isDue = dripTimer <= 0.0f;
            if (isDue)
            {
                dripTimer = MathF.Max(dripTimer + DRIP_INTERVAL, 0.0f);
            }
            Color color = accent;
            int i = 0;
            while (i < shivCount)
            {
                int row = shivs[i];
                if (!IsOwned(row, this, shivVisual))
                {
                    shivs[i] = shivs[--shivCount];
                    continue;
                }
                if (isDue)
                {
                    EmitSized(drip, projectiles.Get(row).position, color, DRIP_SIZE * fxScale);
                }
                i++;
            }
        }

        /// <summary>Poisoned enemies bubble and drip green; primed ones (a rupture away) also smoke with sickly fumes.</summary>
        private void ShowPoisoned(float dt)
        {
            statusTimer -= dt;
            if (statusTimer > 0.0f)
            {
                return;
            }
            statusTimer = STATUS_INTERVAL;
            int found = ledger.CollectPoisoned(statusRows);
            for (int i = 0; i < found; i++)
            {
                int row = statusRows[i];
                Vector3 position = enemies.GetPosition(row);
                float height = enemies.GetHeight(row);
                Vector3 chest = position + Ground.Up(height * CHEST_SHARE) + TOWARD_CAMERA * STATUS_TOWARD_CAMERA;
                bool isPrimed = enemies.GetVenomStacks(row) >= primedStacks;
                effects.EmitSized(aura, chest, 1, RogueLooks.VENOM, height * (isPrimed ? PRIMED_AURA_SIZE_SHARE : AURA_SIZE_SHARE));
                effects.Emit(bubble, chest, isPrimed ? PRIMED_BUBBLES : BUBBLES, RogueLooks.VENOM);
                effects.Emit(venomDrip, chest, 1, RogueLooks.VENOM);
                if (isPrimed)
                {
                    effects.EmitSized(fume, position + Ground.Up(height * FUME_HEIGHT_SHARE), 1, WHITE, height * FUME_SIZE_SHARE);
                }
            }
        }
    }
}

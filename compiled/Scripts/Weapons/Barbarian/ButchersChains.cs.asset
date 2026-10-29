using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>What a Butcher's Chains chain is doing.</summary>
    internal enum ChainPhase : byte
    {
        Idle,
        /// <summary>Flying out to its range.</summary>
        Out,
        /// <summary>Snapping back to the hand after the bite (the yank).</summary>
        Back,
        /// <summary>Maw of the Pit: reeling in with the vortex until the slam.</summary>
        Reel,
    }

    /// <summary>One chain of Butcher's Chains: where it flies and how far its hook is from the hand.</summary>
    internal struct ChainState
    {
        public ChainPhase phase;
        public Vector3 direction;
        public float range;
        public float speed;
        /// <summary>Game time the phase started; a chain waiting for its turn in the volley starts later.</summary>
        public float start;
        /// <summary>The hook's distance from the hand now.</summary>
        public float tip;
        /// <summary>Which way the whip bows: +1 or -1.</summary>
        public float side;
        public CastInfo cast;
    }

    /// <summary>
    /// Butcher's Chains (Barbarian, Rare): hooked chains (Count + Amount, at most 10) fly from the barbarian toward the
    /// densest clusters within Range (spares fan out beside the first) at Speed (Projectile Speed). When a hook reaches
    /// its range it bites and its chain yanks everything along it (Width wide) toward the barbarian: Pull metres at
    /// most and never closer than 1.4 m, into the reach of his axes. Each enemy is yanked once per throw: it is
    /// bloodied for SubDuration seconds (Duration stat), takes Damage and, if it lives, is exposed (+Bonus damage
    /// taken) for as long. An enemy that dies bloodied feeds Bloodlust: BarbarianKit grants Fury (this weapon's Fury) on
    /// top of the kill's.
    /// Evolution Maw of the Pit (+ Lodestone Charm): the hooks fly out in a star to the Radius and bite (Damage,
    /// bloodied); a blood vortex then drags every enemy within the Radius to the barbarian over MajorDelay seconds while
    /// the chains reel in, and the chains slam down: MajorDamage in MajorRadius (Area), throwing the pile back; every
    /// enemy within the Radius is bloodied and exposed.
    /// Look: see <see cref="ChainFx"/>.
    /// </summary>
    public sealed class ButchersChains : Weapon
    {
        /// <summary>
        /// The enemy world's knockback damping: a push of this many m/s carries an enemy exactly one metre. The yank and
        /// the drag are sized with it, not with Tuning.KNOCKBACK_SPEED_PER_METRE (a rounder number for weapon data), so a
        /// yanked enemy stops right at the standoff.
        /// </summary>
        internal const float ENEMY_KNOCKBACK_DAMPING = EnemyWorld.KNOCKBACK_DAMPING;
        private const int SLAM = 0;
        private const int MAX_CHAINS = 10;
        private const int SCRATCH_CAPACITY = 1024;
        /// <summary>The chains of one throw leave this far apart (s).</summary>
        private const float VOLLEY_STAGGER = 0.05f;
        /// <summary>Two chains aimed at clusters are at least this far apart (degrees); spares fan out by the step.</summary>
        private const float MIN_SPREAD = 22.0f;
        private const float FAN_STEP = 26.0f;
        /// <summary>A yank stops this far from the barbarian: inside the reach of his chop and his spin.</summary>
        private const float STANDOFF = 1.4f;
        private const float MIN_KNOCKBACK_STAT = 0.1f;
        private const float YANK_HOP = 0.3f;
        /// <summary>The chain reels in as fast as the yanked enemies slow down: the hook at range x e^(-rate x t).</summary>
        private const float RETRACT_RATE = ENEMY_KNOCKBACK_DAMPING;
        /// <summary>The chain is caught when its hook is this close to the hand (m).</summary>
        private const float RETRACT_END = 0.35f;
        private const int HIT_STOP_TARGETS = 6;
        private const float HIT_STOP_SECONDS = 0.04f;
        private const float YANK_TRAUMA = 0.1f;
        private const float VORTEX_TICK = 0.05f;
        /// <summary>The vortex's rim speed per metre of its reach per second of drag (with Pull's falloff, the rim arrives in time).</summary>
        private const float VORTEX_SPEED_PER_METRE = 1.3f;
        private const float VORTEX_DECAL_INTERVAL = 0.25f;
        private const float MIN_DRAG_SECONDS = 0.1f;
        private const float MIN_SPEED = 1.0f;
        private const float SLAM_KNOCKBACK = 4.0f;
        private const float SLAM_TRAUMA = 0.32f;
        private const float SLAM_HIT_STOP = 0.07f;
        private const int MAX_BLEEDERS = 128;
        private const float DRIP_INTERVAL_MIN = 0.22f;
        private const float DRIP_INTERVAL_MAX = 0.4f;
        /// <summary>At most this many bloodied deaths per frame show their burst and Fury streak (late-game kill rates).</summary>
        private const int MAX_MOTES_PER_FRAME = 6;
        /// <summary>At most this many yanked enemies per bite show their drag (dust, furrow, blood).</summary>
        private const int MAX_YANK_LOOKS = 24;
        /// <summary>Per-frame links live this many frames: each one renders once, so moving chains never smear.</summary>
        private const float LINK_FRAMES = 1.6f;
        private const float LINK_SECONDS_MIN = 0.002f;
        private const float LINK_SECONDS_MAX = 0.05f;
        private const float THROW_VOLUME = 0.9f;
        private const float RATTLE_VOLUME = 0.55f;
        private const float RATTLE_PITCH = 1.2f;
        private const float DRAG_VOLUME = 0.7f;
        private const float DRAG_PITCH = 1.45f;
        private const float SNAP_VOLUME = 0.6f;
        private const float SNAP_PITCH = 0.75f;
        private const float CATCH_VOLUME = 0.5f;
        private const float CATCH_PITCH = 0.8f;

        private static readonly Vector3 CHEST = new Vector3(0.0f, 1.1f, 0.0f);

        private readonly ChainState[] chains = new ChainState[MAX_CHAINS];
        private readonly Vector3[] aims = new Vector3[MAX_CHAINS];
        private readonly int[] scratch = new int[SCRATCH_CAPACITY];
        private readonly int[] bleedRows = new int[MAX_BLEEDERS];
        /// <summary>Enemies already yanked by this throw.</summary>
        private readonly HashSet<Enemy> memory = new HashSet<Enemy>();
        /// <summary>Bloodied enemies that drip until their blood dries or they die, and when each drips next.</summary>
        private readonly TrackedEnemies bleeders;
        private readonly EnemyTable<float> nextDrips = new EnemyTable<float>(0.0f);
        /// <summary>The Barbarian's kit (Fury), or null for another class.</summary>
        private readonly BarbarianKit barbarian;
        private readonly ChainFx fx;
        private int chainCount;
        private bool hasBitten;
        private bool hasCaught;
        private float vortexStart = -1.0f;
        private float vortexEnd = -1.0f;
        /// <summary>How long the maw drags (and the hooks reel in) before the slam.</summary>
        private float dragSeconds = MIN_DRAG_SECONDS;
        private float vortexTimer;
        private float vortexDecalTimer;
        private int motesThisFrame;
        private float linkSeconds = LINK_SECONDS_MAX;

        public ButchersChains(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            barbarian = context.KitAs<BarbarianKit>();
            bleeders = new TrackedEnemies(enemies, MAX_BLEEDERS, enemies.IsBloodied);
            fx = new ChainFx(effects, enemies);
        }

        protected override void OnLevelChanged()
        {
            fx.SetLook(fxScale, isEvolved);
            if (barbarian != null)
            {
                barbarian.bloodiedKillFury = level[WeaponField.Fury];
            }
        }

        /// <summary>Without the chains, bloodied deaths grant no extra Fury.</summary>
        protected override void OnDispose()
        {
            if (barbarian != null)
            {
                barbarian.bloodiedKillFury = 0.0f;
            }
        }

        protected override bool Cast(in CastInfo cast) => isEvolved ? ThrowStar(cast) : ThrowVolley(cast);

        /// <summary>A volley at the densest clusters within range.</summary>
        private bool ThrowVolley(in CastInfo cast)
        {
            int count = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_CHAINS);
            float range = level[WeaponField.Range];
            int aimed = AimAtClusters(count, range);
            if (aimed == 0)
            {
                return false;
            }
            StartThrow(aimed, range, VOLLEY_STAGGER, cast);
            return true;
        }

        /// <summary>Maw of the Pit: a star of hooks led by the densest cluster, then the vortex and the slam.</summary>
        private bool ThrowStar(in CastInfo cast)
        {
            float reach = level[WeaponField.Radius];
            if (FindClusters(reach) == 0)
            {
                return false;
            }
            int count = Math.Clamp(level.GetCount(WeaponField.Count) + amount, 1, MAX_CHAINS);
            Vector3 first = Ground.Direction(heroPosition, ctx.clusters.GetPoint(0), facing);
            for (int i = 0; i < count; i++)
            {
                aims[i] = Ground.Rotate(first, Ground.FULL_TURN * i / count);
            }
            StartThrow(count, reach, 0.0f, cast);
            float flight = reach / MathF.Max(SpeedOf(level[WeaponField.Speed]), MIN_SPEED);
            dragSeconds = MathF.Max(level[WeaponField.MajorDelay], MIN_DRAG_SECONDS);
            vortexStart = clock + flight;
            vortexEnd = vortexStart + dragSeconds;
            vortexTimer = 0.0f;
            vortexDecalTimer = 0.0f;
            Schedule(flight + dragSeconds, new WeaponEvent { kind = SLAM, cast = cast });
            return true;
        }

        /// <summary>
        /// Aims chains at the densest cells within range, skipping cells too close in angle to a chain already aimed;
        /// spares fan out to both sides of the first. Returns how many were aimed (0 when no enemy is in range).
        /// </summary>
        private int AimAtClusters(int count, float range)
        {
            int cells = FindClusters(range);
            if (cells == 0)
            {
                return 0;
            }
            Vector3 origin = heroPosition;
            float minDot = MathF.Cos(MIN_SPREAD * Ground.DEGREES_TO_RADIANS);
            int aimed = 0;
            for (int cell = 0; cell < cells && aimed < count; cell++)
            {
                Vector3 direction = Ground.Direction(origin, ctx.clusters.GetPoint(cell), facing);
                if (IsApart(direction, aimed, minDot))
                {
                    aims[aimed++] = direction;
                }
            }
            for (int spare = 1; aimed < count; spare++)
            {
                aims[aimed++] = Ground.Fan(aims[0], spare, FAN_STEP);
            }
            return aimed;
        }

        private bool IsApart(Vector3 direction, int aimed, float minDot)
        {
            for (int k = 0; k < aimed; k++)
            {
                if (Vector3.Dot(direction, aims[k]) > minDot)
                {
                    return false;
                }
            }
            return true;
        }

        private void StartThrow(int count, float range, float stagger, in CastInfo cast)
        {
            memory.Clear();
            hasBitten = false;
            hasCaught = false;
            float speed = SpeedOf(level[WeaponField.Speed]);
            for (int i = 0; i < count; i++)
            {
                chains[i] = new ChainState
                {
                    phase = ChainPhase.Out,
                    direction = aims[i],
                    range = range,
                    speed = speed,
                    start = clock + stagger * i,
                    side = i % 2 == 0 ? 1.0f : -1.0f,
                    cast = cast,
                };
            }
            for (int i = count; i < chainCount; i++)
            {
                chains[i].phase = ChainPhase.Idle;
            }
            chainCount = count;
            Vector3 origin = heroPosition;
            fx.DrawThrow(origin, aims[0]);
            RequestAttack("Throw");
            PlayAt("javelin_throw", origin, THROW_VOLUME);
            PlayAt("warden_guard_up", origin, RATTLE_VOLUME, RATTLE_PITCH);
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == SLAM)
            {
                Slam(e.cast);
            }
        }

        protected override void Update(float dt)
        {
            motesThisFrame = 0;
            linkSeconds = Math.Clamp(dt * LINK_FRAMES, LINK_SECONDS_MIN, LINK_SECONDS_MAX);
            StepChains();
            StepVortex(dt);
            DripBleeders();
        }

        /// <summary>A bloodied death bursts in blood and sends a red Fury streak into the barbarian (the kit grants the Fury).</summary>
        public override void OnEnemyKilled(in EnemyDeath death)
        {
            if (!death.wasBloodied || motesThisFrame >= MAX_MOTES_PER_FRAME)
            {
                return;
            }
            motesThisFrame++;
            fx.DrawBloodiedDeath(death.position, heroPosition + CHEST);
        }

        // -------------------------------------------------------------------------------------------------------
        // Chains
        // -------------------------------------------------------------------------------------------------------

        private void StepChains()
        {
            Vector3 origin = heroPosition;
            for (int i = 0; i < chainCount; i++)
            {
                ref ChainState chain = ref chains[i];
                if (chain.phase == ChainPhase.Idle || clock < chain.start)
                {
                    continue;
                }
                float elapsed = clock - chain.start;
                if (chain.phase == ChainPhase.Out)
                {
                    chain.tip = MathF.Min(elapsed * chain.speed, chain.range);
                    if (chain.tip >= chain.range)
                    {
                        Bite(ref chain, origin);
                        elapsed = 0.0f;
                    }
                }
                else if (chain.phase == ChainPhase.Back)
                {
                    chain.tip = chain.range * MathF.Exp(-RETRACT_RATE * elapsed);
                    if (chain.tip <= RETRACT_END)
                    {
                        Catch(ref chain, origin);
                        continue;
                    }
                }
                else
                {
                    float share = Math.Clamp(elapsed / dragSeconds, 0.0f, 1.0f);
                    chain.tip = Mathf.Lerp(chain.range, STANDOFF, share * share * (3.0f - 2.0f * share));
                }
                fx.DrawChain(origin, chain, elapsed, linkSeconds);
            }
        }

        /// <summary>The hook bites at its range: the chain yanks (or, for the Maw, hooks) everything along it.</summary>
        private void Bite(ref ChainState chain, Vector3 origin)
        {
            Vector3 from = Ground.Flat(origin);
            Vector3 end = from + chain.direction * chain.range;
            int hits = Rake(from, end, chain.direction, chain.cast);
            chain.phase = isEvolved ? ChainPhase.Reel : ChainPhase.Back;
            chain.start = clock;
            fx.DrawBite(end, chain.direction);
            if (!hasBitten)
            {
                hasBitten = true;
                RequestAttack("HeavySlice");
                PlayAt("king_cleave_sting", origin, DRAG_VOLUME, DRAG_PITCH);
                PlayAt("crossbow_twang", origin, SNAP_VOLUME, SNAP_PITCH);
                ctx.feel.Shake(YANK_TRAUMA);
            }
            if (hits > 0)
            {
                PlayAt("ravager_hit", end);
            }
            TryHitStop(hits, HIT_STOP_TARGETS, HIT_STOP_SECONDS);
        }

        /// <summary>
        /// Every enemy along the chain not yet raked by this throw is bloodied and hit; a volley chain also yanks it
        /// toward the barbarian (Pull metres at most, stopping at the standoff), sized so the Knockback stat does not
        /// stretch it. Returns how many were hit.
        /// </summary>
        private int Rake(Vector3 origin, Vector3 end, Vector3 direction, in CastInfo cast)
        {
            float halfWidth = level[WeaponField.Width] * 0.5f;
            float pull = isEvolved ? 0.0f : level[WeaponField.Pull];
            float seconds = DurationOf(level[WeaponField.SubDuration]);
            float damage = DamageOf(WeaponField.Damage, cast);
            float pushPerMetre = ENEMY_KNOCKBACK_DAMPING / MathF.Max(stats[StatId.Knockback], MIN_KNOCKBACK_STAT);
            int found = enemies.QueryCapsule(origin, end, halfWidth, scratch);
            int hits = 0;
            for (int i = 0; i < found; i++)
            {
                int row = scratch[i];
                if (!memory.Add(enemies.GetEnemy(row)))
                {
                    continue;
                }
                Vector3 position = enemies.GetPosition(row);
                float travel = Math.Clamp(Ground.Distance(origin, position) - STANDOFF, 0.0f, pull);
                Vector3 toward = Ground.Direction(position, origin, -direction);
                float height = enemies.GetHeight(row);
                if (Bleed(row, damage, toward * (travel * pushPerMetre), seconds) == DamageResult.Missed)
                {
                    continue;
                }
                if (hits++ < MAX_YANK_LOOKS)
                {
                    fx.DrawYank(position, toward, travel, height);
                }
            }
            return hits;
        }

        /// <summary>Bloodies an enemy, then hits it; a survivor is exposed for as long, hops and drips while bloodied.</summary>
        private DamageResult Bleed(int row, float damage, Vector3 push, float seconds)
        {
            enemies.Bloody(row, seconds);
            DamageResult result = strikes.Hit(row, damage, push, slot);
            if (result == DamageResult.Hurt)
            {
                enemies.Expose(row, level[WeaponField.Bonus], seconds);
                enemies.Launch(row, YANK_HOP);
                TrackBleeder(row);
            }
            return result;
        }

        /// <summary>The chain is back in his hand.</summary>
        private void Catch(ref ChainState chain, Vector3 origin)
        {
            chain.phase = ChainPhase.Idle;
            fx.DrawCatch(origin, chain.direction);
            if (!hasCaught)
            {
                hasCaught = true;
                PlayAt("guard_tink", origin, CATCH_VOLUME, CATCH_PITCH);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Maw of the Pit
        // -------------------------------------------------------------------------------------------------------

        /// <summary>While the maw is open: everything within the reach is dragged in, every 0.05 s.</summary>
        private void StepVortex(float dt)
        {
            if (vortexStart < 0.0f || clock < vortexStart)
            {
                return;
            }
            if (clock >= vortexEnd)
            {
                vortexStart = -1.0f;
                return;
            }
            float reach = level[WeaponField.Radius];
            vortexDecalTimer -= dt;
            if (vortexDecalTimer <= 0.0f)
            {
                vortexDecalTimer += VORTEX_DECAL_INTERVAL;
                fx.DrawMaw(heroPosition, reach);
            }
            vortexTimer -= dt;
            if (vortexTimer > 0.0f)
            {
                return;
            }
            vortexTimer += VORTEX_TICK;
            Vector3 center = heroPosition;
            float rimSpeed = reach * VORTEX_SPEED_PER_METRE / dragSeconds;
            int found = enemies.Pull(center, reach, rimSpeed * ENEMY_KNOCKBACK_DAMPING * VORTEX_TICK, scratch);
            fx.DrawDrag(center, reach, scratch.AsSpan(0, found), chains.AsSpan(0, chainCount));
        }

        /// <summary>The chains slam: everyone within the reach is bloodied, the pile in the slam radius is hit and thrown back.</summary>
        private void Slam(in CastInfo cast)
        {
            Vector3 center = heroPosition;
            float reach = level[WeaponField.Radius];
            float radius = AreaOf(level[WeaponField.MajorRadius]);
            float seconds = DurationOf(level[WeaponField.SubDuration]);
            int dragged = enemies.QueryCircle(center, reach, scratch);
            for (int i = 0; i < dragged; i++)
            {
                enemies.Bloody(scratch[i], seconds);
            }
            int hits = strikes.Circle(center, radius, MightOf(level[WeaponField.MajorDamage], cast), KnockbackOf(SLAM_KNOCKBACK), slot);
            float bonus = level[WeaponField.Bonus];
            for (int i = 0; i < dragged; i++)
            {
                int row = scratch[i];
                if (enemies.IsTargetable(row))
                {
                    enemies.Expose(row, bonus, seconds);
                    TrackBleeder(row);
                }
            }
            vortexStart = -1.0f;
            fx.DrawSlam(center, radius, chains.AsSpan(0, chainCount));
            for (int i = 0; i < chainCount; i++)
            {
                chains[i].phase = ChainPhase.Idle;
            }
            RequestAttack("HeavyChop");
            PlayAt("leap_land", center);
            if (hits > 0)
            {
                PlayAt("ravager_hit", center);
            }
            ctx.feel.Shake(SLAM_TRAUMA);
            ctx.feel.HitStop(SLAM_HIT_STOP);
        }

        // -------------------------------------------------------------------------------------------------------
        // Bleeding
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Starts an enemy dripping (a full list replaces an older entry); its first drop comes within one interval.</summary>
        private void TrackBleeder(int row)
        {
            Enemy enemy = enemies.GetEnemy(row);
            bleeders.Add(enemy);
            nextDrips.Get(enemy) = clock + Random.Range(0.0f, DRIP_INTERVAL_MAX);
        }

        /// <summary>Every bloodied enemy drips now and then until its blood dries or it dies.</summary>
        private void DripBleeders()
        {
            int found = bleeders.Collect(bleedRows);
            for (int i = 0; i < found; i++)
            {
                int row = bleedRows[i];
                ref float nextDrip = ref nextDrips.Get(enemies.GetEnemy(row));
                if (clock < nextDrip)
                {
                    continue;
                }
                nextDrip = clock + Random.Range(DRIP_INTERVAL_MIN, DRIP_INTERVAL_MAX);
                fx.DrawDrip(enemies.GetPosition(row), enemies.GetHeight(row));
            }
        }
    }
}

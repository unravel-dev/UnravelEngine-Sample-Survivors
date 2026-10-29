using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// A class's active ability and signature passive. The base runs the active's cooldown (the Cooldown stat at half
    /// strength), refuses to fire while the hero is dead or already dashing, and chimes when the active is ready again.
    /// Its hooks let a class change how its own class weapons fire and hit (Arcane Echo, Smoke Roll's critical hits)
    /// without the weapon base knowing any class. Built by the class's <see cref="HeroClassDef.createKit"/>. Main thread.
    /// </summary>
    public abstract class ClassKit
    {
        /// <summary>Floor of the cooldown the HUD meter divides by.</summary>
        private const float MIN_ACTIVE_COOLDOWN = 1e-3f;
        /// <summary>A last move direction shorter than this counts as none: dashes then go where the hero faces.</summary>
        private const float MIN_MOVE_SQUARED = 1e-4f;

        /// <summary>The class whose weapons the hooks apply to.</summary>
        public readonly HeroClass heroClass;
        protected readonly WeaponContext ctx;
        protected readonly HeroController hero;
        protected readonly PlayerStats stats;
        protected readonly Effects effects;
        protected readonly Sounds sounds;
        protected readonly Strikes strikes;
        private float cooldownLeft;

        /// <summary>Raised after the active fires (Quickening Hourglass's Overdrive).</summary>
        public event Action activated;

        protected ClassKit(WeaponContext context, HeroClass heroClass)
        {
            this.heroClass = heroClass;
            ctx = context;
            hero = context.hero;
            stats = context.stats;
            effects = context.effects;
            sounds = context.sounds;
            strikes = context.strikes;
        }

        protected abstract float baseActiveCooldown { get; }

        /// <summary>Seconds between uses after the Cooldown stat at half strength.</summary>
        public float activeCooldown => baseActiveCooldown * stats.activeCooldownFactor;

        /// <summary>0 when ready, 1 right after use.</summary>
        public float activeCooldownFraction => cooldownLeft <= 0.0f ? 0.0f : Math.Clamp(cooldownLeft / MathF.Max(activeCooldown, MIN_ACTIVE_COOLDOWN), 0.0f, 1.0f);

        /// <summary>The signature passive's stacks (Fury, Momentum, bonus Armor), 0 when it has none.</summary>
        public virtual int signatureStacks => 0;

        // -------------------------------------------------------------------------------------------------------
        // Class weapon hooks
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Whether a full cast of one of this class's weapons casts again as an echo; rolled once per cast.</summary>
        public virtual bool RollEcho() => false;

        /// <summary>An echo of one of this class's weapons fires now: its cue.</summary>
        public virtual void OnEcho()
        {
        }

        /// <summary>A hit of one of this class's weapons, before it lands: the kit may change it.</summary>
        public virtual void PrepareHit(ref HitEffect effect)
        {
        }

        // -------------------------------------------------------------------------------------------------------
        // Active and signature
        // -------------------------------------------------------------------------------------------------------

        /// <summary>Uses the active if it is ready; returns whether it fired.</summary>
        public bool TryActivate()
        {
            if (cooldownLeft > 0.0f || !hero.isAlive || hero.isDashing || ctx.run.phase != RunPhase.Playing)
            {
                return false;
            }
            if (!Activate())
            {
                return false;
            }
            cooldownLeft = activeCooldown;
            activated?.Invoke();
            return true;
        }

        /// <summary>Dev (effect reviews): the active now, whatever its cooldown.</summary>
        public bool DevActivate()
        {
            cooldownLeft = 0.0f;
            return TryActivate();
        }

        /// <summary>Dev (effect reviews): fills the signature meter (Fury, Momentum) to show its full look.</summary>
        public virtual void DevFillSignature()
        {
        }

        /// <summary>Steps the cooldown and the class's own state; once per frame while playing.</summary>
        public void Tick(float dt)
        {
            if (cooldownLeft > 0.0f)
            {
                cooldownLeft -= dt;
                if (cooldownLeft <= 0.0f)
                {
                    sounds.Play("active_ready");
                }
            }
            Step(dt);
        }

        /// <summary>A kill (runs inside the damage call that killed: only change state here, never damage).</summary>
        public virtual void OnKill(in EnemyDeath death)
        {
        }

        /// <summary>Lets go of hero events when the run ends.</summary>
        public virtual void Dispose()
        {
        }

        /// <summary>Fires the active; returns false when it could not.</summary>
        protected abstract bool Activate();

        /// <summary>The class's own state, every frame.</summary>
        protected virtual void Step(float dt)
        {
        }

        /// <summary>Where a dash goes: the last movement direction, else where the hero faces.</summary>
        protected Vector3 dashDirection
        {
            get
            {
                Vector3 direction = hero.lastMoveDirection;
                return direction.sqrMagnitude > MIN_MOVE_SQUARED ? direction : hero.facingDirection;
            }
        }

        protected int level => ctx.run.level;
    }
}

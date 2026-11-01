using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// A ring of ghost flames that holds the hero: a dash or a blink through it burns the hero and throws them back in.
    /// Stragglers of the horde walk through it. It can close in over time. Around a boss fight it also clears the field as
    /// it rises (the dead inside fall at once and leave their gems, so a boss arrives with a burst of reward), holds the
    /// boss inside and drops a roast now and then: the only way to heal much under Death's Mark is to go and get one (the
    /// King's last stand closes it in; it falls with the last boss). "The dead close in" raises one of its own
    /// (<see cref="DeadRing"/>). Main thread.
    /// </summary>
    public sealed class BarrowWard
    {
        /// <summary>A flame every this many metres of the ring, re-emitted on the interval.</summary>
        private const float FLAME_SPACING = 1.6f;
        private const int MAX_FLAMES = 90;
        private const float FLAME_INTERVAL = 0.14f;
        private const float FLAME_DRIFT_DEGREES = 3.0f;
        private const float FLAME_SIZE = 2.4f;
        private const float RING_INTERVAL = 0.3f;
        private const float RISE_SECONDS = 0.7f;
        private const float HERO_MARGIN = 0.4f;
        /// <summary>A hero past the ring is thrown this far back in over this long, and burned at most once per interval.</summary>
        private const float THROW_METRES = 3.0f;
        private const float THROW_SECONDS = 0.2f;
        private const float BURN_INTERVAL = 1.0f;
        private const float BURN_HEALTH_SHARE = 0.15f;
        private const float BURN_SHAKE = 0.3f;
        private const float BURN_SHAKE_SECONDS = 0.25f;
        private const float ROAST_FIRST_SECONDS = 25.0f;
        private const float ROAST_INTERVAL = 30.0f;
        private const int MAX_ROASTS = 2;
        /// <summary>Roasts land within this share of the radius, away from the flames.</summary>
        private const float ROAST_SPREAD = 0.7f;
        private const float WAVE_SIZE_PER_RADIUS = 2.0f;
        private const float WAVE_SECONDS = 0.6f;
        /// <summary>The rising wave stays faint: it fills the whole ring, so it must not flood the view.</summary>
        private const float WAVE_ALPHA = 0.3f;
        private const float POP_HEIGHT = 0.8f;
        private const int POP_SHARDS = 5;
        private const float SHAKE = 0.5f;
        private const float SHAKE_SECONDS = 0.6f;
        private const int SCRATCH = 2048;
        private const string SOURCE = "The Barrow Ward";

        private readonly Run run;
        private readonly EventFx ringFx;
        private readonly int ring;
        private readonly int boneBurst;
        private readonly int shockwave;
        private readonly int[] scratch = new int[SCRATCH];
        private Vector3 center;
        private Color color;
        private float radius;
        private float targetRadius;
        private float closeSpeed;
        private float age;
        private float flameTimer;
        private float ringTimer;
        private float flameTurn;
        private float burnTimer;
        private float roastTimer;
        private bool isBossFight;

        public bool isUp { get; private set; }

        public BarrowWard(Run run)
        {
            this.run = run;
            ringFx = new EventFx(run.effects);
            ring = run.effects.Find("ArenaRing");
            boneBurst = run.effects.Find(BossFx.BONE_BURST);
            shockwave = run.effects.Find(BossFx.PHASE_SHOCKWAVE);
        }

        /// <summary>Raises the ward around a point in a colour; for a boss fight it kills the regular dead inside it.</summary>
        public void Raise(Vector3 at, float ringRadius, Color ringColor, bool forBoss)
        {
            isUp = true;
            isBossFight = forBoss;
            center = Ground.Flat(at);
            color = ringColor;
            radius = ringRadius;
            targetRadius = ringRadius;
            closeSpeed = 0.0f;
            age = 0.0f;
            burnTimer = 0.0f;
            roastTimer = ROAST_FIRST_SECONDS;
            if (forBoss)
            {
                ClearField();
            }
            var wave = new Color(ringColor.r, ringColor.g, ringColor.b, WAVE_ALPHA);
            run.effects.Emit(shockwave, center + Ground.Up(Tuning.TELEGRAPH_HEIGHT), 1, wave, ringRadius * WAVE_SIZE_PER_RADIUS, WAVE_SECONDS);
            run.sounds.PlayAt("phase_shockwave", center);
            run.feedback.Shake(SHAKE, SHAKE_SECONDS);
        }

        /// <summary>The regular dead inside fall at once, leaving their gems.</summary>
        private void ClearField()
        {
            EnemyWorld enemies = run.enemies;
            int killed = enemies.KillRegularIn(center, radius, RunRecord.OTHER_SOURCE, scratch);
            for (int i = 0; i < killed; i++)
            {
                run.effects.Emit(boneBurst, enemies.GetPosition(scratch[i]) + Ground.Up(POP_HEIGHT), POP_SHARDS);
            }
        }

        /// <summary>The ward falls (its fight or trap ended, a new run).</summary>
        public void Lower() => isUp = false;

        /// <summary>The ring closes in to a radius over some seconds.</summary>
        public void CloseTo(float ringRadius, float seconds)
        {
            targetRadius = ringRadius;
            closeSpeed = MathF.Max(radius - ringRadius, 0.0f) / MathF.Max(seconds, 0.1f);
        }

        /// <summary>A ground point kept inside the ring by a margin (bosses and what they place).</summary>
        public Vector3 Clamp(Vector3 point, float margin)
        {
            if (!isUp)
            {
                return point;
            }
            Vector3 offset = Ground.Flat(point) - center;
            float limit = MathF.Max(radius - margin, 0.0f);
            float length = offset.magnitude;
            return length > limit ? center + offset * (limit / length) : point;
        }

        /// <summary>Whether a ground point lies outside the ring (the test autopilot avoids it).</summary>
        public bool IsOutside(Vector3 point) => isUp && Ground.Distance(point, center) > radius;

        public void Tick(float dt)
        {
            if (!isUp)
            {
                return;
            }
            age += dt;
            burnTimer -= dt;
            radius = MathF.Max(targetRadius, radius - closeSpeed * dt);
            DrawRing(dt);
            HoldHero();
            if (isBossFight)
            {
                DropRoast(dt);
            }
        }

        private void DropRoast(float dt)
        {
            roastTimer -= dt;
            if (roastTimer > 0.0f)
            {
                return;
            }
            roastTimer = ROAST_INTERVAL;
            if (run.pickups.CountOf(PickupKind.Food) < MAX_ROASTS)
            {
                run.pickups.Spawn(PickupKind.Food, BossGeometry.RandomInDisc(run.rng, center, radius * ROAST_SPREAD), 1.0f);
            }
        }

        /// <summary>A hero past the ring is thrown back in, burned for a share of max health.</summary>
        private void HoldHero()
        {
            HeroController hero = run.hero;
            if (hero == null || !hero.isAlive || hero.isDashing || Ground.Distance(hero.position, center) <= radius - HERO_MARGIN)
            {
                return;
            }
            Vector3 inward = Ground.Direction(hero.position, center, Vector3.forward);
            hero.Dash(inward, THROW_METRES, THROW_SECONDS, false);
            if (burnTimer > 0.0f)
            {
                return;
            }
            burnTimer = BURN_INTERVAL;
            if (hero.TakeDamage(hero.maxHealth * BURN_HEALTH_SHARE, hero.position - inward))
            {
                run.director.lastDamageSource = SOURCE;
            }
            run.feedback.Shake(BURN_SHAKE, BURN_SHAKE_SECONDS);
        }

        /// <summary>The ring on the ground and flames along it, growing out of the ground as the ward rises.</summary>
        private void DrawRing(float dt)
        {
            ringTimer -= dt;
            if (ringTimer <= 0.0f)
            {
                ringTimer = RING_INTERVAL;
                run.effects.Emit(ring, center + Ground.Up(Tuning.TELEGRAPH_HEIGHT), 1, color, radius * 2.0f, RING_INTERVAL * 2.0f);
            }
            flameTimer -= dt;
            if (flameTimer > 0.0f)
            {
                return;
            }
            flameTimer = FLAME_INTERVAL;
            flameTurn += FLAME_DRIFT_DEGREES;
            int flames = Math.Clamp((int)(2.0f * MathF.PI * radius / FLAME_SPACING), 1, MAX_FLAMES);
            float rise = MathF.Min(1.0f, age / RISE_SECONDS);
            for (int i = 0; i < flames; i++)
            {
                Vector3 at = center + Ground.RingDirection(flameTurn, i, flames) * radius;
                ringFx.EmitSized(ringFx.flame, at, color, rise * FLAME_SIZE);
            }
        }
    }
}

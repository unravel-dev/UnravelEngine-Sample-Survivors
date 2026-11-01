using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The run's frame: the steps of the playing and ended phases, the camera and hero view, and the handlers of what
    /// happens during play (deaths, pickups, hits on the hero). Transitions between phases live in Run.cs.
    /// </summary>
    public partial class Run
    {
        private const float HERO_DAMAGE_NUMBER_HEIGHT = 2.2f;
        private const float REVIVE_BLAST_RADIUS = 6.0f;
        private const float REVIVE_BLAST_DAMAGE = 200.0f;
        private const float REVIVE_BLAST_KNOCKBACK = 12.0f;

        /// <summary>One frame of play. Stops as soon as a step leaves Playing, so nothing runs on into a menu or the end.</summary>
        private void StepPlaying(float dt)
        {
            runTime += dt;
            StepHero(dt);
            if (phase != RunPhase.Playing || !StepWorld(dt))
            {
                return;
            }
            markers.Show();
            statusFlames.Tick(dt);
            ui.UpdateHud();
            record.TrackLevelByMinute();
            OpenQueuedMenu();
        }

        /// <summary>Moves the hero by the stick (or the autopilot) and reads the active and pause buttons.</summary>
        private void StepHero(float dt)
        {
            Vector2 input = devAutopilot ? autopilot.GetInput() : new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            hero.Tick(dt, input);
            if (Input.IsPressed("Active"))
            {
                loadout.TryActivate();
            }
            if (Input.IsPressed("Pause"))
            {
                Pause();
            }
        }

        /// <summary>
        /// Steps the world in order; false once a step left Playing: the hero or the last boss fell during the fighting,
        /// or a chest opened when the pickups were collected.
        /// </summary>
        private bool StepWorld(float dt)
        {
            Vector3 heroPosition = hero.position;
            enemies.targetPosition = heroPosition;
            enemies.targetRadius = hero.radius;
            enemies.hasTarget = hero.isAlive;
            projectiles.heroPosition = heroPosition;
            pickups.heroPosition = heroPosition;
            pickups.magnetRadius = stats[StatId.PickupRadius];
            director.Tick(dt);
            enemies.Step(dt);
            record.TakeStatusDamage(enemies.statusDamageBySource);
            projectiles.Step(dt);
            if (phase != RunPhase.Playing)
            {
                return false;
            }
            pickups.Step(dt);
            if (phase != RunPhase.Playing)
            {
                return false;
            }
            abilityExecutor.Tick(dt);
            loadout.Tick(dt);
            mapEvents.Tick(dt);
            pickupBeacons.Tick(dt);
            return phase == RunPhase.Playing;
        }

        /// <summary>The fight plays on in slow motion behind the end, then the results screen shows.</summary>
        private void StepEnded(float dt, float unscaledDt)
        {
            enemies.hasTarget = false;
            enemies.Step(dt);
            projectiles.Step(dt);
            if (endTimer > 0.0f)
            {
                endTimer -= unscaledDt;
                if (endTimer <= 0.0f)
                {
                    ui.ShowResults(record);
                }
            }
        }

        /// <summary>The camera follows the hero (or frames the menu stage), and the hero's ring, light and effects follow.</summary>
        private void UpdateView(float unscaledDt)
        {
            if (phase == RunPhase.Menu)
            {
                cameraRig.Update(stage.focus, Vector3.zero, unscaledDt);
                return;
            }
            if (hero != null)
            {
                cameraRig.Update(hero.position, hero.currentVelocity, unscaledDt);
                presence.Update(hero);
            }
        }

        private void OnEnemyDeath(EnemyDeath death)
        {
            record.AddKill(death.source);
            if (death.isElite)
            {
                record.elitesKilled++;
            }
            loadout?.OnEnemyKilled(death);
            abilityExecutor.OnEnemyDied(death);
            director.OnEnemyDied(death);
            mapEvents?.OnEnemyDied(death);
            drops.OnEnemyDeath(death);
        }

        private void OnPickup(PickupKind kind, float value, Vector3 position) => drops.OnPickup(kind, value, position);

        /// <summary>An enemy projectile hurt the hero: its shooter is named on the death screen if the hero falls.</summary>
        private void OnProjectileHurtHero(string source) => director.lastDamageSource = source;

        private void OnHeroDamaged(float amount, Vector3 from)
        {
            record.damageTaken += amount;
            damageNumbers?.ShowHeroDamage(hero.position + new Vector3(0.0f, HERO_DAMAGE_NUMBER_HEIGHT, 0.0f), amount);
            feedback.OnHeroHurt(amount / MathF.Max(hero.maxHealth, 1.0f));
        }

        /// <summary>A revival blasts the enemies around the hero away.</summary>
        private void OnHeroRevived()
        {
            Combat.DamageCircle(hero.position, REVIVE_BLAST_RADIUS, REVIVE_BLAST_DAMAGE, REVIVE_BLAST_KNOCKBACK, RunRecord.OTHER_SOURCE);
            feedback.OnHeroRevived(hero.position);
        }

        private void OnHeroDied() => EndRun(false, director.lastDamageSource);
    }
}

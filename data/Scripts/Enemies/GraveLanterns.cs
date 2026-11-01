using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Grave Lanterns: breakable props that show and pay their loot. The first is lit 30 s into the run, then one every
    /// 45 s at 14-20 m from the hero while fewer than three stand. Each is a prop entity over a 1 HP proxy row of the
    /// enemy world, so every weapon breaks it like an enemy; its loot hangs above it from the start, with the beacon and
    /// marker of its kind (<see cref="PickupBeacons"/>), and drops when it breaks. Main thread.
    /// </summary>
    public sealed class GraveLanterns
    {
        private const float FIRST_SECONDS = 30.0f;
        private const float INTERVAL = 45.0f;
        private const int MAX_STANDING = 3;
        private const float DISTANCE_MIN = 14.0f;
        private const float DISTANCE_MAX = 20.0f;
        private const float FULL_TURN_DEGREES = 360.0f;

        private readonly Run run;
        private readonly WaveDirector director;
        private readonly List<Lantern> lanterns = new List<Lantern>(MAX_STANDING);
        private float timer;

        private struct Lantern
        {
            public Entity entity;
            public Enemy enemy;
            /// <summary>The loot's row in the pickup world.</summary>
            public int lootRow;
        }

        public GraveLanterns(Run run, WaveDirector director)
        {
            this.run = run;
            this.director = director;
        }

        /// <summary>A new run: no lanterns, the first one after <see cref="FIRST_SECONDS"/>.</summary>
        public void Reset()
        {
            Clear();
            timer = FIRST_SECONDS;
        }

        /// <summary>Removes every lantern without loot.</summary>
        public void Clear()
        {
            for (int i = lanterns.Count - 1; i >= 0; i--)
            {
                Remove(i);
            }
        }

        /// <summary>Forgets lanterns whose row is gone and lights a new one when it is time.</summary>
        public void Tick(float dt)
        {
            timer -= dt;
            for (int i = lanterns.Count - 1; i >= 0; i--)
            {
                if (run.enemies.GetRow(lanterns[i].enemy) < 0)
                {
                    Remove(i);
                }
            }
            if (timer > 0.0f || lanterns.Count >= MAX_STANDING || !AssetChecks.IsLoaded(EnemyCatalog.lanternPrefab))
            {
                return;
            }
            timer = INTERVAL;
            Place();
        }

        /// <summary>A broken lantern pays its loot and its prop goes.</summary>
        public void OnEnemyDied(in EnemyDeath death)
        {
            for (int i = 0; i < lanterns.Count; i++)
            {
                if (lanterns[i].enemy.Equals(death.enemy))
                {
                    run.drops.OnLanternBroken(death.position, lanterns[i].lootRow);
                    Remove(i);
                    return;
                }
            }
        }

        /// <summary>Dev: some lanterns in a ring around the hero, close enough to review on screen.</summary>
        public void DevPlaceNear(int count, float distance)
        {
            for (int i = 0; i < count; i++)
            {
                Place(run.hero.position + Ground.FromYaw(i * FULL_TURN_DEGREES / count) * distance);
            }
        }

        private void Place()
        {
            float angle = (float)run.rng.NextDouble() * FULL_TURN_DEGREES;
            float distance = DISTANCE_MIN + (float)run.rng.NextDouble() * (DISTANCE_MAX - DISTANCE_MIN);
            Place(run.hero.position + Ground.FromYaw(angle) * distance);
        }

        private void Place(Vector3 at)
        {
            Vector3 position = director.ClampToArena(at);
            Entity entity = Scene.Instantiate(EnemyCatalog.lanternPrefab, position);
            var options = new SpawnOptions { healthScale = 1.0f, damageScale = 1.0f, isRewarded = true };
            Enemy enemy = run.enemies.Spawn(EnemyCatalog.lanternType, position, in options);
            lanterns.Add(new Lantern { entity = entity, enemy = enemy, lootRow = run.drops.SpawnLanternLoot(position) });
        }

        private void Remove(int index)
        {
            if (lanterns[index].entity.IsValid())
            {
                Scene.DestroyEntity(lanterns[index].entity);
            }
            run.pickups.RemoveHeld(lanterns[index].lootRow);
            lanterns.RemoveAt(index);
        }
    }
}

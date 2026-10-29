using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>A projectile that only shows something: it ignores the enemies it touches and does nothing at its end.</summary>
    public sealed class VisualOnlyHandler : IProjectileHandler
    {
        public void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
        }

        public void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
        }
    }

    /// <summary>
    /// Everything a weapon, a class kit or a passive trait works with during one run: the run's systems, the shared
    /// damage, feel, cluster and lingering-zone helpers, the class kit and the game clock. Built by the loadout.
    /// </summary>
    public sealed class WeaponContext
    {
        private const float ARENA_MARGIN = 1.5f;
        private const float MIN_ARENA_LIMIT = 1.0f;
        /// <summary>Heal numbers float this high above the hero's feet.</summary>
        private const float HEAL_NUMBER_HEIGHT = 2.2f;
        /// <summary>Heals smaller than this show no number.</summary>
        private const float MIN_SHOWN_HEAL = 1.0f;

        public readonly Run run;
        public readonly PlayerStats stats;
        public readonly EnemyWorld enemies;
        public readonly ProjectileWorld projectiles;
        public readonly Effects effects;
        public readonly Sounds sounds;
        public readonly HeroController hero;
        public readonly WeaponFeel feel;
        public readonly Strikes strikes;
        public readonly ClusterMap clusters;
        public readonly GroundZones zones;
        /// <summary>The handler of projectiles that only show something.</summary>
        public readonly IProjectileHandler visualOnly = new VisualOnlyHandler();
        /// <summary>The hero's class kit; set by the loadout before any weapon is built.</summary>
        public ClassKit kit;
        /// <summary>The loadout this context belongs to (which weapon sits in a hit's source slot); set before any weapon is built.</summary>
        public Loadout loadout;
        /// <summary>Game time (s) since the run's loadout was built.</summary>
        public float clock;

        public WeaponContext(Run run)
        {
            this.run = run;
            stats = run.stats;
            enemies = run.enemies;
            projectiles = run.projectiles;
            effects = run.effects;
            sounds = run.sounds;
            hero = run.hero;
            feel = new WeaponFeel(run);
            strikes = new Strikes(run);
            clusters = new ClusterMap(run.enemies);
            zones = new GroundZones(strikes, run);
        }

        /// <summary>The class kit as a given class's kit (BarbarianKit for its Fury), or null for another class.</summary>
        public T KitAs<T>() where T : ClassKit => kit as T;

        /// <summary>A base damage after the Might stat.</summary>
        public float MightOf(float baseDamage) => baseDamage * stats[StatId.Might];

        /// <summary>Heals the hero and floats the health regained over the hero; returns it.</summary>
        public float HealHero(float amount, HealSource source)
        {
            float healed = hero.Heal(amount, source);
            if (healed >= MIN_SHOWN_HEAL)
            {
                run.damageNumbers?.ShowHeal(hero.position + Ground.Up(HEAL_NUMBER_HEIGHT), healed);
            }
            return healed;
        }

        /// <summary>The index of a projectile look, or the first look (logged) when the catalog lacks it.</summary>
        public int FindVisual(string name)
        {
            int index = projectiles.FindVisual(name);
            if (index < 0)
            {
                Log.Warning($"Weapons: no projectile look named {name}.");
                return 0;
            }
            return index;
        }

        /// <summary>Keeps a ground point inside the playable arena (blink and leap targets).</summary>
        public Vector3 ClampToArena(Vector3 point)
        {
            float limit = MathF.Max(run.arenaRadius - ARENA_MARGIN, MIN_ARENA_LIMIT);
            var flat = new Vector3(point.x, 0.0f, point.z);
            float distance = flat.magnitude;
            if (distance <= limit)
            {
                return point;
            }
            Vector3 clamped = flat * (limit / distance);
            return new Vector3(clamped.x, point.y, clamped.z);
        }

        /// <summary>The farthest distance (up to <paramref name="distance"/>) the hero can travel along a direction inside the arena.</summary>
        public float GetTravel(Vector3 from, Vector3 direction, float distance)
        {
            Vector3 target = ClampToArena(from + direction * distance);
            return MathF.Min(distance, Ground.Distance(from, target));
        }
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The test player of unattended runs (<see cref="Run.devAutopilot"/>, <see cref="Run.devAutoPick"/>). It walks by
    /// scoring 16 directions: the crowd it would walk into (probes 1, 2.5 and 4.5 m ahead), the arena edge, the pull of
    /// pickups (food when hurt, chests, gems when the way is clear) and, for healthy brawlers, the nearest enemy; it
    /// keeps a little of its last heading and uses the class active when hemmed in or low. While a boss fights it dances:
    /// it circles the boss at a distance, steps in on a brawler's reach when the boss recovers, steps out of circles about
    /// to land and stays inside its ward. It picks level-up cards by a fixed preference, a passive every third level like
    /// a player spreading picks. A test player, not a good one: compare classes with it, do not tune feel by it. Main thread.
    /// </summary>
    public sealed class Autopilot
    {
        private const int DIRECTIONS = 16;
        private const int SCAN_CAPACITY = 512;
        /// <summary>Enemies within this radius (m) count toward the danger of every direction.</summary>
        private const float SCAN_RADIUS = 12.0f;
        /// <summary>Probes past this distance (m) from the arena centre count as danger, 2 per metre.</summary>
        private const float ARENA_RADIUS = 30.0f;
        private const float EDGE_DANGER_PER_METRE = 2.0f;
        /// <summary>Softens the inverse square danger of an enemy right on a probe.</summary>
        private const float DANGER_SOFTENING = 0.5f;
        private const float NEAR_PROBE = 1.0f;
        private const float MID_PROBE = 2.5f;
        private const float FAR_PROBE = 4.5f;
        private const float MID_WEIGHT = 0.6f;
        private const float FAR_WEIGHT = 0.3f;
        private const float HEADING_WEIGHT = 0.25f;
        /// <summary>Healthy brawlers fear the crowd this much less than other heroes do.</summary>
        private const float BRAWLER_DANGER_WEIGHT = 0.35f;
        private const float BRAWLER_HEALTH = 0.55f;
        /// <summary>Enemies within this radius (m) hem the hero in.</summary>
        private const float CLOSE_RADIUS = 2.6f;
        private const int HEMMED_IN_CLOSE = 6;
        private const float LOW_HEALTH = 0.35f;
        private const int LOW_HEALTH_CLOSE = 2;
        private const float FOOD_HEALTH = 0.6f;
        private const float FOOD_RANGE = 25.0f;
        private const float FOOD_PULL = 2.0f;
        private const float CHEST_RANGE = 20.0f;
        private const float CHEST_PULL = 1.5f;
        private const float BRAWL_RANGE = 14.0f;
        private const float BRAWL_PULL = 1.2f;
        private const float PICKUP_RANGE = 14.0f;
        private const float PICKUP_PULL = 1.0f;
        /// <summary>Gems are worth a detour only with fewer enemies than this within the scan radius.</summary>
        private const int PICKUP_MAX_CROWD = 8;
        /// <summary>The boss dance: the range held while it attacks, how hard the range and the orbit pull, a reach past its body.</summary>
        private const float DANCE_RANGE = 8.5f;
        /// <summary>Melee heroes dance within reach of their weapons.</summary>
        private const float BRAWLER_DANCE_RANGE = 3.2f;
        private const float DANCE_RADIAL_PULL = 1.4f;
        private const float DANCE_ORBIT_PULL = 2.4f;
        private const float DANCE_REACH = 2.2f;
        private const float WARD_DANGER = 20.0f;
        private const float PENDING_CIRCLE_DANGER = 12.0f;
        /// <summary>Every this many levels a passive outbids rank-ups.</summary>
        private const int PASSIVE_EVERY_LEVELS = 3;
        private const float PASSIVE_TURN_BONUS = 40.0f;
        private const float ORBIT_FLIP_SECONDS = 5.0f;
        // Card preferences: higher wins; a random fraction breaks ties between equal kinds.
        private const float HEAL_CARD_HEALTH = 0.45f;
        private const float HEAL_CARD_SCORE = 100.0f;
        private const float UNKNOWN_CARD_SCORE = 90.0f;
        private const float NEW_WEAPON_SCORE = 70.0f;
        private const float NEW_WEAPON_FULL_SCORE = 35.0f;
        private const int WEAPONS_WANTED = 4;
        private const float WEAPON_RANK_SCORE = 60.0f;
        private const float WEAPON_RANK_PER_RANK = 2.0f;
        private const float CLASS_WEAPON_BONUS = 5.0f;
        private const float EVOLVING_PASSIVE_SCORE = 55.0f;
        private const float PASSIVE_RANK_SCORE = 50.0f;
        private const float NEW_PASSIVE_SCORE = 35.0f;
        private const float BOON_SCORE = 20.0f;

        private readonly Run run;
        private readonly int[] nearby = new int[SCAN_CAPACITY];
        private Vector3 heading = Vector3.forward;
        private float orbitSign = 1.0f;
        private float orbitTimer = ORBIT_FLIP_SECONDS;

        public Autopilot(Run run)
        {
            this.run = run;
        }

        /// <summary>The stick input for this frame; may use the class active.</summary>
        public Vector2 GetInput()
        {
            HeroController hero = run.hero;
            Vector3 position = hero.position;
            float health = hero.healthFraction;
            int found = Math.Min(run.enemies.QueryCircle(position, SCAN_RADIUS, nearby), nearby.Length);
            int close = CountClose(position, found);
            if (close >= HEMMED_IN_CLOSE || health < LOW_HEALTH && close >= LOW_HEALTH_CLOSE)
            {
                run.loadout.TryActivate();
            }
            float dangerWeight = IsBrawler(health) ? BRAWLER_DANGER_WEIGHT : 1.0f;
            Vector3 pull = GetPull(position, health, found);
            Vector3 best = heading;
            float bestScore = float.MinValue;
            for (int k = 0; k < DIRECTIONS; k++)
            {
                Vector3 direction = Ground.FromYaw(360.0f * k / DIRECTIONS);
                float score = -GetDanger(position, direction, found) * dangerWeight + Vector3.Dot(direction, pull) +
                              HEADING_WEIGHT * Vector3.Dot(direction, heading);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = direction;
                }
            }
            heading = best;
            return new Vector2(best.x, best.z);
        }

        /// <summary>The preferred card: a heal when low, then rank-ups of owned weapons (toward evolutions), new weapons
        /// while fewer than four, passives that evolve an owned weapon, other passives, and boons last.</summary>
        public UpgradeOffer PickCard(List<UpgradeOffer> offers)
        {
            UpgradeOffer best = null;
            float bestScore = float.MinValue;
            foreach (UpgradeOffer offer in offers)
            {
                float score = ScoreCard(offer);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = offer;
                }
            }
            return best;
        }

        private bool IsBrawler(float health)
        {
            bool isMelee = run.heroClass == HeroClass.Knight || run.heroClass == HeroClass.Barbarian;
            return isMelee && health > BRAWLER_HEALTH;
        }

        private int CountClose(Vector3 position, int found)
        {
            int close = 0;
            for (int i = 0; i < found; i++)
            {
                Vector3 offset = run.enemies.GetPosition(nearby[i]) - position;
                if (offset.x * offset.x + offset.z * offset.z < CLOSE_RADIUS * CLOSE_RADIUS)
                {
                    close++;
                }
            }
            return close;
        }

        /// <summary>The danger of walking along a direction: three probes ahead, the nearer ones weighing more.</summary>
        private float GetDanger(Vector3 position, Vector3 direction, int found)
        {
            return GetProbeDanger(position + direction * NEAR_PROBE, found) +
                   MID_WEIGHT * GetProbeDanger(position + direction * MID_PROBE, found) +
                   FAR_WEIGHT * GetProbeDanger(position + direction * FAR_PROBE, found);
        }

        /// <summary>The crowd pressing on a point (inverse square of each enemy's distance) plus the arena edge.</summary>
        private float GetProbeDanger(Vector3 probe, int found)
        {
            float danger = 0.0f;
            for (int i = 0; i < found; i++)
            {
                Vector3 offset = run.enemies.GetPosition(nearby[i]) - probe;
                danger += 1.0f / (offset.x * offset.x + offset.z * offset.z + DANGER_SOFTENING);
            }
            if (run.director.IsOutsideWard(probe))
            {
                danger += WARD_DANGER;
            }
            BossHazards pending = run.director.bosses.pending;
            if (pending != null && pending.IsInPendingCircle(probe))
            {
                danger += PENDING_CIRCLE_DANGER;
            }
            var flat = new Vector3(probe.x, 0.0f, probe.z);
            float outside = flat.magnitude - ARENA_RADIUS;
            return outside > 0.0f ? danger + outside * EDGE_DANGER_PER_METRE : danger;
        }

        /// <summary>Where the autopilot wants to go besides away from danger: food when hurt, chests, gems, the crowd for brawlers.</summary>
        private Vector3 GetPull(Vector3 position, float health, int found)
        {
            PickupWorld pickups = run.pickups;
            if (health < FOOD_HEALTH && pickups.FindNearestOfKind(position, FOOD_RANGE, PickupKind.Food, out Vector3 food))
            {
                return Toward(position, food) * FOOD_PULL;
            }
            Boss boss = run.director.bosses.featured;
            if (boss != null)
            {
                return GetBossDance(position, boss, health);
            }
            if (pickups.FindNearestOfKind(position, CHEST_RANGE, PickupKind.Chest, out Vector3 chest))
            {
                return Toward(position, chest) * CHEST_PULL;
            }
            if (IsBrawler(health) && run.enemies.QueryNearest(position, BRAWL_RANGE, out int nearest))
            {
                return Toward(position, run.enemies.GetPosition(nearest)) * BRAWL_PULL;
            }
            if (found < PICKUP_MAX_CROWD && pickups.FindNearest(position, PICKUP_RANGE, out Vector3 pickup))
            {
                return Toward(position, pickup) * PICKUP_PULL;
            }
            return Vector3.zero;
        }

        /// <summary>Circles the boss at a safe range; a healthy brawler steps into reach while it recovers.</summary>
        private Vector3 GetBossDance(Vector3 position, Boss boss, float health)
        {
            orbitTimer -= Time.deltaTime;
            if (orbitTimer <= 0.0f)
            {
                orbitTimer = ORBIT_FLIP_SECONDS;
                orbitSign = -orbitSign;
            }
            Vector3 toBoss = Toward(position, boss.groundPosition);
            bool isMelee = run.heroClass == HeroClass.Knight || run.heroClass == HeroClass.Barbarian;
            float wanted = !isMelee ? DANCE_RANGE : boss.bodyRadius + (boss.isOpen ? DANCE_REACH : BRAWLER_DANCE_RANGE);
            float radial = Math.Clamp(Ground.Distance(position, boss.groundPosition) - wanted, -1.0f, 1.0f);
            return toBoss * (radial * DANCE_RADIAL_PULL) + Ground.Perpendicular(toBoss) * (orbitSign * DANCE_ORBIT_PULL);
        }

        private float ScoreCard(UpgradeOffer offer)
        {
            switch (offer.kind)
            {
                case UpgradeOffer.Kind.Fallback:
                    return offer.id == WeaponCatalog.heartyMeal.id && run.hero.healthFraction < HEAL_CARD_HEALTH ? HEAL_CARD_SCORE : 0.0f;
                case UpgradeOffer.Kind.Boon:
                    return BOON_SCORE + GetTieBreak();
                case UpgradeOffer.Kind.Weapon:
                    return ScoreWeaponCard(offer);
                case UpgradeOffer.Kind.Passive:
                    return ScorePassiveCard(offer);
                default:
                    return UNKNOWN_CARD_SCORE;
            }
        }

        private float ScoreWeaponCard(UpgradeOffer offer)
        {
            WeaponDef def = WeaponCatalog.FindWeapon(offer.id);
            float classBonus = def != null && def.isClassWeapon ? CLASS_WEAPON_BONUS : 0.0f;
            if (!offer.isNew)
            {
                return WEAPON_RANK_SCORE + offer.fromRank * WEAPON_RANK_PER_RANK + classBonus;
            }
            float score = run.loadout.weapons.Count < WEAPONS_WANTED ? NEW_WEAPON_SCORE : NEW_WEAPON_FULL_SCORE;
            return score + classBonus + GetTieBreak();
        }

        private float ScorePassiveCard(UpgradeOffer offer)
        {
            float turnBonus = run.level % PASSIVE_EVERY_LEVELS == 0 ? PASSIVE_TURN_BONUS : 0.0f;
            return turnBonus + ScorePassive(offer);
        }

        private float ScorePassive(UpgradeOffer offer)
        {
            if (!offer.isNew)
            {
                return PASSIVE_RANK_SCORE + GetTieBreak();
            }
            foreach (SlotView weapon in run.loadout.weapons)
            {
                WeaponDef def = WeaponCatalog.FindWeapon(weapon.id);
                if (def != null && def.evolution != null && def.evolution.passiveId == offer.id)
                {
                    return EVOLVING_PASSIVE_SCORE;
                }
            }
            return NEW_PASSIVE_SCORE + GetTieBreak();
        }

        private float GetTieBreak() => (float)run.rng.NextDouble();

        private static Vector3 Toward(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0.0f;
            return offset.sqrMagnitude > 1e-4f ? offset.normalized : Vector3.zero;
        }
    }
}

using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// What enemies drop and what pickups do: experience gems with stochastic rounding, rare drops with caps and
    /// cooldowns scaled by Luck, elite chests, lantern loot, and the pickup effects (heal, vacuum, screen clear,
    /// screen freeze, chests). Owned by the run (Run.drops); its rare-drop timers and gem chime chain reset with each
    /// run. Main thread.
    /// </summary>
    public sealed class Drops
    {
        private const float COIN_CHANCE = 0.04f;
        private const float ROAST_CHANCE = 0.0005f;
        private const float LODESTONE_CHANCE = 0.0003f;
        private const float BELL_CHANCE = 0.0002f;
        private const float HOURGLASS_CHANCE = 0.0002f;
        private const int MAX_ROASTS = 3;
        private const float RARE_COOLDOWN = 120.0f;
        private const float LODESTONE_PITY_SECONDS = 150.0f;
        private const int LODESTONE_PITY_GEMS = 150;
        private const float ROAST_HEAL = 0.25f;
        private const float ELITE_ROAST_CHANCE = 0.5f;
        private const int ELITE_COIN_BAG = 10;
        private const float ELITE_HIT_STOP = 0.06f;
        private const float ELITE_SHAKE = 0.3f;
        private const float ELITE_SHAKE_SECONDS = 0.3f;
        private const float ELITE_BURST_HEIGHT = 0.8f;
        private const float LANTERN_BURST_HEIGHT = 1.0f;
        /// <summary>A lantern's gem cache is worth this much experience before the run's scales.</summary>
        private const float LANTERN_GEM_CACHE = 25.0f;
        /// <summary>Luck points per +100% drop chance.</summary>
        private const float LUCK_POINTS_PER_DOUBLE = 100.0f;
        /// <summary>Radius (m) the bell and the hourglass reach: the view plus a margin.</summary>
        private const float SCREEN_RADIUS = 22.0f;
        private const float BELL_ELITE_DAMAGE = 200.0f;
        private const float BELL_SHAKE = 0.5f;
        private const float BELL_SHAKE_SECONDS = 0.5f;
        private const float BELL_HIT_STOP = 0.08f;
        private const float HOURGLASS_SECONDS = 5.0f;
        private const float HEAL_NUMBER_HEIGHT = 2.2f;
        /// <summary>Chest sizes; the boss chest always holds the biggest.</summary>
        private const int BIG_CHEST_ITEMS = 5;
        private const int MEDIUM_CHEST_ITEMS = 3;
        private const int SMALL_CHEST_ITEMS = 1;
        /// <summary>Chances (before Luck) of an elite chest holding five items, or three or more.</summary>
        private const double ELITE_BIG_CHEST_CHANCE = 0.02;
        private const double ELITE_MEDIUM_CHEST_CHANCE = 0.17;
        /// <summary>Chance (before Luck) of a mini-boss chest holding five items instead of three.</summary>
        private const double MINI_BOSS_BIG_CHEST_CHANCE = 0.2;
        private const float GEM_CHAIN_WINDOW = 0.3f;
        private const float GEM_CHAIN_RESET = 0.5f;
        private const int GEM_CHAIN_MAX = 12;
        private const float SEMITONES_PER_OCTAVE = 12.0f;
        private const int SCRATCH = 4096;
        private const float NEVER = -1000.0f;

        /// <summary>What a Grave Lantern holds, by chance; the gem cache's value grows with the run.</summary>
        private static readonly Loot[] LANTERN_LOOT =
        {
            new Loot(0.40, PickupKind.Food, 1.0f),
            new Loot(0.25, PickupKind.Gold, 5.0f),
            new Loot(0.12, PickupKind.Magnet, 1.0f),
            new Loot(0.08, PickupKind.Bomb, 1.0f),
            new Loot(0.08, PickupKind.Freeze, 1.0f),
            new Loot(0.07, PickupKind.GemSmall, LANTERN_GEM_CACHE),
        };

        private readonly int[] scratch = new int[SCRATCH];
        private float lastBell = NEVER;
        private float lastHourglass = NEVER;
        private float lastLodestone;
        private float lastGemTime = NEVER;
        private int gemChain;
        private readonly Run run;
        private readonly int eliteDeathBurst;
        private readonly int lanternShatter;
        private readonly int healBurst;
        private readonly int bellFlash;
        private readonly int frostWave;

        /// <summary>One entry of a loot table.</summary>
        private readonly struct Loot
        {
            public readonly double chance;
            public readonly PickupKind kind;
            public readonly float value;

            public Loot(double chance, PickupKind kind, float value)
            {
                this.chance = chance;
                this.kind = kind;
                this.value = value;
            }
        }

        /// <summary>Where a chest comes from; the item count odds differ.</summary>
        public enum ChestSource
        {
            Elite,
            MiniBoss,
            Boss,
        }

        public Drops(Run run)
        {
            this.run = run;
            Effects effects = run.effects;
            eliteDeathBurst = effects.Find("EliteDeathBurst");
            lanternShatter = effects.Find("LanternShatter");
            healBurst = effects.Find("HealBurst");
            bellFlash = effects.Find("BellFlash");
            frostWave = effects.Find("FrostWave");
        }

        /// <summary>Resets the rare-drop timers and the gem chime chain (a new run).</summary>
        public void Reset()
        {
            lastBell = NEVER;
            lastHourglass = NEVER;
            lastLodestone = 0.0f;
            lastGemTime = NEVER;
            gemChain = 0;
        }

        public void OnEnemyDeath(in EnemyDeath death)
        {
            if (!death.isRewarded)
            {
                return;
            }
            PickupWorld pickups = run.pickups;
            float experience = RoundStochastic(death.experience * run.director.experienceScale * run.stats[StatId.Growth], run.rng);
            if (experience > 0.0f)
            {
                pickups.Spawn(PickupWorld.GetGemKind(experience), death.position, experience);
            }
            float luck = luckFactor;
            if (Roll(COIN_CHANCE * luck))
            {
                pickups.Spawn(PickupKind.Gold, death.position, 1.0f);
            }
            if (Roll(ROAST_CHANCE * luck) && pickups.CountOf(PickupKind.Food) < MAX_ROASTS)
            {
                pickups.Spawn(PickupKind.Food, death.position, 1.0f);
            }
            float now = run.runTime;
            bool lodestonePity = now - lastLodestone > LODESTONE_PITY_SECONDS && pickups.gemsOnGround > LODESTONE_PITY_GEMS;
            if ((lodestonePity || Roll(LODESTONE_CHANCE * luck)) && pickups.CountOf(PickupKind.Magnet) == 0)
            {
                lastLodestone = now;
                pickups.Spawn(PickupKind.Magnet, death.position, 1.0f);
            }
            if (now - lastBell > RARE_COOLDOWN && Roll(BELL_CHANCE * luck))
            {
                lastBell = now;
                pickups.Spawn(PickupKind.Bomb, death.position, 1.0f);
            }
            if (now - lastHourglass > RARE_COOLDOWN && Roll(HOURGLASS_CHANCE * luck))
            {
                lastHourglass = now;
                pickups.Spawn(PickupKind.Freeze, death.position, 1.0f);
            }
            if (death.isElite)
            {
                OnEliteDeath(death.position);
            }
        }

        /// <summary>Elites always drop a chest, a coin bag and sometimes a roast; the kill feels big.</summary>
        private void OnEliteDeath(Vector3 position)
        {
            run.pickups.Spawn(PickupKind.Chest, position, RollChestItems(ChestSource.Elite));
            run.pickups.Spawn(PickupKind.Gold, position, ELITE_COIN_BAG);
            if (run.rng.NextDouble() < ELITE_ROAST_CHANCE)
            {
                run.pickups.Spawn(PickupKind.Food, position, 1.0f);
            }
            run.feedback.HitStop(ELITE_HIT_STOP);
            run.feedback.Shake(ELITE_SHAKE, ELITE_SHAKE_SECONDS);
            run.effects.Emit(eliteDeathBurst, position + Vector3.up * ELITE_BURST_HEIGHT, 1);
            run.sounds.PlayAt("elite_death", position);
        }

        /// <summary>A Grave Lantern broke: one pick from its loot table.</summary>
        public void OnLanternBroken(Vector3 position)
        {
            run.effects.Emit(lanternShatter, position + Vector3.up * LANTERN_BURST_HEIGHT, 1);
            run.sounds.PlayAt("lantern_break", position);
            Loot loot = PickLoot(LANTERN_LOOT, run.rng.NextDouble());
            if (PickupWorld.IsGem(loot.kind))
            {
                float cache = loot.value * run.director.experienceScale * run.stats[StatId.Growth];
                run.pickups.Spawn(PickupWorld.GetGemKind(cache), position, MathF.Round(cache));
                return;
            }
            run.pickups.Spawn(loot.kind, position, loot.value);
        }

        public void OnPickup(PickupKind kind, float value, Vector3 position)
        {
            switch (kind)
            {
                case PickupKind.GemSmall:
                case PickupKind.GemMedium:
                case PickupKind.GemLarge:
                case PickupKind.GemEpic:
                case PickupKind.GemHuge:
                    if (run.pickups.gemsGiveExperience)
                    {
                        run.AddExperience(value);
                    }
                    PlayGemSound(kind);
                    break;
                case PickupKind.Gold:
                    run.AddGold((int)MathF.Max(1.0f, MathF.Round(value * run.stats[StatId.Greed] * run.director.goldScale)));
                    run.sounds.Play("coin");
                    break;
                case PickupKind.Food:
                    float heal = run.hero.maxHealth * ROAST_HEAL;
                    run.hero.Heal(heal, HealSource.Food);
                    run.damageNumbers?.ShowHeal(run.hero.position + new Vector3(0.0f, HEAL_NUMBER_HEIGHT, 0.0f), heal);
                    run.effects.Emit(healBurst, run.hero.position, 1);
                    run.sounds.Play("heal");
                    break;
                case PickupKind.Magnet:
                    run.pickups.AttractAll();
                    run.sounds.Play("lodestone");
                    break;
                case PickupKind.Bomb:
                    RingBell();
                    break;
                case PickupKind.Freeze:
                    FreezeScreen();
                    break;
                case PickupKind.Chest:
                    run.OpenChest((int)value, position);
                    break;
            }
        }

        /// <summary>Items in a chest: 1, 3 or 5, better with Luck; the first chest of a run always holds 3.</summary>
        public int RollChestItems(ChestSource source)
        {
            float luck = luckFactor;
            double roll = run.rng.NextDouble();
            switch (source)
            {
                case ChestSource.Boss:
                    return BIG_CHEST_ITEMS;
                case ChestSource.MiniBoss:
                    return roll < MINI_BOSS_BIG_CHEST_CHANCE * luck ? BIG_CHEST_ITEMS : MEDIUM_CHEST_ITEMS;
                default:
                    if (run.record.chestsOpened == 0)
                    {
                        // The first chest of a run always holds three items: it sets the expectation.
                        return MEDIUM_CHEST_ITEMS;
                    }
                    if (roll < ELITE_BIG_CHEST_CHANCE * luck)
                    {
                        return BIG_CHEST_ITEMS;
                    }
                    return roll < ELITE_MEDIUM_CHEST_CHANCE * luck ? MEDIUM_CHEST_ITEMS : SMALL_CHEST_ITEMS;
            }
        }

        /// <summary>Holy Bell: every regular enemy in view dies; elites and proxies (bosses, lanterns, pyres) take a heavy hit.</summary>
        private void RingBell()
        {
            EnemyWorld enemies = run.enemies;
            int found = enemies.QueryCircle(run.hero.position, SCREEN_RADIUS, scratch);
            for (int i = 0; i < found; i++)
            {
                int row = scratch[i];
                if (enemies.IsElite(row) || enemies.IsProxy(row))
                {
                    Combat.DamageEnemy(row, BELL_ELITE_DAMAGE, Vector3.zero, RunRecord.OTHER_SOURCE, false);
                }
                else
                {
                    enemies.Kill(enemies.GetEnemy(row), RunRecord.OTHER_SOURCE);
                }
            }
            run.effects.Emit(bellFlash, run.hero.position, 1);
            run.sounds.Play("holy_bell");
            run.feedback.Shake(BELL_SHAKE, BELL_SHAKE_SECONDS);
            run.feedback.HitStop(BELL_HIT_STOP);
        }

        /// <summary>Frost Hourglass: every enemy in view freezes (bosses and other proxies only slow).</summary>
        private void FreezeScreen()
        {
            EnemyWorld enemies = run.enemies;
            int found = enemies.QueryCircle(run.hero.position, SCREEN_RADIUS, scratch);
            for (int i = 0; i < found; i++)
            {
                enemies.Freeze(scratch[i], HOURGLASS_SECONDS);
            }
            run.effects.Emit(frostWave, run.hero.position, 1);
            run.sounds.Play("frost_hourglass");
        }

        /// <summary>Gem pickups climb a semitone per gem picked in quick succession; bigger tiers sound lower.</summary>
        private void PlayGemSound(PickupKind kind)
        {
            float now = Time.realtimeSinceStartup;
            float gap = now - lastGemTime;
            lastGemTime = now;
            gemChain = gap > GEM_CHAIN_RESET ? 0 : gap <= GEM_CHAIN_WINDOW ? Math.Min(gemChain + 1, GEM_CHAIN_MAX) : gemChain;
            float tierPitch = kind switch
            {
                PickupKind.GemMedium => 0.94f,
                PickupKind.GemLarge => 0.89f,
                PickupKind.GemEpic => 0.84f,
                PickupKind.GemHuge => 0.79f,
                _ => 1.0f,
            };
            run.sounds.Play("gem", 1.0f, tierPitch * MathF.Pow(2.0f, gemChain / SEMITONES_PER_OCTAVE));
        }

        /// <summary>The entry of a chance table a roll (0..1) lands in; the last one when the chances fall short.</summary>
        private static Loot PickLoot(Loot[] table, double roll)
        {
            for (int i = 0; i < table.Length - 1; i++)
            {
                roll -= table[i].chance;
                if (roll < 0.0)
                {
                    return table[i];
                }
            }
            return table[table.Length - 1];
        }

        /// <summary>Drop chances multiply by this: 1 + 1% per point of Luck.</summary>
        private float luckFactor => 1.0f + run.stats[StatId.Luck] / LUCK_POINTS_PER_DOUBLE;

        private bool Roll(float chance) => run.rng.NextDouble() < chance;

        private static float RoundStochastic(float value, System.Random rng)
        {
            float floor = MathF.Floor(value);
            return floor + (rng.NextDouble() < value - floor ? 1.0f : 0.0f);
        }
    }
}

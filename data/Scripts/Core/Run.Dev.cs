using System;
using System.Text;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Development hooks of the run: test and capture tools call them through the editor's scripts_invoke; the game
    /// never does. Hooks that need a hero or a run do nothing without one.
    /// </summary>
    public partial class Run
    {
        private const float LINEUP_HOLD_SECONDS = 30.0f;
        /// <summary>The lineup's first enemy stands this far to the hero's left (x) and ahead (z), in metres.</summary>
        private const float LINEUP_LEFT = -7.5f;
        private const float LINEUP_AHEAD = 5.0f;
        private const float LINEUP_COLUMN_SPACING = 3.0f;
        private const float LINEUP_ROW_SPACING = 3.5f;
        private const float LINEUP_HEALTH_SCALE = 100.0f;
        private const float CROWD_HEALTH_SCALE = 40.0f;
        /// <summary>The golden angle (radians): a full crowd ring spreads its enemies evenly around the hero.</summary>
        private const float GOLDEN_ANGLE = 2.39996f;
        /// <summary>1 - 1 / golden ratio and its complement: low-discrepancy steps for crowd arcs and distances.</summary>
        private const float ARC_STEP = 0.381966f;
        private const float DISTANCE_STEP = 0.618034f;
        private const float FULL_CIRCLE_DEGREES = 360.0f;
        private const float THOUSAND = 1000.0f;

        /// <summary>Spawns one of every archetype per tint in rows in front of the hero, held in place, for review.</summary>
        public int DevSpawnLineup(float holdSeconds = LINEUP_HOLD_SECONDS)
        {
            if (hero == null)
            {
                return 0;
            }
            Vector3 origin = hero.position + new Vector3(LINEUP_LEFT, 0.0f, LINEUP_AHEAD);
            int spawned = 0;
            for (int tint = 0; tint < (int)Tint.Count; tint++)
            {
                for (int archetype = 0; archetype < (int)Archetype.Count; archetype++)
                {
                    Vector3 position = origin + new Vector3(archetype * LINEUP_COLUMN_SPACING, 0.0f, tint * LINEUP_ROW_SPACING);
                    var options = new SpawnOptions { healthScale = LINEUP_HEALTH_SCALE, damageScale = 0.0f };
                    Enemy enemy = enemies.Spawn(EnemyCatalog.Get((Archetype)archetype, (Tint)tint), position, in options);
                    enemies.SetYaw(enemy.row, MathF.PI);
                    enemies.Stun(enemy.row, holdSeconds);
                    spawned++;
                }
            }
            return spawned;
        }

        /// <summary>One line about the run for test logs.</summary>
        public string DevReport()
        {
            string health = hero != null ? $"{hero.currentHealth:0}/{hero.maxHealth:0}" : "-";
            MapEvent active = mapEvents?.active;
            string mapEvent = active != null ? $"{active.kind}:{active.state}:{active.progress:0.00}:{active.timeLeft:0}" : "-";
            return $"phase={phase} t={runTime:0.0} level={level} kills={record.kills} alive={enemies.count} projectiles={projectiles.count} " +
                   $"pickups={pickups.count} hp={health} gold={gold} taken={record.damageTaken:0} healed={record.totalHealed:0} [{DescribeHealing()}] " +
                   $"event={mapEvent} weapons={DescribeWeapons()}";
        }

        /// <summary>Turns the unattended-test switches on or off, at a game speed.</summary>
        public void DevAutoplay(bool enabled, float speed = 1.0f)
        {
            devAutoPick = enabled;
            devAutopilot = enabled;
            SetDevSpeed(enabled ? speed : 1.0f);
        }

        /// <summary>Spawns many enemies of the current mix in a ring around the hero (stress tests).</summary>
        public int DevSpawnMany(int count, float innerRadius = 10.0f, float outerRadius = 40.0f)
        {
            if (hero == null)
            {
                return 0;
            }
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)rng.NextDouble() * MathF.PI * 2.0f;
                float distance = innerRadius + (float)rng.NextDouble() * (outerRadius - innerRadius);
                var archetype = (Archetype)(i % (int)Archetype.Count);
                Vector3 position = hero.position + new Vector3(MathF.Sin(angle), 0.0f, MathF.Cos(angle)) * distance;
                var options = new SpawnOptions { healthScale = 1.0f, damageScale = 1.0f, isRewarded = true };
                if (enemies.Spawn(EnemyCatalog.Get(archetype, (Tint)(i % (int)Tint.Count)), position, in options).isValid)
                {
                    spawned++;
                }
            }
            return spawned;
        }

        /// <summary>Jumps the run clock (tint tiers, mix, events) without simulating the time between.</summary>
        public void DevSetTime(float seconds) => runTime = seconds;

        /// <summary>Dev (effect reviews): uses the class active now (cooldown ignored).</summary>
        public bool DevActivate() => loadout != null && loadout.DevActivate();

        /// <summary>Dev (effect reviews): fills the class signature meter (Fury, Momentum).</summary>
        public void DevFillSignature() => loadout?.DevFillSignature();

        /// <summary>Dev (effect reviews): runs the game at a time scale (slow motion for frame captures).</summary>
        public void DevTimeScale(float scale) => SetDevSpeed(scale);

        /// <summary>Dev (balance runs): plays the current run at a Dread level (0-5) without touching the save.</summary>
        public void DevDread(int level) => director.DevSetDread(level);

        /// <summary>Dev: turns the regular waves back on (or off) after the effect-review hooks stopped them.</summary>
        public void DevSpawns(bool enabled) => director.devQuiet = !enabled;

        /// <summary>Dev (performance reviews): adds a weapon to the loadout at a rank, evolved when asked.</summary>
        public bool DevAddWeapon(string id, int rank, bool evolve) => loadout != null && loadout.DevAddWeapon(id, rank, evolve);

        /// <summary>Dev (effect reviews): one weapon only, at a rank or evolved; regular spawns stop.</summary>
        public bool DevWeapon(string id, int rank, bool evolve)
        {
            director.devQuiet = true;
            projectiles.Clear(Team.Hero);
            effects.ResetAll();
            return loadout != null && loadout.DevSetOnlyWeapon(id, rank, evolve);
        }

        /// <summary>Dev (effect reviews): a ring crowd around the hero held in place, tough enough to soak hits.</summary>
        public int DevCrowd(int count, float innerRadius, float outerRadius, float holdSeconds, float arcDegrees = FULL_CIRCLE_DEGREES)
        {
            if (hero == null)
            {
                return 0;
            }
            enemies.Clear();
            int spawned = 0;
            float arc = arcDegrees * Mathf.Deg2Rad;
            for (int i = 0; i < count; i++)
            {
                float angle = arcDegrees >= FULL_CIRCLE_DEGREES ? i * GOLDEN_ANGLE : ((i * ARC_STEP) % 1.0f - 0.5f) * arc;
                float distance = innerRadius + (outerRadius - innerRadius) * ((i * DISTANCE_STEP) % 1.0f);
                Vector3 position = hero.position + new Vector3(MathF.Sin(angle), 0.0f, MathF.Cos(angle)) * distance;
                var archetype = (Archetype)(i % (int)Archetype.Count);
                var options = new SpawnOptions { healthScale = CROWD_HEALTH_SCALE, damageScale = 0.0f };
                Enemy enemy = enemies.Spawn(EnemyCatalog.Get(archetype, Tint.Bone), position, in options);
                if (enemy.isValid)
                {
                    enemies.Stun(enemy.row, holdSeconds);
                    spawned++;
                }
            }
            return spawned;
        }

        /// <summary>Dev (layout reviews): opens a screen by name: levelup, chest, pause, details, results, armory,
        /// settings, heroselect. A level-up (an extra one, earned ones stay queued) or a chest opens only while the run
        /// plays.</summary>
        public void DevScreen(string screen)
        {
            switch (screen)
            {
                case "levelup":
                    if (phase == RunPhase.Playing)
                    {
                        OpenLevelUp();
                    }
                    break;
                case "chest":
                    OpenChest(5, hero != null ? hero.position : Vector3.zero);
                    break;
                case "pause":
                    Pause();
                    break;
                case "details":
                    ui.OpenWeaponDetails(0);
                    break;
                case "results":
                    ui.ShowResults(record);
                    break;
                case "armory":
                    ui.OpenSubmenu(ui.armory);
                    break;
                case "settings":
                    ui.OpenSubmenu(ui.settings);
                    break;
                case "heroselect":
                    ui.OpenHeroSelect();
                    break;
            }
        }

        /// <summary>Dev: drops a chest of <paramref name="items"/> items at a distance in front of the hero (beacon reviews).</summary>
        public void DevDropChest(int items, float distance)
        {
            if (hero != null)
            {
                pickups.Spawn(PickupKind.Chest, hero.position + hero.facingDirection * distance, items);
            }
        }

        /// <summary>Dev: starts a map event (Shrine, Robber, Pyres, Moonwell) 9 m ahead of the hero now.</summary>
        public void DevEvent(string kind)
        {
            if (mapEvents != null && hero != null && Enum.TryParse(kind, true, out MapEventKind parsed) && parsed != MapEventKind.Count)
            {
                mapEvents.DevStart(parsed);
            }
        }

        /// <summary>Dev: moves the hero onto the map event (its center, or the robber), short of it by a distance.</summary>
        public void DevGoToEvent(float shortBy)
        {
            MapEvent active = mapEvents?.active;
            if (active == null || hero == null)
            {
                return;
            }
            Vector3 target = active.markerPosition;
            hero.Teleport(target - Ground.Direction(hero.position, target, Vector3.forward) * shortBy);
        }

        /// <summary>Dev: the hero takes a share of its max health as damage (healing reviews; not while invulnerable).</summary>
        public void DevHurt(float share)
        {
            hero?.TakeDamage(hero.maxHealth * share, hero.position);
        }

        /// <summary>Dev: level-ups offer only boons while on (card reviews).</summary>
        public void DevBoonCards(bool enabled)
        {
            if (loadout != null)
            {
                loadout.devForceBoons = enabled;
            }
        }

        /// <summary>Dev: takes a boon <paramref name="picks"/> times (pause sheet and stat reviews).</summary>
        public void DevBoon(string id, int picks)
        {
            for (int i = 0; i < picks; i++)
            {
                loadout?.Apply(new UpgradeOffer { kind = UpgradeOffer.Kind.Boon, id = id, isChestItem = true });
            }
        }

        /// <summary>Dev: ranks a passive up <paramref name="ranks"/> times (trait reviews; a chest item, so no Epiphany).</summary>
        public void DevPassive(string id, int ranks)
        {
            for (int i = 0; i < ranks; i++)
            {
                loadout?.Apply(new UpgradeOffer { kind = UpgradeOffer.Kind.Passive, id = id, isChestItem = true });
            }
        }

        /// <summary>Dev: opens the Armory's inspect panel on an upgrade after banking extra gold, then buys ranks
        /// from the panel (both saved).</summary>
        public void DevArmory(int index, int addGold, int buys)
        {
            if (save != null)
            {
                save.gold += addGold;
            }
            ui.OpenSubmenu(ui.armory);
            ui.armory.Inspect(index);
            for (int i = 0; i < buys; i++)
            {
                ui.armory.BuyInspected();
            }
        }

        /// <summary>Dev: clears the field, stops regular spawns and puts a boss in front of the hero.</summary>
        public void DevBossArena(int bossKind, float distance = 12.0f)
        {
            if (hero == null)
            {
                return;
            }
            enemies.Clear();
            director.devQuiet = true;
            director.bosses.Spawn((BossKind)bossKind, hero.position + new Vector3(0.0f, 0.0f, distance));
        }

        /// <summary>Dev: opens hero select with a class focused (stage spotlight and cheer).</summary>
        public void DevHeroSelect(int heroClass)
        {
            ui.OpenHeroSelect();
            ui.NotifyHeroPreview((HeroClass)heroClass);
        }

        /// <summary>Frames the camera for captures: pitch (degrees), distance (m) and a focus offset from the hero.</summary>
        public void DevCamera(float pitch, float distance, Vector3 focusOffset)
        {
            cameraRig.pitch = pitch;
            cameraRig.baseDistance = distance;
            cameraRig.focusOffset = focusOffset;
        }

        /// <summary>Makes the hero untouchable, for long captures.</summary>
        public void DevInvulnerable(float seconds) => hero?.SetDevInvulnerable(seconds);

        /// <summary>The test speed of runs; applies at once unless the menus show (they run at normal speed).</summary>
        private void SetDevSpeed(float speed)
        {
            devSpeed = speed;
            if (phase != RunPhase.Menu)
            {
                clock.speed = speed;
            }
        }

        /// <summary>Each weapon slot as id:rank[E]@damage (thousands), then the damage without a slot.</summary>
        private string DescribeWeapons()
        {
            if (loadout == null)
            {
                return "";
            }
            var weapons = new StringBuilder();
            for (int i = 0; i < loadout.weapons.Count; i++)
            {
                SlotView slot = loadout.weapons[i];
                weapons.Append(slot.id).Append(':').Append(slot.rank).Append(slot.isEvolved ? "E" : "")
                       .Append('@').Append((record.damageBySource[i] / THOUSAND).ToString("0.0")).Append("k ");
            }
            weapons.Append("other@").Append((record.damageBySource[RunRecord.OTHER_SOURCE] / THOUSAND).ToString("0.0")).Append('k');
            return weapons.ToString();
        }

        /// <summary>Healing by source as Source:amount, for the sources that healed at all.</summary>
        private string DescribeHealing()
        {
            var healed = new StringBuilder();
            for (int i = 0; i < (int)HealSource.Count; i++)
            {
                if (record.healedBySource[i] > 0.0f)
                {
                    healed.Append(((HealSource)i).ToString()).Append(':').Append(record.healedBySource[i].ToString("0")).Append(' ');
                }
            }
            return healed.ToString();
        }
    }
}

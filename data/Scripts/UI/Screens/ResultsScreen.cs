using System;
using System.Collections.Generic;
using System.Globalization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>One row of the results' weapon table.</summary>
    public sealed class ResultRow
    {
        public string icon = "";
        public string name = "";
        public string damage = "";
        public string dps = "";
        public string kills = "";
        public string owned = "";
        /// <summary>The share bar's inline style: its width is the damage as a share of the top row's.</summary>
        public string shareStyle = "";
        /// <summary>The row that dealt the most damage (highlighted).</summary>
        public bool top;
        public bool evolved;
        /// <summary>Ownership tag: text, classes ("own own-knight") and emblem image.</summary>
        public string ownText = "";
        public string ownClasses = Ownership.NO_TAG_CLASSES;
        public string ownEmblem = UIText.Icon(UIText.PLACEHOLDER_ICON);

        internal void SetOwnership(string id)
        {
            Ownership ownership = UIText.GetOwnership(id);
            ownText = ownership.text ?? "";
            ownClasses = ownership.tagClasses;
            ownEmblem = ownership.tagEmblem;
        }
    }

    /// <summary>The results data model ("results").</summary>
    public sealed class ResultsModel
    {
        private readonly ResultsScreen screen;

        public bool victory;
        public string title = "";
        public string subtitle = "";
        public string time = "00:00";
        public int level;
        public string kills = "0";
        public string gold = "0";
        public string goldLabel = ResultsScreen.GOLD_LABEL;
        public string extra = "";
        public string news = "";
        /// <summary>The weapon table, sorted by damage.</summary>
        public List<ResultRow> rows = new List<ResultRow>();

        internal ResultsModel(ResultsScreen screen)
        {
            this.screen = screen;
        }

        public void Retry() => screen.Retry();

        public void HeroSelect() => screen.OpenHeroSelect();

        public void Armory() => screen.OpenArmory();

        public void Title() => screen.OpenTitle();
    }

    /// <summary>
    /// Game over / victory: banner, time survived, level, kills and gold (Greed bonus shown) counting up, the weapon
    /// table sorted by damage (damage, DPS while owned, kills, time owned, share bar), highest hit, damage taken,
    /// unlocks and new records; Retry (same hero), Hero select, Armory, Title. Main thread.
    /// </summary>
    internal sealed class ResultsScreen : UIScreen
    {
        internal const string GOLD_LABEL = "GOLD EARNED";
        private const float COUNT_SECONDS = 1.2f;
        private const string ABANDONED_CAUSE = "Abandoned";
        /// <summary>Separates the items of the stats and news lines (RmlUi collapses runs of spaces).</summary>
        private const string SEPARATOR = " | ";

        /// <summary>A weapon's numbers before they are formatted, to sort by damage.</summary>
        private struct WeaponResult
        {
            public string icon;
            public string name;
            public float damage;
            public int kills;
            public float owned;
            public bool evolved;
            public string id;
        }

        private readonly ResultsModel model;
        private readonly List<WeaponResult> results = new List<WeaponResult>();
        private RunRecord record;
        private float countTime;
        private int targetKills;
        private int targetGold;
        private long shownKills;
        private long shownGold;

        public ResultsScreen(GameUI ui) : base(ui, "Results", "app:/data/UI/Results.rhtml")
        {
            model = new ResultsModel(this);
            BindModel("results", model);
        }

        /// <summary>Takes the finished run's record before GameUI shows the screen, and plays the victory or defeat sting.</summary>
        public void Prepare(RunRecord runRecord)
        {
            record = runRecord;
            countTime = 0.0f;
            CollectResults();
            FillRows();
            ui.PlaySound(runRecord != null && runRecord.isVictory ? "results_victory" : "results_defeat");
        }

        protected override void Refresh()
        {
            if (record == null)
            {
                return;
            }
            model.victory = record.isVictory;
            model.title = record.isVictory ? "VICTORY" : "FALLEN";
            model.subtitle = DescribeEnd(record);
            model.time = UIText.Time(record.timeSurvived);
            model.level = record.levelReached;
            targetKills = record.kills;
            targetGold = record.goldEarned;
            bool counted = countTime >= COUNT_SECONDS;
            shownKills = counted ? targetKills : 0;
            shownGold = counted ? targetGold : 0;
            model.kills = UIText.Thousands(shownKills);
            model.gold = UIText.Thousands(shownGold);
            model.goldLabel = DescribeGold(record);
            model.extra = $"Highest hit {UIText.Compact(record.highestHit)}{SEPARATOR}Damage taken {UIText.Compact(record.damageTaken)}{SEPARATOR}" +
                          $"Chests {record.chestsOpened}{SEPARATOR}Elites slain {record.elitesKilled}";
            model.news = BuildNews(run.save);
            dataModel.DirtyAll();
        }

        /// <summary>Kills and gold count up with ease-out; a counter is reformatted only when its shown value changes.</summary>
        public override void Tick(float unscaledDt)
        {
            if (countTime >= COUNT_SECONDS || record == null)
            {
                return;
            }
            countTime = MathF.Min(COUNT_SECONDS, countTime + unscaledDt);
            float eased = UIMotion.CubicOut(countTime / COUNT_SECONDS);
            long kills = (long)MathF.Round(targetKills * eased);
            if (kills != shownKills)
            {
                shownKills = kills;
                model.kills = UIText.Thousands(kills);
                dataModel.Dirty(nameof(ResultsModel.kills));
            }
            long gold = (long)MathF.Round(targetGold * eased);
            if (gold != shownGold)
            {
                shownGold = gold;
                model.gold = UIText.Thousands(gold);
                dataModel.Dirty(nameof(ResultsModel.gold));
            }
            if (countTime >= COUNT_SECONDS)
            {
                ui.PlaySound("results_tally");
            }
        }

        protected override void Back()
        {
            OpenTitle();
        }

        internal void Retry()
        {
            ui.PlaySound("ui_confirm");
            run.StartRun(run.heroClass);
        }

        internal void OpenHeroSelect()
        {
            ui.PlaySound("ui_click");
            ui.OpenHeroSelect();
        }

        internal void OpenArmory()
        {
            ui.PlaySound("ui_click");
            ui.OpenSubmenu(ui.armory);
        }

        internal void OpenTitle()
        {
            ui.PlaySound("ui_back");
            ui.BackToTitle();
        }

        private static string DescribeEnd(RunRecord record)
        {
            if (record.isVictory)
            {
                return "THE HOLLOW KING RETURNS TO THE EARTH";
            }
            if (record.deathCause == ABANDONED_CAUSE)
            {
                return "YOU LEFT THE FIELD";
            }
            return string.IsNullOrEmpty(record.deathCause) ? "THE TIDE CLAIMED YOU" : "SLAIN BY " + record.deathCause.ToUpperInvariant();
        }

        /// <summary>"GOLD EARNED", or the share Greed added: "GOLD (+120 GREED)".</summary>
        private string DescribeGold(RunRecord runRecord)
        {
            float greed = run.stats != null ? run.stats[StatId.Greed] : 1.0f;
            int greedBonus = greed > 1.0f ? (int)MathF.Round(runRecord.goldEarned - runRecord.goldEarned / greed) : 0;
            return greedBonus > 0 ? $"GOLD (+{UIText.Thousands(greedBonus)} GREED)" : GOLD_LABEL;
        }

        private static string BuildNews(SaveData save)
        {
            if (save == null)
            {
                return "";
            }
            var parts = new List<string>();
            foreach (HeroClass hero in save.newlyUnlocked)
            {
                parts.Add("NEW HERO UNLOCKED: " + UIText.HeroName(hero));
            }
            foreach (string line in save.newRecords)
            {
                parts.Add(line.ToUpperInvariant());
            }
            return string.Join(SEPARATOR, parts);
        }

        /// <summary>Every weapon's numbers, plus actives and blasts when they dealt damage, sorted by damage.</summary>
        private void CollectResults()
        {
            results.Clear();
            Loadout loadout = run.loadout;
            if (record == null)
            {
                return;
            }
            int weaponCount = loadout != null ? loadout.weapons.Count : 0;
            for (int slot = 0; slot < weaponCount && slot < RunRecord.MAX_SOURCES; slot++)
            {
                SlotView view = loadout.weapons[slot];
                results.Add(new WeaponResult
                {
                    icon = UIText.IconOr(view.icon, view.id),
                    name = string.IsNullOrEmpty(view.name) ? loadout.GetWeaponName(slot) : view.name,
                    damage = record.damageBySource[slot],
                    kills = record.killsBySource[slot],
                    owned = MathF.Max(1.0f, record.timeSurvived - record.ownedSince[slot]),
                    evolved = view.isEvolved,
                    id = view.id,
                });
            }
            float other = record.damageBySource[RunRecord.OTHER_SOURCE];
            if (other > 0.0f)
            {
                results.Add(new WeaponResult
                {
                    icon = UIText.Icon(UIText.PLACEHOLDER_ICON),
                    name = "Actives and blasts",
                    damage = other,
                    kills = record.killsBySource[RunRecord.OTHER_SOURCE],
                    owned = MathF.Max(1.0f, record.timeSurvived),
                });
            }
            results.Sort((a, b) => b.damage.CompareTo(a.damage));
        }

        private void FillRows()
        {
            model.rows.Clear();
            float best = results.Count > 0 ? MathF.Max(results[0].damage, 1.0f) : 1.0f;
            for (int i = 0; i < results.Count; i++)
            {
                WeaponResult result = results[i];
                var row = new ResultRow
                {
                    icon = result.icon,
                    name = result.name,
                    damage = UIText.Thousands((long)result.damage),
                    dps = UIText.Compact(result.damage / result.owned),
                    kills = UIText.Thousands(result.kills),
                    owned = UIText.Time(result.owned),
                    shareStyle = "width: " + (result.damage / best * 100.0f).ToString("0.#", CultureInfo.InvariantCulture) + "%;",
                    top = i == 0 && result.damage > 0.0f,
                    evolved = result.evolved,
                };
                row.SetOwnership(result.id);
                model.rows.Add(row);
            }
        }
    }
}

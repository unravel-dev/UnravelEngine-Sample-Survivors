using System;
using System.Collections.Generic;
using System.Globalization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>A weapon or passive row of the pause sheet.</summary>
    public sealed class BuildRow
    {
        public string icon = "";
        public string name = "";
        public string rank = "";
        public string line = "";
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

    /// <summary>One stat of the pause sheet.</summary>
    public sealed class StatLine
    {
        public string name = "";
        public string value = "";
        /// <summary>Above the class's base value (shown green).</summary>
        public bool boosted;
    }

    /// <summary>The pause data model ("pause").</summary>
    public sealed class PauseModel
    {
        private readonly PauseScreen screen;

        public string summary = "";
        public List<BuildRow> weapons = new List<BuildRow>();
        public List<BuildRow> passives = new List<BuildRow>();
        public List<StatLine> stats = new List<StatLine>();
        public string abandonText = PauseScreen.ABANDON_TEXT;
        public bool confirmAbandon;
        public string damageNote = "";

        internal PauseModel(PauseScreen screen)
        {
            this.screen = screen;
        }

        public void Resume() => screen.ResumeRun();

        public void Settings() => screen.OpenSettings();

        public void Abandon() => screen.AbandonRun();
    }

    /// <summary>
    /// Pause and build summary: each weapon with its rank and damage / DPS / kills so far, each passive and boon
    /// with its effect at its rank, the full stat sheet (armor as damage reduction, boosted stats green), and
    /// Resume, Settings and Abandon (pressed twice). Cancel or Pause resumes. Main thread.
    /// </summary>
    internal sealed class PauseScreen : UIScreen
    {
        internal const string ABANDON_TEXT = "ABANDON RUN";
        private const string CONFIRM_ABANDON_TEXT = "CONFIRM: ABANDON";
        /// <summary>A stat counts as boosted when it differs from the base value by more than this.</summary>
        private const float BOOST_EPSILON = 1e-4f;

        private readonly PauseModel model;

        public PauseScreen(GameUI ui) : base(ui, "Pause", "app:/data/UI/Pause.rhtml")
        {
            model = new PauseModel(this);
            BindModel("pause", model);
        }

        protected override bool closesOnPause => true;

        protected override void Refresh()
        {
            model.confirmAbandon = false;
            model.abandonText = ABANDON_TEXT;
            model.summary = $"{UIText.HeroName(run.heroClass)} | {UIText.Time(run.runTime)} | LEVEL {run.level} | {UIText.Thousands(run.record.kills)} KILLS";
            FillWeapons();
            FillPassives();
            FillStats();
            float other = run.record.damageBySource[RunRecord.OTHER_SOURCE];
            model.damageNote = other > 0.0f ? $"Actives and blasts: {UIText.Compact(other)} damage" : "";
            dataModel.DirtyAll();
        }

        protected override void Back()
        {
            ResumeRun();
        }

        internal void ResumeRun()
        {
            ui.PlaySound("ui_back");
            run.Resume();
        }

        internal void OpenSettings()
        {
            ui.PlaySound("ui_click");
            ui.OpenSubmenu(ui.settings);
        }

        internal void AbandonRun()
        {
            if (!model.confirmAbandon)
            {
                model.confirmAbandon = true;
                model.abandonText = CONFIRM_ABANDON_TEXT;
                ui.PlaySound("ui_error");
                dataModel.Dirty(nameof(PauseModel.confirmAbandon));
                dataModel.Dirty(nameof(PauseModel.abandonText));
                return;
            }
            ui.PlaySound("ui_click");
            run.AbandonRun();
        }

        private void FillWeapons()
        {
            model.weapons.Clear();
            Loadout loadout = run.loadout;
            if (loadout == null)
            {
                return;
            }
            for (int slot = 0; slot < loadout.weapons.Count; slot++)
            {
                SlotView view = loadout.weapons[slot];
                var row = new BuildRow
                {
                    icon = UIText.IconOr(view.icon, view.id),
                    name = string.IsNullOrEmpty(view.name) ? loadout.GetWeaponName(slot) : view.name,
                    rank = view.isEvolved ? "EVOLVED" : $"RANK {view.rank} / {view.maxRank}",
                    line = DescribeWeaponDamage(slot),
                    evolved = view.isEvolved,
                };
                row.SetOwnership(view.id);
                model.weapons.Add(row);
            }
        }

        /// <summary>"1.2k damage | 35 DPS | 120 kills": damage per second over the time the weapon was owned.</summary>
        private string DescribeWeaponDamage(int slot)
        {
            RunRecord record = run.record;
            bool isTracked = slot < RunRecord.MAX_SOURCES;
            float damage = isTracked ? record.damageBySource[slot] : 0.0f;
            int kills = isTracked ? record.killsBySource[slot] : 0;
            float owned = MathF.Max(1.0f, run.runTime - (isTracked ? record.ownedSince[slot] : 0.0f));
            return $"{UIText.Compact(damage)} damage | {UIText.Compact(damage / owned)} DPS | {UIText.Thousands(kills)} kills";
        }

        private void FillPassives()
        {
            model.passives.Clear();
            Loadout loadout = run.loadout;
            if (loadout == null)
            {
                return;
            }
            foreach (SlotView view in loadout.passives)
            {
                var row = new BuildRow
                {
                    icon = UIText.IconOr(view.icon, view.id),
                    name = view.name ?? "",
                    rank = $"{view.rank} / {view.maxRank}",
                    line = view.summary ?? "",
                };
                row.SetOwnership(view.id);
                model.passives.Add(row);
            }
            AddBoonRows(loadout);
        }

        /// <summary>A row per boon taken at least once, with its total effect.</summary>
        private void AddBoonRows(Loadout loadout)
        {
            for (int i = 0; i < WeaponCatalog.boons.Length; i++)
            {
                int picks = loadout.GetBoonPicks(i);
                if (picks <= 0)
                {
                    continue;
                }
                BoonDef boon = WeaponCatalog.boons[i];
                var row = new BuildRow
                {
                    icon = UIText.Icon(boon.id),
                    name = boon.name,
                    rank = "x" + UIText.Int(picks),
                    line = WeaponText.DescribeBoonTotal(boon, picks),
                };
                row.SetOwnership(boon.id);
                model.passives.Add(row);
            }
        }

        private void FillStats()
        {
            model.stats.Clear();
            PlayerStats stats = run.stats;
            if (stats == null)
            {
                return;
            }
            HeroController hero = run.hero;
            float maxHealth = stats[StatId.MaxHealth];
            string health = hero != null ? $"{hero.currentHealth:0} / {maxHealth:0}" : $"{maxHealth:0}";
            AddStat("Max Health", health, StatId.MaxHealth);
            AddStat("Regeneration", $"{stats[StatId.Regeneration]:0.0} HP/s", StatId.Regeneration);
            AddStat("Armor", $"{stats[StatId.Armor]:0} (-{stats.armorReduction * 100.0f:0}% dmg)", StatId.Armor);
            AddStat("Move Speed", $"{stats[StatId.MoveSpeed]:0.0} m/s", StatId.MoveSpeed);
            AddStat("Might", Percent(stats[StatId.Might]), StatId.Might);
            AddStat("Area", Percent(stats[StatId.Area]), StatId.Area);
            AddStat("Cooldown", Percent(stats[StatId.Cooldown]), StatId.Cooldown, lowerIsBetter: true);
            AddStat("Amount", "+" + UIText.Int(stats.GetCount(StatId.Amount)), StatId.Amount);
            AddStat("Projectile Speed", Percent(stats[StatId.ProjectileSpeed]), StatId.ProjectileSpeed);
            AddStat("Duration", Percent(stats[StatId.Duration]), StatId.Duration);
            AddStat("Pickup Radius", $"{stats[StatId.PickupRadius]:0.0} m", StatId.PickupRadius);
            AddStat("Luck", $"{stats[StatId.Luck]:0}", StatId.Luck);
            AddStat("Growth", Percent(stats[StatId.Growth]), StatId.Growth);
            AddStat("Greed", Percent(stats[StatId.Greed]), StatId.Greed);
            AddStat("Crit Chance", Percent(stats[StatId.CritChance]), StatId.CritChance);
            AddStat("Crit Damage", Percent(stats[StatId.CritDamage]), StatId.CritDamage);
            AddStat("Knockback", Percent(stats[StatId.Knockback]), StatId.Knockback);
            if (stats[StatId.BlockChance] > 0.0f)
            {
                AddStat("Block Chance", Percent(stats[StatId.BlockChance]), StatId.BlockChance);
            }
            AddStat("Revival", UIText.Int(stats.GetCount(StatId.Revival)), StatId.Revival);
            AddStat("Reroll", UIText.Int(stats.GetCount(StatId.Reroll)), StatId.Reroll);
            AddStat("Banish", UIText.Int(stats.GetCount(StatId.Banish)), StatId.Banish);
        }

        private void AddStat(string name, string value, StatId stat, bool lowerIsBetter = false)
        {
            PlayerStats stats = run.stats;
            float current = stats[stat];
            float baseValue = stats.GetBase(stat);
            bool boosted = lowerIsBetter ? current < baseValue - BOOST_EPSILON : current > baseValue + BOOST_EPSILON;
            model.stats.Add(new StatLine { name = name, value = value, boosted = boosted });
        }

        private static string Percent(float multiplier) => (multiplier * 100.0f).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}

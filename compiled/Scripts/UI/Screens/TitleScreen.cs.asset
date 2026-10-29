using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The title data model ("title"): the banked gold, the best run, and the menu's buttons.</summary>
    public sealed class TitleModel
    {
        private readonly TitleScreen screen;

        public string gold = "0";
        public string best = TitleScreen.NO_RUNS_TEXT;

        internal TitleModel(TitleScreen screen)
        {
            this.screen = screen;
        }

        public void Play() => screen.Play();

        public void Armory() => screen.OpenArmory();

        public void Settings() => screen.OpenSettings();

        public void Quit() => screen.Quit();
    }

    /// <summary>
    /// Title: Play leads to hero select, Armory and Settings open over it, Quit leaves the app (hidden on touch).
    /// Shows the banked gold and the best run. The smallest example of a screen with a data model: the document
    /// binds <see cref="TitleModel"/>'s fields with {{ }} and its methods with data-event-click. Main thread.
    /// </summary>
    internal sealed class TitleScreen : UIScreen
    {
        internal const string NO_RUNS_TEXT = "No runs yet. The tide is waiting.";
        /// <summary>Separates the items of the best-run line (RmlUi collapses runs of spaces).</summary>
        private const string SEPARATOR = " | ";

        private readonly TitleModel model;

        public TitleScreen(GameUI ui) : base(ui, "Title", "app:/data/UI/Title.rhtml")
        {
            model = new TitleModel(this);
            BindModel("title", model);
        }

        protected override void Refresh()
        {
            SaveData save = run.save;
            if (save == null)
            {
                return;
            }
            model.gold = UIText.Thousands(save.gold);
            model.best = DescribeBest(save);
            dataModel.DirtyAll();
        }

        internal void Play()
        {
            ui.PlaySound("ui_click");
            ui.OpenHeroSelect();
        }

        internal void OpenArmory()
        {
            ui.PlaySound("ui_click");
            ui.OpenSubmenu(ui.armory);
        }

        internal void OpenSettings()
        {
            ui.PlaySound("ui_click");
            ui.OpenSubmenu(ui.settings);
        }

        internal void Quit()
        {
            ui.PlaySound("ui_click");
            Application.Quit();
        }

        /// <summary>Victories when there are any, else the longest stand; with the run count and the best level.</summary>
        private static string DescribeBest(SaveData save)
        {
            if (save.runs == 0)
            {
                return NO_RUNS_TEXT;
            }
            string record = save.victories > 0 ? $"Victories: {save.victories}" : $"Longest stand: {UIText.Time(GetBestTime(save))}";
            return $"{record}{SEPARATOR}Runs: {save.runs}{SEPARATOR}Best level: {save.bestLevel}";
        }

        private static float GetBestTime(SaveData save)
        {
            float bestTime = 0.0f;
            foreach (HeroRecord hero in save.heroes)
            {
                bestTime = MathF.Max(bestTime, hero.bestTime);
            }
            return bestTime;
        }
    }
}

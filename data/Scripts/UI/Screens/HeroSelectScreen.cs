using System;
using System.Collections.Generic;
using System.Globalization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>One hero card as the "heroes" data model shows it.</summary>
    public sealed class HeroCardModel
    {
        public string name = "";
        public string title = "";
        public string portrait = "";
        public bool locked;
        public string hint = "";
        public string progress = "";
        public bool[] hp = new bool[HeroSelectScreen.SEGMENTS];
        public bool[] armor = new bool[HeroSelectScreen.SEGMENTS];
        public bool[] speed = new bool[HeroSelectScreen.SEGMENTS];
        public string hpText = "";
        public string armorText = "";
        public string speedText = "";
        public string weaponName = "";
        public string weaponIcon = "";
        public string passiveName = "";
        public string passiveText = "";
        public string passiveIcon = "";
        public string activeName = "";
        public string activeText = "";
        public string activeIcon = "";
        public bool hasSkulls;
        public bool[] skulls = new bool[SaveData.MAX_DREAD];
        public string best = "";
    }

    /// <summary>The hero select data model ("heroes"): cards, selection, Dread, and the events of its buttons.</summary>
    public sealed class HeroSelectModel
    {
        private readonly HeroSelectScreen screen;

        public List<HeroCardModel> heroes = new List<HeroCardModel>();
        public int selected;
        public bool dreadUnlocked;
        public int dread;
        public string dreadText = "";
        public bool canStart = true;
        public string startText = "BEGIN";

        internal HeroSelectModel(HeroSelectScreen screen)
        {
            this.screen = screen;
        }

        public void Select(int index) => screen.Select(index, true);

        public void ShowWeapon(int index) => screen.ShowAbility(index, DetailsKind.Weapon);

        public void ShowPassive(int index) => screen.ShowAbility(index, DetailsKind.Passive);

        public void ShowActive(int index) => screen.ShowAbility(index, DetailsKind.Active);

        public void Start() => screen.StartRun();

        public void Back() => screen.GoBack();

        public void DreadUp() => screen.ChangeDread(1);

        public void DreadDown() => screen.ChangeDread(-1);
    }

    /// <summary>
    /// Hero select: four class cards from <see cref="HeroCatalog"/> with HP / Armor / Speed bars, the signature
    /// passive, the active and the starting weapon; locked classes show the unlock condition (Run.IsUnlocked
    /// decides). Moving the focus or clicking a card selects it (raising GameUI.heroPreviewChanged for a
    /// turntable), a second click or Submit starts the run with Run.StartRun; a click on an ability row selects
    /// the card and opens that ability's details instead. Dread 0-5 after the first victory. Main thread.
    /// </summary>
    internal sealed class HeroSelectScreen : UIScreen
    {
        internal const int SEGMENTS = 8;
        private const int HERO_COUNT = (int)HeroClass.Count;
        /// <summary>The stat bars span these values: the lowest lights one segment, the highest all.</summary>
        private const float HEALTH_LOW = 60.0f;
        private const float HEALTH_HIGH = 150.0f;
        private const float ARMOR_LOW = -1.0f;
        private const float ARMOR_HIGH = 4.0f;
        private const float SPEED_LOW = 4.3f;
        private const float SPEED_HIGH = 5.5f;
        /// <summary>Shown for a class whose catalog entry has no health / speed.</summary>
        private const float DEFAULT_HEALTH = 100.0f;
        private const float DEFAULT_SPEED = 5.0f;
        private const float PERCENT = 100.0f;
        private const string HERO_CARD_CLASS = "hero-card";
        private const string ABILITY_SELECTOR = ".ability";
        private const int ABILITY_ROWS = 3;

        private readonly HeroSelectModel model;
        /// <summary>The frame of the last click on an ability row: the click bubbles on to the card's Select.</summary>
        private long abilityClickFrame = -1;

        public HeroSelectScreen(GameUI ui) : base(ui, "HeroSelect", "app:/data/UI/HeroSelect.rhtml")
        {
            model = new HeroSelectModel(this);
            for (int i = 0; i < HERO_COUNT; i++)
            {
                model.heroes.Add(new HeroCardModel());
            }
            BindModel("heroes", model);
        }

        protected override void Refresh()
        {
            SaveData save = run.save;
            for (int i = 0; i < HERO_COUNT; i++)
            {
                FillCard(model.heroes[i], (HeroClass)i, save);
            }
            int preferred = save != null ? save.lastHero : 0;
            model.selected = run.IsUnlocked((HeroClass)preferred) ? preferred : 0;
            model.dreadUnlocked = save != null && save.isDreadUnlocked;
            model.dread = save != null ? save.selectedDread : 0;
            UpdateDreadText();
            UpdateStart();
            dataModel.DirtyAll();
            ui.NotifyHeroPreview((HeroClass)model.selected);
        }

        /// <summary>The focus follows the keyboard / gamepad onto a card: that card is selected.</summary>
        protected override void OnNavigated()
        {
            int focusedHero = GetFocusedHero();
            if (focusedHero >= 0)
            {
                Select(focusedHero, false);
            }
        }

        /// <summary>Submit on a card starts the run with it; on a button clicks the button.</summary>
        protected override void Activate()
        {
            int focusedHero = GetFocusedHero();
            if (focusedHero < 0)
            {
                base.Activate();
                return;
            }
            Select(focusedHero, false);
            StartRun();
        }

        protected override void Back()
        {
            GoBack();
        }

        protected override int GetDefaultFocus() => model.selected;

        internal void Select(int index, bool fromClick)
        {
            if (index < 0 || index >= HERO_COUNT)
            {
                return;
            }
            bool isAbilityClick = Time.frameCount == abilityClickFrame;
            if (fromClick && !isAbilityClick && index == model.selected && !model.heroes[index].locked)
            {
                StartRun();
                return;
            }
            if (index == model.selected)
            {
                return;
            }
            model.selected = index;
            ui.PlaySound("ui_select");
            ui.NotifyHeroPreview((HeroClass)index);
            UpdateStart();
            dataModel.Dirty(nameof(HeroSelectModel.selected));
            dataModel.Dirty(nameof(HeroSelectModel.canStart));
            dataModel.Dirty(nameof(HeroSelectModel.startText));
        }

        internal void ShowAbility(int index, DetailsKind kind)
        {
            abilityClickFrame = Time.frameCount;
            ui.OpenHeroDetails(new DetailsRequest { kind = kind, hero = (HeroClass)index });
        }

        /// <summary>Dev: clicks an ability row of an unlocked card (0 weapon, 1 signature, 2 active), as a tap does.</summary>
        internal void DevClickAbility(int hero, int row) => QueryVisible(ABILITY_SELECTOR)[hero * ABILITY_ROWS + row].Click();

        /// <summary>Dev: clicks a card, as a tap on its portrait does.</summary>
        internal void DevClickCard(int hero) => QueryVisible("." + HERO_CARD_CLASS)[hero].Click();

        internal void StartRun()
        {
            HeroClass chosen = (HeroClass)model.selected;
            if (!run.IsUnlocked(chosen))
            {
                Deny(root?.Q("#hs-start"));
                return;
            }
            SaveData save = run.save;
            if (save != null)
            {
                save.lastHero = model.selected;
                save.selectedDread = model.dreadUnlocked ? model.dread : 0;
                save.Save();
            }
            ui.PlaySound("ui_confirm");
            run.StartRun(chosen);
        }

        internal void GoBack()
        {
            ui.PlaySound("ui_back");
            ui.BackToTitle();
        }

        internal void ChangeDread(int delta)
        {
            if (!model.dreadUnlocked)
            {
                return;
            }
            int next = Math.Clamp(model.dread + delta, 0, SaveData.MAX_DREAD);
            if (next == model.dread)
            {
                ui.PlaySound("ui_error");
                return;
            }
            model.dread = next;
            ui.PlaySound("ui_click");
            UpdateDreadText();
            dataModel.Dirty(nameof(HeroSelectModel.dread));
            dataModel.Dirty(nameof(HeroSelectModel.dreadText));
        }

        private void FillCard(HeroCardModel card, HeroClass heroClass, SaveData save)
        {
            HeroInfo info = HeroCatalog.Get(heroClass) ?? new HeroInfo { heroClass = heroClass, name = heroClass.ToString() };
            card.name = UIText.HeroName(heroClass);
            card.title = (info.title ?? "").ToUpperInvariant();
            card.portrait = UIText.Icon(UIText.HeroId(heroClass) + "_portrait");
            card.locked = !run.IsUnlocked(heroClass);
            FillUnlockHint(card, heroClass, info, save);
            FillStats(card, info);
            FillAbilities(card, info);
            FillRecord(card, save != null ? save.GetHero(heroClass) : new HeroRecord());
        }

        /// <summary>The unlock condition and, while the hero is locked, the progress toward a level goal.</summary>
        private static void FillUnlockHint(HeroCardModel card, HeroClass heroClass, HeroInfo info, SaveData save)
        {
            card.hint = info.unlockHint ?? "";
            card.progress = card.locked && save != null ? save.GetLevelGoalProgress(heroClass) : "";
        }

        private static void FillStats(HeroCardModel card, HeroInfo info)
        {
            float health = info.maxHealth > 0.0f ? info.maxHealth : DEFAULT_HEALTH;
            float speed = info.moveSpeed > 0.0f ? info.moveSpeed : DEFAULT_SPEED;
            FillSegments(card.hp, (health - HEALTH_LOW) / (HEALTH_HIGH - HEALTH_LOW));
            FillSegments(card.armor, (info.armor - ARMOR_LOW) / (ARMOR_HIGH - ARMOR_LOW));
            FillSegments(card.speed, (speed - SPEED_LOW) / (SPEED_HIGH - SPEED_LOW));
            card.hpText = health.ToString("0", CultureInfo.InvariantCulture);
            card.armorText = info.armor.ToString("0", CultureInfo.InvariantCulture);
            card.speedText = speed.ToString("0.0", CultureInfo.InvariantCulture) + " m/s";
        }

        private static void FillAbilities(HeroCardModel card, HeroInfo info)
        {
            card.weaponName = info.startWeaponName ?? "";
            card.weaponIcon = UIText.IconOr(info.startWeaponIcon, UIText.ToId(info.startWeaponName));
            card.passiveName = info.passiveName ?? "";
            card.passiveText = info.passiveText ?? "";
            card.passiveIcon = UIText.Icon(string.IsNullOrEmpty(info.passiveName) ? UIText.PLACEHOLDER_ICON : UIText.ToId(info.passiveName));
            card.activeName = info.activeName ?? "";
            card.activeText = info.activeText ?? "";
            card.activeIcon = UIText.Icon(string.IsNullOrEmpty(info.activeName) ? UIText.PLACEHOLDER_ICON : UIText.ToId(info.activeName));
        }

        /// <summary>Dread skulls cleared (after a victory) and the best run.</summary>
        private static void FillRecord(HeroCardModel card, HeroRecord record)
        {
            card.hasSkulls = record.victories > 0;
            for (int s = 0; s < card.skulls.Length; s++)
            {
                card.skulls[s] = s < record.highestDreadCleared;
            }
            card.best = record.runs == 0 ? "No runs yet." :
                        $"Best {UIText.Time(record.bestTime)} | Level {record.bestLevel} | {UIText.Thousands(record.bestKills)} kills";
        }

        private static void FillSegments(bool[] segments, float fraction)
        {
            int lit = Math.Clamp((int)MathF.Round(fraction * segments.Length), 1, segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = i < lit;
            }
        }

        private void UpdateStart()
        {
            HeroCardModel card = model.heroes[Math.Clamp(model.selected, 0, HERO_COUNT - 1)];
            model.canStart = !card.locked;
            model.startText = card.locked ? "LOCKED" : "BEGIN AS " + card.name;
        }

        private void UpdateDreadText()
        {
            int dread = model.dread;
            float health = WaveDirector.DREAD_HEALTH_PER_LEVEL * dread * PERCENT;
            float damage = WaveDirector.DREAD_DAMAGE_PER_LEVEL * dread * PERCENT;
            float spawns = WaveDirector.DREAD_BUDGET_PER_LEVEL * dread * PERCENT;
            float gold = WaveDirector.DREAD_GOLD_PER_LEVEL * dread * PERCENT;
            model.dreadText = dread == 0 ? "The tide as it was." :
                              $"Enemies: health +{health:0}%, damage +{damage:0}%, numbers +{spawns:0}%. Gold +{gold:0}%.";
        }

        private int GetFocusedHero()
        {
            UIElement focused = nav.focused;
            if (focused == null || !focused.HasClass(HERO_CARD_CLASS))
            {
                return -1;
            }
            List<UIElement> cards = QueryVisible("." + HERO_CARD_CLASS);
            for (int i = 0; i < cards.Count; i++)
            {
                if (ReferenceEquals(cards[i], focused))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}

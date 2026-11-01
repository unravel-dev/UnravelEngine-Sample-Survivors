using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>One HUD slot as the "hud" data model shows it.</summary>
    public sealed class HudSlot
    {
        public bool filled;
        public string icon = "";
        public bool evolved;
        public bool canEvolve;
        public bool showPips;
        /// <summary>One entry per possible rank; lit up to the current rank.</summary>
        public bool[] pips = Array.Empty<bool>();
        internal string id = "";
        internal int rank;
        internal int maxRank;
    }

    /// <summary>The values and events the HUD document binds (model "hud"). Changed values are dirtied one by one.</summary>
    public sealed class HudModel
    {
        private readonly HudScreen screen;

        public int level = 1;
        public string time = "00:00";
        public int kills;
        public int gold;
        public List<HudSlot> weapons = CreateSlots(Loadout.WEAPON_SLOTS);
        public List<HudSlot> passives = CreateSlots(Loadout.PASSIVE_SLOTS);
        public string activeIcon = UIText.Icon(UIText.PLACEHOLDER_ICON);

        internal HudModel(HudScreen screen)
        {
            this.screen = screen;
        }

        /// <summary>A weapon slot was tapped / clicked: pause and show its details.</summary>
        public void OpenWeapon(int index) => screen.OpenDetails(DetailsKind.Weapon, index);

        /// <summary>A passive slot was tapped / clicked.</summary>
        public void OpenPassive(int index) => screen.OpenDetails(DetailsKind.Passive, index);

        /// <summary>The active's info badge was tapped / clicked.</summary>
        public void OpenActive() => screen.OpenDetails(DetailsKind.Active, 0);

        /// <summary>The pause button was tapped / clicked.</summary>
        public void Pause() => screen.PauseRun();

        /// <summary>The active ability's button was pressed (it fires on press, not on release).</summary>
        public void UseActive() => screen.UseActive();

        private static List<HudSlot> CreateSlots(int count)
        {
            var slots = new List<HudSlot>(count);
            for (int i = 0; i < count; i++)
            {
                slots.Add(new HudSlot());
            }
            return slots;
        }
    }

    /// <summary>
    /// The in-run HUD: XP bar with an eased fill and a flash before each level-up, level, timer, kill counter (pops
    /// every 10 kills), gold, weapon and passive slots with rank pips (gold frame when evolved, pulse when an
    /// evolution is ready), the active ability with its radial cooldown (tap it on touch) and a pause button.
    /// Values are compared with the last shown ones and only changes reach the document. Main thread.
    /// </summary>
    internal sealed class HudScreen : UIScreen
    {
        private const float XP_EASE_SECONDS = 0.05f;
        private const float XP_FILL_SPEED = 5.0f;
        private const int KILL_POP_STEP = 10;
        /// <summary>The cooldown sweep moves in steps of 1 / SWEEP_STEPS (fewer redraws of the radial bar).</summary>
        private const float SWEEP_STEPS = 100.0f;
        /// <summary>The fallback card whose icon flies to the gold counter instead of the level badge.</summary>
        private const string COIN_PURSE_ID = "coin_purse";

        private readonly HudModel model;
        private UIElement xpFlash;
        private UIElement levelBadge;
        private UIElement killsWrap;
        private UIElement goldWrap;
        private UIElement active;
        private UIElement activeRing;
        private ProgressWriter xpBar;
        private ProgressWriter activeSweep;
        private float xpTarget;
        private float xpShown;
        private bool xpFillingToLevel;
        private int lastSeconds = -1;
        private bool activeWasReady = true;
        private bool hasActive;
        private bool isModal;
        private Loadout boundLoadout;
        private string shownActiveIcon;
        private string shownActiveName;

        public HudScreen(GameUI ui) : base(ui, "Hud", "app:/data/UI/Hud.rhtml")
        {
            model = new HudModel(this);
            BindModel("hud", model);
        }

        protected override void OnLoaded(UIDocument loaded)
        {
            xpBar.Bind(loaded.Q<UIProgress>("#xp"));
            xpFlash = loaded.Q("#xp-flash");
            levelBadge = loaded.Q("#level-badge");
            killsWrap = loaded.Q("#kills-wrap");
            goldWrap = loaded.Q("#gold-wrap");
            active = loaded.Q("#active");
            activeRing = loaded.Q("#active-ring");
            activeSweep.Bind(loaded.Q<UIProgress>("#active-sweep"));
            SetModal(isModal);
            if (active != null)
            {
                active.visible = hasActive;
            }
        }

        public override void Show()
        {
            base.Show();
            if (run.loadout != boundLoadout)
            {
                ResetForRun();
            }
        }

        /// <summary>Behind a card picker, chest or details panel the HUD buttons stop taking clicks.</summary>
        public void SetModal(bool modal)
        {
            isModal = modal;
            root?.SetClass("modal", modal);
        }

        /// <summary>Opens the details of an owned item (paused); ignored behind a card picker or chest.</summary>
        internal void OpenDetails(DetailsKind kind, int index)
        {
            if (isModal || run.phase != RunPhase.Playing || run.loadout == null)
            {
                return;
            }
            int count = kind == DetailsKind.Weapon ? run.loadout.weapons.Count : kind == DetailsKind.Passive ? run.loadout.passives.Count : 1;
            if (index < 0 || index >= count)
            {
                return;
            }
            ui.OpenDetails(new DetailsRequest { kind = kind, slot = index });
        }

        internal void PauseRun()
        {
            if (run.phase == RunPhase.Playing)
            {
                ui.PlaySound("ui_click");
                run.Pause();
            }
        }

        internal void UseActive()
        {
            if (run.phase == RunPhase.Playing && run.loadout != null && !isModal)
            {
                run.loadout.TryActivate();
            }
        }

        /// <summary>Reads the run; only changed values are written.</summary>
        public void UpdateFromRun()
        {
            if (run.loadout != boundLoadout)
            {
                ResetForRun();
                return;
            }
            UpdateLevel();
            UpdateCounters();
            UpdateSlots(run.loadout?.weapons, model.weapons, nameof(HudModel.weapons));
            UpdateSlots(run.loadout?.passives, model.passives, nameof(HudModel.passives));
            UpdateActive();
        }

        public override void Tick(float unscaledDt)
        {
            if (xpFillingToLevel)
            {
                xpShown = MathF.Min(1.0f, xpShown + XP_FILL_SPEED * unscaledDt);
                if (xpShown >= 1.0f)
                {
                    xpFillingToLevel = false;
                    xpFlash?.RestartAnimation();
                    xpShown = 0.0f;
                }
            }
            else
            {
                xpShown += (xpTarget - xpShown) * (1.0f - MathF.Exp(-unscaledDt / XP_EASE_SECONDS));
            }
            xpBar.Write(xpShown);
        }

        /// <summary>
        /// The on-screen rectangle (document pixels) a pick's icon flies to: the item's slot (or the next free one)
        /// for weapons, passives and evolutions; the gold counter for Coin Purse; the level badge for the other
        /// slotless picks (blessings and boons).
        /// </summary>
        public bool TryGetSlotRect(UpgradeOffer offer, out Rect rect)
        {
            rect = default;
            if (!isReady || offer == null || run.loadout == null)
            {
                return false;
            }
            UIElement target = IsSlotless(offer) ? (offer.id == COIN_PURSE_ID ? goldWrap : levelBadge) : FindSlotFrame(offer);
            if (target == null)
            {
                return false;
            }
            rect = target.rect;
            return true;
        }

        private static bool IsSlotless(UpgradeOffer offer) => offer.kind == UpgradeOffer.Kind.Fallback || offer.kind == UpgradeOffer.Kind.Boon;

        private UIElement FindSlotFrame(UpgradeOffer offer)
        {
            bool isPassive = offer.kind == UpgradeOffer.Kind.Passive;
            IReadOnlyList<SlotView> owned = isPassive ? run.loadout.passives : run.loadout.weapons;
            int index = owned.Count;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].id == offer.id)
                {
                    index = i;
                    break;
                }
            }
            List<UIElement> slots = QueryVisible(isPassive ? ".slot.passive" : ".slot.weapon");
            return slots.Count > 0 ? slots[Math.Min(index, slots.Count - 1)].Q(".slot-frame") : null;
        }

        private void ResetForRun()
        {
            boundLoadout = run.loadout;
            xpShown = 0.0f;
            xpTarget = 0.0f;
            xpFillingToLevel = false;
            lastSeconds = -1;
            model.level = run.level;
            model.kills = 0;
            model.gold = 0;
            foreach (HudSlot slot in model.weapons)
            {
                ClearSlot(slot);
            }
            foreach (HudSlot slot in model.passives)
            {
                ClearSlot(slot);
            }
            activeWasReady = true;
            shownActiveIcon = null;
            shownActiveName = null;
            dataModel.DirtyAll();
            UpdateFromRun();
        }

        private void UpdateLevel()
        {
            xpTarget = Math.Clamp(run.experienceFraction, 0.0f, 1.0f);
            if (run.level == model.level)
            {
                return;
            }
            if (run.level > model.level)
            {
                xpFillingToLevel = true;
            }
            model.level = run.level;
            dataModel.Dirty(nameof(HudModel.level));
        }

        private void UpdateCounters()
        {
            int seconds = (int)run.runTime;
            if (seconds != lastSeconds)
            {
                lastSeconds = seconds;
                model.time = UIText.Time(seconds);
                dataModel.Dirty(nameof(HudModel.time));
            }
            int kills = run.record.kills;
            if (kills != model.kills)
            {
                if (kills / KILL_POP_STEP != model.kills / KILL_POP_STEP)
                {
                    killsWrap?.RestartAnimation();
                }
                model.kills = kills;
                dataModel.Dirty(nameof(HudModel.kills));
            }
            if (run.gold != model.gold)
            {
                if (run.gold > model.gold)
                {
                    goldWrap?.RestartAnimation();
                }
                model.gold = run.gold;
                dataModel.Dirty(nameof(HudModel.gold));
            }
        }

        private void UpdateSlots(IReadOnlyList<SlotView> source, List<HudSlot> slots, string variable)
        {
            bool changed = false;
            int count = source != null ? source.Count : 0;
            for (int i = 0; i < slots.Count; i++)
            {
                HudSlot slot = slots[i];
                if (i >= count)
                {
                    if (slot.filled)
                    {
                        ClearSlot(slot);
                        changed = true;
                    }
                    continue;
                }
                SlotView view = source[i];
                if (slot.filled && slot.id == view.id && slot.rank == view.rank && slot.maxRank == view.maxRank &&
                    slot.evolved == view.isEvolved && slot.canEvolve == view.canEvolve)
                {
                    continue;
                }
                FillSlot(slot, view);
                changed = true;
            }
            if (changed)
            {
                dataModel.Dirty(variable);
            }
        }

        private static void FillSlot(HudSlot slot, SlotView view)
        {
            slot.filled = true;
            slot.id = view.id ?? "";
            slot.icon = UIText.IconOr(view.icon, view.id);
            slot.rank = view.rank;
            slot.maxRank = Math.Max(1, view.maxRank);
            slot.evolved = view.isEvolved;
            slot.canEvolve = view.canEvolve && !view.isEvolved;
            slot.showPips = !view.isEvolved;
            if (slot.pips.Length != slot.maxRank)
            {
                slot.pips = new bool[slot.maxRank];
            }
            for (int p = 0; p < slot.pips.Length; p++)
            {
                slot.pips[p] = p < view.rank;
            }
        }

        private static void ClearSlot(HudSlot slot)
        {
            slot.filled = false;
            slot.id = "";
            slot.rank = 0;
            slot.maxRank = 0;
            slot.evolved = false;
            slot.canEvolve = false;
            slot.showPips = false;
            slot.pips = Array.Empty<bool>();
        }

        private void UpdateActive()
        {
            Loadout loadout = run.loadout;
            bool shouldShow = loadout != null && !string.IsNullOrEmpty(loadout.activeName);
            if (shouldShow != hasActive)
            {
                hasActive = shouldShow;
                if (active != null)
                {
                    active.visible = hasActive;
                }
            }
            if (!hasActive)
            {
                return;
            }
            if (!ReferenceEquals(loadout.activeIcon, shownActiveIcon) || !ReferenceEquals(loadout.activeName, shownActiveName))
            {
                shownActiveIcon = loadout.activeIcon;
                shownActiveName = loadout.activeName;
                model.activeIcon = UIText.IconOr(shownActiveIcon, UIText.ToId(shownActiveName));
                dataModel.Dirty(nameof(HudModel.activeIcon));
            }
            float fraction = Math.Clamp(loadout.activeCooldownFraction, 0.0f, 1.0f);
            activeSweep.Write(MathF.Round(fraction * SWEEP_STEPS) / SWEEP_STEPS);
            UpdateActiveReady(fraction <= 0.0f);
        }

        /// <summary>The ready ring flashes (with a sound) when the active comes off cooldown.</summary>
        private void UpdateActiveReady(bool ready)
        {
            if (ready == activeWasReady)
            {
                return;
            }
            activeWasReady = ready;
            active?.SetClass("ready", ready);
            if (ready)
            {
                activeRing?.RestartAnimation();
                ui.PlaySound("active_ready");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>One Armory upgrade as the "armory" data model shows it.</summary>
    public sealed class ArmoryItem
    {
        public string name = "";
        public string icon = "";
        public bool[] pips = Array.Empty<bool>();
        public string effect = "";
        public string next = "";
        public string price = "";
        public bool owned;
        public bool maxed;
        public bool affordable;
    }

    /// <summary>One rank of the inspected upgrade: its total bonus, owned or the next one to buy.</summary>
    public sealed class ArmoryRank
    {
        public string label = "";
        public string bonus = "";
        public bool owned;
        public bool next;
    }

    /// <summary>The Armory data model ("armory").</summary>
    public sealed class ArmoryModel
    {
        private readonly ArmoryScreen screen;

        public string gold = "0";
        public List<ArmoryItem> items = new List<ArmoryItem>();
        public string message = "";
        public string refundText = ArmoryScreen.REFUND_TEXT;
        public bool confirmRefund;
        public bool inspecting;
        public string inspectName = "";
        public string inspectIcon = "";
        public string inspectRank = "";
        public string inspectSummary = "";
        public List<ArmoryRank> inspectRanks = new List<ArmoryRank>();
        public string inspectPrice = "";
        /// <summary>The Buy button's text; the button hides once the upgrade is maxed.</summary>
        public string buyText = "";
        public bool inspectMaxed;
        public bool inspectAffordable;

        internal ArmoryModel(ArmoryScreen screen)
        {
            this.screen = screen;
        }

        public void Inspect(int index) => screen.Inspect(index);

        public void Buy() => screen.BuyInspected();

        public void CloseInspect() => screen.CloseInspect();

        public void Refund() => screen.Refund();

        public void Back() => screen.Close();
    }

    /// <summary>
    /// The Armory: the permanent upgrades of <see cref="MetaShop"/> with rank pips, current bonus, next rank and
    /// price. Choosing an upgrade opens its inspect panel (what it does, every rank's bonus, the price) where it is
    /// bought; buying spends banked gold and saves at once; Refund all (pressed twice) returns every coin spent.
    /// Opens from the title or the results and returns there. Main thread.
    /// </summary>
    internal sealed class ArmoryScreen : UIScreen
    {
        internal const string REFUND_TEXT = "REFUND ALL";
        private const string INSPECT_PANEL_ID = "#inspect-panel";
        private const string INSPECT_FLASH_ID = "#inspect-flash";
        private const string FLASH_CLASS = "flash";
        private const string HINT = "Choose an upgrade to see every rank and buy it.";
        private const int NOT_INSPECTING = -1;

        private readonly ArmoryModel model;
        /// <summary>Upgrade index of the open inspect panel, or NOT_INSPECTING.</summary>
        private int inspected = NOT_INSPECTING;
        /// <summary>Upgrade the focus returns to when the panel closes.</summary>
        private int lastInspected;

        public ArmoryScreen(GameUI ui) : base(ui, "Armory", "app:/data/UI/Armory.rhtml")
        {
            model = new ArmoryModel(this);
            foreach (MetaUpgrade upgrade in MetaShop.upgrades)
            {
                model.items.Add(new ArmoryItem { name = upgrade.name.ToUpperInvariant(), icon = UIText.Icon(upgrade.id), pips = new bool[upgrade.maxRank] });
            }
            BindModel("armory", model);
        }

        private bool isInspecting => inspected != NOT_INSPECTING;

        protected override void Refresh()
        {
            inspected = NOT_INSPECTING;
            model.confirmRefund = false;
            model.message = HINT;
            Fill();
        }

        internal void Inspect(int index)
        {
            if (index < 0 || index >= MetaShop.upgrades.Length)
            {
                return;
            }
            inspected = index;
            lastInspected = index;
            model.confirmRefund = false;
            model.message = "";
            ui.PlaySound("ui_select");
            root?.Q(INSPECT_FLASH_ID)?.SetClass(FLASH_CLASS, false);
            Fill();
            root?.Q(INSPECT_PANEL_ID)?.RestartAnimation();
            MarkNavDirty();
        }

        internal void CloseInspect()
        {
            if (!isInspecting)
            {
                return;
            }
            inspected = NOT_INSPECTING;
            model.message = HINT;
            ui.PlaySound("ui_back");
            Fill();
            MarkNavDirty();
        }

        internal void BuyInspected()
        {
            SaveData save = run.save;
            if (save == null || !isInspecting)
            {
                return;
            }
            MetaUpgrade upgrade = MetaShop.upgrades[inspected];
            int price = MetaShop.GetPrice(save, upgrade);
            if (!MetaShop.Buy(save, upgrade))
            {
                model.message = price < 0 ? upgrade.name + " is fully forged." : $"Not enough gold: {UIText.Thousands(price - save.gold)} more needed.";
                Deny(root?.Q("#inspect-buy"));
                dataModel.Dirty(nameof(ArmoryModel.message));
                return;
            }
            int rank = save.GetRank(upgrade.id);
            model.confirmRefund = false;
            model.message = $"Rank {UIText.Int(rank)} forged: {upgrade.Describe(rank)}.";
            ui.PlaySound("buy");
            PlayBuyFlash();
            Fill();
            MarkNavDirty();
        }

        internal void Refund()
        {
            SaveData save = run.save;
            if (save == null)
            {
                return;
            }
            if (save.goldSpent <= 0)
            {
                model.message = "Nothing to refund.";
                Deny(root?.Q("#refund"));
                dataModel.Dirty(nameof(ArmoryModel.message));
                return;
            }
            if (!model.confirmRefund)
            {
                model.confirmRefund = true;
                model.message = "Press again to take back every rank and refund all gold spent.";
                ui.PlaySound("ui_error");
                Fill();
                return;
            }
            int refunded = MetaShop.RefundAll(save);
            model.confirmRefund = false;
            model.message = $"Refunded {UIText.Thousands(refunded)} gold.";
            ui.PlaySound("refund");
            Fill();
        }

        internal void Close()
        {
            ui.PlaySound("ui_back");
            ui.CloseSubmenu();
        }

        protected override void Back()
        {
            if (isInspecting)
            {
                CloseInspect();
                return;
            }
            Close();
        }

        /// <summary>The open inspect panel takes the focus alone; otherwise the tiles and the footer do (the
        /// panel's buttons are inside a hidden parent, which per-element visibility does not see).</summary>
        protected override UIElement GetNavScope() => root.Q(isInspecting ? INSPECT_PANEL_ID : INTRO_ID);

        /// <summary>The panel's last button (Buy, or close once maxed); the last inspected tile otherwise.</summary>
        protected override int GetDefaultFocus() => isInspecting ? nav.count - 1 : lastInspected;

        private void Fill()
        {
            SaveData save = run.save;
            if (save == null)
            {
                return;
            }
            model.gold = UIText.Thousands(save.gold);
            for (int i = 0; i < MetaShop.upgrades.Length; i++)
            {
                FillItem(model.items[i], MetaShop.upgrades[i], save);
            }
            model.refundText = model.confirmRefund ? "CONFIRM REFUND" :
                               save.goldSpent > 0 ? $"{REFUND_TEXT} ({UIText.Thousands(save.goldSpent)})" : REFUND_TEXT;
            FillInspect(save);
            dataModel.DirtyAll();
        }

        private static void FillItem(ArmoryItem item, MetaUpgrade upgrade, SaveData save)
        {
            int rank = save.GetRank(upgrade.id);
            int price = MetaShop.GetPrice(save, upgrade);
            for (int p = 0; p < item.pips.Length; p++)
            {
                item.pips[p] = p < rank;
            }
            item.owned = rank > 0;
            item.maxed = price < 0;
            item.affordable = price >= 0 && save.gold >= price;
            item.effect = rank > 0 ? upgrade.Describe(rank) : "Not owned";
            item.next = item.maxed ? "Fully forged" : "Next: " + upgrade.Describe(rank + 1);
            item.price = item.maxed ? "MAX" : UIText.Thousands(price);
        }

        /// <summary>The inspect panel: what the upgrade does, every rank's total bonus, the next rank's price.</summary>
        private void FillInspect(SaveData save)
        {
            model.inspecting = isInspecting;
            if (!isInspecting)
            {
                return;
            }
            MetaUpgrade upgrade = MetaShop.upgrades[inspected];
            int rank = save.GetRank(upgrade.id);
            int price = MetaShop.GetPrice(save, upgrade);
            model.inspectName = upgrade.name.ToUpperInvariant();
            model.inspectIcon = UIText.Icon(upgrade.id);
            model.inspectRank = $"RANK {rank} / {upgrade.maxRank}";
            model.inspectSummary = upgrade.summary ?? "";
            model.inspectMaxed = price < 0;
            model.inspectAffordable = price >= 0 && save.gold >= price;
            model.inspectPrice = model.inspectMaxed ? "" : UIText.Thousands(price);
            model.buyText = $"BUY RANK {rank + 1}";
            model.inspectRanks.Clear();
            for (int r = 1; r <= upgrade.maxRank; r++)
            {
                model.inspectRanks.Add(new ArmoryRank { label = "RANK " + UIText.Int(r), bonus = upgrade.Describe(r), owned = r <= rank, next = r == rank + 1 });
            }
        }

        /// <summary>A gold wash over the panel (the "flash" class runs its animation once).</summary>
        private void PlayBuyFlash()
        {
            UIElement flash = root?.Q(INSPECT_FLASH_ID);
            if (flash != null)
            {
                flash.SetClass(FLASH_CLASS, true);
                flash.RestartAnimation();
            }
        }
    }
}

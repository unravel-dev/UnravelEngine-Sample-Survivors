using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The chest opening: the items are already applied by the run; this shows up to five reels that spin random
    /// weapon and passive icons, slow down and land one after another (1.5 s for one item, 2.0 s for up to three,
    /// 3.0 s for more) with a thunk, a rarity flash and the item's name; any button lands them all. Then the gold
    /// counts up and Continue resumes the run. The reels are driven element by element (no data model): every spin
    /// tick swaps an icon and replays its drop, which needs the same element each time. Main thread.
    /// </summary>
    internal sealed class ChestScreen : UIScreen
    {
        private const int REEL_COUNT = 5;
        private const float SPIN_SECONDS_ONE = 1.5f;
        private const float SPIN_SECONDS_THREE = 2.0f;
        private const float SPIN_SECONDS_FIVE = 3.0f;
        private const int FEW_ITEMS = 3;
        /// <summary>With several reels, the first lands this many seconds in; the last at the spin's end.</summary>
        private const float FIRST_LAND_MIN = 0.7f;
        private const float TICK_FAST = 0.05f;
        private const float TICK_SLOW = 0.17f;
        /// <summary>The icon's drop lasts this share of the tick interval.</summary>
        private const float TICK_DROP_SHARE = 0.9f;
        private const float TICK_SOUND_INTERVAL = 0.07f;
        private const float MIN_LAND_SECONDS = 0.01f;
        private const float GOLD_COUNT_SECONDS = 0.6f;
        /// <summary>Seed of the spin's icon order (cosmetic, independent of the run's random numbers).</summary>
        private const int SPIN_SEED = 7;
        private const string TICK_FROM = "translateY(-2.2rem)";
        private const string TICK_TO = "translateY(0rem)";
        private const string DONE_CLASS = "done";
        private const string LANDED_CLASS = "landed";

        /// <summary>Icon keys the reels cycle through while spinning: every weapon and passive.</summary>
        private static readonly string[] spinIcons = BuildSpinIcons();

        private struct Reel
        {
            public UIElement root;
            public UIElement icon;
            public UIElement name;
            public UIElement tag;
            public UIElement own;
            public bool landed;
            public float landAt;
            public float nextTick;
        }

        private readonly Reel[] reels = new Reel[REEL_COUNT];
        private readonly List<UpgradeOffer> items = new List<UpgradeOffer>(REEL_COUNT);
        private readonly System.Random spinRandom = new System.Random(SPIN_SEED);
        private UIElement goldText;
        private Action onDone;
        private int gold;
        private float goldShown;
        private float clock;
        private float lastTickSound;
        private bool isSpinning;
        private bool isDone;

        public ChestScreen(GameUI ui) : base(ui, "Chest", "app:/data/UI/Chest.rhtml")
        {
        }

        protected override void OnLoaded(UIDocument loaded)
        {
            List<UIElement> list = loaded.QueryAll(".reel");
            for (int i = 0; i < REEL_COUNT; i++)
            {
                UIElement reel = i < list.Count ? list[i] : null;
                reels[i].root = reel;
                reels[i].icon = reel?.Q(".reel-icon");
                reels[i].name = reel?.Q(".reel-name");
                reels[i].tag = reel?.Q(".reel-tag");
                reels[i].own = reel?.Q(".own");
            }
            goldText = loaded.Q("#chest-gold-text");
            UIElement continueButton = loaded.Q("#chest-continue");
            if (continueButton != null)
            {
                continueButton.clicked += Close;
            }
        }

        /// <summary>Takes the chest's items and gold before GameUI shows the screen; <paramref name="done"/> runs on close.</summary>
        public void Prepare(List<UpgradeOffer> chestItems, int chestGold, Action done)
        {
            items.Clear();
            for (int i = 0; chestItems != null && i < chestItems.Count && items.Count < REEL_COUNT; i++)
            {
                if (chestItems[i] != null)
                {
                    items.Add(chestItems[i]);
                }
            }
            gold = chestGold;
            onDone = done;
        }

        /// <summary>Starts the spin.</summary>
        protected override void Refresh()
        {
            clock = 0.0f;
            lastTickSound = 0.0f;
            goldShown = 0.0f;
            isDone = false;
            isSpinning = items.Count > 0;
            root.SetClass(DONE_CLASS, false);
            float total = GetSpinSeconds(items.Count);
            for (int i = 0; i < REEL_COUNT; i++)
            {
                ResetReel(ref reels[i], i, total);
            }
            goldText?.SetText("+0");
            if (!isSpinning)
            {
                Finish();
            }
        }

        public override void Tick(float unscaledDt)
        {
            if (!isReady)
            {
                return;
            }
            clock += unscaledDt;
            if (isSpinning)
            {
                SpinReels();
            }
            if (isDone && goldShown < gold)
            {
                goldShown = MathF.Min(gold, goldShown + MathF.Max(1.0f, gold / GOLD_COUNT_SECONDS * unscaledDt));
                goldText?.SetText(UIText.Plus((int)goldShown));
            }
        }

        protected override void HandleInput(UIInput input)
        {
            if (isSpinning)
            {
                if (input.anyButton)
                {
                    LandAll();
                }
                return;
            }
            if (input.submit || input.cancel)
            {
                Close();
                return;
            }
            base.HandleInput(input);
        }

        protected override void CollectNav()
        {
            base.CollectNav();
            nav.EnsureFocus();
        }

        private static float GetSpinSeconds(int count)
        {
            return count <= 1 ? SPIN_SECONDS_ONE : count <= FEW_ITEMS ? SPIN_SECONDS_THREE : SPIN_SECONDS_FIVE;
        }

        /// <summary>Unused reels hide; used ones land evenly from FIRST_LAND_MIN to the end of the spin.</summary>
        private void ResetReel(ref Reel reel, int index, float total)
        {
            if (reel.root == null)
            {
                return;
            }
            int count = items.Count;
            bool used = index < count;
            reel.root.style["display"] = used ? "block" : "none";
            reel.landed = !used;
            reel.root.SetClass(LANDED_CLASS, false);
            foreach (string frame in UIText.FRAME_CLASSES)
            {
                reel.root.SetClass(frame, false);
            }
            reel.landAt = count <= 1 ? total : FIRST_LAND_MIN + (total - FIRST_LAND_MIN) * index / (count - 1);
            reel.nextTick = 0.0f;
        }

        private void SpinReels()
        {
            bool allLanded = true;
            for (int i = 0; i < items.Count; i++)
            {
                if (reels[i].landed)
                {
                    continue;
                }
                if (clock >= reels[i].landAt)
                {
                    Land(i);
                    continue;
                }
                allLanded = false;
                if (clock >= reels[i].nextTick)
                {
                    Spin(ref reels[i]);
                }
            }
            if (allLanded)
            {
                Finish();
            }
        }

        /// <summary>One tick of a spinning reel: a random icon drops in; ticks slow down toward the landing.</summary>
        private void Spin(ref Reel reel)
        {
            float progress = Math.Clamp(clock / MathF.Max(reel.landAt, MIN_LAND_SECONDS), 0.0f, 1.0f);
            float interval = TICK_FAST + (TICK_SLOW - TICK_FAST) * progress * progress;
            reel.nextTick = clock + interval;
            if (reel.icon != null)
            {
                reel.icon.SetAttribute("src", spinIcons[spinRandom.Next(spinIcons.Length)]);
                reel.icon.style["transform"] = TICK_FROM;
                reel.icon.Animate("transform", TICK_TO, interval * TICK_DROP_SHARE, UIEasing.CubicOut);
            }
            if (clock - lastTickSound >= TICK_SOUND_INTERVAL)
            {
                lastTickSound = clock;
                ui.PlaySound("reel_tick");
            }
        }

        private void Land(int index)
        {
            ref Reel reel = ref reels[index];
            reel.landed = true;
            UpgradeOffer item = items[index];
            bool isEvolution = item.kind == UpgradeOffer.Kind.Evolution;
            if (reel.icon != null)
            {
                reel.icon.SetAttribute("src", UIText.IconOr(item.icon, item.id));
                reel.icon.style["transform"] = TICK_TO;
            }
            reel.name?.SetText(item.name ?? "");
            reel.tag?.SetText(GetTag(item));
            UIText.ApplyOwnership(reel.own, UIText.GetOwnership(item.id));
            reel.root.SetClass(UIText.OfferFrameClass(item), true);
            reel.root.SetClass(LANDED_CLASS, true);
            ui.PlaySound(isEvolution ? "reel_land_evolution" : "reel_land");
        }

        /// <summary>"EVOLVED", "NEW" for a new weapon or passive, else the card's rank tag.</summary>
        private static string GetTag(UpgradeOffer item)
        {
            if (item.kind == UpgradeOffer.Kind.Evolution)
            {
                return "EVOLVED";
            }
            return item.isNew && item.kind != UpgradeOffer.Kind.Boon ? "NEW" : UIText.RankTag(item);
        }

        private void LandAll()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (!reels[i].landed)
                {
                    Land(i);
                }
            }
            Finish();
        }

        private void Finish()
        {
            isSpinning = false;
            isDone = true;
            root.SetClass(DONE_CLASS, true);
            if (gold > 0)
            {
                ui.PlaySound("chest_gold");
            }
            MarkNavDirty();
        }

        private void Close()
        {
            if (!isDone)
            {
                return;
            }
            isDone = false;
            ui.PlaySound("ui_confirm");
            HideAndInvoke(ref onDone);
        }

        private static string[] BuildSpinIcons()
        {
            var icons = new string[WeaponCatalog.weapons.Length + WeaponCatalog.passives.Length];
            for (int i = 0; i < WeaponCatalog.weapons.Length; i++)
            {
                icons[i] = UIText.Icon(WeaponCatalog.weapons[i].id);
            }
            for (int i = 0; i < WeaponCatalog.passives.Length; i++)
            {
                icons[WeaponCatalog.weapons.Length + i] = UIText.Icon(WeaponCatalog.passives[i].id);
            }
            return icons;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The level-up card picker: three cards with rarity frames fly in staggered, hover / focus lifts them, a pick
    /// flashes the card and flies its icon to the HUD slot before the run applies it. Reroll and Banish go through
    /// <see cref="Loadout.TryReroll"/> and <see cref="Loadout.TryBanish"/>, which spend the run's charges; a card
    /// shows the banish button only when <see cref="Loadout.CanBanish"/> allows it. Skip pays 5 gold (the run adds
    /// it). The offers stay until a card is picked or skipped. Keys 1-3, R, X, Q; gamepad Y, X, View; mouse and
    /// touch click. The cards are filled and animated element by element (no data model): each card keeps its
    /// element while it flips, flies and flashes, and its description is RML with coloured old / new values. Main
    /// thread.
    /// </summary>
    internal sealed class LevelUpScreen : UIScreen
    {
        private const int CARD_COUNT = 3;
        private const float STAGGER_SECONDS = 0.06f;
        private const float ENTER_SECONDS = 0.35f;
        /// <summary>The fade-in is this share of the fly-in.</summary>
        private const float ENTER_FADE_SHARE = 0.7f;
        private const float DISMISS_SECONDS = 0.25f;
        private const float DISMISS_FADE_SECONDS = 0.2f;
        private const float FLIP_SECONDS = 0.28f;
        private const float FLY_SECONDS = 0.35f;
        private const float FLY_DELAY = 0.05f;
        private const float PICK_DELAY = 0.42f;
        private const string ENTER_FROM = "translateY(9rem) scale(1, 1)";
        private const string RESTING = "translateY(0rem) scale(1, 1)";
        private const string FLIPPED = "translateY(0rem) scale(0.05, 1)";
        private const string DISMISSED = "translateY(5rem) scale(0.92, 0.92)";
        private const float NO_FLIP_SOUND = -1.0f;

        private readonly List<UpgradeOffer> shown = new List<UpgradeOffer>(CARD_COUNT);
        private readonly UIElement[] slots = new UIElement[CARD_COUNT];
        private readonly UIElement[] cards = new UIElement[CARD_COUNT];
        private readonly float[] flipSoundAt = new float[CARD_COUNT];
        private UIElement subtitle;
        private UIElement reroll;
        private UIElement rerollCount;
        private UIElement banishCount;
        private UIElement skip;
        private UIElement fly;
        private Action<UpgradeOffer> onChosen;
        private UpgradeOffer picked;
        private bool isPicking;
        private float pickTimer;
        private float clock;

        public LevelUpScreen(GameUI ui) : base(ui, "LevelUp", "app:/data/UI/LevelUp.rhtml")
        {
        }

        protected override void OnLoaded(UIDocument loaded)
        {
            List<UIElement> slotList = loaded.QueryAll(".card-slot");
            for (int i = 0; i < CARD_COUNT; i++)
            {
                int index = i;
                slots[i] = i < slotList.Count ? slotList[i] : null;
                cards[i] = slots[i]?.Q(".card");
                cards[i]?.On<UIClickEvent>(e => OnCardClicked(index, e));
            }
            subtitle = loaded.Q("#subtitle");
            reroll = loaded.Q("#reroll");
            rerollCount = loaded.Q("#reroll-count");
            banishCount = loaded.Q("#banish-count");
            skip = loaded.Q("#skip");
            fly = loaded.Q("#fly");
            if (reroll != null)
            {
                reroll.clicked += Reroll;
            }
            if (skip != null)
            {
                skip.clicked += Skip;
            }
        }

        /// <summary>Takes a new set of offers before GameUI shows the screen; the pick goes to <paramref name="chosen"/>.</summary>
        public void Prepare(List<UpgradeOffer> offers, Action<UpgradeOffer> chosen)
        {
            onChosen = chosen;
            isPicking = false;
            picked = null;
            SetOffers(offers);
        }

        protected override void Refresh()
        {
            Populate();
            PlayEnter();
        }

        public override void Tick(float unscaledDt)
        {
            clock += unscaledDt;
            PlayDueFlipSounds();
            if (!isPicking || picked == null)
            {
                return;
            }
            pickTimer -= unscaledDt;
            if (pickTimer > 0.0f)
            {
                return;
            }
            UpgradeOffer offer = picked;
            picked = null;
            if (fly != null)
            {
                fly.style["visibility"] = "hidden";
            }
            HideAndInvoke(ref onChosen, offer);
        }

        protected override void HandleInput(UIInput input)
        {
            if (isPicking)
            {
                return;
            }
            if (input.digit >= 0)
            {
                Pick(input.digit);
            }
            else if (input.reroll)
            {
                Reroll();
            }
            else if (input.skip)
            {
                Skip();
            }
            else if (input.banish)
            {
                BanishFocused();
            }
            else
            {
                base.HandleInput(input);
            }
        }

        private void SetOffers(List<UpgradeOffer> offers)
        {
            shown.Clear();
            for (int i = 0; offers != null && i < offers.Count && shown.Count < CARD_COUNT; i++)
            {
                if (offers[i] != null)
                {
                    shown.Add(offers[i]);
                }
            }
            while (shown.Count < CARD_COUNT)
            {
                shown.Add(null);
            }
        }

        private void Populate()
        {
            if (!isReady)
            {
                return;
            }
            Loadout loadout = run.loadout;
            int rerolls = loadout != null ? loadout.rerollsLeft : 0;
            int banishes = loadout != null ? loadout.banishesLeft : 0;
            root.SetClass("can-banish", banishes > 0);
            root.SetClass("no-banish-left", banishes <= 0);
            rerollCount?.SetText(UIText.Int(rerolls));
            banishCount?.SetText(UIText.Int(banishes));
            reroll?.SetClass("disabled", rerolls <= 0);
            subtitle?.SetText("LEVEL " + UIText.Int(run.level));
            for (int i = 0; i < CARD_COUNT; i++)
            {
                FillCard(i);
            }
            MarkNavDirty();
        }

        private void FillCard(int index)
        {
            UIElement slot = slots[index];
            UIElement card = cards[index];
            if (slot == null || card == null)
            {
                return;
            }
            UpgradeOffer offer = shown[index];
            slot.style["visibility"] = offer != null ? "visible" : "hidden";
            card.SetClass("nav", offer != null);
            card.SetClass("picked", false);
            if (offer == null)
            {
                return;
            }
            foreach (string frame in UIText.FRAME_CLASSES)
            {
                card.SetClass(frame, false);
            }
            card.SetClass(UIText.OfferFrameClass(offer), true);
            card.SetClass("no-banish", run.loadout == null || !run.loadout.CanBanish(offer));
            FillCardText(card, offer);
            FillEvolutionHint(card.Q(".card-evo"), offer);
        }

        private static void FillCardText(UIElement card, UpgradeOffer offer)
        {
            UIElement tag = card.Q(".card-tag");
            if (tag != null)
            {
                tag.SetText(UIText.RankTag(offer));
                tag.SetClass("rank", !offer.isNew && offer.kind != UpgradeOffer.Kind.Evolution && offer.kind != UpgradeOffer.Kind.Fallback);
            }
            card.Q(".card-icon")?.SetAttribute("src", UIText.IconOr(offer.icon, offer.id));
            card.Q(".card-name")?.SetText(offer.name ?? "");
            card.Q(".card-rarity")?.SetText(UIText.RarityLine(offer));
            UIText.ApplyOwnership(card.Q(".own"), UIText.GetOwnership(offer.id));
            UIElement description = card.Q(".card-desc");
            if (description != null)
            {
                description.InnerRml = UIText.FormatDescription(offer.description);
            }
        }

        private static void FillEvolutionHint(UIElement evolution, UpgradeOffer offer)
        {
            if (evolution == null)
            {
                return;
            }
            bool hasHint = !string.IsNullOrEmpty(offer.evolutionHint);
            evolution.style["visibility"] = hasHint ? "visible" : "hidden";
            if (hasHint)
            {
                evolution.Q(".card-evo-text")?.SetText(offer.evolutionHint);
                evolution.Q(".card-evo-icon")?.SetAttribute("src", UIText.IconOr(offer.evolutionHintIcon, UIText.PLACEHOLDER_ICON));
            }
        }

        /// <summary>Cards fly in from below, STAGGER_SECONDS apart, each with its flip sound.</summary>
        private void PlayEnter()
        {
            if (!isReady)
            {
                return;
            }
            for (int i = 0; i < CARD_COUNT; i++)
            {
                UIElement slot = slots[i];
                if (slot == null)
                {
                    continue;
                }
                float delay = i * STAGGER_SECONDS;
                slot.style["transform"] = ENTER_FROM;
                slot.style.opacity = 0.0f;
                slot.Animate("transform", RESTING, ENTER_SECONDS, UIEasing.CubicOut, delay: delay);
                slot.Animate("opacity", "1", ENTER_SECONDS * ENTER_FADE_SHARE, UIEasing.CubicOut, delay: delay);
                flipSoundAt[i] = clock + delay;
            }
            if (fly != null)
            {
                fly.style["visibility"] = "hidden";
            }
        }

        private void PlayDueFlipSounds()
        {
            for (int i = 0; i < CARD_COUNT; i++)
            {
                if (flipSoundAt[i] <= 0.0f || clock < flipSoundAt[i])
                {
                    continue;
                }
                flipSoundAt[i] = NO_FLIP_SOUND;
                if (shown[i] != null)
                {
                    ui.PlaySound("card_flip");
                }
            }
        }

        private int GetFocusedCard()
        {
            UIElement focused = nav.focused;
            for (int i = 0; focused != null && i < CARD_COUNT; i++)
            {
                if (ReferenceEquals(cards[i], focused))
                {
                    return i;
                }
            }
            return -1;
        }

        private void OnCardClicked(int index, UIClickEvent e)
        {
            if (isPicking)
            {
                return;
            }
            UIElement target = e?.target;
            if (target != null && target.Closest(".card-banish") != null)
            {
                Banish(index);
                return;
            }
            Pick(index);
        }

        private void Pick(int index)
        {
            if (isPicking || index < 0 || index >= CARD_COUNT || shown[index] == null)
            {
                return;
            }
            isPicking = true;
            picked = shown[index];
            pickTimer = PICK_DELAY;
            ui.PlaySound("card_select");
            cards[index]?.SetClass("picked", true);
            for (int i = 0; i < CARD_COUNT; i++)
            {
                if (i != index && slots[i] != null && shown[i] != null)
                {
                    slots[i].Animate("transform", DISMISSED, DISMISS_SECONDS, UIEasing.CubicIn);
                    slots[i].Animate("opacity", "0", DISMISS_FADE_SECONDS, UIEasing.CubicIn);
                }
            }
            FlyIcon(index, picked);
        }

        /// <summary>The picked icon flies from the card to the place the HUD shows it.</summary>
        private void FlyIcon(int index, UpgradeOffer offer)
        {
            UIElement icon = cards[index]?.Q(".card-icon");
            if (fly == null || icon == null || !ui.hud.TryGetSlotRect(offer, out Rect target))
            {
                return;
            }
            float pixelsPerDp = document.pixelsPerDp > 0.0f ? document.pixelsPerDp : 1.0f;
            Rect from = icon.rect;
            fly.SetAttribute("src", UIText.IconOr(offer.icon, offer.id));
            fly.style.left = from.x / pixelsPerDp;
            fly.style.top = from.y / pixelsPerDp;
            fly.style.width = from.width / pixelsPerDp;
            fly.style.height = from.height / pixelsPerDp;
            fly.style["visibility"] = "visible";
            AnimateFly("left", target.x / pixelsPerDp);
            AnimateFly("top", target.y / pixelsPerDp);
            AnimateFly("width", target.width / pixelsPerDp);
            AnimateFly("height", target.height / pixelsPerDp);
        }

        private void AnimateFly(string property, float dp)
        {
            fly.Animate(property, dp.ToString("0.##", CultureInfo.InvariantCulture) + "dp", FLY_SECONDS, UIEasing.CubicIn, delay: FLY_DELAY);
        }

        private void Reroll()
        {
            if (isPicking)
            {
                return;
            }
            if (run.loadout == null || !run.loadout.TryReroll(run.rng, out List<UpgradeOffer> offers))
            {
                Deny(reroll);
                return;
            }
            SetOffers(offers);
            ui.PlaySound("card_reroll");
            Populate();
            PlayEnter();
        }

        private void BanishFocused()
        {
            int index = GetFocusedCard();
            if (index < 0)
            {
                Deny(null);
                return;
            }
            Banish(index);
        }

        /// <summary>Replaces a card with a fresh one that flips in; the banished item leaves the run's pool.</summary>
        private void Banish(int index)
        {
            if (isPicking || index < 0 || index >= CARD_COUNT)
            {
                return;
            }
            if (run.loadout == null || !run.loadout.TryBanish(shown[index], shown, run.rng, out UpgradeOffer replacement))
            {
                Deny(slots[index]);
                return;
            }
            ui.PlaySound("card_banish");
            shown[index] = replacement;
            Populate();
            FlipIn(slots[index]);
        }

        private static void FlipIn(UIElement slot)
        {
            if (slot == null)
            {
                return;
            }
            slot.style["transform"] = FLIPPED;
            slot.style.opacity = 1.0f;
            slot.Animate("transform", RESTING, FLIP_SECONDS, UIEasing.BackOut);
        }

        private void Skip()
        {
            if (isPicking)
            {
                return;
            }
            isPicking = true;
            ui.PlaySound("card_skip");
            HideAndInvoke(ref onChosen, null);
        }
    }
}

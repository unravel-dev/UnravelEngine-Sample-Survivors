using System;
using System.Collections.Generic;
using System.Text;
using Unravel.Core;

namespace Survivors
{
    /// <summary>What a details panel shows.</summary>
    internal enum DetailsKind
    {
        Weapon,
        Passive,
        Active,
    }

    /// <summary>A tap asking for the details of one owned item (HUD) or of a hero's starting kit (hero select).</summary>
    internal sealed class DetailsRequest
    {
        public DetailsKind kind;
        /// <summary>Weapon or passive slot index (ignored for the active and a hero's kit).</summary>
        public int slot;
        /// <summary>The hero whose kit to show: the kind picks its starting weapon, signature or active. Null: the
        /// run's own items.</summary>
        public HeroClass? hero;
    }

    /// <summary>
    /// Item details over the paused game, opened by tapping / clicking a HUD slot or the active ability's info badge:
    /// icon, name, ownership tag (class unique / shared / passive), rarity and kind, rank pips or the evolved state,
    /// the description, every number of the current rank, what the next rank changes, and the evolution recipe with
    /// its status. Hero select opens it over itself for a hero's starting kit, with no run behind it. Closes on the
    /// close or Continue button, a tap outside the panel, Back / Escape / Pause; play resumes through Run.Resume. The
    /// panel is filled element by element (no data model): its sections are RML built from the weapon's own columns
    /// (a grid of any size, coloured old / new values), which a data model's flat values do not express. Main thread.
    /// </summary>
    internal sealed class DetailsScreen : UIScreen
    {
        /// <summary>Separates the kind from the weapon's tags (RmlUi collapses runs of spaces).</summary>
        private const string SEPARATOR = " | ";
        private const string TAG_SEPARATOR = " - ";
        private const float PERCENT = 100.0f;
        private const string PAUSED_HINT = "Play is paused.";
        /// <summary>On the panel while it only holds a short text: phones then fit it to the text.</summary>
        private const string FIT_CLASS = "fit";

        private readonly StringBuilder rml = new StringBuilder(512);
        private DetailsRequest request;
        private UIElement icon;
        private UIElement nameText;
        private UIElement ownTag;
        private UIElement kindText;
        private UIElement rankRow;
        private UIElement rankText;
        private UIElement pips;
        private UIElement description;
        private UIElement nowSection;
        private UIElement nowLabel;
        private UIElement nowBody;
        private UIElement nextSection;
        private UIElement nextLabel;
        private UIElement nextBody;
        private UIElement evoSection;
        private UIElement evoLabel;
        private UIElement evoIcon;
        private UIElement evoText;
        private UIElement body;
        private UIElement panel;
        private UIElement hint;
        private UIElement okButton;

        public DetailsScreen(GameUI ui) : base(ui, "Details", "app:/data/UI/Details.rhtml")
        {
        }

        protected override bool closesOnPause => true;

        protected override void OnLoaded(UIDocument loaded)
        {
            icon = loaded.Q("#details-icon");
            nameText = loaded.Q("#details-name");
            ownTag = loaded.Q("#details-own");
            kindText = loaded.Q("#details-kind");
            rankRow = loaded.Q("#details-rank");
            rankText = loaded.Q("#details-rank-text");
            pips = loaded.Q("#details-pips");
            description = loaded.Q("#details-desc");
            nowSection = loaded.Q("#sec-now");
            nowLabel = loaded.Q("#sec-now-label");
            nowBody = loaded.Q("#details-now");
            nextSection = loaded.Q("#sec-next");
            nextLabel = loaded.Q("#sec-next-label");
            nextBody = loaded.Q("#details-next");
            evoSection = loaded.Q("#sec-evo");
            evoLabel = loaded.Q("#sec-evo-label");
            evoIcon = loaded.Q("#details-evo-icon");
            evoText = loaded.Q("#details-evo-text");
            body = loaded.Q("#details-body");
            panel = loaded.Q("#details-panel");
            hint = loaded.Q("#details-hint");
            okButton = loaded.Q("#details-ok");
            WireClose(loaded.Q("#details-dim"));
            WireClose(loaded.Q("#details-close"));
            WireClose(okButton);
        }

        /// <summary>Takes the item before GameUI shows the panel; the open sound plays unless it is already up.</summary>
        public void Prepare(DetailsRequest detailsRequest)
        {
            request = detailsRequest;
            if (!isShown)
            {
                ui.PlaySound("ui_select");
            }
        }

        protected override void Refresh()
        {
            bool isHeroKit = request?.hero != null;
            if (request == null || (!isHeroKit && run.loadout == null))
            {
                return;
            }
            hint?.SetText(isHeroKit ? "" : PAUSED_HINT);
            okButton?.SetText(isHeroKit ? "CLOSE" : "CONTINUE");
            panel?.SetClass(FIT_CLASS, isHeroKit && request.kind != DetailsKind.Weapon);
            if (isHeroKit)
            {
                FillHeroKit(request.hero.Value);
            }
            else
            {
                FillOwnedItem();
            }
            if (body != null)
            {
                body.scrollTop = 0.0f;
            }
            MarkNavDirty();
        }

        protected override void Back()
        {
            Close();
        }

        /// <summary>The focus starts on the last stop: Continue.</summary>
        protected override void CollectNav()
        {
            base.CollectNav();
            nav.EnsureFocus(nav.count - 1);
        }

        private void FillOwnedItem()
        {
            switch (request.kind)
            {
                case DetailsKind.Weapon:
                    FillWeapon(request.slot);
                    break;
                case DetailsKind.Passive:
                    FillPassive(request.slot);
                    break;
                default:
                    FillActive();
                    break;
            }
        }

        private void WireClose(UIElement element)
        {
            if (element != null)
            {
                element.clicked += Close;
            }
        }

        private void Close()
        {
            if (!isShown)
            {
                return;
            }
            ui.PlaySound("ui_back");
            ui.CloseDetails();
        }

        // ---------------------------------------------------------------------------------------------------
        // Weapons
        // ---------------------------------------------------------------------------------------------------

        private void FillWeapon(int slot)
        {
            Loadout loadout = run.loadout;
            if (slot < 0 || slot >= loadout.weapons.Count)
            {
                return;
            }
            FillWeapon(loadout.weapons[slot]);
        }

        private void FillWeapon(SlotView view)
        {
            WeaponDef def = WeaponCatalog.FindWeapon(view.id) ?? WeaponCatalog.FindWeaponByEvolution(view.id);
            SetHeader(UIText.IconOr(view.icon, view.id), view.name, UIText.GetOwnership(view.id));
            if (def == null)
            {
                SetKind("WEAPON", false);
                SetRank(view.rank, view.maxRank, view.isEvolved);
                SetText(description, view.summary ?? "");
                ShowSection(nowSection, false);
                ShowSection(nextSection, false);
                ShowSection(evoSection, false);
                return;
            }
            bool evolved = view.isEvolved && def.evolution != null;
            string tags = DescribeTags(def.tags);
            string kind = evolved ? "EVOLUTION" : def.rarity.ToString().ToUpperInvariant() + " WEAPON";
            SetKind(tags.Length > 0 ? kind + SEPARATOR + tags : kind, evolved);
            SetRank(view.rank, WeaponDef.MAX_RANK, evolved);
            SetText(description, evolved ? def.evolution.description : def.description);
            WeaponLevel level = evolved ? def.evolution.level : def.GetLevel(view.rank);
            nowLabel?.SetText(evolved ? "EVOLVED NUMBERS" : "RANK " + UIText.Int(view.rank));
            SetRml(nowBody, BuildLevelGrid(def, level, evolved ? "" : def.GetNote(view.rank)));
            ShowSection(nowSection, true);
            bool hasNext = !evolved && view.rank < WeaponDef.MAX_RANK;
            if (hasNext)
            {
                nextLabel?.SetText("NEXT: RANK " + UIText.Int(view.rank + 1));
                SetRml(nextBody, UIText.FormatDescription(WeaponText.DescribeRankUp(def, view.rank, view.rank + 1)));
            }
            ShowSection(nextSection, hasNext);
            FillWeaponEvolution(def, view, evolved);
        }

        private void FillWeaponEvolution(WeaponDef def, SlotView view, bool evolved)
        {
            EvolutionDef evolution = def.evolution;
            if (evolution == null)
            {
                ShowSection(evoSection, false);
                return;
            }
            ShowSection(evoSection, true);
            PassiveDef passive = WeaponCatalog.FindPassive(evolution.passiveId);
            string passiveName = passive != null ? passive.name : evolution.passiveId;
            int maxRank = WeaponDef.MAX_RANK;
            if (evolved)
            {
                evoLabel?.SetText("EVOLVED");
                evoIcon?.SetAttribute("src", UIText.Icon(def.id));
                SetText(evoText, $"Evolved from {def.name} (rank {maxRank}) with {passiveName}. It cannot rank further.");
                return;
            }
            evoLabel?.SetText("EVOLUTION");
            evoIcon?.SetAttribute("src", UIText.Icon(evolution.id));
            string status = request.hero != null ? "" : " " + DescribeEvolutionStatus(view, evolution, passiveName);
            SetText(evoText, $"Becomes {evolution.name} at rank {maxRank} with {passiveName}.{status}");
        }

        /// <summary>What the weapon still needs to evolve: the max rank, the paired passive, or nothing.</summary>
        private string DescribeEvolutionStatus(SlotView view, EvolutionDef evolution, string passiveName)
        {
            int maxRank = WeaponDef.MAX_RANK;
            if (view.canEvolve)
            {
                return "Ready: the next chest evolves it.";
            }
            if (view.rank >= maxRank)
            {
                return $"Needs {passiveName} (any rank).";
            }
            return OwnsPassive(evolution.passiveId) ? $"Needs rank {maxRank} (now {view.rank}); {passiveName} owned." :
                                                      $"Needs rank {maxRank} (now {view.rank}) and {passiveName}.";
        }

        /// <summary>Every number of a level as a two-column grid, then the rank's note.</summary>
        private string BuildLevelGrid(WeaponDef def, WeaponLevel level, string note)
        {
            rml.Clear();
            rml.Append("<div class=\"stat-grid\">");
            foreach (WeaponColumn column in def.columns)
            {
                rml.Append("<div class=\"stat-cell\"><span class=\"stat-key\">").Append(UIText.Escape(column.label))
                   .Append("</span><span class=\"stat-val\">").Append(UIText.Escape(WeaponText.FormatValue(column.field, level[column.field])))
                   .Append("</span></div>");
            }
            rml.Append("</div>");
            if (!string.IsNullOrEmpty(note))
            {
                rml.Append("<div class=\"stat-note\">").Append(UIText.Escape(note)).Append("</div>");
            }
            return rml.ToString();
        }

        private static string DescribeTags(WeaponTags tags)
        {
            string text = tags.ToString();
            if (text == "0" || text == "None")
            {
                return "";
            }
            return text.Replace(", ", TAG_SEPARATOR).ToUpperInvariant();
        }

        // ---------------------------------------------------------------------------------------------------
        // Passives
        // ---------------------------------------------------------------------------------------------------

        private void FillPassive(int slot)
        {
            Loadout loadout = run.loadout;
            if (slot < 0 || slot >= loadout.passives.Count)
            {
                return;
            }
            SlotView view = loadout.passives[slot];
            PassiveDef def = WeaponCatalog.FindPassive(view.id);
            SetHeader(UIText.IconOr(view.icon, view.id), view.name, UIText.GetOwnership(view.id));
            int maxRank = def != null ? def.maxRank : Math.Max(1, view.maxRank);
            SetKind(def != null ? def.rarity.ToString().ToUpperInvariant() + " PASSIVE" : "PASSIVE", false);
            SetRank(view.rank, maxRank, false);
            string trait = def != null ? PassiveTraits.Describe(def.id, view.rank) : "";
            SetText(description, (trait.Length > 0 ? trait + " " : "") +
                                 $"The evolutions it unlocks deal +{PassiveTraits.BOND_PER_RANK * 100.0f:0}% damage per rank. Passives are shared by all heroes.");
            nowLabel?.SetText("RANK " + UIText.Int(view.rank));
            SetRml(nowBody, "<div class=\"stat-line\">" + UIText.Escape(def != null ? WeaponText.DescribePassive(def, view.rank) : view.summary ?? "") + "</div>");
            ShowSection(nowSection, true);
            bool hasNext = def != null && view.rank < maxRank;
            if (hasNext)
            {
                nextLabel?.SetText("NEXT: RANK " + UIText.Int(view.rank + 1));
                SetRml(nextBody, UIText.FormatDescription(WeaponText.DescribePassiveChange(def, view.rank, view.rank + 1)));
            }
            ShowSection(nextSection, hasNext);
            FillPassiveEvolutions(view.id);
        }

        /// <summary>The weapons this passive evolves (shared ones, and the current class's own).</summary>
        private void FillPassiveEvolutions(string passiveId)
        {
            var lines = new List<string>();
            WeaponDef first = null;
            foreach (WeaponDef weapon in WeaponCatalog.weapons)
            {
                if (weapon.evolution == null || weapon.evolution.passiveId != passiveId)
                {
                    continue;
                }
                if (!weapon.IsOfferedTo(run.heroClass))
                {
                    continue;
                }
                first ??= weapon;
                lines.Add($"{weapon.name} (rank {WeaponDef.MAX_RANK}) becomes {weapon.evolution.name}.");
            }
            ShowSection(evoSection, lines.Count > 0);
            if (lines.Count == 0)
            {
                return;
            }
            evoLabel?.SetText("EVOLVES");
            evoIcon?.SetAttribute("src", UIText.Icon(first.evolution.id));
            SetText(evoText, string.Join("\n", lines));
        }

        // ---------------------------------------------------------------------------------------------------
        // Active ability and signature passive
        // ---------------------------------------------------------------------------------------------------

        private void FillActive()
        {
            Loadout loadout = run.loadout;
            HeroInfo info = HeroCatalog.Get(run.heroClass);
            string activeName = string.IsNullOrEmpty(loadout.activeName) ? info?.activeName ?? "Active" : loadout.activeName;
            SetHeader(UIText.IconOr(loadout.activeIcon, UIText.ToId(activeName)), activeName, UIText.ForHero(run.heroClass, "ACTIVE"));
            SetKind("ACTIVE ABILITY", false);
            SetRank(0, 0, false);
            SetText(description, info != null ? info.activeText : "");
            nowLabel?.SetText("COOLDOWN");
            SetRml(nowBody, "<div class=\"stat-line\">" + DescribeCooldown(loadout.activeCooldownFraction) + " Cooldown stat applies at half strength.</div>");
            ShowSection(nowSection, true);
            ShowSection(nextSection, false);
            bool hasSignature = info != null && !string.IsNullOrEmpty(info.passiveName);
            ShowSection(evoSection, hasSignature);
            if (hasSignature)
            {
                evoLabel?.SetText("SIGNATURE: " + info.passiveName.ToUpperInvariant());
                evoIcon?.SetAttribute("src", UIText.IconOr(loadout.signatureIcon, UIText.ToId(info.passiveName)));
                SetText(evoText, info.passiveText);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // A hero's starting kit (hero select)
        // ---------------------------------------------------------------------------------------------------

        private void FillHeroKit(HeroClass heroClass)
        {
            HeroInfo info = HeroCatalog.Get(heroClass);
            switch (request.kind)
            {
                case DetailsKind.Weapon:
                    WeaponDef start = WeaponCatalog.FindWeapon(HeroCatalog.GetClass(heroClass).startWeaponId);
                    FillWeapon(new SlotView { id = start.id, name = start.name, icon = info.startWeaponIcon, rank = 1, maxRank = WeaponDef.MAX_RANK });
                    break;
                case DetailsKind.Passive:
                    FillHeroAbility(heroClass, info.passiveName, "SIGNATURE PASSIVE", info.passiveText);
                    break;
                default:
                    FillHeroAbility(heroClass, info.activeName, "ACTIVE ABILITY", info.activeText);
                    break;
            }
        }

        private void FillHeroAbility(HeroClass heroClass, string name, string kind, string text)
        {
            SetHeader(UIText.Icon(UIText.ToId(name)), name, UIText.ForHero(heroClass, "UNIQUE"));
            SetKind(kind, false);
            SetRank(0, 0, false);
            SetText(description, text);
            ShowSection(nowSection, false);
            ShowSection(nextSection, false);
            ShowSection(evoSection, false);
        }

        private static string DescribeCooldown(float fraction)
        {
            return fraction <= 0.0f ? "Ready." : $"Recharging: {UIText.Int((int)MathF.Round(fraction * PERCENT))}% left.";
        }

        // ---------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------

        private bool OwnsPassive(string passiveId)
        {
            foreach (SlotView passive in run.loadout.passives)
            {
                if (passive.id == passiveId)
                {
                    return true;
                }
            }
            return false;
        }

        private void SetHeader(string iconKey, string name, Ownership ownership)
        {
            icon?.SetAttribute("src", iconKey);
            nameText?.SetText(name ?? "");
            UIText.ApplyOwnership(ownTag, ownership);
        }

        private void SetKind(string text, bool isEvolution)
        {
            kindText?.SetText(text);
            kindText?.SetClass("evolution", isEvolution);
        }

        private void SetRank(int rank, int maxRank, bool evolved)
        {
            if (rankRow == null)
            {
                return;
            }
            rankRow.style["display"] = maxRank > 0 || evolved ? "flex" : "none";
            rankText?.SetText(evolved ? "EVOLVED" : $"RANK {rank} / {maxRank}");
            rankRow.SetClass("evolved", evolved);
            if (pips == null)
            {
                return;
            }
            rml.Clear();
            for (int i = 0; !evolved && i < maxRank; i++)
            {
                rml.Append(i < rank ? "<div class=\"pip on\"/>" : "<div class=\"pip\"/>");
            }
            pips.InnerRml = rml.ToString();
        }

        private static void SetText(UIElement element, string text)
        {
            if (element != null)
            {
                element.InnerRml = UIText.Escape(text ?? "").Replace("\n", "<br/>");
            }
        }

        private static void SetRml(UIElement element, string markup)
        {
            if (element != null)
            {
                element.InnerRml = markup ?? "";
            }
        }

        private static void ShowSection(UIElement section, bool visible)
        {
            if (section != null)
            {
                section.style["display"] = visible ? "block" : "none";
            }
        }
    }
}

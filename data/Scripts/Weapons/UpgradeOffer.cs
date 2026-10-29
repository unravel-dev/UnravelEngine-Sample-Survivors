namespace Survivors
{
    /// <summary>One level-up card or chest item: what it is and how it reads.</summary>
    public sealed class UpgradeOffer
    {
        public enum Kind
        {
            Weapon,
            Passive,
            Evolution,
            /// <summary>Hearty Meal (when hurt), the chest's gold: cards that are not items.</summary>
            Fallback,
            /// <summary>A slotless boon.</summary>
            Boon,
        }

        public Kind kind;
        /// <summary>Stable item id (e.g. "oathblade_arc", "whetstone").</summary>
        public string id = "";
        public string name = "";
        public Rarity rarity;
        /// <summary>Icon asset key for the UI (app:/data/UI/Icons/...png).</summary>
        public string icon = "";
        public bool isNew;
        public int fromRank;
        public int toRank;
        /// <summary>What the pick does, with numbers, old -> new (one change per line).</summary>
        public string description = "";
        /// <summary>"Evolves with Wide Sigil", or empty.</summary>
        public string evolutionHint = "";
        public string evolutionHintIcon = "";
        /// <summary>The item came from a chest (it does not count toward the level-up pity rule).</summary>
        public bool isChestItem;
    }
}

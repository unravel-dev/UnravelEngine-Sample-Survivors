namespace Survivors
{
    /// <summary>Where the game is.</summary>
    public enum RunPhase
    {
        /// <summary>Title and hero select: the world idles behind the menus.</summary>
        Menu,
        Playing,
        /// <summary>Time stops for a level-up card pick.</summary>
        LevelUp,
        /// <summary>Time stops while a chest opens.</summary>
        Chest,
        Paused,
        /// <summary>The run ended; the results screen is up.</summary>
        Ended,
    }
}

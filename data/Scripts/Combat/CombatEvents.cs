namespace Survivors
{
    /// <summary>
    /// Hero hits as they land, for passives and weapons that react to them (crit traits, venom ruptures, Bloodletting).
    /// Handlers run inside the damage call: change state only, and queue any damage they cause for the next tick.
    /// Cleared when a run starts. Main thread.
    /// </summary>
    public sealed class CombatEvents
    {
        /// <param name="row">The enemy row hit.</param>
        /// <param name="source">The weapon slot (RunRecord sources).</param>
        /// <param name="amount">Damage dealt after crits and target modifiers.</param>
        /// <param name="isCritical">The hit was critical.</param>
        public delegate void HitHandler(int row, int source, float amount, bool isCritical);

        /// <summary>Every hero hit that damaged an enemy.</summary>
        public event HitHandler hit;

        public void RaiseHit(int row, int source, float amount, bool isCritical) => hit?.Invoke(row, source, amount, isCritical);

        public void Clear()
        {
            hit = null;
        }
    }
}

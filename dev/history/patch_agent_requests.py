"""Class agents' core requests: EnemyWorld.IsStunned (replaces the Knight's reflection reader) and burn / venom damage
credited to the weapon that applied it (results table, DevReport)."""
import os

from patchlib import edit

ROOT = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(ROOT, "src", "Scripts")

edit(os.path.join(S, "Enemies", "EnemyWorld.cs"), [
    ("""        public bool IsFrozen(int row) => freezeTime[row] > 0.0f;""", """        public bool IsFrozen(int row) => freezeTime[row] > 0.0f;

        public bool IsStunned(int row) => stunTime[row] > 0.0f;"""),
    ("""        /// <summary>Multiplies burn damage (Everburn Wick's Kindle).</summary>
        public float burnDamageScale = 1.0f;""", """        /// <summary>Multiplies burn damage (Everburn Wick's Kindle).</summary>
        public float burnDamageScale = 1.0f;
        /// <summary>Burn and venom damage dealt since the owner last read it, by the source that applied it (weapon slot,
        /// up to <see cref="STATUS_SOURCES"/>); the owner adds it to its records and clears it.</summary>
        public readonly float[] statusDamageBySource = new float[STATUS_SOURCES];
        public const int STATUS_SOURCES = 16;
        /// <summary>Burn and venom damage a row took in this step (summed by source in FinishStep, on the main thread).</summary>
        private readonly float[] statusDealt;"""),
    ("""            venomDps = new float[size];
            bloodTime = new float[size];""", """            venomDps = new float[size];
            bloodTime = new float[size];
            statusDealt = new float[size];"""),
    ("""            if (venomTime[row] > 0.0f)
            {
                venomTime[row] -= dt;
                health[row] -= venomStacks[row] * venomDps[row] * damageTakenScale[row] * dt;""", """            if (venomTime[row] > 0.0f)
            {
                venomTime[row] -= dt;
                float venom = venomStacks[row] * venomDps[row] * damageTakenScale[row] * dt;
                health[row] -= venom;
                statusDealt[row] += venom;"""),
    ("""                burnTime[row] -= dt;
                health[row] -= burnDps[row] * damageTakenScale[row] * dt;""", """                burnTime[row] -= dt;
                float burn = burnDps[row] * damageTakenScale[row] * dt;
                health[row] -= burn;
                statusDealt[row] += burn;"""),
    ("""        private void FinishStep()
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row])
                {
                    continue;
                }""", """        private void FinishStep()
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (statusDealt[row] > 0.0f)
                {
                    int source = lastSource[row];
                    if (source >= 0 && source < STATUS_SOURCES)
                    {
                        statusDamageBySource[source] += statusDealt[row];
                    }
                    statusDealt[row] = 0.0f;
                }
                if (!alive[row])
                {
                    continue;
                }"""),
])

edit(os.path.join(S, "Core", "Run.cs"), [("""            loadout.Tick(dt);
            chestBeacons.Tick(dt);""", """            loadout.Tick(dt);
            RecordStatusDamage();
            chestBeacons.Tick(dt);"""),
    ("""        private void ShowMarkers()""", """        /// <summary>Credits burn and venom damage to the weapon that applied it (results table, pause sheet).</summary>
        private void RecordStatusDamage()
        {
            float[] dealt = enemies.statusDamageBySource;
            for (int source = 0; source < dealt.Length; source++)
            {
                if (dealt[source] > 0.0f)
                {
                    record.damageBySource[Math.Min(source, RunRecord.MAX_SOURCES - 1)] += dealt[source];
                    dealt[source] = 0.0f;
                }
            }
        }

        private void ShowMarkers()"""),
])

edit(os.path.join(S, "Weapons", "Knight", "DawnHammer.cs"), [
    ("""        private readonly StunReader stuns;
""", ""),
    ("""            stuns = new StunReader(enemies);
""", ""),
    ("""stuns.IsStunned(enemyRow)""", """enemies.IsStunned(enemyRow)"""),
])
os.remove(os.path.join(S, "Weapons", "Knight", "StunReader.cs"))
print("agent requests patched")

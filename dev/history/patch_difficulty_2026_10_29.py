"""Difficulty pass: steeper enemy health over a run, Dread that scales health, damage, spawns and bosses (and pays gold),
mid-boss health, and the Mage's area spells (Prismatic Ray, Starfall) brought in line with the other classes."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from patchlib import edit  # noqa: E402

S = os.path.join(HERE, "..", "..", "data", "Scripts")

edit(os.path.join(S, "Enemies", "WaveDirector.cs"), [
    ("""        /// <summary>Each Dread level adds this share to enemy health (0.15 = +15%).</summary>
        public const float DREAD_HEALTH_PER_LEVEL = 0.15f;
        /// <summary>Each Dread level adds this share to the spawn budget.</summary>
        public const float DREAD_BUDGET_PER_LEVEL = 0.1f;
        /// <summary>Each Dread level adds this share to the experience of kills.</summary>
        public const float DREAD_EXPERIENCE_PER_LEVEL = 0.1f;
""", """        /// <summary>Each Dread level adds this share to enemy and boss health (0.25 = +25%).</summary>
        public const float DREAD_HEALTH_PER_LEVEL = 0.25f;
        /// <summary>Each Dread level adds this share to the damage of enemies and bosses.</summary>
        public const float DREAD_DAMAGE_PER_LEVEL = 0.1f;
        /// <summary>Each Dread level adds this share to the spawn budget.</summary>
        public const float DREAD_BUDGET_PER_LEVEL = 0.15f;
        /// <summary>Each Dread level adds this share to the experience of kills (kept small: experience is power).</summary>
        public const float DREAD_EXPERIENCE_PER_LEVEL = 0.05f;
        /// <summary>Each Dread level adds this share to the gold of coins, chests and bosses: the reward for the risk.</summary>
        public const float DREAD_GOLD_PER_LEVEL = 0.1f;
        /// <summary>Enemy health over the run: 1 + PER_MINUTE t + PER_MINUTE_SQUARED t^2 (x2.9 at 5:00, x7.3 at 10:00).</summary>
        private const float HEALTH_PER_MINUTE = 0.2f;
        private const float HEALTH_PER_MINUTE_SQUARED = 0.045f;
        /// <summary>Enemy damage over the run: 1 + PER_MINUTE t.</summary>
        private const float DAMAGE_PER_MINUTE = 0.07f;
"""),
    ("""        /// <summary>Health multiplier of enemies spawned now: (1 + 0.15 t + 0.02 t^2) x <see cref="dreadScale"/>.</summary>
        public float healthScale => (1.0f + 0.15f * minutes + 0.02f * minutes * minutes) * dreadScale;

        /// <summary>The Dread part of enemy health: 1 + 0.15 dread.</summary>
        public float dreadScale => 1.0f + DREAD_HEALTH_PER_LEVEL * dread;

        /// <summary>Damage multiplier of enemies spawned now: 1 + 0.07 t.</summary>
        public float damageScale => 1.0f + 0.07f * minutes;
""", """        /// <summary>Health multiplier of enemies spawned now: the run's growth x <see cref="dreadScale"/>.</summary>
        public float healthScale => (1.0f + HEALTH_PER_MINUTE * minutes + HEALTH_PER_MINUTE_SQUARED * minutes * minutes) * dreadScale;

        /// <summary>The Dread part of enemy and boss health.</summary>
        public float dreadScale => 1.0f + DREAD_HEALTH_PER_LEVEL * dread;

        /// <summary>The Dread part of enemy and boss damage.</summary>
        public float dreadDamageScale => 1.0f + DREAD_DAMAGE_PER_LEVEL * dread;

        /// <summary>The Dread multiplier of gold picked up.</summary>
        public float goldScale => 1.0f + DREAD_GOLD_PER_LEVEL * dread;

        /// <summary>Damage multiplier of enemies spawned now: the run's growth x <see cref="dreadDamageScale"/>.</summary>
        public float damageScale => (1.0f + DAMAGE_PER_MINUTE * minutes) * dreadDamageScale;
"""),
])

edit(os.path.join(S, "Bosses", "Boss.cs"), [
    ("""            var options = new SpawnOptions { health = maxHealth, damageScale = 1.0f, isRewarded = true };""",
     """            var options = new SpawnOptions { health = maxHealth * run.director.dreadScale, damageScale = 1.0f, isRewarded = true };"""),
    ("""        /// <summary>Hurts the hero for a boss attack and names the source for the death screen.</summary>""",
     """        /// <summary>Hurts the hero for a boss attack (scaled by Dread) and names the source for the death screen.</summary>"""),
    ("""            if (!target.TakeDamage(amount, from))
            {
                return false;
            }
            run.director.lastDamageSource = source;""", """            if (!target.TakeDamage(amount * run.director.dreadDamageScale, from))
            {
                return false;
            }
            run.director.lastDamageSource = source;"""),
])
edit(os.path.join(S, "Bosses", "Gravewarden.cs"), [("private const float HEALTH = 3000.0f;", "private const float HEALTH = 4000.0f;")])
edit(os.path.join(S, "Bosses", "HollowArchmage.cs"), [("private const float HEALTH = 10000.0f;", "private const float HEALTH = 13000.0f;")])

edit(os.path.join(S, "Pickups", "Drops.cs"), [
    ("run.AddGold((int)MathF.Max(1.0f, MathF.Round(value * run.stats[StatId.Greed])));",
     "run.AddGold((int)MathF.Max(1.0f, MathF.Round(value * run.stats[StatId.Greed] * run.director.goldScale)));"),
])

edit(os.path.join(S, "Weapons", "WeaponCatalog.cs"), [
    ("""                    { 50, 6.0f, 2.5f, 1, 2, 4 },
                    { 65, 6.0f, 2.5f, 1, 2, 4 },
                    { 65, 6.0f, 2.5f, 2, 2, 4 },
                    { 65, 6.0f, 3.0f, 2, 2, 4 },
                    { 85, 5.5f, 3.0f, 2, 2, 4 },
                    { 85, 5.5f, 3.0f, 3, 3, 6 },
                    { 85, 5.5f, 3.5f, 3, 3, 6 },
                    { 110, 5.0f, 3.5f, 4, 3, 6 },""", """                    { 38, 6.0f, 2.5f, 1, 2, 3 },
                    { 49, 6.0f, 2.5f, 1, 2, 3 },
                    { 49, 6.0f, 2.5f, 2, 2, 3 },
                    { 49, 6.0f, 3.0f, 2, 2, 3 },
                    { 64, 5.5f, 3.0f, 2, 2, 3 },
                    { 64, 5.5f, 3.0f, 3, 3, 4.5f },
                    { 64, 5.5f, 3.5f, 3, 3, 4.5f },
                    { 82, 5.0f, 3.5f, 4, 3, 4.5f },"""),
    ('description = "Every 7 s a great meteor strikes the densest crowd (300 damage, radius 7 m) ringed by 6 meteors (110 damage). Craters burn 4 s for 10 per 0.5 s.",',
     'description = "Every 7 s a great meteor strikes the densest crowd (225 damage, radius 7 m) ringed by 6 meteors (82 damage). Craters burn 4 s for 8 per 0.5 s.",'),
    ("Set(F.Cooldown, 7), Set(F.Count, 6), Set(F.Damage, 110), Set(F.SubDuration, 4), Set(F.SubDamage, 10),",
     "Set(F.Cooldown, 7), Set(F.Count, 6), Set(F.Damage, 82), Set(F.SubDuration, 4), Set(F.SubDamage, 8),"),
    ("Set(F.MajorDamage, 300), Set(F.MajorRadius, 7),", "Set(F.MajorDamage, 225), Set(F.MajorRadius, 7),"),
])

edit(os.path.join(S, "Weapons", "Mage", "MageCatalog.cs"), [
    ("""                    { 9, 7.0f, 7, 1, 1.6f, 3 },
                    { 12, 7.0f, 7, 1, 1.6f, 3 },
                    { 12, 7.0f, 8, 1, 1.8f, 3 },
                    { 12, 6.5f, 8, 2, 1.8f, 4 },
                    { 16, 6.5f, 8, 2, 1.8f, 4 },
                    { 16, 6.0f, 9, 2, 2.0f, 5 },
                    { 20, 6.0f, 9, 3, 2.0f, 5 },
                    { 20, 5.0f, 10, 3, 2.0f, 6 },""", """                    { 6, 7.0f, 7, 1, 1.6f, 3 },
                    { 8, 7.0f, 7, 1, 1.6f, 3 },
                    { 8, 7.0f, 8, 1, 1.8f, 3 },
                    { 8, 6.5f, 8, 2, 1.8f, 4 },
                    { 10, 6.5f, 8, 2, 1.8f, 4 },
                    { 10, 6.0f, 9, 2, 2.0f, 5 },
                    { 13, 6.0f, 9, 3, 2.0f, 5 },
                    { 13, 5.0f, 10, 3, 2.0f, 6 },"""),
    ("Set(F.BurnTick, 0.5f), Set(F.SubDamage, 40),", "Set(F.BurnTick, 0.5f), Set(F.SubDamage, 30),"),
    ('Note(1, "Thermal shock: 40 + 4 x level damage.")', 'Note(1, "Thermal shock: 30 + 3 x level damage.")'),
    ("Set(F.Count, 6), Set(F.Duration, -1), Set(F.Damage, 26), Set(F.SubDamage, 80)",
     "Set(F.Count, 6), Set(F.Duration, -1), Set(F.Damage, 17), Set(F.SubDamage, 60)"),
])

edit(os.path.join(S, "UI", "Screens", "HeroSelectScreen.cs"), [
    ("""            float health = WaveDirector.DREAD_HEALTH_PER_LEVEL * dread * PERCENT;
            float spawns = WaveDirector.DREAD_BUDGET_PER_LEVEL * dread * PERCENT;
            float experience = WaveDirector.DREAD_EXPERIENCE_PER_LEVEL * dread * PERCENT;
            model.dreadText = dread == 0 ? "The tide as it was." :
                              $"Enemy health +{health:0}%, spawns +{spawns:0}%, experience +{experience:0}%.";""",
     """            float health = WaveDirector.DREAD_HEALTH_PER_LEVEL * dread * PERCENT;
            float damage = WaveDirector.DREAD_DAMAGE_PER_LEVEL * dread * PERCENT;
            float spawns = WaveDirector.DREAD_BUDGET_PER_LEVEL * dread * PERCENT;
            float gold = WaveDirector.DREAD_GOLD_PER_LEVEL * dread * PERCENT;
            model.dreadText = dread == 0 ? "The tide as it was." :
                              $"Enemies: health +{health:0}%, damage +{damage:0}%, numbers +{spawns:0}%. Gold +{gold:0}%.";"""),
])
print("difficulty pass applied")

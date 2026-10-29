"""Healing economy (design update 2): heal sources recorded, capped class heals, Blood Price, block heal, Arcane Ward,
Bloodletting, dodge support, and the hero hit events weapons and passives react to."""
import os

from patchlib import edit

S = os.path.join(os.path.dirname(os.path.abspath(__file__)), "src", "Scripts")


def p(*parts):
    return os.path.join(S, *parts)


# --------------------------------------------------------------------------------------------------- run record
edit(p("Core", "RunRecord.cs"), [
    ("""    /// <summary>What a run did, for the pause sheet and the results screen: damage, kills and time per weapon slot.</summary>
    public sealed class RunRecord""", """    /// <summary>Where the hero's healing came from (balance telemetry).</summary>
    public enum HealSource
    {
        Regen,
        /// <summary>The class signature or active (Fury, Bulwark, Blood Price, Bloodletting).</summary>
        Class,
        /// <summary>Weapons and evolutions (Dawnbreaker, Briar Sanctum).</summary>
        Weapon,
        /// <summary>Passive item traits (Heartbeat).</summary>
        Passive,
        /// <summary>Roasts on the ground.</summary>
        Food,
        /// <summary>Level-up cards (Hearty Meal).</summary>
        Card,
        Other,
        Count,
    }

    /// <summary>What a run did, for the pause sheet and the results screen: damage, kills and time per weapon slot.</summary>
    public sealed class RunRecord"""),
    ("""        public float damageTaken;
        public float highestHit;""", """        public float damageTaken;
        /// <summary>Health restored per <see cref="HealSource"/> (only what landed below max health).</summary>
        public readonly float[] healedBySource = new float[(int)HealSource.Count];
        public float highestHit;"""),
    ("""            damageTaken = 0.0f;
            highestHit = 0.0f;""", """            damageTaken = 0.0f;
            Array.Clear(healedBySource, 0, healedBySource.Length);
            highestHit = 0.0f;"""),
    ("""        public void AddKill(int source)""", """        public void AddHeal(HealSource source, float amount)
        {
            healedBySource[Math.Clamp((int)source, 0, healedBySource.Length - 1)] += amount;
        }

        /// <summary>Total health restored this run.</summary>
        public float totalHealed
        {
            get
            {
                float total = 0.0f;
                foreach (float amount in healedBySource)
                {
                    total += amount;
                }
                return total;
            }
        }

        public void AddKill(int source)"""),
])

# --------------------------------------------------------------------------------------------------- hero
edit(p("Hero", "HeroController.cs"), [
    ("""        /// <summary>Raised when the Knight's block negates a hit.</summary>
        public event Action<Vector3> blocked;""", """        /// <summary>Raised when the Knight's block negates a hit.</summary>
        public event Action<Vector3> blocked;
        /// <summary>Raised when a ward charge absorbs a hit (the Mage's Arcane Ward).</summary>
        public event Action<Vector3> warded;
        /// <summary>Raised when a hit is dodged (Windstride Boots' Tailwind).</summary>
        public event Action<Vector3> dodged;

        /// <summary>Chance (0..1) to dodge a hit entirely; set by the loadout.</summary>
        public float dodgeChance;
        private int wardCharges;
        private float wardUntil;"""),
    ("""                blocked?.Invoke(from);
                return false;
            }
            float taken = run.stats.GetDamageTaken(amount);""", """                blocked?.Invoke(from);
                return false;
            }
            if (wardCharges > 0 && run.runTime < wardUntil)
            {
                wardCharges--;
                invulnerableTime = INVULNERABLE_AFTER_HIT;
                warded?.Invoke(from);
                return false;
            }
            if (dodgeChance > 0.0f && Random.Float() < dodgeChance)
            {
                invulnerableTime = INVULNERABLE_AFTER_HIT;
                dodged?.Invoke(from);
                return false;
            }
            float taken = run.stats.GetDamageTaken(amount);"""),
    ("""        public void Heal(float amount)
        {
            if (!isDead)
            {
                health = MathF.Min(health + amount, maxHealth);
            }
        }""", """        /// <summary>Restores health up to the maximum and records it under <paramref name="source"/>; returns what landed.</summary>
        public float Heal(float amount, HealSource source = HealSource.Other)
        {
            if (isDead || amount <= 0.0f)
            {
                return 0.0f;
            }
            float healed = MathF.Min(amount, maxHealth - health);
            if (healed <= 0.0f)
            {
                return 0.0f;
            }
            health += healed;
            Run.current?.record.AddHeal(source, healed);
            return healed;
        }

        /// <summary>The next <paramref name="hits"/> hits within <paramref name="seconds"/> are absorbed (the stronger ward wins).</summary>
        public void Ward(int hits, float seconds)
        {
            Run run = Run.current;
            if (run == null)
            {
                return;
            }
            wardCharges = Math.Max(run.runTime < wardUntil ? wardCharges : 0, hits);
            wardUntil = MathF.Max(wardUntil, run.runTime + seconds);
        }

        /// <summary>Ward charges left now (0 once the ward expired).</summary>
        public int wardHits => Run.current != null && Run.current.runTime < wardUntil ? wardCharges : 0;"""),
    ("""            float regen = stats[StatId.Regeneration];
            if (regen > 0.0f)
            {
                health = MathF.Min(health + regen * dt, maxHealth);
            }""", """            float regen = stats[StatId.Regeneration];
            if (regen > 0.0f)
            {
                Heal(regen * dt, HealSource.Regen);
            }"""),
])

# --------------------------------------------------------------------------------------------------- hit events
with open(p("Combat", "CombatEvents.cs"), "w", newline="\n") as f:
    f.write("""namespace Survivors
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
""")

edit(p("Combat", "Combat.cs"), [
    ("""            run.record.AddDamage(source, amount);
            if (!options.hideNumber && run.damageNumbers != null)""", """            run.record.AddDamage(source, amount);
            run.combatEvents.RaiseHit(row, source, amount, isCritical);
            if (!options.hideNumber && run.damageNumbers != null)"""),
])

edit(p("Core", "Run.cs"), [
    ("""        public RunRecord record;""", """        public RunRecord record;
        /// <summary>Hero hits as they land (passive traits, weapons that react to crits); cleared each run.</summary>
        public readonly CombatEvents combatEvents = new CombatEvents();"""),
    ("""            loadout?.Dispose();
            loadout = new Loadout(this, chosen);""", """            loadout?.Dispose();
            combatEvents.Clear();
            loadout = new Loadout(this, chosen);"""),
    ("""            return $"phase={phase} t={runTime:0.0} level={level} kills={record.kills} alive={enemies.count} projectiles={projectiles.count} " +
                   $"pickups={pickups.count} hp={health} gold={gold} weapons={weapons}";""", """            var healed = new System.Text.StringBuilder();
            for (int i = 0; i < (int)HealSource.Count; i++)
            {
                if (record.healedBySource[i] > 0.0f)
                {
                    healed.Append(((HealSource)i).ToString()).Append(':').Append(record.healedBySource[i].ToString("0")).Append(' ');
                }
            }
            return $"phase={phase} t={runTime:0.0} level={level} kills={record.kills} alive={enemies.count} projectiles={projectiles.count} " +
                   $"pickups={pickups.count} hp={health} gold={gold} taken={record.damageTaken:0} healed={record.totalHealed:0} [{healed}] weapons={weapons}";"""),
])

# --------------------------------------------------------------------------------------------------- heal call sites
edit(p("Pickups", "Drops.cs"), [("""                    run.hero.Heal(heal);
                    run.damageNumbers?.ShowHeal(run.hero.position + new Vector3(0.0f, 2.2f, 0.0f), heal);
                    run.effects.Emit(run.effects.Find("HealBurst"), run.hero.position, 1);""",
                                  """                    run.hero.Heal(heal, HealSource.Food);
                    run.damageNumbers?.ShowHeal(run.hero.position + new Vector3(0.0f, 2.2f, 0.0f), heal);
                    run.effects.Emit(run.effects.Find("HealBurst"), run.hero.position, 1);""")])
edit(p("Weapons", "Loadout.cs"), [("""                    float heal = run.hero.maxHealth * WeaponCatalog.HEARTY_MEAL_HEAL;
                    run.hero.Heal(heal);""", """                    float heal = run.hero.maxHealth * WeaponCatalog.HEARTY_MEAL_HEAL;
                    run.hero.Heal(heal, HealSource.Card);""")])
edit(p("Weapons", "Knight", "OathbladeArc.cs"), [("""            hero.Heal(amountToHeal);""", """            hero.Heal(amountToHeal, HealSource.Weapon);""")])
edit(p("Weapons", "Shared", "BrambleHalo.cs"), [("""                hero.Heal(heal);""", """                hero.Heal(heal, HealSource.Weapon);""")])
edit(p("Weapons", "Framework", "GroundZones.cs"), [("""                hero.Heal(spec.healPerTick);""", """                hero.Heal(spec.healPerTick, HealSource.Weapon);""")])

# --------------------------------------------------------------------------------------------------- barbarian
edit(p("Weapons", "Kits", "BarbarianKit.cs"), [
    ("""    /// Signature Bloodlust: each kill grants Fury (max 50), each Fury +1% Might and -0.4% Cooldown, decaying 5 per
    /// second 2 s after the last kill; at full Fury kills heal, the barbarian glows red and embers burn in his eyes.""",
     """    /// Signature Bloodlust: each kill grants Fury (max 50), each Fury +1% Might and -0.4% Cooldown, decaying 8 per
    /// second 2 s after the last kill; at full Fury kills heal (at most 1.5% of max health per second), the barbarian
    /// glows red and embers burn in his eyes. Blood Price: a landing with 25 Fury or more spends 25 to heal 8%."""),
    ("""        private const float FURY_DECAY = 5.0f;""", """        private const float FURY_DECAY = 8.0f;"""),
    ("""        private const float FULL_FURY_HEAL = 0.5f;""", """        private const float FULL_FURY_HEAL = 0.3f;
        /// <summary>Full Fury heals at most this share of max health per second, however fast the kills come.</summary>
        private const float FURY_HEAL_SHARE_PER_SECOND = 0.015f;
        /// <summary>Blood Price: the Fury a landing spends and the share of max health it heals.</summary>
        private const float BLOOD_PRICE_FURY = 25.0f;
        private const float BLOOD_PRICE_HEAL = 0.08f;
        private const float HEAL_NUMBER_HEIGHT = 2.2f;"""),
    ("""        private float emberTimer;
""", """        private float emberTimer;
        /// <summary>Health full Fury may still heal this second (refills at the per-second cap).</summary>
        private float furyHealBudget;
"""),
    ("""            if (fury >= FURY_MAX)
            {
                hero.Heal(FULL_FURY_HEAL);
            }""", """            if (fury >= FURY_MAX)
            {
                float heal = MathF.Min(FULL_FURY_HEAL, furyHealBudget);
                furyHealBudget -= hero.Heal(heal, HealSource.Class);
            }"""),
    ("""            if (ctx.clock - lastKill > FURY_GRACE && fury > 0.0f)""", """            float healCap = hero.maxHealth * FURY_HEAL_SHARE_PER_SECOND;
            furyHealBudget = MathF.Min(healCap, furyHealBudget + healCap * dt);
            if (ctx.clock - lastKill > FURY_GRACE && fury > 0.0f)"""),
    ("""            DrawLanding(center);
            sounds.PlayAt("leap_land", center);""", """            DrawLanding(center);
            PayBloodPrice(center);
            sounds.PlayAt("leap_land", center);"""),
    ("""        /// <summary>The landing: flash, shockwave, dust ring, debris and a radial crack ringed by rock spikes at the hit's edge.</summary>""",
     """        /// <summary>Blood Price: with enough Fury the landing spends some of it to heal, in a burst of red flame.</summary>
        private void PayBloodPrice(Vector3 center)
        {
            if (fury < BLOOD_PRICE_FURY)
            {
                return;
            }
            fury -= BLOOD_PRICE_FURY;
            float healed = hero.Heal(hero.maxHealth * BLOOD_PRICE_HEAL, HealSource.Class);
            if (healed > 0.0f)
            {
                ctx.run.damageNumbers?.ShowHeal(center + new Vector3(0.0f, HEAL_NUMBER_HEIGHT, 0.0f), healed);
            }
            furyBurst.Play(center + CHEST, 1.0f, 1.0f, FURY_RED);
            sounds.Play("heal");
        }

        /// <summary>The landing: flash, shockwave, dust ring, debris and a radial crack ringed by rock spikes at the hit's edge.</summary>"""),
])

# --------------------------------------------------------------------------------------------------- knight
edit(p("Weapons", "Kits", "KnightKit.cs"), [
    ("""        private const float BASH_GAP = 0.5f;""", """        private const float BASH_GAP = 0.5f;
        /// <summary>Every block heals this share of max health.</summary>
        private const float BLOCK_HEAL = 0.015f;
        private const float HEAL_NUMBER_HEIGHT = 2.2f;"""),
    ("""        private void OnBlocked(Vector3 from)
        {""", """        private void OnBlocked(Vector3 from)
        {
            float healed = hero.Heal(hero.maxHealth * BLOCK_HEAL, HealSource.Class);
            if (healed >= 1.0f)
            {
                ctx.run.damageNumbers?.ShowHeal(hero.position + new Vector3(0.0f, HEAL_NUMBER_HEIGHT, 0.0f), healed);
            }"""),
])

# --------------------------------------------------------------------------------------------------- mage
edit(p("Weapons", "Kits", "MageKit.cs"), [
    ("""        private const float BLINK_INVULNERABLE = 0.3f;""", """        private const float BLINK_INVULNERABLE = 0.3f;
        /// <summary>Arcane Ward: hits a blink absorbs, and for how long.</summary>
        private const int WARD_HITS = 2;
        private const float WARD_SECONDS = 3.0f;"""),
    ("""            hero.SetInvulnerable(BLINK_INVULNERABLE);""", """            hero.SetInvulnerable(BLINK_INVULNERABLE);
            hero.Ward(WARD_HITS, WARD_SECONDS);"""),
])

# --------------------------------------------------------------------------------------------------- rogue
edit(p("Weapons", "Kits", "RogueKit.cs"), [
    ("""        private const int MOMENTUM_MAX = 10;""", """        private const int MOMENTUM_MAX = 10;
        /// <summary>Bloodletting: a crit at full Momentum heals this much, at most this share of max health per second.</summary>
        private const float BLOODLETTING_HEAL = 0.15f;
        private const float BLOODLETTING_SHARE_PER_SECOND = 0.01f;"""),
    ("""        private int momentum;""", """        private int momentum;
        /// <summary>Health Bloodletting may still heal this second (refills at the per-second cap).</summary>
        private float bloodlettingBudget;"""),
    ("""                                            FxLayer.Ground("RogueShadowShock", 3.0f));
        }
""", """                                            FxLayer.Ground("RogueShadowShock", 3.0f));
            ctx.run.combatEvents.hit += OnHit;
        }

        public override void Dispose()
        {
            ctx.run.combatEvents.hit -= OnHit;
        }

        /// <summary>Bloodletting: crits at full Momentum heal, within the per-second cap.</summary>
        private void OnHit(int row, int source, float amount, bool isCritical)
        {
            if (!isCritical || momentum < MOMENTUM_MAX)
            {
                return;
            }
            float heal = MathF.Min(BLOODLETTING_HEAL, bloodlettingBudget);
            bloodlettingBudget -= hero.Heal(heal, HealSource.Class);
        }
"""),
    ("""        protected override void Step(float dt)
        {
            if (hero.isMoving)""", """        protected override void Step(float dt)
        {
            float healCap = hero.maxHealth * BLOODLETTING_SHARE_PER_SECOND;
            bloodlettingBudget = MathF.Min(healCap, bloodlettingBudget + healCap * dt);
            if (hero.isMoving)"""),
])

# --------------------------------------------------------------------------------------------------- hero select text
edit(p("Weapons", "HeroCatalog.cs"), [
    ("""                passiveText = "15% chance to block any hit. A block releases a shield bash shockwave (2 m, 15 + level damage). +1 Armor every 5 levels.",""",
     """                passiveText = "15% chance to block any hit. A block heals 1.5% of max health and releases a shield bash shockwave (2 m, 15 + level damage). +1 Armor every 5 levels.","""),
    ("""                passiveText = "Each kill grants 1 Fury (max 50): +1% Might and -0.4% Cooldown per Fury. Fury fades 2 s after the last kill. At 50 Fury kills heal.",""",
     """                passiveText = "Each kill grants 1 Fury (max 50): +1% Might and -0.4% Cooldown per Fury. Fury fades 2 s after the last kill. At 50 Fury kills heal, up to 1.5% of max health per second.","""),
    ("""                activeText = "Leap up to 7 m, invulnerable in the air, and land with a 4 m shockwave: 60 + 6 x level damage and a 1 s stun. Cooldown 12 s.",""",
     """                activeText = "Leap up to 7 m, invulnerable in the air, and land with a 4 m shockwave: 60 + 6 x level damage and a 1 s stun. Landing with 25 Fury or more spends it to heal 8% of max health. Cooldown 12 s.","""),
    ("""                activeText = "Teleport 7 m. The rift left behind detonates after 0.4 s for 50 + 5 x level damage. Cooldown 8 s.",""",
     """                activeText = "Teleport 7 m and ward yourself: the next 2 hits within 3 s are absorbed. The rift left behind detonates after 0.4 s for 50 + 5 x level damage. Cooldown 8 s.","""),
    ("""                passiveText = "Every 0.5 s of movement grants 1 Momentum (max 10): +3% crit chance and +1% move speed each. Standing still resets it.",""",
     """                passiveText = "Every 0.5 s of movement grants 1 Momentum (max 10): +3% crit chance and +1% move speed each. Standing still resets it. At full Momentum crits heal, up to 1% of max health per second.","""),
])
print("heal economy patched")

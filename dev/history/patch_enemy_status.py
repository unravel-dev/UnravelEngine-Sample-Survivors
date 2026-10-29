"""Enemy statuses for the class update: expose, venom, bloodied, and Kindle scaling of burns, chills and freezes."""
import os

from patchlib import edit

S = os.path.join(os.path.dirname(os.path.abspath(__file__)), "src", "Scripts", "Enemies")

edit(os.path.join(S, "EnemyTypes.cs"), [("""        /// <summary>The enemy was marked when it died.</summary>
        public bool wasMarked;
    }""", """        /// <summary>The enemy was marked when it died.</summary>
        public bool wasMarked;
        /// <summary>The enemy was bloodied when it died (Butcher's Chains).</summary>
        public bool wasBloodied;
    }""")])

edit(os.path.join(S, "EnemyWorld.cs"), [
    ("""        private readonly float[] markTime;
        private readonly float[] markBonus;
        private readonly float[] wardTime;""", """        private readonly float[] markTime;
        private readonly float[] markBonus;
        private readonly float[] exposeTime;
        private readonly float[] exposeBonus;
        private readonly float[] venomTime;
        private readonly float[] venomStacks;
        private readonly float[] venomDps;
        private readonly float[] bloodTime;
        private readonly float[] wardTime;"""),
    ("""            markTime = new float[size];
            markBonus = new float[size];
            wardTime = new float[size];""", """            markTime = new float[size];
            markBonus = new float[size];
            exposeTime = new float[size];
            exposeBonus = new float[size];
            venomTime = new float[size];
            venomStacks = new float[size];
            venomDps = new float[size];
            bloodTime = new float[size];
            wardTime = new float[size];"""),
    ("""            markTime[row] = 0.0f;
            markBonus[row] = 0.0f;
            wardTime[row] = 0.0f;""", """            markTime[row] = 0.0f;
            markBonus[row] = 0.0f;
            exposeTime[row] = 0.0f;
            exposeBonus[row] = 0.0f;
            venomTime[row] = 0.0f;
            venomStacks[row] = 0.0f;
            venomDps[row] = 0.0f;
            bloodTime[row] = 0.0f;
            wardTime[row] = 0.0f;"""),
    ("""        public bool hasTarget = true;""", """        public bool hasTarget = true;
        /// <summary>Multiplies the seconds of burns, chills and freezes (Everburn Wick's Kindle).</summary>
        public float statusDurationScale = 1.0f;
        /// <summary>Multiplies burn damage (Everburn Wick's Kindle).</summary>
        public float burnDamageScale = 1.0f;"""),
    ("""            if (slowTime[row] <= 0.0f || factor < slowFactor[row])
            {
                slowFactor[row] = factor;
            }
            slowTime[row] = MathF.Max(slowTime[row], seconds);""", """            if (slowTime[row] <= 0.0f || factor < slowFactor[row])
            {
                slowFactor[row] = factor;
            }
            slowTime[row] = MathF.Max(slowTime[row], seconds * statusDurationScale);"""),
    ("""            freezeTime[row] = MathF.Max(freezeTime[row], elite[row] ? seconds * 0.5f : seconds);""",
     """            seconds *= statusDurationScale;
            freezeTime[row] = MathF.Max(freezeTime[row], elite[row] ? seconds * 0.5f : seconds);"""),
    ("""            burnDps[row] = MathF.Max(burnDps[row], damagePerSecond);
            burnTime[row] = MathF.Max(burnTime[row], seconds);""", """            burnDps[row] = MathF.Max(burnDps[row], damagePerSecond * burnDamageScale);
            burnTime[row] = MathF.Max(burnTime[row], seconds * statusDurationScale);"""),
    ("""        public bool IsMarked(int row) => markTime[row] > 0.0f;
""", """        public bool IsMarked(int row) => markTime[row] > 0.0f;

        /// <summary>
        /// Exposes an enemy: it takes <paramref name="bonus"/> more damage from everything (0.2 = +20%) for some seconds;
        /// the strongest exposure wins. Separate from marks, so it shows no mark.
        /// </summary>
        public void Expose(int row, float bonus, float seconds)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            exposeBonus[row] = MathF.Max(exposeTime[row] > 0.0f ? exposeBonus[row] : 0.0f, bonus);
            exposeTime[row] = MathF.Max(exposeTime[row], seconds);
        }

        public bool IsExposed(int row) => exposeTime[row] > 0.0f;

        /// <summary>
        /// Adds venom stacks: each stack deals <paramref name="damagePerStack"/> per second for <paramref name="seconds"/>
        /// (refreshed by every application), up to <paramref name="maxStacks"/>; the stronger per-stack damage wins.
        /// </summary>
        public void AddVenom(int row, float stacks, float damagePerStack, float seconds, float maxStacks, int source = -1)
        {
            if (!IsTargetable(row))
            {
                return;
            }
            float current = venomTime[row] > 0.0f ? venomStacks[row] : 0.0f;
            venomStacks[row] = MathF.Min(MathF.Max(maxStacks, current), current + stacks);
            venomDps[row] = MathF.Max(venomTime[row] > 0.0f ? venomDps[row] : 0.0f, damagePerStack);
            venomTime[row] = MathF.Max(venomTime[row], seconds);
            lastSource[row] = source;
        }

        /// <summary>The venom stacks on an enemy now (0 when it carries none).</summary>
        public float GetVenomStacks(int row) => venomTime[row] > 0.0f ? venomStacks[row] : 0.0f;

        /// <summary>Removes an enemy's venom and returns the damage it had left to deal (stacks x damage x seconds left).</summary>
        public float ConsumeVenom(int row)
        {
            if (row < 0 || row >= rowCount || venomTime[row] <= 0.0f)
            {
                return 0.0f;
            }
            float remaining = venomStacks[row] * venomDps[row] * venomTime[row];
            venomTime[row] = 0.0f;
            venomStacks[row] = 0.0f;
            return remaining;
        }

        /// <summary>Bloodies an enemy for some seconds (Butcher's Chains): a tag weapons and Fury read.</summary>
        public void Bloody(int row, float seconds)
        {
            if (IsTargetable(row))
            {
                bloodTime[row] = MathF.Max(bloodTime[row], seconds);
            }
        }

        public bool IsBloodied(int row) => bloodTime[row] > 0.0f;
"""),
    ("""            return markTime[row] > 0.0f ? multiplier * (1.0f + markBonus[row]) : multiplier;""",
     """            if (exposeTime[row] > 0.0f)
            {
                multiplier *= 1.0f + exposeBonus[row];
            }
            return markTime[row] > 0.0f ? multiplier * (1.0f + markBonus[row]) : multiplier;"""),
    ("""            if (markTime[row] > 0.0f)
            {
                markTime[row] -= dt;
            }
            if (wardTime[row] > 0.0f)""", """            if (markTime[row] > 0.0f)
            {
                markTime[row] -= dt;
            }
            if (exposeTime[row] > 0.0f)
            {
                exposeTime[row] -= dt;
            }
            if (bloodTime[row] > 0.0f)
            {
                bloodTime[row] -= dt;
            }
            if (venomTime[row] > 0.0f)
            {
                venomTime[row] -= dt;
                health[row] -= venomStacks[row] * venomDps[row] * damageTakenScale[row] * dt;
                if (health[row] <= 0.0f)
                {
                    diedInStep[row] = true;
                }
            }
            if (wardTime[row] > 0.0f)"""),
    ("""            bool wasMarked = markTime[row] > 0.0f;
            states[row] = EnemyState.Dying;""", """            bool wasMarked = markTime[row] > 0.0f;
            bool wasBloodied = bloodTime[row] > 0.0f;
            states[row] = EnemyState.Dying;"""),
    ("""            markTime[row] = 0.0f;
            aliveCount--;""", """            markTime[row] = 0.0f;
            exposeTime[row] = 0.0f;
            venomTime[row] = 0.0f;
            bloodTime[row] = 0.0f;
            aliveCount--;"""),
    ("""                wasMarked = wasMarked,
            });""", """                wasMarked = wasMarked,
                wasBloodied = wasBloodied,
            });"""),
])
print("enemy statuses patched")

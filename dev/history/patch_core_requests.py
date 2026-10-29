"""Core requests from the UI and weapons modules (CORE_REQUESTS.md)."""
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "src", "Scripts")


def patch(rel, pairs):
    path = os.path.join(ROOT, rel)
    s = open(path, encoding="ascii").read()
    for a, b in pairs:
        assert s.count(a) == 1, (rel, a[:80], s.count(a))
        s = s.replace(a, b)
    open(path, "w", encoding="ascii", newline="\n").write(s)
    print("ok", rel)


# ---------------------------------------------------------------- Combat: hit options
patch("Combat/Combat.cs", [
    ("""    public static class Combat
    {""", """    /// <summary>How a weapon hit treats crits and its number.</summary>
    public struct HitOptions
    {
        /// <summary>Rolls a critical hit from the hero's stats (plus <see cref="critChanceBonus"/>).</summary>
        public bool canCrit;
        /// <summary>The hit is critical whatever the roll (Smoke Roll, Death Sentence).</summary>
        public bool forceCrit;
        public float critChanceBonus;
        /// <summary>Added to the crit damage multiplier (0.25 = +25%).</summary>
        public float critDamageBonus;
        /// <summary>Colour of the number: Normal picks Critical on a crit.</summary>
        public DamageKind numberKind;
        public bool hideNumber;

        /// <summary>A plain hit that may crit.</summary>
        public static HitOptions Default => new HitOptions { canCrit = true };
    }

    public static class Combat
    {"""),
    ("""        /// <summary>Damages one enemy as the hero; rolls a critical hit unless <paramref name="canCrit"/> is false.</summary>
        public static DamageResult DamageEnemy(int row, float damage, Vector3 push, int source, bool canCrit = true)
        {""", """        /// <summary>Damages one enemy as the hero; rolls a critical hit unless <paramref name="canCrit"/> is false.</summary>
        public static DamageResult DamageEnemy(int row, float damage, Vector3 push, int source, bool canCrit = true)
        {
            var options = new HitOptions { canCrit = canCrit };
            return DamageEnemy(row, damage, push, source, in options, out _);
        }

        /// <summary>Damages one enemy as the hero with explicit crit and number options; reports whether it crit.</summary>
        public static DamageResult DamageEnemy(int row, float damage, Vector3 push, int source, in HitOptions options, out bool isCritical)
        {
            isCritical = false;"""),
    ("""            bool isCritical = canCrit && Random.Float() < stats[StatId.CritChance];
            float amount = (isCritical ? damage * stats[StatId.CritDamage] : damage) * enemies.GetDamageTakenMultiplier(row);""",
     """            isCritical = options.forceCrit || options.canCrit && Random.Float() < stats[StatId.CritChance] + options.critChanceBonus;
            float critMultiplier = stats[StatId.CritDamage] + options.critDamageBonus;
            float amount = (isCritical ? damage * critMultiplier : damage) * enemies.GetDamageTakenMultiplier(row);"""),
    ("""            run.damageNumbers?.Show(position + new Vector3(0.0f, height, 0.0f), amount, isCritical);""",
     """            if (!options.hideNumber && run.damageNumbers != null)
            {
                DamageKind kind = isCritical ? DamageKind.Critical : options.numberKind;
                run.damageNumbers.Show(position + new Vector3(0.0f, height, 0.0f), amount, kind);
            }"""),
])

# ---------------------------------------------------------------- EnemyWorld: death flags, confuse, pull, launch
patch("Enemies/EnemyTypes.cs", [
    ("""        /// <summary>False for summoned enemies: no experience, no drops.</summary>
        public bool isRewarded;""", """        /// <summary>False for summoned enemies: no experience, no drops.</summary>
        public bool isRewarded;
        /// <summary>The enemy was frozen when it died (shatter effects).</summary>
        public bool wasFrozen;
        /// <summary>The enemy was marked when it died.</summary>
        public bool wasMarked;"""),
])
patch("Enemies/EnemyWorld.cs", [
    ("""        private readonly bool[] quickDeath;""", """        private readonly bool[] quickDeath;
        private readonly float[] confuseTime;
        private readonly float[] launchTime;
        private readonly float[] launchHeight;
        private const float LAUNCH_SECONDS = 0.45f;"""),
    ("""            quickDeath = new bool[size];""", """            quickDeath = new bool[size];
            confuseTime = new float[size];
            launchTime = new float[size];
            launchHeight = new float[size];"""),
    ("""            rewarded[row] = isRewarded;
            quickDeath[row] = false;""", """            rewarded[row] = isRewarded;
            quickDeath[row] = false;
            confuseTime[row] = 0.0f;
            launchTime[row] = 0.0f;"""),
    # death flags captured before the clear
    ("""        private void BeginDeath(int row)
        {
            EnemyType kind = types[typeOf[row]];""", """        private void BeginDeath(int row)
        {
            EnemyType kind = types[typeOf[row]];
            bool wasFrozen = freezeTime[row] > 0.0f;
            bool wasMarked = markTime[row] > 0.0f;"""),
    ("""                source = lastSource[row],
                isRewarded = rewarded[row],""", """                source = lastSource[row],
                isRewarded = rewarded[row],
                wasFrozen = wasFrozen,
                wasMarked = wasMarked,"""),
    # public API
    ("""        /// <summary>Wards an enemy (Hex Caster): faster and tougher for some seconds.</summary>""", """        /// <summary>Confuses an enemy: it wanders, ignores the hero and casts nothing for some seconds (smoke).</summary>
        public void Confuse(int row, float seconds)
        {
            if (IsTargetable(row) && !types[typeOf[row]].isProxy)
            {
                confuseTime[row] = MathF.Max(confuseTime[row], elite[row] ? seconds * 0.5f : seconds);
            }
        }

        /// <summary>
        /// Pulls every enemy within a radius toward a point: knockback toward the centre at <paramref name="speed"/>,
        /// strongest at the rim and 30% of it at the centre (vortices); returns how many were pulled.
        /// </summary>
        public int Pull(Vector3 center, float radius, float speed, Span<int> scratch)
        {
            int found = QueryCircle(center, radius, scratch);
            for (int i = 0; i < found; i++)
            {
                int row = scratch[i];
                Vector3 toward = center - currentPositions[row];
                toward.y = 0.0f;
                float distance = toward.magnitude;
                if (distance < MIN_SEPARATION_DISTANCE)
                {
                    continue;
                }
                float falloff = Mathf.Lerp(0.3f, 1.0f, MathF.Min(distance / MathF.Max(radius, 1e-3f), 1.0f));
                knockbacks[row] += toward / distance * (speed * falloff / MathF.Max(types[typeOf[row]].weight, 0.1f));
            }
            return found;
        }

        /// <summary>Pops an enemy up off the ground for a moment (looks only; it keeps its place on the ground plane).</summary>
        public void Launch(int row, float height)
        {
            if (IsTargetable(row) && !types[typeOf[row]].isProxy)
            {
                launchTime[row] = LAUNCH_SECONDS;
                launchHeight[row] = height / MathF.Max(types[typeOf[row]].weight, 1.0f);
            }
        }

        /// <summary>Wards an enemy (Hex Caster): faster and tougher for some seconds.</summary>"""),
    # step: tick confuse/launch
    ("""            if (squashTime[row] > 0.0f)
            {
                squashTime[row] -= dt;
            }""", """            if (squashTime[row] > 0.0f)
            {
                squashTime[row] -= dt;
            }
            if (launchTime[row] > 0.0f)
            {
                launchTime[row] -= dt;
            }"""),
    # chase: confusion wanders, never casts
    ("""            Vector3 desired = Vector3.zero;
            if (marchTime[row] > 0.0f)
            {""", """            Vector3 desired = Vector3.zero;
            if (confuseTime[row] > 0.0f)
            {
                confuseTime[row] -= dt;
                float wander = (((uint)row * 2654435761u) >> 8) * (2.0f * MathF.PI / 16777216.0f) + stateTime[row] * 0.7f;
                desired = new Vector3(MathF.Sin(wander), 0.0f, MathF.Cos(wander)) * (kind.speed * 0.5f);
            }
            else if (marchTime[row] > 0.0f)
            {"""),
    # transform: launch arc
    ("""            float yaw = yaws[row] + kind.yawOffset * DEGREES_TO_RADIANS;
            float cos = MathF.Cos(yaw) * scale * squashXZ;""", """            if (launchTime[row] > 0.0f)
            {
                float t = 1.0f - launchTime[row] / LAUNCH_SECONDS;
                position.y += 4.0f * t * (1.0f - t) * launchHeight[row];
            }
            float yaw = yaws[row] + kind.yawOffset * DEGREES_TO_RADIANS;
            float cos = MathF.Cos(yaw) * scale * squashXZ;"""),
])

# ---------------------------------------------------------------- ProjectileWorld: team query, silent removal
patch("Combat/ProjectileWorld.cs", [
    ("""        /// <summary>Ends a projectile and calls its handler's OnExpire.</summary>""", """        /// <summary>Live projectiles of a team within a ground radius (shields that block enemy shots).</summary>
        public int QueryCircle(Team team, Vector3 center, float radius, Span<int> rows)
        {
            int written = 0;
            for (int row = 0; row < rowCount && written < rows.Length; row++)
            {
                if (!alive[row] || data[row].team != team)
                {
                    continue;
                }
                float dx = data[row].position.x - center.x;
                float dz = data[row].position.z - center.z;
                float reach = radius + data[row].radius * data[row].scale;
                if (dx * dx + dz * dz <= reach * reach)
                {
                    rows[written++] = row;
                }
            }
            return written;
        }

        /// <summary>Ends a projectile without its handler's OnExpire (blocked or absorbed).</summary>
        public void Remove(int row)
        {
            if (row < 0 || row >= rowCount || !alive[row])
            {
                return;
            }
            alive[row] = false;
            liveCount--;
            freeRows.Push(row);
        }

        /// <summary>Ends a projectile and calls its handler's OnExpire.</summary>"""),
])

# ---------------------------------------------------------------- PickupWorld: attract within, markers
patch("Pickups/PickupWorld.cs", [
    ("""        /// <summary>Sends every gem (and gold) on the ground to the hero.</summary>""", """        /// <summary>Sends the gems and gold within a radius of a point to the hero (wisps carrying gems).</summary>
        public int AttractWithin(Vector3 center, float radius)
        {
            float limit = radius * radius;
            int attractedCount = 0;
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row] || attracted[row] || !(IsGem(kinds[row]) || kinds[row] == PickupKind.Gold))
                {
                    continue;
                }
                Vector3 offset = positions[row] - center;
                if (offset.x * offset.x + offset.z * offset.z <= limit)
                {
                    attracted[row] = true;
                    attractedCount++;
                }
            }
            return attractedCount;
        }

        /// <summary>Positions of the pickups of a kind (chest markers); returns how many were written.</summary>
        public int CollectPositions(PickupKind kind, Span<Vector3> buffer)
        {
            int written = 0;
            for (int row = 0; row < rowCount && written < buffer.Length; row++)
            {
                if (alive[row] && kinds[row] == kind)
                {
                    buffer[written++] = positions[row];
                }
            }
            return written;
        }

        /// <summary>The nearest pickup to a point (gems first), for the test autopilot.</summary>
        public bool FindNearest(Vector3 from, float maxDistance, out Vector3 position)
        {
            position = from;
            float best = maxDistance * maxDistance;
            bool found = false;
            for (int row = 0; row < rowCount; row++)
            {
                if (!alive[row] || attracted[row])
                {
                    continue;
                }
                Vector3 offset = positions[row] - from;
                float distance = offset.x * offset.x + offset.z * offset.z;
                if (distance < best)
                {
                    best = distance;
                    position = positions[row];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Sends every gem (and gold) on the ground to the hero.</summary>"""),
])

# ---------------------------------------------------------------- Feedback: hit-stop gap + shake scale
patch("Fx/Feedback.cs", [
    ("""        private const float HIT_STOP_SCALE = 0.05f;""", """        private const float HIT_STOP_SCALE = 0.05f;
        /// <summary>At most one global hit stop per this many seconds (design 10.1); boss phases bypass it.</summary>
        private const float HIT_STOP_GAP = 0.3f;"""),
    ("""        /// <summary>Player setting: camera shake on or off.</summary>
        public bool isShakeEnabled = true;""", """        /// <summary>Player setting: camera shake on or off.</summary>
        public bool isShakeEnabled = true;
        /// <summary>Player setting: camera shake strength, 0..1.</summary>
        public float shakeScale = 1.0f;
        private float lastHitStop = -10.0f;"""),
    ("""        public void HitStop(float seconds)
        {
            if (seconds <= hitStopRemaining)
            {
                return;
            }""", """        public void HitStop(float seconds, bool bypassGap = false)
        {
            if (seconds <= hitStopRemaining)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (!bypassGap && now - lastHitStop < HIT_STOP_GAP)
            {
                return;
            }
            lastHitStop = now;"""),
    ("""            if (isShakeEnabled)
            {
                camera?.Shake(strength, seconds);
            }""", """            if (isShakeEnabled && shakeScale > 0.0f)
            {
                camera?.Shake(strength * shakeScale, seconds);
            }"""),
])

# ---------------------------------------------------------------- Hero: glow
patch("Hero/HeroController.cs", [
    ("""        private readonly int[] nearby = new int[32];""", """        private readonly int[] nearby = new int[32];
        private Color glowColor;
        private float glowIntensity;
        private float glowTime;"""),
    ("""        public void SetInvulnerable(float seconds) => invulnerableTime = MathF.Max(invulnerableTime, seconds);""", """        public void SetInvulnerable(float seconds) => invulnerableTime = MathF.Max(invulnerableTime, seconds);

        /// <summary>Makes the hero glow (Shield Rush gold, full Fury red) for some seconds; hit flashes win over it.</summary>
        public void SetGlow(Color color, float intensity, float seconds)
        {
            glowColor = color;
            glowIntensity = intensity;
            glowTime = seconds;
        }"""),
    ("""            invulnerableTime -= dt;
            flashTime -= dt;""", """            invulnerableTime -= dt;
            flashTime -= dt;
            glowTime -= dt;"""),
    ("""            else if (blinkOff)
            {""", """            else if (glowTime > 0.0f)
            {
                look.emissiveColor = glowColor;
                look.emissiveIntensity = glowIntensity;
            }
            else if (blinkOff)
            {"""),
])

# ---------------------------------------------------------------- Run: markers, dread, shake scale, loadout dispose, autopilot gems
patch("Core/Run.cs", [
    ("""            loadout = new Loadout(this, chosen);""", """            loadout?.Dispose();
            loadout = new Loadout(this, chosen);"""),
    ("""            ui.Update(unscaledDt);
        }""", """            feedback.shakeScale = save.settings.screenShake;
            ui.Update(unscaledDt);
        }"""),
    ("""            loadout.Tick(dt);
            ui.UpdateHud();""", """            loadout.Tick(dt);
            ShowMarkers();
            ui.UpdateHud();"""),
    ("""        private void StepEnded(float dt, float unscaledDt)""", """        private readonly Vector3[] markerPositions = new Vector3[8];

        /// <summary>Off-screen arrows toward elites and chests (the UI draws only those off screen).</summary>
        private void ShowMarkers()
        {
            int chests = pickups.CollectPositions(PickupKind.Chest, markerPositions);
            for (int i = 0; i < chests; i++)
            {
                ui.ShowMarker(markerPositions[i], MarkerKind.Chest);
            }
            int elites = enemies.CollectElites(scratchRows.AsSpan(0, 8 - Math.Min(chests, 8)));
            for (int i = 0; i < elites; i++)
            {
                ui.ShowMarker(enemies.GetPosition(scratchRows[i]), MarkerKind.Elite);
            }
        }

        private void StepEnded(float dt, float unscaledDt)"""),
    ("""            Vector3 direction = away * 3.0f + tangent * 0.6f + inward;""", """            Vector3 seek = Vector3.zero;
            if (found < 3 && pickups.FindNearest(position, 12.0f, out Vector3 pickup))
            {
                Vector3 toPickup = pickup - position;
                toPickup.y = 0.0f;
                seek = toPickup.normalized * 1.2f;
            }
            Vector3 direction = away * 3.0f + tangent * 0.6f + inward + seek;"""),
])
patch("Enemies/EnemyWorld.cs", [
    ("""        /// <summary>Live enemies of a type (archetype caps).</summary>""", """        /// <summary>Rows of live elites (off-screen markers); returns how many were written.</summary>
        public int CollectElites(Span<int> rows)
        {
            int written = 0;
            for (int row = 0; row < rowCount && written < rows.Length; row++)
            {
                if (elite[row] && IsTargetable(row))
                {
                    rows[written++] = row;
                }
            }
            return written;
        }

        /// <summary>Live enemies of a type (archetype caps).</summary>"""),
])
patch("Enemies/WaveDirector.cs", [
    ("""        /// <summary>Health multiplier of enemies spawned now: 1 + 0.15 t + 0.02 t^2.</summary>
        public float healthScale => 1.0f + 0.15f * minutes + 0.02f * minutes * minutes;""", """        /// <summary>Dread chosen for this run (0..5): tougher enemies, a bigger budget, more experience.</summary>
        private int dread;

        /// <summary>Health multiplier of enemies spawned now: (1 + 0.15 t + 0.02 t^2) x (1 + 0.15 dread).</summary>
        public float healthScale => (1.0f + 0.15f * minutes + 0.02f * minutes * minutes) * (1.0f + 0.15f * dread);"""),
    ("""        /// <summary>Experience multiplier of a kill now: 1 + 0.1 t.</summary>
        public float experienceScale => 1.0f + 0.1f * minutes;""", """        /// <summary>Experience multiplier of a kill now: (1 + 0.1 t) x (1 + 0.1 dread).</summary>
        public float experienceScale => (1.0f + 0.1f * minutes) * (1.0f + 0.1f * dread);"""),
    ("""            lastDamageSource = "";
            PlaceOpeningRing();""", """            lastDamageSource = "";
            dread = Math.Clamp(run.save != null ? run.save.selectedDread : 0, 0, 5);
            PlaceOpeningRing();"""),
    ("""            float rate = (1.2f + 1.1f * t + 0.07f * t * t) * GetBudgetFactor();""", """            float rate = (1.2f + 1.1f * t + 0.07f * t * t) * GetBudgetFactor() * (1.0f + 0.1f * dread);"""),
])

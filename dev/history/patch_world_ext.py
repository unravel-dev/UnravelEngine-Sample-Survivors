import sys
p = sys.argv[1]
s = open(p, encoding='ascii').read()


def rep(a, b, n=1):
    global s
    assert s.count(a) == n, (a[:80], s.count(a))
    s = s.replace(a, b)


rep("""        private const float CORPSE_LINGER_SECONDS = 0.35f;
        private const float CORPSE_SINK_SPEED = 1.6f;""", """        /// <summary>Seconds of the death clip shown before the body shrinks away.</summary>
        private const float DEATH_CLIP_SECONDS = 0.55f;
        private const float DEATH_SHRINK_SECONDS = 0.15f;
        /// <summary>Most bodies playing their death at once; later deaths pop at once.</summary>
        private const int DEATH_CLIP_BUDGET = 300;
        private const float HIT_HOLD_SECONDS = 0.05f;
        private const float HIT_SQUASH_SECONDS = 0.1f;
        private const float WARD_SPEED = 1.3f;
        private const float WARD_DAMAGE_TAKEN = 0.75f;""")

rep("""        private readonly float[] markBonus;
""", """        private readonly float[] markBonus;
        private readonly float[] wardTime;
        private readonly float[] holdTime;
        private readonly float[] squashTime;
        private readonly float[] damageTakenScale;
        private readonly float[] marchTime;
        private readonly Vector3[] marchGoal;
        private readonly float[] marchSpeed;
        private readonly bool[] marchToPoint;
        private readonly bool[] rewarded;
        private readonly bool[] quickDeath;
        private int dyingCount;
        private readonly Dictionary<string, BakedAnimation> bakes = new Dictionary<string, BakedAnimation>();
""")
rep("""            markBonus = new float[size];
""", """            markBonus = new float[size];
            wardTime = new float[size];
            holdTime = new float[size];
            squashTime = new float[size];
            damageTakenScale = new float[size];
            marchTime = new float[size];
            marchGoal = new Vector3[size];
            marchSpeed = new float[size];
            marchToPoint = new bool[size];
            rewarded = new bool[size];
            quickDeath = new bool[size];
""")

rep("""            if (type == null || type.mesh == null)
            {
                throw new ArgumentException("An enemy type needs a mesh.");
            }""", """            if (type == null)
            {
                throw new ArgumentException("No enemy type.");
            }
            if (type.isProxy)
            {
                return AddType(type);
            }
            if (type.mesh == null)
            {
                throw new ArgumentException($"{type.name} needs a mesh.");
            }""")
rep("""            type.baked = BakedAnimation.Bake(type.mesh, bakedClips.ToArray(), Tuning.BAKE_FRAMES_PER_SECOND);""",
    """            type.baked = GetBake(type.mesh, bakedClips);""")
rep("""            type.looks = CreateLooks(type);
            type.index = types.Count;
            types.Add(type);
            maxRadius = MathF.Max(maxRadius, type.radius * type.scale * type.eliteScale);
            Array.Resize(ref bucketCounts, types.Count * (int)EnemyLook.Count);
            Array.Resize(ref bucketStarts, types.Count * (int)EnemyLook.Count);
            return type.index;
        }""", """            type.looks = CreateLooks(type);
            return AddType(type);
        }

        private int AddType(EnemyType type)
        {
            type.index = types.Count;
            types.Add(type);
            maxRadius = MathF.Max(maxRadius, type.radius * type.scale * type.eliteScale);
            Array.Resize(ref bucketCounts, types.Count * (int)EnemyLook.Count);
            Array.Resize(ref bucketStarts, types.Count * (int)EnemyLook.Count);
            return type.index;
        }

        /// <summary>One bake per mesh and clip list: tint variants of a model share it.</summary>
        private BakedAnimation GetBake(Mesh mesh, List<AnimationClip> clipList)
        {
            var key = new System.Text.StringBuilder(mesh.uid.ToString());
            foreach (AnimationClip clip in clipList)
            {
                key.Append('|').Append(clip.uid.ToString());
            }
            string id = key.ToString();
            if (!bakes.TryGetValue(id, out BakedAnimation bake) || !bake.isValid)
            {
                bake = BakedAnimation.Bake(mesh, clipList.ToArray(), Tuning.BAKE_FRAMES_PER_SECOND);
                bakes[id] = bake;
            }
            return bake;
        }""")

rep("""            foreach (EnemyType type in types)
            {
                type.baked?.Dispose();
                type.baked = null;""", """            foreach (BakedAnimation bake in bakes.Values)
            {
                bake.Dispose();
            }
            bakes.Clear();
            foreach (EnemyType type in types)
            {
                type.baked = null;""")

rep("""        public Enemy Spawn(int type, Vector3 position, bool isElite = false, float healthScale = 1.0f, float damageScale = 1.0f, bool rise = true)""",
    """        /// <param name="isRewarded">False for summoned enemies: no experience, no drops.</param>
        public Enemy Spawn(int type, Vector3 position, bool isElite = false, float healthScale = 1.0f, float damageScale = 1.0f, bool rise = true, bool isRewarded = true)""")
rep("""            stunTime[row] = 0.0f;
            markTime[row] = 0.0f;
            markBonus[row] = 0.0f;
            alive[row] = true;""", """            stunTime[row] = 0.0f;
            markTime[row] = 0.0f;
            markBonus[row] = 0.0f;
            wardTime[row] = 0.0f;
            holdTime[row] = 0.0f;
            squashTime[row] = 0.0f;
            damageTakenScale[row] = 1.0f;
            marchTime[row] = 0.0f;
            rewarded[row] = isRewarded;
            quickDeath[row] = false;
            alive[row] = true;""")
rep("""            bool hasRise = rise && kind.HasClip(EnemyClip.Rise);
            states[row] = rise ? EnemyState.Rising : EnemyState.Chase;""", """            bool hasRise = rise && kind.HasClip(EnemyClip.Rise);
            states[row] = rise && !kind.isProxy ? EnemyState.Rising : EnemyState.Chase;""")

rep("""            rowCount = 0;
            freeCount = 0;
            aliveCount = 0;
            isGridDirty = true;
        }""", """            rowCount = 0;
            freeCount = 0;
            aliveCount = 0;
            dyingCount = 0;
            isGridDirty = true;
        }""")

rep("""            health[row] -= amount;
            flashTime[row] = FLASH_SECONDS;
            lastSource[row] = source;""", """            health[row] -= amount;
            flashTime[row] = FLASH_SECONDS;
            holdTime[row] = HIT_HOLD_SECONDS;
            squashTime[row] = HIT_SQUASH_SECONDS;
            lastSource[row] = source;""")
rep("""            float multiplier = freezeTime[row] > 0.0f ? Tuning.FROZEN_DAMAGE_TAKEN : 1.0f;
            return markTime[row] > 0.0f ? multiplier * (1.0f + markBonus[row]) : multiplier;""", """            float multiplier = damageTakenScale[row] * (freezeTime[row] > 0.0f ? Tuning.FROZEN_DAMAGE_TAKEN : 1.0f);
            if (wardTime[row] > 0.0f)
            {
                multiplier *= WARD_DAMAGE_TAKEN;
            }
            return markTime[row] > 0.0f ? multiplier * (1.0f + markBonus[row]) : multiplier;""")

rep("""        /// <summary>Whether the enemy burns now.</summary>""", """        /// <summary>Wards an enemy (Hex Caster): faster and tougher for some seconds.</summary>
        public void Ward(int row, float seconds)
        {
            if (IsTargetable(row))
            {
                wardTime[row] = MathF.Max(wardTime[row], seconds);
            }
        }

        public bool IsWarded(int row) => wardTime[row] > 0.0f;

        /// <summary>Scales the damage a row takes (bosses guarding, channeling, dazed); 1 = normal.</summary>
        public void SetDamageTakenScale(int row, float scale)
        {
            if (row >= 0 && row < rowCount && alive[row])
            {
                damageTakenScale[row] = scale;
            }
        }

        /// <summary>
        /// Overrides an enemy's chase for some seconds: it walks to <paramref name="goal"/> (a point) or along it (a
        /// direction), ignoring the hero (encircling rings, swarm rushes). It still attacks when its abilities fire.
        /// </summary>
        public void March(int row, Vector3 goal, bool toPoint, float speed, float seconds)
        {
            if (row < 0 || row >= rowCount || !alive[row])
            {
                return;
            }
            marchGoal[row] = toPoint ? goal : GetFlatDirection(goal, yaws[row]);
            marchToPoint[row] = toPoint;
            marchSpeed[row] = speed;
            marchTime[row] = seconds;
        }

        /// <summary>Puts a row somewhere at once (recycling far enemies, bosses driving their proxy row).</summary>
        public void SetPosition(int row, Vector3 position)
        {
            if (row < 0 || row >= rowCount || !alive[row])
            {
                return;
            }
            currentPositions[row] = position;
            nextPositions[row] = position;
            jumped[row] = true;
            isGridDirty = true;
        }

        /// <summary>Sets a row's heading (radians about up, 0 = +Z).</summary>
        public void SetYaw(int row, float yaw) => yaws[row] = yaw;

        /// <summary>The state an enemy is in.</summary>
        public EnemyState GetState(int row) => states[row];

        /// <summary>Whether killing the row gives experience and drops.</summary>
        public bool IsRewarded(int row) => rewarded[row];

        /// <summary>Health left (not the fraction).</summary>
        public float GetHealth(int row) => health[row];

        /// <summary>Enemies within <paramref name="radius"/> of a point that are not dying (allies count for casters).</summary>
        public int CountNear(Vector3 center, float radius)
        {
            EnsureGrid();
            var counter = new CountVisitor { collidable = collidable };
            grid.Visit(center, radius, ref counter);
            return counter.count;
        }

        /// <summary>Whether the enemy burns now.</summary>""")

rep("""                EnemyType kind = types[typeOf[row]];
                stateTime[row] += dt;
                if (states[row] == EnemyState.Dying)""", """                EnemyType kind = types[typeOf[row]];
                stateTime[row] += dt;
                if (kind.isProxy)
                {
                    nextPositions[row] = currentPositions[row];
                    TickStatus(row, dt);
                    continue;
                }
                if (states[row] == EnemyState.Dying)""")
rep("""            if (markTime[row] > 0.0f)
            {
                markTime[row] -= dt;
            }""", """            if (markTime[row] > 0.0f)
            {
                markTime[row] -= dt;
            }
            if (wardTime[row] > 0.0f)
            {
                wardTime[row] -= dt;
            }
            if (holdTime[row] > 0.0f)
            {
                holdTime[row] -= dt;
            }
            if (squashTime[row] > 0.0f)
            {
                squashTime[row] -= dt;
            }""")
rep("""                float speedFactor = slowTime[row] > 0.0f ? slowFactor[row] : 1.0f;""",
    """                float speedFactor = (slowTime[row] > 0.0f ? slowFactor[row] : 1.0f) * (wardTime[row] > 0.0f ? WARD_SPEED : 1.0f);""")

rep("""            Vector3 desired = Vector3.zero;
            if (hasTarget && distance > 1e-4f)
            {
                Vector3 direction = toTarget / distance;
                desired = GetDesiredVelocity(row, kind, direction, gap);
                if (TryStartCast(row, kind, gap, dt, commands))
                {
                    return;
                }
            }""", """            Vector3 desired = Vector3.zero;
            if (marchTime[row] > 0.0f)
            {
                marchTime[row] -= dt;
                desired = GetMarchVelocity(row, position);
                if (hasTarget && TryStartCast(row, kind, gap, dt, commands))
                {
                    return;
                }
            }
            else if (hasTarget && distance > 1e-4f)
            {
                Vector3 direction = toTarget / distance;
                desired = GetDesiredVelocity(row, kind, direction, gap);
                if (TryStartCast(row, kind, gap, dt, commands))
                {
                    return;
                }
            }""")
rep("""        private Vector3 GetDesiredVelocity(int row, EnemyType kind, Vector3 direction, float gap)""", """        private Vector3 GetMarchVelocity(int row, Vector3 position)
        {
            if (!marchToPoint[row])
            {
                return marchGoal[row] * marchSpeed[row];
            }
            Vector3 to = marchGoal[row] - position;
            to.y = 0.0f;
            float length = to.magnitude;
            return length > 0.25f ? to * (marchSpeed[row] / length) : Vector3.zero;
        }

        private Vector3 GetDesiredVelocity(int row, EnemyType kind, Vector3 direction, float gap)""")
rep("""                EnemyAbility ability = kind.abilities[i];
                if (started || gap < ability.minRange || gap > ability.range)
                {
                    continue;
                }""", """                EnemyAbility ability = kind.abilities[i];
                if (started || gap < ability.minRange || gap > ability.range || !HasAllies(row, ability))
                {
                    continue;
                }""")
rep("""        private void StepCasting(int row, EnemyType kind, float dt, float speedFactor, List<EnemyCommand> commands)""", """        /// <summary>Whether the ally count around a row suits an ability (Raise wants few, Ward wants many).</summary>
        private bool HasAllies(int row, EnemyAbility ability)
        {
            if (ability.minAllies <= 0 && ability.maxAllies == int.MaxValue)
            {
                return true;
            }
            var counter = new CountVisitor { collidable = collidable };
            grid.Visit(currentPositions[row], ability.allyRadius, ref counter);
            int allies = counter.count - 1;
            return allies >= ability.minAllies && allies <= ability.maxAllies;
        }

        private void StepCasting(int row, EnemyType kind, float dt, float speedFactor, List<EnemyCommand> commands)""")

rep("""            if (!castFired[row] && time >= ability.hitTime)
            {
                castFired[row] = true;
                commands.Add(MakeCommand(row, castAbility[row], EnemyCommand.Phase.Hit));""", """            if (!castFired[row] && time >= ability.hitTime)
            {
                castFired[row] = true;
                if (ability.effect != EnemyEffect.Charge)
                {
                    commands.Add(MakeCommand(row, castAbility[row], EnemyCommand.Phase.Hit));
                }""")
rep("""            if (time >= ability.duration)
            {
                BeginChase(row, kind);
            }
        }""", """            float chargeEnd = ability.hitTime + ability.chargeTime;
            if (ability.effect == EnemyEffect.Charge && castFired[row] && time - dt < chargeEnd && time >= chargeEnd)
            {
                velocities[row] = Vector3.zero;
                commands.Add(MakeCommand(row, castAbility[row], EnemyCommand.Phase.Hit));
            }
            if (time >= ability.duration)
            {
                BeginChase(row, kind);
            }
        }""")

rep("""        private void AdvanceClip(int row, float dt, bool loops)
        {
            EnemyType kind = types[typeOf[row]];""", """        private void AdvanceClip(int row, float dt, bool loops)
        {
            if (holdTime[row] > 0.0f && states[row] != EnemyState.Dying)
            {
                return;
            }
            EnemyType kind = types[typeOf[row]];""")

rep("""                if (states[row] == EnemyState.Dying && stateTime[row] >= GetDeathLength(row) + CORPSE_LINGER_SECONDS)
                {
                    Release(row);
                }""", """                if (states[row] == EnemyState.Dying && stateTime[row] >= GetDeathLength(row) + DEATH_SHRINK_SECONDS)
                {
                    dyingCount--;
                    Release(row);
                }""")

rep("""            clips[row] = kind.GetClip(alternate ? EnemyClip.DeathAlt : EnemyClip.Death);
            clipTimes[row] = 0.0f;""", """            clips[row] = kind.GetClip(alternate ? EnemyClip.DeathAlt : EnemyClip.Death);
            clipTimes[row] = 0.0f;
            quickDeath[row] = dyingCount >= DEATH_CLIP_BUDGET || kind.isProxy;
            dyingCount++;""")
rep("""                experience = kind.experience * (elite[row] ? 5.0f : 1.0f),
                source = lastSource[row],""", """                experience = rewarded[row] ? kind.experience * (elite[row] ? Tuning.ELITE_EXPERIENCE : 1.0f) : 0.0f,
                source = lastSource[row],
                isRewarded = rewarded[row],""")
rep("""        private float GetDeathLength(int row)
        {
            EnemyType kind = types[typeOf[row]];
            return kind.HasClip(EnemyClip.Death) ? kind.baked.GetLength(clips[row]) : 0.0f;
        }""", """        /// <summary>Seconds of death clip a dying row shows before it shrinks away.</summary>
        private float GetDeathLength(int row)
        {
            EnemyType kind = types[typeOf[row]];
            if (quickDeath[row] || !kind.HasClip(EnemyClip.Death))
            {
                return 0.0f;
            }
            return MathF.Min(kind.baked.GetLength(clips[row]), DEATH_CLIP_SECONDS);
        }""")

rep("""                if (!alive[row])
                {
                    continue;
                }
                int look = (int)GetLook(row);""", """                if (!alive[row] || types[typeOf[row]].isProxy)
                {
                    continue;
                }
                int look = (int)GetLook(row);""")
rep("""                if (!alive[row])
                {
                    continue;
                }
                flashTime[row] = MathF.Max(flashTime[row] - dt, 0.0f);""", """                if (!alive[row] || types[typeOf[row]].isProxy)
                {
                    continue;
                }
                flashTime[row] = MathF.Max(flashTime[row] - dt, 0.0f);""")

rep("""            Vector3 position = currentPositions[row];
            float scale = scaleOf[row];
            if (states[row] == EnemyState.Dying)
            {
                float sink = stateTime[row] - GetDeathLength(row);
                if (sink > 0.0f)
                {
                    position.y -= sink * CORPSE_SINK_SPEED;
                }
            }
            else if (states[row] == EnemyState.Rising && !kind.HasClip(EnemyClip.Rise))
            {
                scale *= Mathf.Clamp01(stateTime[row] / RISE_SECONDS_WITHOUT_CLIP);
            }
            float yaw = yaws[row] + kind.yawOffset * DEGREES_TO_RADIANS;
            float cos = MathF.Cos(yaw) * scale;
            float sin = MathF.Sin(yaw) * scale;
            return new Matrix4x4(new Vector4(cos, 0.0f, -sin, 0.0f),
                                 new Vector4(0.0f, scale, 0.0f, 0.0f),
                                 new Vector4(sin, 0.0f, cos, 0.0f),
                                 new Vector4(position.x, position.y, position.z, 1.0f));""", """            Vector3 position = currentPositions[row];
            float scale = scaleOf[row];
            float squashXZ = 1.0f;
            float squashY = 1.0f;
            if (states[row] == EnemyState.Dying)
            {
                float shrink = stateTime[row] - GetDeathLength(row);
                if (shrink > 0.0f)
                {
                    scale *= MathF.Max(1.0f - shrink / DEATH_SHRINK_SECONDS, 0.0f);
                }
            }
            else if (states[row] == EnemyState.Rising && !kind.HasClip(EnemyClip.Rise))
            {
                // Climbs out of the ground: starts below it and rises over the rise time.
                float t = Mathf.Clamp01(stateTime[row] / RISE_SECONDS_WITHOUT_CLIP);
                position.y -= (1.0f - t) * Tuning.RISE_DEPTH * scale;
            }
            else if (squashTime[row] > 0.0f)
            {
                float t = squashTime[row] / HIT_SQUASH_SECONDS;
                squashXZ = 1.0f + 0.15f * t;
                squashY = 1.0f - 0.15f * t;
            }
            float yaw = yaws[row] + kind.yawOffset * DEGREES_TO_RADIANS;
            float cos = MathF.Cos(yaw) * scale * squashXZ;
            float sin = MathF.Sin(yaw) * scale * squashXZ;
            return new Matrix4x4(new Vector4(cos, 0.0f, -sin, 0.0f),
                                 new Vector4(0.0f, scale * squashY, 0.0f, 0.0f),
                                 new Vector4(sin, 0.0f, cos, 0.0f),
                                 new Vector4(position.x, position.y, position.z, 1.0f));""")

rep("""        /// <summary>Sums how far the neighbours of one row overlap it, as a push away from them.</summary>""", """        /// <summary>Counts the collidable items a query visits.</summary>
        private struct CountVisitor : ISpatialHashVisitor
        {
            public bool[] collidable;
            public int count;

            public void Visit(int item, float distance)
            {
                if (collidable[item])
                {
                    count++;
                }
            }
        }

        /// <summary>Sums how far the neighbours of one row overlap it, as a push away from them.</summary>""")
open(p, 'w', encoding='ascii', newline='\n').write(s)
print('ok')

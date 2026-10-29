using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The per-row step: statuses, movement and the ability state machine. Runs on worker threads: it reads every row
    /// and writes only the rows of its own block; ability effects leave as commands for the main thread.
    /// </summary>
    public sealed partial class EnemyWorld
    {
        private const float OVERLAP_CORRECTION = 0.25f;
        private const float MIN_TURNING_SPEED = 0.05f;
        private const float MIN_SEPARATION_DISTANCE = 1e-4f;
        /// <summary>Separation pushes harder on small bodies, but never as if they were smaller than this (m).</summary>
        private const float MIN_SEPARATION_RADIUS = 0.05f;
        private const float MIN_TARGET_DISTANCE = 1e-4f;
        /// <summary>Knockback decays as e^(-damping x t): a push of N m/s carries an enemy N / damping metres.</summary>
        public const float KNOCKBACK_DAMPING = 9.0f;
        private const float STAGGER_SECONDS = 0.35f;
        private const float RISE_SECONDS_WITHOUT_CLIP = 0.35f;
        private const float MAX_WALK_RATE = 1.6f;
        private const float KEEP_RANGE_ORBIT = 0.45f;
        /// <summary>A confused enemy wanders at this share of its speed, its heading turning this many radians per second.</summary>
        private const float WANDER_SPEED_SHARE = 0.5f;
        private const float WANDER_TURN_RATE = 0.7f;
        /// <summary>A march to a point ends its walk this close to it (m).</summary>
        private const float MARCH_ARRIVAL_DISTANCE = 0.25f;
        /// <summary>Knuth's multiplicative hash: spreads consecutive row indices over the whole uint range.</summary>
        private const uint KNUTH_HASH = 2654435761u;
        /// <summary>A second odd multiplier, so a pair of rows hashes apart from either row alone.</summary>
        private const uint PAIR_HASH = 40503u;
        /// <summary>The top 24 bits of a hash as a share of a turn.</summary>
        private const int HASH_DROPPED_BITS = 8;
        private const float HASH_24_BIT_RANGE = 16777216.0f;
        private const float HASH_32_BIT_RANGE = 4294967296.0f;
        private const float FULL_TURN = 2.0f * MathF.PI;
        private const float MIN_OBSTACLE_DISTANCE_SQUARED = 1e-8f;
        private const float MIN_ARENA_DISTANCE = 1e-4f;

        /// <summary>How hard overlapping enemies push each other apart.</summary>
        public float separation = 1.0f;

        /// <summary>Steps rows [first, last).</summary>
        private void StepRows(int first, int last, float dt, List<EnemyCommand> commands)
        {
            for (int row = first; row < last; row++)
            {
                if (alive[row])
                {
                    StepRow(row, dt, commands);
                }
                else
                {
                    nextPositions[row] = currentPositions[row];
                }
            }
        }

        private void StepRow(int row, float dt, List<EnemyCommand> commands)
        {
            EnemyType kind = types[typeOf[row]];
            if (kind.isProxy)
            {
                // A proxy's owner moves it; the row only keeps its timers so its statuses wear off.
                stateTime[row] += dt;
                nextPositions[row] = currentPositions[row];
                TickStatus(row, dt);
                return;
            }
            if (states[row] == EnemyState.Dying)
            {
                stateTime[row] += dt;
                nextPositions[row] = currentPositions[row];
                AdvanceClip(row, dt, false);
                return;
            }
            // Read before the countdown: a freeze or stun holds the row through its last partial frame.
            bool isHeld = IsHeld(row);
            TickStatus(row, dt);
            if (isHeld)
            {
                // The state clock stops while held, so a cast resumes where it was.
                MoveRow(row, kind, Vector3.zero, dt, 0.0f);
                return;
            }
            float clockStep = GetStateClockStep(row, dt);
            stateTime[row] += clockStep;
            float speedFactor = GetSpeedFactor(row);
            switch (states[row])
            {
                case EnemyState.Rising:
                    StepRising(row, kind, dt);
                    break;
                case EnemyState.Stagger:
                    MoveRow(row, kind, Vector3.zero, dt, speedFactor);
                    AdvanceClip(row, dt, false);
                    if (stateTime[row] >= STAGGER_SECONDS)
                    {
                        BeginChase(row, kind);
                    }
                    break;
                case EnemyState.Casting:
                    StepCasting(row, kind, dt, clockStep, speedFactor, commands);
                    break;
                default:
                    StepChase(row, kind, dt, speedFactor, commands);
                    break;
            }
        }

        /// <summary>How far the state clock moves this frame: a hasted wind-up runs faster than real time.</summary>
        private float GetStateClockStep(int row, float dt)
        {
            bool isWindingUp = states[row] == EnemyState.Casting && !castFired[row];
            return isWindingUp ? dt / statuses[row].windupScale : dt;
        }

        private void StepRising(int row, EnemyType kind, float dt)
        {
            nextPositions[row] = currentPositions[row];
            AdvanceClip(row, dt, false);
            float riseLength = kind.HasClip(EnemyClip.Rise) ? kind.GetClipLength(EnemyClip.Rise) : RISE_SECONDS_WITHOUT_CLIP;
            if (stateTime[row] >= riseLength)
            {
                BeginChase(row, kind);
            }
        }

        private void StepChase(int row, EnemyType kind, float dt, float speedFactor, List<EnemyCommand> commands)
        {
            Vector3 position = currentPositions[row];
            Vector3 toTarget = new Vector3(targetPosition.x - position.x, 0.0f, targetPosition.z - position.z);
            float distance = toTarget.magnitude;
            float gap = distance - targetRadius - radiusOf[row];
            Vector3 desired = Vector3.zero;
            if (statuses[row].confuseTime > 0.0f)
            {
                desired = GetWanderVelocity(row, kind);
            }
            else if (marchTime[row] > 0.0f)
            {
                marchTime[row] -= dt;
                desired = GetMarchVelocity(row, position);
                if (hasTarget && TryStartCast(row, kind, gap, dt, commands))
                {
                    return;
                }
            }
            else if (hasTarget && distance > MIN_TARGET_DISTANCE)
            {
                Vector3 direction = toTarget / distance;
                desired = GetDesiredVelocity(row, kind, direction, gap);
                if (TryStartCast(row, kind, gap, dt, commands))
                {
                    return;
                }
            }
            MoveRow(row, kind, desired, dt, speedFactor);
            float walkSpeed = velocities[row].magnitude;
            float rate = kind.walkClipPerSpeed > 0.0f ? MathF.Min(walkSpeed * kind.walkClipPerSpeed, MAX_WALK_RATE) : speedFactor;
            clipRates[row] = rate;
            AdvanceClip(row, dt, true);
        }

        /// <summary>A confused row drifts on a slowly turning heading of its own.</summary>
        private Vector3 GetWanderVelocity(int row, EnemyType kind)
        {
            float rowAngle = (((uint)row * KNUTH_HASH) >> HASH_DROPPED_BITS) * (FULL_TURN / HASH_24_BIT_RANGE);
            float wander = rowAngle + stateTime[row] * WANDER_TURN_RATE;
            return BossGeometry.FromYawRadians(wander) * (kind.speed * WANDER_SPEED_SHARE);
        }

        private Vector3 GetMarchVelocity(int row, Vector3 position)
        {
            if (!marchToPoint[row])
            {
                return marchGoal[row] * marchSpeed[row];
            }
            Vector3 to = marchGoal[row] - position;
            to.y = 0.0f;
            float length = to.magnitude;
            return length > MARCH_ARRIVAL_DISTANCE ? to * (marchSpeed[row] / length) : Vector3.zero;
        }

        private Vector3 GetDesiredVelocity(int row, EnemyType kind, Vector3 direction, float gap)
        {
            float speed = kind.speed;
            if (kind.movement == EnemyMovement.KeepRange)
            {
                // Inside the band: circle the hero; outside: close in or back off.
                float distance = gap + targetRadius + radiusOf[row];
                var tangent = new Vector3(direction.z, 0.0f, -direction.x) * ((row & 1) == 0 ? 1.0f : -1.0f);
                if (distance < kind.holdMin)
                {
                    return (-direction + tangent * KEEP_RANGE_ORBIT) * speed;
                }
                if (distance <= kind.holdMax)
                {
                    return tangent * (speed * KEEP_RANGE_ORBIT);
                }
                return direction * speed;
            }
            if (gap <= 0.0f)
            {
                return Vector3.zero;
            }
            // Brakes in time to stop at the hero's edge instead of running through it.
            float braking = MathF.Sqrt(2.0f * kind.acceleration * gap);
            return direction * MathF.Min(speed, braking);
        }

        /// <summary>Counts the ability cooldowns down and starts the first ready ability in range; writes the Begin command.</summary>
        private bool TryStartCast(int row, EnemyType kind, float gap, float dt, List<EnemyCommand> commands)
        {
            bool started = false;
            for (int i = 0; i < kind.abilities.Length; i++)
            {
                int slot = row * MAX_ABILITIES + i;
                if (cooldowns[slot] > 0.0f)
                {
                    cooldowns[slot] -= dt;
                    continue;
                }
                EnemyAbility ability = kind.abilities[i];
                if (started || gap < ability.minRange || gap > ability.range || !HasAllies(row, ability))
                {
                    continue;
                }
                started = true;
                BeginCast(row, kind, i);
                commands.Add(MakeCommand(row, i, EnemyCommand.Phase.Begin));
            }
            if (started)
            {
                nextPositions[row] = currentPositions[row];
            }
            return started;
        }

        private void BeginCast(int row, EnemyType kind, int abilityIndex)
        {
            EnemyAbility ability = kind.abilities[abilityIndex];
            cooldowns[row * MAX_ABILITIES + abilityIndex] = ability.cooldown;
            states[row] = EnemyState.Casting;
            stateTime[row] = 0.0f;
            castAbility[row] = abilityIndex;
            castFired[row] = false;
            castTarget[row] = targetPosition;
            clips[row] = kind.GetClip(ability.clip);
            clipTimes[row] = 0.0f;
            clipRates[row] = ability.clipSpeed;
            if (ability.facesTarget)
            {
                Vector3 to = targetPosition - currentPositions[row];
                yaws[row] = MathF.Atan2(to.x, to.z);
            }
        }

        /// <summary>Whether the ally count around a row suits an ability (Raise wants few, Ward wants many).</summary>
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

        /// <param name="clockStep">How far the state clock moved this frame (see <see cref="GetStateClockStep"/>).</param>
        private void StepCasting(int row, EnemyType kind, float dt, float clockStep, float speedFactor, List<EnemyCommand> commands)
        {
            EnemyAbility ability = kind.abilities[castAbility[row]];
            float time = stateTime[row];
            bool isCharge = ability.effect == EnemyEffect.Charge;
            float chargeEnd = ability.hitTime + ability.chargeTime;
            Vector3 desired = Vector3.zero;
            if (isCharge && castFired[row] && time < chargeEnd)
            {
                Vector3 direction = GetFlatDirection(castTarget[row] - currentPositions[row], yaws[row]);
                desired = direction * ability.speed;
                velocities[row] = desired;
            }
            else if (!castFired[row] && ability.facesTarget && hasTarget)
            {
                // Tracks the hero during the wind-up, so a slow cast still points the right way.
                Vector3 to = targetPosition - currentPositions[row];
                TurnToward(row, kind, MathF.Atan2(to.x, to.z), dt);
                castTarget[row] = ability.effect == EnemyEffect.GroundBurst ? castTarget[row] : targetPosition;
            }
            MoveRow(row, kind, desired, dt, isCharge ? 1.0f : speedFactor, !isCharge);
            AdvanceClip(row, dt * speedFactor, false);
            if (!castFired[row] && time >= ability.hitTime)
            {
                castFired[row] = true;
                if (!isCharge)
                {
                    commands.Add(MakeCommand(row, castAbility[row], EnemyCommand.Phase.Hit));
                }
            }
            // A charge hits once, where its dash ends.
            if (isCharge && castFired[row] && time - clockStep < chargeEnd && time >= chargeEnd)
            {
                velocities[row] = Vector3.zero;
                commands.Add(MakeCommand(row, castAbility[row], EnemyCommand.Phase.Hit));
            }
            if (time >= ability.duration)
            {
                BeginChase(row, kind);
            }
        }

        private void BeginChase(int row, EnemyType kind)
        {
            states[row] = EnemyState.Chase;
            stateTime[row] = 0.0f;
            castAbility[row] = -1;
            clips[row] = kind.GetClip(EnemyClip.Walk);
            clipTimes[row] = Random.Range(0.0f, kind.GetClipLength(EnemyClip.Walk));
            clipRates[row] = 1.0f;
        }

        /// <summary>Moves a row toward a desired velocity with separation, knockback, obstacles and the arena edge.</summary>
        private void MoveRow(int row, EnemyType kind, Vector3 desired, float dt, float speedFactor, bool turns = true)
        {
            Vector3 position = currentPositions[row];
            var push = new SeparationVisitor
            {
                positions = currentPositions,
                radii = radiusOf,
                collidable = collidable,
                row = row,
                position = position,
                radius = radiusOf[row],
            };
            if (separation > 0.0f)
            {
                grid.Visit(position, radiusOf[row] + maxRadius, ref push);
                desired += push.push * (separation * kind.speed / MathF.Max(radiusOf[row], MIN_SEPARATION_RADIUS));
            }
            Vector3 change = desired * speedFactor - velocities[row];
            float changeLength = change.magnitude;
            float maxChange = kind.acceleration * dt;
            velocities[row] += changeLength > maxChange ? change * (maxChange / changeLength) : change;
            knockbacks[row] *= MathF.Exp(-KNOCKBACK_DAMPING * dt);
            Vector3 next = position + (velocities[row] + knockbacks[row]) * dt + push.push * (OVERLAP_CORRECTION * MathF.Min(separation, 1.0f));
            next = ResolveObstacles(next, radiusOf[row]);
            next.y = position.y;
            nextPositions[row] = next;
            Vector3 velocity = velocities[row];
            if (turns && velocity.sqrMagnitude > MIN_TURNING_SPEED * MIN_TURNING_SPEED)
            {
                TurnToward(row, kind, MathF.Atan2(velocity.x, velocity.z), dt);
            }
        }

        private void TurnToward(int row, EnemyType kind, float targetYaw, float dt)
        {
            float maxTurn = kind.turnRate * DEGREES_TO_RADIANS * dt;
            float turn = MathF.IEEERemainder(targetYaw - yaws[row], FULL_TURN);
            yaws[row] += Math.Clamp(turn, -maxTurn, maxTurn);
        }

        private Vector3 ResolveObstacles(Vector3 position, float radius)
        {
            for (int i = 0; i < obstacles.Length; i++)
            {
                Vector4 obstacle = obstacles[i];
                float dx = position.x - obstacle.x;
                float dz = position.z - obstacle.z;
                float reach = obstacle.w + radius;
                float distanceSquared = dx * dx + dz * dz;
                if (distanceSquared >= reach * reach || distanceSquared < MIN_OBSTACLE_DISTANCE_SQUARED)
                {
                    continue;
                }
                float distance = MathF.Sqrt(distanceSquared);
                position.x = obstacle.x + dx / distance * reach;
                position.z = obstacle.z + dz / distance * reach;
            }
            float fromCenterX = position.x - arenaCenter.x;
            float fromCenterZ = position.z - arenaCenter.z;
            float fromCenter = MathF.Sqrt(fromCenterX * fromCenterX + fromCenterZ * fromCenterZ);
            float limit = arenaRadius - radius;
            if (fromCenter > limit && fromCenter > MIN_ARENA_DISTANCE)
            {
                position.x = arenaCenter.x + fromCenterX / fromCenter * limit;
                position.z = arenaCenter.z + fromCenterZ / fromCenter * limit;
            }
            return position;
        }

        private void AdvanceClip(int row, float dt, bool loops)
        {
            if (statuses[row].holdTime > 0.0f && states[row] != EnemyState.Dying)
            {
                return;
            }
            EnemyType kind = types[typeOf[row]];
            float length = kind.baked.GetLength(clips[row]);
            float time = clipTimes[row] + dt * clipRates[row];
            clipTimes[row] = loops && length > 0.0f ? time % length : MathF.Min(time, length);
        }

        private EnemyCommand MakeCommand(int row, int ability, EnemyCommand.Phase phase)
        {
            Vector3 position = currentPositions[row];
            return new EnemyCommand
            {
                phase = phase,
                enemy = MakeId(row),
                type = typeOf[row],
                ability = ability,
                position = position,
                target = castTarget[row],
                direction = GetFlatDirection(castTarget[row] - position, yaws[row]),
                damageScale = damageScale[row],
                isElite = elite[row],
                radius = radiusOf[row],
                scale = scaleOf[row],
                windup = types[typeOf[row]].abilities[ability].hitTime * statuses[row].windupScale,
            };
        }

        /// <summary>The unit ground direction of a vector, or the heading <paramref name="fallbackYaw"/> (radians) when it is zero.</summary>
        private static Vector3 GetFlatDirection(Vector3 vector, float fallbackYaw)
        {
            return Ground.Normalize(vector, BossGeometry.FromYawRadians(fallbackYaw));
        }

        /// <summary>Counts the collidable items a query visits.</summary>
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

        /// <summary>Sums how far the neighbours of one row overlap it, as a push away from them.</summary>
        private struct SeparationVisitor : ISpatialHashVisitor
        {
            public Vector3[] positions;
            public float[] radii;
            public bool[] collidable;
            public int row;
            public Vector3 position;
            public float radius;
            public Vector3 push;

            public void Visit(int other, float distance)
            {
                if (other == row || !collidable[other])
                {
                    return;
                }
                float reach = radius + radii[other];
                if (distance >= reach)
                {
                    return;
                }
                Vector3 away = distance > MIN_SEPARATION_DISTANCE
                                   ? new Vector3(position.x - positions[other].x, 0.0f, position.z - positions[other].z) / distance
                                   : GetTieDirection(row, other);
                push += away * (reach - distance);
            }

            /// <summary>A fixed direction for two rows at the same spot, so they separate the same way every run.</summary>
            private static Vector3 GetTieDirection(int row, int other)
            {
                float angle = (((uint)row * KNUTH_HASH) ^ ((uint)other * PAIR_HASH)) * (FULL_TURN / HASH_32_BIT_RANGE);
                return new Vector3(MathF.Cos(angle), 0.0f, MathF.Sin(angle));
            }
        }
    }
}

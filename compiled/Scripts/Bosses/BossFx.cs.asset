using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The bosses' particle effects and ground telegraphs. Emitters are found once by entity name under the run's
    /// effects root (the same emitters <see cref="Effects"/> fires) and fired with any mix of position, heading, tint,
    /// size and lifetime: oriented decals (cones, lanes, arcs, gapped rings) take their heading from a tiny launch
    /// velocity, which <see cref="Effects"/> cannot combine with a tint. Telegraphs use <see cref="dangerColor"/>,
    /// which the Hollow King turns violet when enraged. Main thread.
    /// </summary>
    public sealed class BossFx
    {
        /// <summary>Launch speed that only carries a heading (oriented decals do not drift).</summary>
        public const float ORIENT_SPEED = 0.001f;

        // Emitters the regular enemies' abilities use too (EnemyAbilityExecutor).
        public const string TELEGRAPH_FILL = "TelegraphFill";
        public const string TELEGRAPH_RING = "TelegraphRing";
        public const string TELEGRAPH_CONE = "TelegraphCone";
        public const string ENEMY_SWING = "EnemySwing";
        public const string GRAVE_BURST = "GraveBurst";
        public const string SUMMON_MOTES = "SummonMotes";
        public const string BONE_BURST = "BoneBurst";
        public const string ELITE_BEAM = "EliteBeam";
        // Boss-only emitters.
        public const string TELEGRAPH_LANE = "TelegraphLane";
        public const string TELEGRAPH_ARC = "TelegraphArc";
        public const string TELEGRAPH_ARC_EDGE = "TelegraphArcEdge";
        public const string SOUL_NOVA_WARN = "SoulNovaWarn";
        public const string SOUL_NOVA_RING = "SoulNovaRing";
        public const string BOSS_SLAM_DUST = "BossSlamDust";
        public const string BOSS_RISE_DUST = "BossRiseDust";
        public const string BOSS_CLEAVE_TRAIL = "BossCleaveTrail";
        public const string BONE_SPIKE = "BoneSpike";
        public const string BONE_RAIN_FALL = "BoneRainFall";
        public const string BONE_RAIN_IMPACT = "BoneRainImpact";
        public const string PHASE_SHOCKWAVE = "PhaseShockwave";
        public const string BOSS_SPIN_WIND = "BossSpinWind";
        public const string BOSS_DAZED = "BossDazed";
        public const string BOSS_CORE_GLOW = "BossCoreGlow";
        public const string KING_CRACK_PULSE = "KingCrackPulse";
        public const string BOSS_GUARD_GLOW = "BossGuardGlow";
        public const string GUARD_SPARK = "GuardSpark";
        public const string ARCHMAGE_SIGIL = "ArchmageSigil";
        public const string ARCHMAGE_GHOST = "ArchmageGhost";
        public const string BLINK_FLASH = "BlinkFlash";
        public const string BOSS_BONE_BURST = "BossBoneBurst";
        public const string BOSS_DEATH_BURST = "BossDeathBurst";
        public const string DEATH_WAVE = "DeathWave";
        public const string GOLD_SHOWER = "GoldShower";

        private readonly List<ParticleEmitterComponent> emitters = new List<ParticleEmitterComponent>();
        private readonly Dictionary<string, int> ids = new Dictionary<string, int>();
        private readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>Tint of every danger decal: red-orange, violet while the Hollow King is enraged.</summary>
        public Color dangerColor = Tuning.DANGER_COLOR;

        public readonly int telegraphFill;
        public readonly int telegraphRing;
        public readonly int telegraphCone;
        public readonly int telegraphLane;
        public readonly int telegraphArc;
        public readonly int telegraphArcEdge;
        public readonly int soulNovaWarn;
        public readonly int soulNovaRing;
        public readonly int enemySwing;
        public readonly int graveBurst;
        public readonly int summonMotes;
        public readonly int boneBurst;
        public readonly int eliteBeam;
        public readonly int slamDust;
        public readonly int riseDust;
        public readonly int cleaveTrail;
        public readonly int boneSpike;
        public readonly int boneRainFall;
        public readonly int boneRainImpact;
        public readonly int phaseShockwave;
        public readonly int spinWind;
        public readonly int dazed;
        public readonly int coreGlow;
        public readonly int crackPulse;
        public readonly int guardGlow;
        public readonly int guardSpark;
        public readonly int archmageSigil;
        public readonly int archmageGhost;
        public readonly int blinkFlash;
        public readonly int bossBoneBurst;
        public readonly int bossDeathBurst;
        public readonly int deathWave;
        public readonly int goldShower;

        /// <summary>Finds every emitter under <paramref name="root"/> and resolves the boss effects (missing ones warn once).</summary>
        public BossFx(Entity root)
        {
            if (root.IsValid())
            {
                foreach (ParticleEmitterComponent emitter in root.GetComponentsInChildren<ParticleEmitterComponent>())
                {
                    string name = emitter.owner.name;
                    if (!ids.ContainsKey(name))
                    {
                        ids[name] = emitters.Count;
                        emitters.Add(emitter);
                    }
                }
            }
            telegraphFill = Find(TELEGRAPH_FILL);
            telegraphRing = Find(TELEGRAPH_RING);
            telegraphCone = Find(TELEGRAPH_CONE);
            telegraphLane = Find(TELEGRAPH_LANE);
            telegraphArc = Find(TELEGRAPH_ARC);
            telegraphArcEdge = Find(TELEGRAPH_ARC_EDGE);
            soulNovaWarn = Find(SOUL_NOVA_WARN);
            soulNovaRing = Find(SOUL_NOVA_RING);
            enemySwing = Find(ENEMY_SWING);
            graveBurst = Find(GRAVE_BURST);
            summonMotes = Find(SUMMON_MOTES);
            boneBurst = Find(BONE_BURST);
            eliteBeam = Find(ELITE_BEAM);
            slamDust = Find(BOSS_SLAM_DUST);
            riseDust = Find(BOSS_RISE_DUST);
            cleaveTrail = Find(BOSS_CLEAVE_TRAIL);
            boneSpike = Find(BONE_SPIKE);
            boneRainFall = Find(BONE_RAIN_FALL);
            boneRainImpact = Find(BONE_RAIN_IMPACT);
            phaseShockwave = Find(PHASE_SHOCKWAVE);
            spinWind = Find(BOSS_SPIN_WIND);
            dazed = Find(BOSS_DAZED);
            coreGlow = Find(BOSS_CORE_GLOW);
            crackPulse = Find(KING_CRACK_PULSE);
            guardGlow = Find(BOSS_GUARD_GLOW);
            guardSpark = Find(GUARD_SPARK);
            archmageSigil = Find(ARCHMAGE_SIGIL);
            archmageGhost = Find(ARCHMAGE_GHOST);
            blinkFlash = Find(BLINK_FLASH);
            bossBoneBurst = Find(BOSS_BONE_BURST);
            bossDeathBurst = Find(BOSS_DEATH_BURST);
            deathWave = Find(DEATH_WAVE);
            goldShower = Find(GOLD_SHOWER);
        }

        /// <summary>The id of an emitter by entity name, or -1 (warned once) when the scene has none.</summary>
        public int Find(string name)
        {
            if (ids.TryGetValue(name, out int id))
            {
                return id;
            }
            if (missing.Add(name))
            {
                Log.Warning($"BossFx: no emitter named {name}.");
            }
            return -1;
        }

        /// <summary>A burst with the emitter's own look.</summary>
        public void Burst(int id, Vector3 position, int count)
        {
            if (id >= 0 && count > 0)
            {
                emitters[id].Emit(count, new EmitParams { position = position });
            }
        }

        /// <summary>A burst tinted and sized: size is the diameter (m), lifetime in seconds.</summary>
        public void Burst(int id, Vector3 position, int count, Color color, float size, float lifetime)
        {
            if (id >= 0 && count > 0)
            {
                emitters[id].Emit(count, new EmitParams { position = position, color = color, size = size, lifetime = lifetime });
            }
        }

        /// <summary>One oriented particle (a ground decal or a swing) facing <paramref name="direction"/>.</summary>
        public void Oriented(int id, Vector3 position, Vector3 direction, Color color, float size, float lifetime)
        {
            if (id < 0)
            {
                return;
            }
            Vector3 heading = BossGeometry.Flat(direction).normalized;
            emitters[id].Emit(1, new EmitParams { position = position, velocity = heading * ORIENT_SPEED, color = color, size = size, lifetime = lifetime });
        }

        /// <summary>Particles launched with a velocity (falling bones, debris).</summary>
        public void Moving(int id, Vector3 position, Vector3 velocity, int count, float size, float lifetime)
        {
            if (id >= 0 && count > 0)
            {
                emitters[id].Emit(count, new EmitParams { position = position, velocity = velocity, size = size, lifetime = lifetime });
            }
        }

        /// <summary>One tinted particle launched with a velocity (the segments of an expanding nova ring).</summary>
        public void Launch(int id, Vector3 position, Vector3 velocity, Color color, float size, float lifetime)
        {
            if (id >= 0)
            {
                emitters[id].Emit(1, new EmitParams { position = position, velocity = velocity, color = color, size = size, lifetime = lifetime });
            }
        }

        // -------------------------------------------------------------------------------------------------------------
        // Telegraphs: the inner fill grows over the given seconds and flashes white at its end (the hit moment).
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>A danger circle (outline + fill) of a radius that fills over <paramref name="seconds"/>.</summary>
        public void Circle(Vector3 center, float radius, float seconds)
        {
            Vector3 ground = OnGround(center);
            Burst(telegraphRing, ground, 1, dangerColor, radius * 2.0f, seconds);
            Burst(telegraphFill, ground, 1, dangerColor, radius * 2.0f, seconds);
        }

        /// <summary>Only the outline of a danger circle (spinning blades that follow their owner).</summary>
        public void RingOutline(Vector3 center, float radius, float seconds)
        {
            Burst(telegraphRing, OnGround(center), 1, dangerColor, radius * 2.0f, seconds);
        }

        /// <summary>
        /// A 120 degree cone of <paramref name="range"/> metres from an origin. The shared cone emitter carries the
        /// danger color in its own gradient (the Shieldbearer's bash), so it is fired untinted.
        /// </summary>
        public void Cone(Vector3 origin, Vector3 direction, float range, float seconds)
        {
            Oriented(telegraphCone, OnGround(origin), direction, Color.white, range * 2.0f, seconds);
        }

        /// <summary>A rectangle running <paramref name="length"/> metres from an origin; fills from its near end.</summary>
        public void Lane(Vector3 origin, Vector3 direction, float length, float seconds)
        {
            Vector3 heading = BossGeometry.Flat(direction).normalized;
            Oriented(telegraphLane, OnGround(origin + heading * (length * 0.5f)), heading, dangerColor, length, seconds);
        }

        /// <summary>
        /// A 200 degree arc of <paramref name="radius"/> metres in front of an origin: its outline shows the full reach
        /// from the start, its fill grows from the origin outward.
        /// </summary>
        public void Arc(Vector3 origin, Vector3 direction, float radius, float seconds)
        {
            Vector3 ground = OnGround(origin);
            Oriented(telegraphArcEdge, ground, direction, dangerColor, radius * 2.0f, seconds);
            Oriented(telegraphArc, ground, direction, dangerColor, radius * 2.0f, seconds);
        }

        /// <summary>A static ring with two opposite 60 degree gaps centered on <paramref name="gapDirection"/>.</summary>
        public void NovaWarning(Vector3 center, float radius, Vector3 gapDirection, float seconds)
        {
            Oriented(soulNovaWarn, OnGround(center), gapDirection, dangerColor, radius * 2.0f, seconds);
        }

        /// <summary>
        /// An expanding ring band with two opposite gaps, drawn as segments launched outward from the center: the
        /// band keeps its thickness and the gaps their place while it grows at <paramref name="speed"/> m/s.
        /// </summary>
        /// <param name="gapHalfAngle">Half a gap's opening (degrees); gaps center on the direction and its opposite.</param>
        /// <param name="segmentLength">Length of one segment along the ring (m); the emitter's U axis.</param>
        public void NovaWave(Vector3 center, Vector3 gapDirection, float gapHalfAngle, float speed, float maxRadius, float thickness, float segmentLength)
        {
            if (soulNovaRing < 0)
            {
                return;
            }
            float arc = 180.0f - 2.0f * gapHalfAngle;
            // Segments sized to just cover the arc at the largest radius; nearer the center they overlap.
            float segmentDegrees = segmentLength / MathF.Max(maxRadius, 0.1f) * Mathf.Rad2Deg;
            int count = Math.Max(1, (int)MathF.Ceiling(arc / MathF.Max(segmentDegrees, 0.1f)));
            float step = arc / count;
            float lifetime = maxRadius / MathF.Max(speed, 0.1f);
            Vector3 ground = OnGround(center);
            for (int side = 0; side < 2; side++)
            {
                float start = gapHalfAngle + side * 180.0f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 direction = BossGeometry.Rotate(gapDirection, start + (i + 0.5f) * step);
                    Launch(soulNovaRing, ground, direction * speed, dangerColor, thickness, lifetime);
                }
            }
        }

        private static Vector3 OnGround(Vector3 point) => new Vector3(point.x, Tuning.TELEGRAPH_HEIGHT, point.z);
    }
}

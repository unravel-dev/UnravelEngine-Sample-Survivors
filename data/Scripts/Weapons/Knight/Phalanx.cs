using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Phalanx (Knight, Uncommon): a wall of spectral shields Width across (Area) marches from the knight toward the nearest
    /// enemy, Range metres at Speed. It bulldozes the crowd: everything it meets takes Damage once and is carried along.
    /// Where the wall stops it crushes what it carries: SubDamage, plus Bonus for each enemy carried (up to BonusCap), and
    /// a stun (Stun). Bosses and other proxies cannot be carried.
    /// Evolution Immortal Legion (+ Iron Heart): four walls march out together, one to each side.
    /// Look: a row of glowing tower shields sliding over the ground in a wake of gold and dust; the crush is a flash, a shock
    /// ring, a cross flare and a row of dust bursts along the wall.
    /// </summary>
    public sealed class Phalanx : Weapon
    {
        private const int MAX_WALLS = 8;
        private const int SCRATCH = 512;
        /// <summary>A shield every this many metres of the wall.</summary>
        private const float SHIELD_SPACING = 1.5f;
        private const int MAX_SHIELDS = 16;
        private const float SHIELD_HEIGHT = 1.0f;
        /// <summary>The wall's depth on the ground, ahead of the shields, and how far ahead of the hero it starts.</summary>
        private const float WALL_DEPTH = 0.9f;
        private const float START_AHEAD = 1.2f;
        private const float WAKE_INTERVAL = 0.05f;
        private const float WAKE_HEIGHT = 0.15f;
        private const int WAKE_POINTS = 5;
        private const float CRUSH_HEIGHT = 0.9f;
        private const float CRUSH_DEPTH = 1.8f;
        private const float CRUSH_TRAUMA = 0.18f;
        private const int CRUSH_DUSTS = 5;
        private const float CRUSH_KNOCKBACK = 2.0f;

        private struct Wall
        {
            public Vector3 start;
            public Vector3 direction;
            public float travelled;
            public float length;
            public float halfWidth;
            public int carried;
        }

        private readonly Wall[] walls = new Wall[MAX_WALLS];
        private readonly HashSet<Enemy>[] memories = new HashSet<Enemy>[MAX_WALLS];
        private readonly int[] scratch = new int[SCRATCH];
        private readonly int shieldVisual;
        private readonly int wake;
        private readonly int dust;
        private readonly FxComposite crush;
        private int wallCount;
        private float wakeTimer;

        public Phalanx(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            for (int i = 0; i < MAX_WALLS; i++)
            {
                memories[i] = new HashSet<Enemy>();
            }
            shieldVisual = context.FindVisual("PhalanxShield");
            wake = effects.Find("KnightShieldWake");
            dust = effects.Find("KnightDust");
            crush = Composite(new FxLayer("KnightFlash", 1, 0.45f), new FxLayer("KnightCrossFlare", 1, 0.7f), FxLayer.Ground("KnightShockRing", 1.1f),
                              new FxLayer("KnightSteelSparks", 16), new FxLayer("KnightMotes", 10));
        }

        private float speed => level[WeaponField.Speed];

        protected override bool Cast(in CastInfo cast)
        {
            float range = level[WeaponField.Range];
            if (!TryNearest(range, out int nearest))
            {
                return false;
            }
            Vector3 direction = Ground.Direction(heroPosition, enemies.GetPosition(nearest), facing);
            int count = level.GetCount(WeaponField.SubCount);
            for (int i = 0; i < count; i++)
            {
                March(Ground.Rotate(direction, Ground.FULL_TURN * i / count), range);
            }
            RequestAttack("Block");
            PlayAt("aegis_summon", heroPosition);
            PlayAt("shield_rush", heroPosition);
            return true;
        }

        private void March(Vector3 direction, float length)
        {
            if (wallCount >= MAX_WALLS)
            {
                return;
            }
            float halfWidth = AreaOf(level[WeaponField.Width]) * 0.5f;
            Vector3 start = Ground.Flat(heroPosition) + direction * START_AHEAD;
            memories[wallCount].Clear();
            walls[wallCount++] = new Wall { start = start, direction = direction, length = length, halfWidth = halfWidth };
            SpawnShields(start, direction, halfWidth, length);
        }

        /// <summary>The shields of a wall: looks only, sliding side by side for the wall's march.</summary>
        private void SpawnShields(Vector3 start, Vector3 direction, float halfWidth, float length)
        {
            int shields = Math.Clamp((int)(halfWidth * 2.0f / SHIELD_SPACING), 1, MAX_SHIELDS);
            Vector3 across = Ground.Perpendicular(direction);
            for (int i = 0; i < shields; i++)
            {
                float offset = shields > 1 ? (i / (shields - 1.0f) - 0.5f) * halfWidth * 2.0f : 0.0f;
                SpawnVisual(shieldVisual, ProjectileMotion.Linear, start + across * offset + Ground.Up(SHIELD_HEIGHT), direction * speed, length / speed);
            }
        }

        protected override void Update(float dt)
        {
            wakeTimer -= dt;
            bool showsWake = wakeTimer <= 0.0f;
            wakeTimer = showsWake ? WAKE_INTERVAL : wakeTimer;
            int i = 0;
            while (i < wallCount)
            {
                if (Advance(ref walls[i], memories[i], dt, showsWake))
                {
                    i++;
                    continue;
                }
                Crush(walls[i]);
                wallCount--;
                walls[i] = walls[wallCount];
                (memories[i], memories[wallCount]) = (memories[wallCount], memories[i]);
            }
        }

        /// <summary>One step of the march: hits what it meets for the first time and carries everything it touches; false at its end.</summary>
        private bool Advance(ref Wall wall, HashSet<Enemy> memory, float dt, bool showsWake)
        {
            float step = speed * dt;
            wall.travelled += step;
            Vector3 center = wall.start + wall.direction * wall.travelled;
            Vector3 across = Ground.Perpendicular(wall.direction) * wall.halfWidth;
            strikes.Capsule(center - across, center + across, WALL_DEPTH, MightOf(level[WeaponField.Damage]), 0.0f, slot, default, PushMode.None, default, memory);
            int found = enemies.QueryCapsule(center - across, center + across, WALL_DEPTH, scratch);
            int carried = 0;
            for (int k = 0; k < found; k++)
            {
                int row = scratch[k];
                if (!enemies.IsProxy(row))
                {
                    enemies.SetPosition(row, enemies.GetPosition(row) + wall.direction * step);
                    carried++;
                }
            }
            wall.carried = Math.Max(wall.carried, carried);
            if (showsWake)
            {
                ShowWake(center, across);
            }
            return wall.travelled < wall.length;
        }

        private void ShowWake(Vector3 center, Vector3 across)
        {
            for (int k = 0; k < WAKE_POINTS; k++)
            {
                Vector3 point = center + across * Random.Range(-1.0f, 1.0f) + Ground.Up(WAKE_HEIGHT);
                Emit(wake, point, 1, KnightLooks.GOLD);
            }
            Emit(dust, center + across * Random.Range(-1.0f, 1.0f), 1, WHITE);
        }

        /// <summary>The wall stops and crushes what it carries: the fuller it is, the harder.</summary>
        private void Crush(in Wall wall)
        {
            Vector3 center = wall.start + wall.direction * wall.length;
            Vector3 across = Ground.Perpendicular(wall.direction) * wall.halfWidth;
            float bonus = MathF.Min(wall.carried * level[WeaponField.Bonus], level[WeaponField.BonusCap]);
            var effect = new HitEffect { stunSeconds = level[WeaponField.Stun] };
            strikes.Capsule(center - across, center + across, CRUSH_DEPTH, MightOf(level[WeaponField.SubDamage]) * (1.0f + bonus), KnockbackOf(CRUSH_KNOCKBACK), slot, effect);
            crush.Play(center + Ground.Up(CRUSH_HEIGHT), wall.halfWidth, fxScale, KnightLooks.GOLD);
            for (int k = 0; k < CRUSH_DUSTS; k++)
            {
                Emit(dust, center + across * ((k / (CRUSH_DUSTS - 1.0f) - 0.5f) * 2.0f), 2, WHITE);
            }
            PlayAt("shield_rush_boom", center);
            ctx.feel.Shake(CRUSH_TRAUMA);
        }
    }
}

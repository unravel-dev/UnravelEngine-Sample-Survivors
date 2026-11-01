using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Firebird (Mage, Uncommon): a bird of fire leaps from the mage toward the nearest enemy and sweeps Range metres over the
    /// field in a long wave (Speed, Projectile Speed), burning everything under its Width (Area) wings: Damage once, and a
    /// burn (Burn per BurnTick for SubDuration) that makes it ripe for thermal shock. Count + Amount birds fly apart at
    /// Spread. Echo casts glow violet.
    /// Evolution Phoenix Ascendant (+ Everburn Wick): two birds that wheel at the end of their flight and sweep back to the
    /// mage, burning everything a second time.
    /// Look: a bird drawn in fire every frame: a white-hot head in a flame with a halo, two swept wings of flame that beat
    /// slowly and a long tail; fire streams back from the wings and embers fall along its path over a line of scorch; it
    /// ends in a burst of flame.
    /// </summary>
    public sealed class Firebird : Weapon
    {
        private const int MAX_BIRDS = 8;
        private const float FLIGHT_HEIGHT = 1.6f;
        /// <summary>The wave: how far it sways per metre of flight, and how many full swings a flight makes.</summary>
        private const float SWAY_PER_METRE = 0.16f;
        private const float SWINGS = 1.5f;
        private const float CORE_SIZE = 0.75f;
        private const float FLAME_SIZE = 1.5f;
        private const float HALO_SIZE = 2.6f;
        /// <summary>A wing is this many flames from the shoulder to the tip, swept back this share of its span.</summary>
        private const int WING_POINTS = 6;
        private const float WING_SWEEP = 0.75f;
        private const float WING_ROOT_SIZE = 1.15f;
        private const float WING_TIP_SIZE = 0.4f;
        /// <summary>The wingbeat: how fast, and how high the tips swing per metre out from the body.</summary>
        private const float BEAT_RADIANS_PER_METRE = 0.55f;
        private const float BEAT_LIFT_PER_METRE = 0.32f;
        private const int TAIL_POINTS = 4;
        private const float TAIL_SPACING = 0.55f;
        private const float TAIL_SIZE = 0.75f;
        /// <summary>Fire streaming back and out from the wingtips, a pair every interval.</summary>
        private const float STREAM_INTERVAL = 0.03f;
        private const float STREAM_BACK = 0.55f;
        private const float STREAM_OUT = 0.4f;
        private const float STREAM_SECONDS = 0.32f;
        private const float STREAM_SIZE = 0.9f;
        private const float SCORCH_EVERY_METRES = 2.0f;
        private const float SCORCH_SIZE_SHARE = 1.2f;
        private const float SCORCH_SECONDS = 2.0f;
        private const int EMBERS = 2;
        private const float END_TRAUMA = 0.14f;
        private const float END_SIZE = 3.0f;

        private struct Bird
        {
            public Vector3 start;
            public Vector3 direction;
            public Vector3 previous;
            public float travelled;
            public float length;
            public float swaySign;
            public float streamTimer;
            public float scorchAt;
            public float scale;
            public bool isReturning;
            public Color color;
        }

        private readonly Bird[] birds = new Bird[MAX_BIRDS];
        private readonly HashSet<Enemy>[] memories = new HashSet<Enemy>[MAX_BIRDS];
        private readonly int coreEffect;
        private readonly int flame;
        private readonly int halo;
        private readonly int wing;
        private readonly int embers;
        private readonly int scorch;
        private readonly FxComposite end;
        private int birdCount;

        public Firebird(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            for (int i = 0; i < MAX_BIRDS; i++)
            {
                memories[i] = new HashSet<Enemy>();
            }
            coreEffect = effects.Find("HeadCore");
            flame = effects.Find("HeadFlame");
            halo = effects.Find("HeadHalo");
            wing = effects.Find("MageBirdWing");
            embers = effects.Find("FireEmbers");
            scorch = effects.Find("ScorchMark");
            end = Composite(new FxLayer("FireFlash", 1, 0.9f), new FxLayer("FireBall", 5, 0.45f), new FxLayer("FireSparks", 18), new FxLayer("FireEmbers", 14));
        }

        protected override bool Cast(in CastInfo cast)
        {
            float range = level[WeaponField.Range];
            if (!TryNearest(range, out int nearest))
            {
                return false;
            }
            Vector3 direction = Ground.Direction(heroPosition, enemies.GetPosition(nearest), facing);
            int count = level.GetCount(WeaponField.Count) + amount;
            float spread = level[WeaponField.Angle];
            for (int i = 0; i < count && birdCount < MAX_BIRDS; i++)
            {
                float turn = (i - (count - 1) * 0.5f) * spread;
                Launch(Ground.Rotate(direction, turn), i % 2 == 0 ? 1.0f : -1.0f, range, cast);
            }
            RequestAttack("CastShoot");
            PlayAt("ember_whoosh", heroPosition);
            PlayAt("starfall_whistle", heroPosition);
            return true;
        }

        private void Launch(Vector3 direction, float swaySign, float length, in CastInfo cast)
        {
            Vector3 start = Ground.Flat(heroPosition);
            memories[birdCount].Clear();
            birds[birdCount++] = new Bird
            {
                start = start,
                direction = direction,
                previous = start,
                length = length,
                swaySign = swaySign,
                scale = cast.damageScale,
                color = Tint(cast, MageLooks.FLAME),
            };
        }

        protected override void Update(float dt)
        {
            int i = 0;
            while (i < birdCount)
            {
                if (Fly(ref birds[i], memories[i], dt))
                {
                    i++;
                    continue;
                }
                End(birds[i]);
                birdCount--;
                birds[i] = birds[birdCount];
                (memories[i], memories[birdCount]) = (memories[birdCount], memories[i]);
            }
        }

        /// <summary>One frame of flight: the wave, the sweep of the wings, the look; false at its end.</summary>
        private bool Fly(ref Bird bird, HashSet<Enemy> memory, float dt)
        {
            bird.travelled += SpeedOf(level[WeaponField.Speed]) * dt;
            if (bird.travelled >= bird.length)
            {
                if (!isEvolved || bird.isReturning)
                {
                    return false;
                }
                // The phoenix wheels: a new sweep back along the wave, every enemy burned again.
                bird.isReturning = true;
                bird.travelled = 0.0f;
                memory.Clear();
                Vector3 far = bird.previous;
                bird.start = far;
                bird.direction = Ground.Direction(far, heroPosition, -bird.direction);
                bird.length = Ground.Distance(far, heroPosition);
                bird.scorchAt = 0.0f;
                PlayAt("ember_whoosh", far);
            }
            Vector3 position = GetPosition(bird);
            var effect = new HitEffect
            {
                burnPerSecond = BurnPerSecondOf(CastInfo.normal) * bird.scale,
                burnSeconds = level[WeaponField.SubDuration],
            };
            strikes.Capsule(bird.previous, position, AreaOf(level[WeaponField.Width]) * 0.5f, MightOf(level[WeaponField.Damage]) * bird.scale, 0.0f, slot, effect,
                            PushMode.None, default, memory);
            Show(ref bird, position, dt);
            bird.previous = position;
            return true;
        }

        private static Vector3 GetPosition(in Bird bird)
        {
            float share = bird.travelled / MathF.Max(bird.length, 0.1f);
            float sway = MathF.Sin(share * SWINGS * MathF.PI * 2.0f) * bird.length * SWAY_PER_METRE * bird.swaySign;
            return bird.start + bird.direction * bird.travelled + Ground.Perpendicular(bird.direction) * sway;
        }

        /// <summary>The body, the wings streaming back from it, and scorch and embers along the path.</summary>
        private void Show(ref Bird bird, Vector3 position, float dt)
        {
            Vector3 body = position + Ground.Up(FLIGHT_HEIGHT);
            Vector3 flight = Ground.Normalize(position - bird.previous, bird.direction);
            effects.EmitSized(coreEffect, body, 1, WHITE, CORE_SIZE * fxScale);
            effects.EmitSized(flame, body, 1, bird.color, FLAME_SIZE * fxScale);
            effects.EmitSized(halo, body, 1, bird.color, HALO_SIZE * fxScale);
            ShowWings(body, flight, ref bird, dt);
            if (bird.travelled >= bird.scorchAt)
            {
                bird.scorchAt = bird.travelled + SCORCH_EVERY_METRES;
                float wide = AreaOf(level[WeaponField.Width]);
                EmitShape(scorch, position + Ground.Up(FxSizes.GROUND_LIFT), WHITE, wide * SCORCH_SIZE_SHARE, SCORCH_SECONDS);
                Emit(embers, body, EMBERS, bird.color);
            }
        }

        /// <summary>The silhouette: two swept wings beating with its flight, a tail behind, and fire streaming off the tips.</summary>
        private void ShowWings(Vector3 body, Vector3 flight, ref Bird bird, float dt)
        {
            Vector3 side = Ground.Perpendicular(flight);
            float span = AreaOf(level[WeaponField.Width]) * 0.5f;
            float beat = MathF.Sin(bird.travelled * BEAT_RADIANS_PER_METRE * MathF.PI * 2.0f);
            bird.streamTimer -= dt;
            bool streams = bird.streamTimer <= 0.0f;
            bird.streamTimer = streams ? STREAM_INTERVAL : bird.streamTimer;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 tip = body;
                for (int k = 1; k <= WING_POINTS; k++)
                {
                    float reach = span * k / WING_POINTS;
                    tip = body + side * (sign * reach) - flight * (reach * WING_SWEEP) + Ground.Up(beat * reach * BEAT_LIFT_PER_METRE);
                    float size = Mathf.Lerp(WING_ROOT_SIZE, WING_TIP_SIZE, (k - 1.0f) / (WING_POINTS - 1.0f));
                    effects.EmitSized(flame, tip, 1, bird.color, size * fxScale);
                }
                if (streams)
                {
                    Vector3 stream = (side * (sign * STREAM_OUT) - flight * STREAM_BACK) * SpeedOf(level[WeaponField.Speed]);
                    effects.EmitMoving(wing, tip, stream, 1, bird.color, STREAM_SIZE * fxScale, STREAM_SECONDS);
                }
            }
            for (int k = 1; k <= TAIL_POINTS; k++)
            {
                effects.EmitSized(flame, body - flight * (k * TAIL_SPACING), 1, bird.color, TAIL_SIZE * fxScale * (1.0f - (float)k / (TAIL_POINTS + 1)));
            }
        }

        private void End(in Bird bird)
        {
            end.Play(bird.previous + Ground.Up(FLIGHT_HEIGHT), END_SIZE, fxScale, bird.color);
            PlayAt("ember_boom", bird.previous);
            ctx.feel.Shake(END_TRAUMA);
        }
    }
}

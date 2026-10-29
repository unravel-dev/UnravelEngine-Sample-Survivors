using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Rime Nova (Mage, Rare): a frost ring expands from the mage to Radius (Area) over Interval, hitting each enemy once
    /// as it passes for Damage; hits are chilled (-Slow speed for SubDuration) and may freeze (Chance, for Freeze; both
    /// after the Duration stat). Each point of Amount adds a nova Stagger later at AmountScale of the radius. Enemies
    /// that die frozen shatter into ice shards.
    /// Evolution Absolute Zero (+ Everburn Wick): wider, freezes everything (elites as long as the rest, bosses are
    /// slowed), and frozen enemies that die shatter for SubDamage in SubRadius, which can chain.
    /// Look: the frost ring races out over the ground with a flash, snow, mist and a frost decal, and ice crystals burst
    /// up along it where it reaches, so the frost visibly sweeps outward; Absolute Zero adds solar rays and twice the
    /// snow. A frozen enemy that dies shatters in a flash, ice shards and snow. Echo casts glow violet.
    /// </summary>
    public sealed class RimeNova : Weapon
    {
        private const int NOVA = 0;
        private const int BAND = 1;
        private const int SHATTER = 2;
        private const int BANDS = 4;
        private const int MEMORIES = 4;
        /// <summary>The frost ring is drawn this high and outlives the expansion by this much.</summary>
        private const float RING_LIFT = 0.1f;
        private const float RING_LINGER = 0.05f;
        private const float SHATTER_DELAY_MIN = 0.02f;
        private const float SHATTER_DELAY_MAX = 0.09f;
        private const float SHATTER_HEIGHT = 0.8f;
        private const float SHATTER_KNOCKBACK = 0.5f;
        private const float BURST_HEIGHT = 0.8f;
        private const int CRYSTALS_MIN = 8;
        private const float CRYSTALS_PER_METRE = 2.6f;
        private const float CRYSTAL_SIZE = 0.75f;
        /// <summary>Each crystal's size varies between these shares of the crystal size.</summary>
        private const float CRYSTAL_SIZE_MIN = 0.7f;
        private const float CRYSTAL_SIZE_MAX = 1.2f;

        private static readonly Color FROST = new Color(0.6f, 0.88f, 1.0f, 1.0f);
        private static readonly Color ICE = new Color(0.8f, 0.95f, 1.0f, 1.0f);

        private readonly HashSet<Enemy>[] memories = new HashSet<Enemy>[MEMORIES];
        private readonly int ring;
        private readonly int crystal;
        private readonly FxComposite novaBurst;
        private readonly FxComposite zeroBurst;
        private readonly FxComposite shatter;
        private int memoryCursor;

        public RimeNova(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            for (int i = 0; i < MEMORIES; i++)
            {
                memories[i] = new HashSet<Enemy>();
            }
            ring = effects.Find("FrostRing");
            crystal = effects.Find("FrostCrystal");
            novaBurst = Composite(new FxLayer("FrostFlash", 1, 0.9f), new FxLayer("FrostSnow", 18), new FxLayer("FrostMist", 10),
                                  FxLayer.Ground("FrostFrost", 2.2f));
            zeroBurst = Composite(new FxLayer("FrostFlash", 1, 1.2f), new FxLayer("SolarRays", 1, 0.9f), new FxLayer("FrostSnow", 36),
                                  new FxLayer("FrostMist", 18), FxLayer.Ground("FrostFrost", 2.4f));
            shatter = Composite(new FxLayer("FrostFlash", 1, 1.6f), new FxLayer("IceShatter", 8, 0.0f, false), new FxLayer("FrostSnow", 6));
        }

        protected override bool Cast(in CastInfo cast)
        {
            float radius = AreaOf(level[WeaponField.Radius]);
            if (!TryNearest(radius, out _))
            {
                return false;
            }
            int novas = level.GetCount(WeaponField.Count) + amount;
            for (int k = 0; k < novas; k++)
            {
                float share = k == 0 ? 1.0f : level[WeaponField.AmountScale];
                Schedule(level[WeaponField.Stagger] * k, new WeaponEvent { kind = NOVA, radius = radius * share, cast = cast });
            }
            RequestAttack("CastLong");
            return true;
        }

        public override void OnEnemyKilled(in EnemyDeath death)
        {
            if (death.wasFrozen)
            {
                Schedule(Random.Range(SHATTER_DELAY_MIN, SHATTER_DELAY_MAX), new WeaponEvent { kind = SHATTER, position = death.position });
            }
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            switch (e.kind)
            {
                case NOVA:
                    Burst(e);
                    break;
                case BAND:
                    Sweep(e);
                    break;
                case SHATTER:
                    Shatter(e.position);
                    break;
            }
        }

        private void Burst(in WeaponEvent e)
        {
            Vector3 center = heroPosition;
            int memory = memoryCursor;
            memoryCursor = (memoryCursor + 1) % MEMORIES;
            memories[memory].Clear();
            float expansion = level[WeaponField.Interval];
            Color color = Tint(e.cast, FROST);
            EmitShape(ring, center + Ground.Up(RING_LIFT), color, e.radius * 2.0f, expansion + RING_LINGER);
            (isEvolved ? zeroBurst : novaBurst).Play(center + Ground.Up(BURST_HEIGHT), e.radius, fxScale, color);
            PlayAt("rime_nova", center);
            for (int b = 1; b <= BANDS; b++)
            {
                Schedule(expansion * b / BANDS, new WeaponEvent { kind = BAND, position = center, radius = e.radius * b / BANDS, index = memory, cast = e.cast });
            }
        }

        /// <summary>The ring reaches a new radius: everything inside it not hit yet by this nova is hit now.</summary>
        private void Sweep(in WeaponEvent e)
        {
            var effect = new HitEffect
            {
                slowFactor = 1.0f - level[WeaponField.Slow],
                slowSeconds = DurationOf(level[WeaponField.SubDuration]),
                freezeChance = level[WeaponField.Chance],
                freezeSeconds = DurationOf(level[WeaponField.Freeze]),
                freezesElitesFully = isEvolved,
                numberKind = NumberKind(e.cast, DamageKind.Frost),
            };
            strikes.Circle(e.position, e.radius, DamageOf(WeaponField.Damage, e.cast), KnockbackOf(level[WeaponField.Knockback]), slot, effect,
                           PushMode.Outward, memories[e.index]);
            RaiseCrystals(e.position, e.radius, Tint(e.cast, ICE));
        }

        /// <summary>Ice bursts up along the ring where it reaches now, so the frost visibly sweeps outward.</summary>
        private void RaiseCrystals(Vector3 center, float radius, Color color)
        {
            int count = Math.Max(CRYSTALS_MIN, (int)(radius * CRYSTALS_PER_METRE * fxScale));
            float start = Ground.RandomYaw();
            float size = CRYSTAL_SIZE * fxScale;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Ground.RingDirection(start, i, count);
                EmitSized(crystal, Ground.Flat(center) + direction * radius, color, size * Random.Range(CRYSTAL_SIZE_MIN, CRYSTAL_SIZE_MAX));
            }
        }

        private void Shatter(Vector3 position)
        {
            shatter.Play(position + Ground.Up(SHATTER_HEIGHT), FxSizes.METRE_UNIT, fxScale, ICE);
            PlayAt("frost_shatter", position);
            if (isEvolved)
            {
                var effect = new HitEffect { numberKind = DamageKind.Frost };
                strikes.Circle(position, AreaOf(level[WeaponField.SubRadius]), MightOf(level[WeaponField.SubDamage]), KnockbackOf(SHATTER_KNOCKBACK), slot, effect);
            }
        }
    }
}

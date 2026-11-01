using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Powder Trail (Rogue, Rare): the rogue spills black powder as she runs, a pile every SubRange metres (the last Count +
    /// Amount piles, one more per Momentum). Every Cooldown she drops a spark on it: fire races along the trail from its far
    /// end toward her, every pile blowing up in turn for Damage in Radius (Area), each enemy hit once per ignition. The
    /// horde that chases her walks right along it. Nothing lit if she stood still.
    /// Evolution Inferno Road (+ Everburn Wick): a far longer trail, wider blasts, and the road burns on behind (SubDamage
    /// every Interval for SubDuration).
    /// Look: a dark smear on the ground with sparks; the fuse is a racing spark, each blast a flash, a fireball, sparks and
    /// smoke over a scorch. Inferno Road's piles burst into standing flames.
    /// </summary>
    public sealed class PowderTrail : Weapon
    {
        private const int BLAST = 0;
        private const int MAX_PILES = 48;
        private const int MIN_PILES_TO_LIGHT = 3;
        /// <summary>Movement in one frame beyond this is a teleport, not running (Smoke Roll's dash is fine).</summary>
        private const float MAX_STEP = 2.5f;
        private const int MEMORIES = 3;
        private const float PILE_SECONDS = 6.0f;
        private const float PILE_SIZE = 1.5f;
        private const float BLAST_HEIGHT = 0.5f;
        private const float SCORCH_SECONDS = 1.8f;
        private const float SCORCH_SHARE = 1.8f;
        private const float BLAST_TRAUMA = 0.1f;
        /// <summary>Only every n-th blast sounds and shakes: a long trail must not roar.</summary>
        private const int SOUND_EVERY = 3;
        /// <summary>Inferno Road burns in stretches this many piles long.</summary>
        private const int ROAD_SEGMENT_PILES = 3;
        private const float ROAD_FLAME_INTERVAL = 0.1f;
        private const int ROAD_FLAMES = 2;

        private readonly Vector3[] piles = new Vector3[MAX_PILES];
        private readonly HashSet<Enemy>[] memories = new HashSet<Enemy>[MEMORIES];
        private readonly RogueKit rogue;
        private readonly int pileEffect;
        private readonly int fuse;
        private readonly int scorch;
        private readonly FxComposite blast;
        private readonly FxComposite roadBlast;
        private readonly ZoneLook roadLook;
        private int pileCount;
        private int pileHead;
        private int memoryIndex;
        private float travelled;
        private Vector3 lastPosition;

        public PowderTrail(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            for (int i = 0; i < MEMORIES; i++)
            {
                memories[i] = new HashSet<Enemy>();
            }
            rogue = context.KitAs<RogueKit>();
            pileEffect = effects.Find("RoguePowderPile");
            fuse = effects.Find("RogueFuseSpark");
            scorch = effects.Find("ScorchMark");
            blast = Composite(new FxLayer("FireFlash", 1, 0.8f), new FxLayer("FireBall", 3, 0.5f), new FxLayer("FireSparks", 6), new FxLayer("FireSmoke", 1, 0.5f, false));
            roadBlast = Composite(new FxLayer("FireFlash", 1, 1.0f), new FxLayer("FireBall", 5, 0.55f), new FxLayer("FireSparks", 10), new FxLayer("FireSmoke", 2, 0.5f, false));
            roadLook = new ZoneLook(new ZoneStream(effects.Find("SharedFlaskFire"), ROAD_FLAME_INTERVAL, ROAD_FLAMES, SharedLooks.FIRE));
            lastPosition = heroPosition;
        }

        /// <summary>The piles a trail holds now: the rank's, Amount, and one per Momentum.</summary>
        private int trailLength => Math.Min(level.GetCount(WeaponField.Count) + amount + ctx.kit.signatureStacks, MAX_PILES);

        protected override void Update(float dt)
        {
            Vector3 position = heroPosition;
            float step = Ground.Distance(lastPosition, position);
            lastPosition = position;
            if (step > MAX_STEP || !hero.isAlive)
            {
                return;
            }
            travelled += step;
            if (travelled >= level[WeaponField.SubRange])
            {
                travelled = 0.0f;
                AddPile(Ground.Flat(position));
            }
        }

        private void AddPile(Vector3 at)
        {
            piles[pileHead] = at;
            pileHead = (pileHead + 1) % MAX_PILES;
            pileCount = Math.Min(pileCount + 1, MAX_PILES);
            EmitShape(pileEffect, at + Ground.Up(FxSizes.GROUND_LIFT), WHITE, PILE_SIZE, PILE_SECONDS);
        }

        /// <summary>The pile n steps back from the newest.</summary>
        private Vector3 GetPile(int stepsBack) => piles[(pileHead - 1 - stepsBack + MAX_PILES * 2) % MAX_PILES];

        protected override bool Cast(in CastInfo cast)
        {
            int count = Math.Min(pileCount, trailLength);
            if (count < MIN_PILES_TO_LIGHT)
            {
                return false;
            }
            memoryIndex = (memoryIndex + 1) % MEMORIES;
            memories[memoryIndex].Clear();
            float interval = level[WeaponField.SubInterval];
            for (int i = 0; i < count; i++)
            {
                Vector3 at = GetPile(count - 1 - i);
                Schedule(interval * i, new WeaponEvent { kind = BLAST, position = at, index = memoryIndex, count = i });
                if (isEvolved && i % ROAD_SEGMENT_PILES == 0 && i + 1 < count)
                {
                    IgniteRoad(at, GetPile(Math.Max(count - 1 - i - ROAD_SEGMENT_PILES, 0)));
                }
            }
            pileCount = 0;
            PlayAt("cinder_fuse", heroPosition);
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == BLAST)
            {
                Blast(e);
            }
        }

        private void Blast(in WeaponEvent e)
        {
            float radius = AreaOf(level[WeaponField.Radius]);
            HitEffect effect = WithAutoCrit(default);
            strikes.Circle(e.position, radius, MightOf(level[WeaponField.Damage]), KnockbackOf(level[WeaponField.Knockback]), slot, effect, PushMode.Outward, memories[e.index]);
            Vector3 height = e.position + Ground.Up(BLAST_HEIGHT);
            (isEvolved ? roadBlast : blast).Play(height, radius, fxScale, SharedLooks.FIRE);
            Emit(fuse, height, 4, WHITE);
            EmitShape(scorch, e.position + Ground.Up(FxSizes.LOW_GROUND_LIFT), WHITE, radius * SCORCH_SHARE, SCORCH_SECONDS);
            if (e.count % SOUND_EVERY == 0)
            {
                PlayAt("ember_boom", e.position);
                ctx.feel.Shake(BLAST_TRAUMA);
            }
        }

        /// <summary>Inferno Road: a stretch of the trail burns on behind the blasts.</summary>
        private void IgniteRoad(Vector3 a, Vector3 b)
        {
            ctx.zones.Add(new ZoneSpec
            {
                shape = ZoneShape.Capsule,
                a = a,
                b = b,
                radius = AreaOf(level[WeaponField.SubRadius]),
                damage = MightOf(level[WeaponField.SubDamage]),
                interval = level[WeaponField.Interval],
                duration = DurationOf(level[WeaponField.SubDuration]),
                source = slot,
                effect = new HitEffect { numberKind = DamageKind.Burn, hideNumber = true },
                look = roadLook,
            });
        }
    }
}

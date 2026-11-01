using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Thousand Cuts (Rogue, Rare): a shadow blade races from enemy to enemy, nearest to nearest (Count + Amount cuts, Interval
    /// apart, each hop at most SubRange), starting at the enemy nearest the rogue within Range. Every cut deals Damage and
    /// leaves a glowing X on its enemy. MajorDelay seconds after the last cut, all the marks detonate at once: MajorDamage
    /// to the marked (it spends Smoke Roll's loaded crits) and SubDamage in SubRadius (Area) around each.
    /// Evolution Death Blossom (+ Keen Eye): many more cuts, and each marked enemy that dies in the blast blooms once more
    /// around where it fell.
    /// Look: a bright violet cut from each enemy to the next, a glint on each hit and an X that hangs over the enemy,
    /// brightening until the blast; every mark bursts in a flash, shadow smoke and glints, all in the same instant.
    /// </summary>
    public sealed class ThousandCuts : Weapon
    {
        private const int CUT = 0;
        private const int DETONATE = 1;
        private const int BLOOM = 2;
        private const int MAX_MARKS = 32;
        /// <summary>The blade races and the marks hang above the heads, where the crowd's bodies cannot hide them.</summary>
        private const float BLADE_HEIGHT = 2.1f;
        private const float MARK_ABOVE_HEAD = 0.5f;
        private const float BURST_HEIGHT = 1.0f;
        private const float SLASH_SECONDS = 0.2f;
        private const float MARK_SIZE = 1.5f;
        private const float CUT_GLINT_SIZE = 0.6f;
        private const float CUT_VOLUME = 0.4f;
        private const float BLAST_TRAUMA = 0.2f;
        /// <summary>A blossom's blast: this much of the mark's damage, this much wider.</summary>
        private const float BLOOM_DAMAGE_SHARE = 0.3f;
        private const float BLOOM_RADIUS_SHARE = 1.6f;
        private const float BLOOM_DELAY = 0.12f;

        private readonly Enemy[] marks = new Enemy[MAX_MARKS];
        private readonly int slash;
        private readonly int markEffect;
        private readonly int glint;
        private readonly FxComposite markBurst;
        private readonly FxComposite bloomBurst;
        private int markCount;
        private int chainLength;
        private Vector3 lastCut;
        private bool isDetonating;

        public ThousandCuts(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            slash = effects.Find("RogueDancerSlash");
            markEffect = effects.Find("RogueCutMark");
            glint = effects.Find("RogueGlint");
            // Size unit = 1 m.
            markBurst = Composite(new FxLayer("RogueFlash", 1, 1.3f), new FxLayer("RogueShadowPuff", 3, 0.8f, false), new FxLayer("RogueStormGlints", 6),
                                  new FxLayer("RogueSparks", 6));
            bloomBurst = Composite(new FxLayer("RogueFlash", 1, 1.8f), FxLayer.Ground("RogueShadowShock", 3.0f), new FxLayer("RogueStormGlints", 10));
        }

        private float delay => level[WeaponField.MajorDelay];

        protected override bool Cast(in CastInfo cast)
        {
            if (markCount > 0 || !TryNearest(level[WeaponField.Range], out int first))
            {
                return false;
            }
            chainLength = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_MARKS);
            lastCut = heroPosition;
            Cut(first);
            Schedule(level[WeaponField.Interval], new WeaponEvent { kind = CUT });
            RequestAttack("Throw");
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            switch (e.kind)
            {
                case CUT:
                    NextCut();
                    break;
                case DETONATE:
                    Detonate();
                    break;
                case BLOOM:
                    Bloom(e.position);
                    break;
            }
        }

        /// <summary>The blade leaps to the next unmarked enemy; when the chain is done or runs dry, the blast is scheduled.</summary>
        private void NextCut()
        {
            ReadOnlySpan<Enemy> done = marks.AsSpan(0, markCount);
            if (markCount >= chainLength || !strikes.FindNearestExcept(lastCut, level[WeaponField.SubRange], done, out int next))
            {
                Schedule(delay, new WeaponEvent { kind = DETONATE });
                return;
            }
            Cut(next);
            Schedule(level[WeaponField.Interval], new WeaponEvent { kind = CUT });
        }

        private void Cut(int row)
        {
            Vector3 at = enemies.GetPosition(row);
            marks[markCount++] = enemies.GetEnemy(row);
            strikes.Hit(row, MightOf(level[WeaponField.Damage]), Vector3.zero, slot);
            Color color = isEvolved ? RogueLooks.LEGION_RIM : RogueLooks.PALE_VIOLET;
            Vector3 above = at + Ground.Up(enemies.GetHeight(row) + MARK_ABOVE_HEAD);
            Vector3 flight = Ground.Direction(lastCut, at, facing);
            effects.EmitMoving(slash, Ground.Flat(lastCut) + Ground.Up(BLADE_HEIGHT), flight * FxSizes.HEADING_SPEED, 1, color, Ground.Distance(lastCut, at) * 2.0f, SLASH_SECONDS);
            EmitShape(markEffect, above, color, MARK_SIZE * fxScale, delay + level[WeaponField.Interval] * (chainLength - markCount + 1));
            EmitSized(glint, above, RogueLooks.STEEL_WHITE, CUT_GLINT_SIZE * fxScale);
            PlayAt("knife_throw", at, CUT_VOLUME);
            lastCut = at;
        }

        /// <summary>Every mark bursts at once.</summary>
        private void Detonate()
        {
            float damage = MightOf(level[WeaponField.MajorDamage]);
            float splash = MightOf(level[WeaponField.SubDamage]);
            float radius = AreaOf(level[WeaponField.SubRadius]);
            Vector3 sound = heroPosition;
            isDetonating = true;
            for (int i = 0; i < markCount; i++)
            {
                int row = enemies.GetRow(marks[i]);
                if (row < 0)
                {
                    continue;
                }
                Vector3 at = enemies.GetPosition(row);
                sound = at;
                strikes.Hit(row, damage, Vector3.zero, slot, WithAutoCrit(default));
                strikes.Circle(at, radius, splash, 0.0f, slot, default, PushMode.None);
                markBurst.Play(at + Ground.Up(BURST_HEIGHT), FxSizes.METRE_UNIT, fxScale, RogueLooks.SHADOW_VIOLET);
            }
            isDetonating = false;
            markCount = 0;
            PlayAt("execute", sound);
            ctx.feel.Shake(BLAST_TRAUMA);
        }

        /// <summary>Death Blossom: a mark that dies in the blast blooms once more where it fell (scheduled: this runs inside the damage call).</summary>
        public override void OnEnemyKilled(in EnemyDeath death)
        {
            if (isEvolved && isDetonating && death.source == slot)
            {
                Schedule(BLOOM_DELAY, new WeaponEvent { kind = BLOOM, position = death.position });
            }
        }

        private void Bloom(Vector3 at)
        {
            float radius = AreaOf(level[WeaponField.SubRadius]) * BLOOM_RADIUS_SHARE;
            strikes.Circle(at, radius, MightOf(level[WeaponField.MajorDamage]) * BLOOM_DAMAGE_SHARE, 0.0f, slot, default, PushMode.None);
            bloomBurst.Play(at + Ground.Up(BURST_HEIGHT), FxSizes.METRE_UNIT, fxScale, RogueLooks.LEGION_RIM);
        }
    }
}

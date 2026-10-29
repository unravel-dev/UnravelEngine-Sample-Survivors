using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Assassin's Mark (Rogue, Rare): marks the strongest enemies within Range (Count + Amount: bosses, then elites, then
    /// the most health left); a marked enemy takes +Bonus damage from everything. Every cooldown a shadow clone appears
    /// behind each mark and stabs it Stagger later for Damage (a 30 ms freeze on that target only). A marked enemy that
    /// dies bursts into SubCount seeking shadow daggers (SubDamage each, SubSpeed, Turn).
    /// Evolution Death Sentence (+ Lucky Knucklebone): more marks, strikes always crit (CritChance 100%), and non-boss
    /// marks below Execute of their health are executed.
    /// Look: a crimson reticle slams onto each new mark and stays over its head (one particle refreshed every 0.25 s and
    /// drifting with the mark, so it is steady and never stacks); the clone is a tall dark shade with a bright violet rim
    /// and crimson eyes, standing behind and to one side of the mark so the mark does not hide it, and its stab is a
    /// thrust streak from the shade, a crescent slash and crimson sparks while the shade bursts into shadow; a dying mark
    /// breaks into crimson shards and a violet shadow burst. Death Sentence rings every reticle with a thin crimson-magenta
    /// rune circle, drops a crimson shaft on each new mark, stabs with a crimson X over a dark slash, and executes with a
    /// rune circle, a falling crimson stroke, the dark slash and a dark splat on the ground.
    /// </summary>
    public sealed class AssassinsMark : Weapon
    {
        private const int CLONE = 0;
        private const int STAB = 1;
        private const int DAGGERS = 2;
        private const int MAX_MARKS = 8;
        private const float KEEP_RANGE_SHARE = 1.5f;
        private const float MARK_EXTRA_SECONDS = 1.0f;
        private const float SIGIL_ABOVE_HEAD = 0.9f;
        private const float CLONE_DISTANCE = 0.9f;
        private const float CLONE_GAP = 0.05f;
        private const float STAB_FREEZE_SECONDS = 0.03f;
        private const float DAGGER_RADIUS = 0.3f;
        private const float DAGGER_LIFETIME = 1.4f;
        private const float DAGGER_HEIGHT = 1.0f;
        private const float DAGGER_OUT = 0.3f;
        private const float EXECUTE_HIT_STOP = 0.08f;
        private const float BODY_HEIGHT = 1.0f;
        private const float STAMP_SIZE = 2.4f;
        /// <summary>Each mark's reticle (and Death Sentence's rune) is one particle refreshed this often, living a little
        /// longer so the new one fades in as the old one fades out.</summary>
        private const float SIGIL_REFRESH = 0.25f;
        private const float SIGIL_OVERLAP = 1.2f;
        private const float RETICLE_SIZE = 1.1f;
        private const float SENTENCE_RETICLE_SIZE = 1.2f;
        /// <summary>Death Sentence's rune circle, just around the reticle's spikes (m).</summary>
        private const float SENTENCE_SIGIL_SIZE = 1.3f;
        private const float SENTENCE_BEAM_SIZE = 2.2f;
        /// <summary>The clone shade's height (m); it lingers this long after the stab while it dissolves.</summary>
        private const float CLONE_HEIGHT = 3.0f;
        private const float SENTENCE_CLONE_SCALE = 1.15f;
        private const float CLONE_LINGER = 0.35f;
        /// <summary>The shade stands behind the mark turned this far to one side, so the mark's body does not hide it.</summary>
        private const float CLONE_SIDE_ANGLE = 50.0f;
        /// <summary>The shade's eyes: apart along the camera's right (+X), at this share of its height.</summary>
        private const float EYE_GAP = 0.11f;
        private const float EYE_HEIGHT_SHARE = 0.62f;
        private const float EYE_SIZE = 0.18f;
        private const float THRUST_SIZE = 2.6f;
        private const float THRUST_SECONDS = 0.14f;
        private const float THRUST_DRIFT = 0.5f;
        private const float STAB_SLASH_SIZE = 2.0f;
        private const float SENTENCE_X_SIZE = 2.0f;
        private const float STAB_DARK_SLASH_SIZE = 2.2f;
        private const float STAB_CRIMSON_SLASH_SIZE = 2.0f;
        private const float EXECUTE_SIGIL_SIZE = 3.0f;
        private const float EXECUTE_DARK_SLASH_SIZE = 3.0f;
        private const float EXECUTE_CRIMSON_SLASH_SIZE = 2.8f;
        /// <summary>The execution stroke falls from this high above the body at this speed (m/s).</summary>
        private const float STROKE_HEIGHT = 4.0f;
        private const float STROKE_SPEED = 30.0f;
        private const float STROKE_WIDTH = 0.28f;
        private const float SPLAT_SIZE = 2.6f;
        private const float SPLAT_SECONDS = 1.6f;
        private const float DAGGER_CUT_SIZE = 0.7f;

        private static readonly Vector3 BODY = Ground.Up(BODY_HEIGHT);
        private static readonly Vector3 LIFT = Ground.Up(FxSizes.GROUND_LIFT);

        private readonly Enemy[] marks = new Enemy[MAX_MARKS];
        private readonly int[] candidates = new int[MAX_MARKS];
        private readonly Enemy[] executions = new Enemy[MAX_MARKS];
        /// <summary>Where each mark's reticle was at the last refresh: its drift until the next one.</summary>
        private readonly Vector3[] lastSigils = new Vector3[MAX_MARKS];
        private readonly int daggerVisual;
        private readonly int markStamp;
        private readonly int markReticle;
        private readonly int sentenceSigil;
        private readonly int sentenceBeam;
        private readonly int cloneShade;
        private readonly int cloneRim;
        private readonly int eyes;
        private readonly int stabThrust;
        private readonly int stabSlash;
        private readonly int sentenceX;
        private readonly int darkSlash;
        private readonly int crimsonSlash;
        private readonly int executeSigil;
        private readonly int executeStroke;
        private readonly int executeSplat;
        private readonly int daggerCut;
        private readonly FxComposite markApply;
        private readonly FxComposite cloneSmoke;
        private readonly FxComposite cloneVanish;
        private readonly FxComposite stab;
        private readonly FxComposite sentenceStab;
        private readonly FxComposite execution;
        private readonly FxComposite deathShadow;
        private readonly FxComposite deathShards;
        private readonly FxComposite daggerHit;
        private int markCount;
        private float sigilTimer;

        public AssassinsMark(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            daggerVisual = context.FindVisual("ShadowDagger");
            markStamp = effects.Find("RogueMarkStamp");
            markReticle = effects.Find("RogueMarkReticle");
            sentenceSigil = effects.Find("RogueSentenceSigil");
            sentenceBeam = effects.Find("RogueSentenceBeam");
            cloneShade = effects.Find("RogueCloneShade");
            cloneRim = effects.Find("RogueCloneRim");
            eyes = effects.Find("RogueEyes");
            stabThrust = effects.Find("RogueStabThrust");
            stabSlash = effects.Find("RogueStabSlash");
            sentenceX = effects.Find("RogueSentenceX");
            darkSlash = effects.Find("RogueDarkSlash");
            crimsonSlash = effects.Find("RogueCrimsonSlash");
            executeSigil = effects.Find("RogueExecSigil");
            executeStroke = effects.Find("RogueExecStroke");
            executeSplat = effects.Find("RogueExecSplat");
            daggerCut = effects.Find("RogueKnifeCut");
            markApply = Composite(new FxLayer("RogueFlash", 1, 0.8f), new FxLayer("RogueMarkMotes", 10));
            cloneSmoke = Composite(new FxLayer("RogueShadowPuff", 4, 0.7f, false));
            cloneVanish = Composite(new FxLayer("RogueFlash", 1, 0.8f), new FxLayer("RogueShadowPuff", 4, 0.8f, false));
            stab = Composite(new FxLayer("RogueFlash", 1, 1.1f), new FxLayer("RogueSparks", 10), new FxLayer("RogueShadowPuff", 2, 0.5f, false));
            sentenceStab = Composite(new FxLayer("RogueFlash", 1, 0.9f), new FxLayer("RogueSparks", 10), new FxLayer("RogueShadowPuff", 2, 0.6f, false));
            execution = Composite(new FxLayer("RogueFlash", 1, 1.6f), new FxLayer("RogueSparks", 12), new FxLayer("RogueShadowPuff", 3, 0.8f, false),
                                  FxLayer.Ground("RogueShadowShock", 4.0f));
            deathShadow = Composite(new FxLayer("RogueFlash", 1, 1.2f), new FxLayer("RogueShadowPuff", 5, 0.8f, false), FxLayer.Ground("RogueShadowShock", 3.5f));
            deathShards = Composite(new FxLayer("RogueShardBurst", 8), new FxLayer("RogueMarkMotes", 6));
            daggerHit = Composite(new FxLayer("RogueSparks", 2));
        }

        protected override bool Cast(in CastInfo cast)
        {
            RefreshMarks();
            if (markCount == 0)
            {
                return false;
            }
            for (int i = 0; i < markCount; i++)
            {
                Schedule(CLONE_GAP * i, new WeaponEvent { kind = CLONE, target = marks[i], cast = cast });
            }
            return true;
        }

        public override void OnEnemyKilled(in EnemyDeath death)
        {
            for (int i = 0; i < markCount; i++)
            {
                if (marks[i].Equals(death.enemy))
                {
                    // The mark leaves the list in the next update: this runs inside another system's damage call.
                    marks[i] = Enemy.None;
                    Schedule(0.0f, new WeaponEvent { kind = DAGGERS, position = death.position });
                    return;
                }
            }
        }

        protected override void Update(float dt)
        {
            sigilTimer -= dt;
            bool isRefresh = sigilTimer <= 0.0f;
            if (isRefresh)
            {
                sigilTimer = MathF.Max(sigilTimer + SIGIL_REFRESH, 0.0f);
            }
            int executionCount = 0;
            for (int i = markCount - 1; i >= 0; i--)
            {
                int row = enemies.GetRow(marks[i]);
                if (row < 0)
                {
                    RemoveMark(i);
                    continue;
                }
                if (isRefresh)
                {
                    EmitSigil(i, GetSigilPosition(row));
                }
                if (isEvolved && !strikes.IsBoss(row) && enemies.GetHealthFraction(row) < level[WeaponField.Execute])
                {
                    executions[executionCount++] = marks[i];
                }
            }
            for (int i = 0; i < executionCount; i++)
            {
                Execute(executions[i]);
            }
        }

        /// <summary>Drops marks that died or fell far behind, then marks the strongest enemies up to the mark count.</summary>
        private void RefreshMarks()
        {
            float range = level[WeaponField.Range];
            float keep = range * KEEP_RANGE_SHARE;
            for (int i = markCount - 1; i >= 0; i--)
            {
                int row = enemies.GetRow(marks[i]);
                if (row < 0 || Ground.Distance(enemies.GetPosition(row), heroPosition) > keep)
                {
                    RemoveMark(i);
                }
            }
            int wanted = Math.Min(level.GetCount(WeaponField.Count) + amount, MAX_MARKS);
            while (markCount > wanted)
            {
                RemoveMark(markCount - 1);
            }
            if (markCount < wanted)
            {
                int found = strikes.FindStrongest(heroPosition, range, marks.AsSpan(0, markCount), candidates, wanted - markCount);
                for (int i = 0; i < found; i++)
                {
                    AddMark(candidates[i]);
                }
            }
            float seconds = cooldown + MARK_EXTRA_SECONDS;
            for (int i = 0; i < markCount; i++)
            {
                enemies.Mark(enemies.GetRow(marks[i]), level[WeaponField.Bonus], seconds);
            }
        }

        /// <summary>Marks an enemy: the reticle slams onto it with a flash and rising motes (Death Sentence: and a crimson shaft).</summary>
        private void AddMark(int row)
        {
            marks[markCount] = enemies.GetEnemy(row);
            Vector3 sigil = GetSigilPosition(row);
            lastSigils[markCount] = sigil;
            markCount++;
            Vector3 position = enemies.GetPosition(row);
            EmitSized(markStamp, sigil, RogueLooks.CRIMSON, STAMP_SIZE);
            markApply.Play(sigil, FxSizes.METRE_UNIT, fxScale, RogueLooks.CRIMSON);
            if (isEvolved)
            {
                EmitSized(sentenceBeam, Ground.Flat(position) + LIFT, RogueLooks.CRIMSON, SENTENCE_BEAM_SIZE);
            }
            PlayAt("mark_apply", position);
        }

        private void RemoveMark(int index)
        {
            int last = markCount - 1;
            marks[index] = marks[last];
            lastSigils[index] = lastSigils[last];
            marks[last] = Enemy.None;
            markCount = last;
        }

        /// <summary>Where a mark's reticle floats: over its head.</summary>
        private Vector3 GetSigilPosition(int row) => enemies.GetPosition(row) + Ground.Up(enemies.GetHeight(row) + SIGIL_ABOVE_HEAD);

        /// <summary>Refreshes a mark's reticle (Death Sentence: and its rune), drifting as the mark moved since the last one.</summary>
        private void EmitSigil(int index, Vector3 sigil)
        {
            Vector3 drift = (sigil - lastSigils[index]) * (1.0f / SIGIL_REFRESH);
            lastSigils[index] = sigil;
            float seconds = SIGIL_REFRESH * SIGIL_OVERLAP;
            if (!isEvolved)
            {
                effects.EmitMoving(markReticle, sigil, drift, 1, RogueLooks.CRIMSON, RETICLE_SIZE, seconds);
                return;
            }
            effects.EmitMoving(markReticle, sigil, drift, 1, RogueLooks.CRIMSON, SENTENCE_RETICLE_SIZE, seconds);
            effects.EmitMoving(sentenceSigil, sigil, drift, 1, RogueLooks.SENTENCE_RUNE, SENTENCE_SIGIL_SIZE, seconds);
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            switch (e.kind)
            {
                case CLONE:
                    ShowClone(e);
                    break;
                case STAB:
                    Stab(e);
                    break;
                case DAGGERS:
                    ReleaseDaggers(e.position);
                    break;
            }
        }

        /// <summary>A smoky afterimage rises behind the mark (seen from the hero); it stabs a moment later.</summary>
        private void ShowClone(in WeaponEvent e)
        {
            int row = enemies.GetRow(e.target);
            if (row < 0)
            {
                return;
            }
            Vector3 position = enemies.GetPosition(row);
            float side = Random.Float() < 0.5f ? -CLONE_SIDE_ANGLE : CLONE_SIDE_ANGLE;
            Vector3 offset = Ground.Rotate(Ground.Direction(heroPosition, position, facing), side);
            Vector3 behind = Ground.Flat(position) + offset * (enemies.GetRadius(row) + CLONE_DISTANCE);
            EmitShade(behind, level[WeaponField.Stagger] + CLONE_LINGER);
            WeaponEvent stab = e;
            stab.kind = STAB;
            stab.position = behind;
            stab.direction = -offset;
            Schedule(level[WeaponField.Stagger], stab);
        }

        /// <summary>The clone: a dark shade with a violet rim and two crimson eyes, rising out of shadow smoke.</summary>
        private void EmitShade(Vector3 behind, float seconds)
        {
            Vector3 foot = behind + LIFT;
            float height = CLONE_HEIGHT * (isEvolved ? SENTENCE_CLONE_SCALE : 1.0f);
            effects.Emit(cloneShade, foot, 1, WHITE, height, seconds);
            effects.Emit(cloneRim, foot, 1, RogueLooks.SHADOW_VIOLET, height, seconds);
            Vector3 eyeLine = foot + Ground.Up(height * EYE_HEIGHT_SHARE);
            effects.Emit(eyes, eyeLine + new Vector3(-EYE_GAP, 0.0f, 0.0f), 1, RogueLooks.CRIMSON, EYE_SIZE, seconds);
            effects.Emit(eyes, eyeLine + new Vector3(EYE_GAP, 0.0f, 0.0f), 1, RogueLooks.CRIMSON, EYE_SIZE, seconds);
            cloneSmoke.Play(behind + BODY, FxSizes.METRE_UNIT, fxScale, WHITE);
        }

        private void Stab(in WeaponEvent e)
        {
            int row = enemies.GetRow(e.target);
            if (row < 0)
            {
                return;
            }
            Vector3 position = enemies.GetPosition(row);
            Vector3 away = Ground.Direction(heroPosition, position, facing);
            var effect = WithAutoCrit(new HitEffect { forceCrit = level[WeaponField.CritChance] >= 1.0f });
            Vector3 push = away * KnockbackOf(level[WeaponField.Knockback]);
            if (strikes.Hit(row, DamageOf(WeaponField.Damage, e.cast), push, slot, effect) == DamageResult.Hurt)
            {
                enemies.Stun(row, STAB_FREEZE_SECONDS);
            }
            Vector3 thrust = e.direction.sqrMagnitude > 0.0f ? e.direction : -away;
            EmitStab(position + BODY, thrust);
            cloneVanish.Play(e.position + BODY, FxSizes.METRE_UNIT, fxScale, RogueLooks.SHADOW_VIOLET);
            PlayAt("mark_stab", position);
        }

        /// <summary>The clone's thrust through the mark along <paramref name="thrust"/>, a crescent slash (Death Sentence: a
        /// crimson X over a dark slash) and crimson sparks.</summary>
        private void EmitStab(Vector3 body, Vector3 thrust)
        {
            effects.EmitMoving(stabThrust, body, thrust * THRUST_DRIFT, 1, RogueLooks.CRIMSON, THRUST_SIZE * fxScale, THRUST_SECONDS);
            if (!isEvolved)
            {
                EmitSized(stabSlash, body, RogueLooks.CRIMSON, STAB_SLASH_SIZE * fxScale);
                stab.Play(body, FxSizes.METRE_UNIT, fxScale, RogueLooks.CRIMSON);
                return;
            }
            EmitSized(darkSlash, body, WHITE, STAB_DARK_SLASH_SIZE);
            EmitSized(crimsonSlash, body, RogueLooks.CRIMSON, STAB_CRIMSON_SLASH_SIZE);
            EmitSized(sentenceX, body, RogueLooks.CRIMSON, SENTENCE_X_SIZE);
            sentenceStab.Play(body, FxSizes.METRE_UNIT, fxScale, RogueLooks.CRIMSON);
        }

        private void Execute(Enemy enemy)
        {
            int row = enemies.GetRow(enemy);
            if (row < 0)
            {
                return;
            }
            Vector3 position = enemies.GetPosition(row);
            enemies.Kill(enemy, slot);
            ctx.feel.HitStop(EXECUTE_HIT_STOP);
            EmitExecution(position);
            PlayAt("execute", position);
        }

        /// <summary>The sentence carried out: a rune circle slams on, a crimson stroke falls from above through a dark
        /// slash, a shaft stands on the spot and a dark splat stays on the ground.</summary>
        private void EmitExecution(Vector3 position)
        {
            Vector3 body = position + BODY;
            Vector3 foot = Ground.Flat(position) + LIFT;
            EmitSized(executeSigil, body, RogueLooks.CRIMSON, EXECUTE_SIGIL_SIZE);
            EmitSized(darkSlash, body, WHITE, EXECUTE_DARK_SLASH_SIZE);
            EmitSized(crimsonSlash, body, RogueLooks.CRIMSON, EXECUTE_CRIMSON_SLASH_SIZE);
            effects.EmitMoving(executeStroke, body + Ground.Up(STROKE_HEIGHT), Ground.Up(-STROKE_SPEED), 1, RogueLooks.CRIMSON, STROKE_WIDTH * fxScale,
                               STROKE_HEIGHT / STROKE_SPEED);
            EmitSized(sentenceBeam, foot, RogueLooks.CRIMSON, SENTENCE_BEAM_SIZE);
            EmitShape(executeSplat, foot, WHITE, SPLAT_SIZE * fxScale, SPLAT_SECONDS);
            execution.Play(body, FxSizes.METRE_UNIT, fxScale, RogueLooks.CRIMSON);
        }

        private void ReleaseDaggers(Vector3 position)
        {
            int count = level.GetCount(WeaponField.SubCount);
            float start = Ground.RandomYaw();
            float speed = SpeedOf(level[WeaponField.SubSpeed]);
            Vector3 origin = position + Ground.Up(DAGGER_HEIGHT);
            for (int k = 0; k < count; k++)
            {
                Vector3 direction = Ground.RingDirection(start, k, count);
                ProjectileSpawn spawn = NewProjectile(daggerVisual, ProjectileMotion.Homing, origin + direction * DAGGER_OUT, direction * speed, DAGGER_LIFETIME);
                spawn.homing = level[WeaponField.Turn] * Ground.DEGREES_TO_RADIANS;
                spawn.damage = MightOf(level[WeaponField.SubDamage]);
                spawn.radius = DAGGER_RADIUS;
                spawn.pierce = 0;
                spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
                spawn.handler = this;
                Spawn(spawn);
            }
            deathShadow.Play(origin, FxSizes.METRE_UNIT, fxScale, RogueLooks.SHADOW_VIOLET);
            deathShards.Play(origin, FxSizes.METRE_UNIT, fxScale, RogueLooks.CRIMSON);
            PlayAt("mark_daggers", position);
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn dagger = ref world.Get(projectile);
            Vector3 impact = dagger.position;
            if (HitAlongFlight(enemyRow, dagger, WithAutoCrit(default)) == DamageResult.Missed)
            {
                return;
            }
            RogueLooks.EmitKnifeCut(effects, daggerCut, impact, FlightOf(dagger), RogueLooks.SHADOW_VIOLET, DAGGER_CUT_SIZE * fxScale);
            daggerHit.Play(impact, FxSizes.METRE_UNIT, fxScale, RogueLooks.SHADOW_VIOLET);
        }
    }
}

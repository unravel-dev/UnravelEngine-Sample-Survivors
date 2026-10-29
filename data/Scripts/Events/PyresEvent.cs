using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Bone Pyres (break them): three giant candle skulls burning with green fire on a 5.5 m circle. When the hero
    /// comes within 14 m they wake for 30 s: each standing pyre raises 3 dead around itself every 3.5 s and, on a green
    /// pulse every second, wards the dead within 7 m (faster and tougher). A pyre has 150 x the minute's health scale. All three
    /// broken: their ashes hold a great gem worth a whole level. Time out: every pyre still standing bursts into an
    /// elite. Look: the skulls with green flames off their candles, the pillar while they wait, health bars over them,
    /// a faint green ring on every other ward pulse.
    /// </summary>
    public sealed class PyresEvent : MapEvent
    {
        private const int PYRES = 3;
        private const float CIRCLE_RADIUS = 5.5f;
        private const float WAKE_DISTANCE = 14.0f;
        private const float LIMIT_SECONDS = 30.0f;
        private const float HEALTH = 150.0f;
        private const float RAISE_INTERVAL = 3.5f;
        private const int RAISE_COUNT = 3;
        private const float RAISE_RADIUS = 1.6f;
        /// <summary>The raise ring turns this many degrees per second of the run, so waves do not rise on the same spots.</summary>
        private const float RAISE_TURN_RATE = 40.0f;
        private const float WARD_INTERVAL = 1.0f;
        private const float WARD_RADIUS = 7.0f;
        private const float WARD_SECONDS = 1.3f;
        private const float MESH_SCALE = 2.6f;
        /// <summary>Each skull turns half around from its spot's outward heading on the circle.</summary>
        private const float SKULL_TURN = 180.0f;
        private const float SHATTER_HEIGHT = 1.0f;
        private const float FULL_TURN_DEGREES = 360.0f;
        private const float FLAME_HEIGHT = 3.0f;
        private const float FLAME_INTERVAL = 0.06f;
        private const int FLAMES_PER_EMIT = 2;
        private const float BAR_HEIGHT = 3.9f;
        /// <summary>The skulls' own green glow (whole-body glows read as white above about 0.35).</summary>
        private const float SKULL_GLOW = 0.3f;
        private const float WARD_RING_SHADE = 0.45f;
        private const float WAITING_GLOW = 0.2f;
        private const int SCRATCH = 512;

        private static readonly Color GREEN = new Color(0.42f, 1.0f, 0.36f, 1.0f);
        /// <summary>The tracker's goal by pyres broken (no string built per frame).</summary>
        private static readonly string[] BREAK_GOALS =
        {
            "Break the pyres: 0 of 3.", "Break the pyres: 1 of 3.", "Break the pyres: 2 of 3.", "Break the pyres: 3 of 3.",
        };

        private readonly Mesh mesh;
        private readonly MaterialInstance skullLook;
        private readonly Vector3[] spots = new Vector3[PYRES];
        private readonly Enemy[] pyres = new Enemy[PYRES];
        private readonly bool[] standing = new bool[PYRES];
        private readonly Matrix4x4[] transforms = new Matrix4x4[PYRES];
        private readonly Matrix4x4[] drawn = new Matrix4x4[PYRES];
        private readonly int[] scratch = new int[SCRATCH];
        private int standingCount;
        private float raiseTimer;
        private float wardTimer;
        private float flameTimer;
        private int wardPulses;

        public override MapEventKind kind => MapEventKind.Pyres;
        public override string title => "Bone Pyres";
        public override string goal => state == MapEventState.Waiting ? "Come near to wake the pyres."
                                     : BREAK_GOALS[PYRES - standingCount];
        public override Color color => GREEN;
        public override string icon => UIText.Icon("xp");
        public override MarkerKind markerKind => MarkerKind.Pyres;

        public PyresEvent(Run run, EventFx fx, Vector3 center) : base(run, fx, center)
        {
            mesh = Assets.GetAsset<Mesh>("app:/data/Environment/Halloween/gltf/skull_candle.gltf");
            Material bone = Assets.GetAsset<Material>("app:/data/Environment/Halloween/gltf/skull_candle_[0] HalloweenBits.mat");
            skullLook = AssetChecks.IsLoaded(bone) ? new MaterialInstance(bone) { emissiveColor = GREEN, emissiveIntensity = SKULL_GLOW } : null;
            float turn = (float)run.rng.NextDouble() * FULL_TURN_DEGREES;
            var options = new SpawnOptions { health = HEALTH * run.director.healthScale, damageScale = 1.0f };
            for (int i = 0; i < PYRES; i++)
            {
                float heading = turn + i * FULL_TURN_DEGREES / PYRES;
                Vector3 spot = center + Ground.FromYaw(heading) * CIRCLE_RADIUS;
                spots[i] = spot;
                pyres[i] = run.enemies.Spawn(EnemyCatalog.pyreType, spot, in options);
                standing[i] = pyres[i].isValid;
                transforms[i] = Matrix4x4.TRS(spot, Quaternion.Euler(0.0f, heading + SKULL_TURN, 0.0f),
                                              new Vector3(MESH_SCALE, MESH_SCALE, MESH_SCALE));
            }
            standingCount = CountStanding();
            Announce("Bone pyres burn on the moor");
        }

        protected override void Tick(float dt, bool refresh)
        {
            ShowPyres(dt, refresh);
            if (state == MapEventState.Waiting)
            {
                if (IsHeroWithin(WAKE_DISTANCE))
                {
                    Activate(LIMIT_SECONDS, "The pyres wake");
                    raiseTimer = 0.0f;
                }
                return;
            }
            raiseTimer -= dt;
            wardTimer -= dt;
            bool raises = raiseTimer <= 0.0f;
            bool wards = wardTimer <= 0.0f;
            raiseTimer = raises ? RAISE_INTERVAL : raiseTimer;
            wardTimer = wards ? WARD_INTERVAL : wardTimer;
            wardPulses += wards ? 1 : 0;
            for (int i = 0; i < PYRES; i++)
            {
                if (!standing[i])
                {
                    continue;
                }
                if (raises)
                {
                    Raise(spots[i]);
                }
                if (wards)
                {
                    Ward(spots[i]);
                }
            }
        }

        /// <summary>Flames off the standing skulls, their health bars, and the pillar and glow while they wait.</summary>
        private void ShowPyres(float dt, bool refresh)
        {
            flameTimer -= dt;
            bool flames = flameTimer <= 0.0f;
            flameTimer = flames ? FLAME_INTERVAL : flameTimer;
            EnemyWorld enemies = run.enemies;
            for (int i = 0; i < PYRES; i++)
            {
                int row = standing[i] ? enemies.GetRow(pyres[i]) : -1;
                if (row < 0)
                {
                    continue;
                }
                if (flames)
                {
                    fx.Emit(fx.flame, spots[i] + new Vector3(0.0f, FLAME_HEIGHT, 0.0f), FLAMES_PER_EMIT, GREEN);
                }
                run.ui.ShowEliteBar(spots[i] + new Vector3(0.0f, BAR_HEIGHT, 0.0f), enemies.GetHealthFraction(row));
            }
            if (refresh && state == MapEventState.Waiting)
            {
                ShowWaiting(CIRCLE_RADIUS, WAITING_GLOW);
            }
        }

        private void Raise(Vector3 spot)
        {
            WaveDirector director = run.director;
            int type = EnemyCatalog.Get(Archetype.Shambler, director.currentTint);
            var options = new SpawnOptions { healthScale = director.healthScale, damageScale = director.damageScale, rises = true, isRewarded = true };
            for (int k = 0; k < RAISE_COUNT; k++)
            {
                Vector3 position = spot + Ground.FromYaw(k * FULL_TURN_DEGREES / RAISE_COUNT + run.runTime * RAISE_TURN_RATE) * RAISE_RADIUS;
                run.enemies.Spawn(type, position, in options);
            }
            run.sounds.PlayAt("raise_chant", spot);
        }

        /// <summary>A pulse that wards the dead around a pyre (proxies such as the pyres shrug it off); every other one
        /// shows as a faint green ring.</summary>
        private void Ward(Vector3 spot)
        {
            int found = run.enemies.QueryCircle(spot, WARD_RADIUS, scratch);
            for (int k = 0; k < found; k++)
            {
                run.enemies.Ward(scratch[k], WARD_SECONDS);
            }
            if (wardPulses % 2 == 1)
            {
                fx.pulse.Play(spot, WARD_RADIUS, 1.0f, Dim(GREEN, WARD_RING_SHADE));
            }
        }

        public override void OnEnemyDied(in EnemyDeath death)
        {
            if (isOver)
            {
                return;
            }
            for (int i = 0; i < PYRES; i++)
            {
                if (!standing[i] || !death.enemy.Equals(pyres[i]))
                {
                    continue;
                }
                standing[i] = false;
                standingCount = CountStanding();
                progress = 1.0f - standingCount / (float)PYRES;
                fx.shatter.Play(spots[i] + new Vector3(0.0f, SHATTER_HEIGHT, 0.0f), 1.0f, 1.0f, GREEN);
                run.sounds.PlayAt("lantern_break", spots[i]);
                if (standingCount == 0)
                {
                    Burn();
                }
                return;
            }
        }

        /// <summary>All three broken: a great gem worth a whole level lies in their ashes.</summary>
        private void Burn()
        {
            float experience = Run.GetExperienceToNext(run.level);
            run.pickups.Spawn(PickupWorld.GetGemKind(experience), center, experience);
            Win("The pyres are ash");
        }

        /// <summary>Every pyre still standing bursts into an elite.</summary>
        protected override void TimeOut()
        {
            for (int i = 0; i < PYRES; i++)
            {
                if (standing[i] && run.enemies.Despawn(pyres[i]))
                {
                    standing[i] = false;
                    run.director.SpawnEliteAt(spots[i]);
                }
            }
            standingCount = 0;
            Lose("The pyres burst");
        }

        protected override void Expire()
        {
            RemovePyres();
            base.Expire();
        }

        public override void Clear()
        {
            RemovePyres();
            skullLook?.Dispose();
        }

        private void RemovePyres()
        {
            for (int i = 0; i < PYRES; i++)
            {
                if (standing[i])
                {
                    run.enemies.Despawn(pyres[i]);
                    standing[i] = false;
                }
            }
            standingCount = 0;
        }

        private int CountStanding()
        {
            int count = 0;
            for (int i = 0; i < PYRES; i++)
            {
                count += standing[i] ? 1 : 0;
            }
            return count;
        }

        public override void Draw()
        {
            if (!AssetChecks.IsLoaded(mesh))
            {
                return;
            }
            int count = 0;
            for (int i = 0; i < PYRES; i++)
            {
                if (standing[i])
                {
                    drawn[count++] = transforms[i];
                }
            }
            if (count > 0)
            {
                Graphics.DrawMeshInstanced(mesh, null, drawn.AsSpan(0, count), materialInstance: skullLook);
            }
        }
    }
}

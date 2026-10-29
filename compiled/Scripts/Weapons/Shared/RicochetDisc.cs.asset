using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Ricochet Disc (shared, Uncommon): Count + Amount discs, Stagger apart, thrown at the nearest enemy within Range
    /// at Speed (Projectile Speed); every hit deals Damage and re-aims the disc, at the same speed, at the nearest
    /// enemy within SubRange it has not visited yet (the visited list clears when it has nowhere new to go), for
    /// SubCount bounces; each bounce pings a semitone higher.
    /// Evolution Eclipse Disc (+ Everburn Wick): the discs bounce without limit for Duration (Duration stat), gaining
    /// +Bonus damage with every bounce, up to BonusCap.
    /// Look: a cyan blade-whirl around the disc (SharedLooks), a swirl at the throw, and on every bounce a flash, a
    /// slash, a ping ring and sparks that grow with the bounce count. Eclipse Disc is a black sun in violet fire whose
    /// hits swallow light (a dark ring) and flare gold corona rays.
    /// </summary>
    public sealed class RicochetDisc : Weapon
    {
        private const int THROW = 0;
        private const int MAX_DISCS = 12;
        private const int VISITED_PER_DISC = 6;
        private const float DISC_RADIUS = 0.45f;
        private const float LAUNCH_HEIGHT = 1.0f;
        private const float LAUNCH_FORWARD = 0.5f;
        private const float RANGE_LIFETIME_SHARE = 1.25f;
        private const float BOUNCE_LIFETIME_SHARE = 1.6f;
        /// <summary>A bounce's flight time divides by the disc's speed, never by less than this.</summary>
        private const float MIN_SPEED = 0.1f;
        /// <summary>The projectile world may hit an enemy again after this; the visited list decides where the disc goes.</summary>
        private const float REHIT_SECONDS = 0.35f;
        /// <summary>2^(1/12): one semitone.</summary>
        private const float SEMITONE = 1.0594631f;
        private const int MAX_PITCH_STEPS = 12;
        private const float THROW_SWIRL_SIZE = 1.2f;
        /// <summary>Bounce hits show at this share of the enemy's height.</summary>
        private const float HIT_HEIGHT_SHARE = 0.6f;
        /// <summary>Each bounce grows the hit effects by this share, up to GROWTH_BOUNCES bounces.</summary>
        private const float BOUNCE_GROWTH = 0.05f;
        private const int GROWTH_BOUNCES = 10;

        private static readonly Color CORONA_GOLD = new Color(1.0f, 0.78f, 0.42f, 1.0f);

        private struct Disc
        {
            public int row;
            /// <summary>Bounces left; -1 never runs out.</summary>
            public int bouncesLeft;
            public int bounces;
            public int visitedCount;
        }

        private readonly Disc[] discs = new Disc[MAX_DISCS];
        private readonly Enemy[] visited = new Enemy[MAX_DISCS * VISITED_PER_DISC];
        private readonly int discVisual;
        private readonly int eclipseVisual;
        private readonly int swirl;
        private readonly FxComposite discHit;
        private readonly FxComposite eclipseHit;
        private readonly FxComposite corona;
        private readonly FxComposite discFade;
        private readonly FxComposite eclipseFade;

        public RicochetDisc(WeaponContext context, WeaponDef definition, int slotIndex) : base(context, definition, slotIndex)
        {
            discVisual = context.FindVisual("RicochetDisc");
            eclipseVisual = context.FindVisual("EclipseDisc");
            swirl = effects.Find("SharedTwirl");
            discHit = Composite(new FxLayer("SharedFlash", 1, 0.6f), new FxLayer("SharedDiscSlash", 1, 1.2f), new FxLayer("SharedPing", 1, 0.8f),
                                new FxLayer("SharedSparks", 7), new FxLayer("SharedGlints", 3));
            eclipseHit = Composite(new FxLayer("SharedFlash", 1, 0.75f), new FxLayer("SharedDiscSlash", 1, 1.35f),
                                   new FxLayer("SharedEclipseVoid", 1, 1.6f, false), new FxLayer("SharedSparks", 9));
            corona = Composite(new FxLayer("SolarRays", 1, 1.1f), new FxLayer("SharedGlints", 5));
            discFade = Composite(new FxLayer("SharedPing", 1, 0.9f), new FxLayer("SharedGlints", 5), new FxLayer("SharedMotes", 2));
            eclipseFade = Composite(new FxLayer("SharedEclipseVoid", 1, 1.8f, false), new FxLayer("SharedPing", 1, 1.2f),
                                    new FxLayer("SharedGlints", 8), new FxLayer("SharedMotes", 4));
            for (int i = 0; i < MAX_DISCS; i++)
            {
                discs[i].row = -1;
            }
        }

        protected override bool Cast(in CastInfo cast)
        {
            if (!TryNearest(level[WeaponField.Range], out _))
            {
                return false;
            }
            int count = level.GetCount(WeaponField.Count) + amount;
            for (int i = 0; i < count; i++)
            {
                Schedule(level[WeaponField.Stagger] * i, new WeaponEvent { kind = THROW, cast = cast });
            }
            RequestAttack("Throw");
            return true;
        }

        protected override void OnEvent(in WeaponEvent e)
        {
            if (e.kind == THROW)
            {
                Throw(e.cast);
            }
        }

        private void Throw(in CastInfo cast)
        {
            int index = FindFreeDisc();
            if (index < 0)
            {
                return;
            }
            Vector3 direction = AimFacingNearest(level[WeaponField.Range]);
            Vector3 origin = heroPosition + direction * LAUNCH_FORWARD + Ground.Up(LAUNCH_HEIGHT);
            float speed = SpeedOf(level[WeaponField.Speed]);
            float lifetime = isEvolved ? DurationOf(level[WeaponField.Duration]) : level[WeaponField.Range] * RANGE_LIFETIME_SHARE / speed;
            ProjectileSpawn spawn = NewProjectile(isEvolved ? eclipseVisual : discVisual, ProjectileMotion.Linear, origin, direction * speed, lifetime);
            spawn.damage = DamageOf(WeaponField.Damage, cast);
            spawn.radius = AreaOf(DISC_RADIUS);
            spawn.rehitInterval = REHIT_SECONDS;
            spawn.knockback = KnockbackOf(level[WeaponField.Knockback]);
            spawn.handler = this;
            spawn.userValue = index;
            int row = Spawn(spawn);
            if (row < 0)
            {
                return;
            }
            discs[index] = new Disc { row = row, bouncesLeft = level.GetCount(WeaponField.SubCount), bounces = 0, visitedCount = 0 };
            EmitSized(swirl, origin, isEvolved ? SharedLooks.ECLIPSE : SharedLooks.DISC, THROW_SWIRL_SIZE * fxScale);
            PlayAt("disc_throw", origin);
        }

        public override void OnHitEnemy(ProjectileWorld world, int projectile, int enemyRow)
        {
            ref ProjectileSpawn spawn = ref world.Get(projectile);
            int index = spawn.userValue;
            if (index < 0 || index >= MAX_DISCS || discs[index].row != projectile)
            {
                return;
            }
            ref Disc disc = ref discs[index];
            Vector3 position = enemies.GetPosition(enemyRow);
            float bonus = MathF.Min(level[WeaponField.BonusCap], level[WeaponField.Bonus] * disc.bounces);
            strikes.Hit(enemyRow, spawn.damage * (1.0f + bonus), FlightOf(spawn) * spawn.knockback, slot);
            PlayAt("disc_ping", position, 1.0f, MathF.Pow(SEMITONE, Math.Min(disc.bounces, MAX_PITCH_STEPS)));
            ShowBounce(position + Ground.Up(enemies.GetHeight(enemyRow) * HIT_HEIGHT_SHARE), disc.bounces, spawn.visual == eclipseVisual);
            Remember(index, enemies.GetEnemy(enemyRow));
            disc.bounces++;
            if (disc.bouncesLeft == 0)
            {
                world.Expire(projectile);
                return;
            }
            if (!TryFindNext(index, position, out int next))
            {
                return;
            }
            if (disc.bouncesLeft > 0)
            {
                disc.bouncesLeft--;
            }
            float speed = spawn.velocity.magnitude;
            spawn.velocity = Ground.Direction(position, enemies.GetPosition(next), FlightOf(spawn)) * speed;
            if (!isEvolved)
            {
                spawn.lifetime = world.GetAge(projectile) + level[WeaponField.SubRange] * BOUNCE_LIFETIME_SHARE / MathF.Max(speed, MIN_SPEED);
            }
        }

        public override void OnExpire(ProjectileWorld world, int projectile, Vector3 position)
        {
            int index = world.Get(projectile).userValue;
            if (index >= 0 && index < MAX_DISCS && discs[index].row == projectile)
            {
                discs[index].row = -1;
                ShowFade(position, world.Get(projectile).visual == eclipseVisual);
            }
        }

        /// <summary>A bounce hit: grander with every bounce; the Eclipse Disc adds its dark ring and gold corona.</summary>
        private void ShowBounce(Vector3 at, int bounces, bool isEclipse)
        {
            float size = fxScale * (1.0f + BOUNCE_GROWTH * Math.Min(bounces, GROWTH_BOUNCES));
            if (!isEclipse)
            {
                discHit.Play(at, size, fxScale, SharedLooks.DISC);
                return;
            }
            eclipseHit.Play(at, size, fxScale, SharedLooks.ECLIPSE);
            corona.Play(at, size, fxScale, CORONA_GOLD);
        }

        private void ShowFade(Vector3 at, bool isEclipse)
        {
            if (!isEclipse)
            {
                discFade.Play(at, fxScale, fxScale, SharedLooks.DISC);
                return;
            }
            eclipseFade.Play(at, fxScale, fxScale, SharedLooks.ECLIPSE);
            corona.Play(at, fxScale, fxScale, CORONA_GOLD);
        }

        /// <summary>The nearest unvisited enemy within bounce range; when there is none the disc forgets all but the last.</summary>
        private bool TryFindNext(int index, Vector3 from, out int next)
        {
            float range = level[WeaponField.SubRange];
            if (strikes.FindNearestExcept(from, range, GetVisited(index), out next))
            {
                return true;
            }
            ref Disc disc = ref discs[index];
            if (disc.visitedCount <= 1)
            {
                return false;
            }
            int start = index * VISITED_PER_DISC;
            visited[start] = visited[start + (disc.visitedCount - 1) % VISITED_PER_DISC];
            disc.visitedCount = 1;
            return strikes.FindNearestExcept(from, range, GetVisited(index), out next);
        }

        private ReadOnlySpan<Enemy> GetVisited(int index)
        {
            return visited.AsSpan(index * VISITED_PER_DISC, Math.Min(discs[index].visitedCount, VISITED_PER_DISC));
        }

        private void Remember(int index, Enemy enemy)
        {
            ref Disc disc = ref discs[index];
            visited[index * VISITED_PER_DISC + disc.visitedCount % VISITED_PER_DISC] = enemy;
            disc.visitedCount++;
        }

        private int FindFreeDisc()
        {
            for (int i = 0; i < MAX_DISCS; i++)
            {
                if (discs[i].row < 0 || !projectiles.IsAlive(discs[i].row) || !ReferenceEquals(projectiles.Get(discs[i].row).handler, this))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}

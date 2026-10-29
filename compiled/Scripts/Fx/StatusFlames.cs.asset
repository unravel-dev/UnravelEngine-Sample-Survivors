using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Flames licking off burning bodies: every few hundredths of a second, one burst on each of up to 24 burning
    /// enemies, sized by the body. The burn glow alone reads flat; the budget keeps a burning crowd cheap. Main thread.
    /// </summary>
    public sealed class StatusFlames
    {
        private const float INTERVAL = 0.08f;
        private const int MAX_FLAMES = 24;
        /// <summary>Flame size as a share of the body height.</summary>
        private const float SIZE_SHARE = 0.75f;
        /// <summary>Flames rise from this share of the body height.</summary>
        private const float HEIGHT_SHARE = 0.45f;

        private readonly Effects effects;
        private readonly EnemyWorld enemies;
        private readonly int flame;
        private readonly int[] burning = new int[MAX_FLAMES];
        private float timer;

        public StatusFlames(Effects effects, EnemyWorld enemies)
        {
            this.effects = effects;
            this.enemies = enemies;
            flame = effects.Find("BurnLick");
        }

        /// <summary>Emits the flames when their interval has passed; once per frame with game time.</summary>
        public void Tick(float dt)
        {
            timer -= dt;
            if (timer > 0.0f)
            {
                return;
            }
            timer = INTERVAL;
            int count = enemies.CollectBurning(burning);
            for (int i = 0; i < count; i++)
            {
                int row = burning[i];
                float height = enemies.GetHeight(row);
                Vector3 chest = enemies.GetPosition(row) + new Vector3(0.0f, height * HEIGHT_SHARE, 0.0f);
                effects.EmitSized(flame, chest, 1, Color.white, height * SIZE_SHARE);
            }
        }
    }
}

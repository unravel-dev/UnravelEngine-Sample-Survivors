using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The scene pieces that travel with the hero without being its body: the ambient effects (fog, fireflies) kept on
    /// its ground position, the health ring under its feet (green over half health, amber over a quarter, red and
    /// pulsing below, hidden once the hero falls) and the warm light that makes it read in the crowd. The menus hide
    /// the ring and the light. Main thread.
    /// </summary>
    public sealed class HeroPresence
    {
        private const float HURT_SHARE = 0.5f;
        private const float CRITICAL_SHARE = 0.25f;
        private const float RING_GLOW = 1.4f;
        private const float RING_CRITICAL_PULSE_HERTZ = 2.5f;
        private const int NO_BAND = -1;
        private const int HEALTHY_BAND = 0;
        private const int HURT_BAND = 1;
        private const int CRITICAL_BAND = 2;
        private static readonly Color RING_HEALTHY = new Color(0.35f, 1.0f, 0.45f, 1.0f);
        private static readonly Color RING_HURT = new Color(1.0f, 0.72f, 0.2f, 1.0f);
        private static readonly Color RING_CRITICAL = new Color(1.0f, 0.22f, 0.15f, 1.0f);

        private readonly Entity followEffects;
        private readonly Entity ring;
        private readonly Entity light;
        private MaterialInstance ringLook;
        private int ringBand = NO_BAND;

        /// <param name="followEffects">Root of the ambient effects that follow the hero; may be invalid.</param>
        /// <param name="ring">The health ring (a child of the follow root); may be invalid.</param>
        /// <param name="light">The light over the hero (a child of the follow root); may be invalid.</param>
        public HeroPresence(Entity followEffects, Entity ring, Entity light)
        {
            this.followEffects = followEffects;
            this.ring = ring;
            this.light = light;
        }

        /// <summary>Shows the ring and the light during a run, hides them on the menus.</summary>
        public void SetShown(bool isShown)
        {
            if (ring.IsValid())
            {
                ring.SetActive(isShown);
            }
            if (light.IsValid())
            {
                light.SetActive(isShown);
            }
        }

        /// <summary>Keeps the follow root under the hero and the ring's colour on its health; once per frame.</summary>
        public void Update(HeroController hero)
        {
            if (followEffects.IsValid())
            {
                followEffects.transform.position = new Vector3(hero.position.x, 0.0f, hero.position.z);
            }
            UpdateRing(hero);
        }

        private void UpdateRing(HeroController hero)
        {
            if (!ring.IsValid())
            {
                return;
            }
            ring.SetActive(hero.isAlive);
            ringLook ??= ring.GetComponent<ModelComponent>()?.GetMaterialInstance(0);
            if (ringLook == null)
            {
                return;
            }
            int band = GetBand(hero.healthFraction);
            if (band != ringBand)
            {
                ringBand = band;
                Color color = band == HEALTHY_BAND ? RING_HEALTHY : band == HURT_BAND ? RING_HURT : RING_CRITICAL;
                ringLook.baseColor = color;
                ringLook.emissiveColor = color;
            }
            float pulse = band == CRITICAL_BAND ? 0.5f + 0.5f * MathF.Sin(Time.realtimeSinceStartup * RING_CRITICAL_PULSE_HERTZ * MathF.PI * 2.0f) : 0.0f;
            ringLook.emissiveIntensity = RING_GLOW * (1.0f + pulse);
        }

        private static int GetBand(float healthFraction)
        {
            if (healthFraction > HURT_SHARE)
            {
                return HEALTHY_BAND;
            }
            return healthFraction > CRITICAL_SHARE ? HURT_BAND : CRITICAL_BAND;
        }
    }
}

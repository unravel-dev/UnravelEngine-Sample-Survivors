using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// The backdrop of the title and hero select: the four display heroes by the sanctum (children of the stage named
    /// like <see cref="HeroClass"/>), the camera framing them and the spotlight on the hero select's focused class,
    /// which cheers while the others stand ready. A run hides the stage. Main thread.
    /// </summary>
    public sealed class MenuStage
    {
        private const float CAMERA_PITCH = 18.0f;
        private const float CAMERA_DISTANCE = 11.0f;
        private const string FOCUSED_STATE = "Cheer";
        private const string WAITING_STATE = "Locomotion";
        private const float CROSS_FADE_SECONDS = 0.25f;
        private static readonly Vector3 FOCUS_LIFT = new Vector3(0.0f, 1.2f, 0.0f);
        private static readonly Vector3 SPOTLIGHT_OFFSET = new Vector3(0.0f, 4.5f, -2.5f);

        private readonly Entity stage;
        private readonly Entity spotlight;
        private readonly Entity[] heroes = new Entity[(int)HeroClass.Count];

        /// <param name="stage">Parent of the display heroes; may be invalid (no stage, the camera frames the origin).</param>
        /// <param name="spotlight">Light moved over the focused hero; may be invalid.</param>
        public MenuStage(Entity stage, Entity spotlight)
        {
            this.stage = stage;
            this.spotlight = spotlight;
            if (!stage.IsValid())
            {
                return;
            }
            for (int i = 0; i < heroes.Length; i++)
            {
                heroes[i] = stage.transform.FindChild(((HeroClass)i).ToString());
            }
        }

        /// <summary>The point the menu camera looks at.</summary>
        public Vector3 focus { get; private set; }

        /// <summary>Shows the stage, frames it with the camera and focuses the Knight.</summary>
        public void Show(CameraRig camera)
        {
            SetActive(true);
            focus = stage.IsValid() ? stage.transform.position + FOCUS_LIFT : Vector3.zero;
            camera.pitch = CAMERA_PITCH;
            camera.baseDistance = CAMERA_DISTANCE;
            camera.focusOffset = Vector3.zero;
            camera.Snap(focus);
            Focus(HeroClass.Knight);
        }

        /// <summary>Hides the stage (a run starts).</summary>
        public void Hide() => SetActive(false);

        /// <summary>Hero select focused a class: it cheers under the spotlight, the others stand ready.</summary>
        public void Focus(HeroClass focused)
        {
            for (int i = 0; i < heroes.Length; i++)
            {
                AnimationComponent animator = heroes[i].IsValid() ? heroes[i].GetComponent<AnimationComponent>() : null;
                animator?.CrossFade(i == (int)focused ? FOCUSED_STATE : WAITING_STATE, CROSS_FADE_SECONDS);
            }
            Entity hero = heroes[(int)focused];
            if (hero.IsValid() && spotlight.IsValid())
            {
                spotlight.transform.position = hero.transform.position + SPOTLIGHT_OFFSET;
            }
        }

        private void SetActive(bool isActive)
        {
            if (stage.IsValid())
            {
                stage.SetActive(isActive);
            }
        }
    }
}

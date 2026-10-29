using Unravel.Core;
using Unravel.Samples;

namespace Survivors
{
    /// <summary>
    /// The on-screen stick and skill button of touch screens: shown only while a run plays (the menus take taps and the
    /// test autopilot needs none), with the stick on the side the player's setting picks. Main thread.
    /// </summary>
    public sealed class TouchControls
    {
        private readonly Entity root;
        private SampleTouchJoystick stick;

        /// <param name="root">The controls' entity; may be invalid (no touch controls in the scene).</param>
        public TouchControls(Entity root)
        {
            this.root = root;
        }

        /// <summary>Shows or hides the controls; a newly shown stick takes its side from <paramref name="settings"/>.</summary>
        public void SetShown(bool isShown, GameSettings settings)
        {
            if (!root.IsValid() || root.activeLocal == isShown)
            {
                return;
            }
            if (isShown)
            {
                stick ??= root.GetComponent<SampleTouchJoystick>();
                if (stick != null)
                {
                    stick.RightStick = settings.joystickOnRight;
                }
            }
            root.SetActive(isShown);
        }
    }
}

using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The device the player used last; decides which hints and whether the focus ring show.</summary>
    public enum InputDevice
    {
        Mouse,
        Keyboard,
        Gamepad,
        Touch,
    }

    /// <summary>A step of keyboard / gamepad navigation in screen axes.</summary>
    internal enum NavDirection
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// Menu input read once per frame. Keys reach RmlUi documents only while a text field has focus, so menus read
    /// the keyboard with key codes and the gamepad through input actions (UISubmit, UICancel, UIUp, UIDown, UILeft,
    /// UIRight, UIReroll, UIBanish, UISkip; see manifest_ui.md); the Horizontal / Vertical axes (WASD, arrows, left
    /// stick) move the focus with key repeat. During play only the device is tracked: the pointer, a touch, walking
    /// with the keyboard or a gamepad stick (on touch screens the on-screen stick drives the gamepad axes, so there
    /// stick movement does not count as a gamepad). Main thread.
    /// </summary>
    internal sealed class UIInput
    {
        private const float AXIS_PRESS = 0.6f;
        private const float AXIS_RELEASE = 0.35f;
        private const float REPEAT_DELAY = 0.38f;
        private const float REPEAT_INTERVAL = 0.11f;
        private const float MOUSE_MOVE_PIXELS = 3.0f;
        private const int NO_DIGIT = -1;

        private static readonly KeyCode[] keyboardNavigation =
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Up, KeyCode.Down, KeyCode.Left, KeyCode.Right,
        };

        private NavDirection heldDirection;
        private float repeatTimer;
        private Vector2 lastMouse;
        private bool hasMouse;

        /// <summary>The navigation step of this frame (with key repeat), or None.</summary>
        public NavDirection direction;
        public bool submit;
        public bool cancel;
        public bool pause;
        public bool reroll;
        public bool banish;
        public bool skip;
        /// <summary>-1, or 0..2 when 1 / 2 / 3 was pressed.</summary>
        public int digit = NO_DIGIT;
        /// <summary>Any key, button, click or new touch this frame (lands the chest reels).</summary>
        public bool anyButton;
        public InputDevice device = InputDevice.Mouse;
        /// <summary>The device changed this frame.</summary>
        public bool deviceChanged;

        public bool up => direction == NavDirection.Up;
        public bool down => direction == NavDirection.Down;
        public bool left => direction == NavDirection.Left;
        public bool right => direction == NavDirection.Right;

        /// <summary>The keyboard or a gamepad moves the focus ring (mouse and touch point instead).</summary>
        public bool isFocusDevice => device == InputDevice.Keyboard || device == InputDevice.Gamepad;

        /// <summary>Reads this frame's input: everything while a menu is up, only the device during play.</summary>
        public void Update(float unscaledDt, bool isMenu)
        {
            InputDevice previous = device;
            ClearFrame();
            DetectPointer();
            if (isMenu)
            {
                ReadButtons();
                ReadDirections(unscaledDt);
            }
            else
            {
                DetectMovement();
            }
            deviceChanged = device != previous;
        }

        private void ClearFrame()
        {
            direction = NavDirection.None;
            submit = cancel = pause = reroll = banish = skip = anyButton = false;
            digit = NO_DIGIT;
        }

        private void DetectPointer()
        {
            if (Input.touchCount > 0)
            {
                device = InputDevice.Touch;
                return;
            }
            Vector2 mouse = Input.mousePosition;
            if (hasMouse && (mouse - lastMouse).sqrMagnitude > MOUSE_MOVE_PIXELS * MOUSE_MOVE_PIXELS && device != InputDevice.Touch)
            {
                device = InputDevice.Mouse;
            }
            if (Input.IsPressed(MouseButton.Left) && device != InputDevice.Touch)
            {
                device = InputDevice.Mouse;
            }
            lastMouse = mouse;
            hasMouse = true;
        }

        /// <summary>During play: walking with the keyboard, or a gamepad stick off touch screens.</summary>
        private void DetectMovement()
        {
            if (IsKeyboardNavigationDown())
            {
                device = InputDevice.Keyboard;
                return;
            }
            if (!Input.touchSupported && IsAxisPressed(AXIS_PRESS))
            {
                device = InputDevice.Gamepad;
            }
        }

        private void ReadButtons()
        {
            bool keySubmit = Input.IsPressed(KeyCode.Enter) || Input.IsPressed(KeyCode.KpEnter) || Input.IsPressed(KeyCode.Space);
            bool keyCancel = Input.IsPressed(KeyCode.Escape) || Input.IsPressed(KeyCode.Backspace);
            bool keyPause = Input.IsPressed(KeyCode.P);
            bool keyReroll = Input.IsPressed(KeyCode.R);
            bool keyBanish = Input.IsPressed(KeyCode.X);
            bool keySkip = Input.IsPressed(KeyCode.Q);
            digit = ReadDigit();
            bool padSubmit = Input.IsPressed("UISubmit");
            bool padCancel = Input.IsPressed("UICancel");
            bool padReroll = Input.IsPressed("UIReroll");
            bool padBanish = Input.IsPressed("UIBanish");
            bool padSkip = Input.IsPressed("UISkip");
            bool padPause = Input.IsPressed("Pause");
            submit = keySubmit || padSubmit;
            cancel = keyCancel || padCancel;
            pause = keyPause || padPause;
            reroll = keyReroll || padReroll;
            banish = keyBanish || padBanish;
            skip = keySkip || padSkip;
            bool anyKey = keySubmit || keyCancel || keyPause || keyReroll || keyBanish || keySkip || digit != NO_DIGIT;
            bool anyPad = padSubmit || padCancel || padReroll || padBanish || padSkip;
            if (anyKey)
            {
                device = InputDevice.Keyboard;
            }
            else if (anyPad)
            {
                device = InputDevice.Gamepad;
            }
            anyButton = anyKey || anyPad || padPause || Input.IsPressed(MouseButton.Left) || HasNewTouch();
        }

        /// <summary>0..2 for the keys 1 / 2 / 3 (top row or keypad), else -1.</summary>
        private static int ReadDigit()
        {
            int pressed = NO_DIGIT;
            if (Input.IsPressed(KeyCode.Digit1) || Input.IsPressed(KeyCode.KpDigit1)) pressed = 0;
            if (Input.IsPressed(KeyCode.Digit2) || Input.IsPressed(KeyCode.KpDigit2)) pressed = 1;
            if (Input.IsPressed(KeyCode.Digit3) || Input.IsPressed(KeyCode.KpDigit3)) pressed = 2;
            return pressed;
        }

        /// <summary>A finger touched the screen this frame (a finger resting on it is no press).</summary>
        private static bool HasNewTouch()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).Phase == TouchPhase.Began)
                {
                    return true;
                }
            }
            return false;
        }

        private void ReadDirections(float dt)
        {
            NavDirection held = ReadHeldDirection(out bool fromPad);
            if (held == NavDirection.None)
            {
                heldDirection = NavDirection.None;
                return;
            }
            if (held != heldDirection)
            {
                heldDirection = held;
                repeatTimer = REPEAT_DELAY;
                device = fromPad ? InputDevice.Gamepad : InputDevice.Keyboard;
                direction = held;
                return;
            }
            repeatTimer -= dt;
            if (repeatTimer <= 0.0f)
            {
                repeatTimer += REPEAT_INTERVAL;
                direction = held;
            }
        }

        /// <summary>The direction held now: the d-pad first, then the axes (with hysteresis while one is held).</summary>
        private NavDirection ReadHeldDirection(out bool fromPad)
        {
            fromPad = true;
            if (Input.IsDown("UIUp")) return NavDirection.Up;
            if (Input.IsDown("UIDown")) return NavDirection.Down;
            if (Input.IsDown("UILeft")) return NavDirection.Left;
            if (Input.IsDown("UIRight")) return NavDirection.Right;
            NavDirection axis = ReadAxisDirection(heldDirection != NavDirection.None ? AXIS_RELEASE : AXIS_PRESS);
            fromPad = axis != NavDirection.None && !IsKeyboardNavigationDown();
            return axis;
        }

        private static NavDirection ReadAxisDirection(float threshold)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            if (MathF.Abs(v) >= MathF.Abs(h) && MathF.Abs(v) > threshold)
            {
                return v > 0.0f ? NavDirection.Up : NavDirection.Down;
            }
            if (MathF.Abs(h) > threshold)
            {
                return h < 0.0f ? NavDirection.Left : NavDirection.Right;
            }
            return NavDirection.None;
        }

        private static bool IsAxisPressed(float threshold) => ReadAxisDirection(threshold) != NavDirection.None;

        private static bool IsKeyboardNavigationDown()
        {
            foreach (KeyCode key in keyboardNavigation)
            {
                if (Input.IsDown(key))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Keyboard and gamepad focus over a screen's ".nav" elements: spatial moves (the nearest element in the pressed
    /// direction), a "nav-focus" class for the ring, and mouse hover that moves the focus too. Main thread.
    /// </summary>
    internal sealed class UINavigator
    {
        private const string FOCUS_CLASS = "nav-focus";
        private const string DISABLED_CLASS = "disabled";
        private const float CROSS_AXIS_WEIGHT = 2.5f;
        /// <summary>Candidates must lie at least this many pixels ahead in the pressed direction.</summary>
        private const float MIN_STEP_PIXELS = 1.0f;

        private readonly List<UIElement> items = new List<UIElement>();
        private UIElement current;

        /// <summary>The focused element, or null.</summary>
        public UIElement focused => current != null && current.IsValid() ? current : null;

        public int count => items.Count;

        /// <summary>Collects the visible ".nav" elements of <paramref name="scope"/> (data-for templates are hidden and
        /// skipped). Keeps the focus when its element is still there.</summary>
        public void Collect(UIElement scope, string selector = ".nav")
        {
            items.Clear();
            if (scope == null || !scope.IsValid())
            {
                return;
            }
            foreach (UIElement element in scope.QueryAll(selector))
            {
                if (element.IsVisible && !element.HasClass(DISABLED_CLASS))
                {
                    items.Add(element);
                }
            }
            if (current != null && !Contains(current))
            {
                SetFocus(null);
            }
        }

        public UIElement Get(int index) => index >= 0 && index < items.Count ? items[index] : null;

        public int IndexOf(UIElement element)
        {
            if (element == null)
            {
                return -1;
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Equals(element))
                {
                    return i;
                }
            }
            return -1;
        }

        public bool Contains(UIElement element) => IndexOf(element) >= 0;

        /// <summary>Moves the ring to <paramref name="element"/> (null clears it); returns whether it changed.</summary>
        public bool SetFocus(UIElement element)
        {
            if (element != null && current != null && element.Equals(current))
            {
                return false;
            }
            if (current != null && current.IsValid())
            {
                current.SetClass(FOCUS_CLASS, false);
            }
            current = element;
            if (current != null && current.IsValid())
            {
                current.SetClass(FOCUS_CLASS, true);
            }
            return true;
        }

        /// <summary>Focuses the item at <paramref name="preferred"/> (clamped) when nothing is focused.</summary>
        public void EnsureFocus(int preferred = 0)
        {
            if (focused == null && items.Count > 0)
            {
                SetFocus(items[Math.Clamp(preferred, 0, items.Count - 1)]);
            }
        }

        /// <summary>Moves toward a direction; returns whether the focus moved.</summary>
        public bool Move(NavDirection direction)
        {
            if (items.Count == 0 || direction == NavDirection.None)
            {
                return false;
            }
            if (focused == null)
            {
                SetFocus(items[0]);
                return true;
            }
            UIElement best = FindNearest(GetStep(direction));
            return best != null && SetFocus(best);
        }

        public void Clear()
        {
            SetFocus(null);
            items.Clear();
        }

        /// <summary>The direction as a unit step in screen axes (y down).</summary>
        private static Vector2 GetStep(NavDirection direction)
        {
            switch (direction)
            {
                case NavDirection.Up: return new Vector2(0.0f, -1.0f);
                case NavDirection.Down: return new Vector2(0.0f, 1.0f);
                case NavDirection.Left: return new Vector2(-1.0f, 0.0f);
                default: return new Vector2(1.0f, 0.0f);
            }
        }

        /// <summary>The item ahead of the focused one along <paramref name="step"/> with the best score: distance
        /// along the step plus a weighted sideways offset.</summary>
        private UIElement FindNearest(Vector2 step)
        {
            Vector2 origin = GetCenter(current.rect);
            UIElement best = null;
            float bestScore = float.MaxValue;
            foreach (UIElement candidate in items)
            {
                if (candidate.Equals(current))
                {
                    continue;
                }
                Vector2 delta = GetCenter(candidate.rect) - origin;
                float along = delta.x * step.x + delta.y * step.y;
                if (along <= MIN_STEP_PIXELS)
                {
                    continue;
                }
                float across = MathF.Abs(step.x != 0.0f ? delta.y : delta.x);
                float score = along + across * CROSS_AXIS_WEIGHT;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private static Vector2 GetCenter(Rect rect) => new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
    }
}

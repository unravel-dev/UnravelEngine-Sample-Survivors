using System;
using System.Globalization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>The settings data model ("settings"): every property writes through to the player's settings.</summary>
    public sealed class SettingsModel
    {
        private readonly SettingsScreen screen;

        internal SettingsModel(SettingsScreen screen)
        {
            this.screen = screen;
        }

        public float master
        {
            get => settings.masterVolume;
            set { settings.masterVolume = Math.Clamp(value, 0.0f, 1.0f); screen.OnChanged(); }
        }

        public float music
        {
            get => settings.musicVolume;
            set { settings.musicVolume = Math.Clamp(value, 0.0f, 1.0f); screen.OnChanged(); }
        }

        public float effects
        {
            get => settings.sfxVolume;
            set { settings.sfxVolume = Math.Clamp(value, 0.0f, 1.0f); screen.OnChanged(); }
        }

        public float shake
        {
            get => settings.screenShake;
            set { settings.screenShake = Math.Clamp(value, 0.0f, 1.0f); screen.OnChanged(); }
        }

        /// <summary>0 = Off, 1 = Big hits, 2 = All (<see cref="DamageNumberMode"/>).</summary>
        public int numbers => settings.damageNumbers;

        /// <summary>0 = 100%, 1 = 60%, 2 = 30%.</summary>
        public int opacity => SettingsScreen.GetOpacityIndex(settings.effectsOpacity);

        public bool eliteBars => settings.showEliteBars;

        public bool joystickRight => settings.joystickOnRight;

        /// <summary>0 = 80%, 1 = 100%, 2 = 120%, 3 = 140%.</summary>
        public int uiScale => SettingsScreen.GetUiScaleIndex(settings.uiScale);

        private GameSettings settings => screen.settings;

        public void SetNumbers(int mode) => screen.SetNumbers(mode);

        public void SetOpacity(int index) => screen.SetOpacity(index);

        public void SetEliteBars(int on) => screen.SetEliteBars(on != 0);

        public void SetJoystick(int right) => screen.SetJoystick(right != 0);

        public void SetUiScale(int index) => screen.SetUiScale(index);

        public void Back() => screen.Close();
    }

    /// <summary>
    /// Settings: master / music / effects volume, screen shake, damage numbers (Off / Big hits / All), hero effects
    /// opacity, elite health bars, the touch joystick side and the Interface size. Changes apply at once (audio
    /// buses, shake, layout) and are saved when the screen closes. Each row is a focus stop: left / right change
    /// its value, Submit cycles a choice row's options. Opens from the title or the pause sheet and returns there.
    /// Main thread.
    /// </summary>
    internal sealed class SettingsScreen : UIScreen
    {
        /// <summary>Slider step of left / right (Settings.rhtml's range inputs use the same step).</summary>
        private const float SLIDER_STEP = 0.05f;
        private const int DAMAGE_NUMBER_MODES = (int)DamageNumberMode.All + 1;
        private const string SETTING_ATTRIBUTE = "setting";
        private const string MASTER = "master";
        private const string MUSIC = "music";
        private const string EFFECTS = "effects";
        private const string SHAKE = "shake";
        private const string NUMBERS = "numbers";
        private const string OPACITY = "opacity";
        private const string ELITE_BARS = "elitebars";
        private const string JOYSTICK = "joystick";
        private const string UI_SCALE = "uiscale";

        private static readonly float[] opacityLevels = { 1.0f, 0.6f, 0.3f };
        private static readonly float[] uiScaleLevels = { 0.8f, 1.0f, 1.2f, 1.4f };

        private readonly SettingsModel model;
        private bool isDirty;

        public SettingsScreen(GameUI ui) : base(ui, "Settings", "app:/data/UI/Settings.rhtml")
        {
            model = new SettingsModel(this);
            BindModel("settings", model);
        }

        internal GameSettings settings => ui.gameSettings;

        /// <summary>The opacity option closest to a saved value.</summary>
        internal static int GetOpacityIndex(float value) => GetNearestIndex(opacityLevels, value);

        /// <summary>The Interface size option closest to a saved value.</summary>
        internal static int GetUiScaleIndex(float value) => GetNearestIndex(uiScaleLevels, value);

        protected override void OnModelBound()
        {
            dataModel.AddTransform<float>("percent", v => (v * 100.0f).ToString("0", CultureInfo.InvariantCulture) + "%");
        }

        protected override void Refresh()
        {
            dataModel.DirtyAll();
        }

        /// <summary>A value changed: apply audio and shake now, save on close.</summary>
        internal void OnChanged()
        {
            isDirty = true;
            ui.ApplySettings();
        }

        internal void SetNumbers(int mode)
        {
            settings.damageNumbers = Math.Clamp(mode, (int)DamageNumberMode.Off, (int)DamageNumberMode.All);
            Changed(nameof(SettingsModel.numbers));
        }

        internal void SetOpacity(int index)
        {
            settings.effectsOpacity = opacityLevels[Math.Clamp(index, 0, opacityLevels.Length - 1)];
            Changed(nameof(SettingsModel.opacity));
        }

        internal void SetEliteBars(bool on)
        {
            settings.showEliteBars = on;
            Changed(nameof(SettingsModel.eliteBars));
        }

        internal void SetJoystick(bool right)
        {
            settings.joystickOnRight = right;
            Changed(nameof(SettingsModel.joystickRight));
            ui.ApplyLayoutToAll();
        }

        /// <summary>Interface size: re-scales every document at once.</summary>
        internal void SetUiScale(int index)
        {
            settings.uiScale = uiScaleLevels[Math.Clamp(index, 0, uiScaleLevels.Length - 1)];
            Changed(nameof(SettingsModel.uiScale));
            ui.ApplyLayoutToAll();
        }

        internal void Close()
        {
            if (isDirty)
            {
                isDirty = false;
                run.save?.Save();
            }
            ui.PlaySound("ui_back");
            ui.CloseSubmenu();
        }

        protected override void HandleInput(UIInput input)
        {
            string setting = nav.focused?.GetAttribute(SETTING_ATTRIBUTE);
            if (string.IsNullOrEmpty(setting))
            {
                base.HandleInput(input);
                return;
            }
            if (input.left || input.right)
            {
                Adjust(setting, input.right ? 1 : -1);
                return;
            }
            if (input.submit && !IsSlider(setting))
            {
                Cycle(setting);
                return;
            }
            base.HandleInput(input);
        }

        protected override void Back()
        {
            Close();
        }

        private void Changed(string variable)
        {
            ui.PlaySound("ui_toggle");
            OnChanged();
            dataModel.Dirty(variable);
        }

        /// <summary>Left / right on the focused row: a slider steps, a choice moves to its neighbour (no wrap).</summary>
        private void Adjust(string setting, int direction)
        {
            switch (setting)
            {
                case MASTER:
                    model.master = Step(model.master, direction);
                    break;
                case MUSIC:
                    model.music = Step(model.music, direction);
                    break;
                case EFFECTS:
                    model.effects = Step(model.effects, direction);
                    break;
                case SHAKE:
                    model.shake = Step(model.shake, direction);
                    break;
                case NUMBERS:
                    SetNumbers(settings.damageNumbers + direction);
                    return;
                case OPACITY:
                    SetOpacity(model.opacity + direction);
                    return;
                case ELITE_BARS:
                    SetEliteBars(!settings.showEliteBars);
                    return;
                case JOYSTICK:
                    SetJoystick(direction > 0);
                    return;
                case UI_SCALE:
                    SetUiScale(model.uiScale + direction);
                    return;
                default:
                    return;
            }
            ui.PlaySound("ui_tick");
            dataModel.Dirty(setting);
        }

        /// <summary>Submit on a choice row: the next option, after the last one the first again.</summary>
        private void Cycle(string setting)
        {
            switch (setting)
            {
                case NUMBERS:
                    SetNumbers((settings.damageNumbers + 1) % DAMAGE_NUMBER_MODES);
                    break;
                case OPACITY:
                    SetOpacity((model.opacity + 1) % opacityLevels.Length);
                    break;
                case ELITE_BARS:
                    SetEliteBars(!settings.showEliteBars);
                    break;
                case JOYSTICK:
                    SetJoystick(!settings.joystickOnRight);
                    break;
                case UI_SCALE:
                    SetUiScale((model.uiScale + 1) % uiScaleLevels.Length);
                    break;
            }
        }

        private static bool IsSlider(string setting) => setting == MASTER || setting == MUSIC || setting == EFFECTS || setting == SHAKE;

        private static float Step(float value, int direction)
        {
            return Math.Clamp(MathF.Round((value + direction * SLIDER_STEP) / SLIDER_STEP) * SLIDER_STEP, 0.0f, 1.0f);
        }

        /// <summary>The index of the level closest to <paramref name="value"/> (the first of equally close ones).</summary>
        private static int GetNearestIndex(float[] levels, float value)
        {
            int best = 0;
            for (int i = 1; i < levels.Length; i++)
            {
                if (MathF.Abs(levels[i] - value) < MathF.Abs(levels[best] - value))
                {
                    best = i;
                }
            }
            return best;
        }
    }
}

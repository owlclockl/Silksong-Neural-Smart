using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Поддержка геймпада без жёсткой зависимости от конкретной библиотеки ввода.
    ///
    /// Приоритет источников:
    ///   1. InControl — библиотека, на которой работает сам Silksong (через рефлексию).
    ///      Она даёт «семантические» кнопки (Action1 = A/Cross и т.д.), то есть
    ///      раскладка Xbox/PlayStation/Switch учитывается автоматически.
    ///   2. Классический UnityEngine.Input — кнопки JoystickButton0..9 и оси
    ///      джойстика, если InControl недоступен.
    ///
    /// Состояния кнопок считаются самостоятельно (held/pressed/released),
    /// поэтому источник можно менять на лету без потери «фронтов».
    /// </summary>
    internal static class GamepadInput
    {
        public enum Btn
        {
            Confirm = 0,      // A / Cross
            Cancel,           // B / Circle
            Alt,              // X / Square
            Option,           // Y / Triangle
            LeftBumper,
            RightBumper,
            LeftTrigger,
            RightTrigger,
            Back,             // Back / Select / View
            Start,            // Start / Menu / Options
            LeftStickButton,
            RightStickButton,
            DPadUp,
            DPadDown,
            DPadLeft,
            DPadRight,
            Count,
        }

        private const int BtnCount = (int)Btn.Count;

        private static readonly bool[] Held = new bool[BtnCount];
        private static readonly bool[] Prev = new bool[BtnCount];
        private static readonly bool[] Needed = new bool[BtnCount];

        private static Vector2 _move;
        private static float _lastActivity = -999f;
        private static int _navX, _navY;
        private static float _navNextAt;
        private static int _navDirX, _navDirY;
        private static float _lastTickFrame = -1f;

        /// <summary>Подключён ли хоть один геймпад.</summary>
        public static bool Available { get; private set; }

        /// <summary>Направление «стик + крестовина», уже с мёртвой зоной.</summary>
        public static Vector2 Move { get { return _move; } }

        /// <summary>Шаг навигации по горизонтали с автоповтором: -1, 0 или 1.</summary>
        public static int NavX { get { return _navX; } }

        /// <summary>Шаг навигации по вертикали с автоповтором: -1 (вверх), 0, 1 (вниз).</summary>
        public static int NavY { get { return _navY; } }

        /// <summary>Время последнего действия геймпадом (Time.unscaledTime).</summary>
        public static float LastActivity { get { return _lastActivity; } }

        /// <summary>Использовался ли геймпад в последние несколько секунд.</summary>
        public static bool RecentlyUsed
        {
            get { return Time.unscaledTime - _lastActivity < 4f; }
        }

        /// <summary>Название активного источника ввода — для лога.</summary>
        public static string Backend
        {
            get
            {
                if (!ModConfig.GamepadEnabled) return "disabled";
                if (_inControlOk) return "InControl";
                return Available ? "Unity legacy input" : "none";
            }
        }

        // ------------------------------------------------------------------
        //  Опрос
        // ------------------------------------------------------------------

        /// <summary>
        /// Опросить геймпад. Вызывается раз в кадр.
        /// <paramref name="full"/> = окно открыто (нужны все кнопки и оси),
        /// иначе опрашиваются только кнопки сочетаний открытия/быстрой отправки.
        /// </summary>
        public static void Tick(bool full)
        {
            if (_lastTickFrame == Time.frameCount) return;
            _lastTickFrame = Time.frameCount;

            Array.Copy(Held, Prev, BtnCount);

            if (!ModConfig.GamepadEnabled)
            {
                Available = false;
                ClearState();
                return;
            }

            UpdateNeeded(full);

            bool read = false;
            if (ModConfig.GamepadUseInControl)
                read = ReadInControl(full);

            if (!read)
                read = ReadLegacy(full);

            Available = read;
            if (!read)
            {
                ClearState();
                return;
            }

            // крестовина тоже даёт направление
            float dx = _move.x;
            float dy = _move.y;
            if (Held[(int)Btn.DPadLeft]) dx -= 1f;
            if (Held[(int)Btn.DPadRight]) dx += 1f;
            if (Held[(int)Btn.DPadUp]) dy += 1f;
            if (Held[(int)Btn.DPadDown]) dy -= 1f;
            _move = new Vector2(Mathf.Clamp(dx, -1f, 1f), Mathf.Clamp(dy, -1f, 1f));

            UpdateNavigation();
            UpdateActivity();
        }

        private static void ClearState()
        {
            for (int i = 0; i < BtnCount; i++) Held[i] = false;
            _move = Vector2.zero;
            _navX = 0;
            _navY = 0;
            _navDirX = 0;
            _navDirY = 0;
        }

        private static void UpdateNeeded(bool full)
        {
            for (int i = 0; i < BtnCount; i++)
                Needed[i] = full;

            if (full) return;

            MarkNeeded(ModConfig.MenuCombo);
            MarkNeeded(ModConfig.QuickSendCombo);
        }

        private static void MarkNeeded(Combo combo)
        {
            if (combo == null || combo.Buttons == null) return;
            for (int i = 0; i < combo.Buttons.Length; i++)
                Needed[(int)combo.Buttons[i]] = true;
        }

        private static void UpdateNavigation()
        {
            float dead = Mathf.Clamp(ModConfig.GamepadDeadzone, 0.1f, 0.9f);
            int dirX = _move.x > dead ? 1 : (_move.x < -dead ? -1 : 0);
            int dirY = _move.y > dead ? -1 : (_move.y < -dead ? 1 : 0); // вверх = -1 (на пункт выше)

            _navX = 0;
            _navY = 0;

            if (dirX == 0 && dirY == 0)
            {
                _navDirX = 0;
                _navDirY = 0;
                return;
            }

            float now = Time.unscaledTime;
            if (dirX != _navDirX || dirY != _navDirY)
            {
                _navDirX = dirX;
                _navDirY = dirY;
                _navX = dirX;
                _navY = dirY;
                _navNextAt = now + Mathf.Max(0.05f, ModConfig.GamepadRepeatDelay);
                return;
            }

            if (now >= _navNextAt)
            {
                _navX = dirX;
                _navY = dirY;
                _navNextAt = now + Mathf.Max(0.02f, ModConfig.GamepadRepeatRate);
            }
        }

        private static void UpdateActivity()
        {
            if (_move.sqrMagnitude > 0.25f)
            {
                _lastActivity = Time.unscaledTime;
                return;
            }

            for (int i = 0; i < BtnCount; i++)
            {
                if (Held[i])
                {
                    _lastActivity = Time.unscaledTime;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------
        //  Состояния кнопок
        // ------------------------------------------------------------------

        public static bool IsHeld(Btn button)
        {
            return Held[(int)button];
        }

        public static bool Pressed(Btn button)
        {
            int i = (int)button;
            return Held[i] && !Prev[i];
        }

        public static bool Released(Btn button)
        {
            int i = (int)button;
            return !Held[i] && Prev[i];
        }

        /// <summary>«Принять» с учётом настройки обмена A/B (для привычки Nintendo).</summary>
        public static bool ConfirmPressed()
        {
            return Pressed(ModConfig.GamepadSwapConfirm ? Btn.Cancel : Btn.Confirm);
        }

        public static bool ConfirmHeld()
        {
            return IsHeld(ModConfig.GamepadSwapConfirm ? Btn.Cancel : Btn.Confirm);
        }

        public static bool CancelPressed()
        {
            return Pressed(ModConfig.GamepadSwapConfirm ? Btn.Confirm : Btn.Cancel);
        }

        // ------------------------------------------------------------------
        //  Источник 1: InControl (рефлексия)
        // ------------------------------------------------------------------

        private static bool _inControlResolved;
        private static bool _inControlOk;
        private static PropertyInfo _pActiveDevice;
        private static PropertyInfo _pIsAttached;
        private static MethodInfo _mGetControl;
        private static PropertyInfo _pControlIsPressed;
        private static PropertyInfo _pControlValue;
        private static readonly object[] ControlIds = new object[BtnCount];
        private static object _axisLeftStickX;
        private static object _axisLeftStickY;
        private static readonly object[] InvokeArg = new object[1];
        private static int _inControlFailures;

        private static readonly string[][] ControlNames =
        {
            new[] { "Action1" },
            new[] { "Action2" },
            new[] { "Action3" },
            new[] { "Action4" },
            new[] { "LeftBumper", "LeftShoulder" },
            new[] { "RightBumper", "RightShoulder" },
            new[] { "LeftTrigger" },
            new[] { "RightTrigger" },
            new[] { "Back", "Select", "View", "Share" },
            new[] { "Start", "Menu", "Command", "Options", "Pause" },
            new[] { "LeftStickButton" },
            new[] { "RightStickButton" },
            new[] { "DPadUp" },
            new[] { "DPadDown" },
            new[] { "DPadLeft" },
            new[] { "DPadRight" },
        };

        private static bool ResolveInControl()
        {
            if (_inControlResolved) return _inControlOk;
            _inControlResolved = true;

            try
            {
                Type managerType = FindType("InControl.InputManager");
                if (managerType == null) return false;

                _pActiveDevice = managerType.GetProperty("ActiveDevice",
                    BindingFlags.Public | BindingFlags.Static);
                if (_pActiveDevice == null) return false;

                Type deviceType = _pActiveDevice.PropertyType;
                Type controlIdType = managerType.Assembly.GetType("InControl.InputControlType");
                if (deviceType == null || controlIdType == null || !controlIdType.IsEnum) return false;

                _mGetControl = deviceType.GetMethod("GetControl", new[] { controlIdType });
                _pIsAttached = deviceType.GetProperty("IsAttached", BindingFlags.Public | BindingFlags.Instance);
                if (_mGetControl == null || _pIsAttached == null) return false;

                Type controlType = _mGetControl.ReturnType;
                _pControlIsPressed = controlType.GetProperty("IsPressed", BindingFlags.Public | BindingFlags.Instance);
                _pControlValue = controlType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if (_pControlIsPressed == null || _pControlValue == null) return false;

                for (int i = 0; i < BtnCount; i++)
                    ControlIds[i] = ParseEnum(controlIdType, ControlNames[i]);

                _axisLeftStickX = ParseEnum(controlIdType, new[] { "LeftStickX" });
                _axisLeftStickY = ParseEnum(controlIdType, new[] { "LeftStickY" });

                _inControlOk = ControlIds[(int)Btn.Confirm] != null;
                if (_inControlOk)
                    RosarySharePlugin.LogInfo("Gamepad: using the game's InControl input.");

                return _inControlOk;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("InControl bridge failed: " + e.Message);
                _inControlOk = false;
                return false;
            }
        }

        private static object ParseEnum(Type enumType, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    if (Enum.IsDefined(enumType, names[i]))
                        return Enum.Parse(enumType, names[i]);
                }
                catch
                {
                    // имя отсутствует в этой версии InControl — пробуем следующее
                }
            }
            return null;
        }

        private static bool ReadInControl(bool full)
        {
            if (!ResolveInControl()) return false;
            if (_inControlFailures > 20) return false;

            try
            {
                object device = _pActiveDevice.GetValue(null, null);
                if (device == null) return false;

                object attached = _pIsAttached.GetValue(device, null);
                if (!(attached is bool) || !(bool)attached) return false;

                for (int i = 0; i < BtnCount; i++)
                {
                    if (!Needed[i] || ControlIds[i] == null)
                    {
                        Held[i] = false;
                        continue;
                    }

                    Held[i] = ControlPressed(device, ControlIds[i]);
                }

                _inControlFailures = 0;

                if (full && _axisLeftStickX != null && _axisLeftStickY != null)
                {
                    _move = new Vector2(
                        ControlValue(device, _axisLeftStickX),
                        ControlValue(device, _axisLeftStickY));
                }
                else
                {
                    _move = Vector2.zero;
                }

                return true;
            }
            catch (Exception e)
            {
                _inControlFailures++;
                RosarySharePlugin.LogDebug("InControl read failed: " + e.Message);
                return false;
            }
        }

        private static bool ControlPressed(object device, object controlId)
        {
            InvokeArg[0] = controlId;
            object control = _mGetControl.Invoke(device, InvokeArg);
            if (control == null) return false;

            object pressed = _pControlIsPressed.GetValue(control, null);
            return pressed is bool && (bool)pressed;
        }

        private static float ControlValue(object device, object controlId)
        {
            InvokeArg[0] = controlId;
            object control = _mGetControl.Invoke(device, InvokeArg);
            if (control == null) return 0f;

            object value = _pControlValue.GetValue(control, null);
            return value is float ? (float)value : 0f;
        }

        private static Type FindType(string fullName)
        {
            Type direct = Type.GetType(fullName);
            if (direct != null) return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type t = assemblies[i].GetType(fullName, false);
                    if (t != null) return t;
                }
                catch
                {
                    // динамические сборки могут бросать — пропускаем
                }
            }

            return null;
        }

        // ------------------------------------------------------------------
        //  Источник 2: классический Unity Input
        // ------------------------------------------------------------------

        private static readonly KeyCode[] LegacyButtons =
        {
            KeyCode.JoystickButton0,  // A
            KeyCode.JoystickButton1,  // B
            KeyCode.JoystickButton2,  // X
            KeyCode.JoystickButton3,  // Y
            KeyCode.JoystickButton4,  // LB
            KeyCode.JoystickButton5,  // RB
            KeyCode.None,             // LT — ось
            KeyCode.None,             // RT — ось
            KeyCode.JoystickButton6,  // Back
            KeyCode.JoystickButton7,  // Start
            KeyCode.JoystickButton8,  // L3
            KeyCode.JoystickButton9,  // R3
            KeyCode.None,             // крестовина — оси
            KeyCode.None,
            KeyCode.None,
            KeyCode.None,
        };

        private static readonly HashSet<string> MissingAxes = new HashSet<string>();
        private static float _joystickCheckAt;
        private static bool _joystickPresent;

        private static bool ReadLegacy(bool full)
        {
            if (!JoystickPresent()) return false;

            for (int i = 0; i < BtnCount; i++)
            {
                if (!Needed[i] || LegacyButtons[i] == KeyCode.None)
                {
                    Held[i] = false;
                    continue;
                }

                try
                {
                    Held[i] = Input.GetKey(LegacyButtons[i]);
                }
                catch
                {
                    Held[i] = false;
                }
            }

            // триггеры и крестовина живут на осях
            if (Needed[(int)Btn.LeftTrigger] || Needed[(int)Btn.RightTrigger])
            {
                float triggers = Axis("joystick 1 analog 2") + Axis("joystick 2 analog 2");
                Held[(int)Btn.LeftTrigger] = triggers > 0.5f;
                Held[(int)Btn.RightTrigger] = triggers < -0.5f;
            }

            if (full)
            {
                float dpadX = Axis("joystick 1 analog 5") + Axis("joystick 2 analog 5");
                float dpadY = Axis("joystick 1 analog 6") + Axis("joystick 2 analog 6");
                Held[(int)Btn.DPadLeft] = dpadX < -0.5f;
                Held[(int)Btn.DPadRight] = dpadX > 0.5f;
                Held[(int)Btn.DPadUp] = dpadY > 0.5f;
                Held[(int)Btn.DPadDown] = dpadY < -0.5f;

                float x = Axis("joystick 1 analog 0") + Axis("joystick 2 analog 0");
                float y = Axis("joystick 1 analog 1") + Axis("joystick 2 analog 1");
                // в сыром виде ось Y у джойстиков направлена вниз
                _move = new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(-y, -1f, 1f));
            }
            else
            {
                _move = Vector2.zero;
            }

            return true;
        }

        private static bool JoystickPresent()
        {
            float now = Time.unscaledTime;
            if (now < _joystickCheckAt) return _joystickPresent;
            _joystickCheckAt = now + 2f;

            try
            {
                string[] names = Input.GetJoystickNames();
                _joystickPresent = false;
                for (int i = 0; i < names.Length; i++)
                {
                    if (!string.IsNullOrEmpty(names[i]))
                    {
                        _joystickPresent = true;
                        break;
                    }
                }
            }
            catch
            {
                _joystickPresent = false;
            }

            return _joystickPresent;
        }

        private static float Axis(string name)
        {
            if (MissingAxes.Contains(name)) return 0f;

            try
            {
                return Input.GetAxisRaw(name);
            }
            catch
            {
                // оси с таким именем нет в InputManager игры — больше не спрашиваем
                MissingAxes.Add(name);
                return 0f;
            }
        }

        // ------------------------------------------------------------------
        //  Сочетания кнопок
        // ------------------------------------------------------------------

        /// <summary>Сочетание кнопок геймпада с необязательным удержанием.</summary>
        internal sealed class Combo
        {
            public Btn[] Buttons;
            public float HoldSeconds;
            public string Text;

            private float _heldSince = -1f;
            private bool _fired;

            /// <summary>true ровно один раз за нажатие сочетания.</summary>
            public bool Triggered()
            {
                if (Buttons == null || Buttons.Length == 0) return false;

                bool all = true;
                for (int i = 0; i < Buttons.Length; i++)
                {
                    if (!IsHeld(Buttons[i]))
                    {
                        all = false;
                        break;
                    }
                }

                if (!all)
                {
                    _heldSince = -1f;
                    _fired = false;
                    return false;
                }

                float now = Time.unscaledTime;
                if (_heldSince < 0f) _heldSince = now;

                if (_fired) return false;
                if (now - _heldSince < HoldSeconds) return false;

                _fired = true;
                return true;
            }

            public void Reset()
            {
                _heldSince = -1f;
                _fired = true; // не срабатывать повторно, пока кнопки не отпустят
            }
        }

        private static readonly Dictionary<string, Btn> Tokens = new Dictionary<string, Btn>(StringComparer.OrdinalIgnoreCase)
        {
            { "a", Btn.Confirm }, { "action1", Btn.Confirm }, { "cross", Btn.Confirm }, { "confirm", Btn.Confirm },
            { "b", Btn.Cancel }, { "action2", Btn.Cancel }, { "circle", Btn.Cancel }, { "cancel", Btn.Cancel },
            { "x", Btn.Alt }, { "action3", Btn.Alt }, { "square", Btn.Alt },
            { "y", Btn.Option }, { "action4", Btn.Option }, { "triangle", Btn.Option },
            { "lb", Btn.LeftBumper }, { "l1", Btn.LeftBumper }, { "leftbumper", Btn.LeftBumper }, { "leftshoulder", Btn.LeftBumper },
            { "rb", Btn.RightBumper }, { "r1", Btn.RightBumper }, { "rightbumper", Btn.RightBumper }, { "rightshoulder", Btn.RightBumper },
            { "lt", Btn.LeftTrigger }, { "l2", Btn.LeftTrigger }, { "lefttrigger", Btn.LeftTrigger },
            { "rt", Btn.RightTrigger }, { "r2", Btn.RightTrigger }, { "righttrigger", Btn.RightTrigger },
            { "back", Btn.Back }, { "select", Btn.Back }, { "view", Btn.Back }, { "share", Btn.Back },
            { "start", Btn.Start }, { "menu", Btn.Start }, { "options", Btn.Start }, { "command", Btn.Start },
            { "ls", Btn.LeftStickButton }, { "l3", Btn.LeftStickButton }, { "leftstick", Btn.LeftStickButton },
            { "rs", Btn.RightStickButton }, { "r3", Btn.RightStickButton }, { "rightstick", Btn.RightStickButton },
            { "dup", Btn.DPadUp }, { "dpadup", Btn.DPadUp }, { "up", Btn.DPadUp },
            { "ddown", Btn.DPadDown }, { "dpaddown", Btn.DPadDown }, { "down", Btn.DPadDown },
            { "dleft", Btn.DPadLeft }, { "dpadleft", Btn.DPadLeft }, { "left", Btn.DPadLeft },
            { "dright", Btn.DPadRight }, { "dpadright", Btn.DPadRight }, { "right", Btn.DPadRight },
        };

        /// <summary>Разбирает строку вида «LB+RB» в сочетание кнопок.</summary>
        public static Combo ParseCombo(string text, float holdSeconds)
        {
            if (string.IsNullOrEmpty(text)) return null;

            string trimmed = text.Trim();
            if (trimmed.Length == 0 ||
                trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("-", StringComparison.Ordinal))
                return null;

            string[] parts = trimmed.Split(new[] { '+', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<Btn> buttons = new List<Btn>();

            for (int i = 0; i < parts.Length; i++)
            {
                Btn button;
                if (Tokens.TryGetValue(parts[i].Trim(), out button))
                {
                    if (!buttons.Contains(button))
                        buttons.Add(button);
                }
                else
                {
                    RosarySharePlugin.LogWarning("Unknown gamepad button in combo: " + parts[i]);
                }
            }

            if (buttons.Count == 0) return null;

            return new Combo
            {
                Buttons = buttons.ToArray(),
                HoldSeconds = Mathf.Max(0f, holdSeconds),
                Text = Describe(buttons),
            };
        }

        private static string Describe(List<Btn> buttons)
        {
            string[] labels = new string[buttons.Count];
            for (int i = 0; i < buttons.Count; i++)
                labels[i] = Label(buttons[i]);
            return string.Join("+", labels);
        }

        /// <summary>Короткая подпись кнопки для подсказок в окне.</summary>
        public static string Label(Btn button)
        {
            switch (button)
            {
                case Btn.Confirm: return "A";
                case Btn.Cancel: return "B";
                case Btn.Alt: return "X";
                case Btn.Option: return "Y";
                case Btn.LeftBumper: return "LB";
                case Btn.RightBumper: return "RB";
                case Btn.LeftTrigger: return "LT";
                case Btn.RightTrigger: return "RT";
                case Btn.Back: return "Back";
                case Btn.Start: return "Start";
                case Btn.LeftStickButton: return "L3";
                case Btn.RightStickButton: return "R3";
                case Btn.DPadUp: return "D-Up";
                case Btn.DPadDown: return "D-Down";
                case Btn.DPadLeft: return "D-Left";
                case Btn.DPadRight: return "D-Right";
                default: return button.ToString();
            }
        }

        /// <summary>Подпись кнопки «принять» с учётом обмена A/B.</summary>
        public static string ConfirmLabel
        {
            get { return Label(ModConfig.GamepadSwapConfirm ? Btn.Cancel : Btn.Confirm); }
        }

        /// <summary>Подпись кнопки «отмена» с учётом обмена A/B.</summary>
        public static string CancelLabel
        {
            get { return Label(ModConfig.GamepadSwapConfirm ? Btn.Confirm : Btn.Cancel); }
        }
    }
}

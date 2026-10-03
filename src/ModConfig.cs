using System;
using BepInEx.Configuration;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>Настройки мода (BepInEx/config/com.silksong.rosaryshare.cfg).</summary>
    internal static class ModConfig
    {
        public enum CursorStyle
        {
            /// <summary>Рисуем собственный курсор-иглу, системный скрыт.</summary>
            Soft,

            /// <summary>Только обычный системный курсор.</summary>
            System,

            /// <summary>И свой, и системный.</summary>
            Both,
        }

        // --- Interface ---
        public static string Language = "Auto";
        public static bool ShowToasts = true;
        public static float UiScale = 1f;
        public static bool UseSerifFont = true;
        public static string FontName = string.Empty;
        public static float BackgroundDim = 0.78f;

        // --- Controls ---
        public static string MenuKey = "F7";
        public static string QuickSendKey = "G";
        public static int QuickSendAmount = 100;
        public static bool BlockGameInput = true;
        public static string CursorMode = "Soft";
        public static float CursorScale = 1f;

        // --- Gamepad ---
        public static bool GamepadEnabled = true;
        public static bool GamepadUseInControl = true;
        public static string GamepadMenuCombo = "LB+RB";
        public static float GamepadMenuHoldSeconds = 0.3f;
        public static string GamepadQuickSendCombo = string.Empty;
        public static float GamepadDeadzone = 0.45f;
        public static float GamepadRepeatDelay = 0.35f;
        public static float GamepadRepeatRate = 0.11f;
        public static bool GamepadSwapConfirm = false;

        /// <summary>Разобранное сочетание кнопок открытия окна (может быть null).</summary>
        public static GamepadInput.Combo MenuCombo { get; private set; }

        /// <summary>Разобранное сочетание кнопок быстрой отправки (может быть null).</summary>
        public static GamepadInput.Combo QuickSendCombo { get; private set; }

        // --- Transfers ---
        public static bool AllowReceive = true;
        public static int MaxSendAmount = 100000;
        public static int MaxReceiveAmount = 1000000;
        public static float SendCooldownSeconds = 1.5f;
        public static float AckTimeoutSeconds = 6f;

        public static KeyCode MenuKeyCode
        {
            get { return ParseKey(MenuKey, KeyCode.F7); }
        }

        public static KeyCode QuickSendKeyCode
        {
            get { return ParseKey(QuickSendKey, KeyCode.G); }
        }

        public static CursorStyle CursorStyleValue
        {
            get
            {
                if (string.IsNullOrEmpty(CursorMode)) return CursorStyle.Soft;
                if (CursorMode.Equals("System", StringComparison.OrdinalIgnoreCase)) return CursorStyle.System;
                if (CursorMode.Equals("Both", StringComparison.OrdinalIgnoreCase)) return CursorStyle.Both;
                return CursorStyle.Soft;
            }
        }

        /// <summary>Рисовать ли собственный курсор поверх меню.</summary>
        public static bool DrawSoftCursor
        {
            get { return CursorStyleValue != CursorStyle.System; }
        }

        /// <summary>Показывать ли системный курсор, пока меню открыто.</summary>
        public static bool ShowSystemCursor
        {
            get { return CursorStyleValue != CursorStyle.Soft; }
        }

        private static KeyCode ParseKey(string value, KeyCode fallback)
        {
            KeyCode key;
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out key))
                return key;
            return fallback;
        }

        public static void Bind(ConfigFile config)
        {
            Language = config.Bind(
                "Interface", "Language", Language,
                "Язык интерфейса мода: Auto, English или Russian. / UI language: Auto, English or Russian.").Value;

            ShowToasts = config.Bind(
                "Interface", "Show Toasts", ShowToasts,
                "Показывать всплывающие уведомления о переводах. / Show toast notifications about transfers.").Value;

            UiScale = config.Bind(
                "Interface", "UI Scale", UiScale,
                "Дополнительный масштаб окна (1.0 — авто по разрешению экрана). / Extra menu scale (1.0 — automatic from the screen resolution).").Value;

            UseSerifFont = config.Bind(
                "Interface", "Serif Font", UseSerifFont,
                "Использовать системный шрифт с засечками в духе игры (false — стандартный шрифт Unity). / Use a game-like serif system font (false — the default Unity font).").Value;

            FontName = config.Bind(
                "Interface", "Font Name", FontName,
                "Имя предпочитаемого системного шрифта, например Trajan Pro или Cinzel (пусто — подобрать автоматически). / Preferred system font name, e.g. Trajan Pro or Cinzel (empty — pick automatically).").Value;

            BackgroundDim = config.Bind(
                "Interface", "Background Dim", BackgroundDim,
                "Насколько затемнять игру за открытым меню, 0..1. / How much the game behind the open menu is dimmed, 0..1.").Value;

            MenuKey = config.Bind(
                "Controls", "Menu Key", MenuKey,
                "Клавиша открытия окна обмена бусинами (например F7, F8, Insert). / Key that opens the bead sharing window (e.g. F7, F8, Insert).").Value;

            QuickSendKey = config.Bind(
                "Controls", "Quick Send Key", QuickSendKey,
                "Клавиша быстрой отправки бусин выбранному игроку без открытия окна. / Key that instantly sends beads to the selected player without opening the window.").Value;

            QuickSendAmount = config.Bind(
                "Controls", "Quick Send Amount", QuickSendAmount,
                "Сколько бусин отправляется клавишей быстрой отправки. / Amount of beads sent by the quick send key.").Value;

            BlockGameInput = config.Bind(
                "Controls", "Block Game Input", BlockGameInput,
                "Пока меню открыто, игра не получает нажатия (Хорнет не бегает и не атакует). / While the menu is open the game ignores input (Hornet will not run or attack).").Value;

            CursorMode = config.Bind(
                "Controls", "Cursor Mode", CursorMode,
                "Курсор в меню: Soft — рисованный курсор-игла, System — обычный системный, Both — оба. / Menu cursor: Soft — drawn needle cursor, System — the regular OS cursor, Both — both of them.").Value;

            CursorScale = config.Bind(
                "Controls", "Cursor Scale", CursorScale,
                "Размер рисованного курсора. / Size of the drawn cursor.").Value;

            GamepadEnabled = config.Bind(
                "Gamepad", "Enable Gamepad", GamepadEnabled,
                "Управление меню геймпадом. / Control the menu with a gamepad.").Value;

            GamepadUseInControl = config.Bind(
                "Gamepad", "Use Game Input Library", GamepadUseInControl,
                "Читать геймпад через InControl самой игры (раскладка Xbox/PlayStation определяется автоматически). false — только классический ввод Unity. / Read the gamepad through the game's own InControl (Xbox/PlayStation layouts detected automatically). false — plain Unity input only.").Value;

            GamepadMenuCombo = config.Bind(
                "Gamepad", "Menu Combo", GamepadMenuCombo,
                "Кнопки геймпада, открывающие окно: например LB+RB, Back, Start+Y (пусто — выключено). / Gamepad buttons that open the window: e.g. LB+RB, Back, Start+Y (empty — disabled).").Value;

            GamepadMenuHoldSeconds = config.Bind(
                "Gamepad", "Menu Combo Hold Seconds", GamepadMenuHoldSeconds,
                "Сколько секунд держать сочетание, чтобы открыть окно (защита от случайных нажатий). / How long the combo must be held to open the window (protection against accidental presses).").Value;

            GamepadQuickSendCombo = config.Bind(
                "Gamepad", "Quick Send Combo", GamepadQuickSendCombo,
                "Сочетание кнопок быстрой отправки без открытия окна, например LB+Y (пусто — выключено). / Gamepad combo for quick sending without opening the window, e.g. LB+Y (empty — disabled).").Value;

            GamepadDeadzone = config.Bind(
                "Gamepad", "Stick Deadzone", GamepadDeadzone,
                "Мёртвая зона стика при навигации по меню. / Stick deadzone used for menu navigation.").Value;

            GamepadRepeatDelay = config.Bind(
                "Gamepad", "Repeat Delay", GamepadRepeatDelay,
                "Задержка перед автоповтором при удержании направления. / Delay before the held direction starts repeating.").Value;

            GamepadRepeatRate = config.Bind(
                "Gamepad", "Repeat Rate", GamepadRepeatRate,
                "Интервал автоповтора при удержании направления. / Interval between repeats while a direction is held.").Value;

            GamepadSwapConfirm = config.Bind(
                "Gamepad", "Swap Confirm And Cancel", GamepadSwapConfirm,
                "Поменять местами кнопки «принять» и «отмена» (B принимает, A отменяет). / Swap the confirm and cancel buttons (B confirms, A cancels).").Value;

            AllowReceive = config.Bind(
                "Transfers", "Allow Receive", AllowReceive,
                "Принимать бусины от других игроков (false — входящие переводы автоматически отклоняются и возвращаются отправителю). / Accept beads from other players (false — incoming transfers are automatically declined and refunded).").Value;

            MaxSendAmount = config.Bind(
                "Transfers", "Max Send Amount", MaxSendAmount,
                "Максимум бусин за один исходящий перевод (защита от опечаток). / Maximum beads per outgoing transfer (typo protection).").Value;

            MaxReceiveAmount = config.Bind(
                "Transfers", "Max Receive Amount", MaxReceiveAmount,
                "Максимум бусин за один входящий перевод. / Maximum beads per incoming transfer.").Value;

            SendCooldownSeconds = config.Bind(
                "Transfers", "Send Cooldown Seconds", SendCooldownSeconds,
                "Пауза между исходящими переводами (защита от двойных нажатий). / Delay between outgoing transfers (double-click protection).").Value;

            AckTimeoutSeconds = config.Bind(
                "Transfers", "Ack Timeout Seconds", AckTimeoutSeconds,
                "Сколько секунд ждать подтверждение доставки, прежде чем вернуть бусины. / How long to wait for the delivery acknowledgement before refunding the beads.").Value;

            // санитайзинг значений
            if (QuickSendAmount < 1) QuickSendAmount = 1;
            if (MaxSendAmount < 1) MaxSendAmount = 1;
            if (MaxReceiveAmount < MaxSendAmount) MaxReceiveAmount = MaxSendAmount;
            if (SendCooldownSeconds < 0f) SendCooldownSeconds = 0f;
            if (AckTimeoutSeconds < 2f) AckTimeoutSeconds = 2f;
            if (UiScale < 0.5f) UiScale = 0.5f;
            if (UiScale > 3f) UiScale = 3f;
            if (CursorScale < 0.4f) CursorScale = 0.4f;
            if (CursorScale > 4f) CursorScale = 4f;
            if (BackgroundDim < 0f) BackgroundDim = 0f;
            if (BackgroundDim > 1f) BackgroundDim = 1f;
            if (GamepadDeadzone < 0.1f) GamepadDeadzone = 0.1f;
            if (GamepadDeadzone > 0.9f) GamepadDeadzone = 0.9f;
            if (GamepadMenuHoldSeconds < 0f) GamepadMenuHoldSeconds = 0f;
            if (GamepadRepeatDelay < 0.05f) GamepadRepeatDelay = 0.05f;
            if (GamepadRepeatRate < 0.02f) GamepadRepeatRate = 0.02f;

            MenuCombo = GamepadInput.ParseCombo(GamepadMenuCombo, GamepadMenuHoldSeconds);
            QuickSendCombo = GamepadInput.ParseCombo(GamepadQuickSendCombo, Mathf.Max(0.15f, GamepadMenuHoldSeconds));
        }
    }
}

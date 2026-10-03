using System;
using BepInEx.Configuration;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>Настройки мода (BepInEx/config/com.silksong.rosaryshare.cfg).</summary>
    internal static class ModConfig
    {
        // --- Interface ---
        public static string Language = "Auto";
        public static bool ShowToasts = true;

        // --- Controls ---
        public static string MenuKey = "F7";
        public static string QuickSendKey = "G";
        public static int QuickSendAmount = 100;

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

            MenuKey = config.Bind(
                "Controls", "Menu Key", MenuKey,
                "Клавиша открытия окна обмена бусинами (например F7, F8, Insert). / Key that opens the bead sharing window (e.g. F7, F8, Insert).").Value;

            QuickSendKey = config.Bind(
                "Controls", "Quick Send Key", QuickSendKey,
                "Клавиша быстрой отправки бусин выбранному игроку без открытия окна. / Key that instantly sends beads to the selected player without opening the window.").Value;

            QuickSendAmount = config.Bind(
                "Controls", "Quick Send Amount", QuickSendAmount,
                "Сколько бусин отправляется клавишей быстрой отправки. / Amount of beads sent by the quick send key.").Value;

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
        }
    }
}

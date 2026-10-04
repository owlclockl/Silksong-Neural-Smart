using System;
using System.Collections;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// RosaryShare — отдельный мод-компаньон к «Silksong Multiplayer Mod» (XvX),
    /// позволяющий передавать бусины (розарии) между игроками лобби Steam.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.XvX", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class RosarySharePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.silksong.rosaryshare";
        public const string PluginName = "RosaryShare";
        public const string PluginVersion = "1.4.0";

        internal static RosarySharePlugin Instance { get; private set; }

        /// <summary>Логгер для статических классов (null-безопасно).</summary>
        internal static ManualLogSource Log
        {
            get { return Instance != null ? Instance.Logger : null; }
        }

        internal static void LogInfo(string msg) { if (Log != null) Log.LogInfo(msg); }
        internal static void LogWarning(string msg) { if (Log != null) Log.LogWarning(msg); }
        internal static void LogError(string msg) { if (Log != null) Log.LogError(msg); }
        internal static void LogDebug(string msg) { if (Log != null) Log.LogDebug(msg); }

        /// <summary>
        /// Запускает корутину на объекте плагина. Нужна статическим помощникам
        /// (например, ItemBridge), чтобы выносить тяжёлую работу из OnGUI
        /// в фоновые кадры. Возвращает false, если плагин ещё не создан.
        /// </summary>
        internal static bool Run(IEnumerator routine)
        {
            if (routine == null) return false;

            RosarySharePlugin instance = Instance;
            if (instance == null || !instance.isActiveAndEnabled) return false;

            try
            {
                instance.StartCoroutine(routine);
                return true;
            }
            catch (Exception e)
            {
                LogDebug("Could not start background routine: " + e.Message);
                return false;
            }
        }

        private void Awake()
        {
            Instance = this;

            ModConfig.Bind(Config);
            Texts.Reload();

            gameObject.AddComponent<ToastLog>();
            gameObject.AddComponent<TransferManager>();
            gameObject.AddComponent<TransferWindow>();

            LogInfo(string.Format("{0} {1} loaded. Menu key: {2}. Gamepad combo: {3}.",
                PluginName, PluginVersion, ModConfig.MenuKeyCode,
                ModConfig.MenuCombo != null ? ModConfig.MenuCombo.Text : "none"));

            if (XvXBridge.IsMultiplayerPresent())
                LogInfo("Multiplayer mod (SilksongMultiplayer) detected.");
            else
                LogWarning("Multiplayer mod is not detected yet. RosaryShare will activate once you host/join a Steam lobby.");
        }
    }
}

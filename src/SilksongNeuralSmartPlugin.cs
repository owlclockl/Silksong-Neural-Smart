using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using SilksongNeuralSmart.Config;
using SilksongNeuralSmart.Patches;
using SilksongNeuralSmart.Training;
using SilksongNeuralSmart.UI;

namespace SilksongNeuralSmart
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class SilksongNeuralSmartPlugin : BaseUnityPlugin
    {
        public static SilksongNeuralSmartPlugin Instance { get; private set; } = null!;
        public ModConfig ModConfiguration { get; private set; } = new ModConfig();
        
        private Harmony? _harmony;
        private GameObject? _trainingRoot;
        private TrainingArenaManager? _arenaManager;
        private TrainingHUD? _hud;

        private void Awake()
        {
            Instance = this;

            // Bind configuration
            ModConfiguration.Bind(Config);

            Logger.LogInfo($"[SilksongNeuralSmart] {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} initialized.");

            // Apply Harmony Patches
            try
            {
                _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                _harmony.PatchAll();
                EnemyFSMOverridePatch.PatchAll(_harmony);
                Logger.LogInfo("[SilksongNeuralSmart] Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[SilksongNeuralSmart] Harmony patch failure: {ex.Message}");
            }

            // Initialize Training Arena Infrastructure
            InitTrainingInfrastructure();
        }

        private void InitTrainingInfrastructure()
        {
            _trainingRoot = new GameObject("SilksongNeuralSmart_TrainingRoot");
            DontDestroyOnLoad(_trainingRoot);

            _arenaManager = _trainingRoot.AddComponent<TrainingArenaManager>();
            _hud = _trainingRoot.AddComponent<TrainingHUD>();

            // Setup initial config values
            _arenaManager.HornetTrainer.MutationRate = ModConfiguration.MutationRate.Value;
            _arenaManager.HornetTrainer.MutationStrength = ModConfiguration.MutationStrength.Value;
            _arenaManager.MobTrainer.MutationRate = ModConfiguration.MutationRate.Value;
            _arenaManager.MobTrainer.MutationStrength = ModConfiguration.MutationStrength.Value;

            if (ModConfiguration.EnableTrainingMode.Value)
            {
                _arenaManager.StartTraining();
            }
        }

        private void Update()
        {
            // Hotkey handling
            if (Input.GetKeyDown(ModConfiguration.ToggleTrainingMenuKey.Value))
            {
                if (_hud != null)
                {
                    _hud.Visible = !_hud.Visible;
                    Logger.LogInfo($"[SilksongNeuralSmart] Training HUD toggled: {_hud.Visible}");
                }
            }

            if (Input.GetKeyDown(ModConfiguration.ToggleHornetAIKey.Value))
            {
                HeroControllerPatch.ToggleHornetAI();
            }

            if (Input.GetKeyDown(ModConfiguration.ResetEpisodeKey.Value))
            {
                _arenaManager?.ResetEpisode();
            }

            if (Input.GetKeyDown(ModConfiguration.SpeedUpKey.Value))
            {
                if (_arenaManager != null)
                {
                    _arenaManager.SetTimeScale(_arenaManager.TimeScale * 1.5f);
                    Logger.LogInfo($"[SilksongNeuralSmart] Speed increased to {_arenaManager.TimeScale:F1}x");
                }
            }

            if (Input.GetKeyDown(ModConfiguration.SlowDownKey.Value))
            {
                if (_arenaManager != null)
                {
                    _arenaManager.SetTimeScale(Mathf.Max(0.5f, _arenaManager.TimeScale / 1.5f));
                    Logger.LogInfo($"[SilksongNeuralSmart] Speed decreased to {_arenaManager.TimeScale:F1}x");
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (_trainingRoot != null)
            {
                Destroy(_trainingRoot);
            }
        }
    }
}

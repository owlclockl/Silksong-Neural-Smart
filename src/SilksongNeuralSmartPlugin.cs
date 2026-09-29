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

        // Отдельный режим "Великая Арена" (кнопка в главном меню + свои сохранения)
        private GrandArenaMode? _grandArena;
        private GrandArenaHUD? _grandArenaHud;
        private MainMenuInjector? _mainMenuInjector;

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
            InitGrandArenaMode();
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

        /// <summary>
        /// Регистрирует отдельный режим "Великая Арена": движок режима, его HUD
        /// и кнопку запуска в главном меню игры.
        /// </summary>
        private void InitGrandArenaMode()
        {
            if (_trainingRoot == null) return;

            _grandArena = _trainingRoot.AddComponent<GrandArenaMode>();
            _grandArenaHud = _trainingRoot.AddComponent<GrandArenaHUD>();
            _mainMenuInjector = _trainingRoot.AddComponent<MainMenuInjector>();

            // Параметры режима из конфигурации
            _grandArena.BaseMobCount = Mathf.Clamp(ModConfiguration.GrandArenaMobCount.Value, GrandArenaMode.MIN_MOBS, GrandArenaMode.MAX_MOBS);
            _grandArena.MobCount = _grandArena.BaseMobCount;
            _grandArena.MaxEpisodeDuration = Mathf.Clamp(ModConfiguration.GrandArenaEpisodeDuration.Value, 10f, 300f);
            _grandArena.AutoSaveEveryEpisodes = Mathf.Max(0, ModConfiguration.GrandArenaAutoSaveEpisodes.Value);
            _grandArena.WaveScaling = ModConfiguration.GrandArenaWaveScaling.Value;
            _grandArena.BalancePackDamage = ModConfiguration.GrandArenaBalancePackDamage.Value;
            _grandArena.TimeScale = Mathf.Clamp(ModConfiguration.GrandArenaTimeScale.Value, 0.25f, 50f);
            _grandArena.ReactionDelayMs = ModConfiguration.AIReactionDelayMs.Value;
            _grandArena.ErrorMarginRate = ModConfiguration.AIErrorMarginRate.Value;

            foreach (var key in GrandArenaMode.ArchetypeKeys)
            {
                if (_grandArena.MobTrainers.TryGetValue(key, out var trainer))
                {
                    trainer.MutationRate = ModConfiguration.MutationRate.Value;
                    trainer.MutationStrength = ModConfiguration.MutationStrength.Value;
                }
            }
            _grandArena.HornetTrainer.MutationRate = ModConfiguration.MutationRate.Value;
            _grandArena.HornetTrainer.MutationStrength = ModConfiguration.MutationStrength.Value;

            // Кнопка в главном меню
            _mainMenuInjector.ButtonEnabled = ModConfiguration.EnableMainMenuButton.Value;
            _mainMenuInjector.ForceAlwaysVisible = ModConfiguration.AlwaysShowMainMenuButton.Value;
            _mainMenuInjector.MenuSceneKeywords = ParseKeywords(ModConfiguration.MainMenuSceneKeywords.Value);

            Logger.LogInfo("[SilksongNeuralSmart] Режим 'Великая Арена' зарегистрирован: кнопка в главном меню активна.");
        }

        private static string[] ParseKeywords(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return new[] { "menu", "title" };

            string[] parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();

            return parts;
        }

        private void Update()
        {
            bool grandArenaActive = _grandArena != null && _grandArena.IsActive;

            // --- Горячие клавиши отдельного режима ---
            if (Input.GetKeyDown(ModConfiguration.GrandArenaMenuKey.Value))
            {
                if (grandArenaActive)
                {
                    if (_grandArenaHud != null)
                        _grandArenaHud.Visible = !_grandArenaHud.Visible;
                }
                else
                {
                    _mainMenuInjector?.TogglePanel();
                }
            }

            if (Input.GetKeyDown(ModConfiguration.GrandArenaQuickSaveKey.Value) && grandArenaActive)
            {
                _grandArena?.SaveCurrentSlot();
            }

            // --- Классическое додзё ---
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
                if (grandArenaActive && _grandArena != null)
                {
                    _grandArena.PlayerControlsHornet = !_grandArena.PlayerControlsHornet;
                    _grandArena.ShowStatus(_grandArena.PlayerControlsHornet
                        ? "Хорнет под управлением игрока"
                        : "Хорнет под управлением нейросети");
                }
                else
                {
                    HeroControllerPatch.ToggleHornetAI();
                }
            }

            if (Input.GetKeyDown(ModConfiguration.ResetEpisodeKey.Value))
            {
                if (grandArenaActive) _grandArena?.ResetEpisode();
                else _arenaManager?.ResetEpisode();
            }

            if (Input.GetKeyDown(ModConfiguration.SpeedUpKey.Value))
            {
                if (grandArenaActive && _grandArena != null)
                {
                    _grandArena.SetTimeScale(_grandArena.TimeScale * 1.5f);
                    Logger.LogInfo($"[SilksongNeuralSmart] Grand Arena speed: {_grandArena.TimeScale:F1}x");
                }
                else if (_arenaManager != null)
                {
                    _arenaManager.SetTimeScale(_arenaManager.TimeScale * 1.5f);
                    Logger.LogInfo($"[SilksongNeuralSmart] Speed increased to {_arenaManager.TimeScale:F1}x");
                }
            }

            if (Input.GetKeyDown(ModConfiguration.SlowDownKey.Value))
            {
                if (grandArenaActive && _grandArena != null)
                {
                    _grandArena.SetTimeScale(Mathf.Max(0.25f, _grandArena.TimeScale / 1.5f));
                    Logger.LogInfo($"[SilksongNeuralSmart] Grand Arena speed: {_grandArena.TimeScale:F1}x");
                }
                else if (_arenaManager != null)
                {
                    _arenaManager.SetTimeScale(Mathf.Max(0.5f, _arenaManager.TimeScale / 1.5f));
                    Logger.LogInfo($"[SilksongNeuralSmart] Speed decreased to {_arenaManager.TimeScale:F1}x");
                }
            }
        }

        private void OnDestroy()
        {
            if (_grandArena != null && _grandArena.IsActive)
            {
                _grandArena.StopMode(true);
            }

            _harmony?.UnpatchSelf();
            if (_trainingRoot != null)
            {
                Destroy(_trainingRoot);
            }
        }
    }
}

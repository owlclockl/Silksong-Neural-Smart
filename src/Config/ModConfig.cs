using System;
using BepInEx.Configuration;
using UnityEngine;

namespace SilksongNeuralSmart.Config
{
    public class ModConfig
    {
        // General AI
        public ConfigEntry<bool> EnableNeuralAI { get; private set; } = null!;
        public ConfigEntry<bool> EnableBalancingSystem { get; private set; } = null!;
        public ConfigEntry<float> AIReactionDelayMs { get; private set; } = null!;
        public ConfigEntry<float> AIErrorMarginRate { get; private set; } = null!;
        public ConfigEntry<float> DifficultyMultiplier { get; private set; } = null!;
        
        // Training Mode
        public ConfigEntry<bool> EnableTrainingMode { get; private set; } = null!;
        public ConfigEntry<KeyCode> ToggleTrainingMenuKey { get; private set; } = null!;
        public ConfigEntry<KeyCode> ToggleHornetAIKey { get; private set; } = null!;
        public ConfigEntry<KeyCode> ResetEpisodeKey { get; private set; } = null!;
        public ConfigEntry<KeyCode> SpeedUpKey { get; private set; } = null!;
        public ConfigEntry<KeyCode> SlowDownKey { get; private set; } = null!;
        public ConfigEntry<int> DefaultTrainingRoom { get; private set; } = null!;
        public ConfigEntry<int> PopulationSize { get; private set; } = null!;
        public ConfigEntry<float> MutationRate { get; private set; } = null!;
        public ConfigEntry<float> MutationStrength { get; private set; } = null!;
        public ConfigEntry<float> TimeScaleMultiplier { get; private set; } = null!;

        // Grand Arena (отдельный режим: мобы + Хорнет на одной большой арене)
        public ConfigEntry<bool> EnableMainMenuButton { get; private set; } = null!;
        public ConfigEntry<bool> AlwaysShowMainMenuButton { get; private set; } = null!;
        public ConfigEntry<string> MainMenuSceneKeywords { get; private set; } = null!;
        public ConfigEntry<KeyCode> GrandArenaMenuKey { get; private set; } = null!;
        public ConfigEntry<KeyCode> GrandArenaQuickSaveKey { get; private set; } = null!;
        public ConfigEntry<int> GrandArenaDefaultSlot { get; private set; } = null!;
        public ConfigEntry<int> GrandArenaMobCount { get; private set; } = null!;
        public ConfigEntry<float> GrandArenaEpisodeDuration { get; private set; } = null!;
        public ConfigEntry<int> GrandArenaAutoSaveEpisodes { get; private set; } = null!;
        public ConfigEntry<bool> GrandArenaWaveScaling { get; private set; } = null!;
        public ConfigEntry<bool> GrandArenaBalancePackDamage { get; private set; } = null!;
        public ConfigEntry<float> GrandArenaTimeScale { get; private set; } = null!;

        // Visuals & HUD
        public ConfigEntry<bool> ShowTrainingHUD { get; private set; } = null!;
        public ConfigEntry<bool> ShowNeuralVisualizer { get; private set; } = null!;
        public ConfigEntry<bool> ShowSensoryRaycasts { get; private set; } = null!;
        public ConfigEntry<bool> ShowActionProbabilities { get; private set; } = null!;

        public void Bind(ConfigFile config)
        {
            // General
            EnableNeuralAI = config.Bind("General", "EnableNeuralAI", true, "Enable custom neural network controller for all enemies.");
            EnableBalancingSystem = config.Bind("General", "EnableBalancingSystem", true, "Enforce human-like reaction time, telegraphs, and fatigue to prevent unfair bot behavior.");
            AIReactionDelayMs = config.Bind("Balancing", "AIReactionDelayMs", 110.0f, "Reaction delay buffer in milliseconds (60ms = pro, 120ms = human, 220ms = casual).");
            AIErrorMarginRate = config.Bind("Balancing", "AIErrorMarginRate", 0.05f, "Probability (0.0 to 1.0) of neural AI making a minor tactical miscalculation.");
            DifficultyMultiplier = config.Bind("Balancing", "DifficultyMultiplier", 1.0f, "Global difficulty and aggression scaling.");

            // Training
            EnableTrainingMode = config.Bind("Training", "EnableTrainingMode", false, "Enable the standalone Silk Dojo Neural Training Mode.");
            ToggleTrainingMenuKey = config.Bind("Training", "ToggleTrainingMenuKey", KeyCode.F7, "Key to open/close the Neural Training HUD.");
            ToggleHornetAIKey = config.Bind("Training", "ToggleHornetAIKey", KeyCode.F8, "Key to toggle autonomous AI control for Hornet.");
            ResetEpisodeKey = config.Bind("Training", "ResetEpisodeKey", KeyCode.F9, "Key to manually reset current training episode.");
            SpeedUpKey = config.Bind("Training", "SpeedUpKey", KeyCode.PageUp, "Increase simulation speed for faster learning.");
            SlowDownKey = config.Bind("Training", "SlowDownKey", KeyCode.PageDown, "Decrease simulation speed.");
            DefaultTrainingRoom = config.Bind("Training", "DefaultTrainingRoom", 1, "Initial room (1: Moss Dojo, 2: Vertical Chasm, 3: Citadel Hazards).");
            PopulationSize = config.Bind("Training", "PopulationSize", 24, "Genetic population size for neuroevolution.");
            MutationRate = config.Bind("Training", "MutationRate", 0.08f, "Probability of weight mutations per generation.");
            MutationStrength = config.Bind("Training", "MutationStrength", 0.25f, "Magnitude of weight mutations.");
            TimeScaleMultiplier = config.Bind("Training", "TimeScaleMultiplier", 1.0f, "Current training time scale.");

            // Grand Arena — отдельный режим с кнопкой в главном меню
            EnableMainMenuButton = config.Bind("GrandArena", "EnableMainMenuButton", true,
                "Показывать кнопку 'НЕЙРО-АРЕНА' в главном меню игры (запуск отдельного режима обучения).");
            AlwaysShowMainMenuButton = config.Bind("GrandArena", "AlwaysShowMainMenuButton", false,
                "Показывать кнопку режима всегда, а не только в главном меню (полезно, если название сцены меню не распознано).");
            MainMenuSceneKeywords = config.Bind("GrandArena", "MainMenuSceneKeywords", "menu,title,start,intro,logo,quit",
                "Ключевые слова названий сцен главного меню (через запятую) для показа кнопки режима.");
            GrandArenaMenuKey = config.Bind("GrandArena", "GrandArenaMenuKey", KeyCode.F6,
                "Клавиша открытия меню режима 'Великая Арена' / показа-скрытия его интерфейса.");
            GrandArenaQuickSaveKey = config.Bind("GrandArena", "GrandArenaQuickSaveKey", KeyCode.F5,
                "Клавиша быстрого сохранения прогресса режима в активный слот.");
            GrandArenaDefaultSlot = config.Bind("GrandArena", "GrandArenaDefaultSlot", 1,
                "Слот сохранения режима по умолчанию (1-3).");
            GrandArenaMobCount = config.Bind("GrandArena", "GrandArenaMobCount", 4,
                "Сколько мобов обучается одновременно вместе с Хорнет на большой арене (1-8).");
            GrandArenaEpisodeDuration = config.Bind("GrandArena", "GrandArenaEpisodeDuration", 60f,
                "Максимальная длительность одного раунда обучения в режиме 'Великая Арена', секунды.");
            GrandArenaAutoSaveEpisodes = config.Bind("GrandArena", "GrandArenaAutoSaveEpisodes", 5,
                "Автосохранение слота каждые N эпизодов (0 — выключить автосохранение).");
            GrandArenaWaveScaling = config.Bind("GrandArena", "GrandArenaWaveScaling", true,
                "Увеличивать стаю мобов по мере побед Хорнет (система волн).");
            GrandArenaBalancePackDamage = config.Bind("GrandArena", "GrandArenaBalancePackDamage", true,
                "Честный баланс: суммарный урон стаи масштабируется по её численности.");
            GrandArenaTimeScale = config.Bind("GrandArena", "GrandArenaTimeScale", 1.0f,
                "Стартовое ускорение симуляции в режиме 'Великая Арена' (1-50).");

            // Visuals
            ShowTrainingHUD = config.Bind("Visuals", "ShowTrainingHUD", true, "Show the training statistics and control HUD.");
            ShowNeuralVisualizer = config.Bind("Visuals", "ShowNeuralVisualizer", true, "Render live neural network node activations.");
            ShowSensoryRaycasts = config.Bind("Visuals", "ShowSensoryRaycasts", true, "Draw in-game sensory raycasts and target vectors.");
            ShowActionProbabilities = config.Bind("Visuals", "ShowActionProbabilities", true, "Show probability bars for AI actions.");
        }
    }
}

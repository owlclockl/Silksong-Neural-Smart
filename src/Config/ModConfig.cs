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

            // Visuals
            ShowTrainingHUD = config.Bind("Visuals", "ShowTrainingHUD", true, "Show the training statistics and control HUD.");
            ShowNeuralVisualizer = config.Bind("Visuals", "ShowNeuralVisualizer", true, "Render live neural network node activations.");
            ShowSensoryRaycasts = config.Bind("Visuals", "ShowSensoryRaycasts", true, "Draw in-game sensory raycasts and target vectors.");
            ShowActionProbabilities = config.Bind("Visuals", "ShowActionProbabilities", true, "Show probability bars for AI actions.");
        }
    }
}

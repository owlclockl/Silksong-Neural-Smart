using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SilksongNeuralSmart.Agents;
using SilksongNeuralSmart.Agents.Archetypes;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.Training
{
    public class TrainingArenaManager : MonoBehaviour
    {
        public static TrainingArenaManager Instance { get; private set; } = null!;

        public bool IsTrainingActive { get; set; } = false;
        public int CurrentRoomId { get; private set; } = 1;
        public TrainingRoom ActiveRoom { get; private set; } = null!;

        public HornetAgent Hornet { get; private set; } = null!;
        public MobAgent ActiveMob { get; private set; } = null!;
        public string ActiveMobType { get; private set; } = "Grunt";

        // Trainers
        public GeneticTrainer HornetTrainer { get; private set; } = null!;
        public GeneticTrainer MobTrainer { get; private set; } = null!;

        // Episode Metrics
        public int TotalEpisodes { get; private set; }
        public int HornetWins { get; private set; }
        public int MobWins { get; private set; }
        public int Draws { get; private set; }
        public float EpisodeTimer { get; private set; }
        public float MaxEpisodeDuration { get; set; } = 45f; // seconds
        public float TimeScale { get; set; } = 1.0f;

        public float HornetWinRate => TotalEpisodes > 0 ? (float)HornetWins / TotalEpisodes * 100f : 0f;
        public float MobWinRate => TotalEpisodes > 0 ? (float)MobWins / TotalEpisodes * 100f : 0f;

        public List<float> HornetFitnessLog { get; } = new List<float>();
        public List<float> MobFitnessLog { get; } = new List<float>();

        private int _totalDecisionsThisSec;
        private float _decisionFpsTimer;
        public int DecisionsPerSecond { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            int[] brainShape = new int[] { ObservationSensor.INPUT_VECTOR_SIZE, 32, 32, ActionDecoders.OUTPUT_ACTION_COUNT };
            HornetTrainer = new GeneticTrainer(24, brainShape);
            MobTrainer = new GeneticTrainer(24, brainShape);

            LoadRoom(1);
        }

        public void LoadRoom(int roomId)
        {
            CurrentRoomId = roomId;
            ActiveRoom = RoomLayouts.CreateRoom(roomId);
            ResetEpisode();
        }

        public void SetMobArchetype(string archetype)
        {
            ActiveMobType = archetype;
            if (ActiveMob != null)
            {
                Destroy(ActiveMob.gameObject);
            }

            var mobObj = new GameObject("NeuralMob_" + archetype);
            mobObj.transform.parent = transform;

            switch (archetype.ToLowerInvariant())
            {
                case "flying":
                case "hunter":
                    ActiveMob = mobObj.AddComponent<FlyingHunterAgent>();
                    break;
                case "knight":
                case "shield":
                    ActiveMob = mobObj.AddComponent<ShieldKnightAgent>();
                    break;
                case "assassin":
                case "weaver":
                    ActiveMob = mobObj.AddComponent<AssassinWeaverAgent>();
                    break;
                default:
                    ActiveMob = mobObj.AddComponent<ScoutGruntAgent>();
                    break;
            }

            ActiveMob.Brain = MobTrainer.GetCurrentGenome().Brain;
            if (Hornet != null)
            {
                ActiveMob.SetTarget(Hornet.transform);
                Hornet.TargetEnemy = ActiveMob.transform;
            }

            ResetEpisode();
        }

        public void StartTraining()
        {
            IsTrainingActive = true;
            Time.timeScale = TimeScale;
            if (Hornet == null)
            {
                var hornetObj = new GameObject("NeuralHornet");
                hornetObj.transform.parent = transform;
                Hornet = hornetObj.AddComponent<HornetAgent>();
            }

            if (ActiveMob == null)
            {
                SetMobArchetype("Grunt");
            }

            ResetEpisode();
        }

        public void PauseTraining()
        {
            IsTrainingActive = false;
            Time.timeScale = 1.0f;
        }

        public void SetTimeScale(float scale)
        {
            TimeScale = Mathf.Clamp(scale, 0.2f, 50.0f);
            if (IsTrainingActive)
            {
                Time.timeScale = TimeScale;
            }
        }

        private void Update()
        {
            if (!IsTrainingActive || Hornet == null || ActiveMob == null)
                return;

            float dt = Time.deltaTime;
            EpisodeTimer += dt;

            // Step decisions
            Hornet.StepDecision(dt);
            ActiveMob.StepDecision(dt);

            _totalDecisionsThisSec += 2;
            _decisionFpsTimer += Time.unscaledDeltaTime;
            if (_decisionFpsTimer >= 1.0f)
            {
                DecisionsPerSecond = _totalDecisionsThisSec;
                _totalDecisionsThisSec = 0;
                _decisionFpsTimer = 0f;
            }

            // Check combat hitboxes / interactions
            SimulateCombatInteractions();

            // Check episode completion condition
            bool hornetDead = Hornet.Health <= 0f;
            bool mobDead = ActiveMob.Health <= 0f;
            bool timeout = EpisodeTimer >= MaxEpisodeDuration;

            if (hornetDead || mobDead || timeout)
            {
                FinishEpisode(hornetDead, mobDead, timeout);
            }
        }

        private void SimulateCombatInteractions()
        {
            float dist = Vector2.Distance(Hornet.transform.position, ActiveMob.transform.position);

            // Hornet attacking Mob
            if (Hornet.IsAttacking && dist < 3.2f)
            {
                float dmg = Hornet.IsPogoing ? 32f : 24f;
                ActiveMob.TakeDamage(dmg * Time.deltaTime * 3f);
                Hornet.OnAttackLanded(dmg * Time.deltaTime * 3f, Hornet.IsPogoing);
            }

            // Mob attacking Hornet
            if (ActiveMob.IsAttacking && dist < 3.0f)
            {
                if (!Hornet.IsDashing) // Hornet invulnerable while dashing
                {
                    float dmg = 20f;
                    Hornet.TakeDamage(dmg * Time.deltaTime * 3f);
                    ActiveMob.OnAttackLanded(dmg * Time.deltaTime * 3f);
                }
                else
                {
                    Hornet.SuccessfulDodges++;
                }
            }
        }

        public void FinishEpisode(bool hornetDead, bool mobDead, bool timeout)
        {
            TotalEpisodes++;

            bool hornetWon = mobDead && !hornetDead;
            bool mobWon = hornetDead && !mobDead;

            if (hornetWon) HornetWins++;
            else if (mobWon) MobWins++;
            else Draws++;

            // Evaluate fitness
            float hornetFit = FitnessFunctions.CalculateHornetFitness(Hornet, ActiveMob, EpisodeTimer, hornetWon);
            float mobFit = FitnessFunctions.CalculateMobFitness(ActiveMob, Hornet, EpisodeTimer, mobWon);

            HornetTrainer.GetCurrentGenome().Fitness = hornetFit;
            MobTrainer.GetCurrentGenome().Fitness = mobFit;

            HornetFitnessLog.Add(hornetFit);
            MobFitnessLog.Add(mobFit);

            // Advance genomes & generations
            HornetTrainer.AdvanceToNextGenome();
            MobTrainer.AdvanceToNextGenome();

            ResetEpisode();
        }

        public void ResetEpisode()
        {
            EpisodeTimer = 0f;

            if (ActiveRoom == null)
                ActiveRoom = RoomLayouts.CreateRoom(CurrentRoomId);

            if (Hornet != null)
            {
                Hornet.Brain = HornetTrainer.GetCurrentGenome().Brain;
                Hornet.ResetAgent(ActiveRoom.HornetSpawnPoint);
            }

            if (ActiveMob != null)
            {
                ActiveMob.Brain = MobTrainer.GetCurrentGenome().Brain;
                ActiveMob.ResetAgent(ActiveRoom.MobSpawnPoint);
            }
        }

        public void SaveBrains(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                string hornetJson = HornetTrainer.BestGenomeEver?.Brain.ToJson() ?? HornetTrainer.GetCurrentGenome().Brain.ToJson();
                string mobJson = MobTrainer.BestGenomeEver?.Brain.ToJson() ?? MobTrainer.GetCurrentGenome().Brain.ToJson();

                File.WriteAllText(Path.Combine(directoryPath, "hornet_trained.brain.json"), hornetJson);
                File.WriteAllText(Path.Combine(directoryPath, $"mob_{ActiveMobType.ToLower()}_trained.brain.json"), mobJson);
                Debug.Log("[SilksongNeuralSmart] Brain models saved successfully to " + directoryPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[SilksongNeuralSmart] Error saving brains: " + ex.Message);
            }
        }

        public void LoadBrain(string filePath, bool isHornet)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    NeuralNetwork loaded = NeuralNetwork.FromJson(json);
                    if (isHornet && Hornet != null)
                    {
                        HornetTrainer.SetBestBrain(loaded);
                        Hornet.Brain = loaded;
                    }
                    else if (!isHornet && ActiveMob != null)
                    {
                        MobTrainer.SetBestBrain(loaded);
                        ActiveMob.Brain = loaded;
                    }
                    Debug.Log($"[SilksongNeuralSmart] Successfully loaded brain from {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[SilksongNeuralSmart] Failed to load brain: " + ex.Message);
            }
        }
    }
}

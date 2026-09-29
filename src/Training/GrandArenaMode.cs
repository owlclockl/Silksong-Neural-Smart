using System;
using System.Collections.Generic;
using UnityEngine;
using SilksongNeuralSmart.Agents;
using SilksongNeuralSmart.Agents.Archetypes;
using SilksongNeuralSmart.Core;
using SilksongNeuralSmart.Saves;

namespace SilksongNeuralSmart.Training
{
    public enum GrandArenaState
    {
        Inactive = 0,
        Running = 1,
        Paused = 2
    }

    /// <summary>
    /// Один боец стаи: агент + его геном + собственное тело в физике арены.
    /// </summary>
    public class PackMember
    {
        public MobAgent Agent = null!;
        public string ArchetypeKey = "grunt";
        public int GenomeOffset;
        public ArenaBody Body;
        public Color Tint = Color.green;
        public bool WasCountedDead;

        public bool Alive => Agent != null && Agent.Health > 0f;
    }

    /// <summary>
    /// ОТДЕЛЬНЫЙ РЕЖИМ "ВЕЛИКАЯ АРЕНА".
    ///
    /// Запускается кнопкой из главного меню игры и представляет собой самостоятельный
    /// игровой режим со своими сохранениями (слотами): на одной огромной арене
    /// одновременно обучаются ВСЕ архетипы мобов и Хорнет.
    ///
    /// Режим полностью автономен: он не зависит от сцен и физики игры,
    /// поэтому работает прямо из главного меню.
    /// </summary>
    public class GrandArenaMode : MonoBehaviour
    {
        public static GrandArenaMode Instance { get; private set; } = null!;

        public const int MAX_MOBS = 8;
        public const int MIN_MOBS = 1;

        public static readonly string[] ArchetypeKeys = { "grunt", "flying", "knight", "assassin" };
        public static readonly string[] ArchetypeTitles = { "Разведчик Мха", "Летающий Охотник", "Рыцарь Цитадели", "Ассасин-Ткач" };

        // ------------------------------------------------------------------
        // Состояние режима
        // ------------------------------------------------------------------
        public GrandArenaState State { get; private set; } = GrandArenaState.Inactive;
        public bool IsActive => State != GrandArenaState.Inactive;
        public bool IsRunning => State == GrandArenaState.Running;

        public int ActiveSlot { get; private set; } = 1;
        public string ProfileName { get; set; } = "Великая Арена";

        public TrainingRoom Arena { get; private set; } = null!;
        public HornetAgent Hornet { get; private set; } = null!;
        public List<PackMember> Pack { get; } = new List<PackMember>();

        public GeneticTrainer HornetTrainer { get; private set; } = null!;
        public Dictionary<string, GeneticTrainer> MobTrainers { get; } = new Dictionary<string, GeneticTrainer>();

        // ------------------------------------------------------------------
        // Настройки режима (сохраняются в слот)
        // ------------------------------------------------------------------
        public int BaseMobCount = 4;
        public int MobCount = 4;
        public float MaxEpisodeDuration = 60f;
        public float TimeScale = 1.0f;
        public bool WaveScaling = true;
        public bool BalancePackDamage = true;
        public bool PlayerControlsHornet;
        public int AutoSaveEveryEpisodes = 5;
        public float ReactionDelayMs = 110f;
        public float ErrorMarginRate = 0.05f;

        // ------------------------------------------------------------------
        // Статистика прогресса (сохраняется в слот)
        // ------------------------------------------------------------------
        public int Wave = 1;
        public int BestWave = 1;
        public int TotalEpisodes;
        public int HornetWins;
        public int PackWins;
        public int Draws;
        public int TotalMobsDefeated;
        public float PlayTimeSeconds;

        public List<float> HornetFitnessLog { get; } = new List<float>();
        public List<float> PackFitnessLog { get; } = new List<float>();

        // ------------------------------------------------------------------
        // Телеметрия текущего эпизода
        // ------------------------------------------------------------------
        public float EpisodeTimer { get; private set; }
        public int DecisionsPerSecond { get; private set; }
        public float LastHornetFitness { get; private set; }
        public float LastPackFitness { get; private set; }
        public string StatusMessage { get; set; } = "";
        public float StatusMessageTime { get; private set; }

        private ArenaBody _hornetBody;
        private int _decisionCounter;
        private float _decisionTimer;
        private int _episodesSinceSave;
        private float _savedGameTimeScale = 1f;

        public int AliveMobCount
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < Pack.Count; i++)
                    if (Pack[i].Alive) alive++;
                return alive;
            }
        }

        // ==================================================================
        // Жизненный цикл
        // ==================================================================
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            int[] brainShape = { ObservationSensor.INPUT_VECTOR_SIZE, 32, 32, ActionDecoders.OUTPUT_ACTION_COUNT };
            HornetTrainer = new GeneticTrainer(24, brainShape);

            for (int i = 0; i < ArchetypeKeys.Length; i++)
            {
                MobTrainers[ArchetypeKeys[i]] = new GeneticTrainer(24, brainShape);
            }

            Arena = RoomLayouts.CreateRoom(RoomLayouts.GRAND_ARENA_ROOM_ID);
        }

        /// <summary>Запуск режима из главного меню (кнопка "НЕЙРО-АРЕНА").</summary>
        public void StartMode(int slot, bool newProfile)
        {
            ActiveSlot = Mathf.Clamp(slot, 1, GrandArenaSaveSystem.SLOT_COUNT);

            // Классическое додзё ставим на паузу — режимы не должны конфликтовать
            if (TrainingArenaManager.Instance != null && TrainingArenaManager.Instance.IsTrainingActive)
            {
                TrainingArenaManager.Instance.PauseTraining();
            }

            if (newProfile)
            {
                ResetProgress();
                GrandArenaSaveSystem.DeleteSlot(ActiveSlot);
                ProfileName = "Слот " + ActiveSlot;
            }
            else if (GrandArenaSaveSystem.SlotExists(ActiveSlot))
            {
                GrandArenaSaveSystem.Load(ActiveSlot, this);
            }
            else
            {
                ResetProgress();
                ProfileName = "Слот " + ActiveSlot;
            }

            Arena = RoomLayouts.CreateRoom(RoomLayouts.GRAND_ARENA_ROOM_ID);
            EnsureHornet();
            RebuildPack();
            ResetEpisode();

            _savedGameTimeScale = Time.timeScale;
            State = GrandArenaState.Running;
            Time.timeScale = TimeScale;

            ShowStatus(newProfile
                ? $"Новая тренировка начата (слот {ActiveSlot})"
                : $"Загружен слот {ActiveSlot}: поколение {HornetTrainer.Generation}, волна {Wave}");

            Debug.Log($"[SilksongNeuralSmart] ВЕЛИКАЯ АРЕНА запущена. Слот {ActiveSlot}, мобов: {MobCount}, волна {Wave}.");
        }

        /// <summary>Выход из режима (с автосохранением прогресса).</summary>
        public void StopMode(bool save = true)
        {
            if (!IsActive) return;

            if (save)
            {
                GrandArenaSaveSystem.Save(ActiveSlot, this);
            }

            State = GrandArenaState.Inactive;
            Time.timeScale = _savedGameTimeScale <= 0f ? 1f : _savedGameTimeScale;
            Debug.Log("[SilksongNeuralSmart] ВЕЛИКАЯ АРЕНА остановлена.");
        }

        public void TogglePause()
        {
            if (State == GrandArenaState.Running)
            {
                State = GrandArenaState.Paused;
                Time.timeScale = 1f;
                ShowStatus("Пауза");
            }
            else if (State == GrandArenaState.Paused)
            {
                State = GrandArenaState.Running;
                Time.timeScale = TimeScale;
                ShowStatus("Продолжаем обучение");
            }
        }

        public void SetTimeScale(float scale)
        {
            TimeScale = Mathf.Clamp(scale, 0.25f, 50f);
            if (State == GrandArenaState.Running)
                Time.timeScale = TimeScale;
        }

        public void SetMobCount(int count)
        {
            BaseMobCount = Mathf.Clamp(count, MIN_MOBS, MAX_MOBS);
            MobCount = Mathf.Clamp(count, MIN_MOBS, MAX_MOBS);
            RebuildPack();
            ResetEpisode();
        }

        public void ResetProgress()
        {
            Wave = 1;
            BestWave = 1;
            TotalEpisodes = 0;
            HornetWins = 0;
            PackWins = 0;
            Draws = 0;
            TotalMobsDefeated = 0;
            PlayTimeSeconds = 0f;
            MobCount = BaseMobCount;
            HornetFitnessLog.Clear();
            PackFitnessLog.Clear();

            int[] brainShape = { ObservationSensor.INPUT_VECTOR_SIZE, 32, 32, ActionDecoders.OUTPUT_ACTION_COUNT };
            HornetTrainer = new GeneticTrainer(24, brainShape);
            for (int i = 0; i < ArchetypeKeys.Length; i++)
                MobTrainers[ArchetypeKeys[i]] = new GeneticTrainer(24, brainShape);
        }

        // ==================================================================
        // Построение арены
        // ==================================================================
        private void EnsureHornet()
        {
            if (Hornet == null)
            {
                var go = new GameObject("GrandArena_Hornet");
                go.transform.parent = transform;
                Hornet = go.AddComponent<HornetAgent>();
            }

            Hornet.Balancer.ReactionDelaySeconds = ReactionDelayMs / 1000f;
            Hornet.Balancer.ErrorMarginRate = ErrorMarginRate;
        }

        /// <summary>Пересобирает стаю: архетипы чередуются, каждый получает свой геном.</summary>
        public void RebuildPack()
        {
            for (int i = 0; i < Pack.Count; i++)
            {
                if (Pack[i].Agent != null)
                    Destroy(Pack[i].Agent.gameObject);
            }
            Pack.Clear();

            var perArchetypeIndex = new Dictionary<string, int>();
            for (int i = 0; i < ArchetypeKeys.Length; i++)
                perArchetypeIndex[ArchetypeKeys[i]] = 0;

            for (int i = 0; i < MobCount; i++)
            {
                string key = ArchetypeKeys[i % ArchetypeKeys.Length];
                var go = new GameObject($"GrandArena_Mob_{i}_{key}");
                go.transform.parent = transform;

                MobAgent agent;
                switch (key)
                {
                    case "flying": agent = go.AddComponent<FlyingHunterAgent>(); break;
                    case "knight": agent = go.AddComponent<ShieldKnightAgent>(); break;
                    case "assassin": agent = go.AddComponent<AssassinWeaverAgent>(); break;
                    default: agent = go.AddComponent<ScoutGruntAgent>(); break;
                }

                agent.Balancer.ReactionDelaySeconds = ReactionDelayMs / 1000f;
                agent.Balancer.ErrorMarginRate = ErrorMarginRate;
                if (Hornet != null) agent.SetTarget(Hornet.transform);

                var member = new PackMember
                {
                    Agent = agent,
                    ArchetypeKey = key,
                    GenomeOffset = perArchetypeIndex[key],
                    Tint = GetArchetypeColor(key),
                    Body = ArenaBody.Create(Arena.GetMobSpawn(i), agent.ColliderSize, agent.IsFlyingArchetype)
                };
                perArchetypeIndex[key] = perArchetypeIndex[key] + 1;

                Pack.Add(member);
            }
        }

        public static Color GetArchetypeColor(string key)
        {
            switch (key)
            {
                case "flying": return new Color(0.61f, 0.53f, 1.00f);
                case "knight": return new Color(0.98f, 0.77f, 0.19f);
                case "assassin": return new Color(0.91f, 0.25f, 0.09f);
                default: return new Color(0.30f, 0.82f, 0.22f);
            }
        }

        public static string GetArchetypeTitle(string key)
        {
            for (int i = 0; i < ArchetypeKeys.Length; i++)
                if (ArchetypeKeys[i] == key) return ArchetypeTitles[i];
            return key;
        }

        public void ResetEpisode()
        {
            EpisodeTimer = 0f;

            if (Arena == null)
                Arena = RoomLayouts.CreateRoom(RoomLayouts.GRAND_ARENA_ROOM_ID);

            if (Hornet != null)
            {
                Hornet.Brain = HornetTrainer.GetCurrentGenome().Brain;
                Hornet.ResetAgent(Arena.HornetSpawnPoint);
                _hornetBody = ArenaBody.Create(Arena.HornetSpawnPoint, Hornet.ColliderSize, false);
            }

            for (int i = 0; i < Pack.Count; i++)
            {
                PackMember m = Pack[i];
                GeneticTrainer trainer = MobTrainers[m.ArchetypeKey];
                m.Agent.Brain = trainer.GetGenomeOffset(m.GenomeOffset).Brain;

                Vector2 spawn = Arena.GetMobSpawn(i);
                m.Agent.ResetAgent(spawn);
                m.Body = ArenaBody.Create(spawn, m.Agent.ColliderSize, m.Agent.IsFlyingArchetype);
                m.WasCountedDead = false;
            }
        }

        // ==================================================================
        // Главный цикл симуляции
        // ==================================================================
        private void Update()
        {
            if (State != GrandArenaState.Running || Hornet == null || Pack.Count == 0)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            EpisodeTimer += dt;
            PlayTimeSeconds += Time.unscaledDeltaTime;

            if (StatusMessageTime > 0f)
                StatusMessageTime -= Time.unscaledDeltaTime;

            StepHornet(dt);
            StepPack(dt);
            ResolveCombat(dt);

            _decisionTimer += Time.unscaledDeltaTime;
            if (_decisionTimer >= 1f)
            {
                DecisionsPerSecond = _decisionCounter;
                _decisionCounter = 0;
                _decisionTimer = 0f;
            }

            bool hornetDead = Hornet.Health <= 0f;
            bool packDead = AliveMobCount == 0;
            bool timeout = EpisodeTimer >= MaxEpisodeDuration;

            if (hornetDead || packDead || timeout)
                FinishEpisode(hornetDead, packDead, timeout);
        }

        private void StepHornet(float dt)
        {
            PackMember? target = FindNearestAliveMob();
            if (target != null)
                Hornet.TargetEnemy = target.Agent.transform;

            if (PlayerControlsHornet)
            {
                Hornet.ApplyExternalAction(ReadPlayerInput(), dt);
            }
            else
            {
                SensorData data = BuildHornetSensors(target);
                Hornet.StepDecision(data, dt);
                _decisionCounter++;
            }

            // Собственная физика арены
            _hornetBody.Velocity = Hornet.Velocity;
            ArenaPhysics.Step(ref _hornetBody, Arena, dt, out bool hazard);
            if (hazard)
            {
                Hornet.TakeDamage(ArenaPhysics.HazardDamage * dt * 4f);
            }
            Hornet.SetKinematicState(_hornetBody.Position, _hornetBody.Velocity, _hornetBody.Grounded);
        }

        private void StepPack(float dt)
        {
            for (int i = 0; i < Pack.Count; i++)
            {
                PackMember m = Pack[i];
                if (!m.Alive) continue;

                SensorData data = BuildMobSensors(m);
                m.Agent.StepDecision(data, dt);
                _decisionCounter++;

                m.Body.Velocity = m.Agent.Velocity;
                ArenaBody body = m.Body;
                ArenaPhysics.Step(ref body, Arena, dt, out bool hazard);
                m.Body = body;

                if (hazard)
                    m.Agent.TakeDamage(ArenaPhysics.HazardDamage * dt * 4f);

                m.Agent.SetKinematicState(m.Body.Position, m.Body.Velocity, m.Body.Grounded);
            }
        }

        private DecodedAction ReadPlayerInput()
        {
            var action = new DecodedAction();
            float move = 0f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move -= 1f;

            action.MoveX = move;
            action.JumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.K);
            action.DashPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.L);
            action.AttackPressed = Input.GetKey(KeyCode.J);
            action.DownAttackPressed = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            action.ParryPressed = Input.GetKey(KeyCode.O);
            action.HealPressed = Input.GetKeyDown(KeyCode.U);
            action.Confidence = 1f;
            return action;
        }

        private SensorData BuildHornetSensors(PackMember? target)
        {
            Vector2 pos = _hornetBody.Position;
            var data = new SensorData
            {
                SelfPosition = pos,
                SelfVelocity = _hornetBody.Velocity,
                SelfHealth = Hornet.Health,
                SelfMaxHealth = Hornet.MaxHealth,
                SelfIsGrounded = _hornetBody.Grounded,
                SelfCanAttack = Hornet.Balancer.AttackCooldownTimer <= 0f,
                SelfSilkOrStamina = Hornet.SilkMeter,
                SelfFacingDir = Hornet.FacingDirection,
                RayLeftDist = ArenaPhysics.RayHorizontal(Arena, pos, -1),
                RayRightDist = ArenaPhysics.RayHorizontal(Arena, pos, 1),
                RayDownDist = ArenaPhysics.RayDown(Arena, pos),
                HasSpikesBelow = ArenaPhysics.HasHazardBelow(Arena, pos),
                HasHazardAhead = ArenaPhysics.HasHazardBelow(Arena, pos + new Vector2(Hornet.FacingDirection * 3f, 0f)),
                ClosestProjectileDist = 15f
            };

            if (target != null)
            {
                data.TargetPosition = target.Body.Position;
                data.TargetVelocity = target.Body.Velocity;
                data.TargetHealth = target.Agent.Health;
                data.TargetMaxHealth = target.Agent.MaxHealth;
                data.TargetIsGrounded = target.Body.Grounded;
                data.TargetIsAttacking = target.Agent.IsAttacking;
                data.TargetIsDashing = target.Agent.IsDashing;
                data.TargetIsParrying = target.Agent.IsParrying;

                // Вторая ближайшая угроза кодируется как "снаряд" — Хорнет учится следить за окружением
                PackMember? second = FindSecondNearestAliveMob(target);
                if (second != null)
                    data.ClosestProjectileDist = Vector2.Distance(pos, second.Body.Position);
            }

            return data;
        }

        private SensorData BuildMobSensors(PackMember member)
        {
            Vector2 pos = member.Body.Position;
            var data = new SensorData
            {
                SelfPosition = pos,
                SelfVelocity = member.Body.Velocity,
                SelfHealth = member.Agent.Health,
                SelfMaxHealth = member.Agent.MaxHealth,
                SelfIsGrounded = member.Body.Grounded,
                SelfCanAttack = member.Agent.Balancer.AttackCooldownTimer <= 0f,
                SelfSilkOrStamina = member.Agent.Balancer.CurrentStamina,
                SelfFacingDir = member.Agent.FacingDirection,
                RayLeftDist = ArenaPhysics.RayHorizontal(Arena, pos, -1),
                RayRightDist = ArenaPhysics.RayHorizontal(Arena, pos, 1),
                RayDownDist = ArenaPhysics.RayDown(Arena, pos),
                HasSpikesBelow = ArenaPhysics.HasHazardBelow(Arena, pos),
                HasHazardAhead = ArenaPhysics.HasHazardBelow(Arena, pos + new Vector2(member.Agent.FacingDirection * 3f, 0f)),
                ClosestProjectileDist = DistanceToNearestAlly(member),

                TargetPosition = _hornetBody.Position,
                TargetVelocity = _hornetBody.Velocity,
                TargetHealth = Hornet.Health,
                TargetMaxHealth = Hornet.MaxHealth,
                TargetIsGrounded = _hornetBody.Grounded,
                TargetIsAttacking = Hornet.IsAttacking,
                TargetIsDashing = Hornet.IsDashing,
                TargetIsParrying = Hornet.IsHealing
            };

            return data;
        }

        private float DistanceToNearestAlly(PackMember self)
        {
            float best = 15f;
            for (int i = 0; i < Pack.Count; i++)
            {
                PackMember other = Pack[i];
                if (other == self || !other.Alive) continue;
                float d = Vector2.Distance(self.Body.Position, other.Body.Position);
                if (d < best) best = d;
            }
            return best;
        }

        public PackMember? FindNearestAliveMob()
        {
            PackMember? best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Pack.Count; i++)
            {
                if (!Pack[i].Alive) continue;
                float d = Vector2.Distance(_hornetBody.Position, Pack[i].Body.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = Pack[i];
                }
            }
            return best;
        }

        private PackMember? FindSecondNearestAliveMob(PackMember exclude)
        {
            PackMember? best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Pack.Count; i++)
            {
                if (!Pack[i].Alive || Pack[i] == exclude) continue;
                float d = Vector2.Distance(_hornetBody.Position, Pack[i].Body.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = Pack[i];
                }
            }
            return best;
        }

        // ==================================================================
        // Боевые взаимодействия: Хорнет ⟷ вся стая
        // ==================================================================
        private void ResolveCombat(float dt)
        {
            int alive = Mathf.Max(1, AliveMobCount);
            float packDamageScale = BalancePackDamage ? 1f / Mathf.Sqrt(alive) : 1f;

            for (int i = 0; i < Pack.Count; i++)
            {
                PackMember m = Pack[i];
                if (!m.Alive)
                {
                    if (!m.WasCountedDead)
                    {
                        m.WasCountedDead = true;
                        TotalMobsDefeated++;
                    }
                    continue;
                }

                float dist = Vector2.Distance(_hornetBody.Position, m.Body.Position);

                // Хорнет бьёт моба
                if (Hornet.IsAttacking && dist < 3.2f)
                {
                    float dmg = (Hornet.IsPogoing ? 32f : 24f) * dt * 3f;
                    m.Agent.TakeDamage(dmg);
                    Hornet.OnAttackLanded(dmg, Hornet.IsPogoing);

                    if (Hornet.IsPogoing)
                    {
                        // Отскок пого — классическая механика Silksong
                        _hornetBody.Velocity = new Vector2(_hornetBody.Velocity.x, 13f);
                    }
                }

                // Моб бьёт Хорнет
                if (m.Agent.IsAttacking && dist < 3.0f)
                {
                    if (Hornet.IsDashing)
                    {
                        Hornet.SuccessfulDodges++;
                    }
                    else
                    {
                        float dmg = 20f * dt * 3f * packDamageScale;
                        Hornet.TakeDamage(dmg);
                        m.Agent.OnAttackLanded(dmg);
                    }
                }
            }
        }

        // ==================================================================
        // Завершение эпизода и эволюция
        // ==================================================================
        private void FinishEpisode(bool hornetDead, bool packDead, bool timeout)
        {
            TotalEpisodes++;
            _episodesSinceSave++;

            int packSize = Pack.Count;
            int defeated = 0;
            float packDamageTotal = 0f;
            for (int i = 0; i < Pack.Count; i++)
            {
                if (!Pack[i].Alive) defeated++;
                packDamageTotal += Pack[i].Agent.TotalDamageDealt;
            }

            bool hornetWon = packDead && !hornetDead;
            bool packWon = hornetDead;

            if (hornetWon) HornetWins++;
            else if (packWon) PackWins++;
            else Draws++;

            // --- Фитнес Хорнет ---
            float hornetFitness = FitnessFunctions.CalculateGrandArenaHornetFitness(
                Hornet, defeated, packSize, EpisodeTimer, hornetWon, Wave);
            HornetTrainer.GetCurrentGenome().Fitness = hornetFitness;
            LastHornetFitness = hornetFitness;
            PushLog(HornetFitnessLog, hornetFitness);

            // --- Фитнес каждого бойца стаи (параллельная оценка геномов) ---
            var evaluatedPerArchetype = new Dictionary<string, int>();
            float packFitnessSum = 0f;

            for (int i = 0; i < Pack.Count; i++)
            {
                PackMember m = Pack[i];
                float share = packDamageTotal > 0.01f ? m.Agent.TotalDamageDealt / packDamageTotal : 0f;

                float fit = FitnessFunctions.CalculateGrandArenaMobFitness(
                    m.Agent, Hornet, EpisodeTimer, packWon, packSize, share);

                GeneticTrainer trainer = MobTrainers[m.ArchetypeKey];
                trainer.GetGenomeOffset(m.GenomeOffset).Fitness = fit;
                packFitnessSum += fit;

                evaluatedPerArchetype.TryGetValue(m.ArchetypeKey, out int c);
                evaluatedPerArchetype[m.ArchetypeKey] = c + 1;
            }

            LastPackFitness = packSize > 0 ? packFitnessSum / packSize : 0f;
            PushLog(PackFitnessLog, LastPackFitness);

            // --- Продвижение популяций ---
            HornetTrainer.AdvanceToNextGenome();
            foreach (var pair in evaluatedPerArchetype)
            {
                MobTrainers[pair.Key].AdvanceGenomes(pair.Value);
            }

            // --- Прогресс волн ---
            if (hornetWon)
            {
                Wave++;
                if (Wave > BestWave) BestWave = Wave;
                ShowStatus($"Хорнет зачистила волну {Wave - 1}! Стая усиливается.");
            }
            else if (packWon)
            {
                Wave = Mathf.Max(1, Wave - 1);
                ShowStatus("Стая победила Хорнет — эволюция продолжается.");
            }

            if (WaveScaling)
            {
                int desired = Mathf.Clamp(BaseMobCount + (Wave - 1) / 2, MIN_MOBS, MAX_MOBS);
                if (desired != MobCount)
                {
                    MobCount = desired;
                    RebuildPack();
                }
            }

            // --- Автосохранение слота ---
            if (AutoSaveEveryEpisodes > 0 && _episodesSinceSave >= AutoSaveEveryEpisodes)
            {
                _episodesSinceSave = 0;
                GrandArenaSaveSystem.Save(ActiveSlot, this);
                ShowStatus($"Автосохранение в слот {ActiveSlot}");
            }

            ResetEpisode();
        }

        private static void PushLog(List<float> log, float value)
        {
            log.Add(value);
            if (log.Count > 400) log.RemoveAt(0);
        }

        public void ShowStatus(string message)
        {
            StatusMessage = message;
            StatusMessageTime = 4f;
        }

        // ==================================================================
        // Сохранения
        // ==================================================================
        public void SaveCurrentSlot()
        {
            if (GrandArenaSaveSystem.Save(ActiveSlot, this))
                ShowStatus($"Сохранено в слот {ActiveSlot}");
            else
                ShowStatus("Ошибка сохранения (см. лог BepInEx)");
        }

        public void LoadSlot(int slot)
        {
            if (!GrandArenaSaveSystem.SlotExists(slot))
            {
                ShowStatus($"Слот {slot} пуст");
                return;
            }

            ActiveSlot = slot;
            GrandArenaSaveSystem.Load(slot, this);
            RebuildPack();
            ResetEpisode();
            ShowStatus($"Загружен слот {slot}");
        }

        public Vector2 HornetPosition => _hornetBody.Position;
        public ArenaBody HornetBody => _hornetBody;

        private void OnDestroy()
        {
            if (IsActive)
            {
                Time.timeScale = 1f;
            }
        }
    }
}

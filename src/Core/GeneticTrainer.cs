using System;
using System.Collections.Generic;
using System.Linq;

namespace SilksongNeuralSmart.Core
{
    public class Genome
    {
        public NeuralNetwork Brain { get; set; }
        public float Fitness { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public float TotalDamageDealt { get; set; }
        public float TotalDamageTaken { get; set; }
        public int TotalAttacksLanded { get; set; }
        public int TotalDodges { get; set; }
        public float SurvivalTime { get; set; }

        public Genome(NeuralNetwork brain)
        {
            Brain = brain;
            Fitness = 0f;
        }

        public void ResetMetrics()
        {
            Fitness = 0f;
            Wins = 0;
            Losses = 0;
            TotalDamageDealt = 0f;
            TotalDamageTaken = 0f;
            TotalAttacksLanded = 0;
            TotalDodges = 0;
            SurvivalTime = 0f;
        }
    }

    public class GeneticTrainer
    {
        public int PopulationSize { get; private set; }
        public int Generation { get; private set; }
        public List<Genome> Population { get; private set; }
        public int CurrentGenomeIndex { get; private set; }

        public float MutationRate { get; set; } = 0.08f;
        public float MutationStrength { get; set; } = 0.25f;
        public int EliteCount { get; set; } = 2;

        public float BestFitnessEver { get; private set; }
        public Genome BestGenomeEver { get; private set; } = null!;
        public List<float> FitnessHistory { get; private set; } = new List<float>();

        private readonly Random _rng = new Random();

        public GeneticTrainer(int populationSize, int[] layerSizes)
        {
            PopulationSize = Math.Max(4, populationSize);
            Generation = 1;
            Population = new List<Genome>(PopulationSize);

            for (int i = 0; i < PopulationSize; i++)
            {
                var net = new NeuralNetwork(layerSizes);
                Population.Add(new Genome(net));
            }

            BestFitnessEver = float.MinValue;
            BestGenomeEver = Population[0];
        }

        public Genome GetCurrentGenome()
        {
            if (CurrentGenomeIndex >= Population.Count)
                CurrentGenomeIndex = 0;
            return Population[CurrentGenomeIndex];
        }

        public bool AdvanceToNextGenome()
        {
            CurrentGenomeIndex++;
            if (CurrentGenomeIndex >= Population.Count)
            {
                EvolveGeneration();
                CurrentGenomeIndex = 0;
                return true; // Generation finished
            }
            return false;
        }

        public void EvolveGeneration()
        {
            // Sort population by fitness descending
            Population = Population.OrderByDescending(g => g.Fitness).ToList();

            float genBestFitness = Population[0].Fitness;
            FitnessHistory.Add(genBestFitness);

            if (genBestFitness > BestFitnessEver)
            {
                BestFitnessEver = genBestFitness;
                BestGenomeEver = new Genome(Population[0].Brain.Clone())
                {
                    Fitness = genBestFitness
                };
            }

            var nextGen = new List<Genome>(PopulationSize);

            // Elitism: Preserve best individuals without mutation
            for (int i = 0; i < EliteCount && i < Population.Count; i++)
            {
                nextGen.Add(new Genome(Population[i].Brain.Clone()));
            }

            // Generate offspring through tournament selection, crossover, and mutation
            while (nextGen.Count < PopulationSize)
            {
                Genome parentA = TournamentSelect(4);
                Genome parentB = TournamentSelect(4);

                NeuralNetwork childBrain = NeuralNetwork.Crossover(parentA.Brain, parentB.Brain);
                childBrain.Mutate(MutationRate, MutationStrength);

                nextGen.Add(new Genome(childBrain));
            }

            Population = nextGen;
            Generation++;
        }

        private Genome TournamentSelect(int tournamentSize)
        {
            Genome best = null!;
            float bestFit = float.MinValue;

            for (int i = 0; i < tournamentSize; i++)
            {
                int idx = _rng.Next(Population.Count);
                if (Population[idx].Fitness > bestFit || best == null)
                {
                    best = Population[idx];
                    bestFit = best.Fitness;
                }
            }

            return best;
        }

        public void SetBestBrain(NeuralNetwork brain)
        {
            BestGenomeEver = new Genome(brain.Clone());
            Population[0] = new Genome(brain.Clone());
        }

        /// <summary>
        /// Возвращает геном со смещением относительно текущего.
        /// Позволяет оценивать несколько геномов параллельно (несколько мобов в одной арене).
        /// </summary>
        public Genome GetGenomeOffset(int offset)
        {
            if (Population.Count == 0)
                throw new InvalidOperationException("Population is empty.");

            int idx = (CurrentGenomeIndex + offset) % Population.Count;
            if (idx < 0) idx += Population.Count;
            return Population[idx];
        }

        /// <summary>
        /// Продвигает указатель популяции сразу на несколько геномов
        /// (после эпизода, в котором параллельно оценивалось несколько особей).
        /// </summary>
        public bool AdvanceGenomes(int count)
        {
            bool evolved = false;
            for (int i = 0; i < Math.Max(1, count); i++)
            {
                if (AdvanceToNextGenome())
                    evolved = true;
            }
            return evolved;
        }

        /// <summary>
        /// Восстанавливает тренера из сохранения отдельного режима:
        /// лучший мозг становится элитой, остальная популяция засевается его мутациями.
        /// </summary>
        public void RestoreFromBrain(NeuralNetwork brain, int generation, float bestFitness, List<float>? fitnessHistory = null)
        {
            if (brain == null) return;

            BestGenomeEver = new Genome(brain.Clone()) { Fitness = bestFitness };
            BestFitnessEver = bestFitness;
            Generation = Math.Max(1, generation);
            CurrentGenomeIndex = 0;

            Population.Clear();
            Population.Add(new Genome(brain.Clone()));
            Population.Add(new Genome(brain.Clone()));

            while (Population.Count < PopulationSize)
            {
                var child = brain.Clone();
                child.Mutate(Math.Max(0.02f, MutationRate), MutationStrength);
                Population.Add(new Genome(child));
            }

            if (fitnessHistory != null)
            {
                FitnessHistory.Clear();
                FitnessHistory.AddRange(fitnessHistory);
            }
        }
    }
}

using System;
using UnityEngine;
using SilksongNeuralSmart.Agents;

namespace SilksongNeuralSmart.Training
{
    public static class FitnessFunctions
    {
        public static float CalculateHornetFitness(HornetAgent hornet, MobAgent mob, float episodeDuration, bool won)
        {
            float fitness = 0f;

            // Damage rewards & penalties
            fitness += hornet.TotalDamageDealt * 3.5f;
            fitness -= hornet.TotalDamageTaken * 2.0f;

            // Combat successes
            fitness += hornet.SuccessfulPogos * 18.0f;
            fitness += hornet.SuccessfulDodges * 8.0f;
            fitness += hornet.SuccessfulHeals * 15.0f;

            // Survival & Win outcome
            fitness += Mathf.Min(episodeDuration, 60f) * 0.5f;
            if (won)
            {
                fitness += 200.0f;
                // Bonus for fast victory
                fitness += Mathf.Max(0f, 40f - episodeDuration) * 5.0f;
            }

            // Health percentage remaining
            fitness += (hornet.Health / hornet.MaxHealth) * 50f;

            return Mathf.Max(0f, fitness);
        }

        public static float CalculateMobFitness(MobAgent mob, HornetAgent hornet, float episodeDuration, bool won)
        {
            float fitness = 0f;

            fitness += mob.TotalDamageDealt * 3.5f;
            fitness -= mob.TotalDamageTaken * 1.8f;
            fitness += mob.SuccessfulDodges * 8.0f;
            fitness += mob.AttacksHit * 12.0f;

            fitness += Mathf.Min(episodeDuration, 60f) * 0.4f;
            if (won)
            {
                fitness += 180.0f;
                fitness += Mathf.Max(0f, 40f - episodeDuration) * 4.0f;
            }

            fitness += (mob.Health / mob.MaxHealth) * 40f;

            return Mathf.Max(0f, fitness);
        }
    }
}

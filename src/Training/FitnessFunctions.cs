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

        // =====================================================================
        // РЕЖИМ "ВЕЛИКАЯ АРЕНА": Хорнет против стаи, все учатся одновременно
        // =====================================================================

        /// <summary>
        /// Фитнес Хорнет в бою против нескольких мобов сразу.
        /// Учитывает численный перевес врагов, зачистку волны и экономию здоровья.
        /// </summary>
        public static float CalculateGrandArenaHornetFitness(
            HornetAgent hornet,
            int mobsDefeated,
            int packSize,
            float episodeDuration,
            bool clearedWave,
            int wave)
        {
            float fitness = 0f;
            float outnumbered = Mathf.Max(1f, packSize);

            // Урон: награда масштабируется числом врагов (сложнее — ценнее)
            fitness += hornet.TotalDamageDealt * 3.0f;
            fitness -= hornet.TotalDamageTaken * (2.4f / Mathf.Sqrt(outnumbered));

            // Точные боевые приёмы
            fitness += hornet.SuccessfulPogos * 20.0f;
            fitness += hornet.SuccessfulDodges * 7.0f;
            fitness += hornet.SuccessfulHeals * 12.0f;

            // Убитые мобы
            fitness += mobsDefeated * 70.0f;

            // Выживание в окружении
            fitness += Mathf.Min(episodeDuration, 90f) * (0.6f * outnumbered);

            if (clearedWave)
            {
                fitness += 240.0f + wave * 40.0f;
                fitness += Mathf.Max(0f, 60f - episodeDuration) * 4.0f;
            }

            fitness += (hornet.Health / Mathf.Max(1f, hornet.MaxHealth)) * 60f;

            return Mathf.Max(0f, fitness);
        }

        /// <summary>
        /// Фитнес одного моба из стаи. Поощряет командную работу:
        /// каждый получает бонус за общий успех стаи и за собственный вклад.
        /// </summary>
        public static float CalculateGrandArenaMobFitness(
            MobAgent mob,
            HornetAgent hornet,
            float episodeDuration,
            bool packWon,
            int packSize,
            float packDamageShare)
        {
            float fitness = 0f;

            fitness += mob.TotalDamageDealt * 4.0f;
            fitness -= mob.TotalDamageTaken * 1.4f;
            fitness += mob.AttacksHit * 10.0f;
            fitness += mob.SuccessfulDodges * 6.0f;

            // Вклад в общий урон стаи (борьба с «безбилетниками», которые прячутся)
            fitness += Mathf.Clamp01(packDamageShare) * 60.0f;

            // Выживание: ценно, но меньше, чем давление на Хорнет
            fitness += Mathf.Min(episodeDuration, 90f) * 0.25f;
            fitness += (mob.Health / Mathf.Max(1f, mob.MaxHealth)) * 25f;

            if (packWon)
            {
                fitness += 150.0f + Mathf.Max(0f, 60f - episodeDuration) * 3.0f;
                // Чем меньше стая — тем ценнее победа
                fitness += Mathf.Max(0f, 8f - packSize) * 10f;
            }

            return Mathf.Max(0f, fitness);
        }
    }
}

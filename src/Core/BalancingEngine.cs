using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongNeuralSmart.Core
{
    public class BalancingEngine
    {
        private readonly Queue<BufferedObservation> _reactionBuffer = new Queue<BufferedObservation>();
        private readonly System.Random _rng = new System.Random();

        public float ReactionDelaySeconds { get; set; } = 0.11f; // 110ms human baseline
        public float ErrorMarginRate { get; set; } = 0.05f;      // 5% tactical inaccuracy
        public float StaminaRecoveryRate { get; set; } = 0.35f;  // Stamina regen/sec
        public float CurrentStamina { get; private set; } = 1.0f;
        public float AttackCooldownTimer { get; private set; } = 0.0f;

        private struct BufferedObservation
        {
            public float Timestamp;
            public float[] Observation;
        }

        public void Reset()
        {
            _reactionBuffer.Clear();
            CurrentStamina = 1.0f;
            AttackCooldownTimer = 0.0f;
        }

        public void Update(float deltaTime)
        {
            if (AttackCooldownTimer > 0)
                AttackCooldownTimer -= deltaTime;

            CurrentStamina = Mathf.Clamp01(CurrentStamina + StaminaRecoveryRate * deltaTime);
        }

        /// <summary>
        /// Inserts observation into time-delayed reaction buffer
        /// </summary>
        public void PushObservation(float[] observation, float currentTime)
        {
            _reactionBuffer.Enqueue(new BufferedObservation
            {
                Timestamp = currentTime,
                Observation = (float[])observation.Clone()
            });
        }

        /// <summary>
        /// Retrieves the observation that corresponds to the AI's reaction latency
        /// </summary>
        public float[] GetPerceivedObservation(float currentTime, float[] fallbackCurrent)
        {
            if (_reactionBuffer.Count == 0) return fallbackCurrent;

            float targetTime = currentTime - ReactionDelaySeconds;
            BufferedObservation selected = _reactionBuffer.Peek();

            while (_reactionBuffer.Count > 1)
            {
                var next = _reactionBuffer.Peek();
                if (next.Timestamp <= targetTime)
                {
                    selected = _reactionBuffer.Dequeue();
                }
                else
                {
                    break;
                }
            }

            return selected.Observation ?? fallbackCurrent;
        }

        /// <summary>
        /// Applies fair-play filters to neural network decisions (cooldowns, stamina, mistakes)
        /// </summary>
        public DecodedAction FilterAction(DecodedAction rawAction)
        {
            var filtered = rawAction;

            // 1. Stochastic human error margin
            if (_rng.NextDouble() < ErrorMarginRate)
            {
                // Jitter movement or slight hesitation
                filtered.MoveX *= 0.5f;
            }

            // 2. Cooldown & Stamina checks for heavy actions
            if (filtered.AttackPressed || filtered.SpecialPressed || filtered.DownAttackPressed)
            {
                if (AttackCooldownTimer > 0f || CurrentStamina < 0.20f)
                {
                    // Cancel attack, convert to repositioning
                    filtered.AttackPressed = false;
                    filtered.SpecialPressed = false;
                    filtered.DownAttackPressed = false;
                }
                else
                {
                    // Deduct stamina and apply attack recovery lock
                    CurrentStamina -= 0.25f;
                    AttackCooldownTimer = 0.40f; // 400ms recovery window
                }
            }

            // 3. Dash Stamina check
            if (filtered.DashPressed)
            {
                if (CurrentStamina < 0.15f)
                {
                    filtered.DashPressed = false;
                }
                else
                {
                    CurrentStamina -= 0.18f;
                }
            }

            return filtered;
        }
    }
}

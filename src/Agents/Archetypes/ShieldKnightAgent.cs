using System;
using UnityEngine;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.Agents.Archetypes
{
    public class ShieldKnightAgent : MobAgent
    {
        public override string ArchetypeName => "Citadel Shield Knight";

        public bool IsShieldRaised { get; private set; }

        protected override void Awake()
        {
            MaxHealth = 130f;
            Health = 130f;
            base.Awake();
        }

        public override void TakeDamage(float amount)
        {
            if (IsShieldRaised)
            {
                // Shield blocks 85% of incoming frontal damage
                amount *= 0.15f;
            }
            base.TakeDamage(amount);
        }

        protected override void ExecuteAction(DecodedAction action, float deltaTime)
        {
            float moveSpeed = IsShieldRaised ? 3.0f : 5.0f;
            float targetVelX = action.MoveX * moveSpeed;

            if (_rb2d != null)
            {
                _rb2d.velocity = new Vector2(targetVelX, _rb2d.velocity.y);
            }
            Velocity = new Vector2(targetVelX, Velocity.y);

            if (action.MoveX > 0.1f) FacingDirection = 1;
            else if (action.MoveX < -0.1f) FacingDirection = -1;

            // Shield Block
            IsShieldRaised = action.ParryPressed;
            IsParrying = IsShieldRaised;

            // Overhead Greatsword Smash
            if (action.AttackPressed && !IsShieldRaised)
            {
                IsAttacking = true;
            }
            else
            {
                IsAttacking = false;
            }

            // Shield Charge / Rush
            if (action.SpecialPressed)
            {
                IsDashing = true;
                if (_rb2d != null)
                    _rb2d.velocity = new Vector2(FacingDirection * 10.0f, _rb2d.velocity.y);
            }
            else
            {
                IsDashing = false;
            }
        }
    }
}

using System;
using UnityEngine;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.Agents.Archetypes
{
    public class AssassinWeaverAgent : MobAgent
    {
        public override string ArchetypeName => "Silk Assassin Weaver";

        protected override void Awake()
        {
            MaxHealth = 70f;
            Health = 70f;
            base.Awake();
        }

        protected override void ExecuteAction(DecodedAction action, float deltaTime)
        {
            float moveSpeed = 8.5f;
            float targetVelX = action.MoveX * moveSpeed;

            if (_rb2d != null)
            {
                _rb2d.velocity = new Vector2(targetVelX, _rb2d.velocity.y);
            }
            Velocity = new Vector2(targetVelX, Velocity.y);

            if (action.MoveX > 0.1f) FacingDirection = 1;
            else if (action.MoveX < -0.1f) FacingDirection = -1;

            // Acrobatics: High jump & wall jump
            if (action.JumpPressed && IsGrounded)
            {
                if (_rb2d != null) _rb2d.velocity = new Vector2(_rb2d.velocity.x, 15.0f);
                Velocity = new Vector2(Velocity.x, 15.0f);
                IsGrounded = false;
            }

            // Silk Blink / Flash Dash
            if (action.DashPressed)
            {
                IsDashing = true;
                if (_rb2d != null)
                    _rb2d.velocity = new Vector2(FacingDirection * 19.0f, 0f);
            }
            else
            {
                IsDashing = false;
            }

            // Needle Cross-Slash
            if (action.AttackPressed)
            {
                IsAttacking = true;
            }
            else
            {
                IsAttacking = false;
            }

            // Silk Trap placement / Thread Snipe
            if (action.SpecialPressed)
            {
                IsAttacking = true;
            }

            IsParrying = action.ParryPressed;
        }
    }
}

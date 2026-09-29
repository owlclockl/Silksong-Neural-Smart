using System;
using UnityEngine;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.Agents.Archetypes
{
    public class ScoutGruntAgent : MobAgent
    {
        public override string ArchetypeName => "Moss Scout Grunt";

        protected override void Awake()
        {
            MaxHealth = 80f;
            Health = 80f;
            base.Awake();
        }

        protected override void ExecuteAction(DecodedAction action, float deltaTime)
        {
            // Horizontal movement
            float moveSpeed = 6.0f;
            float targetVelX = action.MoveX * moveSpeed;

            if (_rb2d != null)
            {
                _rb2d.velocity = new Vector2(targetVelX, _rb2d.velocity.y);
            }
            Velocity = new Vector2(targetVelX, Velocity.y);

            if (action.MoveX > 0.1f) FacingDirection = 1;
            else if (action.MoveX < -0.1f) FacingDirection = -1;

            // Jump / Hop
            if (action.JumpPressed && IsGrounded)
            {
                if (_rb2d != null) _rb2d.velocity = new Vector2(_rb2d.velocity.x, 11.0f);
                Velocity = new Vector2(Velocity.x, 11.0f);
                IsGrounded = false;
            }

            // Quick Dash / Backstep
            if (action.DashPressed)
            {
                IsDashing = true;
                float dashSpeed = FacingDirection * 12.0f;
                if (_rb2d != null) _rb2d.velocity = new Vector2(dashSpeed, _rb2d.velocity.y);
            }
            else
            {
                IsDashing = false;
            }

            // Melee Slash
            if (action.AttackPressed)
            {
                IsAttacking = true;
            }
            else
            {
                IsAttacking = false;
            }

            // Parry / Guard stance
            IsParrying = action.ParryPressed;
        }
    }
}

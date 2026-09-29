using System;
using UnityEngine;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.Agents.Archetypes
{
    public class FlyingHunterAgent : MobAgent
    {
        public override string ArchetypeName => "Flying Silk Hunter";

        public override bool IsFlyingArchetype => true;

        public override Vector2 ColliderSize => new Vector2(1.2f, 1.2f);

        protected override void Awake()
        {
            MaxHealth = 65f;
            Health = 65f;
            base.Awake();
        }

        protected override void ExecuteAction(DecodedAction action, float deltaTime)
        {
            float flySpeedX = action.MoveX * 7.5f;
            float flySpeedY = 0f;

            // Flying vertical control
            if (action.JumpPressed)
            {
                flySpeedY = 6.0f; // Ascend
            }
            else if (action.DownAttackPressed)
            {
                flySpeedY = -8.0f; // Dive down
            }
            else
            {
                // Hover sinusoidal oscillation
                flySpeedY = Mathf.Sin(Time.time * 3f) * 1.5f;
            }

            if (_rb2d != null)
            {
                _rb2d.velocity = new Vector2(flySpeedX, flySpeedY);
            }
            Velocity = new Vector2(flySpeedX, flySpeedY);

            if (action.MoveX > 0.1f) FacingDirection = 1;
            else if (action.MoveX < -0.1f) FacingDirection = -1;

            // Dive-bomb swoop
            if (action.SpecialPressed)
            {
                IsAttacking = true;
                if (_rb2d != null)
                    _rb2d.velocity = new Vector2(FacingDirection * 14.0f, -10.0f);
            }
            else if (action.AttackPressed)
            {
                IsAttacking = true;
            }
            else
            {
                IsAttacking = false;
            }

            // Air Dash evade
            IsDashing = action.DashPressed;
        }
    }
}

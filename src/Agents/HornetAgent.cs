using System;
using UnityEngine;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.Agents
{
    public class HornetAgent : MonoBehaviour
    {
        public NeuralNetwork Brain { get; set; } = null!;
        public BalancingEngine Balancer { get; private set; } = new BalancingEngine();

        public float Health { get; set; } = 100f;
        public float MaxHealth { get; set; } = 100f;
        public float SilkMeter { get; set; } = 1.0f; // 0.0 to 1.0 (9 spools)
        public Vector2 Velocity { get; set; }
        public bool IsGrounded { get; set; } = true;
        public bool IsDashing { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsPogoing { get; set; }
        public bool IsHealing { get; set; }
        public int FacingDirection { get; set; } = 1;

        public float TotalDamageDealt { get; set; }
        public float TotalDamageTaken { get; set; }
        public int SuccessfulPogos { get; set; }
        public int SuccessfulDodges { get; set; }
        public int SuccessfulHeals { get; set; }

        public Transform TargetEnemy { get; set; } = null!;
        private Rigidbody2D _rb2d = null!;

        private void Awake()
        {
            _rb2d = GetComponent<Rigidbody2D>();
            if (Brain == null)
            {
                Brain = new NeuralNetwork(ObservationSensor.INPUT_VECTOR_SIZE, 32, 32, ActionDecoders.OUTPUT_ACTION_COUNT);
            }
        }

        public void ResetAgent(Vector2 spawnPosition)
        {
            transform.position = spawnPosition;
            Health = MaxHealth;
            SilkMeter = 1.0f;
            Velocity = Vector2.zero;
            if (_rb2d != null) _rb2d.velocity = Vector2.zero;
            Balancer.Reset();
            IsDashing = false;
            IsAttacking = false;
            IsPogoing = false;
            IsHealing = false;
            TotalDamageDealt = 0;
            TotalDamageTaken = 0;
            SuccessfulPogos = 0;
            SuccessfulDodges = 0;
            SuccessfulHeals = 0;
        }

        public void TakeDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            TotalDamageTaken += amount;
        }

        public void OnAttackLanded(float damage, bool isPogo = false)
        {
            TotalDamageDealt += damage;
            SilkMeter = Mathf.Clamp01(SilkMeter + 0.15f); // Gain silk on hit
            if (isPogo) SuccessfulPogos++;
        }

        public SensorData CollectSensors()
        {
            var data = new SensorData
            {
                SelfPosition = transform.position,
                SelfVelocity = _rb2d != null ? _rb2d.velocity : Velocity,
                SelfHealth = Health,
                SelfMaxHealth = MaxHealth,
                SelfIsGrounded = IsGrounded,
                SelfCanAttack = Balancer.AttackCooldownTimer <= 0f,
                SelfSilkOrStamina = SilkMeter,
                SelfFacingDir = FacingDirection,
                RayLeftDist = 12f,
                RayRightDist = 12f,
                RayDownDist = 2.5f,
                HasSpikesBelow = false,
                HasHazardAhead = false,
                ClosestProjectileDist = 15f
            };

            if (TargetEnemy != null)
            {
                data.TargetPosition = TargetEnemy.position;
                var enemyAgent = TargetEnemy.GetComponent<MobAgent>();
                if (enemyAgent != null)
                {
                    data.TargetVelocity = enemyAgent.Velocity;
                    data.TargetHealth = enemyAgent.Health;
                    data.TargetMaxHealth = enemyAgent.MaxHealth;
                    data.TargetIsGrounded = enemyAgent.IsGrounded;
                    data.TargetIsAttacking = enemyAgent.IsAttacking;
                    data.TargetIsDashing = enemyAgent.IsDashing;
                    data.TargetIsParrying = enemyAgent.IsParrying;
                }
            }

            return data;
        }

        public DecodedAction StepDecision(float deltaTime)
        {
            Balancer.Update(deltaTime);

            var sensorData = CollectSensors();
            float[] obs = ObservationSensor.EncodeObservation(sensorData);

            Balancer.PushObservation(obs, Time.time);
            float[] perceivedObs = Balancer.GetPerceivedObservation(Time.time, obs);

            float[] actionLogits = Brain.Forward(perceivedObs);
            DecodedAction rawAction = ActionDecoders.Decode(actionLogits);

            DecodedAction finalAction = Balancer.FilterAction(rawAction);
            ExecuteHornetAction(finalAction, deltaTime);

            return finalAction;
        }

        private void ExecuteHornetAction(DecodedAction action, float deltaTime)
        {
            // Horizontal locomotion
            float targetSpeed = action.MoveX * 8.5f;
            if (_rb2d != null)
            {
                _rb2d.velocity = new Vector2(targetSpeed, _rb2d.velocity.y);
            }
            Velocity = new Vector2(targetSpeed, Velocity.y);

            if (action.MoveX > 0.1f) FacingDirection = 1;
            else if (action.MoveX < -0.1f) FacingDirection = -1;

            // Jump
            if (action.JumpPressed && IsGrounded)
            {
                if (_rb2d != null) _rb2d.velocity = new Vector2(_rb2d.velocity.x, 14.5f);
                Velocity = new Vector2(Velocity.x, 14.5f);
                IsGrounded = false;
            }

            // Dash
            if (action.DashPressed && !IsDashing)
            {
                IsDashing = true;
                if (_rb2d != null) _rb2d.velocity = new Vector2(FacingDirection * 18.0f, 0f);
            }

            // Silk Heal (Focus)
            if (action.HealPressed && SilkMeter >= 0.33f && IsGrounded)
            {
                IsHealing = true;
                SilkMeter -= 0.33f;
                Health = Mathf.Min(MaxHealth, Health + 25f);
                SuccessfulHeals++;
            }
            else
            {
                IsHealing = false;
            }

            // Pogo Down Slash
            if (action.DownAttackPressed && !IsGrounded)
            {
                IsPogoing = true;
            }
            else
            {
                IsPogoing = false;
            }

            // Attack
            if (action.AttackPressed)
            {
                IsAttacking = true;
            }
            else
            {
                IsAttacking = false;
            }
        }
    }
}

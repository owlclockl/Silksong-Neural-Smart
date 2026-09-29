using System;
using UnityEngine;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.Agents
{
    public abstract class MobAgent : MonoBehaviour
    {
        public NeuralNetwork Brain { get; set; } = null!;
        public BalancingEngine Balancer { get; private set; } = new BalancingEngine();
        
        public float Health { get; protected set; } = 100f;
        public float MaxHealth { get; protected set; } = 100f;
        public Vector2 Velocity { get; protected set; }
        public bool IsGrounded { get; protected set; }
        public bool IsAttacking { get; protected set; }
        public bool IsDashing { get; protected set; }
        public bool IsParrying { get; protected set; }
        public int FacingDirection { get; protected set; } = 1;

        public DecodedAction LastAction { get; protected set; }
        public DecodedAction LastRawAction { get; protected set; }

        public float TotalDamageDealt { get; set; }
        public float TotalDamageTaken { get; set; }
        public int SuccessfulDodges { get; set; }
        public int AttacksHit { get; set; }

        protected Transform _targetTransform = null!;
        protected Rigidbody2D _rb2d = null!;

        public abstract string ArchetypeName { get; }

        /// <summary>Летающие архетипы игнорируют гравитацию в автономных тренировочных аренах.</summary>
        public virtual bool IsFlyingArchetype => false;

        /// <summary>Габариты агента для собственной физики тренировочных арен (в юнитах Unity).</summary>
        public virtual Vector2 ColliderSize => new Vector2(1.1f, 1.7f);

        /// <summary>
        /// Применяет результат собственной физики арены (используется режимом "Великая Арена",
        /// где агенты живут в виртуальной арене без Rigidbody2D игры).
        /// </summary>
        public void SetKinematicState(Vector2 position, Vector2 velocity, bool grounded)
        {
            transform.position = position;
            Velocity = velocity;
            IsGrounded = grounded;
            if (_rb2d != null) _rb2d.velocity = velocity;
        }

        /// <summary>Внешняя установка цели наблюдения без обращения к приватным полям.</summary>
        public Transform CurrentTarget => _targetTransform;

        protected virtual void Awake()
        {
            _rb2d = GetComponent<Rigidbody2D>();
            if (Brain == null)
            {
                // Default architecture: 24 inputs -> 32 -> 32 -> 10 outputs
                Brain = new NeuralNetwork(ObservationSensor.INPUT_VECTOR_SIZE, 32, 32, ActionDecoders.OUTPUT_ACTION_COUNT);
            }
        }

        public void SetTarget(Transform target)
        {
            _targetTransform = target;
        }

        public virtual void ResetAgent(Vector2 spawnPosition)
        {
            transform.position = spawnPosition;
            Health = MaxHealth;
            Velocity = Vector2.zero;
            if (_rb2d != null) _rb2d.velocity = Vector2.zero;
            Balancer.Reset();
            IsAttacking = false;
            IsDashing = false;
            IsParrying = false;
            TotalDamageDealt = 0;
            TotalDamageTaken = 0;
            SuccessfulDodges = 0;
            AttacksHit = 0;
        }

        public virtual void TakeDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            TotalDamageTaken += amount;
        }

        public virtual void OnAttackLanded(float damage)
        {
            TotalDamageDealt += damage;
            AttacksHit++;
        }

        public virtual void OnDodgeSuccess()
        {
            SuccessfulDodges++;
        }

        public virtual SensorData CollectSensors()
        {
            var data = new SensorData
            {
                SelfPosition = transform.position,
                SelfVelocity = _rb2d != null ? _rb2d.velocity : Velocity,
                SelfHealth = Health,
                SelfMaxHealth = MaxHealth,
                SelfIsGrounded = IsGrounded,
                SelfCanAttack = Balancer.AttackCooldownTimer <= 0f,
                SelfSilkOrStamina = Balancer.CurrentStamina,
                SelfFacingDir = FacingDirection,
                RayLeftDist = 10f,
                RayRightDist = 10f,
                RayDownDist = 2f,
                HasSpikesBelow = false,
                HasHazardAhead = false,
                ClosestProjectileDist = 15f
            };

            if (_targetTransform != null)
            {
                data.TargetPosition = _targetTransform.position;
                var targetRb = _targetTransform.GetComponent<Rigidbody2D>();
                data.TargetVelocity = targetRb != null ? targetRb.velocity : Vector2.zero;
                data.TargetHealth = 100f;
                data.TargetMaxHealth = 100f;
                data.TargetIsGrounded = true;
                data.TargetIsAttacking = false;
                data.TargetIsDashing = false;
                data.TargetIsParrying = false;
            }

            return data;
        }

        public virtual DecodedAction StepDecision(float deltaTime)
        {
            return StepDecision(CollectSensors(), deltaTime);
        }

        /// <summary>
        /// Шаг принятия решения с готовым набором сенсоров.
        /// Используется автономными аренами (режим "Великая Арена"), которые
        /// считают лучи и окружение самостоятельно.
        /// </summary>
        public virtual DecodedAction StepDecision(SensorData rawSensor, float deltaTime)
        {
            Balancer.Update(deltaTime);

            float[] obs = ObservationSensor.EncodeObservation(rawSensor);

            // Time-buffer for balanced human reaction latency
            Balancer.PushObservation(obs, Time.time);
            float[] perceivedObs = Balancer.GetPerceivedObservation(Time.time, obs);

            // Fast neural inference
            float[] actionLogits = Brain.Forward(perceivedObs);
            DecodedAction rawAction = ActionDecoders.Decode(actionLogits);

            // Filter with balancing rules
            DecodedAction finalAction = Balancer.FilterAction(rawAction);
            LastRawAction = rawAction;
            LastAction = finalAction;
            ExecuteAction(finalAction, deltaTime);

            return finalAction;
        }

        protected abstract void ExecuteAction(DecodedAction action, float deltaTime);
    }
}

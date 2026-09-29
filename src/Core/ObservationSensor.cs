using System;
using UnityEngine;

namespace SilksongNeuralSmart.Core
{
    public struct SensorData
    {
        public Vector2 SelfPosition;
        public Vector2 SelfVelocity;
        public float SelfHealth;
        public float SelfMaxHealth;
        public bool SelfIsGrounded;
        public bool SelfCanAttack;
        public float SelfSilkOrStamina;
        public int SelfFacingDir; // -1: Left, +1: Right

        public Vector2 TargetPosition;
        public Vector2 TargetVelocity;
        public float TargetHealth;
        public float TargetMaxHealth;
        public bool TargetIsGrounded;
        public bool TargetIsAttacking;
        public bool TargetIsDashing;
        public bool TargetIsParrying;

        // Environmental raycasts (distances in Unity units, max 15.0f)
        public float RayLeftDist;
        public float RayRightDist;
        public float RayDownDist;
        public bool HasSpikesBelow;
        public bool HasHazardAhead;
        public float ClosestProjectileDist;
    }

    public static class ObservationSensor
    {
        public const int INPUT_VECTOR_SIZE = 24;
        private const float MAX_ARENA_DIM_X = 35.0f;
        private const float MAX_ARENA_DIM_Y = 20.0f;
        private const float MAX_VELOCITY = 25.0f;
        private const float MAX_RAY_DIST = 15.0f;

        public static float[] EncodeObservation(SensorData data)
        {
            float[] obs = new float[INPUT_VECTOR_SIZE];

            float dx = (data.TargetPosition.x - data.SelfPosition.x);
            float dy = (data.TargetPosition.y - data.SelfPosition.y);
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            // 1. Spatial relative offsets
            obs[0] = Mathf.Clamp(dx / MAX_ARENA_DIM_X, -1.0f, 1.0f);
            obs[1] = Mathf.Clamp(dy / MAX_ARENA_DIM_Y, -1.0f, 1.0f);
            obs[2] = Mathf.Clamp01(dist / MAX_ARENA_DIM_X);
            obs[3] = Mathf.Atan2(dy, dx) / Mathf.PI; // Normalized angle [-1, 1]

            // 2. Velocities
            obs[4] = Mathf.Clamp(data.SelfVelocity.x / MAX_VELOCITY, -1.0f, 1.0f);
            obs[5] = Mathf.Clamp(data.SelfVelocity.y / MAX_VELOCITY, -1.0f, 1.0f);
            obs[6] = Mathf.Clamp(data.TargetVelocity.x / MAX_VELOCITY, -1.0f, 1.0f);
            obs[7] = Mathf.Clamp(data.TargetVelocity.y / MAX_VELOCITY, -1.0f, 1.0f);

            // 3. Health & Resources
            obs[8] = data.SelfMaxHealth > 0 ? Mathf.Clamp01(data.SelfHealth / data.SelfMaxHealth) : 1f;
            obs[9] = data.TargetMaxHealth > 0 ? Mathf.Clamp01(data.TargetHealth / data.TargetMaxHealth) : 1f;
            obs[10] = data.SelfIsGrounded ? 1.0f : 0.0f;
            obs[11] = data.TargetIsGrounded ? 1.0f : 0.0f;

            // 4. Combat States
            obs[12] = data.SelfCanAttack ? 1.0f : 0.0f;
            obs[13] = data.TargetIsAttacking ? 1.0f : 0.0f;
            obs[14] = data.TargetIsDashing ? 1.0f : 0.0f;
            obs[15] = data.TargetIsParrying ? 1.0f : 0.0f;
            obs[16] = Mathf.Clamp01(data.SelfSilkOrStamina);

            // 5. Environmental Proximity Rays
            obs[17] = Mathf.Clamp01(data.RayLeftDist / MAX_RAY_DIST);
            obs[18] = Mathf.Clamp01(data.RayRightDist / MAX_RAY_DIST);
            obs[19] = Mathf.Clamp01(data.RayDownDist / MAX_RAY_DIST);
            obs[20] = data.HasSpikesBelow ? 1.0f : 0.0f;
            obs[21] = data.HasHazardAhead ? 1.0f : 0.0f;

            // 6. Projectile & Facing
            obs[22] = Mathf.Clamp01(data.ClosestProjectileDist / MAX_RAY_DIST);
            obs[23] = data.SelfFacingDir >= 0 ? 1.0f : -1.0f;

            return obs;
        }
    }
}

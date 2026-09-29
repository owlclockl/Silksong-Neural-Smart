using System;
using UnityEngine;

namespace SilksongNeuralSmart.Training
{
    /// <summary>
    /// Лёгкая детерминированная 2D-физика для автономных тренировочных арен.
    /// Не зависит от Rigidbody2D игры: режим "Великая Арена" полностью самодостаточен,
    /// поэтому обучение работает даже когда сцена игры не содержит коллайдеров.
    /// </summary>
    public struct ArenaBody
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 HalfSize;
        public bool Grounded;
        public bool IsFlying;

        public static ArenaBody Create(Vector2 position, Vector2 size, bool isFlying)
        {
            return new ArenaBody
            {
                Position = position,
                Velocity = Vector2.zero,
                HalfSize = size * 0.5f,
                Grounded = false,
                IsFlying = isFlying
            };
        }
    }

    public static class ArenaPhysics
    {
        public const float Gravity = 38.0f;
        public const float MaxFallSpeed = 32.0f;
        public const float HazardDamage = 26.0f;
        public const float HazardBounceVelocity = 15.0f;

        /// <summary>
        /// Интегрирует одно тело внутри комнаты: гравитация, коллизии платформ, стен и шипов.
        /// </summary>
        public static void Step(ref ArenaBody body, TrainingRoom room, float deltaTime, out bool hitHazard)
        {
            hitHazard = false;
            if (room == null || deltaTime <= 0f)
                return;

            deltaTime = Mathf.Min(deltaTime, 0.05f); // защита от больших шагов на ускорении времени

            // 1. Гравитация
            if (!body.IsFlying)
            {
                body.Velocity = new Vector2(body.Velocity.x, Mathf.Max(-MaxFallSpeed, body.Velocity.y - Gravity * deltaTime));
            }
            else
            {
                body.Velocity = new Vector2(body.Velocity.x, Mathf.Clamp(body.Velocity.y, -MaxFallSpeed, MaxFallSpeed));
            }

            Vector2 previous = body.Position;
            body.Position += body.Velocity * deltaTime;
            body.Grounded = false;

            // 2. Коллизии с геометрией комнаты
            for (int i = 0; i < room.Platforms.Count; i++)
            {
                PlatformBox platform = room.Platforms[i];
                Vector2 pHalf = platform.Size * 0.5f;

                float overlapX = (body.HalfSize.x + pHalf.x) - Mathf.Abs(body.Position.x - platform.Center.x);
                float overlapY = (body.HalfSize.y + pHalf.y) - Mathf.Abs(body.Position.y - platform.Center.y);

                if (overlapX <= 0f || overlapY <= 0f)
                    continue;

                if (platform.IsHazard)
                {
                    hitHazard = true;
                    body.Velocity = new Vector2(body.Velocity.x, HazardBounceVelocity);
                    body.Position = new Vector2(body.Position.x, platform.Center.y + pHalf.y + body.HalfSize.y + 0.1f);
                    continue;
                }

                if (platform.IsPassThrough)
                {
                    // Проходимые платформы ловят агента только сверху и только при падении
                    float platformTop = platform.Center.y + pHalf.y;
                    bool falling = body.Velocity.y <= 0.01f;
                    bool wasAbove = (previous.y - body.HalfSize.y) >= platformTop - 0.25f;

                    if (falling && wasAbove)
                    {
                        body.Position = new Vector2(body.Position.x, platformTop + body.HalfSize.y);
                        body.Velocity = new Vector2(body.Velocity.x, 0f);
                        body.Grounded = true;
                    }
                    continue;
                }

                // Сплошная геометрия: выталкиваем по оси наименьшего проникновения
                if (overlapY < overlapX)
                {
                    if (body.Position.y >= platform.Center.y)
                    {
                        body.Position = new Vector2(body.Position.x, platform.Center.y + pHalf.y + body.HalfSize.y);
                        if (body.Velocity.y < 0f) body.Velocity = new Vector2(body.Velocity.x, 0f);
                        body.Grounded = true;
                    }
                    else
                    {
                        body.Position = new Vector2(body.Position.x, platform.Center.y - pHalf.y - body.HalfSize.y);
                        if (body.Velocity.y > 0f) body.Velocity = new Vector2(body.Velocity.x, 0f);
                    }
                }
                else
                {
                    if (body.Position.x >= platform.Center.x)
                    {
                        body.Position = new Vector2(platform.Center.x + pHalf.x + body.HalfSize.x, body.Position.y);
                    }
                    else
                    {
                        body.Position = new Vector2(platform.Center.x - pHalf.x - body.HalfSize.x, body.Position.y);
                    }
                    body.Velocity = new Vector2(0f, body.Velocity.y);
                }
            }

            // 3. Границы арены
            float minX = room.BoundsMin.x + body.HalfSize.x;
            float maxX = room.BoundsMax.x - body.HalfSize.x;
            float minY = room.BoundsMin.y + body.HalfSize.y;
            float maxY = room.BoundsMax.y - body.HalfSize.y;

            if (body.Position.x < minX) { body.Position = new Vector2(minX, body.Position.y); body.Velocity = new Vector2(0f, body.Velocity.y); }
            if (body.Position.x > maxX) { body.Position = new Vector2(maxX, body.Position.y); body.Velocity = new Vector2(0f, body.Velocity.y); }
            if (body.Position.y > maxY) { body.Position = new Vector2(body.Position.x, maxY); body.Velocity = new Vector2(body.Velocity.x, 0f); }
            if (body.Position.y < minY)
            {
                body.Position = new Vector2(body.Position.x, minY);
                body.Velocity = new Vector2(body.Velocity.x, 0f);
                body.Grounded = true;
            }
        }

        /// <summary>Дистанция рейкаста по горизонтали до ближайшей сплошной стены (для сенсоров).</summary>
        public static float RayHorizontal(TrainingRoom room, Vector2 origin, int direction, float maxDistance = 15f)
        {
            if (room == null) return maxDistance;

            float best = maxDistance;
            for (int i = 0; i < room.Platforms.Count; i++)
            {
                PlatformBox p = room.Platforms[i];
                if (p.IsPassThrough) continue;

                Vector2 half = p.Size * 0.5f;
                if (origin.y < p.Center.y - half.y || origin.y > p.Center.y + half.y)
                    continue;

                float dist = direction > 0
                    ? (p.Center.x - half.x) - origin.x
                    : origin.x - (p.Center.x + half.x);

                if (dist >= 0f && dist < best)
                    best = dist;
            }

            return Mathf.Clamp(best, 0f, maxDistance);
        }

        /// <summary>Дистанция до поверхности под агентом (для сенсоров и определения ям).</summary>
        public static float RayDown(TrainingRoom room, Vector2 origin, float maxDistance = 15f)
        {
            if (room == null) return maxDistance;

            float best = maxDistance;
            for (int i = 0; i < room.Platforms.Count; i++)
            {
                PlatformBox p = room.Platforms[i];
                if (p.IsHazard) continue;

                Vector2 half = p.Size * 0.5f;
                if (origin.x < p.Center.x - half.x || origin.x > p.Center.x + half.x)
                    continue;

                float dist = origin.y - (p.Center.y + half.y);
                if (dist >= 0f && dist < best)
                    best = dist;
            }

            return Mathf.Clamp(best, 0f, maxDistance);
        }

        /// <summary>Проверяет наличие шипов под агентом (сенсор опасности).</summary>
        public static bool HasHazardBelow(TrainingRoom room, Vector2 origin, float lookAhead = 6f)
        {
            if (room == null) return false;

            for (int i = 0; i < room.Platforms.Count; i++)
            {
                PlatformBox p = room.Platforms[i];
                if (!p.IsHazard) continue;

                Vector2 half = p.Size * 0.5f;
                if (origin.x >= p.Center.x - half.x - 1.0f && origin.x <= p.Center.x + half.x + 1.0f &&
                    origin.y >= p.Center.y && origin.y - p.Center.y <= lookAhead)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

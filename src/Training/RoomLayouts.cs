using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongNeuralSmart.Training
{
    public struct PlatformBox
    {
        public Vector2 Center;
        public Vector2 Size;
        public bool IsHazard; // Spikes / Lava
        public bool IsPassThrough;
    }

    public class TrainingRoom
    {
        public int RoomId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Vector2 BoundsMin { get; set; }
        public Vector2 BoundsMax { get; set; }
        public Vector2 HornetSpawnPoint { get; set; }
        public Vector2 MobSpawnPoint { get; set; }
        public List<PlatformBox> Platforms { get; set; } = new List<PlatformBox>();
    }

    public static class RoomLayouts
    {
        public static TrainingRoom CreateRoom(int roomId)
        {
            switch (roomId)
            {
                case 1:
                    return CreateMossDojo();
                case 2:
                    return CreateVerticalChasm();
                case 3:
                    return CreateCitadelTrial();
                default:
                    return CreateMossDojo();
            }
        }

        private static TrainingRoom CreateMossDojo()
        {
            var room = new TrainingRoom
            {
                RoomId = 1,
                Name = "The Arena of Moss (Flat Duel Dojo)",
                Description = "Balanced ground combat arena with wide floor and two low platforms. Focuses on spacing, basic pokes, and dashes.",
                BoundsMin = new Vector2(-16f, -2f),
                BoundsMax = new Vector2(16f, 12f),
                HornetSpawnPoint = new Vector2(-8f, 0f),
                MobSpawnPoint = new Vector2(8f, 0f)
            };

            // Main Floor
            room.Platforms.Add(new PlatformBox { Center = new Vector2(0f, -1f), Size = new Vector2(34f, 2f), IsHazard = false });
            // Left & Right Walls
            room.Platforms.Add(new PlatformBox { Center = new Vector2(-17f, 6f), Size = new Vector2(2f, 16f), IsHazard = false });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(17f, 6f), Size = new Vector2(2f, 16f), IsHazard = false });
            // Elevated Side Platforms
            room.Platforms.Add(new PlatformBox { Center = new Vector2(-7f, 3.5f), Size = new Vector2(6f, 0.8f), IsPassThrough = true });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(7f, 3.5f), Size = new Vector2(6f, 0.8f), IsPassThrough = true });

            return room;
        }

        private static TrainingRoom CreateVerticalChasm()
        {
            var room = new TrainingRoom
            {
                RoomId = 2,
                Name = "The Vertical Chasm (Acrobatics & Pogo)",
                Description = "Deep chamber with spike pits and tiered stepping platforms. Forces agents to master wall climbing, pogoing, and aerial combat.",
                BoundsMin = new Vector2(-14f, -4f),
                BoundsMax = new Vector2(14f, 18f),
                HornetSpawnPoint = new Vector2(-6f, 2f),
                MobSpawnPoint = new Vector2(6f, 6f)
            };

            // Ground with central spike pit
            room.Platforms.Add(new PlatformBox { Center = new Vector2(-10f, -1f), Size = new Vector2(8f, 2f), IsHazard = false });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(10f, -1f), Size = new Vector2(8f, 2f), IsHazard = false });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(0f, -2f), Size = new Vector2(12f, 1.5f), IsHazard = true }); // Spikes

            // Walls
            room.Platforms.Add(new PlatformBox { Center = new Vector2(-15f, 8f), Size = new Vector2(2f, 22f), IsHazard = false });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(15f, 8f), Size = new Vector2(2f, 22f), IsHazard = false });

            // Tiered Platforms
            room.Platforms.Add(new PlatformBox { Center = new Vector2(-5f, 4f), Size = new Vector2(5f, 0.8f), IsPassThrough = true });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(5f, 7f), Size = new Vector2(5f, 0.8f), IsPassThrough = true });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(0f, 11f), Size = new Vector2(6f, 0.8f), IsPassThrough = true });

            return room;
        }

        private static TrainingRoom CreateCitadelTrial()
        {
            var room = new TrainingRoom
            {
                RoomId = 3,
                Name = "The Citadel Trial (Dynamic Hazards & Obstacles)",
                Description = "Multi-level complex fortress with moving hazards, narrow choke points, and vertical pillars. Trains elite evasive maneuvers.",
                BoundsMin = new Vector2(-18f, -2f),
                BoundsMax = new Vector2(18f, 14f),
                HornetSpawnPoint = new Vector2(-10f, 0f),
                MobSpawnPoint = new Vector2(10f, 0f)
            };

            room.Platforms.Add(new PlatformBox { Center = new Vector2(0f, -1f), Size = new Vector2(38f, 2f), IsHazard = false });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(-19f, 6f), Size = new Vector2(2f, 16f), IsHazard = false });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(19f, 6f), Size = new Vector2(2f, 16f), IsHazard = false });

            // Pillars
            room.Platforms.Add(new PlatformBox { Center = new Vector2(-4f, 2f), Size = new Vector2(2f, 4f), IsHazard = false });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(4f, 2f), Size = new Vector2(2f, 4f), IsHazard = false });
            room.Platforms.Add(new PlatformBox { Center = new Vector2(0f, 6f), Size = new Vector2(8f, 0.8f), IsPassThrough = true });

            return room;
        }
    }
}

using System;
using HarmonyLib;
using UnityEngine;
using SilksongNeuralSmart.Agents;

namespace SilksongNeuralSmart.Patches
{
    public static class HeroControllerPatch
    {
        public static bool IsHornetNeuralControlled = false;
        private static HornetAgent? _attachedHornetAgent;

        public static void ToggleHornetAI()
        {
            IsHornetNeuralControlled = !IsHornetNeuralControlled;
            Debug.Log($"[SilksongNeuralSmart] Autonomous Hornet AI: {(IsHornetNeuralControlled ? "ENABLED" : "DISABLED")}");
        }

        public static void AttachToHero(GameObject heroObject)
        {
            if (heroObject == null) return;

            _attachedHornetAgent = heroObject.GetComponent<HornetAgent>();
            if (_attachedHornetAgent == null)
            {
                _attachedHornetAgent = heroObject.AddComponent<HornetAgent>();
            }
        }
    }
}

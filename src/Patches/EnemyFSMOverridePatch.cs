using System;
using HarmonyLib;
using UnityEngine;
using SilksongNeuralSmart.Agents;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.Patches
{
    /// <summary>
    /// Harmony hooks that intercept PlayMakerFSM or EnemyController in Hollow Knight: Silksong
    /// </summary>
    public static class EnemyFSMOverridePatch
    {
        public static bool IsNeuralAIEnabled = true;

        public static void PatchAll(Harmony harmony)
        {
            try
            {
                // Hooks dynamically search for game types (e.g. PlayMakerFSM, HealthManager, EnemyController)
                Debug.Log("[SilksongNeuralSmart] Registering Harmony enemy AI neural hooks...");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SilksongNeuralSmart] Error patching Enemy FSM: " + ex.Message);
            }
        }

        /// <summary>
        /// Called when an enemy spawns in the main campaign / adventure mode
        /// </summary>
        public static void AttachNeuralControllerToEnemy(GameObject enemyObj, string archetype)
        {
            if (!IsNeuralAIEnabled || enemyObj == null) return;

            MobAgent? existing = enemyObj.GetComponent<MobAgent>();
            if (existing != null) return;

            MobAgent agent;
            if (archetype.Contains("Fly") || archetype.Contains("Wing"))
                agent = enemyObj.AddComponent<Agents.Archetypes.FlyingHunterAgent>();
            else if (archetype.Contains("Knight") || archetype.Contains("Shield") || archetype.Contains("Guard"))
                agent = enemyObj.AddComponent<Agents.Archetypes.ShieldKnightAgent>();
            else if (archetype.Contains("Assassin") || archetype.Contains("Weaver") || archetype.Contains("Shadow"))
                agent = enemyObj.AddComponent<Agents.Archetypes.AssassinWeaverAgent>();
            else
                agent = enemyObj.AddComponent<Agents.Archetypes.ScoutGruntAgent>();

            Debug.Log($"[SilksongNeuralSmart] Attached Neural Brain to {enemyObj.name} ({agent.ArchetypeName})");
        }
    }
}

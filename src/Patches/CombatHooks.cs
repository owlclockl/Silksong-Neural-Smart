using System;
using UnityEngine;
using SilksongNeuralSmart.Agents;
using SilksongNeuralSmart.Training;

namespace SilksongNeuralSmart.Patches
{
    public static class CombatHooks
    {
        public static void OnDamageInflicted(GameObject attacker, GameObject victim, float damageAmount, bool isPogo)
        {
            if (attacker == null || victim == null) return;

            var hornet = attacker.GetComponent<HornetAgent>();
            if (hornet != null)
            {
                hornet.OnAttackLanded(damageAmount, isPogo);
            }

            var mob = attacker.GetComponent<MobAgent>();
            if (mob != null)
            {
                mob.OnAttackLanded(damageAmount);
            }

            var victimHornet = victim.GetComponent<HornetAgent>();
            if (victimHornet != null)
            {
                victimHornet.TakeDamage(damageAmount);
            }

            var victimMob = victim.GetComponent<MobAgent>();
            if (victimMob != null)
            {
                victimMob.TakeDamage(damageAmount);
            }
        }
    }
}

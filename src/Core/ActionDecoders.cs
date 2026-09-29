using System;

namespace SilksongNeuralSmart.Core
{
    public enum CombatAction
    {
        MoveLeft = 0,
        MoveRight = 1,
        IdleSpacing = 2,
        Jump = 3,
        Dash = 4,
        AttackSlash = 5,
        SpecialSkill = 6,
        PogoDownSlash = 7,
        ParryBlock = 8,
        SilkHealOrFocus = 9
    }

    public struct DecodedAction
    {
        public float MoveX; // -1.0 (Left), 0.0 (Idle), +1.0 (Right)
        public bool JumpPressed;
        public bool DashPressed;
        public bool AttackPressed;
        public bool SpecialPressed;
        public bool DownAttackPressed;
        public bool ParryPressed;
        public bool HealPressed;

        public CombatAction PrimaryAction;
        public float Confidence;
    }

    public static class ActionDecoders
    {
        public const int OUTPUT_ACTION_COUNT = 10;

        public static DecodedAction Decode(float[] actionProbabilities, float randomness = 0.0f)
        {
            var action = new DecodedAction();
            if (actionProbabilities == null || actionProbabilities.Length < OUTPUT_ACTION_COUNT)
                return action;

            // Argmax or sample
            int chosenIdx = 0;
            float maxProb = actionProbabilities[0];

            for (int i = 1; i < actionProbabilities.Length; i++)
            {
                if (actionProbabilities[i] > maxProb)
                {
                    maxProb = actionProbabilities[i];
                    chosenIdx = i;
                }
            }

            action.PrimaryAction = (CombatAction)chosenIdx;
            action.Confidence = maxProb;

            // Map action enum to physical input commands
            switch (action.PrimaryAction)
            {
                case CombatAction.MoveLeft:
                    action.MoveX = -1.0f;
                    break;
                case CombatAction.MoveRight:
                    action.MoveX = 1.0f;
                    break;
                case CombatAction.IdleSpacing:
                    action.MoveX = 0.0f;
                    break;
                case CombatAction.Jump:
                    action.JumpPressed = true;
                    break;
                case CombatAction.Dash:
                    action.DashPressed = true;
                    break;
                case CombatAction.AttackSlash:
                    action.AttackPressed = true;
                    break;
                case CombatAction.SpecialSkill:
                    action.SpecialPressed = true;
                    break;
                case CombatAction.PogoDownSlash:
                    action.DownAttackPressed = true;
                    break;
                case CombatAction.ParryBlock:
                    action.ParryPressed = true;
                    break;
                case CombatAction.SilkHealOrFocus:
                    action.HealPressed = true;
                    break;
            }

            return action;
        }
    }
}

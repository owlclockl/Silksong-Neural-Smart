using System;
using UnityEngine;
using SilksongNeuralSmart.Training;
using SilksongNeuralSmart.Core;

namespace SilksongNeuralSmart.UI
{
    public class TrainingHUD : MonoBehaviour
    {
        public bool Visible { get; set; } = true;
        public bool ShowNetworkVisualizer { get; set; } = true;
        public bool ShowSensorsOverlay { get; set; } = true;

        private Rect _windowRect = new Rect(20, 20, 420, 680);
        private Rect _netVisRect = new Rect(450, 20, 360, 480);

        private GUIStyle? _headerStyle;
        private GUIStyle? _labelStyle;
        private GUIStyle? _boxStyle;

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.4f);

                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12
                };
                _labelStyle.normal.textColor = Color.white;

                _boxStyle = new GUIStyle(GUI.skin.box);
            }
        }

        private void OnGUI()
        {
            if (!Visible) return;

            // Внутри отдельного режима "Великая Арена" работает только его собственный интерфейс
            var grandArena = GrandArenaMode.Instance;
            if (grandArena != null && grandArena.IsActive) return;

            InitStyles();

            _windowRect = GUI.Window(9876, _windowRect, DrawMainWindow, "SILKSONG NEURAL SMART - DOJO");

            if (ShowNetworkVisualizer)
            {
                _netVisRect = GUI.Window(9877, _netVisRect, DrawNeuralVisualizerWindow, "LIVE NEURAL NETWORK ACTIVATIONS");
            }
        }

        private void DrawMainWindow(int windowId)
        {
            var mgr = TrainingArenaManager.Instance;
            if (mgr == null)
            {
                GUILayout.Label("Initializing Training Arena...", _labelStyle);
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginVertical("box");
            GUILayout.Label("=== TRAINING STATUS ===", _headerStyle);
            GUILayout.Label($"State: {(mgr.IsTrainingActive ? "<color=#44ff44>TRAINING ACTIVE</color>" : "<color=#ffaa44>PAUSED</color>")}", _labelStyle);
            GUILayout.Label($"Room: <b>{mgr.ActiveRoom?.Name ?? "Dojo"}</b>", _labelStyle);
            GUILayout.Label($"Active Mob: <b>{mgr.ActiveMobType}</b>", _labelStyle);
            GUILayout.Label($"Generation: <b>{mgr.HornetTrainer?.Generation ?? 1}</b>  |  Episode: <b>{mgr.TotalEpisodes}</b>", _labelStyle);
            GUILayout.Label($"Episode Time: <b>{mgr.EpisodeTimer:F1}s / {mgr.MaxEpisodeDuration:F0}s</b>", _labelStyle);
            GUILayout.Label($"Decisions / Sec: <b>{mgr.DecisionsPerSecond}</b> (Latency: <b>~0.018ms</b>)", _labelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Win rate & metrics
            GUILayout.BeginVertical("box");
            GUILayout.Label("=== COMBAT METRICS ===", _headerStyle);
            GUILayout.Label($"Hornet Wins: <color=#66ddff>{mgr.HornetWins}</color> ({mgr.HornetWinRate:F1}%)", _labelStyle);
            GUILayout.Label($"Mob Wins:    <color=#ff6666>{mgr.MobWins}</color> ({mgr.MobWinRate:F1}%)", _labelStyle);
            GUILayout.Label($"Draws / Timeouts: {mgr.Draws}", _labelStyle);

            if (mgr.Hornet != null)
            {
                GUILayout.Label($"Hornet HP: {mgr.Hornet.Health:F0}/{mgr.Hornet.MaxHealth:F0} | Silk: {mgr.Hornet.SilkMeter * 100f:F0}%", _labelStyle);
                GUILayout.Label($"Hornet Dmg Dealt: {mgr.Hornet.TotalDamageDealt:F0} | Pogos: {mgr.Hornet.SuccessfulPogos}", _labelStyle);
            }
            if (mgr.ActiveMob != null)
            {
                GUILayout.Label($"Mob HP: {mgr.ActiveMob.Health:F0}/{mgr.ActiveMob.MaxHealth:F0} | Stamina: {mgr.ActiveMob.Balancer.CurrentStamina * 100f:F0}%", _labelStyle);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Simulation Speed Controls
            GUILayout.BeginVertical("box");
            GUILayout.Label("=== SIMULATION SPEED ===", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x")) mgr.SetTimeScale(1.0f);
            if (GUILayout.Button("2x")) mgr.SetTimeScale(2.0f);
            if (GUILayout.Button("5x")) mgr.SetTimeScale(5.0f);
            if (GUILayout.Button("10x")) mgr.SetTimeScale(10.0f);
            if (GUILayout.Button("25x")) mgr.SetTimeScale(25.0f);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Current Speed: <b>{mgr.TimeScale:F1}x</b>", _labelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Room Selector
            GUILayout.BeginVertical("box");
            GUILayout.Label("=== SELECT TRAINING ARENA ===", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1: Moss Dojo")) mgr.LoadRoom(1);
            if (GUILayout.Button("2: Vertical Chasm")) mgr.LoadRoom(2);
            if (GUILayout.Button("3: Citadel Trial")) mgr.LoadRoom(3);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Mob Archetype Selector
            GUILayout.BeginVertical("box");
            GUILayout.Label("=== SPAWN MOB ARCHETYPE ===", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Grunt")) mgr.SetMobArchetype("Grunt");
            if (GUILayout.Button("Flying")) mgr.SetMobArchetype("Flying");
            if (GUILayout.Button("Knight")) mgr.SetMobArchetype("Knight");
            if (GUILayout.Button("Assassin")) mgr.SetMobArchetype("Assassin");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Action Buttons
            GUILayout.BeginHorizontal();
            if (!mgr.IsTrainingActive)
            {
                if (GUILayout.Button("<color=#44ff44>START TRAINING</color>")) mgr.StartTraining();
            }
            else
            {
                if (GUILayout.Button("<color=#ffaa44>PAUSE</color>")) mgr.PauseTraining();
            }

            if (GUILayout.Button("Reset Episode")) mgr.ResetEpisode();
            if (GUILayout.Button("Save Brains")) mgr.SaveBrains(Application.persistentDataPath + "/SilksongBrains");
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void DrawNeuralVisualizerWindow(int windowId)
        {
            var mgr = TrainingArenaManager.Instance;
            if (mgr == null || mgr.Hornet?.Brain == null)
            {
                GUILayout.Label("No active neural brain to visualize.", _labelStyle);
                GUI.DragWindow();
                return;
            }

            var brain = mgr.Hornet.Brain;
            GUILayout.Label($"Architecture: {string.Join(" → ", brain.LayerSizes)}", _labelStyle);

            // Draw layer columns
            float startX = 20f;
            float startY = 40f;
            float layerSpacing = 75f;
            float nodeRadius = 5f;

            if (Event.current.type == EventType.Repaint)
            {
                for (int l = 0; l < brain.LayerSizes.Length; l++)
                {
                    int count = Math.Min(brain.LayerSizes[l], 16); // render up to 16 nodes per layer
                    float colX = startX + l * layerSpacing;
                    float nodeSpacing = 380f / (count + 1);

                    for (int n = 0; n < count; n++)
                    {
                        float nodeY = startY + (n + 1) * nodeSpacing;
                        float actVal = (brain.Activations != null && l < brain.Activations.Length && n < brain.Activations[l].Length)
                            ? Mathf.Clamp01(Math.Abs(brain.Activations[l][n]))
                            : 0.5f;

                        Color nodeColor = Color.Lerp(new Color(0.2f, 0.2f, 0.4f), new Color(0.2f, 1.0f, 0.4f), actVal);
                        DrawCircle(new Vector2(colX, nodeY), nodeRadius, nodeColor);
                    }
                }
            }

            GUILayout.Space(390);

            // Draw action names & probabilities
            GUILayout.Label("<b>Output Action Distribution:</b>", _labelStyle);
            if (brain.Activations != null && brain.Activations.Length > 0)
            {
                float[] outLayer = brain.Activations[brain.Activations.Length - 1];
                string[] actionNames = new[] { "Left", "Right", "Idle", "Jump", "Dash", "Slash", "Skill", "Pogo", "Parry", "Heal" };
                for (int i = 0; i < outLayer.Length && i < actionNames.Length; i++)
                {
                    float pct = outLayer[i] * 100f;
                    GUILayout.Label($"{actionNames[i],-6}: [{new string('|', Mathf.Clamp((int)(pct / 5f), 0, 20)),-20}] {pct:F1}%", _labelStyle);
                }
            }

            GUI.DragWindow();
        }

        private void DrawCircle(Vector2 center, float radius, Color color)
        {
            // Lightweight pixel box approximation for Unity IMGUI
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}

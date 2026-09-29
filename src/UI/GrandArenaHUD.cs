using System;
using System.Collections.Generic;
using UnityEngine;
using SilksongNeuralSmart.Agents;
using SilksongNeuralSmart.Core;
using SilksongNeuralSmart.Saves;
using SilksongNeuralSmart.Training;

namespace SilksongNeuralSmart.UI
{
    /// <summary>
    /// Интерфейс отдельного режима "Великая Арена":
    /// живой вид арены (все мобы + Хорнет), статистика обучения,
    /// управление скоростью, сохранение/загрузка слотов и выход в главное меню.
    /// </summary>
    public class GrandArenaHUD : MonoBehaviour
    {
        public static GrandArenaHUD Instance { get; private set; } = null!;

        public bool Visible { get; set; } = true;
        public bool ShowViewport { get; set; } = true;
        public bool ShowBrainPanel { get; set; } = true;

        private Rect _statsRect = new Rect(20, 20, 400, 560);
        private Rect _viewportRect = new Rect(440, 20, 820, 420);
        private Rect _brainRect = new Rect(440, 452, 820, 210);

        private GUIStyle? _header;
        private GUIStyle? _label;
        private GUIStyle? _small;
        private Texture2D? _bg;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void InitStyles()
        {
            if (_header != null) return;

            _bg = MainMenuInjector.MakeTexture(new Color(0.04f, 0.05f, 0.08f, 0.95f));

            _header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter
            };
            _header.normal.textColor = new Color(0.98f, 0.85f, 0.45f);

            _label = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true };
            _label.normal.textColor = new Color(0.92f, 0.94f, 0.97f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 10, richText = true, wordWrap = true };
            _small.normal.textColor = new Color(0.68f, 0.74f, 0.82f);
        }

        private void OnGUI()
        {
            var mode = GrandArenaMode.Instance;
            if (mode == null || !mode.IsActive || !Visible) return;

            InitStyles();
            LayoutWindowsForResolution();

            _statsRect = GUI.Window(9881, _statsRect, DrawStatsWindow, "");
            if (ShowViewport)
                _viewportRect = GUI.Window(9882, _viewportRect, DrawViewportWindow, "");
            if (ShowBrainPanel)
                _brainRect = GUI.Window(9883, _brainRect, DrawBrainWindow, "");
        }

        private void LayoutWindowsForResolution()
        {
            float maxWidth = Screen.width - 40f;
            if (_viewportRect.xMax > maxWidth)
            {
                float width = Mathf.Max(420f, maxWidth - _viewportRect.x);
                _viewportRect.width = width;
                _brainRect.width = width;
            }
        }

        // ==================================================================
        // Окно статистики и управления
        // ==================================================================
        private void DrawStatsWindow(int id)
        {
            var mode = GrandArenaMode.Instance;
            GUI.DrawTexture(new Rect(0, 0, _statsRect.width, _statsRect.height), _bg!, ScaleMode.StretchToFill);
            MainMenuInjector.DrawBorder(new Rect(0, 0, _statsRect.width, _statsRect.height), new Color(0.85f, 0.7f, 0.35f, 0.9f), 2f);

            GUILayout.BeginArea(new Rect(12, 10, _statsRect.width - 24, _statsRect.height - 20));

            GUILayout.Label("⚔ ВЕЛИКАЯ АРЕНА — РЕЖИМ ОБУЧЕНИЯ", _header);
            GUILayout.Label($"<b>Слот {mode.ActiveSlot}</b> · {mode.ProfileName} · " +
                            (mode.IsRunning ? "<color=#5ce65c>ИДЁТ ОБУЧЕНИЕ</color>" : "<color=#ffb84d>ПАУЗА</color>"), _label);

            GUILayout.Space(4);
            GUILayout.BeginVertical("box");
            GUILayout.Label($"Волна: <b>{mode.Wave}</b> (рекорд {mode.BestWave}) · В стае: <b>{mode.Pack.Count}</b> · Живых: <b>{mode.AliveMobCount}</b>", _label);
            GUILayout.Label($"Эпизод: <b>{mode.TotalEpisodes}</b> · Время раунда: <b>{mode.EpisodeTimer:F1}</b>/{mode.MaxEpisodeDuration:F0} с", _label);
            GUILayout.Label($"Решений в секунду: <b>{mode.DecisionsPerSecond}</b> · Скорость: <b>{mode.TimeScale:F1}x</b>", _label);
            GUILayout.Label($"В режиме проведено: <b>{FormatTime(mode.PlayTimeSeconds)}</b>", _label);
            GUILayout.EndVertical();

            GUILayout.Space(4);
            GUILayout.BeginVertical("box");
            GUILayout.Label("=== РЕЗУЛЬТАТЫ ===", _header);
            GUILayout.Label($"Победы Хорнет: <color=#66ddff>{mode.HornetWins}</color> · Победы стаи: <color=#ff6b6b>{mode.PackWins}</color> · Ничьи: {mode.Draws}", _label);
            GUILayout.Label($"Мобов повержено всего: <b>{mode.TotalMobsDefeated}</b>", _label);
            GUILayout.Label($"Фитнес прошлого раунда — Хорнет: <b>{mode.LastHornetFitness:F0}</b>, стая: <b>{mode.LastPackFitness:F0}</b>", _label);

            if (mode.Hornet != null)
            {
                GUILayout.Label($"Хорнет HP: <b>{mode.Hornet.Health:F0}/{mode.Hornet.MaxHealth:F0}</b> · Шёлк: <b>{mode.Hornet.SilkMeter * 100f:F0}%</b>", _label);
            }
            GUILayout.EndVertical();

            GUILayout.Space(4);
            GUILayout.BeginVertical("box");
            GUILayout.Label("=== ЭВОЛЮЦИЯ ПОПУЛЯЦИЙ ===", _header);
            GUILayout.Label($"Хорнет — поколение <b>{mode.HornetTrainer.Generation}</b>, лучший фитнес <b>{SafeFitness(mode.HornetTrainer.BestFitnessEver):F0}</b>", _label);

            for (int i = 0; i < GrandArenaMode.ArchetypeKeys.Length; i++)
            {
                string key = GrandArenaMode.ArchetypeKeys[i];
                if (!mode.MobTrainers.TryGetValue(key, out GeneticTrainer trainer)) continue;

                Color c = GrandArenaMode.GetArchetypeColor(key);
                string hex = ColorUtility.ToHtmlStringRGB(c);
                GUILayout.Label($"<color=#{hex}>■</color> {GrandArenaMode.GetArchetypeTitle(key)} — поколение <b>{trainer.Generation}</b>, фитнес <b>{SafeFitness(trainer.BestFitnessEver):F0}</b>", _label);
            }
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // --- Скорость симуляции ---
            GUILayout.Label("СКОРОСТЬ СИМУЛЯЦИИ", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x")) mode.SetTimeScale(1f);
            if (GUILayout.Button("2x")) mode.SetTimeScale(2f);
            if (GUILayout.Button("5x")) mode.SetTimeScale(5f);
            if (GUILayout.Button("10x")) mode.SetTimeScale(10f);
            if (GUILayout.Button("25x")) mode.SetTimeScale(25f);
            GUILayout.EndHorizontal();

            // --- Размер стаи ---
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Мобов в стае: <b>{mode.MobCount}</b>", _label, GUILayout.Width(150));
            if (GUILayout.Button("−", GUILayout.Width(32))) mode.SetMobCount(mode.MobCount - 1);
            if (GUILayout.Button("+", GUILayout.Width(32))) mode.SetMobCount(mode.MobCount + 1);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(mode.PlayerControlsHornet ? "🎮 Игрок" : "🤖 ИИ", GUILayout.Width(90)))
                mode.PlayerControlsHornet = !mode.PlayerControlsHornet;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // --- Управление режимом ---
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(mode.IsRunning ? "⏸ Пауза" : "▶ Продолжить", GUILayout.Height(28))) mode.TogglePause();
            if (GUILayout.Button("↺ Новый раунд", GUILayout.Height(28))) mode.ResetEpisode();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("💾 Сохранить", GUILayout.Height(26))) mode.SaveCurrentSlot();
            for (int slot = 1; slot <= GrandArenaSaveSystem.SLOT_COUNT; slot++)
            {
                if (GUILayout.Button($"📂 {slot}", GUILayout.Height(26), GUILayout.Width(48)))
                    mode.LoadSlot(slot);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ShowViewport = GUILayout.Toggle(ShowViewport, " Арена", GUILayout.Width(90));
            ShowBrainPanel = GUILayout.Toggle(ShowBrainPanel, " Нейросеть", GUILayout.Width(110));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("⏏ В главное меню", GUILayout.Height(26)))
            {
                mode.StopMode(true);
                MainMenuInjector.Instance?.RefreshSlots();
            }
            GUILayout.EndHorizontal();

            if (mode.StatusMessageTime > 0f && !string.IsNullOrEmpty(mode.StatusMessage))
            {
                GUILayout.Label($"<color=#ffd166>{mode.StatusMessage}</color>", _label);
            }

            GUILayout.Label($"<size=10>F6 — меню режима · F5 — быстрое сохранение · Page Up/Down — скорость</size>", _small);

            GUILayout.EndArea();
            GUI.DragWindow(new Rect(0, 0, _statsRect.width, 26));
        }

        // ==================================================================
        // Живой вид арены
        // ==================================================================
        private void DrawViewportWindow(int id)
        {
            var mode = GrandArenaMode.Instance;
            var room = mode.Arena;

            var full = new Rect(0, 0, _viewportRect.width, _viewportRect.height);
            GUI.DrawTexture(full, _bg!, ScaleMode.StretchToFill);
            MainMenuInjector.DrawBorder(full, new Color(0.3f, 0.36f, 0.45f, 0.9f), 2f);

            GUI.Label(new Rect(10, 4, _viewportRect.width - 20, 20),
                $"<b>{room.Name}</b>   <size=10>(живая симуляция: {mode.Pack.Count} мобов + Хорнет)</size>", _label);

            var view = new Rect(8, 26, _viewportRect.width - 16, _viewportRect.height - 34);
            GUI.BeginGroup(view);

            float arenaW = room.BoundsMax.x - room.BoundsMin.x;
            float arenaH = room.BoundsMax.y - room.BoundsMin.y;
            float scale = Mathf.Min(view.width / arenaW, view.height / arenaH);
            float offsetX = (view.width - arenaW * scale) * 0.5f;
            float offsetY = (view.height - arenaH * scale) * 0.5f;

            Func<Vector2, Vector2> toScreen = world => new Vector2(
                offsetX + (world.x - room.BoundsMin.x) * scale,
                offsetY + (room.BoundsMax.y - world.y) * scale);

            // Фон арены
            DrawRect(new Rect(offsetX, offsetY, arenaW * scale, arenaH * scale), new Color(0.07f, 0.09f, 0.13f, 1f));

            // Платформы и шипы
            for (int i = 0; i < room.Platforms.Count; i++)
            {
                PlatformBox p = room.Platforms[i];
                Vector2 topLeft = toScreen(new Vector2(p.Center.x - p.Size.x * 0.5f, p.Center.y + p.Size.y * 0.5f));
                var r = new Rect(topLeft.x, topLeft.y, p.Size.x * scale, p.Size.y * scale);

                if (p.IsHazard)
                    DrawRect(r, new Color(0.85f, 0.18f, 0.18f, 0.9f));
                else if (p.IsPassThrough)
                    DrawRect(r, new Color(0.44f, 0.5f, 0.58f, 0.9f));
                else
                    DrawRect(r, new Color(0.18f, 0.21f, 0.25f, 1f));
            }

            // Мобы
            for (int i = 0; i < mode.Pack.Count; i++)
            {
                PackMember m = mode.Pack[i];
                if (m.Agent == null) continue;

                Vector2 size = m.Agent.ColliderSize;
                Vector2 tl = toScreen(new Vector2(m.Body.Position.x - size.x * 0.5f, m.Body.Position.y + size.y * 0.5f));
                var r = new Rect(tl.x, tl.y, size.x * scale, size.y * scale);

                if (!m.Alive)
                {
                    DrawRect(r, new Color(0.25f, 0.25f, 0.28f, 0.45f));
                    continue;
                }

                Color tint = m.Tint;
                if (m.Agent.IsAttacking) tint = Color.Lerp(tint, Color.white, 0.55f);
                DrawRect(r, tint);

                if (m.Agent.IsParrying)
                    MainMenuInjector.DrawBorder(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(0f, 0.82f, 0.82f, 0.9f), 2f);

                // Полоса здоровья
                float hp = Mathf.Clamp01(m.Agent.Health / Mathf.Max(1f, m.Agent.MaxHealth));
                var hb = new Rect(r.x, r.y - 6f, r.width, 3f);
                DrawRect(hb, new Color(0.1f, 0.1f, 0.12f, 0.9f));
                DrawRect(new Rect(hb.x, hb.y, hb.width * hp, hb.height), new Color(0.3f, 0.85f, 0.3f, 0.95f));
            }

            // Хорнет
            if (mode.Hornet != null)
            {
                Vector2 hSize = mode.Hornet.ColliderSize;
                Vector2 tl = toScreen(new Vector2(mode.HornetPosition.x - hSize.x * 0.5f, mode.HornetPosition.y + hSize.y * 0.5f));
                var r = new Rect(tl.x, tl.y, hSize.x * scale, hSize.y * scale);

                Color hornetColor = new Color(0.91f, 0.25f, 0.09f);
                if (mode.Hornet.IsDashing) hornetColor = new Color(1f, 0.75f, 0.35f);
                if (mode.Hornet.IsAttacking) hornetColor = Color.Lerp(hornetColor, Color.white, 0.5f);
                DrawRect(r, hornetColor);
                MainMenuInjector.DrawBorder(r, Color.white, 1f);

                float hp = Mathf.Clamp01(mode.Hornet.Health / Mathf.Max(1f, mode.Hornet.MaxHealth));
                var hb = new Rect(r.x - 4f, r.y - 8f, r.width + 8f, 4f);
                DrawRect(hb, new Color(0.1f, 0.1f, 0.12f, 0.9f));
                DrawRect(new Rect(hb.x, hb.y, hb.width * hp, hb.height), new Color(0.95f, 0.35f, 0.25f, 0.95f));

                var sb = new Rect(hb.x, hb.y + 5f, hb.width, 3f);
                DrawRect(sb, new Color(0.1f, 0.1f, 0.12f, 0.9f));
                DrawRect(new Rect(sb.x, sb.y, sb.width * Mathf.Clamp01(mode.Hornet.SilkMeter), sb.height), new Color(0.95f, 0.95f, 1f, 0.9f));
            }

            GUI.EndGroup();
            GUI.DragWindow(new Rect(0, 0, _viewportRect.width, 24));
        }

        // ==================================================================
        // Панель нейросети и графика обучения
        // ==================================================================
        private void DrawBrainWindow(int id)
        {
            var mode = GrandArenaMode.Instance;
            var full = new Rect(0, 0, _brainRect.width, _brainRect.height);
            GUI.DrawTexture(full, _bg!, ScaleMode.StretchToFill);
            MainMenuInjector.DrawBorder(full, new Color(0.3f, 0.36f, 0.45f, 0.9f), 2f);

            GUI.Label(new Rect(10, 4, 380, 20), "<b>🧠 НЕЙРОСЕТЬ ХОРНЕТ (live)</b>", _label);
            GUI.Label(new Rect(_brainRect.width * 0.5f + 10, 4, 380, 20), "<b>📈 ЭВОЛЮЦИЯ ФИТНЕСА</b>", _label);

            // --- Вероятности действий Хорнет ---
            NeuralNetwork? brain = mode.Hornet != null ? mode.Hornet.Brain : null;
            string[] actionNames = { "Влево", "Вправо", "Стоять", "Прыжок", "Рывок", "Удар", "Скилл", "Пого", "Парир.", "Лечение" };

            if (brain != null && brain.Activations != null && brain.Activations.Length > 0)
            {
                float[] outputs = brain.Activations[brain.Activations.Length - 1];
                float y = 26f;
                for (int i = 0; i < outputs.Length && i < actionNames.Length; i++)
                {
                    float pct = Mathf.Clamp01(outputs[i]);
                    GUI.Label(new Rect(12, y, 70, 16), $"<size=10>{actionNames[i]}</size>", _small);
                    var bar = new Rect(84, y + 3, _brainRect.width * 0.5f - 150, 9);
                    DrawRect(bar, new Color(0.15f, 0.17f, 0.21f, 1f));
                    DrawRect(new Rect(bar.x, bar.y, bar.width * pct, bar.height), new Color(0.29f, 0.81f, 0.98f, 0.95f));
                    GUI.Label(new Rect(bar.xMax + 6, y, 54, 16), $"<size=10>{pct * 100f:F1}%</size>", _small);
                    y += 16f;
                }
            }

            // --- График фитнеса ---
            var chart = new Rect(_brainRect.width * 0.5f + 10, 26, _brainRect.width * 0.5f - 22, _brainRect.height - 40);
            DrawRect(chart, new Color(0.08f, 0.09f, 0.12f, 1f));
            MainMenuInjector.DrawBorder(chart, new Color(0.25f, 0.29f, 0.35f, 0.8f), 1f);

            DrawSeries(chart, mode.HornetFitnessLog, new Color(0.29f, 0.81f, 0.98f));
            DrawSeries(chart, mode.PackFitnessLog, new Color(1f, 0.42f, 0.42f));

            GUI.Label(new Rect(chart.x + 6, chart.yMax - 16, chart.width - 12, 16),
                "<size=10><color=#4bcffa>■ Хорнет</color>   <color=#ff6b6b>■ Стая мобов</color></size>", _small);

            GUI.DragWindow(new Rect(0, 0, _brainRect.width, 24));
        }

        private void DrawSeries(Rect area, List<float> data, Color color)
        {
            if (data == null || data.Count < 2) return;

            float max = 1f;
            for (int i = 0; i < data.Count; i++)
                if (data[i] > max) max = data[i];

            int count = data.Count;
            float stepX = area.width / Mathf.Max(1, count - 1);

            for (int i = 0; i < count; i++)
            {
                float v = Mathf.Clamp01(data[i] / max);
                float x = area.x + i * stepX;
                float h = Mathf.Max(1f, v * (area.height - 20f));
                DrawRect(new Rect(x, area.yMax - 18f - h, Mathf.Max(1f, stepX * 0.7f), h), new Color(color.r, color.g, color.b, 0.75f));
            }
        }

        // ==================================================================
        private static void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static float SafeFitness(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= float.MinValue * 0.5f ? 0f : value;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            return h > 0 ? $"{h}ч {m:00}м {s:00}с" : $"{m}м {s:00}с";
        }
    }
}

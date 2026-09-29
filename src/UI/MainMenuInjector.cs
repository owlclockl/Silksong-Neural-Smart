using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SilksongNeuralSmart.Saves;
using SilksongNeuralSmart.Training;

namespace SilksongNeuralSmart.UI
{
    /// <summary>
    /// Встраивает кнопку "НЕЙРО-АРЕНА" в ГЛАВНОЕ МЕНЮ игры.
    ///
    /// Кнопка запускает отдельный режим "Великая Арена" — одновременное обучение
    /// боя всех мобов и Хорнет на одной большой арене, со своими слотами сохранений.
    ///
    /// Реализация намеренно сделана на Unity IMGUI: она не зависит от внутренних
    /// UI-префабов Silksong и продолжает работать после обновлений игры.
    /// </summary>
    public class MainMenuInjector : MonoBehaviour
    {
        public static MainMenuInjector Instance { get; private set; } = null!;

        public bool ButtonEnabled { get; set; } = true;
        public bool ForceAlwaysVisible { get; set; }
        public bool PanelOpen { get; private set; }

        public string[] MenuSceneKeywords { get; set; } = { "menu", "title", "start", "intro", "logo", "quit" };

        private bool _isMenuScene;
        private string _activeSceneName = "";
        private float _sceneCheckTimer;
        private float _slotRefreshTimer;
        private List<GrandArenaSlotInfo> _slots = new List<GrandArenaSlotInfo>();
        private int _selectedSlot = 1;

        private Rect _panelRect = new Rect(0, 0, 720, 560);
        private Vector2 _slotScroll;

        private GUIStyle? _titleStyle;
        private GUIStyle? _subtitleStyle;
        private GUIStyle? _slotTitleStyle;
        private GUIStyle? _labelStyle;
        private Texture2D? _menuButtonBg;
        private Texture2D? _menuButtonHover;
        private Texture2D? _panelBg;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            RefreshSlots();
        }

        private void Update()
        {
            _sceneCheckTimer -= Time.unscaledDeltaTime;
            if (_sceneCheckTimer <= 0f)
            {
                _sceneCheckTimer = 0.5f;
                DetectMenuScene();
            }

            if (PanelOpen)
            {
                _slotRefreshTimer -= Time.unscaledDeltaTime;
                if (_slotRefreshTimer <= 0f)
                {
                    _slotRefreshTimer = 2f;
                    RefreshSlots();
                }
            }
        }

        private void DetectMenuScene()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                _activeSceneName = scene.name ?? "";
                string lower = _activeSceneName.ToLowerInvariant();

                _isMenuScene = false;
                for (int i = 0; i < MenuSceneKeywords.Length; i++)
                {
                    string kw = MenuSceneKeywords[i];
                    if (!string.IsNullOrEmpty(kw) && lower.Contains(kw.Trim().ToLowerInvariant()))
                    {
                        _isMenuScene = true;
                        break;
                    }
                }
            }
            catch
            {
                _isMenuScene = false;
            }
        }

        public void RefreshSlots()
        {
            _slots = GrandArenaSaveSystem.GetAllSlots();
        }

        public void TogglePanel()
        {
            PanelOpen = !PanelOpen;
            if (PanelOpen) RefreshSlots();
        }

        public void OpenPanel()
        {
            PanelOpen = true;
            RefreshSlots();
        }

        public void ClosePanel() => PanelOpen = false;

        public bool IsMenuScene => _isMenuScene;
        public string ActiveSceneName => _activeSceneName;

        // ==================================================================
        // Отрисовка
        // ==================================================================
        private void OnGUI()
        {
            if (!ButtonEnabled) return;

            var mode = GrandArenaMode.Instance;
            if (mode != null && mode.IsActive)
                return; // Внутри режима работает его собственный HUD

            InitStyles();

            bool visible = ForceAlwaysVisible || _isMenuScene || PanelOpen;
            if (!visible) return;

            DrawMainMenuButton();

            if (PanelOpen)
            {
                if (_panelRect.x <= 1f && _panelRect.y <= 1f)
                {
                    _panelRect.x = Mathf.Max(10f, (Screen.width - _panelRect.width) * 0.5f);
                    _panelRect.y = Mathf.Max(10f, (Screen.height - _panelRect.height) * 0.5f);
                }

                _panelRect = GUI.Window(9880, _panelRect, DrawModePanel, "");
            }
        }

        private void DrawMainMenuButton()
        {
            const float width = 380f;
            const float height = 74f;
            float x = 48f;
            float y = Screen.height - height - 70f;

            var rect = new Rect(x, y, width, height);
            bool hover = rect.Contains(Event.current.mousePosition);

            // Фон кнопки
            GUI.DrawTexture(rect, (hover ? _menuButtonHover : _menuButtonBg)!, ScaleMode.StretchToFill);

            // Золотая рамка в стиле Pharloom
            DrawBorder(rect, hover ? new Color(1f, 0.85f, 0.45f, 1f) : new Color(0.75f, 0.62f, 0.28f, 0.9f), 2f);

            GUI.Label(new Rect(rect.x + 18f, rect.y + 8f, rect.width - 24f, 26f),
                "⚔  НЕЙРО-АРЕНА: ВЕЛИКАЯ ТРЕНИРОВКА", _titleStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 36f, rect.width - 24f, 34f),
                "Мобы и Хорнет учатся вместе · отдельный режим · свои сохранения", _subtitleStyle);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                TogglePanel();
            }
        }

        private void DrawModePanel(int windowId)
        {
            GUI.DrawTexture(new Rect(0, 0, _panelRect.width, _panelRect.height), _panelBg!, ScaleMode.StretchToFill);
            DrawBorder(new Rect(0, 0, _panelRect.width, _panelRect.height), new Color(0.85f, 0.7f, 0.35f, 0.95f), 2f);

            GUILayout.BeginArea(new Rect(16, 14, _panelRect.width - 32, _panelRect.height - 28));

            GUILayout.Label("ВЕЛИКАЯ АРЕНА — ОТДЕЛЬНЫЙ РЕЖИМ ОБУЧЕНИЯ", _titleStyle);
            GUILayout.Label(
                "Одна огромная арена. Хорнет и стая мобов всех архетипов обучаются одновременно, " +
                "каждый прогресс хранится в собственном слоте сохранения этого режима.",
                _subtitleStyle);

            GUILayout.Space(8);

            var mode = GrandArenaMode.Instance;
            if (mode == null)
            {
                GUILayout.Label("Режим ещё инициализируется...", _labelStyle);
                GUILayout.EndArea();
                GUI.DragWindow(new Rect(0, 0, _panelRect.width, 40));
                return;
            }

            // ------------------------------------------------------------------
            // Слоты сохранений режима
            // ------------------------------------------------------------------
            GUILayout.Label("<b>СЛОТЫ СОХРАНЕНИЙ РЕЖИМА</b>", _slotTitleStyle);
            _slotScroll = GUILayout.BeginScrollView(_slotScroll, GUILayout.Height(228));

            for (int i = 0; i < _slots.Count; i++)
            {
                GrandArenaSlotInfo info = _slots[i];
                bool selected = _selectedSlot == info.Slot;

                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();

                string marker = selected ? "<color=#ffd166>▶</color> " : "   ";
                if (info.Exists)
                {
                    GUILayout.Label(
                        $"{marker}<b>СЛОТ {info.Slot}</b> — {info.Name}\n" +
                        $"      Волна <b>{info.Wave}</b> · Поколение <b>{info.Generation}</b> · Эпизодов <b>{info.TotalEpisodes}</b> · " +
                        $"Мобов в стае <b>{info.MobCount}</b>\n" +
                        $"      Победы Хорнет: <color=#66ddff>{info.HornetWins}</color> · Победы стаи: <color=#ff6b6b>{info.PackWins}</color> · " +
                        $"В игре: {info.PlayTimeFormatted}",
                        _labelStyle, GUILayout.ExpandWidth(true));
                }
                else
                {
                    GUILayout.Label($"{marker}<b>СЛОТ {info.Slot}</b> — <color=#888888>пусто</color>\n" +
                                    "      Новая нейросеть, волна 1, чистая статистика.",
                        _labelStyle, GUILayout.ExpandWidth(true));
                }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(info.Exists ? "▶ ПРОДОЛЖИТЬ" : "▶ НАЧАТЬ", GUILayout.Height(26)))
                {
                    _selectedSlot = info.Slot;
                    StartMode(info.Slot, !info.Exists);
                }

                if (GUILayout.Button("✚ Новая тренировка", GUILayout.Height(26)))
                {
                    _selectedSlot = info.Slot;
                    StartMode(info.Slot, true);
                }

                GUI.enabled = info.Exists;
                if (GUILayout.Button("🗑 Удалить", GUILayout.Height(26), GUILayout.Width(110)))
                {
                    GrandArenaSaveSystem.DeleteSlot(info.Slot);
                    RefreshSlots();
                }
                GUI.enabled = true;

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(4);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);

            // ------------------------------------------------------------------
            // Параметры запуска
            // ------------------------------------------------------------------
            GUILayout.Label("<b>ПАРАМЕТРЫ АРЕНЫ</b>", _slotTitleStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Мобов одновременно: <b>{mode.BaseMobCount}</b>", _labelStyle, GUILayout.Width(200));
            if (GUILayout.Button("−", GUILayout.Width(34))) mode.BaseMobCount = Mathf.Max(GrandArenaMode.MIN_MOBS, mode.BaseMobCount - 1);
            if (GUILayout.Button("+", GUILayout.Width(34))) mode.BaseMobCount = Mathf.Min(GrandArenaMode.MAX_MOBS, mode.BaseMobCount + 1);
            GUILayout.Space(16);
            GUILayout.Label($"Длительность раунда: <b>{mode.MaxEpisodeDuration:F0} с</b>", _labelStyle, GUILayout.Width(210));
            mode.MaxEpisodeDuration = GUILayout.HorizontalSlider(mode.MaxEpisodeDuration, 20f, 180f, GUILayout.Width(130));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            mode.WaveScaling = GUILayout.Toggle(mode.WaveScaling, " Рост волн (стая растёт после победы Хорнет)", GUILayout.Width(330));
            mode.BalancePackDamage = GUILayout.Toggle(mode.BalancePackDamage, " Честный баланс урона стаи", GUILayout.Width(250));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Хорнет:", _labelStyle, GUILayout.Width(70));
            if (GUILayout.Button(mode.PlayerControlsHornet ? "🎮 Игрок (переключить на ИИ)" : "🤖 Нейросеть (переключить на игрока)", GUILayout.Width(300)))
            {
                mode.PlayerControlsHornet = !mode.PlayerControlsHornet;
            }
            GUILayout.Space(12);
            GUILayout.Label($"Скорость: <b>{mode.TimeScale:F1}x</b>", _labelStyle, GUILayout.Width(110));
            if (GUILayout.Button("1x", GUILayout.Width(40))) mode.SetTimeScale(1f);
            if (GUILayout.Button("5x", GUILayout.Width(40))) mode.SetTimeScale(5f);
            if (GUILayout.Button("10x", GUILayout.Width(44))) mode.SetTimeScale(10f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<size=10><color=#9aa4b2>Сохранения: {GrandArenaSaveSystem.RootDirectory}</color></size>", _labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("ЗАКРЫТЬ", GUILayout.Width(120), GUILayout.Height(28)))
            {
                ClosePanel();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
            GUI.DragWindow(new Rect(0, 0, _panelRect.width, 40));
        }

        private void StartMode(int slot, bool newProfile)
        {
            var mode = GrandArenaMode.Instance;
            if (mode == null) return;

            ClosePanel();
            mode.StartMode(slot, newProfile);

            var hud = GrandArenaHUD.Instance;
            if (hud != null) hud.Visible = true;
        }

        // ==================================================================
        // Стили и примитивы
        // ==================================================================
        private void InitStyles()
        {
            if (_titleStyle != null) return;

            _menuButtonBg = MakeTexture(new Color(0.06f, 0.08f, 0.12f, 0.94f));
            _menuButtonHover = MakeTexture(new Color(0.13f, 0.11f, 0.06f, 0.97f));
            _panelBg = MakeTexture(new Color(0.05f, 0.06f, 0.09f, 0.97f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = false
            };
            _titleStyle.normal.textColor = new Color(0.98f, 0.85f, 0.45f);

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true,
                wordWrap = true
            };
            _subtitleStyle.normal.textColor = new Color(0.72f, 0.78f, 0.86f);

            _slotTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                richText = true
            };
            _slotTitleStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true,
                wordWrap = true
            };
            _labelStyle.normal.textColor = new Color(0.9f, 0.92f, 0.95f);

        }

        internal static Texture2D MakeTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        internal static void DrawBorder(Rect rect, Color color, float thickness)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}

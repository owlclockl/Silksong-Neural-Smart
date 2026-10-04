using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Меню обмена бусинами в стиле Silksong (по умолчанию F7 или LB+RB на геймпаде):
    /// затемнённый фон, резная панель, список странников лобби, выбор суммы,
    /// журнал переводов. Управляется мышью (со своим курсором-иглой),
    /// клавиатурой и геймпадом.
    /// </summary>
    internal sealed class TransferWindow : MonoBehaviour
    {
        // ---------------- Макет (условные единицы, умножаются на масштаб) ----------------

        private const float PanelW = 820f;
        private const float PanelH = 580f;
        private const float Pad = 30f;
        private const float ListW = 300f;
        private const float ColGap = 24f;
        private const float RowH = 33f;
        private const float LogLineH = 22f;
        private const int VisiblePlayers = 6;

        // ---- Инвентарная решётка («как в сталкере»): квадратные ячейки ----
        private const int GridCols = 7;
        private const int GridRows = 3;
        private const float SlotGap = 6f;
        private const float ScrollLaneW = 10f;
        private const float GridTop = 142f;

        /// <summary>Ширина правой колонки в условных единицах макета.</summary>
        private const float RightColumnW = PanelW - Pad * 2f - ListW - ColGap;

        private static readonly int[] PresetAmounts = { 100, 500, 1000, 5000 };

        private const string AmountControlName = "RosaryShareAmount";
        private const string PlayerPrefix = "player:";
        private const string PresetPrefix = "preset:";
        private const string FocusAll = "preset:all";
        private const string FocusMinus = "minus";
        private const string FocusPlus = "plus";
        private const string FocusField = "field";
        private const string FocusSend = "send";
        private const string FocusClose = "close";
        private const string FocusBeads = "resource:beads";
        private const string FocusShards = "resource:shards";
        private const string FocusItems = "resource:items";
        private const string ItemPrefix = "slot:";
        private const string FocusItemAll = "item:all";

        // ---------------- Состояние ----------------

        private bool _open;

        private CSteamID _selectedId;
        private int _selectedIndex = -1;
        private string _amountText = "100";
        private int _amount = 100;
        private TransferManager.ResourceKind _resource = TransferManager.ResourceKind.Beads;

        /// <summary>Ключ выбранной вещи (а не индекс: состав решётки меняется на ходу).</summary>
        private string _itemKey = string.Empty;

        /// <summary>Прокрутка решётки в строках.</summary>
        private int _itemRowOffset;

        /// <summary>Безопасные предметы, которые сейчас есть в инвентаре.</summary>
        private readonly List<ItemBridge.ItemEntry> _itemView = new List<ItemBridge.ItemEntry>(64);

        private Rect _gridRect;
        private float _itemViewAt = -99f;
        private float _extra;
        private string _tipTitle;
        private string _tipLine;
        private Vector2 _tipAt;

        private int _playerOffset;
        private int _logOffset;
        private int _seenHistoryVersion;

        private readonly List<Focusable> _focusables = new List<Focusable>(32);
        private string _focusId = FocusSend;
        private int _navX;
        private int _navY;
        private bool _activate;
        private bool _pointerMode = true;
        private float _lastPointerAt;
        private Vector3 _lastMousePosition;
        private bool _textFieldFocused;

        private string _repeatId;
        private float _repeatNextAt;

        private float _keyNavNextAt;
        private int _keyDirX;
        private int _keyDirY;

        private bool _cursorSaved;
        private CursorLockMode _prevLockState;
        private bool _prevCursorVisible;

        private struct Focusable
        {
            public string Id;
            public Rect Rect;
        }

        // ---------------- Жизненный цикл ----------------

        private bool _loggedBackend;

        private void Start()
        {
            _lastMousePosition = Input.mousePosition;
        }

        private void Update()
        {
            GamepadInput.Tick(_open);
            GameBridge.TickInputBlock();

            // The sharing window is deliberately standalone.  It must not be
            // attached to the game's inventory screen: F7 (or the configured
            // gamepad combo) is the single entry point and works from any game
            // scene where a save/lobby is available.
            bool toggleByKey = Input.GetKeyDown(ModConfig.MenuKeyCode);
            bool toggleByPad = ModConfig.MenuCombo != null && ModConfig.MenuCombo.Triggered();

            bool openedWithGamepad = false;
            if (toggleByKey || toggleByPad)
            {
                openedWithGamepad = toggleByPad && !_open;
                if (toggleByPad) _pointerMode = false;
                Toggle();
            }

            if (!_open)
            {
                if (Input.GetKeyDown(ModConfig.QuickSendKeyCode))
                    QuickSend();
                else if (ModConfig.QuickSendCombo != null && ModConfig.QuickSendCombo.Triggered())
                    QuickSend();
                return;
            }

            TrackPointer();
            HandleMenuInput(openedWithGamepad);

            if (!Input.GetMouseButton(0) && !GamepadInput.ConfirmHeld())
                _repeatId = null;
        }

        // Порядок кадра в Unity: Update → LateUpdate → OnGUI, поэтому «фронты»
        // ввода выставляются в Update и живут до следующего Update — их успевает
        // прочитать отрисовка. Здесь только возвращаем курсор, который игра
        // прячет каждый кадр в своём Update.
        private void LateUpdate()
        {
            if (!_open) return;
            ApplyCursorState();
        }

        private void OnDisable()
        {
            if (_open)
            {
                _open = false;
                GameBridge.SetInputBlocked(false);
            }
            RestoreCursor();
        }

        // ---------------- Открытие и закрытие ----------------

        private void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        private void Open()
        {
            _open = true;
            _logOffset = 0;
            _lastPointerAt = Time.unscaledTime;
            _lastMousePosition = Input.mousePosition;
            _pointerMode = !GamepadInput.RecentlyUsed;

            if (string.IsNullOrEmpty(_focusId))
                _focusId = FocusSend;

            if (_resource == TransferManager.ResourceKind.Item) SyncItemSelection();

            SaveCursor();
            ApplyCursorState();
            GameBridge.SetInputBlocked(true);

            if (!_loggedBackend)
            {
                _loggedBackend = true;
                RosarySharePlugin.LogInfo("Menu opened. Gamepad: " + GamepadInput.Backend +
                    ", menu combo: " + (ModConfig.MenuCombo != null ? ModConfig.MenuCombo.Text : "none") +
                    ", input block: " + (GameBridge.InputBlocked ? "on" : "off"));
            }
        }

        private void Close()
        {
            _open = false;
            _activate = false;
            _navX = 0;
            _navY = 0;
            _repeatId = null;
            _textFieldFocused = false;

            GUIUtility.keyboardControl = 0;
            GameBridge.SetInputBlocked(false);
            RestoreCursor();
        }

        // ---------------- Курсор ----------------

        private void SaveCursor()
        {
            if (_cursorSaved) return;
            _cursorSaved = true;
            _prevLockState = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;
        }

        private void ApplyCursorState()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = ModConfig.ShowSystemCursor;
        }

        private void RestoreCursor()
        {
            if (!_cursorSaved) return;
            _cursorSaved = false;
            Cursor.lockState = _prevLockState;
            Cursor.visible = _prevCursorVisible;
        }

        // ---------------- Ввод ----------------

        private void TrackPointer()
        {
            Vector3 mouse = Input.mousePosition;
            bool moved = (mouse - _lastMousePosition).sqrMagnitude > 4f;
            _lastMousePosition = mouse;

            if (moved || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                _lastPointerAt = Time.unscaledTime;
                _pointerMode = true;
            }
            else if (GamepadInput.LastActivity > _lastPointerAt)
            {
                _pointerMode = false;
            }
        }

        private void HandleMenuInput(bool openedWithGamepad)
        {
            // «фронты» прошлого кадра уже отрисованы — начинаем с чистого листа
            _activate = false;
            _navX = 0;
            _navY = 0;

            // --- закрытие ---
            if (Input.GetKeyDown(KeyCode.Escape) ||
                (!openedWithGamepad && (GamepadInput.CancelPressed() ||
                    GamepadInput.Pressed(GamepadInput.Btn.Start))))
            {
                Close();
                return;
            }

            // --- навигация ---
            int navX = GamepadInput.NavX;
            int navY = GamepadInput.NavY;

            int keyX, keyY;
            KeyboardNavigation(out keyX, out keyY);
            if (keyX != 0 || keyY != 0)
            {
                navX = keyX;
                navY = keyY;
                _pointerMode = false;
            }
            else if (navX != 0 || navY != 0)
            {
                _pointerMode = false;
            }

            _navX = navX;
            _navY = navY;

            // --- подтверждение ---
            if ((!openedWithGamepad && GamepadInput.ConfirmPressed()) ||
                (!_textFieldFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))))
            {
                _activate = true;
                _pointerMode = false;
            }

            // --- быстрые действия геймпада ---
            // Не используем тот же фронт, который только что открыл меню:
            // например LB+X должно открыть окно, а не сразу выбрать «Всё».
            if (!openedWithGamepad)
            {
                if (GamepadInput.Pressed(GamepadInput.Btn.LeftBumper)) CyclePreset(-1);
                if (GamepadInput.Pressed(GamepadInput.Btn.RightBumper)) CyclePreset(1);

                if (GamepadInput.Pressed(GamepadInput.Btn.Alt)) SetAmount(Mathf.Min(EffectiveBalance(), ModConfig.MaxSendAmount));

                if (GamepadInput.Pressed(GamepadInput.Btn.LeftTrigger)) ScrollLog(-1);
                if (GamepadInput.Pressed(GamepadInput.Btn.RightTrigger)) ScrollLog(1);
            }
        }

        private void KeyboardNavigation(out int x, out int y)
        {
            x = 0;
            y = 0;
            if (_textFieldFocused) return;

            int dirX = 0, dirY = 0;
            if (Input.GetKey(KeyCode.LeftArrow)) dirX--;
            if (Input.GetKey(KeyCode.RightArrow)) dirX++;
            if (Input.GetKey(KeyCode.UpArrow)) dirY--;
            if (Input.GetKey(KeyCode.DownArrow)) dirY++;

            if (dirX == 0 && dirY == 0)
            {
                _keyDirX = 0;
                _keyDirY = 0;
                return;
            }

            float now = Time.unscaledTime;
            if (dirX != _keyDirX || dirY != _keyDirY)
            {
                _keyDirX = dirX;
                _keyDirY = dirY;
                _keyNavNextAt = now + Mathf.Max(0.05f, ModConfig.GamepadRepeatDelay);
                x = dirX;
                y = dirY;
                return;
            }

            if (now >= _keyNavNextAt)
            {
                _keyNavNextAt = now + Mathf.Max(0.02f, ModConfig.GamepadRepeatRate);
                x = dirX;
                y = dirY;
            }
        }

        // ---------------- Быстрая отправка ----------------

        private void QuickSend()
        {
            TransferManager mgr = TransferManager.Instance;
            if (mgr == null) return;

            TransferManager.RemotePlayer target = ResolveSelection();
            if (target == null)
            {
                NotifyError(TransferManager.SendError.NoTarget);
                return;
            }

            TransferManager.SendError error = mgr.TrySend(target, ModConfig.QuickSendAmount);
            if (error != TransferManager.SendError.Ok)
                NotifyError(error);
        }

        private TransferManager.RemotePlayer ResolveSelection()
        {
            TransferManager mgr = TransferManager.Instance;
            if (mgr == null) return null;

            foreach (TransferManager.RemotePlayer p in mgr.Players)
                if (p.Id == _selectedId) return p;

            if (mgr.Players.Count == 1)
                return mgr.Players[0];

            if (_selectedIndex >= 0 && _selectedIndex < mgr.Players.Count)
                return mgr.Players[_selectedIndex];

            return null;
        }

        private void Send()
        {
            TransferManager mgr = TransferManager.Instance;
            if (mgr == null) return;

            TransferManager.RemotePlayer target = ResolveSelection();
            if (target == null)
            {
                NotifyError(TransferManager.SendError.NoTarget);
                return;
            }

            TransferManager.SendError error;
            if (_resource == TransferManager.ResourceKind.Item)
            {
                if (string.IsNullOrEmpty(_itemKey) || ItemBridge.Count(_itemKey) <= 0)
                {
                    SyncItemSelection();
                    if (string.IsNullOrEmpty(_itemKey)) return;
                }
                ItemBridge.ItemEntry entry = ItemBridge.Find(_itemKey);
                if (entry == null) return;
                int available = ItemBridge.Count(_itemKey);
                if (available <= 0) return;
                int sendCount = entry.Unique ? 1 : Mathf.Clamp(_amount, 1, available);
                error = mgr.TrySendItem(target, _itemKey, sendCount);
                if (error == TransferManager.SendError.Ok) SyncItemSelection();
            }
            else error = mgr.TrySend(target, _amount, _resource);
            if (error != TransferManager.SendError.Ok)
                NotifyError(error);
        }

        // ---------------- Отрисовка ----------------

        private void OnGUI()
        {
            // This is an independent overlay, not a tab in the native inventory.
            if (!_open) return;

            // Вкладка «Вещи» показывает решётку инвентаря — панель под неё выше.
            _extra = _resource == TransferManager.ResourceKind.Item ? GridExtraUnits() : 0f;
            float panelH = PanelH + _extra;

            float scale = ComputeScale(panelH);
            SilkUi.EnsureStyles(scale);
            GUI.depth = -500;

            _tipTitle = null;
            _tipLine = null;

            Rect panel = new Rect(
                Mathf.Round((Screen.width - PanelW * scale) * 0.5f),
                Mathf.Round((Screen.height - panelH * scale) * 0.5f),
                Mathf.Round(PanelW * scale),
                Mathf.Round(panelH * scale));

            DrawBackdrop();

            _focusables.Clear();

            SilkUi.Panel(panel, scale);
            DrawHeader(panel, scale);
            DrawPlayers(panel, scale);
            DrawAmount(panel, scale);
            DrawSend(panel, scale);
            DrawJournal(panel, scale);
            DrawFooter(panel, scale);

            HandleScrollWheel(panel, scale);

            // подсказка предмета рисуется поверх всего, кроме курсора
            if (!string.IsNullOrEmpty(_tipTitle))
                SilkUi.Tooltip(_tipAt, _tipTitle, _tipLine, scale,
                    new Rect(8f, 8f, Screen.width - 16f, Screen.height - 16f));

            if (Event.current.type == EventType.Repaint)
            {
                ProcessNavigation();
                DrawSoftCursor(scale);
            }
        }

        private float ComputeScale(float panelH)
        {
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            scale = Mathf.Clamp(scale, 0.62f, 2.2f) * Mathf.Clamp(ModConfig.UiScale, 0.5f, 3f);

            float maxByWidth = (Screen.width - 32f) / PanelW;
            float maxByHeight = (Screen.height - 32f) / Mathf.Max(1f, panelH);
            return Mathf.Max(0.4f, Mathf.Min(scale, Mathf.Min(maxByWidth, maxByHeight)));
        }

        // ---------------- Геометрия решётки ----------------

        /// <summary>Сторона квадратной ячейки в условных единицах макета.</summary>
        private static float SlotUnits()
        {
            return (RightColumnW - ScrollLaneW - SlotGap * (GridCols - 1)) / GridCols;
        }

        /// <summary>
        /// Насколько панель вырастает, когда открыта вкладка «Вещи»: решётка,
        /// строка описания и поле количества занимают больше места, чем обычные
        /// пресеты сумм. Считается из той же геометрии, что и отрисовка.
        /// </summary>
        private static float GridExtraUnits()
        {
            float grid = GridRows * SlotUnits() + (GridRows - 1) * SlotGap;

            // решётка → описание (8+34) → количество (6+34) → кнопка (10+54) → подсказка (6+18)
            float bottom = GridTop + grid + 8f + 34f + 6f + 34f + 10f + 54f + 6f + 18f;

            // в обычном режиме низ правой колонки (подсказка под кнопкой) = 310
            return Mathf.Max(0f, bottom - 310f);
        }

        private void DrawBackdrop()
        {
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            float dim = Mathf.Clamp01(ModConfig.BackgroundDim);
            if (dim > 0f)
                SilkUi.FillColor(screen, new Color(0.012f, 0.010f, 0.016f, dim * 0.82f));

            SilkUi.Fill(screen, UiKit.Vignette, new Color(1f, 1f, 1f, 0.55f + dim * 0.45f));
        }

        private void DrawHeader(Rect panel, float s)
        {
            float x0 = panel.x + Pad * s;
            float width = panel.width - Pad * s * 2f;

            SilkUi.Text(new Rect(x0, panel.y + 20f * s, width, 34f * s),
                SilkUi.Spaced(Texts.MenuTitle), SilkUi.Title, UiKit.Bone);

            SilkUi.Text(new Rect(x0, panel.y + 56f * s, width, 18f * s),
                Texts.MenuSubtitle, SilkUi.Status, UiKit.BoneDim);

            // счётчик бусин с «бусиной» слева
            string balance = Texts.T("Бусины ", "Beads ") + GameBridge.GetGeo() + "   ·   " + Texts.T("Осколки ", "Shards ") + GameBridge.GetShards();
            Rect balanceRect = new Rect(panel.xMax - (Pad + 200f) * s, panel.y + 22f * s, 200f * s, 26f * s);
            SilkUi.Text(balanceRect, balance, SilkUi.Balance, UiKit.Gold);

            Vector2 size = SilkUi.Balance.CalcSize(new GUIContent(balance));
            Vector2 bead = new Vector2(balanceRect.xMax - size.x - 14f * s, balanceRect.center.y);
            SilkUi.Diamond(bead, 15f * s, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.9f));
            SilkUi.Diamond(bead, 9f * s, UiKit.Crimson);

            SilkUi.Divider(new Rect(x0, panel.y + 84f * s, width, Mathf.Max(1f, s)),
                new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.9f), true);

            SilkUi.Text(new Rect(x0, panel.y + 92f * s, width, 22f * s), StatusLine(), SilkUi.Status, StatusColor());
        }

        private string StatusLine()
        {
            TransferManager mgr = TransferManager.Instance;

            if (mgr == null || !XvXBridge.BridgeOk) return Texts.StatusNoMod;
            if (!mgr.InLobby) return Texts.StatusNoLobby;
            if (!GameBridge.InGame) return Texts.NeedSave;

            string status = string.Format(Texts.LobbyCountFormat, mgr.Players.Count + 1);
            if (mgr.PendingCount > 0)
                status += "   ·   " + string.Format(Texts.PendingFormat, mgr.PendingCount);

            return status;
        }

        private Color StatusColor()
        {
            TransferManager mgr = TransferManager.Instance;
            if (mgr == null || !XvXBridge.BridgeOk || !mgr.InLobby || !GameBridge.InGame)
                return UiKit.Warn;
            return UiKit.BoneDim;
        }

        private void DrawPlayers(Rect panel, float s)
        {
            TransferManager mgr = TransferManager.Instance;
            float x0 = panel.x + Pad * s;

            int count = mgr != null ? mgr.Players.Count : 0;
            ClampPlayerWindow(count);
            int last = Mathf.Min(count, _playerOffset + VisiblePlayers);

            SilkUi.Text(new Rect(x0, panel.y + 118f * s, ListW * s, 20f * s),
                Texts.PlayersHeader.ToUpperInvariant(), SilkUi.Section, UiKit.Gold);

            // «1–6 / 9» в строке заголовка, чтобы не занимать место под списком
            if (count > VisiblePlayers)
            {
                SilkUi.Text(new Rect(x0, panel.y + 118f * s, ListW * s, 20f * s),
                    (_playerOffset + 1) + "–" + last + " / " + count,
                    SilkUi.ItemRight, UiKit.BoneDim);
            }

            Rect well = new Rect(x0, panel.y + 142f * s, ListW * s, RowH * VisiblePlayers * s);
            SilkUi.Well(well, s);

            if (count == 0)
            {
                _playerOffset = 0;
                SilkUi.Text(well, Texts.NoPlayers, SilkUi.Empty, UiKit.BoneDim);
                return;
            }

            for (int i = _playerOffset; i < last; i++)
            {
                TransferManager.RemotePlayer player = mgr.Players[i];
                Rect row = new Rect(well.x + 2f * s, well.y + 2f * s + (i - _playerOffset) * RowH * s,
                    well.width - 4f * s, RowH * s - 2f * s);

                bool hover, focused;
                bool clicked = Control(PlayerPrefix + i, row, false, out hover, out focused);

                bool selected = player.Id == _selectedId;
                string name = SilkUi.Ellipsize(player.Name, SilkUi.Item, row.width - 40f * s);
                SilkUi.MenuItem(row, name, selected, hover, focused, s);

                if (clicked)
                    SelectPlayer(i);
            }

            // указатели прокрутки
            Color arrow = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.85f);
            if (_playerOffset > 0)
                SilkUi.Text(new Rect(well.xMax - 22f * s, well.y + 2f * s, 18f * s, 16f * s), "▲", SilkUi.Hint, arrow);
            if (last < count)
                SilkUi.Text(new Rect(well.xMax - 22f * s, well.yMax - 18f * s, 18f * s, 16f * s), "▼", SilkUi.Hint, arrow);

        }

        private void DrawAmount(Rect panel, float s)
        {
            float rightX = panel.x + (Pad + ListW + ColGap) * s;
            float rightW = panel.width - (Pad + ListW + ColGap) * s - Pad * s;

            bool itemsMode = _resource == TransferManager.ResourceKind.Item;
            SilkUi.Text(new Rect(rightX, panel.y + 118f * s, rightW * 0.42f, 20f * s),
                (itemsMode ? Texts.ItemsHeader : Texts.AmountHeader).ToUpperInvariant(),
                SilkUi.Section, UiKit.Gold);

            float tabW = 82f * s;
            Rect beadsTab = new Rect(rightX + rightW - tabW * 3f - 12f * s, panel.y + 112f * s, tabW, 26f * s);
            Rect shardsTab = new Rect(beadsTab.xMax + 6f * s, beadsTab.y, tabW, beadsTab.height);
            Rect itemsTab = new Rect(shardsTab.xMax + 6f * s, beadsTab.y, tabW, beadsTab.height);
            bool bh, bf, sh, sf, ih, inf;
            if (Control(FocusBeads, beadsTab, false, out bh, out bf)) { _resource = TransferManager.ResourceKind.Beads; ClampAmountToBalance(); }
            if (Control(FocusShards, shardsTab, false, out sh, out sf)) { _resource = TransferManager.ResourceKind.Shards; ClampAmountToBalance(); }
            if (Control(FocusItems, itemsTab, false, out ih, out inf)) { _resource = TransferManager.ResourceKind.Item; SyncItemSelection(); }
            SilkUi.SmallButton(beadsTab, Texts.T("Бусины", "Beads"), _resource == TransferManager.ResourceKind.Beads, bh, bf, true, s);
            SilkUi.SmallButton(shardsTab, Texts.T("Осколки", "Shards"), _resource == TransferManager.ResourceKind.Shards, sh, sf, true, s);
            SilkUi.SmallButton(itemsTab, Texts.ItemsTab, _resource == TransferManager.ResourceKind.Item, ih, inf, true, s);

            if (itemsMode) { DrawItemGrid(panel, s, rightX, rightW); return; }

            // --- пресеты ---
            int buttons = PresetAmounts.Length + 1;
            float gap = 8f * s;
            float buttonW = (rightW - gap * (buttons - 1)) / buttons;
            float presetY = panel.y + 142f * s;
            float presetH = 34f * s;

            for (int i = 0; i < PresetAmounts.Length; i++)
            {
                Rect rect = new Rect(rightX + i * (buttonW + gap), presetY, buttonW, presetH);
                bool hover, focused;
                bool clicked = Control(PresetPrefix + i, rect, false, out hover, out focused);
                SilkUi.SmallButton(rect, PresetAmounts[i].ToString(), _amount == PresetAmounts[i], hover, focused, true, s);
                if (clicked) SetAmount(PresetAmounts[i]);
            }

            Rect allRect = new Rect(rightX + PresetAmounts.Length * (buttonW + gap), presetY, buttonW, presetH);
            bool allHover, allFocused;
            bool allClicked = Control(FocusAll, allRect, false, out allHover, out allFocused);
            int maxSendable = Mathf.Min(EffectiveBalance(), ModConfig.MaxSendAmount);
            SilkUi.SmallButton(allRect, Texts.AllBeads, _amount > 0 && _amount == maxSendable, allHover, allFocused, true, s);
            if (allClicked) SetAmount(maxSendable);

            // --- своё значение ---
            float stepY = panel.y + 186f * s;
            float stepH = 34f * s;
            float stepW = 40f * s;
            float fieldW = 120f * s;

            Rect minusRect = new Rect(rightX, stepY, stepW, stepH);
            bool minusHover, minusFocused;
            bool minusClicked = Control(FocusMinus, minusRect, true, out minusHover, out minusFocused);
            SilkUi.SmallButton(minusRect, "−", false, minusHover, minusFocused, true, s);
            if (minusClicked) StepAmount(-1);

            Rect fieldRect = new Rect(minusRect.xMax + 6f * s, stepY, fieldW, stepH);
            DrawAmountField(fieldRect, s);

            Rect plusRect = new Rect(fieldRect.xMax + 6f * s, stepY, stepW, stepH);
            bool plusHover, plusFocused;
            bool plusClicked = Control(FocusPlus, plusRect, true, out plusHover, out plusFocused);
            SilkUi.SmallButton(plusRect, "+", false, plusHover, plusFocused, true, s);
            if (plusClicked) StepAmount(1);

            Rect availableRect = new Rect(plusRect.xMax + 14f * s, stepY, rightW - (plusRect.xMax - rightX) - 14f * s, stepH);
            SilkUi.Text(availableRect, string.Format(Texts.AvailableFormat, EffectiveBalance()), SilkUi.Hint, UiKit.BoneDim);
        }

        /// <summary>
        /// Инвентарь-решётка: квадратные ячейки с иконками и количеством, как в
        /// обычном инвентаре (или в «сталкерском» рюкзаке). Показываются только
        /// безопасные вещи, которые сейчас есть у игрока; выбор — мышью, стрелками
        /// или геймпадом, прокрутка — колесом либо выходом за нижний ряд.
        /// </summary>
        private void DrawItemGrid(Rect panel, float s, float rightX, float rightW)
        {
            RefreshItemView(false);
            int count = _itemView.Count;

            float gap = SlotGap * s;
            float lane = ScrollLaneW * s;
            float slot = (rightW - lane - gap * (GridCols - 1)) / GridCols;
            float gridH = GridRows * slot + (GridRows - 1) * gap;

            Rect grid = new Rect(rightX, panel.y + GridTop * s, rightW - lane, gridH);
            _gridRect = new Rect(rightX, grid.y, rightW, gridH);

            SilkUi.Well(new Rect(grid.x - 5f * s, grid.y - 5f * s, _gridRect.width + 10f * s, gridH + 10f * s), s);

            int rows = Mathf.Max(1, (count + GridCols - 1) / GridCols);
            int maxOffset = Mathf.Max(0, rows - GridRows);
            _itemRowOffset = Mathf.Clamp(_itemRowOffset, 0, maxOffset);

            // держим в поле зрения ту ячейку, на которой стоит фокус геймпада
            int focusedIndex = FocusedSlotIndex();
            if (focusedIndex >= 0 && count > 0)
                EnsureSlotVisible(Mathf.Min(focusedIndex, count - 1), maxOffset);

            int hovered = -1;
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridCols; col++)
                {
                    int index = (row + _itemRowOffset) * GridCols + col;
                    Rect cell = new Rect(
                        Mathf.Round(grid.x + col * (slot + gap)),
                        Mathf.Round(grid.y + row * (slot + gap)),
                        Mathf.Round(slot), Mathf.Round(slot));

                    if (index >= count)
                    {
                        SilkUi.Slot(cell, false, false, false, false, s);
                        continue;
                    }

                    ItemBridge.ItemEntry entry = _itemView[index];
                    bool hover, focused;
                    bool clicked = Control(ItemPrefix + index, cell, false, out hover, out focused);
                    bool selected = entry.Key == _itemKey;

                    SilkUi.Slot(cell, true, selected, hover, focused, s);
                    DrawSlotContent(cell, entry, s);

                    if (hover) hovered = index;
                    if (clicked) SelectItem(entry.Key);
                }
            }

            SilkUi.ScrollLane(new Rect(_gridRect.xMax - lane + 2f * s, grid.y, lane - 2f * s, gridH),
                _itemRowOffset, GridRows, rows, s);

            // счётчик вещей — справа от заголовка, но левее вкладок ресурсов
            if (count > 0)
            {
                SilkUi.Text(new Rect(rightX, panel.y + 118f * s, rightW - 268f * s, 20f * s),
                    string.Format(Texts.ItemsCountFormat, count), SilkUi.ItemRight, UiKit.BoneDim);
            }

            // ---- описание выбранной (или наведённой) вещи ----
            ItemBridge.ItemEntry shown = hovered >= 0 ? _itemView[hovered] : ItemBridge.Find(_itemKey);
            Rect bar = new Rect(rightX, grid.yMax + 8f * s, rightW, 34f * s);
            SilkUi.Well(bar, s);

            if (count == 0)
            {
                SilkUi.Text(grid, Texts.NoItems, SilkUi.Empty, UiKit.BoneDim);
                SilkUi.Text(bar, Texts.SafeOnly, SilkUi.Empty, UiKit.BoneDim);
            }
            else if (shown == null)
            {
                SilkUi.Text(bar, Texts.PickItem, SilkUi.Empty, UiKit.BoneDim);
            }
            else
            {
                float icon = bar.height - 8f * s;
                Rect iconRect = new Rect(bar.x + 5f * s, bar.y + 4f * s, icon, icon);
                DrawItemIcon(iconRect, shown);

                int owned = shown.Amount;
                string tail = shown.Unique ? Texts.ItemUnique : "× " + owned;
                Rect nameRect = new Rect(iconRect.xMax + 8f * s, bar.y, bar.width - icon - 140f * s, bar.height);
                SilkUi.Text(nameRect, SilkUi.Ellipsize(shown.Name, SilkUi.Item, nameRect.width), SilkUi.Item, UiKit.Bone);
                SilkUi.Text(new Rect(bar.xMax - 112f * s, bar.y, 102f * s, bar.height),
                    shown.Category + "   " + tail, SilkUi.ItemRight, UiKit.Gold);

                if (hovered >= 0)
                {
                    _tipTitle = shown.Name;
                    _tipLine = shown.Category + "  ·  " + (shown.Unique ? Texts.ItemUnique : "× " + owned);
                    _tipAt = Event.current.mousePosition;
                }
            }

            // ---- сколько передаём ----
            float y = bar.yMax + 6f * s;
            float stepW = 42f * s;
            float fieldW = 96f * s;

            Rect minus = new Rect(rightX, y, stepW, 34f * s);
            Rect field = new Rect(minus.xMax + 6f * s, y, fieldW, 34f * s);
            Rect plus = new Rect(field.xMax + 6f * s, y, stepW, 34f * s);
            Rect all = new Rect(plus.xMax + 10f * s, y, 64f * s, 34f * s);

            ItemBridge.ItemEntry currentItem = ItemBridge.Find(_itemKey);
            bool isUnique = currentItem != null && currentItem.Unique;
            bool canStep = !isUnique && EffectiveBalance() > 0;

            bool mh, mf, xh, xf, ah, af;
            if (Control(FocusMinus, minus, true, out mh, out mf)) { if (canStep) StepAmount(-1); }
            DrawAmountField(field, s);
            if (Control(FocusPlus, plus, true, out xh, out xf)) { if (canStep) StepAmount(1); }
            if (Control(FocusItemAll, all, false, out ah, out af)) { if (canStep) SetAmount(Mathf.Min(EffectiveBalance(), ModConfig.MaxSendAmount)); }

            SilkUi.SmallButton(minus, "−", false, mh, mf, canStep, s);
            SilkUi.SmallButton(plus, "+", false, xh, xf, canStep, s);
            SilkUi.SmallButton(all, Texts.AllBeads, canStep && _amount == EffectiveBalance(), ah, af, canStep, s);

            SilkUi.Text(new Rect(all.xMax + 12f * s, y, rightW - (all.xMax - rightX) - 12f * s, 34f * s),
                string.Format(Texts.AvailableFormat, EffectiveBalance()), SilkUi.Hint, UiKit.BoneDim);
        }

        /// <summary>
        /// Пересобирает список вещей в руках. Чтение PlayerData идёт через
        /// рефлексию, поэтому не чаще нескольких раз в секунду — на глаз это
        /// незаметно, а кадр не грузит.
        /// </summary>
        private void RefreshItemView(bool force)
        {
            float now = Time.unscaledTime;
            if (!force && now - _itemViewAt < 0.25f) return;

            _itemViewAt = now;
            ItemBridge.CollectOwned(_itemView);
        }

        private static void DrawItemIcon(Rect rect, ItemBridge.ItemEntry entry)
        {
            if (entry == null) return;
            if (entry.Sprite != null)
            {
                SilkUi.Sprite(rect, entry.Sprite, Color.white);
            }
            else if (entry.FallbackSprite != null)
            {
                SilkUi.FillFitted(rect, entry.FallbackSprite, Color.white);
            }
        }

        /// <summary>Иконка вещи и количество в углу ячейки.</summary>
        private static void DrawSlotContent(Rect cell, ItemBridge.ItemEntry entry, float s)
        {
            float pad = 6f * s;
            Rect iconRect = new Rect(cell.x + pad, cell.y + pad, cell.width - pad * 2f, cell.height - pad * 2f);
            DrawItemIcon(iconRect, entry);

            int owned = entry.Amount;
            if (entry.Unique || owned <= 1) return;

            string countText = "×" + owned;
            Vector2 size = SilkUi.Badge.CalcSize(new GUIContent(countText));
            float badgeW = size.x + 8f * s;
            float badgeH = 14f * s;
            Rect badgeRect = new Rect(cell.xMax - badgeW - 3f * s, cell.yMax - badgeH - 3f * s, badgeW, badgeH);

            SilkUi.FillColor(badgeRect, new Color(0.04f, 0.03f, 0.05f, 0.88f));
            SilkUi.Frame(badgeRect, new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.5f), 1f);
            SilkUi.Text(new Rect(badgeRect.x, badgeRect.y - 1f * s, badgeRect.width - 2f * s, badgeRect.height),
                countText, SilkUi.Badge, UiKit.Gold);
        }

        private int FocusedSlotIndex()
        {
            if (string.IsNullOrEmpty(_focusId) || !_focusId.StartsWith(ItemPrefix, StringComparison.Ordinal)) return -1;
            int index;
            return int.TryParse(_focusId.Substring(ItemPrefix.Length), out index) ? index : -1;
        }

        private void EnsureSlotVisible(int index, int maxOffset)
        {
            int row = index / GridCols;
            if (row < _itemRowOffset) _itemRowOffset = row;
            else if (row >= _itemRowOffset + GridRows) _itemRowOffset = row - GridRows + 1;
            _itemRowOffset = Mathf.Clamp(_itemRowOffset, 0, maxOffset);
        }

        /// <summary>Выбирает вещь по ключу и подставляет разумное количество.</summary>
        private void SelectItem(string key)
        {
            _itemKey = key ?? string.Empty;
            int available = ItemBridge.Count(_itemKey);
            SetAmount(available > 0 ? Mathf.Min(1, available) : 0);
        }

        /// <summary>
        /// Следит, чтобы выбранная вещь существовала: состав решётки меняется
        /// (вещь передали, вещь подобрали), поэтому выбор хранится ключом.
        /// </summary>
        private void SyncItemSelection()
        {
            RefreshItemView(true);
            if (_itemView.Count == 0)
            {
                _itemKey = string.Empty;
                SetAmount(0);
                return;
            }

            for (int i = 0; i < _itemView.Count; i++)
                if (_itemView[i].Key == _itemKey)
                {
                    SelectItem(_itemKey);
                    return;
                }

            SelectItem(_itemView[0].Key);
        }

        private void DrawAmountField(Rect rect, float s)
        {
            bool hover, focused;
            _focusables.Add(new Focusable { Id = FocusField, Rect = rect });
            hover = _pointerMode && rect.Contains(Event.current.mousePosition);
            focused = !_pointerMode && _focusId == FocusField;

            SilkUi.Frame(rect, focused || hover
                ? new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.9f)
                : new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.5f), Mathf.Max(1f, s));

            if (focused)
                SilkUi.FocusMarkers(rect, s);

            GUI.SetNextControlName(AmountControlName);
            string before = _amountText;
            string after = GUI.TextField(SilkUi.Inset(rect, Mathf.Max(1f, s)), _amountText, 9, SilkUi.Field);
            if (after != before)
            {
                _amountText = FilterDigits(after);
                int parsed;
                _amount = int.TryParse(_amountText, out parsed) ? parsed : 0;

                if (_resource == TransferManager.ResourceKind.Item)
                {
                    ItemBridge.ItemEntry currentItem = ItemBridge.Find(_itemKey);
                    if (currentItem != null && currentItem.Unique)
                    {
                        _amount = 1;
                        _amountText = "1";
                    }
                    else
                    {
                        int cap = EffectiveBalance();
                        if (cap > 0 && _amount > cap)
                        {
                            _amount = cap;
                            _amountText = cap.ToString();
                        }
                    }
                }
            }

            if (_activate && _focusId == FocusField)
            {
                _activate = false;
                GUI.FocusControl(AmountControlName);
            }

            if (Event.current.type == EventType.Repaint)
                _textFieldFocused = GUI.GetNameOfFocusedControl() == AmountControlName;
        }

        private void DrawSend(Rect panel, float s)
        {
            float rightX = panel.x + (Pad + ListW + ColGap) * s;
            float rightW = panel.width - (Pad + ListW + ColGap) * s - Pad * s;

            TransferManager.RemotePlayer target = ResolveSelection();
            bool itemsMode = _resource == TransferManager.ResourceKind.Item;
            bool ready = target != null && _amount > 0 && (!itemsMode || !string.IsNullOrEmpty(_itemKey));

            string what = _amount.ToString();
            if (itemsMode && !string.IsNullOrEmpty(_itemKey))
                what = _amount + " × " + ItemBridge.NameOf(_itemKey);

            string label = ready
                ? string.Format(Texts.SendFormat, what, SilkUi.Ellipsize(target.Name, SilkUi.Send, rightW * 0.30f))
                : (itemsMode && string.IsNullOrEmpty(_itemKey) ? Texts.PickItem : Texts.ChoosePrompt);

            Rect rect = new Rect(rightX, panel.y + (232f + _extra) * s, rightW, 54f * s);
            bool hover, focused;
            bool clicked = Control(FocusSend, rect, false, out hover, out focused);
            SilkUi.OrnateButton(rect, SilkUi.Ellipsize(label, SilkUi.Send, rect.width - 28f * s), ready, hover, focused, s);
            if (clicked) Send();

            string hint = itemsMode
                ? Texts.SafeNote
                : (ModConfig.QuickSendCombo != null
                    ? string.Format("{0} / {1} — {2} ({3})", ModConfig.QuickSendKeyCode, ModConfig.QuickSendCombo.Text,
                        Texts.HintQuickSend.ToLowerInvariant(), ModConfig.QuickSendAmount)
                    : string.Format("{0} — {1} ({2})", ModConfig.QuickSendKeyCode, Texts.HintQuickSend.ToLowerInvariant(), ModConfig.QuickSendAmount));

            Rect hintRect = new Rect(rightX, panel.y + (292f + _extra) * s, rightW, 18f * s);
            SilkUi.Text(hintRect, SilkUi.Ellipsize(hint, SilkUi.Hint, hintRect.width), SilkUi.Hint, UiKit.BoneDim);
        }

        private void DrawJournal(Rect panel, float s)
        {
            TransferManager mgr = TransferManager.Instance;
            float x0 = panel.x + Pad * s;
            float width = panel.width - Pad * s * 2f;

            SilkUi.Divider(new Rect(x0, panel.y + (348f + _extra) * s, width, Mathf.Max(1f, s)),
                new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.65f), true);

            SilkUi.Text(new Rect(x0, panel.y + (362f + _extra) * s, width, 20f * s),
                Texts.HistoryHeader.ToUpperInvariant(), SilkUi.Section, UiKit.Gold);

            Rect well = new Rect(x0, panel.y + (386f + _extra) * s, width, 126f * s);
            SilkUi.Well(well, s);

            if (mgr == null || mgr.History.Count == 0)
            {
                SilkUi.Text(well, Texts.EmptyJournal, SilkUi.Empty, UiKit.BoneDim);
                return;
            }

            if (_seenHistoryVersion != mgr.HistoryVersion)
            {
                _seenHistoryVersion = mgr.HistoryVersion;
                _logOffset = 0;
            }

            int lines = Mathf.Max(1, Mathf.FloorToInt(well.height / (LogLineH * s)));
            int total = mgr.History.Count;
            _logOffset = Mathf.Clamp(_logOffset, 0, Mathf.Max(0, total - lines));

            int first = Mathf.Max(0, total - lines - _logOffset);
            int shown = Mathf.Min(lines, total - first);

            for (int i = 0; i < shown; i++)
            {
                TransferManager.HistoryEntry entry = mgr.History[first + i];
                Rect line = new Rect(well.x + 10f * s, well.y + 2f * s + i * LogLineH * s, well.width - 20f * s, LogLineH * s);
                SilkUi.Text(line, SilkUi.Ellipsize(entry.Text, SilkUi.Log, line.width), SilkUi.Log, LogColor(entry.Kind));
            }

            if (_logOffset > 0)
                SilkUi.Text(new Rect(well.xMax - 24f * s, well.yMax - 18f * s, 20f * s, 16f * s), "▼", SilkUi.Hint,
                    new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.85f));
            if (first > 0)
                SilkUi.Text(new Rect(well.xMax - 24f * s, well.y + 2f * s, 20f * s, 16f * s), "▲", SilkUi.Hint,
                    new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.85f));
        }

        private static Color LogColor(ToastLog.Kind kind)
        {
            switch (kind)
            {
                case ToastLog.Kind.Success: return UiKit.Success;
                case ToastLog.Kind.Warn: return UiKit.Warn;
                case ToastLog.Kind.Error: return UiKit.Error;
                default: return UiKit.BoneSoft;
            }
        }

        private void DrawFooter(Rect panel, float s)
        {
            float x0 = panel.x + Pad * s;
            float y = panel.y + (538f + _extra) * s;

            // кнопка закрытия справа
            Rect close = new Rect(panel.xMax - (Pad + 150f) * s, y - 16f * s, 150f * s, 32f * s);
            bool hover, focused;
            bool clicked = Control(FocusClose, close, false, out hover, out focused);
            SilkUi.SmallButton(close, Texts.Close, false, hover, focused, true, s);
            if (clicked) Close();

            // подсказки управления
            float x = x0;
            if (GamepadInput.Available && ModConfig.GamepadEnabled)
            {
                x = SilkUi.HintChip(new Vector2(x, y), GamepadInput.ConfirmLabel, Texts.HintConfirm, s);
                x = SilkUi.HintChip(new Vector2(x, y), GamepadInput.CancelLabel, Texts.HintClose, s);
                x = SilkUi.HintChip(new Vector2(x, y), "LB/RB", Texts.HintAmount, s);
                if (x < close.x - 90f * s)
                    x = SilkUi.HintChip(new Vector2(x, y), "LT/RT", Texts.HintJournal, s);
            }
            else
            {
                x = SilkUi.HintChip(new Vector2(x, y), "↑↓←→", Texts.HintSelect, s);
                x = SilkUi.HintChip(new Vector2(x, y), "Enter", Texts.HintConfirm, s);
                x = SilkUi.HintChip(new Vector2(x, y), ModConfig.MenuKeyCode.ToString(), Texts.HintClose, s);
                if (ModConfig.MenuCombo != null && x < close.x - 120f * s)
                    SilkUi.HintChip(new Vector2(x, y), ModConfig.MenuCombo.Text, Texts.HintOpen, s);
            }
        }

        private void DrawSoftCursor(float s)
        {
            if (!ModConfig.DrawSoftCursor) return;

            float fade = _pointerMode
                ? 1f
                : Mathf.Clamp01(1f - (Time.unscaledTime - _lastPointerAt - 1.2f) / 0.6f);

            if (fade <= 0.02f) return;

            Vector2 p = Event.current.mousePosition;
            float scale = Mathf.Max(0.5f, s) * Mathf.Clamp(ModConfig.CursorScale, 0.4f, 4f);
            float size = UiKit.CursorSize * scale;

            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 3.2f);
            float glow = 20f * scale;
            SilkUi.Fill(new Rect(p.x - glow, p.y - glow, glow * 2f, glow * 2f), UiKit.Glow,
                new Color(UiKit.Crimson.r, UiKit.Crimson.g, UiKit.Crimson.b, 0.30f * fade * pulse));

            Rect rect = new Rect(
                p.x - UiKit.CursorHotspot.x * scale,
                p.y - UiKit.CursorHotspot.y * scale,
                size, size);
            SilkUi.Fill(rect, UiKit.CursorNeedle, new Color(1f, 1f, 1f, fade));
        }

        // ---------------- Виджеты и фокус ----------------

        private bool Control(string id, Rect rect, bool repeat, out bool hover, out bool focused)
        {
            _focusables.Add(new Focusable { Id = id, Rect = rect });

            hover = _pointerMode && rect.Contains(Event.current.mousePosition);
            focused = !_pointerMode && _focusId == id;

            bool clicked;
            if (repeat)
            {
                bool down = GUI.RepeatButton(rect, GUIContent.none, GUIStyle.none);
                if (down) _focusId = id;
                clicked = down && RepeatTick(id);

                if (focused && GamepadInput.ConfirmHeld() && !GamepadInput.ConfirmPressed() && RepeatTick(id))
                    clicked = true;
            }
            else
            {
                clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
                if (clicked) _focusId = id;
            }

            if (_activate && _focusId == id)
            {
                _activate = false;
                clicked = true;
            }

            return clicked;
        }

        private bool RepeatTick(string id)
        {
            float now = Time.unscaledTime;
            if (_repeatId != id)
            {
                _repeatId = id;
                _repeatNextAt = now + 0.38f;
                return true;
            }

            if (now >= _repeatNextAt)
            {
                _repeatNextAt = now + 0.07f;
                return true;
            }

            return false;
        }

        private void ProcessNavigation()
        {
            if (_navX == 0 && _navY == 0) return;
            MoveFocus(_navX, _navY);
            _navX = 0;
            _navY = 0;
        }

        private void MoveFocus(int dx, int dy)
        {
            if (_focusables.Count == 0) return;

            // внутри списка игроков вертикаль работает как прокрутка выбора
            if (dy != 0 && !string.IsNullOrEmpty(_focusId) && _focusId.StartsWith(PlayerPrefix, StringComparison.Ordinal))
            {
                TransferManager mgr = TransferManager.Instance;
                int count = mgr != null ? mgr.Players.Count : 0;
                int index;
                if (int.TryParse(_focusId.Substring(PlayerPrefix.Length), out index))
                {
                    int next = index + dy;
                    if (next >= 0 && next < count)
                    {
                        SelectPlayer(next);
                        return;
                    }
                    if (dy < 0) return;
                }
            }

            // внутри решётки предметов ходим по ячейкам, а не «по геометрии»:
            // влево/вправо — соседняя вещь, вверх/вниз — ряд выше/ниже
            int slotIndex = FocusedSlotIndex();
            if (slotIndex >= 0 && _itemView.Count > 0)
            {
                int count = _itemView.Count;
                int rows = Mathf.Max(1, (count + GridCols - 1) / GridCols);
                int maxOffset = Mathf.Max(0, rows - GridRows);

                if (dx < 0 && slotIndex % GridCols == 0)
                {
                    // Выход влево из первого столбца — переходим пространственно к списку игроков
                }
                else if (dx > 0 && slotIndex % GridCols == GridCols - 1)
                {
                    // Правый край строки — не перескакиваем на следующую строку
                }
                else if (dy < 0 && slotIndex < GridCols)
                {
                    // Верхний ряд — выходим вверх на вкладку «Вещи»
                    SetFocus(FocusItems);
                    return;
                }
                else
                {
                    int next = slotIndex + (dx != 0 ? dx : dy * GridCols);

                    if (next >= 0 && next < count)
                    {
                        SelectSlot(next, maxOffset);
                        return;
                    }

                    // вниз с последнего неполного ряда — встаём на последнюю вещь
                    if (dy > 0 && next >= count && slotIndex < count - 1)
                    {
                        SelectSlot(count - 1, maxOffset);
                        return;
                    }
                }
            }

            Rect current = FindFocusRect(_focusId);
            if (current.width <= 0f)
            {
                SetFocus(_focusables[0].Id);
                return;
            }

            Vector2 from = current.center;
            string best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < _focusables.Count; i++)
            {
                Focusable candidate = _focusables[i];
                if (candidate.Id == _focusId) continue;

                Vector2 to = candidate.Rect.center;
                float along = dx != 0 ? (to.x - from.x) * dx : (to.y - from.y) * dy;
                float across = dx != 0 ? Mathf.Abs(to.y - from.y) : Mathf.Abs(to.x - from.x);

                if (along <= 2f) continue;

                float score = along + across * 2.2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate.Id;
                }
            }

            if (best != null)
                SetFocus(best);
        }

        private Rect FindFocusRect(string id)
        {
            if (string.IsNullOrEmpty(id)) return new Rect();
            for (int i = 0; i < _focusables.Count; i++)
                if (_focusables[i].Id == id)
                    return _focusables[i].Rect;
            return new Rect();
        }

        private void SetFocus(string id)
        {
            _focusId = id;

            if (string.IsNullOrEmpty(id)) return;

            if (id.StartsWith(PlayerPrefix, StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(id.Substring(PlayerPrefix.Length), out index))
                    SelectPlayer(index);
                return;
            }

            if (id.StartsWith(ItemPrefix, StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(id.Substring(ItemPrefix.Length), out index) &&
                    index >= 0 && index < _itemView.Count)
                    SelectItem(_itemView[index].Key);
            }
        }

        /// <summary>Переводит фокус на ячейку решётки и сразу выбирает её вещь.</summary>
        private void SelectSlot(int index, int maxOffset)
        {
            if (index < 0 || index >= _itemView.Count) return;

            _focusId = ItemPrefix + index;
            EnsureSlotVisible(index, maxOffset);
            SelectItem(_itemView[index].Key);
        }

        private void SelectPlayer(int index)
        {
            TransferManager mgr = TransferManager.Instance;
            if (mgr == null || index < 0 || index >= mgr.Players.Count) return;

            _selectedIndex = index;
            _selectedId = mgr.Players[index].Id;
            _focusId = PlayerPrefix + index;

            if (index < _playerOffset) _playerOffset = index;
            else if (index >= _playerOffset + VisiblePlayers) _playerOffset = index - VisiblePlayers + 1;
        }

        private void ClampPlayerWindow(int count)
        {
            int maxOffset = Mathf.Max(0, count - VisiblePlayers);
            _playerOffset = Mathf.Clamp(_playerOffset, 0, maxOffset);
        }

        private void HandleScrollWheel(Rect panel, float s)
        {
            if (Event.current.type != EventType.ScrollWheel) return;

            Vector2 mouse = Event.current.mousePosition;
            int delta = Event.current.delta.y > 0f ? 1 : -1;

            Rect list = new Rect(panel.x + Pad * s, panel.y + 142f * s, ListW * s, RowH * VisiblePlayers * s);
            Rect journal = new Rect(panel.x + Pad * s, panel.y + (386f + _extra) * s, panel.width - Pad * s * 2f, 126f * s);

            if (_resource == TransferManager.ResourceKind.Item && _gridRect.Contains(mouse))
            {
                ScrollGrid(delta);
                Event.current.Use();
            }
            else if (list.Contains(mouse))
            {
                TransferManager mgr = TransferManager.Instance;
                int count = mgr != null ? mgr.Players.Count : 0;
                _playerOffset = Mathf.Clamp(_playerOffset + delta, 0, Mathf.Max(0, count - VisiblePlayers));
                Event.current.Use();
            }
            else if (journal.Contains(mouse))
            {
                ScrollLog(-delta);
                Event.current.Use();
            }
        }

        /// <summary>Прокрутка решётки предметов построчно.</summary>
        private void ScrollGrid(int delta)
        {
            int rows = Mathf.Max(1, (_itemView.Count + GridCols - 1) / GridCols);
            _itemRowOffset = Mathf.Clamp(_itemRowOffset + delta, 0, Mathf.Max(0, rows - GridRows));
        }

        private void ScrollLog(int delta)
        {
            TransferManager mgr = TransferManager.Instance;
            int total = mgr != null ? mgr.History.Count : 0;
            _logOffset = Mathf.Clamp(_logOffset + delta, 0, Mathf.Max(0, total - 1));
        }

        // ---------------- Суммы ----------------

        private int EffectiveBalance()
        {
            if (_resource == TransferManager.ResourceKind.Shards) return GameBridge.GetShards();
            if (_resource == TransferManager.ResourceKind.Item)
                return string.IsNullOrEmpty(_itemKey) ? 0 : ItemBridge.Count(_itemKey);
            return GameBridge.GetGeo();
        }

        private void ClampAmountToBalance()
        {
            int balance = EffectiveBalance();
            if (_amount > balance) SetAmount(balance);
        }

        private void SetAmount(int amount)
        {
            if (amount < 0) amount = 0;
            _amount = amount;
            _amountText = amount > 0 ? amount.ToString() : string.Empty;
        }

        private void StepAmount(int direction)
        {
            // у вещей стопки маленькие — шагаем по одной штуке
            int step = _resource == TransferManager.ResourceKind.Item
                ? 1
                : (_amount < 200 ? 10 : (_amount < 2000 ? 50 : (_amount < 20000 ? 250 : 1000)));
            int cap = EffectiveBalance();
            if (cap <= 0)
            {
                SetAmount(0);
                return;
            }

            SetAmount(Mathf.Clamp(_amount + step * direction, 0, cap));
        }

        private void CyclePreset(int direction)
        {
            // у вещей «пресеты сумм» не нужны — бампер меняет количество на единицу
            if (_resource == TransferManager.ResourceKind.Item)
            {
                StepAmount(direction);
                return;
            }

            int index = -1;
            for (int i = 0; i < PresetAmounts.Length; i++)
                if (_amount == PresetAmounts[i]) index = i;

            if (index < 0)
                index = direction > 0 ? -1 : PresetAmounts.Length;

            index += direction;
            if (index < 0) index = PresetAmounts.Length - 1;
            if (index >= PresetAmounts.Length) index = 0;

            int balance = EffectiveBalance();
            SetAmount(balance > 0 ? Mathf.Min(PresetAmounts[index], balance) : 0);
        }

        private static string FilterDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            char[] chars = text.ToCharArray();
            int w = 0;
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] >= '0' && chars[i] <= '9')
                    chars[w++] = chars[i];

            return new string(chars, 0, w);
        }

        // ---------------- Ошибки ----------------

        private static void NotifyError(TransferManager.SendError error)
        {
            string text;
            switch (error)
            {
                case TransferManager.SendError.NoLobby:
                    text = Texts.T("Нет подключения к лобби", "Not connected to a lobby");
                    break;
                case TransferManager.SendError.NotInGame:
                    text = Texts.NeedSave;
                    break;
                case TransferManager.SendError.NoTarget:
                    text = Texts.T("Выберите игрока в списке", "Select a player in the list");
                    break;
                case TransferManager.SendError.InvalidAmount:
                    text = Texts.T("Введите корректную сумму", "Enter a valid amount");
                    break;
                case TransferManager.SendError.ExceedsLimit:
                    text = string.Format(Texts.T("Превышен лимит суммы ({0})", "Amount exceeds the limit ({0})"), ModConfig.MaxSendAmount);
                    break;
                case TransferManager.SendError.NotEnough:
                    text = Texts.T("Недостаточно бусин", "Not enough beads");
                    break;
                case TransferManager.SendError.Cooldown:
                    text = Texts.T("Слишком часто — подождите секунду", "Too fast — wait a second");
                    break;
                case TransferManager.SendError.TooManyPending:
                    text = Texts.T("Слишком много незавершённых переводов", "Too many pending transfers");
                    break;
                case TransferManager.SendError.NetworkError:
                    text = Texts.T("Ошибка сети — бусины возвращены", "Network error — beads refunded");
                    break;
                default:
                    text = error.ToString();
                    break;
            }

            if (ToastLog.Instance != null)
                ToastLog.Instance.Add(text, ToastLog.Kind.Warn);
        }
    }
}

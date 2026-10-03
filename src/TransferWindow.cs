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

        // ---------------- Состояние ----------------

        private bool _open;

        private CSteamID _selectedId;
        private int _selectedIndex = -1;
        private string _amountText = "100";
        private int _amount = 100;

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

            bool toggleByKey = Input.GetKeyDown(ModConfig.MenuKeyCode);
            bool toggleByPad = ModConfig.MenuCombo != null && ModConfig.MenuCombo.Triggered();

            if (toggleByKey || toggleByPad)
            {
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

            GameBridge.TickInputBlock();
            TrackPointer();
            HandleMenuInput();

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

        private void HandleMenuInput()
        {
            // «фронты» прошлого кадра уже отрисованы — начинаем с чистого листа
            _activate = false;
            _navX = 0;
            _navY = 0;

            // --- закрытие ---
            if (Input.GetKeyDown(KeyCode.Escape) || GamepadInput.CancelPressed() ||
                GamepadInput.Pressed(GamepadInput.Btn.Start))
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
            if (GamepadInput.ConfirmPressed() ||
                (!_textFieldFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))))
            {
                _activate = true;
                _pointerMode = false;
            }

            // --- быстрые действия геймпада ---
            if (GamepadInput.Pressed(GamepadInput.Btn.LeftBumper)) CyclePreset(-1);
            if (GamepadInput.Pressed(GamepadInput.Btn.RightBumper)) CyclePreset(1);
            if (GamepadInput.Pressed(GamepadInput.Btn.Alt)) SetAmount(EffectiveBalance());

            if (GamepadInput.Pressed(GamepadInput.Btn.LeftTrigger)) ScrollLog(-1);
            if (GamepadInput.Pressed(GamepadInput.Btn.RightTrigger)) ScrollLog(1);
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

            TransferManager.SendError error = mgr.TrySend(target, _amount);
            if (error != TransferManager.SendError.Ok)
                NotifyError(error);
        }

        // ---------------- Отрисовка ----------------

        private void OnGUI()
        {
            if (!_open) return;

            float scale = ComputeScale();
            SilkUi.EnsureStyles(scale);
            GUI.depth = -500;

            Rect panel = new Rect(
                Mathf.Round((Screen.width - PanelW * scale) * 0.5f),
                Mathf.Round((Screen.height - PanelH * scale) * 0.5f),
                Mathf.Round(PanelW * scale),
                Mathf.Round(PanelH * scale));

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

            if (Event.current.type == EventType.Repaint)
            {
                ProcessNavigation();
                DrawSoftCursor(scale);
            }
        }

        private float ComputeScale()
        {
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            scale = Mathf.Clamp(scale, 0.62f, 2.2f) * Mathf.Clamp(ModConfig.UiScale, 0.5f, 3f);

            float maxByWidth = (Screen.width - 32f) / PanelW;
            float maxByHeight = (Screen.height - 32f) / PanelH;
            return Mathf.Max(0.4f, Mathf.Min(scale, Mathf.Min(maxByWidth, maxByHeight)));
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
            string balance = GameBridge.GetGeo().ToString();
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

            SilkUi.Text(new Rect(x0, panel.y + 118f * s, ListW * s, 20f * s),
                Texts.PlayersHeader.ToUpperInvariant(), SilkUi.Section, UiKit.Gold);

            Rect well = new Rect(x0, panel.y + 142f * s, ListW * s, RowH * VisiblePlayers * s);
            SilkUi.Well(well, s);

            int count = mgr != null ? mgr.Players.Count : 0;
            if (count == 0)
            {
                _playerOffset = 0;
                SilkUi.Text(well, Texts.NoPlayers, SilkUi.Empty, UiKit.BoneDim);
                return;
            }

            ClampPlayerWindow(count);

            int last = Mathf.Min(count, _playerOffset + VisiblePlayers);
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

            if (count > VisiblePlayers)
            {
                SilkUi.Text(new Rect(x0, well.yMax + 6f * s, ListW * s, 18f * s),
                    (_playerOffset + 1) + "–" + last + " / " + count, SilkUi.Hint, UiKit.BoneDim);
            }
        }

        private void DrawAmount(Rect panel, float s)
        {
            float rightX = panel.x + (Pad + ListW + ColGap) * s;
            float rightW = panel.width - (Pad + ListW + ColGap) * s - Pad * s;

            SilkUi.Text(new Rect(rightX, panel.y + 118f * s, rightW, 20f * s),
                Texts.AmountHeader.ToUpperInvariant(), SilkUi.Section, UiKit.Gold);

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
            SilkUi.SmallButton(allRect, Texts.AllBeads, _amount > 0 && _amount == EffectiveBalance(), allHover, allFocused, true, s);
            if (allClicked) SetAmount(EffectiveBalance());

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
            bool ready = target != null && _amount > 0;

            string label = ready
                ? string.Format(Texts.SendFormat, _amount, SilkUi.Ellipsize(target.Name, SilkUi.Send, rightW * 0.45f))
                : Texts.ChoosePrompt;

            Rect rect = new Rect(rightX, panel.y + 232f * s, rightW, 54f * s);
            bool hover, focused;
            bool clicked = Control(FocusSend, rect, false, out hover, out focused);
            SilkUi.OrnateButton(rect, label, ready, hover, focused, s);
            if (clicked) Send();

            string hint = ModConfig.QuickSendCombo != null
                ? string.Format("{0} / {1} — {2} ({3})", ModConfig.QuickSendKeyCode, ModConfig.QuickSendCombo.Text,
                    Texts.HintQuickSend.ToLowerInvariant(), ModConfig.QuickSendAmount)
                : string.Format("{0} — {1} ({2})", ModConfig.QuickSendKeyCode, Texts.HintQuickSend.ToLowerInvariant(), ModConfig.QuickSendAmount);

            SilkUi.Text(new Rect(rightX, panel.y + 292f * s, rightW, 18f * s), hint, SilkUi.Hint, UiKit.BoneDim);
        }

        private void DrawJournal(Rect panel, float s)
        {
            TransferManager mgr = TransferManager.Instance;
            float x0 = panel.x + Pad * s;
            float width = panel.width - Pad * s * 2f;

            SilkUi.Text(new Rect(x0, panel.y + 372f * s, width, 20f * s),
                Texts.HistoryHeader.ToUpperInvariant(), SilkUi.Section, UiKit.Gold);

            Rect well = new Rect(x0, panel.y + 396f * s, width, 116f * s);
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
            float y = panel.y + 538f * s;

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

            if (!string.IsNullOrEmpty(id) && id.StartsWith(PlayerPrefix, StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(id.Substring(PlayerPrefix.Length), out index))
                    SelectPlayer(index);
            }
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
            Rect journal = new Rect(panel.x + Pad * s, panel.y + 396f * s, panel.width - Pad * s * 2f, 116f * s);

            if (list.Contains(mouse))
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

        private void ScrollLog(int delta)
        {
            TransferManager mgr = TransferManager.Instance;
            int total = mgr != null ? mgr.History.Count : 0;
            _logOffset = Mathf.Clamp(_logOffset + delta, 0, Mathf.Max(0, total - 1));
        }

        // ---------------- Суммы ----------------

        private static int EffectiveBalance()
        {
            return GameBridge.GetGeo();
        }

        private void SetAmount(int amount)
        {
            if (amount < 0) amount = 0;
            _amount = amount;
            _amountText = amount > 0 ? amount.ToString() : string.Empty;
        }

        private void StepAmount(int direction)
        {
            int step = _amount < 200 ? 10 : (_amount < 2000 ? 50 : (_amount < 20000 ? 250 : 1000));
            int cap = EffectiveBalance();
            if (cap <= 0) cap = ModConfig.MaxSendAmount;

            SetAmount(Mathf.Clamp(_amount + step * direction, 0, cap));
        }

        private void CyclePreset(int direction)
        {
            int index = -1;
            for (int i = 0; i < PresetAmounts.Length; i++)
                if (_amount == PresetAmounts[i]) index = i;

            if (index < 0)
                index = direction > 0 ? -1 : PresetAmounts.Length;

            index += direction;
            if (index < 0) index = PresetAmounts.Length - 1;
            if (index >= PresetAmounts.Length) index = 0;

            SetAmount(PresetAmounts[index]);
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

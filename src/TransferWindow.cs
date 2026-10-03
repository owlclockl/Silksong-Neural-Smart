using System;
using Steamworks;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// IMGUI-окно обмена бусинами (по умолчанию F7):
    /// список игроков лобби, выбор суммы, отправка, журнал переводов.
    /// </summary>
    internal sealed class TransferWindow : MonoBehaviour
    {
        private const int WindowId = 845120;
        private const float WinWidth = 560f;
        private const float WinHeight = 440f;

        private static readonly int[] PresetAmounts = { 100, 500, 1000, 5000 };

        private bool _open;
        private bool _rectInitialized;
        private Rect _winRect = new Rect(0, 0, WinWidth, WinHeight);

        private CSteamID _selectedId;
        private int _selectedIndex = -1;
        private string _amountText = "100";
        private int _amount = 100;

        private Vector2 _playersScroll = Vector2.zero;
        private Vector2 _logScroll = Vector2.zero;
        private int _seenHistoryVersion;

        private bool _cursorSaved;
        private CursorLockMode _prevLockState;
        private bool _prevCursorVisible;

        // ---------------- Стили (лениво, только из OnGUI) ----------------

        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _balanceStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _rowSelectedStyle;
        private GUIStyle _sendButtonStyle;
        private GUIStyle _fieldStyle;
        private GUIStyle _smallLabelStyle;
        private GUIStyle[] _logKindStyles;

        // ---------------- Жизненный цикл окна ----------------

        private void Update()
        {
            if (Input.GetKeyDown(ModConfig.MenuKeyCode))
                Toggle();

            if (!_open && Input.GetKeyDown(ModConfig.QuickSendKeyCode))
                QuickSend();
        }

        private void Toggle()
        {
            _open = !_open;

            if (_open)
            {
                SaveCursor();
                EnsureRect();
            }
            else
            {
                RestoreCursor();
            }
        }

        private void EnsureRect()
        {
            if (_rectInitialized) return;
            _rectInitialized = true;
            _winRect = new Rect(
                (Screen.width - WinWidth) / 2f,
                (Screen.height - WinHeight) / 2f,
                WinWidth, WinHeight);
        }

        private void SaveCursor()
        {
            if (_cursorSaved) return;
            _cursorSaved = true;
            _prevLockState = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            if (!_cursorSaved) return;
            _cursorSaved = false;
            Cursor.lockState = _prevLockState;
            Cursor.visible = _prevCursorVisible;
        }

        private void OnDisable()
        {
            RestoreCursor();
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

        // ---------------- Отрисовка ----------------

        private void OnGUI()
        {
            if (!_open) return;

            EnsureStyles();

            _winRect = GUI.Window(WindowId, _winRect, DrawWindowContents, string.Empty, _windowStyle);

            // окно не должно уползать за края экрана
            float pad = 20f;
            _winRect.x = Mathf_Clamp(_winRect.x, -_winRect.width + pad * 2, Screen.width - pad * 2);
            _winRect.y = Mathf_Clamp(_winRect.y, 0, Screen.height - pad);
        }

        private static float Mathf_Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private void DrawWindowContents(int id)
        {
            TransferManager mgr = TransferManager.Instance;

            GUILayout.BeginVertical();

            DrawHeader(mgr);
            DrawStatusLine(mgr);

            GUILayout.BeginHorizontal();

            DrawPlayersColumn(mgr);

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawAmountBlock(mgr);
            DrawSendBlock(mgr);
            DrawLog(mgr);
            DrawFooter(mgr);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void DrawHeader(TransferManager mgr)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Texts.WindowTitle, _titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format(Texts.BalanceFormat, GameBridge.GetGeo()), _balanceStyle, GUILayout.Width(180));
            GUILayout.EndHorizontal();
        }

        private void DrawStatusLine(TransferManager mgr)
        {
            string status;
            if (mgr == null || !XvXBridge.BridgeOk)
                status = Texts.StatusNoMod;
            else if (!mgr.InLobby)
                status = Texts.StatusNoLobby;
            else if (!GameBridge.InGame)
                status = Texts.NeedSave;
            else
                status = string.Format(Texts.LobbyCountFormat, mgr.Players.Count + 1) +
                         (mgr.PendingCount > 0
                             ? "  ·  " + Texts.T("ожидание: ", "pending: ") + mgr.PendingCount
                             : string.Empty);

            GUILayout.Label(status, _statusStyle);
        }

        private void DrawPlayersColumn(TransferManager mgr)
        {
            GUILayout.BeginVertical(GUILayout.Width(240));

            GUILayout.Label(Texts.PlayersHeader, _headerStyle);

            _playersScroll = GUILayout.BeginScrollView(_playersScroll, GUILayout.Height(236));

            if (mgr == null || mgr.Players.Count == 0)
            {
                GUILayout.Label(Texts.NoPlayers, _hintStyle);
            }
            else
            {
                for (int i = 0; i < mgr.Players.Count; i++)
                {
                    TransferManager.RemotePlayer player = mgr.Players[i];
                    bool selected = player.Id == _selectedId;
                    GUIStyle style = selected ? _rowSelectedStyle : _rowStyle;

                    if (GUILayout.Button(player.Name, style, GUILayout.Height(26)))
                    {
                        _selectedId = player.Id;
                        _selectedIndex = i;
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawAmountBlock(TransferManager mgr)
        {
            GUILayout.Label(Texts.AmountHeader, _headerStyle);

            // пресеты
            GUILayout.BeginHorizontal();
            for (int i = 0; i < PresetAmounts.Length; i++)
            {
                int preset = PresetAmounts[i];
                bool active = _amount == preset && _amountText == preset.ToString();

                if (GUILayout.Button(preset.ToString(), active ? _rowSelectedStyle : GUI.skin.button, GUILayout.Width(52)))
                    SetAmount(preset);
            }

            if (GUILayout.Button(Texts.AllBeads, GUI.skin.button, GUILayout.Width(52)))
                SetAmount(EffectiveBalance());

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // своё значение
            GUILayout.BeginHorizontal();
            GUILayout.Label(Texts.T("Своё:", "Custom:"), _smallLabelStyle, GUILayout.Width(46));

            string before = _amountText;
            string after = GUILayout.TextField(_amountText, 9, _fieldStyle, GUILayout.Width(90));
            if (after != before)
            {
                _amountText = FilterDigits(after);
                int parsed;
                _amount = int.TryParse(_amountText, out parsed) ? parsed : 0;
            }

            GUILayout.Label(Texts.T("доступно: ", "available: ") + EffectiveBalance(), _hintStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawSendBlock(TransferManager mgr)
        {
            if (mgr == null) return;

            TransferManager.RemotePlayer target = ResolveSelection();

            string label = target != null
                ? string.Format(Texts.T("Отправить {0} → {1}", "Send {0} → {1}"), _amount, target.Name)
                : Texts.T("Выберите игрока и сумму", "Select a player and amount");

            if (GUILayout.Button(label, _sendButtonStyle, GUILayout.Height(30)))
            {
                if (target == null)
                {
                    NotifyError(TransferManager.SendError.NoTarget);
                }
                else
                {
                    TransferManager.SendError error = mgr.TrySend(target, _amount);
                    if (error != TransferManager.SendError.Ok)
                        NotifyError(error);
                }
            }
        }

        private void DrawLog(TransferManager mgr)
        {
            GUILayout.Label(Texts.HistoryHeader, _headerStyle);

            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));

            if (mgr != null)
            {
                if (_seenHistoryVersion != mgr.HistoryVersion)
                {
                    _seenHistoryVersion = mgr.HistoryVersion;
                    _logScroll.y = float.MaxValue; // автопрокрутка вниз
                }

                for (int i = 0; i < mgr.History.Count; i++)
                {
                    TransferManager.HistoryEntry entry = mgr.History[i];
                    GUILayout.Label(entry.Text, _logKindStyles[(int)entry.Kind]);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawFooter(TransferManager mgr)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(
                string.Format(Texts.T("Быстрая отправка: {0} (+{1} бусин выбранному игроку)", "Quick send: {0} (+{1} beads to the selected player)"),
                    ModConfig.QuickSendKeyCode, ModConfig.QuickSendAmount),
                _hintStyle);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(Texts.Close + " (" + ModConfig.MenuKeyCode + ")", GUI.skin.button, GUILayout.Width(110)))
                Toggle();

            GUILayout.EndHorizontal();
        }

        // ---------------- Помощники ----------------

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

        // ---------------- Стили ----------------

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;

            GUISkin skin = GUI.skin;

            _windowStyle = new GUIStyle(skin.window);
            _windowStyle.normal.background = UiKit.Panel;
            _windowStyle.padding.left = 10;
            _windowStyle.padding.right = 10;
            _windowStyle.padding.top = 8;
            _windowStyle.padding.bottom = 8;

            _titleStyle = new GUIStyle(skin.label);
            _titleStyle.fontSize = 16;
            _titleStyle.alignment = TextAnchor.MiddleLeft;
            _titleStyle.normal.textColor = new Color(1f, 0.9f, 0.6f, 1f);

            _balanceStyle = new GUIStyle(skin.label);
            _balanceStyle.fontSize = 13;
            _balanceStyle.alignment = TextAnchor.MiddleRight;
            _balanceStyle.normal.textColor = new Color(1f, 0.85f, 0.5f, 1f);

            _statusStyle = new GUIStyle(skin.label);
            _statusStyle.fontSize = 12;
            _statusStyle.normal.textColor = new Color(0.75f, 0.8f, 0.9f, 1f);

            _hintStyle = new GUIStyle(skin.label);
            _hintStyle.fontSize = 11;
            _hintStyle.alignment = TextAnchor.MiddleLeft;
            _hintStyle.normal.textColor = new Color(0.6f, 0.63f, 0.7f, 1f);

            _headerStyle = new GUIStyle(skin.label);
            _headerStyle.fontSize = 13;
            _headerStyle.normal.textColor = new Color(0.9f, 0.9f, 0.95f, 1f);

            _rowStyle = new GUIStyle(skin.button);
            _rowStyle.alignment = TextAnchor.MiddleLeft;
            _rowStyle.normal.background = UiKit.RowHover;
            _rowStyle.normal.textColor = Color.white;

            _rowSelectedStyle = new GUIStyle(skin.button);
            _rowSelectedStyle.alignment = TextAnchor.MiddleLeft;
            _rowSelectedStyle.normal.background = UiKit.RowSelected;
            _rowSelectedStyle.normal.textColor = Color.white;

            _sendButtonStyle = new GUIStyle(skin.button);
            _sendButtonStyle.fontSize = 14;
            _sendButtonStyle.alignment = TextAnchor.MiddleCenter;
            _sendButtonStyle.normal.background = UiKit.Accent;
            _sendButtonStyle.normal.textColor = Color.white;

            _fieldStyle = new GUIStyle(skin.textField);
            _fieldStyle.alignment = TextAnchor.MiddleLeft;

            _smallLabelStyle = new GUIStyle(skin.label);
            _smallLabelStyle.fontSize = 12;
            _smallLabelStyle.alignment = TextAnchor.MiddleLeft;

            _logKindStyles = new GUIStyle[4];
            _logKindStyles[(int)ToastLog.Kind.Info] = MakeLogStyle(skin.label, new Color(0.85f, 0.87f, 0.95f, 1f));
            _logKindStyles[(int)ToastLog.Kind.Success] = MakeLogStyle(skin.label, new Color(0.55f, 0.95f, 0.6f, 1f));
            _logKindStyles[(int)ToastLog.Kind.Warn] = MakeLogStyle(skin.label, new Color(1f, 0.85f, 0.45f, 1f));
            _logKindStyles[(int)ToastLog.Kind.Error] = MakeLogStyle(skin.label, new Color(1f, 0.55f, 0.55f, 1f));
        }

        private static GUIStyle MakeLogStyle(GUIStyle basis, Color color)
        {
            GUIStyle style = new GUIStyle(basis);
            style.fontSize = 12;
            style.alignment = TextAnchor.UpperLeft;
            style.wordWrap = true;
            style.normal.textColor = color;
            return style;
        }
    }
}

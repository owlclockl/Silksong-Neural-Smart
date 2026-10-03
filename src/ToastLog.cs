using System.Collections.Generic;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Всплывающие уведомления RosaryShare поверх игры (правый верхний угол).
    /// Отдельно от журнала — показываются, только если включены в конфиге.
    /// </summary>
    internal sealed class ToastLog : MonoBehaviour
    {
        public static ToastLog Instance { get; private set; }

        public enum Kind
        {
            Info,
            Success,
            Warn,
            Error,
        }

        private sealed class Toast
        {
            public string Text;
            public Kind Kind;
            public float Until;
        }

        private const int MaxToasts = 5;
        private const float ToastSeconds = 6f;
        private const float BoxWidth = 430f;
        private const float BoxHeight = 24f;
        private const float Margin = 12f;

        private readonly List<Toast> _toasts = new List<Toast>();

        private GUIStyle _shadowStyle;
        private GUIStyle[] _kindStyles;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Add(string text, Kind kind)
        {
            if (string.IsNullOrEmpty(text)) return;

            _toasts.Add(new Toast
            {
                Text = text,
                Kind = kind,
                Until = Time.unscaledTime + ToastSeconds,
            });

            while (_toasts.Count > MaxToasts)
                _toasts.RemoveAt(0);
        }

        private void OnGUI()
        {
            if (!ModConfig.ShowToasts || _toasts.Count == 0) return;

            float now = Time.unscaledTime;
            for (int i = _toasts.Count - 1; i >= 0; i--)
                if (_toasts[i].Until < now) _toasts.RemoveAt(i);

            if (_toasts.Count == 0) return;

            EnsureStyles();

            float x = Screen.width - BoxWidth - Margin;
            float y = Margin;

            for (int i = 0; i < _toasts.Count; i++)
            {
                Rect shadow = new Rect(x + 1f, y + 1f, BoxWidth, BoxHeight);
                Rect main = new Rect(x, y, BoxWidth, BoxHeight);

                GUI.Label(shadow, _toasts[i].Text, _shadowStyle);
                GUI.Label(main, _toasts[i].Text, _kindStyles[(int)_toasts[i].Kind]);

                y += BoxHeight + 4f;
            }
        }

        private void EnsureStyles()
        {
            if (_kindStyles != null) return;

            GUIStyle skinLabel = GUI.skin.label;

            _shadowStyle = new GUIStyle(skinLabel);
            _shadowStyle.fontSize = 14;
            _shadowStyle.alignment = TextAnchor.UpperRight;
            _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);

            _kindStyles = new GUIStyle[4];
            _kindStyles[(int)Kind.Info] = MakeStyle(skinLabel, new Color(0.85f, 0.88f, 1f, 1f));
            _kindStyles[(int)Kind.Success] = MakeStyle(skinLabel, new Color(0.55f, 0.95f, 0.6f, 1f));
            _kindStyles[(int)Kind.Warn] = MakeStyle(skinLabel, new Color(1f, 0.85f, 0.45f, 1f));
            _kindStyles[(int)Kind.Error] = MakeStyle(skinLabel, new Color(1f, 0.5f, 0.5f, 1f));
        }

        private static GUIStyle MakeStyle(GUIStyle basis, Color color)
        {
            GUIStyle style = new GUIStyle(basis);
            style.fontSize = 14;
            style.alignment = TextAnchor.UpperRight;
            style.normal.textColor = color;
            return style;
        }
    }
}

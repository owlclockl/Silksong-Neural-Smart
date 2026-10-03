using System.Text;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Рисование элементов интерфейса в стиле Silksong: резные рамки,
    /// пункты меню с ромбами-маркерами, багровые кнопки, подсказки.
    /// Все размеры приходят уже умноженными на масштаб интерфейса.
    /// </summary>
    internal static class SilkUi
    {
        // ------------------------------------------------------------------
        //  Стили
        // ------------------------------------------------------------------

        public static GUIStyle Title;
        public static GUIStyle Balance;
        public static GUIStyle Status;
        public static GUIStyle Section;
        public static GUIStyle Item;
        public static GUIStyle ItemRight;
        public static GUIStyle Centered;
        public static GUIStyle Send;
        public static GUIStyle Hint;
        public static GUIStyle Field;
        public static GUIStyle Log;
        public static GUIStyle Empty;

        private static float _builtScale = -1f;

        /// <summary>Пересобирает стили, если изменился масштаб (вызывать только из OnGUI).</summary>
        public static void EnsureStyles(float scale)
        {
            if (Title != null && Mathf.Abs(scale - _builtScale) < 0.01f) return;
            _builtScale = scale;

            GUISkin skin = GUI.skin;
            Font font = UiKit.Serif;

            Title = Label(skin, font, 25, scale, TextAnchor.MiddleCenter);
            Balance = Label(skin, font, 16, scale, TextAnchor.MiddleRight);
            Status = Label(skin, font, 13, scale, TextAnchor.MiddleCenter);
            Section = Label(skin, font, 14, scale, TextAnchor.MiddleLeft);
            Item = Label(skin, font, 16, scale, TextAnchor.MiddleLeft);
            ItemRight = Label(skin, font, 14, scale, TextAnchor.MiddleRight);
            Centered = Label(skin, font, 16, scale, TextAnchor.MiddleCenter);
            Send = Label(skin, font, 19, scale, TextAnchor.MiddleCenter);
            Hint = Label(skin, font, 12, scale, TextAnchor.MiddleLeft);
            Empty = Label(skin, font, 14, scale, TextAnchor.MiddleCenter);

            Log = Label(skin, font, 13, scale, TextAnchor.MiddleLeft);
            Log.wordWrap = false;
            Log.clipping = TextClipping.Clip;

            Field = new GUIStyle(skin.textField);
            if (font != null) Field.font = font;
            Field.fontSize = Mathf.Max(8, Mathf.RoundToInt(17 * scale));
            Field.alignment = TextAnchor.MiddleCenter;
            Field.normal.background = UiKit.FieldFill;
            Field.focused.background = UiKit.FieldFill;
            Field.hover.background = UiKit.FieldFill;
            Field.active.background = UiKit.FieldFill;
            Field.normal.textColor = UiKit.Bone;
            Field.focused.textColor = UiKit.Bone;
            Field.hover.textColor = UiKit.Bone;
            Field.active.textColor = UiKit.Bone;
            Field.border = new RectOffset(2, 2, 2, 2);
            Field.margin = new RectOffset(0, 0, 0, 0);
            Field.padding = new RectOffset(4, 4, 0, 0);
        }

        private static GUIStyle Label(GUISkin skin, Font font, int size, float scale, TextAnchor anchor)
        {
            GUIStyle style = new GUIStyle(skin.label);
            if (font != null) style.font = font;
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * scale));
            style.alignment = anchor;
            style.wordWrap = false;
            style.richText = false;
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            return style;
        }

        // ------------------------------------------------------------------
        //  Примитивы
        // ------------------------------------------------------------------

        public static void Fill(Rect rect, Texture2D texture, Color tint)
        {
            if (texture == null) return;
            Color prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, texture);
            GUI.color = prev;
        }

        public static void FillColor(Rect rect, Color color)
        {
            Fill(rect, UiKit.Solid(Color.white), color);
        }

        public static void Tile(Rect rect, Texture2D texture, Color tint, float tileSize)
        {
            if (texture == null || tileSize <= 0f) return;
            Color prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture,
                new Rect(0f, 0f, rect.width / tileSize, rect.height / tileSize));
            GUI.color = prev;
        }

        /// <summary>Прямоугольная рамка заданной толщины.</summary>
        public static void Frame(Rect rect, Color color, float thickness)
        {
            if (thickness <= 0f) return;
            FillColor(new Rect(rect.x, rect.y, rect.width, thickness), color);
            FillColor(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            FillColor(new Rect(rect.x, rect.y + thickness, thickness, rect.height - thickness * 2f), color);
            FillColor(new Rect(rect.xMax - thickness, rect.y + thickness, thickness, rect.height - thickness * 2f), color);
        }

        public static void Diamond(Vector2 center, float size, Color color)
        {
            Fill(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), UiKit.Diamond, color);
        }

        public static void Glow(Rect rect, Color color)
        {
            Fill(rect, UiKit.Glow, color);
        }

        /// <summary>Декоративная линия-разделитель с ромбом посередине.</summary>
        public static void Divider(Rect rect, Color color, bool withDiamond)
        {
            Fill(rect, UiKit.Divider, color);
            if (!withDiamond) return;

            Vector2 centre = new Vector2(rect.center.x, rect.center.y);
            float s = Mathf.Max(5f, rect.height * 5f);
            Diamond(centre, s, color);
            Diamond(new Vector2(centre.x - s * 1.6f, centre.y), s * 0.55f, new Color(color.r, color.g, color.b, color.a * 0.7f));
            Diamond(new Vector2(centre.x + s * 1.6f, centre.y), s * 0.55f, new Color(color.r, color.g, color.b, color.a * 0.7f));
        }

        /// <summary>Текст с мягкой тенью (цвет задаётся здесь, стиль — белый).</summary>
        public static void Text(Rect rect, string text, GUIStyle style, Color color)
        {
            if (string.IsNullOrEmpty(text) || style == null) return;

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f * color.a);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = prev;
        }

        // ------------------------------------------------------------------
        //  Составные элементы
        // ------------------------------------------------------------------

        /// <summary>Фон панели меню: пергамент, зерно, двойная рамка с ромбами в углах.</summary>
        public static void Panel(Rect rect, float scale)
        {
            // мягкая тень/ореол вокруг панели
            float halo = 46f * scale;
            Fill(new Rect(rect.x - halo, rect.y - halo, rect.width + halo * 2f, rect.height + halo * 2f),
                UiKit.Glow, new Color(0f, 0f, 0f, 0.55f));

            Fill(rect, UiKit.Panel, Color.white);
            Tile(rect, UiKit.Grain, Color.white, 64f * Mathf.Max(0.5f, scale));

            // верхний багровый отсвет — «шёлк»
            Fill(new Rect(rect.x, rect.y, rect.width, 90f * scale), UiKit.SilkFill,
                new Color(1f, 1f, 1f, 0.16f));

            float t = 2f * scale;
            Frame(rect, new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.95f), t);

            Rect inner = Inset(rect, 5f * scale);
            Frame(inner, new Color(UiKit.Crimson.r, UiKit.Crimson.g, UiKit.Crimson.b, 0.55f), Mathf.Max(1f, scale));

            // резные углы
            float d = 13f * scale;
            Color corner = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.95f);
            Diamond(new Vector2(rect.x, rect.y), d, corner);
            Diamond(new Vector2(rect.xMax, rect.y), d, corner);
            Diamond(new Vector2(rect.x, rect.yMax), d, corner);
            Diamond(new Vector2(rect.xMax, rect.yMax), d, corner);

            // маленькие «шипы» по центрам сторон
            Color spike = new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.9f);
            Diamond(new Vector2(rect.center.x, rect.y), d * 0.7f, spike);
            Diamond(new Vector2(rect.center.x, rect.yMax), d * 0.7f, spike);
        }

        /// <summary>Рамка-углубление для списков и журнала.</summary>
        public static void Well(Rect rect, float scale)
        {
            FillColor(rect, new Color(0f, 0f, 0f, 0.30f));
            Frame(rect, new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.38f), Mathf.Max(1f, scale));
        }

        /// <summary>Пункт списка (игрок, строка меню) с подсветкой и маркерами.</summary>
        public static void MenuItem(Rect rect, string text, bool selected, bool hover, bool focused, float scale)
        {
            if (selected)
                Fill(rect, UiKit.RowSelected, Color.white);
            else if (hover || focused)
                Fill(rect, UiKit.RowHover, Color.white);

            if (focused)
                FocusMarkers(rect, scale);

            Color color = selected ? UiKit.Bone : (hover || focused ? UiKit.Bone : UiKit.BoneSoft);

            float pad = 16f * scale;
            Rect textRect = new Rect(rect.x + pad, rect.y, rect.width - pad * 2f, rect.height);
            Text(textRect, text, Item, color);

            if (selected)
                Diamond(new Vector2(rect.x + pad * 0.5f, rect.center.y), 8f * scale, UiKit.Crimson);
        }

        /// <summary>Небольшая кнопка-пункт (пресеты сумм, «Закрыть», шаг +/-).</summary>
        public static void SmallButton(Rect rect, string text, bool active, bool hover, bool focused, bool enabled, float scale)
        {
            Color fill = active
                ? new Color(UiKit.Crimson.r, UiKit.Crimson.g, UiKit.Crimson.b, 0.38f)
                : new Color(0f, 0f, 0f, hover || focused ? 0.42f : 0.26f);
            FillColor(rect, fill);

            Color border = active
                ? new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.95f)
                : (hover || focused
                    ? new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.72f)
                    : new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.45f));
            Frame(rect, border, Mathf.Max(1f, scale));

            if (focused)
                FocusMarkers(rect, scale);

            Color color = !enabled
                ? UiKit.BoneDim
                : (active || hover || focused ? UiKit.Bone : UiKit.BoneSoft);

            Text(rect, text, Centered, color);
        }

        /// <summary>Главная кнопка — багровый шёлк в резной рамке.</summary>
        public static void OrnateButton(Rect rect, string text, bool enabled, bool hover, bool focused, float scale)
        {
            if (enabled)
            {
                Fill(rect, UiKit.SilkFill, hover || focused ? new Color(1.25f, 1.1f, 1.1f, 1f) : Color.white);

                if (hover || focused)
                {
                    float halo = 18f * scale;
                    Fill(new Rect(rect.x - halo, rect.y - halo, rect.width + halo * 2f, rect.height + halo * 2f),
                        UiKit.Glow, new Color(UiKit.Crimson.r, UiKit.Crimson.g, UiKit.Crimson.b, 0.35f));
                }
            }
            else
            {
                FillColor(rect, new Color(0.08f, 0.07f, 0.08f, 0.75f));
            }

            Color border = enabled
                ? (hover || focused ? UiKit.Gold : new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.75f))
                : new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.45f);
            Frame(rect, border, Mathf.Max(1f, 2f * scale));

            Rect inner = Inset(rect, 4f * scale);
            Frame(inner, new Color(0f, 0f, 0f, 0.35f), Mathf.Max(1f, scale));

            float d = 9f * scale;
            Diamond(new Vector2(rect.x, rect.center.y), d, border);
            Diamond(new Vector2(rect.xMax, rect.center.y), d, border);

            if (focused)
                FocusMarkers(rect, scale);

            Text(rect, text, Send, enabled ? UiKit.Bone : UiKit.BoneDim);
        }

        /// <summary>Пульсирующие ромбы фокуса геймпада/клавиатуры.</summary>
        public static void FocusMarkers(Rect rect, float scale)
        {
            float pulse = 0.62f + 0.38f * Mathf.Sin(Time.unscaledTime * 5.5f);
            Color c = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.55f + 0.45f * pulse);
            float size = 10f * scale;
            float off = 9f * scale;
            Diamond(new Vector2(rect.x - off, rect.center.y), size, c);
            Diamond(new Vector2(rect.xMax + off, rect.center.y), size, c);
        }

        /// <summary>Клавиша/кнопка в подсказке: [A] Передать.</summary>
        public static float HintChip(Vector2 position, string key, string caption, float scale)
        {
            GUIContent keyContent = new GUIContent(key);
            Vector2 keySize = Hint.CalcSize(keyContent);
            float h = Mathf.Round(20f * scale);
            float keyW = Mathf.Max(h, keySize.x + 10f * scale);

            Rect keyRect = new Rect(position.x, position.y - h * 0.5f, keyW, h);
            FillColor(keyRect, new Color(0f, 0f, 0f, 0.45f));
            Frame(keyRect, new Color(UiKit.GoldDim.r, UiKit.GoldDim.g, UiKit.GoldDim.b, 0.75f), 1f);

            GUIStyle centered = Hint;
            TextAnchor prev = centered.alignment;
            centered.alignment = TextAnchor.MiddleCenter;
            Text(keyRect, key, centered, UiKit.Gold);
            centered.alignment = prev;

            float x = keyRect.xMax + 6f * scale;
            Vector2 capSize = Hint.CalcSize(new GUIContent(caption));
            Rect capRect = new Rect(x, position.y - h * 0.5f, capSize.x + 2f, h);
            Text(capRect, caption, Hint, UiKit.BoneDim);

            return capRect.xMax + 16f * scale;
        }

        public static Rect Inset(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
        }

        /// <summary>«Р О З А Р И Й» — разрядка заголовка, как в меню игры.</summary>
        public static string Spaced(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            StringBuilder sb = new StringBuilder(text.Length * 2);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ')
                {
                    sb.Append("   ");
                    continue;
                }

                sb.Append(c);
                if (i < text.Length - 1 && text[i + 1] != ' ')
                    sb.Append(' ');
            }

            return sb.ToString();
        }

        /// <summary>Обрезает строку по ширине, добавляя многоточие.</summary>
        public static string Ellipsize(string text, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || style == null) return text;
            if (style.CalcSize(new GUIContent(text)).x <= maxWidth) return text;

            string result = text;
            while (result.Length > 1 && style.CalcSize(new GUIContent(result + "…")).x > maxWidth)
                result = result.Substring(0, result.Length - 1);

            return result + "…";
        }
    }
}

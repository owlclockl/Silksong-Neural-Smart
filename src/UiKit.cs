using System;
using System.Collections.Generic;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Палитра и процедурные текстуры интерфейса в стиле Silksong:
    /// тёмный «костяной» пергамент, багровые акценты, золотая вязь.
    /// Все текстуры генерируются кодом (мод остаётся одним .dll без ассетов)
    /// и кешируются на всю сессию.
    /// </summary>
    internal static class UiKit
    {
        // ------------------------------------------------------------------
        //  Палитра
        // ------------------------------------------------------------------

        /// <summary>Светлая «кость» — основной цвет текста.</summary>
        public static readonly Color Bone = new Color(0.945f, 0.918f, 0.855f, 1f);

        /// <summary>Приглушённая кость — неактивные пункты.</summary>
        public static readonly Color BoneSoft = new Color(0.760f, 0.735f, 0.685f, 1f);

        /// <summary>Тусклая кость — подсказки и второстепенный текст.</summary>
        public static readonly Color BoneDim = new Color(0.545f, 0.525f, 0.495f, 1f);

        /// <summary>Золото — заголовки, количество бусин.</summary>
        public static readonly Color Gold = new Color(0.878f, 0.745f, 0.455f, 1f);

        /// <summary>Тусклое золото — рамки и разделители.</summary>
        public static readonly Color GoldDim = new Color(0.560f, 0.465f, 0.285f, 1f);

        /// <summary>Багрянец плаща Хорнет — выделение и акценты.</summary>
        public static readonly Color Crimson = new Color(0.725f, 0.165f, 0.205f, 1f);

        /// <summary>Светлый багрянец — наведение.</summary>
        public static readonly Color CrimsonLight = new Color(0.905f, 0.330f, 0.330f, 1f);

        /// <summary>Глубокая тень багрянца.</summary>
        public static readonly Color CrimsonDeep = new Color(0.265f, 0.045f, 0.070f, 1f);

        /// <summary>Почти чёрные чернила — фон панели.</summary>
        public static readonly Color Ink = new Color(0.047f, 0.043f, 0.055f, 1f);

        public static readonly Color Success = new Color(0.600f, 0.880f, 0.620f, 1f);
        public static readonly Color Warn = new Color(0.960f, 0.800f, 0.420f, 1f);
        public static readonly Color Error = new Color(0.930f, 0.420f, 0.400f, 1f);

        // ------------------------------------------------------------------
        //  Кеш текстур
        // ------------------------------------------------------------------

        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        /// <summary>Однопиксельная заливка нужного цвета (совместимость со старым кодом).</summary>
        public static Texture2D Get(string key, float r, float g, float b, float a)
        {
            Texture2D tex;
            if (Textures.TryGetValue(key, out tex) && tex != null)
                return tex;

            tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            // чтобы Unity не собрал наши текстуры через Resources.UnloadUnusedAssets
            // при смене сцены — они нужны стилям окна всю сессию
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixel(0, 0, new Color(r, g, b, a));
            tex.Apply();
            Textures[key] = tex;
            return tex;
        }

        /// <summary>Однопиксельная заливка произвольного цвета.</summary>
        public static Texture2D Solid(Color c)
        {
            string key = "solid:" + c.r.ToString("F3") + "," + c.g.ToString("F3") + "," +
                         c.b.ToString("F3") + "," + c.a.ToString("F3");
            return Get(key, c.r, c.g, c.b, c.a);
        }

        private static Texture2D Make(string key, int w, int h, bool repeat, Func<float, float, Color> shade)
        {
            Texture2D tex;
            if (Textures.TryGetValue(key, out tex) && tex != null)
                return tex;

            tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                // v = 0 у нижнего края текстуры (так же, как рисует GUI.DrawTexture)
                float v = h > 1 ? (float)y / (h - 1) : 0f;
                for (int x = 0; x < w; x++)
                {
                    float u = w > 1 ? (float)x / (w - 1) : 0f;
                    pixels[y * w + x] = shade(u, v);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            Textures[key] = tex;
            return tex;
        }

        // ------------------------------------------------------------------
        //  Текстуры окна
        // ------------------------------------------------------------------

        /// <summary>Фон панели: вертикальный градиент тёмного пергамента с лёгким свечением сверху.</summary>
        public static Texture2D Panel
        {
            get
            {
                return Make("panel", 8, 160, false, delegate(float u, float v)
                {
                    // сверху чуть теплее и светлее, книзу уходит в чернила
                    float t = Smooth(v);
                    float r = Mathf.Lerp(0.038f, 0.105f, t);
                    float g = Mathf.Lerp(0.034f, 0.088f, t);
                    float b = Mathf.Lerp(0.045f, 0.098f, t);

                    // мягкая «подсветка» по центру по горизонтали
                    float centre = 1f - Mathf.Abs(u - 0.5f) * 2f;
                    float lift = centre * centre * 0.018f;

                    return new Color(r + lift, g + lift * 0.9f, b + lift, 0.965f);
                });
            }
        }

        /// <summary>Тонкое зерно поверх панели (тайлится).</summary>
        public static Texture2D Grain
        {
            get
            {
                return Make("grain", 64, 64, true, delegate(float u, float v)
                {
                    float n = Noise(u * 64f, v * 64f);
                    float n2 = Noise(u * 64f * 0.37f + 11.3f, v * 64f * 0.37f + 7.7f);
                    float a = (n * 0.65f + n2 * 0.35f);
                    a = (a - 0.5f) * 2f;
                    return a > 0f
                        ? new Color(0.85f, 0.80f, 0.72f, a * 0.030f)
                        : new Color(0f, 0f, 0f, -a * 0.055f);
                });
            }
        }

        /// <summary>Радиальное затемнение экрана за меню.</summary>
        public static Texture2D Vignette
        {
            get
            {
                return Make("vignette", 128, 128, false, delegate(float u, float v)
                {
                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                    float a = Mathf.Clamp01((d - 0.18f) / 0.82f);
                    return new Color(0.01f, 0.01f, 0.015f, a * a * 0.92f);
                });
            }
        }

        /// <summary>Мягкое радиальное свечение (для подсветки выбранного и курсора).</summary>
        public static Texture2D Glow
        {
            get
            {
                return Make("glow", 64, 64, false, delegate(float u, float v)
                {
                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = 1f - d;
                    return new Color(1f, 1f, 1f, a * a * a);
                });
            }
        }

        /// <summary>Подсветка выбранной строки: багровая волна, затухающая вправо.</summary>
        public static Texture2D RowSelected
        {
            get
            {
                return Make("rowSelected", 64, 8, false, delegate(float u, float v)
                {
                    float fade = Mathf.Pow(1f - u, 1.6f);
                    float edge = 1f - Mathf.Abs(v - 0.5f) * 2f;
                    float a = fade * (0.42f + edge * 0.24f);
                    return new Color(0.52f, 0.12f, 0.16f, a);
                });
            }
        }

        /// <summary>Подсветка строки под курсором.</summary>
        public static Texture2D RowHover
        {
            get
            {
                return Make("rowHover", 64, 8, false, delegate(float u, float v)
                {
                    float fade = Mathf.Pow(1f - u, 2.0f);
                    return new Color(0.92f, 0.88f, 0.80f, fade * 0.125f);
                });
            }
        }

        /// <summary>Заливка главной кнопки — багровый шёлк.</summary>
        public static Texture2D SilkFill
        {
            get
            {
                return Make("silkFill", 8, 48, false, delegate(float u, float v)
                {
                    float t = Smooth(v);
                    float r = Mathf.Lerp(0.215f, 0.470f, t);
                    float g = Mathf.Lerp(0.038f, 0.090f, t);
                    float b = Mathf.Lerp(0.055f, 0.115f, t);
                    return new Color(r, g, b, 0.95f);
                });
            }
        }

        /// <summary>Заливка поля ввода.</summary>
        public static Texture2D FieldFill
        {
            get
            {
                return Make("fieldFill", 8, 32, false, delegate(float u, float v)
                {
                    float t = Smooth(v);
                    float g = Mathf.Lerp(0.085f, 0.045f, t);
                    return new Color(g * 0.92f, g * 0.88f, g, 0.95f);
                });
            }
        }

        /// <summary>Декоративная черта: ярче в центре, растворяется к краям.</summary>
        public static Texture2D Divider
        {
            get
            {
                return Make("divider", 128, 1, false, delegate(float u, float v)
                {
                    float a = Mathf.Sin(u * Mathf.PI);
                    a = Mathf.Pow(Mathf.Clamp01(a), 0.7f);
                    return new Color(0.70f, 0.58f, 0.34f, a);
                });
            }
        }

        /// <summary>Ромб-орнамент (углы рамки, маркеры выбора).</summary>
        public static Texture2D Diamond
        {
            get
            {
                return Make("diamond", 32, 32, false, delegate(float u, float v)
                {
                    float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                    float a = 1f - Mathf.Clamp01((d - 0.34f) / 0.06f);
                    return new Color(1f, 1f, 1f, a);
                });
            }
        }

        /// <summary>Курсор-игла с бусиной — «мышь» в стиле Silksong.</summary>
        public static Texture2D CursorNeedle
        {
            get { return Make("cursorNeedle", CursorSize, CursorSize, false, ShadeCursor); }
        }

        /// <summary>Размер стороны текстуры курсора в пикселях.</summary>
        public const int CursorSize = 40;

        /// <summary>Положение острия внутри текстуры курсора (горячая точка).</summary>
        public static readonly Vector2 CursorHotspot = new Vector2(3f, 3f);

        private static Color ShadeCursor(float u, float v)
        {
            // переводим в пиксели с началом в левом ВЕРХНЕМ углу
            float x = u * (CursorSize - 1);
            float y = (1f - v) * (CursorSize - 1);

            const float tipX = 3f, tipY = 3f;     // остриё иглы
            const float endX = 25f, endY = 25f;   // ушко иглы
            const float beadX = 29.5f, beadY = 29.5f, beadR = 6.2f;

            float t;
            float dLine = DistanceToSegment(x, y, tipX, tipY, endX, endY, out t);
            float halfWidth = Mathf.Lerp(0.15f, 2.45f, Mathf.Pow(t, 0.75f));

            float body = 1f - Mathf.Clamp01((dLine - halfWidth) / 1.05f);
            float bodyOutline = 1f - Mathf.Clamp01((dLine - (halfWidth + 1.35f)) / 1.1f);

            float dBead = Mathf.Sqrt((x - beadX) * (x - beadX) + (y - beadY) * (y - beadY));
            float bead = 1f - Mathf.Clamp01((dBead - beadR) / 1.05f);
            float beadOutline = 1f - Mathf.Clamp01((dBead - (beadR + 1.3f)) / 1.1f);

            Color c = new Color(0f, 0f, 0f, 0f);

            // тёмный контур, чтобы курсор читался на любом фоне
            float outline = Mathf.Max(bodyOutline, beadOutline);
            c = Over(c, new Color(0.035f, 0.030f, 0.040f, 0.92f), outline);

            // тело иглы: у острия ярче
            float shine = Mathf.Lerp(1f, 0.74f, t);
            Color needle = new Color(0.97f * shine, 0.95f * shine, 0.88f * shine, 1f);
            c = Over(c, needle, body);

            // бусина: багровая с золотым ободком и бликом
            if (bead > 0f)
            {
                float rim = Mathf.Clamp01((dBead - (beadR - 1.5f)) / 1.5f);
                Color beadColor = Color.Lerp(new Color(0.80f, 0.20f, 0.24f, 1f),
                                             new Color(0.42f, 0.07f, 0.10f, 1f), rim);
                beadColor = Color.Lerp(beadColor, new Color(0.84f, 0.70f, 0.40f, 1f), rim * rim * 0.75f);
                c = Over(c, beadColor, bead);

                float hx = x - (beadX - 2.1f);
                float hy = y - (beadY - 2.3f);
                float dh = Mathf.Sqrt(hx * hx + hy * hy);
                float highlight = 1f - Mathf.Clamp01(dh / 1.9f);
                c = Over(c, new Color(1f, 0.93f, 0.86f, 1f), highlight * 0.75f * bead);
            }

            return c;
        }

        // ------------------------------------------------------------------
        //  Шрифт
        // ------------------------------------------------------------------

        private static Font _serif;
        private static bool _serifResolved;

        /// <summary>
        /// Системный шрифт с засечками, близкий по духу к шрифту игры.
        /// Если подходящего нет (или отключён в конфиге) — вернётся null,
        /// и стили останутся на стандартном шрифте Unity.
        /// </summary>
        public static Font Serif
        {
            get
            {
                if (_serifResolved) return _serif;
                _serifResolved = true;

                if (!ModConfig.UseSerifFont) return null;

                try
                {
                    List<string> names = new List<string>();
                    if (!string.IsNullOrEmpty(ModConfig.FontName))
                        names.Add(ModConfig.FontName.Trim());

                    // Georgia/Times есть почти везде и содержат кириллицу;
                    // Trajan/Cinzel (шрифты в духе игры) — если пользователь их поставил.
                    names.AddRange(new[]
                    {
                        "Georgia", "Palatino Linotype", "Book Antiqua", "Garamond",
                        "Times New Roman", "PT Serif", "DejaVu Serif", "Liberation Serif",
                        "Noto Serif", "Serif",
                    });

                    Font font = Font.CreateDynamicFontFromOSFont(names.ToArray(), 16);
                    if (font != null)
                    {
                        font.hideFlags = HideFlags.HideAndDontSave;
                        _serif = font;
                    }
                }
                catch (Exception e)
                {
                    RosarySharePlugin.LogDebug("Serif font is not available: " + e.Message);
                    _serif = null;
                }

                return _serif;
            }
        }

        /// <summary>Сбросить кеш шрифта (после смены настроек).</summary>
        public static void ResetFont()
        {
            _serif = null;
            _serifResolved = false;
        }

        // ------------------------------------------------------------------
        //  Мелкая математика
        // ------------------------------------------------------------------

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Noise(float x, float y)
        {
            float v = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by, out float t)
        {
            float vx = bx - ax, vy = by - ay;
            float wx = px - ax, wy = py - ay;
            float len = vx * vx + vy * vy;
            t = len > 0f ? Mathf.Clamp01((wx * vx + wy * vy) / len) : 0f;
            float cx = ax + vx * t, cy = ay + vy * t;
            float dx = px - cx, dy = py - cy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Color Over(Color dst, Color src, float alpha)
        {
            float a = Mathf.Clamp01(src.a * Mathf.Clamp01(alpha));
            if (a <= 0f) return dst;

            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);

            float r = (src.r * a + dst.r * dst.a * (1f - a)) / outA;
            float g = (src.g * a + dst.g * dst.a * (1f - a)) / outA;
            float b = (src.b * a + dst.b * dst.a * (1f - a)) / outA;
            return new Color(r, g, b, outA);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>Общие IMGUI-ресурсы: цветные текстуры для заливок стилей.</summary>
    internal static class UiKit
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        public static Texture2D Panel
        {
            get { return Get("panel", 0.07f, 0.08f, 0.11f, 0.96f); }
        }

        public static Texture2D RowSelected
        {
            get { return Get("rowSelected", 0.2f, 0.35f, 0.55f, 1f); }
        }

        public static Texture2D RowHover
        {
            get { return Get("rowHover", 0.16f, 0.18f, 0.24f, 1f); }
        }

        public static Texture2D Accent
        {
            get { return Get("accent", 0.25f, 0.55f, 0.3f, 1f); }
        }

        public static Texture2D Get(string key, float r, float g, float b, float a)
        {
            Texture2D tex;
            if (Textures.TryGetValue(key, out tex) && tex != null)
                return tex;

            tex = new Texture2D(1, 1);
            // чтобы Unity не собрал наши текстуры через Resources.UnloadUnusedAssets
            // игрок смены сцены — они нужны стилям окна всю сессию
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixel(0, 0, new Color(r, g, b, a));
            tex.Apply();
            Textures[key] = tex;
            return tex;
        }
    }
}

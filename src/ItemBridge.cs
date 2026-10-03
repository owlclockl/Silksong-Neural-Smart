using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Runtime inventory adapter. It intentionally resolves PlayerData from the live game
    /// instead of compiling against a particular Silksong patch. Transferable entries are
    /// discovered from public integer and boolean save fields; volatile combat/currency
    /// fields are excluded. This also lets newer game items appear without a mod update.
    ///
    /// Item icons are resolved from the game's loaded Sprite assets first. A procedural
    /// icon remains only as a safe fallback for a field whose asset has not been loaded
    /// by the current game scene yet.
    /// </summary>
    internal static class ItemBridge
    {
        internal sealed class ItemEntry
        {
            public string Key;
            public string Name;
            public string Category;
            public bool Unique;

            /// <summary>Оригинальный спрайт предмета из ресурсов игры.</summary>
            public UnityEngine.Sprite Sprite;

            /// <summary>Запасная пиктограмма, если игровой Sprite ещё не загружен.</summary>
            public Texture2D FallbackSprite;
            internal FieldInfo Field;
        }

        private sealed class SpriteCandidate
        {
            public UnityEngine.Sprite Sprite;
            public string NormalizedName;
        }

        private static readonly List<ItemEntry> ItemsInternal = new List<ItemEntry>();
        private static readonly Dictionary<string, ItemEntry> ByKey = new Dictionary<string, ItemEntry>();
        private static readonly List<SpriteCandidate> GameSprites = new List<SpriteCandidate>();
        private static readonly HashSet<int> GameSpriteIds = new HashSet<int>();
        private static Type _resolvedType;
        private static bool _loadedSpritesScanned;
        private static bool _resourcesScanned;
        private static float _nextSpriteRefresh;

        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "geo", "ShellShards", "health", "maxHealth", "silk", "silkMax", "isInventoryOpen",
            "respawnMarkerName", "respawnScene", "hazardRespawnLocation", "MPCharge", "MPReserve",
            "profileID", "playTime", "permadeathMode", "atBench", "disablePause"
        };

        public static IReadOnlyList<ItemEntry> Items
        {
            get { EnsureCatalog(); return ItemsInternal; }
        }

        public static void EnsureCatalog()
        {
            object pd = GetPlayerData();
            if (pd == null) return;

            Type type = pd.GetType();
            if (_resolvedType == type && ItemsInternal.Count > 0)
            {
                // The inventory can be opened before some addressable/UI sprites are
                // loaded. Retry cheaply for a few missing icons as the scene settles.
                EnsureGameSprites();
                return;
            }

            _resolvedType = type;
            ItemsInternal.Clear();
            ByKey.Clear();

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, delegate(FieldInfo a, FieldInfo b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            foreach (FieldInfo field in fields)
            {
                if (Excluded.Contains(field.Name)) continue;
                if (field.FieldType != typeof(bool) && field.FieldType != typeof(int)) continue;
                if (!LooksLikeInventory(field.Name)) continue;

                string key = "pd:" + field.Name;
                string category = CategoryFor(field.Name);
                ItemEntry entry = new ItemEntry
                {
                    Key = key,
                    Name = PrettyName(field.Name),
                    Category = category,
                    Unique = field.FieldType == typeof(bool),
                    Sprite = null,
                    FallbackSprite = UiKit.ItemSprite(key, category),
                    Field = field,
                };
                ItemsInternal.Add(entry);
                ByKey[entry.Key] = entry;
            }

            EnsureGameSprites();
            RosarySharePlugin.LogInfo("Runtime item catalogue: " + ItemsInternal.Count + " PlayerData entries; game sprites matched: " + CountGameSprites() + ".");
        }

        public static int Count(string key)
        {
            ItemEntry entry;
            object pd = GetPlayerData();
            if (pd == null || !TryGet(key, out entry)) return 0;
            try
            {
                if (entry.Unique) return (bool)entry.Field.GetValue(pd) ? 1 : 0;
                return Math.Max(0, (int)entry.Field.GetValue(pd));
            }
            catch { return 0; }
        }

        public static bool TryRemove(string key, int amount)
        {
            if (amount <= 0) return false;
            ItemEntry entry;
            object pd = GetPlayerData();
            if (pd == null || !TryGet(key, out entry)) return false;
            try
            {
                int current = Count(key);
                if (amount > current || (entry.Unique && amount != 1)) return false;
                entry.Field.SetValue(pd, entry.Unique ? (object)false : current - amount);
                return true;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogWarning("Could not remove item " + key + ": " + e.Message);
                return false;
            }
        }

        public static bool TryAdd(string key, int amount)
        {
            if (amount <= 0) return false;
            ItemEntry entry;
            object pd = GetPlayerData();
            if (pd == null || !TryGet(key, out entry)) return false;
            try
            {
                int current = Count(key);
                if (entry.Unique)
                {
                    if (amount != 1 || current != 0) return false;
                    entry.Field.SetValue(pd, true);
                }
                else
                {
                    long next = (long)current + amount;
                    if (next > int.MaxValue) return false;
                    entry.Field.SetValue(pd, (int)next);
                }
                return true;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogWarning("Could not add item " + key + ": " + e.Message);
                return false;
            }
        }

        public static string NameOf(string key)
        {
            ItemEntry entry;
            return TryGet(key, out entry) ? entry.Name : key;
        }

        private static bool TryGet(string key, out ItemEntry entry)
        {
            EnsureCatalog();
            entry = null;
            return !string.IsNullOrEmpty(key) && ByKey.TryGetValue(key, out entry);
        }

        private static object GetPlayerData()
        {
            try { return PlayerData.instance; }
            catch { return null; }
        }

        // ------------------------------------------------------------------
        //  Sprites из самой игры
        // ------------------------------------------------------------------

        private static void EnsureGameSprites()
        {
            try
            {
                float now = Time.unscaledTime;
                if (now < _nextSpriteRefresh) return;
                _nextSpriteRefresh = now + 2f;

                if (!_loadedSpritesScanned)
                {
                    _loadedSpritesScanned = true;
                    ScanLoadedSprites();
                }
                else if (HasMissingSprites())
                {
                    // Addressable assets can appear after the first F7 press.
                    // Scan only while something still needs a real game icon.
                    ScanLoadedSprites();
                }

                int matched = AssignGameSprites();

                // Некоторые версии игры держат иконки в Resources, но ещё не
                // прикрепили их к объектам сцены. Загружаем весь Resources один
                // раз только если среди уже загруженных ассетов ничего не нашлось.
                if (matched == 0 && !_resourcesScanned)
                {
                    _resourcesScanned = true;
                    ScanResourceSprites();
                    AssignGameSprites();
                }
            }
            catch (Exception e)
            {
                // A missing/changed Unity API must never prevent the transfer menu
                // from opening; the generated fallback icon is still available.
                RosarySharePlugin.LogDebug("Game item sprite lookup failed: " + e.Message);
            }
        }

        private static void ScanLoadedSprites()
        {
            try
            {
                UnityEngine.Sprite[] sprites = Resources.FindObjectsOfTypeAll<UnityEngine.Sprite>();
                AddSprites(sprites);
                RosarySharePlugin.LogDebug("Loaded game sprites available for item matching: " + GameSprites.Count + ".");
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("Could not enumerate loaded game sprites: " + e.Message);
            }
        }

        private static void ScanResourceSprites()
        {
            try
            {
                UnityEngine.Sprite[] sprites = Resources.LoadAll<UnityEngine.Sprite>(string.Empty);
                AddSprites(sprites);
                RosarySharePlugin.LogDebug("Resources sprites available for item matching: " + GameSprites.Count + ".");
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("Could not load Resources sprites: " + e.Message);
            }
        }

        private static void AddSprites(UnityEngine.Sprite[] sprites)
        {
            if (sprites == null) return;
            for (int i = 0; i < sprites.Length; i++)
            {
                UnityEngine.Sprite sprite = sprites[i];
                if (sprite == null) continue;

                try
                {
                    int id = sprite.GetInstanceID();
                    if (!GameSpriteIds.Add(id)) continue;
                    string normalized = Normalize(sprite.name);
                    if (normalized.Length == 0) continue;
                    GameSprites.Add(new SpriteCandidate { Sprite = sprite, NormalizedName = normalized });
                }
                catch
                {
                    // A destroyed asset can throw while Unity marshals its name.
                }
            }
        }

        private static bool HasMissingSprites()
        {
            for (int i = 0; i < ItemsInternal.Count; i++)
                if (ItemsInternal[i].Sprite == null) return true;
            return false;
        }

        private static int AssignGameSprites()
        {
            int matched = 0;
            for (int i = 0; i < ItemsInternal.Count; i++)
            {
                ItemEntry entry = ItemsInternal[i];
                if (entry.Sprite != null) { matched++; continue; }

                entry.Sprite = FindGameSprite(entry.Field.Name, entry.Category);
                if (entry.Sprite != null) matched++;
            }
            return matched;
        }

        private static int CountGameSprites()
        {
            int count = 0;
            for (int i = 0; i < ItemsInternal.Count; i++)
                if (ItemsInternal[i].Sprite != null) count++;
            return count;
        }

        private static UnityEngine.Sprite FindGameSprite(string fieldName, string category)
        {
            string core = Normalize(fieldName);
            if (core.Length < 3) return null;

            UnityEngine.Sprite best = null;
            int bestScore = 0;
            for (int i = 0; i < GameSprites.Count; i++)
            {
                SpriteCandidate candidate = GameSprites[i];
                int score = SpriteScore(core, candidate.NormalizedName, category);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate.Sprite;
                }
            }

            // Do not attach a vaguely similar icon to an unrelated save field.
            return bestScore >= 70 ? best : null;
        }

        private static int SpriteScore(string core, string spriteName, string category)
        {
            int score;
            if (spriteName == core) score = 125;
            else if (spriteName.EndsWith(core, StringComparison.Ordinal)) score = 105;
            else if (spriteName.Contains(core)) score = 90;
            else if (core.Contains(spriteName) && spriteName.Length >= 5) score = 72;
            else return 0;

            if (spriteName.Contains("locked") || spriteName.Contains("inactive") ||
                spriteName.Contains("disabled") || spriteName.Contains("grey") || spriteName.Contains("gray"))
                score -= 35;
            if (spriteName.Contains("icon")) score += 4;

            string family = (category ?? string.Empty).ToLowerInvariant();
            if (family.Contains("key") && spriteName.Contains("key")) score += 3;
            if (family.Contains("map") && (spriteName.Contains("map") || spriteName.Contains("journal"))) score += 3;
            if (family.Contains("ability") && (spriteName.Contains("ability") || spriteName.Contains("skill"))) score += 3;
            return score;
        }

        /// <summary>Убирает типичные префиксы PlayerData и разделители имён ассетов.</summary>
        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            StringBuilder b = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(c)) b.Append(c);
            }

            string result = b.ToString();
            bool changed = true;
            while (changed)
            {
                changed = false;
                string[] prefixes = { "has", "owned", "is", "got", "item", "collectible", "upgrade" };
                for (int i = 0; i < prefixes.Length; i++)
                {
                    if (result.StartsWith(prefixes[i], StringComparison.Ordinal) && result.Length > prefixes[i].Length + 2)
                    {
                        result = result.Substring(prefixes[i].Length);
                        changed = true;
                        break;
                    }
                }
            }
            return result;
        }

        private static bool LooksLikeInventory(string name)
        {
            string n = name.ToLowerInvariant();
            string[] tokens = { "has", "owned", "item", "key", "map", "tool", "crest", "ability", "skill", "quest", "relic", "memento", "collect", "fragment", "piece", "locket", "metal", "oil", "journal", "flea", "mask", "spool", "pouch", "kit", "upgrade" };
            for (int i = 0; i < tokens.Length; i++) if (n.Contains(tokens[i])) return true;
            return false;
        }

        private static string CategoryFor(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("key") || n.Contains("quest")) return Texts.T("Ключи и задания", "Keys & quests");
            if (n.Contains("map") || n.Contains("journal")) return Texts.T("Карты и журнал", "Maps & journal");
            if (n.Contains("tool") || n.Contains("crest")) return Texts.T("Инструменты", "Tools");
            if (n.Contains("ability") || n.Contains("skill")) return Texts.T("Способности", "Abilities");
            return Texts.T("Предметы", "Items");
        }

        private static string PrettyName(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            StringBuilder b = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1])) b.Append(' ');
                else if (c == '_') { b.Append(' '); continue; }
                b.Append(c);
            }
            string result = b.ToString().Trim();
            return char.ToUpperInvariant(result[0]) + result.Substring(1);
        }
    }
}

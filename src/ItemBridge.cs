using System;
using System.Collections.Generic;
using System.Reflection;

namespace RosaryShare
{
    /// <summary>
    /// Runtime inventory adapter. It intentionally resolves PlayerData from the live game
    /// instead of compiling against a particular Silksong patch. Transferable entries are
    /// discovered from public integer and boolean save fields; volatile combat/currency
    /// fields are excluded. This also lets newer game items appear without a mod update.
    /// </summary>
    internal static class ItemBridge
    {
        internal sealed class ItemEntry
        {
            public string Key;
            public string Name;
            public string Category;
            public bool Unique;
            internal FieldInfo Field;
        }

        private static readonly List<ItemEntry> ItemsInternal = new List<ItemEntry>();
        private static readonly Dictionary<string, ItemEntry> ByKey = new Dictionary<string, ItemEntry>();
        private static Type _resolvedType;

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
            if (_resolvedType == type && ItemsInternal.Count > 0) return;

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

                ItemEntry entry = new ItemEntry
                {
                    Key = "pd:" + field.Name,
                    Name = PrettyName(field.Name),
                    Category = CategoryFor(field.Name),
                    Unique = field.FieldType == typeof(bool),
                    Field = field,
                };
                ItemsInternal.Add(entry);
                ByKey[entry.Key] = entry;
            }
            RosarySharePlugin.LogInfo("Runtime item catalogue: " + ItemsInternal.Count + " PlayerData entries.");
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
            return !string.IsNullOrEmpty(key) && ByKey.TryGetValue(key, out entry);
        }

        private static object GetPlayerData()
        {
            try { return PlayerData.instance; }
            catch { return null; }
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
            System.Text.StringBuilder b = new System.Text.StringBuilder(value.Length + 8);
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

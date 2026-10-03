using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Runtime inventory adapter. It intentionally resolves PlayerData from the live game
    /// instead of compiling against a particular Silksong patch. This also lets newer game
    /// items appear without a mod update.
    ///
    /// Safety model: transferable entries are only ever picked from an explicit SAFE
    /// allow-list of plain, stackable collectibles (keys, relics, fleas, keepsakes,
    /// crafting materials — the kind of thing you would hand a friend in the base game).
    /// Anything that looks even remotely like a movement/combat ability, a crest or tool
    /// loadout slot, a max-health/max-silk upgrade, a quest/story flag, a map or journal
    /// reveal, or any other engine/save-file bookkeeping field is rejected outright by a
    /// DANGER word list that always wins, even if a field also happens to contain a safe
    /// word. The goal is that a transfer can never desync HeroController's cached ability
    /// state, never let someone skip a scripted unlock, and never leave max health/silk or
    /// quest progress inconsistent — i.e. nothing a player can send or receive here should
    /// ever be able to corrupt a save or soft-lock a game.
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

            /// <summary>Количество на момент последней сборки решётки инвентаря.</summary>
            public int Amount;

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

        // Exact field names that must never be touched, regardless of the word-based
        // filters below. Kept as a first line of defence for the handful of fields we
        // know by name (currency/health/save bookkeeping already handled elsewhere).
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "geo", "ShellShards", "health", "maxHealth", "silk", "silkMax", "isInventoryOpen",
            "respawnMarkerName", "respawnScene", "hazardRespawnLocation", "MPCharge", "MPReserve",
            "profileID", "playTime", "permadeathMode", "atBench", "disablePause",
            "CurrentCrestID", "IsSilkSpoolBroken", "mapAllRooms", "infiniteAirJump",
            "UnlockedExtraBlueSlot", "UnlockedExtraYellowSlot",
        };

        // Word-level block list. A save field is rejected the moment ANY of its camelCase
        // words matches one of these — even if the same field also contains a safe word —
        // because none of these categories behave like a simple, giveable item:
        //  * movement/combat abilities: taking one away mid-run (or mid-air) desyncs
        //    HeroController's cached move-set, and granting one early can let a player
        //    skip the cutscene/tutorial that is supposed to teach it;
        //  * crest/tool equip-loadout bookkeeping: tied to per-slot UI state, not a count;
        //  * max health / max silk upgrades: PlayerData keeps a separate derived cap that
        //    is only recalculated by the game's own pickup code, so poking the raw counter
        //    desyncs the HUD and the actual cap;
        //  * quest/story/world-state flags, map reveals, hunter's-journal and completion
        //    stats: these drive NPC/FSM logic and progression tracking, not inventory.
        private static readonly HashSet<string> DangerWords = new HashSet<string>(StringComparer.Ordinal)
        {
            // Movement & combat abilities / skills.
            "dash", "walljump", "jump", "brolly", "harpoon", "needolin", "needle", "throw",
            "thread", "sphere", "parry", "charge", "bomb", "slash", "bind", "focus", "crawl",
            "sprint", "climb", "swim", "glide", "pogo", "infinite", "cling", "wallcling",
            "soar", "clawline", "silksoar",
            // Crests / tool loadout & equip slots.
            "crest", "equip", "slot", "loadout", "toolequip",
            // Health / silk capacity upgrades (feed a derived cap, not a plain counter).
            "mask", "spool", "silk", "heart", "nail", "maxhealth", "maxsilk", "damage", "regen",
            // Quest / story / world-state / map / journal / stats / debug.
            "quest", "wish", "map", "journal", "hunter", "kill", "defeat", "encounter",
            "discover", "discovered", "visit", "visited", "seen", "met", "complete",
            "completed", "finish", "finished", "percent", "percentage", "stat", "stats",
            "record", "active", "state", "flag", "trigger", "cutscene", "boss", "unlocked",
            "collector", "reward", "given", "stage", "step", "progress", "phase", "broken",
            "rooms", "all", "achievement",
            // Save / system bookkeeping (defence in depth alongside Excluded above).
            "save", "bench", "respawn", "profile", "permadeath", "playtime", "debug", "cheat",
            "test", "inventoryopen", "geo", "shellshards", "id",
        };

        // Word -> localized category. A field is accepted only once it has survived the
        // DANGER check AND one of its words matches here — i.e. this is a strict allow-list,
        // not a generic "looks item-ish" heuristic. Everything else is left untouched.
        // Rebuilt alongside the catalogue (not cached forever) so a language change picked
        // up by Texts.Reload() is reflected the next time the catalogue is rebuilt.
        private static Dictionary<string, string> BuildSafeWordCategory()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            string keys = Texts.T("Ключи", "Keys");
            string relics = Texts.T("Реликвии", "Relics");
            string fleas = Texts.T("Блохи", "Fleas");
            string keepsakes = Texts.T("Памятные вещи", "Keepsakes");
            string materials = Texts.T("Материалы", "Materials");
            string items = Texts.T("Предметы", "Items");

            foreach (string w in new[] { "key", "keys", "simplekey", "misckey" }) map[w] = keys;
            foreach (string w in new[] { "relic", "relics", "scroll", "scrolls", "harp", "harps",
                "effigy", "effigies", "choral", "psalm", "psalms", "cylinder", "cylinders",
                "commandment", "commandments", "egg", "eggs" }) map[w] = relics;
            foreach (string w in new[] { "flea", "fleas" }) map[w] = fleas;
            foreach (string w in new[] { "memento", "mementos", "locket", "lockets" }) map[w] = keepsakes;
            foreach (string w in new[] { "craftmetal", "metal", "metals", "oil", "mossberry",
                "mossberries" }) map[w] = materials;
            foreach (string w in new[] { "trinket", "trinkets" }) map[w] = items;
            return map;
        }

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

            Dictionary<string, string> safeWordCategory = BuildSafeWordCategory();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, delegate(FieldInfo a, FieldInfo b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            foreach (FieldInfo field in fields)
            {
                if (Excluded.Contains(field.Name)) continue;
                if (field.FieldType != typeof(bool) && field.FieldType != typeof(int)) continue;

                string category;
                if (!IsSafeField(field.Name, safeWordCategory, out category)) continue;

                string key = "pd:" + field.Name;
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

        /// <summary>Запись каталога по ключу (или null) — нужна окну обмена для иконки и подписи.</summary>
        public static ItemEntry Find(string key)
        {
            ItemEntry entry;
            return TryGet(key, out entry) ? entry : null;
        }

        /// <summary>
        /// Заполняет <paramref name="target"/> теми безопасными предметами, которые
        /// сейчас реально лежат в инвентаре (количество больше нуля). Решётка окна
        /// показывает именно их: передать можно только то, что есть на руках.
        /// </summary>
        public static void CollectOwned(List<ItemEntry> target)
        {
            if (target == null) return;
            target.Clear();

            EnsureCatalog();
            for (int i = 0; i < ItemsInternal.Count; i++)
            {
                ItemEntry entry = ItemsInternal[i];
                entry.Amount = Count(entry.Key);
                if (entry.Amount > 0) target.Add(entry);
            }
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

        /// <summary>
        /// Strict allow-list check: <paramref name="name"/> is only accepted when none of
        /// its camelCase/underscore words is in <see cref="DangerWords"/> AND at least one
        /// word is a recognised safe collectible, in which case its category is returned.
        /// Anything ambiguous or unrecognised is rejected — under-including is the safe
        /// failure mode here, over-including is not.
        /// </summary>
        private static bool IsSafeField(string name, Dictionary<string, string> safeWordCategory, out string category)
        {
            category = null;
            List<string> words = Tokenize(name);
            if (words.Count == 0) return false;

            for (int i = 0; i < words.Count; i++)
                if (DangerWords.Contains(words[i])) return false;

            for (int i = 0; i < words.Count; i++)
            {
                string found;
                if (safeWordCategory.TryGetValue(words[i], out found))
                {
                    category = found;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Splits a PascalCase/camelCase/underscore field name into lowercase words.</summary>
        private static List<string> Tokenize(string name)
        {
            List<string> words = new List<string>();
            if (string.IsNullOrEmpty(name)) return words;

            StringBuilder current = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_' || c == '-' || c == ' ')
                {
                    FlushWord(words, current);
                    continue;
                }

                bool startsNewWord = false;
                if (current.Length > 0)
                {
                    char prev = name[i - 1];
                    if (char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev)))
                        startsNewWord = true; // fooBar -> foo | Bar
                    else if (char.IsUpper(c) && char.IsUpper(prev) && i + 1 < name.Length && char.IsLower(name[i + 1]))
                        startsNewWord = true; // HTMLBar -> HTML | Bar (acronym boundary)
                    else if (char.IsDigit(c) != char.IsDigit(prev))
                        startsNewWord = true; // foo2 -> foo | 2, 2foo -> 2 | foo
                }

                if (startsNewWord) FlushWord(words, current);
                current.Append(char.ToLowerInvariant(c));
            }
            FlushWord(words, current);
            return words;
        }

        private static void FlushWord(List<string> words, StringBuilder current)
        {
            if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Length = 0;
            }
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

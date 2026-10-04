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
    /// Item icons are resolved from:
    ///   1. Live in-game UI / Inventory components (exact 1:1 sprite bindings),
    ///   2. Canonical Silksong & Hollow Knight item sprite name mappings,
    ///   3. Multi-layer intelligent token matching with strict noise filtering,
    ///   4. High quality procedural fallbacks if an asset is not yet loaded into memory.
    /// </summary>
    internal static class ItemBridge
    {
        public enum CanonicalCategory
        {
            Key,
            Relic,
            Flea,
            Egg,
            Keepsake,
            Material,
            Scroll,
            Item,
        }

        internal sealed class ItemEntry
        {
            public string Key;
            public string RawFieldName;
            public string Name;
            public string Category;
            public CanonicalCategory CategoryKind;
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
            public string RawName;
            public string NormalizedName;
            public List<string> Tokens;
            public int Width;
            public int Height;
        }

        private static readonly List<ItemEntry> ItemsInternal = new List<ItemEntry>();
        private static readonly Dictionary<string, ItemEntry> ByKey = new Dictionary<string, ItemEntry>();
        private static readonly List<SpriteCandidate> GameSprites = new List<SpriteCandidate>();
        private static readonly HashSet<int> GameSpriteIds = new HashSet<int>();
        private static Type _resolvedType;
        private static Texts.Lang _resolvedLang;
        private static bool _loadedSpritesScanned;
        private static bool _resourcesScanned;
        private static float _nextSpriteRefresh;

        // Exact field names that must never be touched
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "geo", "ShellShards", "health", "maxHealth", "silk", "silkMax", "isInventoryOpen",
            "respawnMarkerName", "respawnScene", "hazardRespawnLocation", "MPCharge", "MPReserve",
            "profileID", "playTime", "permadeathMode", "atBench", "disablePause",
            "CurrentCrestID", "IsSilkSpoolBroken", "mapAllRooms", "infiniteAirJump",
            "UnlockedExtraBlueSlot", "UnlockedExtraYellowSlot",
        };

        // Word-level block list for dangerous non-transferable engine/gameplay fields
        private static readonly HashSet<string> DangerWords = new HashSet<string>(StringComparer.Ordinal)
        {
            // Movement & combat abilities / skills
            "dash", "walljump", "jump", "brolly", "harpoon", "needolin", "needle", "throw",
            "thread", "sphere", "parry", "charge", "bomb", "slash", "bind", "focus", "crawl",
            "sprint", "climb", "swim", "glide", "pogo", "infinite", "cling", "wallcling",
            "soar", "clawline", "silksoar",
            // Crests / tool loadout & equip slots
            "crest", "equip", "slot", "loadout", "toolequip",
            // Health / silk capacity upgrades
            "mask", "spool", "silk", "heart", "nail", "maxhealth", "maxsilk", "damage", "regen",
            // Quest / story / world-state / map / journal / stats / debug
            "quest", "wish", "map", "journal", "hunter", "kill", "defeat", "encounter",
            "discover", "discovered", "visit", "visited", "seen", "met", "complete",
            "completed", "finish", "finished", "percent", "percentage", "stat", "stats",
            "record", "active", "state", "flag", "trigger", "cutscene", "boss", "unlocked",
            "collector", "reward", "given", "stage", "step", "progress", "phase", "broken",
            "rooms", "all", "achievement",
            // Save / system bookkeeping
            "save", "bench", "respawn", "profile", "permadeath", "playtime", "debug", "cheat",
            "test", "inventoryopen", "geo", "shellshards", "id",
        };

        // Negative words for sprite candidate filtering (noise like particles, UI frames, animations, etc.)
        private static readonly HashSet<string> NoiseSpriteTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "particle", "particles", "fx", "anim", "animation", "sheet", "atlas", "debris",
            "dust", "smoke", "explosion", "spark", "sparks", "glow", "shadow", "mask", "alpha",
            "normal", "specular", "diffuse", "light", "fader", "bg", "background", "backdrop",
            "screen", "blur", "border", "box", "panel", "window", "bar", "fill", "slider",
            "knob", "arrow", "pointer", "cursor", "button", "btn", "press", "pressed", "hover",
            "disabled", "inactive", "locked", "enemy", "boss", "hazard", "spike", "bullet",
            "slash", "hit", "damage", "death", "die", "corpse", "npc", "cutscene", "cinematic",
            "font", "glyph", "text", "tutorial", "prompt", "pad", "stick", "keyboard", "tile",
            "tileset", "wall", "floor", "ground", "platform", "frame_corner", "corner"
        };

        // Known canonical sprite name patterns for Silksong & Hollow Knight collectibles
        private static readonly Dictionary<string, string[]> KnownItemSpriteAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "simpleKeys", new[] { "inv_item_simple_key", "item_key_simple", "key_simple", "inv_key_simple", "simple_key_icon", "simplekey_icon", "simple_key", "simplekey" } },
            { "simpleKey", new[] { "inv_item_simple_key", "item_key_simple", "key_simple", "inv_key_simple", "simple_key_icon", "simplekey_icon", "simple_key", "simplekey" } },
            { "hasSimpleKey", new[] { "inv_item_simple_key", "item_key_simple", "key_simple", "inv_key_simple", "simple_key_icon", "simplekey_icon", "simple_key", "simplekey" } },
            { "keyCity", new[] { "inv_item_city_key", "item_key_city", "key_city", "inv_key_city", "city_key_icon", "city_key", "citykey" } },
            { "hasCityKey", new[] { "inv_item_city_key", "item_key_city", "key_city", "inv_key_city", "city_key_icon", "city_key", "citykey" } },
            { "keyRuins", new[] { "inv_item_ruins_key", "item_key_ruins", "key_ruins", "inv_key_ruins", "ruins_key_icon", "ruins_key", "ruinskey" } },
            { "hasRuinsKey", new[] { "inv_item_ruins_key", "item_key_ruins", "key_ruins", "inv_key_ruins", "ruins_key_icon", "ruins_key", "ruinskey" } },
            { "keyTower", new[] { "inv_item_tower_key", "item_key_tower", "key_tower", "inv_key_tower", "tower_key_icon", "tower_key", "towerkey" } },
            { "hasTowerKey", new[] { "inv_item_tower_key", "item_key_tower", "key_tower", "inv_key_tower", "tower_key_icon", "tower_key", "towerkey" } },
            { "keySlyKey", new[] { "inv_item_storeroom_key", "item_key_storeroom", "key_storeroom", "sly_key", "storeroom_key" } },
            { "hasSlyKey", new[] { "inv_item_storeroom_key", "item_key_storeroom", "key_storeroom", "sly_key", "storeroom_key" } },
            { "keyLove", new[] { "inv_item_love_key", "item_key_love", "key_love", "love_key" } },
            { "hasLoveKey", new[] { "inv_item_love_key", "item_key_love", "key_love", "love_key" } },
            { "keyMine", new[] { "inv_item_mine_key", "item_key_mine", "key_mine", "mine_key" } },
            { "hasMineKey", new[] { "inv_item_mine_key", "item_key_mine", "key_mine", "mine_key" } },
            { "keyElegant", new[] { "inv_item_elegant_key", "key_elegant", "elegant_key" } },
            { "hasElegantKey", new[] { "inv_item_elegant_key", "key_elegant", "elegant_key" } },
            { "rancidEggs", new[] { "inv_item_egg", "item_rancid_egg", "rancid_egg", "egg_rancid", "inv_egg", "egg_icon", "egg" } },
            { "rancidEgg", new[] { "inv_item_egg", "item_rancid_egg", "rancid_egg", "egg_rancid", "inv_egg", "egg_icon", "egg" } },
            { "hasRancidEgg", new[] { "inv_item_egg", "item_rancid_egg", "rancid_egg", "egg_rancid", "inv_egg", "egg_icon", "egg" } },
            { "fleaCount", new[] { "inv_item_flea", "item_flea", "inv_flea", "flea_icon", "flea_saved", "saved_flea", "flea" } },
            { "savedFleas", new[] { "inv_item_flea", "item_flea", "inv_flea", "flea_icon", "flea_saved", "saved_flea", "flea" } },
            { "fleas", new[] { "inv_item_flea", "item_flea", "inv_flea", "flea_icon", "flea_saved", "saved_flea", "flea" } },
            { "craftMetal", new[] { "inv_item_craft_metal", "item_craft_metal", "craft_metal", "craftmetal", "metal_craft", "crafting_metal" } },
            { "metals", new[] { "inv_item_craft_metal", "item_craft_metal", "craft_metal", "craftmetal", "metal_craft", "crafting_metal" } },
            { "craftMetalCount", new[] { "inv_item_craft_metal", "item_craft_metal", "craft_metal", "craftmetal", "metal_craft", "crafting_metal" } },
            { "mossBerries", new[] { "inv_item_moss_berry", "item_moss_berry", "moss_berry", "mossberry", "mossberries", "moss_berries" } },
            { "mossBerry", new[] { "inv_item_moss_berry", "item_moss_berry", "moss_berry", "mossberry", "mossberries", "moss_berries" } },
            { "choralPsalm", new[] { "inv_item_choral_psalm", "item_psalm", "choral_psalm", "psalm", "choral" } },
            { "rosaryStrings", new[] { "inv_item_rosary_string", "rosary_string", "item_string", "rosary_strand", "string" } },
            { "rosaryString", new[] { "inv_item_rosary_string", "rosary_string", "item_string", "rosary_strand", "string" } },
            { "trinket1", new[] { "inv_item_trinket1", "inv_trinket1", "trinket1", "trinket_1", "relic1" } },
            { "trinket2", new[] { "inv_item_trinket2", "inv_trinket2", "trinket2", "trinket_2", "relic2" } },
            { "trinket3", new[] { "inv_item_trinket3", "inv_trinket3", "trinket3", "trinket_3", "relic3" } },
            { "trinket4", new[] { "inv_item_trinket4", "inv_trinket4", "trinket4", "trinket_4", "relic4" } },
            { "relicCount", new[] { "inv_item_relic", "inv_relic", "relic_icon", "relic", "seal", "journal" } },
        };

        private static Dictionary<string, CanonicalCategory> BuildSafeWordCategory()
        {
            Dictionary<string, CanonicalCategory> map = new Dictionary<string, CanonicalCategory>(StringComparer.Ordinal);

            foreach (string w in new[] { "key", "keys", "simplekey", "misckey", "storeroomkey", "citykey", "ruinskey", "towerkey", "minekey", "lovekey", "elegantkey", "shopkey" })
                map[w] = CanonicalCategory.Key;

            foreach (string w in new[] { "relic", "relics", "scroll", "scrolls", "harp", "harps", "effigy", "effigies", "choral", "psalm", "psalms", "cylinder", "cylinders", "commandment", "commandments", "trinket", "trinkets", "seal", "journal", "memorabilia" })
                map[w] = CanonicalCategory.Relic;

            foreach (string w in new[] { "flea", "fleas", "savedflea", "savedfleas" })
                map[w] = CanonicalCategory.Flea;

            foreach (string w in new[] { "egg", "eggs", "rancidegg", "rancideggs" })
                map[w] = CanonicalCategory.Egg;

            foreach (string w in new[] { "memento", "mementos", "locket", "lockets", "keepsake", "keepsakes", "gift", "token", "ribbon" })
                map[w] = CanonicalCategory.Keepsake;

            foreach (string w in new[] { "craftmetal", "metal", "metals", "oil", "oils", "mossberry", "mossberries", "berry", "berries", "ore", "paleore", "silkthread", "rosarystring", "rosarystrings", "thread", "shard", "shards" })
                map[w] = CanonicalCategory.Material;

            return map;
        }

        private static string LocalizedCategory(CanonicalCategory cat)
        {
            switch (cat)
            {
                case CanonicalCategory.Key: return Texts.T("Ключи", "Keys");
                case CanonicalCategory.Relic: return Texts.T("Реликвии", "Relics");
                case CanonicalCategory.Flea: return Texts.T("Блохи", "Fleas");
                case CanonicalCategory.Egg: return Texts.T("Яйца", "Eggs");
                case CanonicalCategory.Keepsake: return Texts.T("Памятные вещи", "Keepsakes");
                case CanonicalCategory.Material: return Texts.T("Материалы", "Materials");
                case CanonicalCategory.Scroll: return Texts.T("Свитки", "Scrolls");
                default: return Texts.T("Предметы", "Items");
            }
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
            if (_resolvedType == type && _resolvedLang == Texts.Current && ItemsInternal.Count > 0)
            {
                EnsureGameSprites();
                return;
            }

            _resolvedType = type;
            _resolvedLang = Texts.Current;
            ItemsInternal.Clear();
            ByKey.Clear();

            Dictionary<string, CanonicalCategory> safeWordCategory = BuildSafeWordCategory();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, delegate(FieldInfo a, FieldInfo b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            foreach (FieldInfo field in fields)
            {
                if (Excluded.Contains(field.Name)) continue;
                if (field.FieldType != typeof(bool) && field.FieldType != typeof(int)) continue;

                CanonicalCategory cat;
                if (!IsSafeField(field.Name, safeWordCategory, out cat)) continue;

                string key = "pd:" + field.Name;
                string catName = LocalizedCategory(cat);
                ItemEntry entry = new ItemEntry
                {
                    Key = key,
                    RawFieldName = field.Name,
                    Name = PrettyName(field.Name),
                    Category = catName,
                    CategoryKind = cat,
                    Unique = field.FieldType == typeof(bool),
                    Sprite = null,
                    FallbackSprite = UiKit.ItemSprite(key, cat.ToString()),
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

        public static ItemEntry Find(string key)
        {
            ItemEntry entry;
            return TryGet(key, out entry) ? entry : null;
        }

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
        //  Поиск и сопоставление спрайтов из самой игры
        // ------------------------------------------------------------------

        private static void EnsureGameSprites()
        {
            try
            {
                float now = Time.unscaledTime;
                if (now < _nextSpriteRefresh) return;
                _nextSpriteRefresh = now + 1.5f;

                if (!_loadedSpritesScanned)
                {
                    _loadedSpritesScanned = true;
                    ScanLoadedSprites();
                }
                else if (HasMissingSprites())
                {
                    ScanLoadedSprites();
                }

                int matched = AssignGameSprites();

                if (matched < ItemsInternal.Count && !_resourcesScanned)
                {
                    _resourcesScanned = true;
                    ScanResourceSprites();
                    AssignGameSprites();
                }
            }
            catch (Exception e)
            {
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

                    string rawName = sprite.name;
                    if (string.IsNullOrEmpty(rawName)) continue;

                    string normalized = Normalize(rawName);
                    List<string> tokens = Tokenize(rawName);

                    int w = 0, h = 0;
                    try
                    {
                        w = Mathf.RoundToInt(sprite.rect.width);
                        h = Mathf.RoundToInt(sprite.rect.height);
                    }
                    catch { }

                    GameSprites.Add(new SpriteCandidate
                    {
                        Sprite = sprite,
                        RawName = rawName,
                        NormalizedName = normalized,
                        Tokens = tokens,
                        Width = w,
                        Height = h
                    });
                }
                catch
                {
                    // destroyed or unloaded asset
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

                // 1. Попытка прямой привязки из компонентов инвентаря игры
                UnityEngine.Sprite direct = TryResolveFromSceneUI(entry.RawFieldName);
                if (direct != null)
                {
                    entry.Sprite = direct;
                    matched++;
                    continue;
                }

                // 2. Попытка поиска по известным алиасам и шаблонам имён
                UnityEngine.Sprite fromAlias = FindKnownAliasSprite(entry.RawFieldName);
                if (fromAlias != null)
                {
                    entry.Sprite = fromAlias;
                    matched++;
                    continue;
                }

                // 3. Интеллектуальный поиск по токенам и исключению шума
                UnityEngine.Sprite heuristic = FindHeuristicGameSprite(entry.RawFieldName, entry.CategoryKind);
                if (heuristic != null)
                {
                    entry.Sprite = heuristic;
                    matched++;
                }
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

        /// <summary>
        /// Поиск прямого спрайта из компонентов UI инвентаря игры (если сцена загружена).
        /// </summary>
        private static UnityEngine.Sprite TryResolveFromSceneUI(string fieldName)
        {
            try
            {
                // Ищем любые активные компоненты UI инвентаря с полями привязки к PlayerData
                MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour mb = behaviours[i];
                    if (!mb) continue;
                    Type t = mb.GetType();
                    string typeName = t.Name;

                    if (!typeName.Contains("Inventory") && !typeName.Contains("Item") &&
                        !typeName.Contains("Collectable") && !typeName.Contains("Pane") &&
                        !typeName.Contains("Display") && !typeName.Contains("Satchel"))
                        continue;

                    FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    bool matchesField = false;
                    for (int f = 0; f < fields.Length; f++)
                    {
                        FieldInfo fi = fields[f];
                        if (fi.FieldType == typeof(string))
                        {
                            string val = fi.GetValue(mb) as string;
                            if (string.Equals(val, fieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                matchesField = true;
                                break;
                            }
                        }
                    }

                    if (matchesField)
                    {
                        for (int f = 0; f < fields.Length; f++)
                        {
                            FieldInfo fi = fields[f];
                            if (fi.FieldType == typeof(UnityEngine.Sprite))
                            {
                                UnityEngine.Sprite sp = fi.GetValue(mb) as UnityEngine.Sprite;
                                if (sp != null) return sp;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Поиск по известным таблицам точных соответствий имён спрайтов.
        /// </summary>
        private static UnityEngine.Sprite FindKnownAliasSprite(string fieldName)
        {
            string[] aliases;
            if (!KnownItemSpriteAliases.TryGetValue(fieldName, out aliases) || aliases == null)
                return null;

            for (int a = 0; a < aliases.Length; a++)
            {
                string target = Normalize(aliases[a]);
                for (int i = 0; i < GameSprites.Count; i++)
                {
                    SpriteCandidate candidate = GameSprites[i];
                    if (candidate.NormalizedName == target)
                        return candidate.Sprite;
                }
            }
            return null;
        }

        /// <summary>
        /// Интеллектуальный ранжирующий матчер спрайтов.
        /// Фильтрует шум (частицы, кнопки, анимации, фоны), требует совпадения
        /// всех ключевых токенов предмета и награждает инвентарные маркеры.
        /// </summary>
        private static UnityEngine.Sprite FindHeuristicGameSprite(string fieldName, CanonicalCategory category)
        {
            List<string> coreTokens = CleanItemTokens(Tokenize(fieldName));
            if (coreTokens.Count == 0) return null;

            UnityEngine.Sprite best = null;
            int bestScore = 0;

            for (int i = 0; i < GameSprites.Count; i++)
            {
                SpriteCandidate candidate = GameSprites[i];
                int score = ScoreSpriteCandidate(coreTokens, candidate, category);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate.Sprite;
                }
            }

            // Порог уверенности для принятия спрайта
            return bestScore >= 160 ? best : null;
        }

        private static int ScoreSpriteCandidate(List<string> coreTokens, SpriteCandidate candidate, CanonicalCategory category)
        {
            // 1. Жёсткая отсечка шума (частицы, эффекты, анимации, шрифты, кнопки, фоны)
            for (int t = 0; t < candidate.Tokens.Count; t++)
            {
                string token = candidate.Tokens[t];
                if (NoiseSpriteTokens.Contains(token))
                    return -1000;
            }

            // 2. Проверка габаритов: иконка инвентаря не может быть 1024x1024 или 2x2 пикселя
            if (candidate.Width > 0 && candidate.Height > 0)
            {
                if (candidate.Width > 512 || candidate.Height > 512) return -1000; // фоны и атласы
                if (candidate.Width < 8 || candidate.Height < 8) return -1000;       // мелкие точки
                float aspect = (float)candidate.Width / candidate.Height;
                if (aspect > 4.5f || aspect < 0.22f) return -500;                    // длинные разделители
            }

            // 3. Проверка совпадения ключевых токенов предмета
            int matchedTokens = 0;
            for (int c = 0; c < coreTokens.Count; c++)
            {
                string wanted = coreTokens[c];
                string wantedStem = Stem(wanted);

                bool found = false;
                for (int t = 0; t < candidate.Tokens.Count; t++)
                {
                    string candToken = candidate.Tokens[t];
                    string candStem = Stem(candToken);

                    if (candStem == wantedStem || candToken.Contains(wanted) || wanted.Contains(candToken))
                    {
                        found = true;
                        break;
                    }
                }
                if (found) matchedTokens++;
            }

            // Для составных предметов (например, simple + key или moss + berry) ВСЕ ключевые токены обязаны совпасть
            if (matchedTokens < coreTokens.Count)
                return 0;

            int score = 200 + (matchedTokens * 50);

            // 4. Бонусы за инвентарные префиксы/суффиксы
            for (int t = 0; t < candidate.Tokens.Count; t++)
            {
                string tok = candidate.Tokens[t];
                if (tok == "inv" || tok == "inventory" || tok == "item" || tok == "icon" || tok == "collectible" || tok == "satchel" || tok == "pickup")
                    score += 60;
            }

            // 5. Штраф за лишние посторонние слова
            int extraTokens = candidate.Tokens.Count - matchedTokens;
            if (extraTokens > 0)
                score -= Mathf.Min(60, extraTokens * 15);

            return score;
        }

        private static List<string> CleanItemTokens(List<string> tokens)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < tokens.Count; i++)
            {
                string t = tokens[i].ToLowerInvariant();
                if (t == "has" || t == "is" || t == "got" || t == "owned" || t == "item" ||
                    t == "collectible" || t == "count" || t == "amount" || t == "unlocked")
                    continue;
                if (t.Length >= 2)
                    result.Add(t);
            }
            return result;
        }

        private static string Stem(string word)
        {
            if (string.IsNullOrEmpty(word)) return string.Empty;
            string w = word.ToLowerInvariant();
            if (w.EndsWith("ies") && w.Length > 4) return w.Substring(0, w.Length - 3) + "y";
            if (w.EndsWith("es") && w.Length > 3) return w.Substring(0, w.Length - 2);
            if (w.EndsWith("s") && w.Length > 2) return w.Substring(0, w.Length - 1);
            return w;
        }

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

        private static bool IsSafeField(string name, Dictionary<string, CanonicalCategory> safeWordCategory, out CanonicalCategory category)
        {
            category = CanonicalCategory.Item;
            List<string> words = Tokenize(name);
            if (words.Count == 0) return false;

            for (int i = 0; i < words.Count; i++)
                if (DangerWords.Contains(words[i])) return false;

            for (int i = 0; i < words.Count; i++)
            {
                CanonicalCategory found;
                if (safeWordCategory.TryGetValue(words[i], out found))
                {
                    category = found;
                    return true;
                }
            }
            return false;
        }

        private static List<string> Tokenize(string name)
        {
            List<string> words = new List<string>();
            if (string.IsNullOrEmpty(name)) return words;

            StringBuilder current = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_' || c == '-' || c == ' ' || c == '.' || c == '/')
                {
                    FlushWord(words, current);
                    continue;
                }

                bool startsNewWord = false;
                if (current.Length > 0)
                {
                    char prev = name[i - 1];
                    if (char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev)))
                        startsNewWord = true;
                    else if (char.IsUpper(c) && char.IsUpper(prev) && i + 1 < name.Length && char.IsLower(name[i + 1]))
                        startsNewWord = true;
                    else if (char.IsDigit(c) != char.IsDigit(prev))
                        startsNewWord = true;
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

        private static string PrettyName(string rawFieldName)
        {
            if (string.IsNullOrEmpty(rawFieldName)) return rawFieldName;

            bool ru = Texts.Current == Texts.Lang.Ru;

            // Известные красивые названия предметов
            if (string.Equals(rawFieldName, "simpleKeys", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "simpleKey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasSimpleKey", StringComparison.OrdinalIgnoreCase))
                return ru ? "Простые ключи" : "Simple Keys";

            if (string.Equals(rawFieldName, "keyCity", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasCityKey", StringComparison.OrdinalIgnoreCase))
                return ru ? "Ключ от Города" : "City Key";

            if (string.Equals(rawFieldName, "keyRuins", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasRuinsKey", StringComparison.OrdinalIgnoreCase))
                return ru ? "Ключ от Руин" : "Ruins Key";

            if (string.Equals(rawFieldName, "keyTower", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasTowerKey", StringComparison.OrdinalIgnoreCase))
                return ru ? "Ключ от Башни" : "Tower Key";

            if (string.Equals(rawFieldName, "keySlyKey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasSlyKey", StringComparison.OrdinalIgnoreCase))
                return ru ? "Ключ Слая" : "Sly's Key";

            if (string.Equals(rawFieldName, "keyLove", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasLoveKey", StringComparison.OrdinalIgnoreCase))
                return ru ? "Ключ Любви" : "Love Key";

            if (string.Equals(rawFieldName, "keyMine", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasMineKey", StringComparison.OrdinalIgnoreCase))
                return ru ? "Ключ от Шахты" : "Mine Key";

            if (string.Equals(rawFieldName, "keyElegant", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasElegantKey", StringComparison.OrdinalIgnoreCase))
                return ru ? "Изящный ключ" : "Elegant Key";

            if (string.Equals(rawFieldName, "rancidEggs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "rancidEgg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "hasRancidEgg", StringComparison.OrdinalIgnoreCase))
                return ru ? "Тухлые яйца" : "Rancid Eggs";

            if (string.Equals(rawFieldName, "fleaCount", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "savedFleas", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "fleas", StringComparison.OrdinalIgnoreCase))
                return ru ? "Спасённые блохи" : "Saved Fleas";

            if (string.Equals(rawFieldName, "craftMetal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "metals", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "craftMetalCount", StringComparison.OrdinalIgnoreCase))
                return ru ? "Металл для ковки" : "Craft Metal";

            if (string.Equals(rawFieldName, "mossBerries", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "mossBerry", StringComparison.OrdinalIgnoreCase))
                return ru ? "Моховые ягоды" : "Mossberries";

            if (string.Equals(rawFieldName, "choralPsalm", StringComparison.OrdinalIgnoreCase))
                return ru ? "Хоральный псалом" : "Choral Psalm";

            if (string.Equals(rawFieldName, "rosaryStrings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawFieldName, "rosaryString", StringComparison.OrdinalIgnoreCase))
                return ru ? "Нити для бусин" : "Rosary Strings";

            List<string> tokens = CleanItemTokens(Tokenize(rawFieldName));
            if (tokens.Count == 0) return rawFieldName;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                string t = tokens[i];
                if (t.Length == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(t[0]));
                if (t.Length > 1) sb.Append(t.Substring(1));
            }
            return sb.ToString();
        }
    }
}

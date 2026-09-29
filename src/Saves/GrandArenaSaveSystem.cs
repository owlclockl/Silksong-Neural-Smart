using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using SilksongNeuralSmart.Core;
using SilksongNeuralSmart.Training;

namespace SilksongNeuralSmart.Saves
{
    /// <summary>
    /// Краткая информация о слоте сохранения для меню выбора.
    /// </summary>
    public class GrandArenaSlotInfo
    {
        public int Slot;
        public bool Exists;
        public string Name = "";
        public int Wave;
        public int Generation;
        public int TotalEpisodes;
        public int HornetWins;
        public int PackWins;
        public int MobCount;
        public float PlayTimeSeconds;
        public string UpdatedUtc = "";

        public string PlayTimeFormatted
        {
            get
            {
                int total = Mathf.Max(0, Mathf.RoundToInt(PlayTimeSeconds));
                int h = total / 3600;
                int m = (total % 3600) / 60;
                int s = total % 60;
                return h > 0 ? $"{h}ч {m:00}м" : $"{m}м {s:00}с";
            }
        }
    }

    /// <summary>
    /// СОБСТВЕННАЯ СИСТЕМА СОХРАНЕНИЙ РЕЖИМА "ВЕЛИКАЯ АРЕНА".
    ///
    /// Полностью изолирована от сохранений основной игры и от классического додзё:
    /// свои слоты, свои веса нейросетей, свой прогресс волн и статистика.
    ///
    /// Структура на диске:
    ///   BepInEx/config/SilksongNeuralSmart/GrandArena/slot_1/profile.json
    ///                                               /slot_1/hornet.brain.json
    ///                                               /slot_1/mob_grunt.brain.json
    ///                                               /slot_1/mob_flying.brain.json
    ///                                               /slot_1/mob_knight.brain.json
    ///                                               /slot_1/mob_assassin.brain.json
    /// </summary>
    public static class GrandArenaSaveSystem
    {
        public const int SLOT_COUNT = 3;
        public const int SAVE_FORMAT_VERSION = 1;

        private static string? _rootCache;

        /// <summary>Корневая папка сохранений режима.</summary>
        public static string RootDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_rootCache))
                    return _rootCache!;

                _rootCache = Path.Combine(ResolveBaseDirectory(), Path.Combine("SilksongNeuralSmart", "GrandArena"));

                try
                {
                    if (!Directory.Exists(_rootCache))
                        Directory.CreateDirectory(_rootCache);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[SilksongNeuralSmart] Не удалось создать папку сохранений: " + ex.Message);
                }

                return _rootCache!;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ResolveBaseDirectory()
        {
            // BepInEx/config — стандартное место для данных плагина
            try
            {
                string cfg = BepInEx.Paths.ConfigPath;
                if (!string.IsNullOrEmpty(cfg))
                    return cfg;
            }
            catch
            {
                // BepInEx может быть недоступен (например, при автономных тестах)
            }

            return Application.persistentDataPath;
        }

        public static string SlotDirectory(int slot)
        {
            return Path.Combine(RootDirectory, "slot_" + Mathf.Clamp(slot, 1, SLOT_COUNT));
        }

        public static string ProfilePath(int slot)
        {
            return Path.Combine(SlotDirectory(slot), "profile.json");
        }

        public static bool SlotExists(int slot)
        {
            try { return File.Exists(ProfilePath(slot)); }
            catch { return false; }
        }

        // ==================================================================
        // Чтение информации о слотах (для меню)
        // ==================================================================
        public static List<GrandArenaSlotInfo> GetAllSlots()
        {
            var list = new List<GrandArenaSlotInfo>(SLOT_COUNT);
            for (int i = 1; i <= SLOT_COUNT; i++)
                list.Add(GetSlotInfo(i));
            return list;
        }

        public static GrandArenaSlotInfo GetSlotInfo(int slot)
        {
            var info = new GrandArenaSlotInfo { Slot = slot, Name = "Пустой слот" };

            try
            {
                string path = ProfilePath(slot);
                if (!File.Exists(path))
                    return info;

                string json = File.ReadAllText(path, Encoding.UTF8);
                info.Exists = true;
                info.Name = ReadString(json, "name", "Слот " + slot);
                info.Wave = ReadInt(json, "wave", 1);
                info.Generation = ReadInt(json, "hornetGeneration", 1);
                info.TotalEpisodes = ReadInt(json, "totalEpisodes", 0);
                info.HornetWins = ReadInt(json, "hornetWins", 0);
                info.PackWins = ReadInt(json, "packWins", 0);
                info.MobCount = ReadInt(json, "mobCount", 4);
                info.PlayTimeSeconds = ReadFloat(json, "playTimeSeconds", 0f);
                info.UpdatedUtc = ReadString(json, "updatedUtc", "");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SilksongNeuralSmart] Не удалось прочитать слот {slot}: {ex.Message}");
            }

            return info;
        }

        // ==================================================================
        // Сохранение
        // ==================================================================
        public static bool Save(int slot, GrandArenaMode mode)
        {
            if (mode == null) return false;

            try
            {
                string dir = SlotDirectory(slot);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // 1. Мозг Хорнет
                NeuralNetwork hornetBrain = mode.HornetTrainer.BestGenomeEver?.Brain
                                            ?? mode.HornetTrainer.GetCurrentGenome().Brain;
                File.WriteAllText(Path.Combine(dir, "hornet.brain.json"), hornetBrain.ToJson(), Encoding.UTF8);

                // 2. Мозги всех архетипов мобов
                foreach (string key in GrandArenaMode.ArchetypeKeys)
                {
                    if (!mode.MobTrainers.TryGetValue(key, out GeneticTrainer trainer))
                        continue;

                    NeuralNetwork brain = trainer.BestGenomeEver?.Brain ?? trainer.GetCurrentGenome().Brain;
                    File.WriteAllText(Path.Combine(dir, $"mob_{key}.brain.json"), brain.ToJson(), Encoding.UTF8);
                }

                // 3. Профиль режима
                File.WriteAllText(ProfilePath(slot), BuildProfileJson(slot, mode), Encoding.UTF8);

                Debug.Log($"[SilksongNeuralSmart] Прогресс режима 'Великая Арена' сохранён в слот {slot} ({dir})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SilksongNeuralSmart] Ошибка сохранения слота {slot}: {ex.Message}");
                return false;
            }
        }

        private static string BuildProfileJson(int slot, GrandArenaMode mode)
        {
            var sb = new StringBuilder();
            string created = SlotExists(slot)
                ? ReadString(SafeRead(ProfilePath(slot)), "createdUtc", DateTime.UtcNow.ToString("O"))
                : DateTime.UtcNow.ToString("O");

            sb.Append("{\n");
            sb.Append($"  \"mode\": \"GrandArena\",\n");
            sb.Append($"  \"formatVersion\": {SAVE_FORMAT_VERSION},\n");
            sb.Append($"  \"slot\": {slot},\n");
            sb.Append($"  \"name\": \"{Escape(mode.ProfileName)}\",\n");
            sb.Append($"  \"createdUtc\": \"{Escape(created)}\",\n");
            sb.Append($"  \"updatedUtc\": \"{DateTime.UtcNow:O}\",\n");
            sb.Append($"  \"playTimeSeconds\": {F(mode.PlayTimeSeconds)},\n");

            sb.Append($"  \"wave\": {mode.Wave},\n");
            sb.Append($"  \"bestWave\": {mode.BestWave},\n");
            sb.Append($"  \"mobCount\": {mode.MobCount},\n");
            sb.Append($"  \"baseMobCount\": {mode.BaseMobCount},\n");
            sb.Append($"  \"totalEpisodes\": {mode.TotalEpisodes},\n");
            sb.Append($"  \"hornetWins\": {mode.HornetWins},\n");
            sb.Append($"  \"packWins\": {mode.PackWins},\n");
            sb.Append($"  \"draws\": {mode.Draws},\n");
            sb.Append($"  \"mobsDefeated\": {mode.TotalMobsDefeated},\n");

            sb.Append($"  \"timeScale\": {F(mode.TimeScale)},\n");
            sb.Append($"  \"maxEpisodeDuration\": {F(mode.MaxEpisodeDuration)},\n");
            sb.Append($"  \"waveScaling\": {(mode.WaveScaling ? "true" : "false")},\n");
            sb.Append($"  \"balancePackDamage\": {(mode.BalancePackDamage ? "true" : "false")},\n");
            sb.Append($"  \"playerControlsHornet\": {(mode.PlayerControlsHornet ? "true" : "false")},\n");
            sb.Append($"  \"autoSaveEveryEpisodes\": {mode.AutoSaveEveryEpisodes},\n");
            sb.Append($"  \"reactionDelayMs\": {F(mode.ReactionDelayMs)},\n");
            sb.Append($"  \"errorMarginRate\": {F(mode.ErrorMarginRate)},\n");

            sb.Append($"  \"hornetGeneration\": {mode.HornetTrainer.Generation},\n");
            sb.Append($"  \"hornetBestFitness\": {F(SafeFitness(mode.HornetTrainer.BestFitnessEver))},\n");

            foreach (string key in GrandArenaMode.ArchetypeKeys)
            {
                if (!mode.MobTrainers.TryGetValue(key, out GeneticTrainer trainer))
                    continue;

                sb.Append($"  \"gen_{key}\": {trainer.Generation},\n");
                sb.Append($"  \"fit_{key}\": {F(SafeFitness(trainer.BestFitnessEver))},\n");
            }

            sb.Append($"  \"hornetFitnessHistory\": {FloatArray(mode.HornetFitnessLog)},\n");
            sb.Append($"  \"packFitnessHistory\": {FloatArray(mode.PackFitnessLog)}\n");
            sb.Append("}\n");

            return sb.ToString();
        }

        // ==================================================================
        // Загрузка
        // ==================================================================
        public static bool Load(int slot, GrandArenaMode mode)
        {
            if (mode == null) return false;

            try
            {
                string dir = SlotDirectory(slot);
                string profilePath = ProfilePath(slot);
                if (!File.Exists(profilePath))
                {
                    Debug.LogWarning($"[SilksongNeuralSmart] Слот {slot} пуст — запускаем новую тренировку.");
                    return false;
                }

                string json = File.ReadAllText(profilePath, Encoding.UTF8);

                mode.ProfileName = ReadString(json, "name", "Слот " + slot);
                mode.PlayTimeSeconds = ReadFloat(json, "playTimeSeconds", 0f);
                mode.Wave = Mathf.Max(1, ReadInt(json, "wave", 1));
                mode.BestWave = Mathf.Max(mode.Wave, ReadInt(json, "bestWave", mode.Wave));
                mode.BaseMobCount = Mathf.Clamp(ReadInt(json, "baseMobCount", 4), GrandArenaMode.MIN_MOBS, GrandArenaMode.MAX_MOBS);
                mode.MobCount = Mathf.Clamp(ReadInt(json, "mobCount", mode.BaseMobCount), GrandArenaMode.MIN_MOBS, GrandArenaMode.MAX_MOBS);
                mode.TotalEpisodes = ReadInt(json, "totalEpisodes", 0);
                mode.HornetWins = ReadInt(json, "hornetWins", 0);
                mode.PackWins = ReadInt(json, "packWins", 0);
                mode.Draws = ReadInt(json, "draws", 0);
                mode.TotalMobsDefeated = ReadInt(json, "mobsDefeated", 0);

                mode.TimeScale = Mathf.Clamp(ReadFloat(json, "timeScale", 1f), 0.25f, 50f);
                mode.MaxEpisodeDuration = Mathf.Clamp(ReadFloat(json, "maxEpisodeDuration", 60f), 10f, 300f);
                mode.WaveScaling = ReadBool(json, "waveScaling", true);
                mode.BalancePackDamage = ReadBool(json, "balancePackDamage", true);
                mode.PlayerControlsHornet = ReadBool(json, "playerControlsHornet", false);
                mode.AutoSaveEveryEpisodes = ReadInt(json, "autoSaveEveryEpisodes", 5);
                mode.ReactionDelayMs = ReadFloat(json, "reactionDelayMs", 110f);
                mode.ErrorMarginRate = ReadFloat(json, "errorMarginRate", 0.05f);

                mode.HornetFitnessLog.Clear();
                mode.HornetFitnessLog.AddRange(ReadFloatArray(json, "hornetFitnessHistory"));
                mode.PackFitnessLog.Clear();
                mode.PackFitnessLog.AddRange(ReadFloatArray(json, "packFitnessHistory"));

                // --- Мозг Хорнет ---
                string hornetPath = Path.Combine(dir, "hornet.brain.json");
                if (File.Exists(hornetPath))
                {
                    NeuralNetwork brain = NeuralNetwork.FromJson(File.ReadAllText(hornetPath, Encoding.UTF8));
                    mode.HornetTrainer.RestoreFromBrain(
                        brain,
                        ReadInt(json, "hornetGeneration", 1),
                        ReadFloat(json, "hornetBestFitness", 0f),
                        mode.HornetFitnessLog);
                }

                // --- Мозги мобов ---
                foreach (string key in GrandArenaMode.ArchetypeKeys)
                {
                    string mobPath = Path.Combine(dir, $"mob_{key}.brain.json");
                    if (!File.Exists(mobPath)) continue;
                    if (!mode.MobTrainers.TryGetValue(key, out GeneticTrainer trainer)) continue;

                    NeuralNetwork brain = NeuralNetwork.FromJson(File.ReadAllText(mobPath, Encoding.UTF8));
                    trainer.RestoreFromBrain(
                        brain,
                        ReadInt(json, "gen_" + key, 1),
                        ReadFloat(json, "fit_" + key, 0f));
                }

                Debug.Log($"[SilksongNeuralSmart] Слот {slot} загружен: волна {mode.Wave}, эпизодов {mode.TotalEpisodes}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SilksongNeuralSmart] Ошибка загрузки слота {slot}: {ex.Message}");
                return false;
            }
        }

        public static bool DeleteSlot(int slot)
        {
            try
            {
                string dir = SlotDirectory(slot);
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                    Debug.Log($"[SilksongNeuralSmart] Слот {slot} удалён.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SilksongNeuralSmart] Не удалось удалить слот {slot}: {ex.Message}");
                return false;
            }
        }

        // ==================================================================
        // Мини-JSON утилиты (без внешних зависимостей)
        // ==================================================================
        private static string SafeRead(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : ""; }
            catch { return ""; }
        }

        private static float SafeFitness(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= float.MinValue * 0.5f ? 0f : value;
        }

        private static string F(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        private static string FloatArray(List<float> values)
        {
            if (values == null || values.Count == 0) return "[]";

            var sb = new StringBuilder("[");
            int start = Mathf.Max(0, values.Count - 200); // храним последние 200 точек графика
            for (int i = start; i < values.Count; i++)
            {
                sb.Append(F(values[i]));
                if (i < values.Count - 1) sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string FindValueToken(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";

            int idx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (idx < 0) return "";

            int colon = json.IndexOf(':', idx);
            if (colon < 0) return "";

            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return "";

            if (json[i] == '"')
            {
                int end = i + 1;
                var sb = new StringBuilder();
                while (end < json.Length && json[end] != '"')
                {
                    if (json[end] == '\\' && end + 1 < json.Length)
                    {
                        end++;
                        sb.Append(json[end]);
                    }
                    else
                    {
                        sb.Append(json[end]);
                    }
                    end++;
                }
                return sb.ToString();
            }

            if (json[i] == '[')
            {
                int end = json.IndexOf(']', i);
                if (end < 0) return "";
                return json.Substring(i, end - i + 1);
            }

            int tokenEnd = i;
            while (tokenEnd < json.Length && json[tokenEnd] != ',' && json[tokenEnd] != '}' && json[tokenEnd] != '\n')
                tokenEnd++;

            return json.Substring(i, tokenEnd - i).Trim();
        }

        public static string ReadString(string json, string key, string fallback)
        {
            string token = FindValueToken(json, key);
            return string.IsNullOrEmpty(token) ? fallback : token;
        }

        public static int ReadInt(string json, string key, int fallback)
        {
            string token = FindValueToken(json, key);
            if (string.IsNullOrEmpty(token)) return fallback;
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? Mathf.RoundToInt(f) : fallback);
        }

        public static float ReadFloat(string json, string key, float fallback)
        {
            string token = FindValueToken(json, key);
            if (string.IsNullOrEmpty(token)) return fallback;
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        public static bool ReadBool(string json, string key, bool fallback)
        {
            string token = FindValueToken(json, key);
            if (string.IsNullOrEmpty(token)) return fallback;
            if (token.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (token.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }

        public static List<float> ReadFloatArray(string json, string key)
        {
            var result = new List<float>();
            string token = FindValueToken(json, key);
            if (string.IsNullOrEmpty(token) || token.Length < 2) return result;

            token = token.Trim('[', ']');
            if (string.IsNullOrEmpty(token)) return result;

            string[] parts = token.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    result.Add(value);
            }

            return result;
        }
    }
}

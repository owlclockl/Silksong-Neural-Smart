using System;
using System.Collections.Generic;
using System.Reflection;
using Steamworks;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Рефлекционный мост к мультиплеерному моду XvX (SilksongMultiplayer.dll).
    /// Мод не нужен RosaryShare на этапе компиляции: мы находим его объект
    /// «LobbyManager(Clone)» и компонент SilksongMultiplayer.RoomManager,
    /// а из публичных полей читаем id лобби Steam (currentRoomID) и флаг
    /// входа (enterRoom). Если мод отсутствует или поля переименуют в новой
    /// версии — RosaryShare аккуратно отключается и пишет диагностику в лог.
    /// </summary>
    internal static class XvXBridge
    {
        private const string LobbyObjectName = "LobbyManager(Clone)";
        private const string LobbyObjectNameAlt = "LobbyManager";
        private const string RoomManagerTypeName = "SilksongMultiplayer.RoomManager";

        private static object _roomManager;
        private static FieldInfo _fieldRoomId;
        private static FieldInfo _fieldEnterRoom;

        private static float _nextResolveAt;
        private static float _nextDeepScanAt;
        private static bool _loggedResolutionFailure;

        /// <summary>Как часто разрешён полный перебор объектов сцены (секунды).</summary>
        private const float DeepScanIntervalSeconds = 5f;

        /// <summary>Мост разрешён и готов к чтению лобби.</summary>
        public static bool BridgeOk { get; private set; }

        /// <summary>Вызывается каждый кадр из TransferManager.Update; ресолвит мост не чаще раза в секунду.</summary>
        public static void Tick()
        {
            if (Time.unscaledTime < _nextResolveAt) return;
            _nextResolveAt = Time.unscaledTime + 1f;
            Resolve();
        }

        /// <summary>Немедленно проверить наличие мультиплеерного мода.</summary>
        public static bool IsMultiplayerPresent()
        {
            Resolve();
            return BridgeOk;
        }

        private static void Resolve()
        {
            // Сначала проверяем, не умер ли закешированный экземпляр
            // (мод XvX пересоздаёт LobbyManager при возврате в главное меню).
            Component cached = _roomManager as Component;
            if (cached != null && !cached)
                _roomManager = null;

            if (_roomManager == null)
            {
                MonoBehaviour found = FindRoomManager();
                if (found == null)
                {
                    MarkFailed("LobbyManager object is not found. The multiplayer mod is either not installed or no menu has been loaded yet.");
                    return;
                }

                Type t = found.GetType();
                _fieldRoomId = t.GetField("currentRoomID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _fieldEnterRoom = t.GetField("enterRoom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (_fieldRoomId == null)
                {
                    MarkFailed("Field 'currentRoomID' was not found on " + RoomManagerTypeName + ". This multiplayer mod version may be incompatible. Fields: " + DescribeFields(t));
                    return;
                }

                _roomManager = found;
                _loggedResolutionFailure = false;
                RosarySharePlugin.LogInfo("XvX multiplayer bridge resolved: " + RoomManagerTypeName);
            }

            BridgeOk = true;
        }

        private static MonoBehaviour FindRoomManager()
        {
            try
            {
                // Мод XvX пересоздаёт LobbyManager при каждом возврате в главное
                // меню и может накапливать дубликаты — ищем по ВСЕМ объектам
                // с нужным именем и предпочитаем RoomManager в состоянии enterRoom.
                MonoBehaviour fallback = null;

                // Сначала быстрый поиск по имени: в 99% случаев объект один,
                // и перебирать всю сцену каждую секунду незачем (это заметные
                // просадки кадра в больших локациях).
                MonoBehaviour quick = ScanObject(GameObject.Find(LobbyObjectName), ref fallback);
                if (quick != null) return quick;

                quick = ScanObject(GameObject.Find(LobbyObjectNameAlt), ref fallback);
                if (quick != null) return quick;

                if (fallback != null) return fallback;

                // Полный перебор сцены — редкий запасной путь (мод XvX умеет
                // плодить дубликаты LobbyManager при возврате в меню).
                if (Time.unscaledTime < _nextDeepScanAt) return null;
                _nextDeepScanAt = Time.unscaledTime + DeepScanIntervalSeconds;

                GameObject[] sceneObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (GameObject go in sceneObjects)
                {
                    if (go == null) continue;
                    if (go.name != LobbyObjectName && go.name != LobbyObjectNameAlt) continue;

                    MonoBehaviour match = ScanObject(go, ref fallback);
                    if (match != null) return match;
                }

                return fallback;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("FindRoomManager failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Ищет RoomManager на конкретном объекте. Возвращает компонент,
        /// который уже вошёл в комнату, иначе запоминает его как запасной.
        /// </summary>
        private static MonoBehaviour ScanObject(GameObject go, ref MonoBehaviour fallback)
        {
            if (!go) return null;

            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (!mb) continue; // и реальный null, и «фейковый» null уничтоженных Unity-объектов
                Type type = mb.GetType();
                if (type.FullName != RoomManagerTypeName) continue;

                if (fallback == null) fallback = mb;
                try
                {
                    FieldInfo er = type.GetField("enterRoom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (er != null && er.GetValue(mb) is bool b && b) return mb;
                }
                catch { /* игнорируем, возьмём fallback */ }
            }

            return null;
        }

        private static void MarkFailed(string reason)
        {
            BridgeOk = false;
            _roomManager = null;
            if (!_loggedResolutionFailure)
            {
                _loggedResolutionFailure = true;
                RosarySharePlugin.LogWarning("XvX bridge: " + reason);
            }
        }

        private static string DescribeFields(Type t)
        {
            try
            {
                List<string> names = new List<string>();
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    names.Add(f.Name + ":" + f.FieldType.Name);
                return string.Join(", ", names.ToArray());
            }
            catch
            {
                return "(no details)";
            }
        }

        /// <summary>Текущее лобби Steam, если мы в него вошли.</summary>
        public static bool TryGetLobby(out CSteamID lobbyId)
        {
            lobbyId = new CSteamID(0);

            if (!BridgeOk || _roomManager == null) return false;
            Component comp = _roomManager as Component;
            if (comp == null || !comp) { _roomManager = null; BridgeOk = false; return false; }

            try
            {
                if (_fieldEnterRoom != null && !(_fieldEnterRoom.GetValue(_roomManager) is bool entered && entered))
                    return false;

                object raw = _fieldRoomId.GetValue(_roomManager);
                if (!(raw is CSteamID)) return false;

                CSteamID id = (CSteamID)raw;
                if (id.m_SteamID == 0) return false;

                lobbyId = id;
                return true;
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("TryGetLobby failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Список участников лобби (включая себя).</summary>
        public static List<CSteamID> GetLobbyMembers(CSteamID lobbyId)
        {
            List<CSteamID> members = new List<CSteamID>();
            try
            {
                int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
                for (int i = 0; i < count; i++)
                {
                    CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
                    if (member.m_SteamID != 0) members.Add(member);
                }
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("GetLobbyMembers failed: " + e.Message);
            }
            return members;
        }

        /// <summary>Имя игрока Steam (как показывает сам мультиплеерный мод).</summary>
        public static string GetPlayerName(CSteamID id)
        {
            try
            {
                string name = SteamFriends.GetFriendPersonaName(id);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch
            {
                // ignore
            }

            string digits = id.m_SteamID.ToString();
            return "Player " + (digits.Length > 4 ? digits.Substring(digits.Length - 4) : digits);
        }

        /// <summary>Steam ID локального игрока (0, если Steam недоступен).</summary>
        public static CSteamID SelfId
        {
            get
            {
                try { return SteamUser.GetSteamID(); }
                catch { return new CSteamID(0); }
            }
        }

        /// <summary>Имя локального игрока Steam.</summary>
        public static string SelfName()
        {
            try
            {
                string name = SteamFriends.GetPersonaName();
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch
            {
                // ignore
            }
            return "Player";
        }
    }
}

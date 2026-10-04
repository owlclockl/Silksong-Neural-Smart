using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace RosaryShare
{
    /// <summary>
    /// Ядро RosaryShare: жизненный цикл перевода бусин.
    ///
    /// Отправка:  списываем бусины -> шлём Transfer -> ждём Ack    (иначе возврат)
    /// Отказ:     получатель отвечает Reject      -> возврат отправителю
    /// Таймаут:   нет ответа AckTimeoutSeconds    -> возврат отправителю
    /// Уход цели: игрок вышел из лобби до ответа  -> возврат отправителю
    ///
    /// Бусины никогда не «исчезают» молча — любой недоставленный перевод
    /// возвращается отправителю с уведомлением.
    /// </summary>
    internal sealed class TransferManager : MonoBehaviour
    {
        public static TransferManager Instance { get; private set; }

        // ---------------- Публичные модели ----------------

        public sealed class RemotePlayer
        {
            public CSteamID Id;
            public string Name;
        }

        public sealed class HistoryEntry
        {
            public string Text;
            public ToastLog.Kind Kind;
            public float AtTime;
        }

        public enum ResourceKind { Beads, Shards, Item }

        public enum SendError
        {
            Ok,
            NoLobby,
            NotInGame,
            NoTarget,
            InvalidAmount,
            ExceedsLimit,
            NotEnough,
            Cooldown,
            TooManyPending,
            NetworkError,
        }

        // ---------------- Состояние ----------------

        private sealed class PendingTx
        {
            public uint TxId;
            public CSteamID Target;
            public string TargetName;
            public int Amount;
            public float Deadline;
            public ResourceKind Resource;
            public string ItemKey;
        }

        private const int MaxPendingTransfers = 16;
        private const int MaxHistoryEntries = 50;
        private const int MaxProcessedTx = 1024;

        private static readonly System.Random Rng = new System.Random();

        private readonly List<RemotePlayer> _players = new List<RemotePlayer>();
        private readonly List<PendingTx> _pending = new List<PendingTx>();
        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();
        private readonly Dictionary<string, string> _nameOverrides = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _knownNames = new Dictionary<string, string>();
        private readonly HashSet<uint> _processedTx = new HashSet<uint>();
        private readonly Queue<uint> _processedOrder = new Queue<uint>();

        private static uint _txCounter;

        private float _sendCooldownUntil;
        private float _nextMemberPoll;
        private float _nextHello;
        private bool _wasInLobby;

        public IReadOnlyList<RemotePlayer> Players
        {
            get { return _players; }
        }

        public IReadOnlyList<HistoryEntry> History
        {
            get { return _history; }
        }

        public int PendingCount
        {
            get { return _pending.Count; }
        }

        /// <summary>Версия журнала (для автопрокрутки окна).</summary>
        public int HistoryVersion { get; private set; }

        public bool InLobby { get; private set; }

        // ---------------- Unity ----------------

        private void Awake()
        {
            Instance = this;
            SteamChannel.OnPacket += HandlePacket;
        }

        private void OnDestroy()
        {
            SteamChannel.OnPacket -= HandlePacket;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            XvXBridge.Tick();
            SteamChannel.Pump();

            float now = Time.unscaledTime;

            CSteamID lobby = new CSteamID(0);
            bool inLobby = XvXBridge.BridgeOk && XvXBridge.TryGetLobby(out lobby);
            InLobby = inLobby;

            if (inLobby != _wasInLobby)
            {
                _wasInLobby = inLobby;

                if (inLobby)
                {
                    RosarySharePlugin.LogInfo("Entered lobby " + lobby.m_SteamID);
                    RefreshMembers();
                    SendHello();
                    _nextMemberPoll = now + 1f;
                    _nextHello = now + 5f;
                }
                else
                {
                    OnLobbyLost();
                }
            }

            if (inLobby)
            {
                if (now >= _nextMemberPoll)
                {
                    _nextMemberPoll = now + 1f;
                    RefreshMembers();
                }

                if (now >= _nextHello)
                {
                    _nextHello = now + 5f;
                    SendHello();
                }
            }

            SweepTimeouts(now);
        }

        // ---------------- Отправка ----------------

        /// <summary>Попытаться отправить бусины. Возвращает код ошибки (Ok — перевод ушёл в сеть).</summary>
        public SendError TrySend(RemotePlayer target, int amount, ResourceKind resource = ResourceKind.Beads)
        {
            if (!XvXBridge.BridgeOk || !InLobby) return SendError.NoLobby;
            if (!GameBridge.InGame) return SendError.NotInGame;
            if (target == null || target.Id.m_SteamID == 0) return SendError.NoTarget;
            if (amount <= 0) return SendError.InvalidAmount;
            if (amount > ModConfig.MaxSendAmount) return SendError.ExceedsLimit;

            float now = Time.unscaledTime;
            if (now < _sendCooldownUntil) return SendError.Cooldown;
            if (_pending.Count >= MaxPendingTransfers) return SendError.TooManyPending;

            // цель должна сейчас быть в том же лобби
            if (!IsMember(target.Id)) return SendError.NoTarget;

            int reserved = 0;
            foreach (PendingTx tx in _pending) if (tx.Resource == resource) reserved += tx.Amount;

            int effectiveBalance = (resource == ResourceKind.Shards ? GameBridge.GetShards() : GameBridge.GetGeo()) - reserved;
            if (amount > effectiveBalance) return SendError.NotEnough;

            // списываем сразу, и только потом шлём пакет
            if (resource == ResourceKind.Shards) GameBridge.AddShards(-amount);
            else GameBridge.TakeGeo(amount);

            uint txId = NextTxId();
            PendingTx pending = new PendingTx
            {
                TxId = txId,
                Target = target.Id,
                TargetName = target.Name ?? XvXBridge.GetPlayerName(target.Id),
                Amount = amount,
                Deadline = now + ModConfig.AckTimeoutSeconds,
                Resource = resource,
            };
            _pending.Add(pending);
            _sendCooldownUntil = now + ModConfig.SendCooldownSeconds;

            byte[] packet = Packets.MakeTransfer(txId, amount, XvXBridge.SelfName(), resource == ResourceKind.Shards);
            if (!SteamChannel.Send(target.Id, packet))
            {
                // сеть недоступна — тут же возвращаем списанное
                _pending.Remove(pending);
                if (resource == ResourceKind.Shards) GameBridge.AddShards(amount); else GameBridge.AddGeo(amount);
                RosarySharePlugin.LogWarning("Steam send failed, resource refunded.");
                return SendError.NetworkError;
            }

            AddHistory(string.Format(resource == ResourceKind.Shards ? Texts.T("→ {0}: {1} осколков (ожидание подтверждения…)", "→ {0}: {1} shards (awaiting acknowledgement…)") : Texts.T("→ {0}: {1} бусин (ожидание подтверждения…)", "→ {0}: {1} beads (awaiting acknowledgement…)"), pending.TargetName, amount), ToastLog.Kind.Info);
            RosarySharePlugin.LogInfo(string.Format("Transfer {0}: {1} {2} -> {3} ({4})", txId, amount, resource == ResourceKind.Shards ? "shards" : "beads", pending.TargetName, target.Id.m_SteamID));
            return SendError.Ok;
        }

        public SendError TrySendItem(RemotePlayer target, string itemKey, int amount)
        {
            if (!XvXBridge.BridgeOk || !InLobby) return SendError.NoLobby;
            if (!GameBridge.InGame) return SendError.NotInGame;
            if (target == null || !IsMember(target.Id)) return SendError.NoTarget;
            if (amount <= 0 || amount > ModConfig.MaxSendAmount) return SendError.InvalidAmount;
            float now = Time.unscaledTime;
            if (now < _sendCooldownUntil) return SendError.Cooldown;
            if (_pending.Count >= MaxPendingTransfers) return SendError.TooManyPending;
            if (!ItemBridge.TryRemove(itemKey, amount)) return SendError.NotEnough;

            uint txId = NextTxId();
            PendingTx pending = new PendingTx { TxId = txId, Target = target.Id,
                TargetName = target.Name ?? XvXBridge.GetPlayerName(target.Id), Amount = amount,
                Deadline = now + ModConfig.AckTimeoutSeconds, Resource = ResourceKind.Item, ItemKey = itemKey };
            _pending.Add(pending);
            _sendCooldownUntil = now + ModConfig.SendCooldownSeconds;
            if (!SteamChannel.Send(target.Id, Packets.MakeItemTransfer(txId, amount, XvXBridge.SelfName(), itemKey)))
            {
                _pending.Remove(pending);
                ItemBridge.TryAdd(itemKey, amount);
                return SendError.NetworkError;
            }
            AddHistory(string.Format(Texts.T("→ {0}: {1} × {2} (ожидание…)", "→ {0}: {1} × {2} (pending…)"), pending.TargetName, amount, ItemBridge.NameOf(itemKey)), ToastLog.Kind.Info);
            return SendError.Ok;
        }

        private static uint NextTxId()
        {
            uint id = ((uint)Rng.Next(0, 0xFFFF) << 16) ^ (_txCounter++);
            if (id == 0) id = 1;
            return id;
        }

        // ---------------- Приём пакетов ----------------

        private void HandlePacket(CSteamID sender, byte[] data)
        {
            PacketKind kind;
            uint txId;
            int amount;
            string text;

            if (!Packets.TryParse(data, out kind, out txId, out amount, out text))
                return;

            // принимаем трафик только от текущих членов нашего лобби
            if (kind != PacketKind.Hello && !IsMember(sender) && !HasPendingWith(sender))
                return;

            switch (kind)
            {
                case PacketKind.Hello:
                    if (!string.IsNullOrEmpty(text))
                    {
                        _nameOverrides[sender.m_SteamID.ToString()] = text;
                        RefreshNames();
                    }
                    break;

                case PacketKind.Transfer:
                    OnIncomingTransfer(sender, txId, amount, text, ResourceKind.Beads);
                    break;

                case PacketKind.TransferShards:
                    OnIncomingTransfer(sender, txId, amount, text, ResourceKind.Shards);
                    break;

                case PacketKind.TransferItem:
                    OnIncomingItem(sender, txId, amount, text);
                    break;

                case PacketKind.Ack:
                    OnAck(sender, txId);
                    break;

                case PacketKind.Reject:
                    OnReject(sender, txId, text);
                    break;
            }
        }

        private void OnIncomingTransfer(CSteamID sender, uint txId, int amount, string senderName, ResourceKind resource)
        {
            // защита от повторной обработки одного пакета
            if (_processedTx.Contains(txId))
            {
                SteamChannel.Send(sender, Packets.MakeAck(txId));
                return;
            }
            RememberProcessed(txId);

            string fromName = !string.IsNullOrEmpty(senderName) ? senderName : XvXBridge.GetPlayerName(sender);

            if (!ModConfig.AllowReceive)
            {
                SteamChannel.Send(sender, Packets.MakeReject(txId, "disabled"));
                RosarySharePlugin.LogInfo("Declined incoming transfer from " + fromName + " (receiving disabled).");
                return;
            }

            if (!GameBridge.InGame)
            {
                SteamChannel.Send(sender, Packets.MakeReject(txId, "busy"));
                AddHistory(string.Format(resource == ResourceKind.Shards ? Texts.T("{0} пытался(ась) передать вам {1} осколков, но сохранение не загружено — перевод отклонён", "{0} tried to send you {1} shards, but no save is loaded — transfer declined") : Texts.T("{0} пытался(ась) передать вам {1} бусин, но сохранение не загружено — перевод отклонён", "{0} tried to send you {1} beads, but no save is loaded — transfer declined"), fromName, amount), ToastLog.Kind.Warn);
                return;
            }

            if (amount <= 0 || amount > ModConfig.MaxReceiveAmount)
            {
                SteamChannel.Send(sender, Packets.MakeReject(txId, "invalid"));
                RosarySharePlugin.LogWarning("Rejected malformed transfer from " + fromName + ": amount=" + amount);
                return;
            }

            if (resource == ResourceKind.Shards) GameBridge.AddShards(amount); else GameBridge.AddGeo(amount);
            SteamChannel.Send(sender, Packets.MakeAck(txId));

            string msg = string.Format(resource == ResourceKind.Shards ? Texts.T("Получено {0} осколков от {1}", "Received {0} shards from {1}") : Texts.T("Получено {0} бусин от {1}", "Received {0} beads from {1}"), amount, fromName);
            Toast(ToastLog.Kind.Success, msg);
            AddHistory("← " + msg, ToastLog.Kind.Success);
            RosarySharePlugin.LogInfo(msg);
        }

        private void OnIncomingItem(CSteamID sender, uint txId, int amount, string payload)
        {
            int split = payload != null ? payload.IndexOf('\n') : -1;
            string senderName = split >= 0 ? payload.Substring(0, split) : XvXBridge.GetPlayerName(sender);
            string itemKey = split >= 0 ? payload.Substring(split + 1) : string.Empty;
            if (_processedTx.Contains(txId)) { SteamChannel.Send(sender, Packets.MakeAck(txId)); return; }
            RememberProcessed(txId);
            if (!ModConfig.AllowReceive || !GameBridge.InGame || amount <= 0 || amount > ModConfig.MaxReceiveAmount || !ItemBridge.TryAdd(itemKey, amount))
            {
                SteamChannel.Send(sender, Packets.MakeReject(txId, "invalid"));
                return;
            }
            SteamChannel.Send(sender, Packets.MakeAck(txId));
            string msg = string.Format(Texts.T("Получено: {0} × {1} от {2}", "Received: {0} × {1} from {2}"), amount, ItemBridge.NameOf(itemKey), senderName);
            Toast(ToastLog.Kind.Success, msg); AddHistory("← " + msg, ToastLog.Kind.Success);
        }

        private void OnAck(CSteamID sender, uint txId)
        {
            PendingTx tx = FindPending(txId, sender);
            if (tx == null) return;
            _pending.Remove(tx);

            string msg = tx.Resource == ResourceKind.Item ? string.Format(Texts.T("Доставлено: {0} получил(а) {1} × {2}", "Delivered: {0} received {1} × {2}"), tx.TargetName, tx.Amount, ItemBridge.NameOf(tx.ItemKey)) : string.Format(tx.Resource == ResourceKind.Shards ? Texts.T("Доставлено: {0} получил(а) {1} осколков", "Delivered: {0} received {1} shards") : Texts.T("Доставлено: {0} получил(а) {1} бусин", "Delivered: {0} received {1} beads"), tx.TargetName, tx.Amount);
            Toast(ToastLog.Kind.Success, msg);
            AddHistory("✓ " + msg, ToastLog.Kind.Success);
            RosarySharePlugin.LogInfo(msg);
        }

        private void OnReject(CSteamID sender, uint txId, string reasonCode)
        {
            PendingTx tx = FindPending(txId, sender);
            if (tx == null) return;
            _pending.Remove(tx);

            Refund(tx, LocalizedReason(reasonCode));
        }

        private void SweepTimeouts(float now)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingTx tx = _pending[i];
                if (now < tx.Deadline) continue;

                _pending.RemoveAt(i);
                Refund(tx, Texts.T("нет ответа", "no response"));
            }
        }

        private void Refund(PendingTx tx, string reason)
        {
            if (GameBridge.InGame) { if (tx.Resource == ResourceKind.Item) ItemBridge.TryAdd(tx.ItemKey, tx.Amount); else if (tx.Resource == ResourceKind.Shards) GameBridge.AddShards(tx.Amount); else GameBridge.AddGeo(tx.Amount); }

            string msg = tx.Resource == ResourceKind.Item ? string.Format(Texts.T("Возврат: {0} × {1} ({2})", "Refunded: {0} × {1} ({2})"), tx.Amount, ItemBridge.NameOf(tx.ItemKey), reason) : string.Format(tx.Resource == ResourceKind.Shards ? Texts.T("Возврат: {0} осколков ({1})", "Refunded: {0} shards ({1})") : Texts.T("Возврат: {0} бусин ({1})", "Refunded: {0} beads ({1})"), tx.Amount, reason);
            Toast(ToastLog.Kind.Warn, msg);
            AddHistory("↩ " + msg, ToastLog.Kind.Warn);
            RosarySharePlugin.LogInfo(msg + " (tx " + tx.TxId + " -> " + tx.TargetName + ")");
        }

        private string LocalizedReason(string reasonCode)
        {
            switch (reasonCode)
            {
                case "disabled": return Texts.T("у игрока отключён приём переводов", "the player has disabled receiving");
                case "busy": return Texts.T("игрок не в игре", "the player is not in game");
                case "invalid": return Texts.T("отклонено получателем", "declined by the recipient");
                default: return Texts.T("отклонено", "declined");
            }
        }

        // ---------------- Лобби / члены ----------------

        private bool IsMember(CSteamID id)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Id == id) return true;

            return id == XvXBridge.SelfId;
        }

        private bool HasPendingWith(CSteamID id)
        {
            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i].Target == id) return true;
            return false;
        }

        private PendingTx FindPending(uint txId, CSteamID sender)
        {
            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i].TxId == txId && _pending[i].Target == sender)
                    return _pending[i];
            return null;
        }

        private void RefreshMembers()
        {
            CSteamID lobby;
            if (!XvXBridge.TryGetLobby(out lobby)) return;

            List<CSteamID> members = XvXBridge.GetLobbyMembers(lobby);
            CSteamID self = XvXBridge.SelfId;

            List<RemotePlayer> fresh = new List<RemotePlayer>();
            foreach (CSteamID member in members)
            {
                if (member == self) continue;

                fresh.Add(new RemotePlayer
                {
                    Id = member,
                    Name = ResolveName(member),
                });
            }

            // новые имена не должны «моргать», если Steam временно не вернул persona name
            foreach (RemotePlayer p in fresh)
            {
                string key = p.Id.m_SteamID.ToString();
                if (!string.IsNullOrEmpty(p.Name)) _knownNames[key] = p.Name;
                else if (_knownNames.TryGetValue(key, out string cached)) p.Name = cached;
            }

            _players.Clear();
            _players.AddRange(fresh);

            // цели незавершённых переводов ещё в лобби?
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingTx tx = _pending[i];
                bool stillHere = false;
                foreach (CSteamID member in members)
                {
                    if (member == tx.Target) { stillHere = true; break; }
                }

                if (!stillHere)
                {
                    _pending.RemoveAt(i);
                    Refund(tx, Texts.T("игрок вышел из лобби", "the player left the lobby"));
                }
            }
        }

        private void RefreshNames()
        {
            foreach (RemotePlayer p in _players)
                p.Name = ResolveName(p.Id);
        }

        private string ResolveName(CSteamID id)
        {
            string key = id.m_SteamID.ToString();

            string helloName;
            if (_nameOverrides.TryGetValue(key, out helloName) && !string.IsNullOrEmpty(helloName))
                return helloName;

            string steamName = XvXBridge.GetPlayerName(id);
            if (!string.IsNullOrEmpty(steamName)) return steamName;

            string cached;
            if (_knownNames.TryGetValue(key, out cached)) return cached;

            return "Player";
        }

        private void OnLobbyLost()
        {
            if (_pending.Count > 0)
            {
                foreach (PendingTx tx in _pending)
                {
                    if (GameBridge.InGame) { if (tx.Resource == ResourceKind.Item) ItemBridge.TryAdd(tx.ItemKey, tx.Amount); else if (tx.Resource == ResourceKind.Shards) GameBridge.AddShards(tx.Amount); else GameBridge.AddGeo(tx.Amount); }
                    string refundMsg = tx.Resource == ResourceKind.Item
                        ? string.Format(Texts.T("↩ Возврат: {0} × {1} (лобби закрыто)", "↩ Refunded: {0} × {1} (lobby closed)"), tx.Amount, ItemBridge.NameOf(tx.ItemKey))
                        : (tx.Resource == ResourceKind.Shards
                            ? string.Format(Texts.T("↩ Возврат: {0} осколков (лобби закрыто)", "↩ Refunded: {0} shards (lobby closed)"), tx.Amount)
                            : string.Format(Texts.T("↩ Возврат: {0} бусин (лобби закрыто)", "↩ Refunded: {0} beads (lobby closed)"), tx.Amount));
                    AddHistory(refundMsg, ToastLog.Kind.Warn);
                }
                _pending.Clear();

                Toast(ToastLog.Kind.Warn, Texts.T("Лобби закрыто — незавершённые переводы возвращены", "Lobby closed — pending transfers were refunded"));
            }

            _players.Clear();
            _nameOverrides.Clear();
            RosarySharePlugin.LogInfo("Left the lobby.");
        }

        private void SendHello()
        {
            CSteamID lobby;
            if (!XvXBridge.TryGetLobby(out lobby)) return;

            byte[] hello = Packets.MakeHello(XvXBridge.SelfName());
            CSteamID self = XvXBridge.SelfId;

            foreach (CSteamID member in XvXBridge.GetLobbyMembers(lobby))
            {
                if (member == self) continue;
                SteamChannel.Send(member, hello);
            }
        }

        private void RememberProcessed(uint txId)
        {
            _processedTx.Add(txId);
            _processedOrder.Enqueue(txId);

            while (_processedOrder.Count > MaxProcessedTx)
                _processedTx.Remove(_processedOrder.Dequeue());
        }

        // ---------------- Журнал / тосты ----------------

        private void AddHistory(string text, ToastLog.Kind kind)
        {
            _history.Add(new HistoryEntry
            {
                Text = text,
                Kind = kind,
                AtTime = Time.unscaledTime,
            });

            while (_history.Count > MaxHistoryEntries)
                _history.RemoveAt(0);

            HistoryVersion++;
        }

        private static void Toast(ToastLog.Kind kind, string text)
        {
            if (ToastLog.Instance != null) ToastLog.Instance.Add(text, kind);
        }
    }
}

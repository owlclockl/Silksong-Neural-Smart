using System;
using Steamworks;

namespace RosaryShare
{
    /// <summary>
    /// Собственный сетевой канал RosaryShare поверх Steam P2P.
    /// Мультиплеерный мод XvX гоняет свои пакеты по каналу 0, поэтому мы
    /// используем канал 1: та же P2P-сеcсия между участниками лобби Steam
    /// (Steam автоматически принимает сессии между членами одного лобби),
    /// но полностью независимый поток сообщений — протоколы не пересекаются.
    /// </summary>
    internal static class SteamChannel
    {
        public const int Channel = 1;
        public const int MaxPacket = 4096;

        /// <summary>(отправитель, данные). Вызывается в главном потоке (из Update).</summary>
        public static Action<CSteamID, byte[]> OnPacket;

        /// <summary>Разобрать очередь входящих пакетов. Вызывать каждый кадр.</summary>
        public static void Pump()
        {
            try
            {
                uint size;
                while (SteamNetworking.IsP2PPacketAvailable(out size, Channel))
                {
                    if (size == 0 || size > MaxPacket)
                    {
                        // вычитываем и выбрасываем, чтобы не застрять на битом пакете
                        byte[] drain = new byte[size <= 0 ? 1 : size];
                        uint ignored;
                        CSteamID ignoredSender;
                        SteamNetworking.ReadP2PPacket(drain, (uint)drain.Length, out ignored, out ignoredSender, Channel);
                        continue;
                    }

                    byte[] buffer = new byte[size];
                    uint read;
                    CSteamID sender;
                    if (!SteamNetworking.ReadP2PPacket(buffer, size, out read, out sender, Channel))
                        continue;

                    if (read != size)
                        continue;

                    Action<CSteamID, byte[]> handler = OnPacket;
                    if (handler != null)
                    {
                        try
                        {
                            handler(sender, buffer);
                        }
                        catch (Exception e)
                        {
                            RosarySharePlugin.LogError("Packet handler failed: " + e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Steam API может быть временно недоступен — просто пробуем позже
                RosarySharePlugin.LogDebug("SteamChannel.Pump: " + e.Message);
            }
        }

        /// <summary>Надёжно (TCP-подобно) отправить пакет конкретному игроку.</summary>
        public static bool Send(CSteamID to, byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > MaxPacket) return false;
            if (to.m_SteamID == 0) return false;

            try
            {
                return SteamNetworking.SendP2PPacket(to, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable, Channel);
            }
            catch (Exception e)
            {
                RosarySharePlugin.LogDebug("SteamChannel.Send: " + e.Message);
                return false;
            }
        }
    }
}

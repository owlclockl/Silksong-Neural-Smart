using System;
using System.IO;
using System.Text;

namespace RosaryShare
{
    internal enum PacketKind : byte
    {
        Transfer = 0, // txId, amount, senderName
        Ack = 1,      // txId
        Reject = 2,   // txId, reasonCode
        Hello = 3,    // name (периодический анонс для красивых имён в списке)
        TransferShards = 4, // txId, amount, senderName
    }

    /// <summary>
    /// Двоичный формат пакетов RosaryShare (канал 1).
    /// Заголовок: 5 байт "RSWP1", дальше тип пакета и его поля.
    /// </summary>
    internal static class Packets
    {
        private static readonly byte[] Magic =
        {
            (byte)'R', (byte)'S', (byte)'W', (byte)'P', (byte)'1',
        };

        private const int MaxStringBytes = 192;

        // ---------------- Сериализация ----------------

        public static byte[] MakeTransfer(uint txId, int amount, string senderName, bool shards = false)
        {
            using (MemoryStream ms = StartPacket(shards ? PacketKind.TransferShards : PacketKind.Transfer))
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(txId);
                bw.Write(amount);
                WriteString(bw, senderName);
                return ms.ToArray();
            }
        }

        public static byte[] MakeAck(uint txId)
        {
            using (MemoryStream ms = StartPacket(PacketKind.Ack))
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(txId);
                return ms.ToArray();
            }
        }

        public static byte[] MakeReject(uint txId, string reasonCode)
        {
            using (MemoryStream ms = StartPacket(PacketKind.Reject))
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(txId);
                WriteString(bw, reasonCode);
                return ms.ToArray();
            }
        }

        public static byte[] MakeHello(string name)
        {
            using (MemoryStream ms = StartPacket(PacketKind.Hello))
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                WriteString(bw, name);
                return ms.ToArray();
            }
        }

        private static MemoryStream StartPacket(PacketKind kind)
        {
            MemoryStream ms = new MemoryStream(64);
            ms.Write(Magic, 0, Magic.Length);
            ms.WriteByte((byte)kind);
            return ms;
        }

        private static void WriteString(BinaryWriter bw, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                bw.Write((ushort)0);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > MaxStringBytes)
            {
                // урезаем до допустимого размера, не разрывая UTF-8 последовательность
                int cut = MaxStringBytes;
                while (cut > 0 && (bytes[cut] & 0xC0) == 0x80) cut--;
                Array.Resize(ref bytes, cut);
            }

            bw.Write((ushort)bytes.Length);
            bw.Write(bytes);
        }

        private static string ReadString(BinaryReader br)
        {
            ushort length = br.ReadUInt16();
            if (length == 0) return string.Empty;
            if (length > MaxStringBytes) throw new InvalidDataException("String too long: " + length);
            byte[] bytes = br.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }

        // ---------------- Разбор ----------------

        public static bool TryParse(byte[] data, out PacketKind kind, out uint txId, out int amount, out string text)
        {
            kind = PacketKind.Transfer;
            txId = 0;
            amount = 0;
            text = null;

            try
            {
                if (data == null || data.Length < Magic.Length + 1) return false;

                for (int i = 0; i < Magic.Length; i++)
                    if (data[i] != Magic[i]) return false;

                PacketKind parsedKind = (PacketKind)data[Magic.Length];
                if (!Enum.IsDefined(typeof(PacketKind), parsedKind)) return false;

                using (MemoryStream ms = new MemoryStream(data, false))
                using (BinaryReader br = new BinaryReader(ms, Encoding.UTF8))
                {
                    ms.Position = Magic.Length;
                    br.ReadByte(); // kind

                    switch (parsedKind)
                    {
                        case PacketKind.Transfer:
                        case PacketKind.TransferShards:
                            txId = br.ReadUInt32();
                            amount = br.ReadInt32();
                            text = ReadString(br);
                            break;

                        case PacketKind.Ack:
                            txId = br.ReadUInt32();
                            break;

                        case PacketKind.Reject:
                            txId = br.ReadUInt32();
                            text = ReadString(br);
                            break;

                        case PacketKind.Hello:
                            text = ReadString(br);
                            break;
                    }
                }

                kind = parsedKind;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

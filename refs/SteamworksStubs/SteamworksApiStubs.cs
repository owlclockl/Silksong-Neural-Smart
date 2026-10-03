// Reference stub for com.rlabrecque.steamworks.net (Steamworks.NET).
// Public signatures copied from the official Steamworks.NET sources
// (isteanetworking.cs / isteammatchmaking.cs / isteamfriends.cs /
//  isteamuser.cs / SteamEnums.cs / CSteamID.cs). Compile-time only.

using System;

namespace Steamworks
{
    public struct CSteamID : IEquatable<CSteamID>, IComparable<CSteamID>
    {
        public ulong m_SteamID;

        public CSteamID(ulong accountId)
        {
            m_SteamID = accountId;
        }

        public override string ToString() { return m_SteamID.ToString(); }

        public override int GetHashCode() { return m_SteamID.GetHashCode(); }

        public override bool Equals(object other)
        {
            return other is CSteamID id && this == id;
        }

        public bool Equals(CSteamID other) { return m_SteamID == other.m_SteamID; }

        public int CompareTo(CSteamID other) { return m_SteamID.CompareTo(other.m_SteamID); }

        public static bool operator ==(CSteamID x, CSteamID y) { return x.m_SteamID == y.m_SteamID; }
        public static bool operator !=(CSteamID x, CSteamID y) { return !(x == y); }
    }

    public enum EP2PSend : int
    {
        k_EP2PSendUnreliable = 0,
        k_EP2PSendUnreliableNoDelay = 1,
        k_EP2PSendReliable = 2,
        k_EP2PSendReliableWithBuffering = 3,
    }

    public static class SteamNetworking
    {
        public static bool SendP2PPacket(CSteamID steamIDRemote, byte[] pubData, uint cubData, EP2PSend eP2PSendType, int nChannel = 0)
        {
            throw new NotImplementedException();
        }

        public static bool IsP2PPacketAvailable(out uint pcubMsgSize, int nChannel = 0)
        {
            throw new NotImplementedException();
        }

        public static bool ReadP2PPacket(byte[] pubDest, uint cubDest, out uint pcubMsgSize, out CSteamID psteamIDRemote, int nChannel = 0)
        {
            throw new NotImplementedException();
        }
    }

    public static class SteamMatchmaking
    {
        public static int GetNumLobbyMembers(CSteamID steamIDLobby)
        {
            throw new NotImplementedException();
        }

        public static CSteamID GetLobbyMemberByIndex(CSteamID steamIDLobby, int iMember)
        {
            throw new NotImplementedException();
        }

        public static CSteamID GetLobbyOwner(CSteamID steamIDLobby)
        {
            throw new NotImplementedException();
        }
    }

    public static class SteamFriends
    {
        public static string GetPersonaName()
        {
            throw new NotImplementedException();
        }

        public static string GetFriendPersonaName(CSteamID steamIDFriend)
        {
            throw new NotImplementedException();
        }
    }

    public static class SteamUser
    {
        public static CSteamID GetSteamID()
        {
            throw new NotImplementedException();
        }
    }
}

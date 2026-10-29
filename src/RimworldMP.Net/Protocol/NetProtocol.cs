namespace RimworldMP.Net.Protocol;

/// <summary>
/// Wire framing: length-prefixed frames with a leading protocol version on handshake.
/// Frame layout (TCP): [u32 LE length][u8 messageType][payload…] where length covers type+payload.
/// LiteNetLib: same [u8 messageType][payload…] body without the length prefix (UDP framing by LNL).
/// Protocol v3: hard-gate join on protocol version + ordered modlist hash in the handshake.
/// Protocol v4: optional handshake trailer (transport kind + pacing flags); optional JIP chunk-size
/// trailer; MinSupportedVersion stays 3 so v3 TCP peers still interconnect.
/// </summary>
public static class NetProtocol
{
    public const byte Version = 4;
    /// <summary>Oldest protocol version this build will accept on handshake.</summary>
    public const byte MinSupportedVersion = 3;
    public const int DefaultPort = 30555;
    /// <summary>UDP discovery beacon port (launcher-only — do not bind game transport here).</summary>
    public const int DiscoveryPort = 30556;
    /// <summary>LAN ModSync TCP port (launcher/mod manager — stay clear of game transport).</summary>
    public const int ModSyncPort = 30557;
    /// <summary>Per-frame cap. JIP save blobs are split into chunks under this limit.</summary>
    public const int MaxFrameBytes = 4 << 20; // 4 MiB
    public const int JoinPointChunkBytes = 256 * 1024;
    /// <summary>v4+ LiteNetLib preferred JIP chunk size (still ≤ <see cref="JoinPointChunkBytes"/>).</summary>
    public const int LiteNetLibJoinPointChunkBytes = 32 * 1024;
    /// <summary>Default inter-chunk sleep for LiteNetLib paced JIP sends.</summary>
    public const int LiteNetLibInterChunkDelayMs = 2;

    /// <summary>Handshake magic (unchanged from v3 so MinSupportedVersion=3 peers keep wire identity).</summary>
    public static readonly byte[] HandshakeMagic = "RMP3"u8.ToArray();

    /// <summary>LiteNetLib connection key shared by host and client.</summary>
    public const string LiteNetLibConnectKey = "RimworldMP";

    public static bool IsCompatible(byte remoteVersion) =>
        remoteVersion >= MinSupportedVersion && remoteVersion <= Version;
}

public enum NetMessageType : byte
{
    Handshake = 1,
    JoinAck = 2,
    CommandBatch = 3,
    StateHash = 4,
    DesyncFreeze = 5,
    KeepAlive = 6,
    JoinPointRequest = 7,
    JoinPointHeader = 8,
    JoinPointChunk = 9,
    JoinPointDone = 10,
    AutosaveAnnounce = 11,
    ReadyStatus = 12,
    PeerLeaveNotice = 13,
    /// <summary>Host → client: hard reject (protocol / modlist). Payload: u8 reason + utf8 message.</summary>
    JoinReject = 14,
    /// <summary>Synced lobby/game chat. Payload: i32 playerId + u16 len + utf8 text. Not a sim command.</summary>
    Chat = 15,
    /// <summary>System notice (kick, leave, ready, mismatch). Payload: u8 kind + u16 len + utf8 text.</summary>
    SystemNotice = 16,
    /// <summary>Player display-name announce. Payload: i32 playerId + u16 len + utf8 name.</summary>
    PlayerInfo = 17,
    /// <summary>Host → client: you were kicked. Payload: u16 len + utf8 reason.</summary>
    KickNotice = 18,
}

/// <summary>Reasons for <see cref="NetMessageType.JoinReject"/>.</summary>
public enum JoinRejectReason : byte
{
    ProtocolMismatch = 1,
    ModlistMismatch = 2,
    LobbyFull = 3,
    DlcMismatch = 4,
    Unspecified = 255,
}

/// <summary>Kinds for <see cref="NetMessageType.SystemNotice"/>.</summary>
public enum SystemNoticeKind : byte
{
    Info = 0,
    PeerJoined = 1,
    PeerLeft = 2,
    PeerKicked = 3,
    ReadyChanged = 4,
    ProtocolMismatch = 5,
    ModlistMismatch = 6,
    DlcMismatch = 7,
    Recovery = 8,
}

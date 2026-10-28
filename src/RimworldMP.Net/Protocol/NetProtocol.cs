namespace RimworldMP.Net.Protocol;

/// <summary>
/// Wire framing: length-prefixed frames with a leading protocol version on handshake.
/// Frame layout: [u32 LE length][u8 messageType][payload…] where length covers type+payload.
/// Protocol v3: hard-gate join on protocol version + ordered modlist hash in the handshake.
/// </summary>
public static class NetProtocol
{
    public const byte Version = 3;
    /// <summary>Oldest protocol version this build will accept on handshake.</summary>
    public const byte MinSupportedVersion = 3;
    public const int DefaultPort = 30555;
    /// <summary>Per-frame cap. JIP save blobs are split into chunks under this limit.</summary>
    public const int MaxFrameBytes = 4 << 20; // 4 MiB
    public const int JoinPointChunkBytes = 256 * 1024;

    /// <summary>Handshake magic for protocol v3+ (was <c>RMP2</c> through v0.5.0).</summary>
    public static readonly byte[] HandshakeMagic = "RMP3"u8.ToArray();

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
}

/// <summary>Reasons for <see cref="NetMessageType.JoinReject"/>.</summary>
public enum JoinRejectReason : byte
{
    ProtocolMismatch = 1,
    ModlistMismatch = 2,
    LobbyFull = 3,
    Unspecified = 255,
}

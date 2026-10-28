namespace RimworldMP.Net.Protocol;

/// <summary>
/// Wire framing: length-prefixed frames with a leading protocol version on handshake.
/// Frame layout: [u32 LE length][u8 messageType][payload…] where length covers type+payload.
/// Protocol v2 adds join-in-progress (JIP) transfer messages.
/// </summary>
public static class NetProtocol
{
    public const byte Version = 2;
    public const int DefaultPort = 30555;
    /// <summary>Per-frame cap. JIP save blobs are split into chunks under this limit.</summary>
    public const int MaxFrameBytes = 4 << 20; // 4 MiB
    public const int JoinPointChunkBytes = 256 * 1024;

    public static readonly byte[] HandshakeMagic = "RMP2"u8.ToArray();
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
}

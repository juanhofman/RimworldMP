namespace RimworldMP.Net.Protocol;

/// <summary>
/// Wire framing: length-prefixed frames with a leading protocol version on handshake.
/// Frame layout: [u32 LE length][u8 messageType][payload…] where length covers type+payload.
/// </summary>
public static class NetProtocol
{
    public const byte Version = 1;
    public const int DefaultPort = 30555;
    public const int MaxFrameBytes = 1 << 20; // 1 MiB hard cap

    public static readonly byte[] HandshakeMagic = "RMP1"u8.ToArray();
}

public enum NetMessageType : byte
{
    Handshake = 1,
    JoinAck = 2,
    CommandBatch = 3,
    StateHash = 4,
    DesyncFreeze = 5,
    KeepAlive = 6,
}

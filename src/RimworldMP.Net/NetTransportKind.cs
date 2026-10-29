namespace RimworldMP.Net;

/// <summary>
/// Selectable game-session transports. Default remains <see cref="Tcp"/>.
/// Discovery (30556) and ModSync (30557) are separate and unaffected.
/// </summary>
public enum NetTransportKind : byte
{
    /// <summary>LAN TCP length-prefixed frames (<see cref="LanTcpTransport"/>).</summary>
    Tcp = 0,

    /// <summary>LAN reliable UDP via LiteNetLib (<see cref="LiteNetLibTransport"/>).</summary>
    LiteNetLib = 1,
}

/// <summary>Creates an <see cref="INetTransport"/> for the configured kind.</summary>
public static class NetTransportFactory
{
    public static INetTransport Create(NetTransportKind kind) =>
        kind switch
        {
            NetTransportKind.Tcp => new LanTcpTransport(),
            NetTransportKind.LiteNetLib => new LiteNetLibTransport(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown transport kind."),
        };

    /// <summary>Parse handoff / config strings: <c>tcp</c>, <c>litenetlib</c>, <c>udp</c>, <c>lnl</c>.</summary>
    public static bool TryParse(string? value, out NetTransportKind kind)
    {
        kind = NetTransportKind.Tcp;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "tcp":
            case "lan-tcp":
            case "lantcp":
                kind = NetTransportKind.Tcp;
                return true;
            case "litenetlib":
            case "lite-net-lib":
            case "lnl":
            case "udp":
            case "reliable-udp":
                kind = NetTransportKind.LiteNetLib;
                return true;
            default:
                return false;
        }
    }
}

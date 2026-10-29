using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Session;

/// <summary>
/// Process-wide multiplayer defaults. Override before <see cref="SessionFacade.HostLobbyAsync"/> /
/// join. Documented for launcher handoff + in-mod lobby.
/// </summary>
public static class MpConfig
{
    /// <summary>Default LAN client cap (raised from the Phase-1 toy default of 1).</summary>
    public const int DefaultMaxClients = 4;

    /// <summary>Host: max simultaneous clients. Clamped to ≥ 1.</summary>
    public static int MaxClients { get; set; } = DefaultMaxClients;

    /// <summary>Default listen / join port (game MP — TCP or LiteNetLib UDP).</summary>
    public static int DefaultPort { get; set; } = NetProtocol.DefaultPort;

    /// <summary>
    /// When set, host and client handshake must agree on this ordered modlist hash
    /// (hard gate). Empty means "unset" — LAN transports still require both sides to send
    /// the same string (including empty) or the host rejects.
    /// </summary>
    public static string ModlistHash { get; set; } = string.Empty;

    /// <summary>When true, host stalls <see cref="MpSession.HostEmitNextBatch"/> until ready gate passes.</summary>
    public static bool ReadyRequired { get; set; }

    /// <summary>
    /// Game-session transport. Default <see cref="NetTransportKind.Tcp"/> (fallback).
    /// Set to <see cref="NetTransportKind.LiteNetLib"/> for reliable UDP. Both peers must match.
    /// </summary>
    public static NetTransportKind TransportKind { get; set; } = NetTransportKind.Tcp;

    public static void ResetToDefaults()
    {
        MaxClients = DefaultMaxClients;
        DefaultPort = NetProtocol.DefaultPort;
        ModlistHash = string.Empty;
        ReadyRequired = false;
        TransportKind = NetTransportKind.Tcp;
    }
}

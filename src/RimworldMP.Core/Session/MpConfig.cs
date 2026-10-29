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

    /// <summary>Host: max simultaneous TCP clients. Clamped to ≥ 1.</summary>
    public static int MaxClients { get; set; } = DefaultMaxClients;

    /// <summary>Default listen / join port.</summary>
    public static int DefaultPort { get; set; } = NetProtocol.DefaultPort;

    /// <summary>
    /// When set, host and client handshake must agree on this ordered modlist hash
    /// (hard gate). Empty means "unset" — LAN TCP still requires both sides to send
    /// the same string (including empty) or the host rejects.
    /// </summary>
    public static string ModlistHash { get; set; } = string.Empty;

    /// <summary>
    /// When true, host stalls <see cref="MpSession.HostEmitNextBatch"/> and blocks unpause/speed
    /// until the ready gate passes. Lobby UI enables this on host; harness defaults stay false.
    /// </summary>
    public static bool ReadyRequired { get; set; }

    /// <summary>Optional local display name applied on host/join (handoff or lobby field).</summary>
    public static string PlayerDisplayName { get; set; } = string.Empty;

    public static void ResetToDefaults()
    {
        MaxClients = DefaultMaxClients;
        DefaultPort = NetProtocol.DefaultPort;
        ModlistHash = string.Empty;
        ReadyRequired = false;
        PlayerDisplayName = string.Empty;
    }
}

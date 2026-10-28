namespace RimworldMP.API;

/// <summary>
/// Public entry points for third-party mods. Phase 1: session flags are wired through
/// <c>RimworldMP.Core.Session.SessionFacade</c> when the main mod is loaded.
/// </summary>
public static class MP
{
    private static Func<bool>? _isInSession;
    private static Func<bool>? _isHost;
    private static Func<int>? _localPlayerId;
    private static Action<ushort, byte[]?>? _submit;
    private static event Action? DesyncHandlers;

    /// <summary>True when RimworldMP assemblies are loaded.</summary>
    public static bool Enabled => true;

    /// <summary>True when a multiplayer session is active.</summary>
    public static bool IsInSession => _isInSession?.Invoke() ?? false;

    /// <summary>True when this peer is the in-game host.</summary>
    public static bool IsHost => _isHost?.Invoke() ?? false;

    /// <summary>Local player id within the session, or -1 when not in session.</summary>
    public static int LocalPlayerId => _localPlayerId?.Invoke() ?? -1;

    /// <summary>
    /// Advanced: enqueue a lockstep command by opcode. Prefer higher-level helpers as they land.
    /// </summary>
    public static void ExecuteCommand(ushort opcode, byte[]? payload = null) =>
        _submit?.Invoke(opcode, payload);

    /// <summary>Subscribe to desync freeze notifications (host and clients).</summary>
    public static event Action? OnDesync
    {
        add => DesyncHandlers += value;
        remove => DesyncHandlers -= value;
    }

    /// <summary>Called by the main mod to bind API accessors — not for third-party use.</summary>
    public static void Bind(
        Func<bool> isInSession,
        Func<bool> isHost,
        Func<int> localPlayerId,
        Action<ushort, byte[]?> submit,
        Action raiseDesync)
    {
        _isInSession = isInSession;
        _isHost = isHost;
        _localPlayerId = localPlayerId;
        _submit = submit;
        _ = raiseDesync;
        DesyncRaise = () => DesyncHandlers?.Invoke();
    }

    /// <summary>Internal trampoline invoked by the main mod on desync.</summary>
    public static Action? DesyncRaise { get; private set; }
}

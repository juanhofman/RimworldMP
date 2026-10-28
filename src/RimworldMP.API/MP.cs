using System.Collections.Concurrent;
using System.Reflection;

namespace RimworldMP.API;

/// <summary>
/// Public entry points for third-party mods. Prefer these over soft-depending on Core.
/// </summary>
public static class MP
{
    private static Func<bool>? _isInSession;
    private static Func<bool>? _isHost;
    private static Func<int>? _localPlayerId;
    private static Func<long>? _tick;
    private static Action<ushort, byte[]?>? _submit;
    private static event Action? DesyncHandlers;

    private static readonly ConcurrentDictionary<string, MethodInfo> SyncMethods = new(StringComparer.Ordinal);

    /// <summary>True when RimworldMP assemblies are loaded.</summary>
    public static bool Enabled => true;

    /// <summary>True when a multiplayer session is active.</summary>
    public static bool IsInSession => _isInSession?.Invoke() ?? false;

    /// <summary>True when this peer is the in-game host.</summary>
    public static bool IsHost => _isHost?.Invoke() ?? false;

    /// <summary>Local player id within the session, or -1 when not in session.</summary>
    public static int LocalPlayerId => _localPlayerId?.Invoke() ?? -1;

    /// <summary>Current lockstep tick, or -1 when not in session.</summary>
    public static long Tick => _tick?.Invoke() ?? -1;

    /// <summary>Enqueue a lockstep command by opcode.</summary>
    public static void ExecuteCommand(ushort opcode, byte[]? payload = null) =>
        _submit?.Invoke(opcode, payload);

    /// <summary>
    /// Register a method that should be invoked deterministically on all peers.
    /// Phase 2 stores the registration for the host framework to wire; authors should still
    /// route mutations through commands where possible.
    /// </summary>
    public static void RegisterSyncMethod(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var key = method.DeclaringType?.FullName + "::" + method.Name;
        SyncMethods[key] = method;
    }

    /// <summary>True if <paramref name="method"/> was registered via <see cref="RegisterSyncMethod"/>.</summary>
    public static bool IsSyncMethodRegistered(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var key = method.DeclaringType?.FullName + "::" + method.Name;
        return SyncMethods.ContainsKey(key);
    }

    public static IReadOnlyCollection<string> RegisteredSyncMethodKeys => SyncMethods.Keys.ToArray();

    /// <summary>Subscribe to desync freeze notifications.</summary>
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
        Action raiseDesync,
        Func<long>? tick = null)
    {
        _isInSession = isInSession;
        _isHost = isHost;
        _localPlayerId = localPlayerId;
        _submit = submit;
        _tick = tick;
        _ = raiseDesync;
        DesyncRaise = () => DesyncHandlers?.Invoke();
    }

    public static Action? DesyncRaise { get; private set; }
}

using System.Collections.Concurrent;
using System.Reflection;

namespace RimworldMP.API;

/// <summary>
/// Public entry points for third-party mods. Reference <c>RimworldMP.API</c> only —
/// do not soft-depend on Core/Net unless you need internals.
/// </summary>
public static class MP
{
    private static Func<bool>? _isInSession;
    private static Func<bool>? _isHost;
    private static Func<int>? _localPlayerId;
    private static Func<long>? _tick;
    private static Action<ushort, byte[]?>? _submit;
    private static event Action? DesyncHandlers;

    private static readonly ConcurrentDictionary<string, SyncMethodRegistration> SyncMethods = new(StringComparer.Ordinal);

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

    /// <summary>Enqueue a lockstep command by opcode. Prefer SyncMethod helpers when possible.</summary>
    /// <param name="opcode">Command opcode (see Core <c>CommandOpcode</c>).</param>
    /// <param name="payload">Optional compact binary payload.</param>
    public static void ExecuteCommand(ushort opcode, byte[]? payload = null) =>
        _submit?.Invoke(opcode, payload);

    /// <summary>
    /// Register a method that should be invoked deterministically on all peers.
    /// Authors should still route mutations through commands / SyncMethod apply bodies.
    /// </summary>
    /// <param name="method">Static or instance method to register.</param>
    public static void RegisterSyncMethod(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var attr = method.GetCustomAttribute<SyncMethodAttribute>();
        var key = KeyOf(method, attr?.Name);
        SyncMethods[key] = new SyncMethodRegistration(key, method, attr?.Name);
    }

    /// <summary>
    /// Register a delegate's method as a SyncMethod. Useful for static local functions /
    /// method groups without manual <see cref="MethodInfo"/> lookup.
    /// </summary>
    public static void RegisterSyncMethod(Delegate method)
    {
        ArgumentNullException.ThrowIfNull(method);
        RegisterSyncMethod(method.Method);
    }

    /// <summary>
    /// Discover and register every method on <paramref name="type"/> marked with
    /// <see cref="SyncMethodAttribute"/>.
    /// </summary>
    /// <returns>Number of methods newly considered (including re-registrations).</returns>
    public static int RegisterSyncMethodsFromType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var count = 0;
        foreach (var method in type.GetMethods(flags))
        {
            if (method.GetCustomAttribute<SyncMethodAttribute>() is null)
                continue;
            RegisterSyncMethod(method);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Scan <paramref name="assembly"/> for <see cref="SyncMethodAttribute"/> methods and register them.
    /// </summary>
    public static int RegisterSyncMethodsFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var count = 0;
        foreach (var type in assembly.GetTypes())
            count += RegisterSyncMethodsFromType(type);
        return count;
    }

    /// <summary>True if <paramref name="method"/> was registered via <see cref="RegisterSyncMethod(MethodInfo)"/>.</summary>
    public static bool IsSyncMethodRegistered(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return SyncMethods.ContainsKey(KeyOf(method));
    }

    /// <summary>True if a SyncMethod is registered under <paramref name="key"/>.</summary>
    public static bool IsSyncMethodRegistered(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return SyncMethods.ContainsKey(key);
    }

    /// <summary>Registered SyncMethod keys (<c>DeclaringType::Name</c> form).</summary>
    public static IReadOnlyCollection<string> RegisteredSyncMethodKeys => SyncMethods.Keys.ToArray();

    /// <summary>
    /// Invoke a registered SyncMethod by key. Used by harness tests and (later) Core apply.
    /// </summary>
    /// <returns>True when a registration was found and invoked.</returns>
    public static bool TryInvokeSyncMethod(string key, object? target, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!SyncMethods.TryGetValue(key, out var reg))
            return false;
        reg.Method.Invoke(target, args);
        return true;
    }

    /// <summary>Builds the stable registry key for a method.</summary>
    public static string KeyOf(MethodInfo method, string? nameOverride = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        var name = nameOverride
                   ?? method.GetCustomAttribute<SyncMethodAttribute>()?.Name
                   ?? method.Name;
        return method.DeclaringType?.FullName + "::" + name;
    }

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

    /// <summary>Raises <see cref="OnDesync"/> — bound by the main mod.</summary>
    public static Action? DesyncRaise { get; private set; }

    /// <summary>
    /// Clears SyncMethod registrations and session binders. Intended for unit tests only.
    /// </summary>
    public static void ResetForTests()
    {
        SyncMethods.Clear();
        _isInSession = null;
        _isHost = null;
        _localPlayerId = null;
        _tick = null;
        _submit = null;
        DesyncHandlers = null;
        DesyncRaise = null;
        FieldWatch.Reset();
    }

    private sealed record SyncMethodRegistration(string Key, MethodInfo Method, string? FriendlyName);
}

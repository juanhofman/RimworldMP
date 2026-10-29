using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RimworldMP.API;

/// <summary>
/// Begin/end field-watch stubs for UI → sim flows.
/// Capture a value before a dialog, compare after OK, and enqueue a sync command on change.
/// </summary>
/// <remarks>
/// Without a live RimWorld session these helpers only track local snapshots for tests and
/// offline examples. Core binds real command emission later via <see cref="BindEmitter"/>.
/// </remarks>
public static class FieldWatch
{
    private static readonly ConcurrentDictionary<string, SyncFieldRegistration> Fields = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, object?> Snapshots = new(StringComparer.Ordinal);
    private static Action<string, object?, object?>? _emitChange;

    /// <summary>Registered SyncField keys (Type::Member form).</summary>
    public static IReadOnlyCollection<string> RegisteredFieldKeys => Fields.Keys.ToArray();

    /// <summary>True when <paramref name="member"/> was registered via <see cref="Register"/>.</summary>
    public static bool IsRegistered(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Fields.ContainsKey(KeyOf(member));
    }

    /// <summary>Register a field or property for watch helpers.</summary>
    public static void Register(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (member is not FieldInfo and not PropertyInfo)
            throw new ArgumentException("SyncField registration requires a field or property.", nameof(member));

        var attr = member.GetCustomAttribute<SyncFieldAttribute>();
        var key = KeyOf(member, attr?.Name);
        Fields[key] = new SyncFieldRegistration(key, member, attr?.Name);
    }

    /// <summary>Register every <see cref="SyncFieldAttribute"/> member on <paramref name="type"/>.</summary>
    public static int RegisterFromType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var count = 0;
        foreach (var member in type.GetMembers(flags))
        {
            if (member.GetCustomAttribute<SyncFieldAttribute>() is null)
                continue;
            if (member is not FieldInfo and not PropertyInfo)
                continue;
            Register(member);
            count++;
        }

        return count;
    }

    /// <summary>Snapshot the current value before a UI dialog mutates it.</summary>
    public static void WatchBegin(object? target, MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var key = KeyOf(member);
        if (!Fields.ContainsKey(key))
            Register(member);
        Snapshots[WatchInstanceKey(target, key)] = ReadValue(target, member);
    }

    /// <summary>
    /// Compare post-dialog value to the begin snapshot. When changed, invoke the bound emitter
    /// (if any) so Core can turn the delta into a lockstep command.
    /// </summary>
    /// <returns>True when the value changed since <see cref="WatchBegin"/>.</returns>
    public static bool WatchEnd(object? target, MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var key = KeyOf(member);
        var instanceKey = WatchInstanceKey(target, key);
        Snapshots.TryGetValue(instanceKey, out var before);
        var after = ReadValue(target, member);
        Snapshots.TryRemove(instanceKey, out _);

        if (Equals(before, after))
            return false;

        _emitChange?.Invoke(key, before, after);
        return true;
    }

    /// <summary>
    /// Convenience: watch <paramref name="member"/> for the lifetime of the returned scope.
    /// Dispose (end of <c>using</c>) calls <see cref="WatchEnd"/>.
    /// </summary>
    public static IDisposable Watch(object? target, MemberInfo member)
    {
        WatchBegin(target, member);
        return new WatchScope(target, member);
    }

    /// <summary>Bound by the main mod to enqueue field-change commands — not for third-party use.</summary>
    public static void BindEmitter(Action<string, object?, object?>? emitChange) =>
        _emitChange = emitChange;

    /// <summary>Clears registrations and snapshots — test / rebind helper.</summary>
    public static void Reset()
    {
        Fields.Clear();
        Snapshots.Clear();
        _emitChange = null;
    }

    /// <summary>Builds the stable registry key for a member.</summary>
    public static string KeyOf(MemberInfo member, string? nameOverride = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        var name = nameOverride
                   ?? member.GetCustomAttribute<SyncFieldAttribute>()?.Name
                   ?? member.Name;
        return member.DeclaringType?.FullName + "::" + name;
    }

    private static string WatchInstanceKey(object? target, string fieldKey) =>
        fieldKey + "#" + (target is null
            ? "static"
            : RuntimeHelpers.GetHashCode(target).ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static object? ReadValue(object? target, MemberInfo member) =>
        member switch
        {
            FieldInfo f => f.GetValue(f.IsStatic ? null : target),
            PropertyInfo p => p.GetValue(p.GetMethod?.IsStatic == true ? null : target),
            _ => throw new ArgumentException("Expected field or property.", nameof(member)),
        };

    private sealed class WatchScope(object? target, MemberInfo member) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            WatchEnd(target, member);
        }
    }

    private sealed record SyncFieldRegistration(string Key, MemberInfo Member, string? FriendlyName);
}

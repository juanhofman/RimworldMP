namespace RimworldMP.Compat;

/// <summary>
/// Common sim-compat helpers for Workshop mods. Prefer these over ad-hoc nondeterministic patterns.
/// </summary>
public static class SimPatterns
{
    /// <summary>Stable sort by key before iterating collections that affect sim outcomes.</summary>
    public static IEnumerable<T> StableOrder<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector)
        where TKey : IComparable<TKey> =>
        source.OrderBy(keySelector);

    /// <summary>Host-only side effect gate — callers should sync results via MP commands.</summary>
    public static bool ShouldRunHostOnly(bool isHost) => isHost;

    /// <summary>
    /// Marker for fields that must be watched begin/end around UI dialogs.
    /// Actual watch plumbing is owned by Core session when a session is active.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SyncFieldAttribute : Attribute
    {
        public SyncFieldAttribute(string? name = null) => Name = name;
        public string? Name { get; }
    }

    /// <summary>Marker for methods that must execute identically on all peers.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SyncMethodAttribute : Attribute
    {
    }
}

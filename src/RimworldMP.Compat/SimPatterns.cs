namespace RimworldMP.Compat;

/// <summary>
/// Common sim-compat helpers for Workshop mods. Prefer these over ad-hoc nondeterministic patterns.
/// SyncMethod / SyncField / host-inject types live in <c>RimworldMP.API</c>.
/// </summary>
public static class SimPatterns
{
    /// <summary>Stable sort by key before iterating collections that affect sim outcomes.</summary>
    public static IEnumerable<T> StableOrder<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector)
        where TKey : IComparable<TKey> =>
        source.OrderBy(keySelector);

    /// <summary>Host-only side effect gate — callers should sync results via MP commands / HostInject.</summary>
    public static bool ShouldRunHostOnly(bool isHost) => isHost;
}

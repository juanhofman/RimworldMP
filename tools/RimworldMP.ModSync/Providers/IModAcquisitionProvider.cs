namespace RimworldMP.ModSync.Providers;

/// <summary>
/// Optional secondary source for acquiring a mod when LAN transfer is unavailable
/// or as a local cache hit. Implementations must not scrape Steam web illegally.
/// </summary>
public interface IModAcquisitionProvider
{
    string Name { get; }

    /// <summary>
    /// When <see langword="false"/>, this provider never resolves folders — it exists only to
    /// document a deliberate non-goal (e.g. SteamCMD). Prefer reading <see cref="LimitationMessage"/>.
    /// </summary>
    bool IsImplemented { get; }

    /// <summary>Human-readable limitation when <see cref="IsImplemented"/> is false; otherwise null.</summary>
    string? LimitationMessage { get; }

    /// <summary>
    /// Try to resolve a local folder that already contains the requested package
    /// (e.g. Steam Workshop cache). Returns null when not available.
    /// </summary>
    Task<string?> TryResolveLocalFolderAsync(
        ModManifestEntry needed,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Documents that live Steam Workshop download via SteamCMD is <strong>not</strong> implemented.
/// Always resolves to null — never pretends to download. Prefer LAN host transfer or a local cache hit.
/// </summary>
public sealed class SteamCmdProviderStub : IModAcquisitionProvider
{
    public const string StubName = "steamcmd-unavailable";

    public const string StubLimitation =
        "Steam Workshop live download / SteamCMD is not supported. " +
        "Pull mods over LAN from the host (TCP 30557), copy from the local Workshop cache when hashes match, " +
        "or install the missing mod manually before syncing.";

    public string Name => StubName;

    public bool IsImplemented => false;

    public string? LimitationMessage => StubLimitation;

    public Task<string?> TryResolveLocalFolderAsync(
        ModManifestEntry needed,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = needed;
        // Intentionally unimplemented: never fabricate a download path.
        return Task.FromResult<string?>(null);
    }
}

namespace RimworldMP.ModSync.Providers;

/// <summary>
/// Optional secondary source for acquiring a mod when LAN transfer is unavailable
/// or as a local cache hit. Implementations must not scrape Steam web illegally.
/// </summary>
public interface IModAcquisitionProvider
{
    string Name { get; }

    /// <summary>
    /// Try to resolve a local folder that already contains the requested package
    /// (e.g. Steam Workshop cache). Returns null when not available.
    /// </summary>
    Task<string?> TryResolveLocalFolderAsync(
        ModManifestEntry needed,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stub for a future SteamCMD-based downloader. Not implemented — documents limitation.
/// </summary>
public sealed class SteamCmdProviderStub : IModAcquisitionProvider
{
    public string Name => "steamcmd-stub";

    public Task<string?> TryResolveLocalFolderAsync(
        ModManifestEntry needed,
        CancellationToken cancellationToken = default)
    {
        // Intentionally unimplemented: live Workshop download without an owned Steam
        // session / SteamCMD workflow is out of scope. Prefer LAN host transfer.
        return Task.FromResult<string?>(null);
    }
}

using System.Net.Sockets;
using RimworldMP.ModSync.Protocol;
using RimworldMP.ModSync.Providers;

namespace RimworldMP.ModSync;

/// <summary>Outcome of a LAN pull (library-level; launcher UI uses its own ModSyncResult DTO).</summary>
public sealed class LanSyncOutcome
{
    public required SessionModManifest HostManifest { get; init; }
    public required IReadOnlyList<string> Downloaded { get; init; }
    public required IReadOnlyList<string> CopiedFromWorkshop { get; init; }
    public required IReadOnlyList<string> AlreadyMatched { get; init; }
    public required string LocalModlistHash { get; init; }
    public bool HashesMatch =>
        ModlistHashMirror.Matches(LocalModlistHash, HostManifest.ModlistHash);
}

/// <summary>
/// Client: compare local inventory vs host manifest; pull missing/mismatched mods over LAN
/// (primary), or copy from local Workshop cache when hashes match (secondary).
/// </summary>
public sealed class ClientModSync
{
    private readonly string _modsRoot;
    private readonly IReadOnlyList<IModAcquisitionProvider> _providers;

    public ClientModSync(
        string modsRoot,
        IEnumerable<IModAcquisitionProvider>? providers = null)
    {
        if (string.IsNullOrWhiteSpace(modsRoot))
            throw new ArgumentException("Mods root required.", nameof(modsRoot));
        _modsRoot = Path.GetFullPath(modsRoot);
        _providers = (providers ?? Array.Empty<IModAcquisitionProvider>()).ToList();
    }

    public event Action<string>? Log;

    public async Task<SessionModManifest> FetchManifestAsync(
        string host,
        int port = ModSyncProtocol.DefaultPort,
        CancellationToken cancellationToken = default)
    {
        using var client = await ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        await HandshakeAsync(stream, cancellationToken).ConfigureAwait(false);
        await ModSyncFrameIo.WriteFrameAsync(stream, ModSyncOpcode.RequestManifest, ReadOnlyMemory<byte>.Empty, cancellationToken)
            .ConfigureAwait(false);
        var (op, payload) = await ModSyncFrameIo.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
        if (op == ModSyncOpcode.Error)
            throw new InvalidOperationException(ModSyncFrameIo.DecodeUtf8(payload));
        if (op != ModSyncOpcode.Manifest)
            throw new InvalidOperationException($"Expected Manifest, got {op}");
        return ModSyncFrameIo.DecodeManifest(payload);
    }

    public async Task<LanSyncOutcome> SyncFromHostAsync(
        string host,
        int port,
        IReadOnlyList<ModManifestEntry> localInstalled,
        IProgress<(string Phase, string? PackageId, double Fraction)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_modsRoot);

        progress?.Report(("connect", null, 0));
        using var client = await ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        await HandshakeAsync(stream, cancellationToken).ConfigureAwait(false);

        progress?.Report(("manifest", null, 0.05));
        await ModSyncFrameIo.WriteFrameAsync(stream, ModSyncOpcode.RequestManifest, ReadOnlyMemory<byte>.Empty, cancellationToken)
            .ConfigureAwait(false);
        var (op, payload) = await ModSyncFrameIo.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
        if (op == ModSyncOpcode.Error)
            throw new InvalidOperationException(ModSyncFrameIo.DecodeUtf8(payload));
        if (op != ModSyncOpcode.Manifest)
            throw new InvalidOperationException($"Expected Manifest, got {op}");

        var hostManifest = ModSyncFrameIo.DecodeManifest(payload);
        return await SyncAgainstManifestAsync(
                stream,
                hostManifest,
                localInstalled,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LanSyncOutcome> SyncAgainstManifestAsync(
        System.Net.Sockets.NetworkStream stream,
        SessionModManifest hostManifest,
        IReadOnlyList<ModManifestEntry> localInstalled,
        IProgress<(string Phase, string? PackageId, double Fraction)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var localMap = localInstalled.ToDictionary(m => m.PackageId, StringComparer.OrdinalIgnoreCase);

        var downloaded = new List<string>();
        var fromWorkshop = new List<string>();
        var matched = new List<string>();
        var total = Math.Max(1, hostManifest.Mods.Count);
        var index = 0;

        foreach (var needed in hostManifest.Mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            var frac = 0.1 + 0.85 * (index / (double)total);
            progress?.Report(("check", needed.PackageId, frac));

            if (localMap.TryGetValue(needed.PackageId, out var local) &&
                string.Equals(local.ContentHash, needed.ContentHash, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(local.Version ?? "", needed.Version ?? "", StringComparison.Ordinal))
            {
                matched.Add(needed.PackageId);
                Log?.Invoke($"ok (match): {needed.PackageId} @{needed.Version}");
                continue;
            }

            // Secondary: local workshop cache with matching content hash
            var resolved = await TryProvidersAsync(needed, cancellationToken).ConfigureAwait(false);
            if (resolved is not null)
            {
                var hash = ContentHasher.HashDirectory(resolved);
                if (string.Equals(hash, needed.ContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    var dest = CopyFolderIntoMods(resolved, needed.PackageId);
                    fromWorkshop.Add(needed.PackageId);
                    Log?.Invoke($"copied local cache → {dest}");
                    localMap[needed.PackageId] = needed with { LocalPath = dest };
                    progress?.Report(("cache", needed.PackageId, frac));
                    continue;
                }

                Log?.Invoke($"local cache hash mismatch for {needed.PackageId}; falling back to LAN");
            }

            // Primary: LAN from host
            Log?.Invoke($"downloading {needed.PackageId} from host…");
            progress?.Report(("download", needed.PackageId, frac));
            await ModSyncFrameIo.WriteFrameAsync(
                    stream,
                    ModSyncOpcode.RequestMod,
                    ModSyncFrameIo.EncodeUtf8(needed.PackageId),
                    cancellationToken)
                .ConfigureAwait(false);

            var (op, payload) = await ModSyncFrameIo.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
            if (op == ModSyncOpcode.Error)
                throw new InvalidOperationException(ModSyncFrameIo.DecodeUtf8(payload));
            if (op != ModSyncOpcode.ModBlob)
                throw new InvalidOperationException($"Expected ModBlob, got {op}");

            var (packageId, zip) = ModSyncFrameIo.DecodeModBlob(payload);
            if (!packageId.Equals(needed.PackageId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Host sent unexpected packageId '{packageId}'.");

            var folderName = PathSandbox.SafeFolderName(needed.PackageId);
            var destDir = ModArchive.ExtractZipToMods(zip, _modsRoot, folderName);
            var gotHash = ContentHasher.HashDirectory(destDir);
            if (!string.Equals(gotHash, needed.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Content hash mismatch after download for {needed.PackageId}: got {gotHash}, expected {needed.ContentHash}");
            }

            downloaded.Add(needed.PackageId);
            localMap[needed.PackageId] = needed with { LocalPath = destDir };
            Log?.Invoke($"installed {needed.PackageId} → {destDir}");
        }

        progress?.Report(("done", null, 1.0));
        var localHash = ModlistHashMirror.Compute(hostManifest.OrderedPackageIds);
        return new LanSyncOutcome
        {
            HostManifest = hostManifest,
            Downloaded = downloaded,
            CopiedFromWorkshop = fromWorkshop,
            AlreadyMatched = matched,
            LocalModlistHash = localHash,
        };
    }

    private async Task<string?> TryProvidersAsync(ModManifestEntry needed, CancellationToken ct)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var path = await provider.TryResolveLocalFolderAsync(needed, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    return path;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"provider {provider.Name} failed: {ex.Message}");
            }
        }

        return null;
    }

    private string CopyFolderIntoMods(string sourceDir, string packageId)
    {
        var folderName = PathSandbox.SafeFolderName(packageId);
        var dest = PathSandbox.RequireUnderRoot(_modsRoot, folderName);
        if (Directory.Exists(dest))
            Directory.Delete(dest, recursive: true);

        CopyDirectory(sourceDir, dest);
        return dest;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destDir, rel));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(destDir, rel);
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static async Task<TcpClient> ConnectAsync(string host, int port, CancellationToken ct)
    {
        var client = new TcpClient();
        await client.ConnectAsync(host, port, ct).ConfigureAwait(false);
        return client;
    }

    private static async Task HandshakeAsync(NetworkStream stream, CancellationToken ct)
    {
        await ModSyncFrameIo.WriteFrameAsync(stream, ModSyncOpcode.Hello, ModSyncFrameIo.EncodeHello(), ct)
            .ConfigureAwait(false);
        var (op, payload) = await ModSyncFrameIo.ReadFrameAsync(stream, ct).ConfigureAwait(false);
        if (op == ModSyncOpcode.Error)
            throw new InvalidOperationException(ModSyncFrameIo.DecodeUtf8(payload));
        if (op != ModSyncOpcode.HelloAck)
            throw new InvalidOperationException($"Expected HelloAck, got {op}");
        ModSyncFrameIo.ValidateHello(payload);
    }
}

namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// LAN modlist sync client (pull missing/mismatched packages from a host).
/// Production default is <c>LanModSyncClient</c>; <c>StubModSyncClient</c> is for tests / UI layout.
/// See docs/launcher-mod-manager-hooks.md.
/// </summary>
public interface IModSyncClient
{
    /// <summary>True when a real sync backend is registered (not the stub).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Compare local catalog against a host session (beacon / control channel).
    /// Does not download — returns missing, extra, and order/hash mismatches.
    /// </summary>
    Task<ModSyncPlan> CompareAsync(ModSyncHostTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute an approved plan: fetch packages, write into local Mods, optionally refresh ModsConfig.
    /// Reports progress via <paramref name="progress"/>.
    /// </summary>
    Task<ModSyncResult> SyncAsync(
        ModSyncPlan plan,
        IProgress<ModSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class ModSyncHostTarget
{
    public required string Host { get; init; }
    public int Port { get; init; } = 30557; // control / sync port (≠ game 30555, ≠ discovery 30556)
    public string? SessionName { get; init; }
    public string? ExpectedModlistHash { get; init; }
}

public sealed class ModSyncPlan
{
    public required string LocalHash { get; init; }
    public required string RemoteHash { get; init; }
    public bool HashesMatch { get; init; }
    public IReadOnlyList<ModSyncDelta> Missing { get; init; } = Array.Empty<ModSyncDelta>();
    public IReadOnlyList<ModSyncDelta> Mismatched { get; init; } = Array.Empty<ModSyncDelta>();
    public IReadOnlyList<ModSyncDelta> ExtraLocal { get; init; } = Array.Empty<ModSyncDelta>();
    public IReadOnlyList<string> RemoteOrder { get; init; } = Array.Empty<string>();
    /// <summary>Non-fatal but important notices (beacon hash drift, acquisition limits, etc.).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class ModSyncDelta
{
    public required string PackageId { get; init; }
    public string? DisplayName { get; init; }
    public string? Reason { get; init; }
    public long? ByteSize { get; init; }
}

public sealed class ModSyncProgress
{
    public string Phase { get; init; } = "";
    public string? CurrentPackageId { get; init; }
    public double Fraction { get; init; } // 0..1
    public long BytesTransferred { get; init; }
    public long? BytesTotal { get; init; }
    public string? Message { get; init; }
}

public sealed class ModSyncResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? ResultingHash { get; init; }
    public IReadOnlyList<string> AppliedPackageIds { get; init; } = Array.Empty<string>();
}

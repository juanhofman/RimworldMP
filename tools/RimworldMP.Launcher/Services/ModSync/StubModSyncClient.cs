namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Placeholder sync client so the Avalonia Mods screen can bind without a LAN file server yet.
/// The mod-sync agent should replace DI registration with a real implementation.
/// </summary>
public sealed class StubModSyncClient : IModSyncClient
{
    public bool IsAvailable => false;

    public Task<ModSyncPlan> CompareAsync(ModSyncHostTarget target, CancellationToken cancellationToken = default)
    {
        _ = target;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ModSyncPlan
        {
            LocalHash = "",
            RemoteHash = "",
            HashesMatch = false,
            Missing = Array.Empty<ModSyncDelta>(),
            Mismatched = Array.Empty<ModSyncDelta>(),
            ExtraLocal = Array.Empty<ModSyncDelta>(),
            RemoteOrder = Array.Empty<string>(),
        });
    }

    public Task<ModSyncResult> SyncAsync(
        ModSyncPlan plan,
        IProgress<ModSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _ = plan;
        progress?.Report(new ModSyncProgress
        {
            Phase = "unavailable",
            Fraction = 0,
            Message = "LAN mod sync is not implemented in this build. Waiting on mod-sync agent.",
        });
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ModSyncResult
        {
            Success = false,
            Error = "IModSyncClient stub: LAN transfer not wired. See docs/launcher-mod-manager-hooks.md.",
        });
    }
}

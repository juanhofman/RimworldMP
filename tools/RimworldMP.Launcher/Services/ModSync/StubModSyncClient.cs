namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Placeholder sync client for tests / Avalonia layout without a network backend.
/// Production uses <see cref="LanModSyncClient"/> via <see cref="ModSyncServices.UseLanBackends"/>.
/// </summary>
public sealed class StubModSyncClient : IModSyncClient
{
    public const string UnavailableMessage =
        "LAN mod sync backend not registered (stub). Call ModSyncServices.UseLanBackends() " +
        "or set ModSyncServices.Sync = new LanModSyncClient(...). See docs/launcher-mod-manager-hooks.md.";

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
            Warnings = new[] { UnavailableMessage },
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
            Message = UnavailableMessage,
        });
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ModSyncResult
        {
            Success = false,
            Error = UnavailableMessage,
        });
    }
}

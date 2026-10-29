using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RimworldMP.Launcher.Models;
using RimworldMP.Launcher.Services;
using RimworldMP.Launcher.Services.ModSync;

namespace RimworldMP.Launcher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SessionOrchestrator _orch = new();
    private readonly IModCatalog _catalog;
    private readonly IModSyncClient _modSync;
    private LanDiscovery? _hostDiscovery;
    private CancellationTokenSource? _hostCts;
    private CancellationTokenSource? _syncCts;
    private ModSyncPlan? _lastSyncPlan;

    public MainViewModel()
        : this(ModSyncServices.Catalog, ModSyncServices.Sync)
    {
    }

    public MainViewModel(IModCatalog catalog, IModSyncClient modSync)
    {
        _catalog = catalog;
        _modSync = modSync;
        SyncFromHostAvailable = _modSync.IsAvailable;
        SyncStatusMessage = _modSync.IsAvailable
            ? "Ready to compare with a LAN host."
            : "LAN mod sync backend not registered yet — UI hooks are ready.";
        RefreshInstall();
        RefreshModsSnapshot();
        StatusLine = "Local-only LAN launcher — no telemetry.";
    }

    [ObservableProperty] private LauncherNavPage _currentPage = LauncherNavPage.Home;

    [ObservableProperty] private string _statusLine = "";

    [ObservableProperty] private string? _installPath;

    [ObservableProperty] private string _installSummary = "RimWorld not found — set path on Launch.";

    [ObservableProperty] private string _sessionName = "RimworldMP LAN";

    [ObservableProperty] private string _hostPort = LauncherPaths.DefaultGamePort.ToString();

    [ObservableProperty] private string _bindHost = SessionOrchestrator.GetPrimaryLanIPv4() ?? "127.0.0.1";

    [ObservableProperty] private int _maxClients = 4;

    [ObservableProperty] private bool _prepareModsOnLaunch = true;

    [ObservableProperty] private bool _launchGame = true;

    [ObservableProperty] private bool _isHosting;

    [ObservableProperty] private string _hostStatus = "Idle";

    [ObservableProperty] private string _joinIp = "";

    [ObservableProperty] private string _joinPort = LauncherPaths.DefaultGamePort.ToString();

    [ObservableProperty] private bool _isDiscovering;

    [ObservableProperty] private DiscoveryBeacon? _selectedBeacon;

    [ObservableProperty] private string _modlistHash = "—";

    [ObservableProperty] private string _modsConfigPath = "—";

    [ObservableProperty] private string _modsWarning = "";

    [ObservableProperty] private string _launchLog = "Ready.";

    [ObservableProperty] private string _lastHandoffPath = LauncherPaths.HandoffPath;

    // --- LAN mod sync (IModSyncClient hooks; stub until mod-sync agent lands) ---
    [ObservableProperty] private string _syncHost = "";

    [ObservableProperty] private string _syncPort = "30557";

    [ObservableProperty] private bool _syncFromHostAvailable;

    [ObservableProperty] private bool _isSyncing;

    [ObservableProperty] private double _syncProgressFraction;

    [ObservableProperty] private string _syncProgressLabel = "";

    [ObservableProperty] private string _syncStatusMessage = "";

    [ObservableProperty] private bool _hasSyncDeltas;

    public ObservableCollection<string> ActiveMods { get; } = new();
    public ObservableCollection<ModListRow> ModRows { get; } = new();
    public ObservableCollection<DiscoveryBeacon> DiscoveredHosts { get; } = new();
    public ObservableCollection<ModSyncDeltaRow> SyncMissing { get; } = new();
    public ObservableCollection<ModSyncDeltaRow> SyncMismatched { get; } = new();

    public bool IsHome => CurrentPage == LauncherNavPage.Home;
    public bool IsHost => CurrentPage == LauncherNavPage.Host;
    public bool IsJoin => CurrentPage == LauncherNavPage.Join;
    public bool IsMods => CurrentPage == LauncherNavPage.Mods;
    public bool IsLaunch => CurrentPage == LauncherNavPage.Launch;

    partial void OnCurrentPageChanged(LauncherNavPage value)
    {
        OnPropertyChanged(nameof(IsHome));
        OnPropertyChanged(nameof(IsHost));
        OnPropertyChanged(nameof(IsJoin));
        OnPropertyChanged(nameof(IsMods));
        OnPropertyChanged(nameof(IsLaunch));
    }

    [RelayCommand]
    private void GoHome() => CurrentPage = LauncherNavPage.Home;

    [RelayCommand]
    private void GoHost() => CurrentPage = LauncherNavPage.Host;

    [RelayCommand]
    private void GoJoin() => CurrentPage = LauncherNavPage.Join;

    [RelayCommand]
    private void GoMods()
    {
        CurrentPage = LauncherNavPage.Mods;
        RefreshModsSnapshot();
    }

    [RelayCommand]
    private void GoLaunch() => CurrentPage = LauncherNavPage.Launch;

    [RelayCommand]
    private void RefreshInstall()
    {
        var install = _orch.Locate(string.IsNullOrWhiteSpace(InstallPath) ? null : InstallPath);
        if (install is null)
        {
            InstallSummary = "RimWorld not found — browse or type the install folder.";
            return;
        }

        InstallPath = install.InstallDir;
        InstallSummary = $"{install.Source} · {install.VersionHint ?? "1.7"} · {install.ExecutablePath}";
        StatusLine = $"Located RimWorld ({install.Source}).";
    }

    [RelayCommand]
    private void SetInstallPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        InstallPath = path.Trim();
        RefreshInstall();
    }

    [RelayCommand]
    private void RefreshModsSnapshot()
    {
        var snap = _orch.ShowMods();
        ActiveMods.Clear();
        ModRows.Clear();
        if (snap is null)
        {
            // Prefer catalog when ModsConfig missing but catalog knows packages
            var fromCatalog = _catalog.GetActivePackageIds();
            if (fromCatalog.Count > 0)
            {
                ApplyModList(fromCatalog, _catalog.ComputeActiveHash());
                ModsConfigPath = _orch.Mods.ResolveModsConfigPath() ?? "(catalog)";
                return;
            }

            ModsConfigPath = _orch.Mods.ResolveModsConfigPath() ?? "(not found)";
            ModlistHash = "—";
            ModsWarning = "ModsConfig.xml not found yet.";
            return;
        }

        ModsConfigPath = snap.Path;
        ApplyModList(snap.ActiveMods, _orch.Mods.ComputeHash(snap.ActiveMods));
    }

    [RelayCommand]
    private void PrepareMods()
    {
        try
        {
            var result = _orch.PrepareMods(profileName: "ui");
            ApplyModList(result.Ordered, result.Hash);
            ModsConfigPath = result.ConfigPath;
            ModsWarning = result.Warnings.Count == 0 ? "" : string.Join(" ", result.Warnings);
            StatusLine = $"Mods prepared · hash {ShortHash(result.Hash)}";
            LaunchLog = $"Prepared mods. Backup: {result.BackupPath}\nHash: {result.Hash}";
        }
        catch (Exception ex)
        {
            StatusLine = $"Mods prepare failed: {ex.Message}";
            LaunchLog = ex.Message;
        }
    }

    /// <summary>
    /// Prefill sync host from the last Join beacon / Join IP so Mods → Sync from host is one click.
    /// </summary>
    [RelayCommand]
    private void UseJoinTargetForSync()
    {
        if (!string.IsNullOrWhiteSpace(JoinIp))
            SyncHost = JoinIp.Trim();
        else if (SelectedBeacon?.Host is { Length: > 0 } h)
            SyncHost = h;
    }

    [RelayCommand]
    private async Task CompareWithHostAsync()
    {
        if (!_modSync.IsAvailable)
        {
            SyncStatusMessage = "Sync backend unavailable. Implement IModSyncClient and set ModSyncServices.Sync.";
            StatusLine = SyncStatusMessage;
            return;
        }

        if (string.IsNullOrWhiteSpace(SyncHost))
            UseJoinTargetForSync();
        if (string.IsNullOrWhiteSpace(SyncHost))
        {
            SyncStatusMessage = "Enter a host IP (or Join a beacon first).";
            return;
        }

        if (!int.TryParse(SyncPort, out var port) || port is < 1 or > 65535)
        {
            SyncStatusMessage = "Invalid sync port.";
            return;
        }

        try
        {
            IsSyncing = true;
            SyncProgressFraction = 0;
            SyncProgressLabel = "Comparing…";
            var plan = await _modSync.CompareAsync(new ModSyncHostTarget
            {
                Host = SyncHost.Trim(),
                Port = port,
                SessionName = SessionName,
                ExpectedModlistHash = SelectedBeacon?.ModlistHash,
            }).ConfigureAwait(true);

            _lastSyncPlan = plan;
            PopulateSyncDeltas(plan);
            ModlistHash = string.IsNullOrEmpty(plan.LocalHash) ? ModlistHash : plan.LocalHash;
            SyncStatusMessage = plan.HashesMatch
                ? $"Match · {ShortHash(plan.LocalHash)}"
                : $"Diff · local {ShortHash(plan.LocalHash)} vs host {ShortHash(plan.RemoteHash)} · missing {plan.Missing.Count}, mismatched {plan.Mismatched.Count}";
            StatusLine = SyncStatusMessage;
        }
        catch (Exception ex)
        {
            SyncStatusMessage = $"Compare failed: {ex.Message}";
            StatusLine = SyncStatusMessage;
        }
        finally
        {
            IsSyncing = false;
            SyncProgressLabel = "";
        }
    }

    [RelayCommand]
    private async Task SyncFromHostAsync()
    {
        if (!_modSync.IsAvailable)
        {
            SyncStatusMessage = "LAN mod sync not wired yet — placeholder only. See docs/launcher-mod-manager-hooks.md.";
            StatusLine = SyncStatusMessage;
            // Still exercise progress UI so layout can be validated
            SyncProgressFraction = 0;
            SyncProgressLabel = "Unavailable";
            return;
        }

        if (_lastSyncPlan is null)
        {
            await CompareWithHostAsync().ConfigureAwait(true);
            if (_lastSyncPlan is null)
                return;
        }

        _syncCts?.Cancel();
        _syncCts = new CancellationTokenSource();
        var progress = new Progress<ModSyncProgress>(p =>
        {
            SyncProgressFraction = Math.Clamp(p.Fraction, 0, 1);
            SyncProgressLabel = string.IsNullOrEmpty(p.CurrentPackageId)
                ? (p.Message ?? p.Phase)
                : $"{p.Phase}: {p.CurrentPackageId}";
        });

        try
        {
            IsSyncing = true;
            var result = await _modSync.SyncAsync(_lastSyncPlan, progress, _syncCts.Token).ConfigureAwait(true);
            if (result.Success)
            {
                SyncStatusMessage = $"Synced · hash {ShortHash(result.ResultingHash ?? "")}";
                RefreshModsSnapshot();
            }
            else
            {
                SyncStatusMessage = result.Error ?? "Sync failed.";
            }

            StatusLine = SyncStatusMessage;
        }
        catch (OperationCanceledException)
        {
            SyncStatusMessage = "Sync cancelled.";
        }
        catch (Exception ex)
        {
            SyncStatusMessage = $"Sync failed: {ex.Message}";
            StatusLine = SyncStatusMessage;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private void CancelSync()
    {
        _syncCts?.Cancel();
        IsSyncing = false;
        SyncProgressLabel = "Cancelled";
    }

    private void ApplyModList(IReadOnlyList<string> ordered, string hash)
    {
        ActiveMods.Clear();
        ModRows.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var id = ordered[i];
            ActiveMods.Add(id);
            ModRows.Add(new ModListRow
            {
                Index = i + 1,
                PackageId = id,
                IsHarmony = id.Equals(LauncherPaths.HarmonyPackageId, StringComparison.OrdinalIgnoreCase),
                IsRimworldMp = id.Equals(LauncherPaths.RimworldMpPackageId, StringComparison.OrdinalIgnoreCase),
            });
        }

        ModlistHash = hash;
        var warnings = SessionOrchestrator.CollectModWarnings(ordered);
        ModsWarning = warnings.Count == 0 ? "" : string.Join(" ", warnings);
    }

    private void PopulateSyncDeltas(ModSyncPlan plan)
    {
        SyncMissing.Clear();
        SyncMismatched.Clear();
        foreach (var d in plan.Missing)
            SyncMissing.Add(ModSyncDeltaRow.From(d, "missing"));
        foreach (var d in plan.Mismatched)
            SyncMismatched.Add(ModSyncDeltaRow.From(d, "mismatch"));
        HasSyncDeltas = SyncMissing.Count > 0 || SyncMismatched.Count > 0;
    }

    [RelayCommand]
    private void RestoreMods()
    {
        try
        {
            _orch.RestoreMods();
            RefreshModsSnapshot();
            StatusLine = "Restored pre-MP ModsConfig backup.";
            LaunchLog = "ModsConfig restored from pre-mp backup.";
        }
        catch (Exception ex)
        {
            StatusLine = $"Restore failed: {ex.Message}";
            LaunchLog = ex.Message;
        }
    }

    [RelayCommand]
    private async Task StartHostAsync()
    {
        if (IsHosting)
        {
            await StopHostAsync().ConfigureAwait(true);
            return;
        }

        try
        {
            if (!int.TryParse(HostPort, out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Invalid game port.");

            var options = new HostSessionOptions
            {
                SessionName = string.IsNullOrWhiteSpace(SessionName) ? "RimworldMP LAN" : SessionName.Trim(),
                Port = port,
                BindHost = string.IsNullOrWhiteSpace(BindHost) ? SessionOrchestrator.GetPrimaryLanIPv4() ?? "127.0.0.1" : BindHost.Trim(),
                InstallPath = InstallPath,
                PrepareMods = PrepareModsOnLaunch,
                LaunchGame = LaunchGame,
                MaxClients = MaxClients,
            };

            var (ordered, hash, warnings) = PrepLocal(options.PrepareMods, "host");
            var install = _orch.Locate(options.InstallPath);
            var rwBuild = install?.VersionHint ?? "1.7";
            var bindHost = options.BindHost!;

            var handoff = HandoffWriter.Create(
                role: "host",
                host: "0.0.0.0",
                port: options.Port,
                modlistHash: hash,
                sessionName: options.SessionName,
                rwBuild: rwBuild,
                maxClients: options.MaxClients);
            LastHandoffPath = HandoffWriter.Write(handoff);

            ActiveMods.Clear();
            foreach (var m in ordered)
                ActiveMods.Add(m);
            ModlistHash = hash;

            var beacon = _orch.CreateHostBeacon(options, hash, bindHost, rwBuild);
            _hostCts = new CancellationTokenSource();
            _hostDiscovery = await _orch.BeginHostBroadcastAsync(beacon, options.DiscoveryPort, _hostCts.Token)
                .ConfigureAwait(true);

            LaunchResult? launch = null;
            if (options.LaunchGame)
            {
                if (install is null)
                    throw new InvalidOperationException("RimWorld not found. Set install path on Launch.");
                launch = ProcessLauncher.Launch(install);
            }

            IsHosting = true;
            HostStatus = $"Live · {bindHost}:{port} · hash {ShortHash(hash)}";
            CurrentPage = LauncherNavPage.Launch;
            var warn = warnings.Count == 0 ? "" : "\n" + string.Join("\n", warnings);
            LaunchLog =
                $"Hosting “{options.SessionName}”\nAdvertise {bindHost}:{port}\nDiscovery UDP {options.DiscoveryPort}\nHandoff: {LastHandoffPath}\nHash: {hash}" +
                (launch is null ? "\n(Game not launched)" : $"\nLaunched pid={launch.ProcessId}") + warn;
            StatusLine = "Beacon live — stop from Host when done.";
        }
        catch (Exception ex)
        {
            await StopHostAsync().ConfigureAwait(true);
            StatusLine = $"Host failed: {ex.Message}";
            LaunchLog = ex.Message;
            CurrentPage = LauncherNavPage.Launch;
        }
    }

    [RelayCommand]
    private async Task StopHostAsync()
    {
        try { _hostCts?.Cancel(); } catch { /* ignore */ }
        if (_hostDiscovery is not null)
        {
            await _hostDiscovery.DisposeAsync().ConfigureAwait(true);
            _hostDiscovery = null;
        }

        _hostCts = null;
        IsHosting = false;
        HostStatus = "Idle";
        StatusLine = "Host beacon stopped.";
    }

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        IsDiscovering = true;
        DiscoveredHosts.Clear();
        SelectedBeacon = null;
        StatusLine = "Listening for LAN beacons…";
        try
        {
            var found = await SessionOrchestrator.DiscoverAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
            foreach (var b in found)
                DiscoveredHosts.Add(b);
            StatusLine = found.Count == 0
                ? "No hosts found — enter IP manually."
                : $"Found {found.Count} host(s).";
        }
        catch (Exception ex)
        {
            StatusLine = $"Discover failed: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    private void SelectBeacon(DiscoveryBeacon? beacon)
    {
        if (beacon is null)
            return;
        SelectedBeacon = beacon;
        JoinIp = beacon.Host ?? "";
        JoinPort = beacon.Port.ToString();
        SessionName = beacon.Name;
        if (!string.IsNullOrWhiteSpace(beacon.Host))
            SyncHost = beacon.Host;
    }

    [RelayCommand]
    private void JoinSelected()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(JoinIp))
                throw new InvalidOperationException("Enter a host IP or discover a LAN session.");
            if (!int.TryParse(JoinPort, out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Invalid join port.");

            var result = _orch.StartJoin(new JoinSessionOptions
            {
                Host = JoinIp.Trim(),
                Port = port,
                SessionName = string.IsNullOrWhiteSpace(SessionName) ? "RimworldMP LAN" : SessionName.Trim(),
                InstallPath = InstallPath,
                PrepareMods = PrepareModsOnLaunch,
                LaunchGame = LaunchGame,
                HostModlistHash = SelectedBeacon?.ModlistHash,
            });

            LastHandoffPath = result.HandoffPath;
            ModlistHash = result.ModlistHash;
            ActiveMods.Clear();
            foreach (var m in result.OrderedMods)
                ActiveMods.Add(m);

            CurrentPage = LauncherNavPage.Launch;
            var warn = result.Warnings.Count == 0 ? "" : "\n" + string.Join("\n", result.Warnings);
            LaunchLog =
                $"Joining {result.AdvertiseHost}:{port}\nHandoff: {result.HandoffPath}\nHash: {result.ModlistHash}" +
                (result.HashMatchesHost ? "" : "\n⚠ Hash mismatch with host beacon") +
                (result.Launch is null ? "\n(Game not launched)" : $"\nLaunched pid={result.Launch.ProcessId}") + warn;
            StatusLine = result.HashMatchesHost
                ? $"Join handoff written · {ShortHash(result.ModlistHash)}"
                : "Join handoff written — hash mismatch warning.";
        }
        catch (Exception ex)
        {
            StatusLine = $"Join failed: {ex.Message}";
            LaunchLog = ex.Message;
            CurrentPage = LauncherNavPage.Launch;
        }
    }

    private (IReadOnlyList<string> Ordered, string Hash, IReadOnlyList<string> Warnings) PrepLocal(bool prepare, string profile)
    {
        if (prepare)
        {
            var r = _orch.PrepareMods(profileName: profile);
            return (r.Ordered, r.Hash, r.Warnings);
        }

        var snap = _orch.ShowMods()
            ?? throw new InvalidOperationException("No ModsConfig.xml — prepare mods first.");
        return (snap.ActiveMods, _orch.Mods.ComputeHash(snap.ActiveMods),
            SessionOrchestrator.CollectModWarnings(snap.ActiveMods));
    }

    private static string ShortHash(string hash) =>
        hash.Length >= 8 ? hash[..8] : hash;
}

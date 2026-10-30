using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RimworldMP.Launcher.Models;
using RimworldMP.Launcher.Services;
using RimworldMP.Launcher.Services.ModSync;

namespace RimworldMP.Launcher.ViewModels;

public partial class MainViewModel : ObservableObject, IAsyncDisposable, IDisposable
{
    private readonly SessionOrchestrator _orch = new();
    private readonly IModCatalog _catalog;
    private readonly IModSyncClient _modSync;
    private readonly HostModShareService _hostModShare = new();
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
            ? "Ready to match mods with a friend on your network."
            : StubModSyncClient.UnavailableMessage;
        RefreshInstall();
        RefreshModsSnapshot();
        StatusLine = "Local only — nothing leaves your network.";
        HostStatus = "Ready when you are.";
        LaunchLog = "Ready to play.";
        ApplyInitialPageFromEnvironment();
    }

    /// <summary>Optional: RIMWORLDMP_LAUNCHER_PAGE=Home|Host|Join|Mods|Launch for screenshot/automation.</summary>
    private void ApplyInitialPageFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("RIMWORLDMP_LAUNCHER_PAGE");
        if (string.IsNullOrWhiteSpace(raw))
            return;
        if (Enum.TryParse<LauncherNavPage>(raw.Trim(), ignoreCase: true, out var page))
            CurrentPage = page;
    }

    [ObservableProperty] private LauncherNavPage _currentPage = LauncherNavPage.Home;

    [ObservableProperty] private string _statusLine = "";

    [ObservableProperty] private string? _installPath;

    [ObservableProperty] private string _installSummary = "RimWorld not found — set path on Launch.";

    [ObservableProperty] private string _sessionName = "RimworldMP LAN";

    [ObservableProperty] private string _playerDisplayName = "";

    [ObservableProperty] private string _hostPort = LauncherPaths.DefaultGamePort.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty] private string _bindHost = SessionOrchestrator.GetPrimaryLanIPv4() ?? "127.0.0.1";

    [ObservableProperty] private int _maxClients = 4;

    /// <summary>Handoff transport token shown in Host/Join (<c>tcp</c> or <c>litenetlib</c>).</summary>
    [ObservableProperty] private string _transport = "tcp";

    [ObservableProperty] private bool _prepareModsOnLaunch = true;

    [ObservableProperty] private bool _launchGame = true;

    [ObservableProperty] private bool _isHosting;

    [ObservableProperty] private string _hostStatus = "Ready when you are.";

    [ObservableProperty] private string _joinIp = "";

    [ObservableProperty] private string _joinPort = LauncherPaths.DefaultGamePort.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty] private bool _isDiscovering;

    [ObservableProperty] private DiscoveryBeacon? _selectedBeacon;

    [ObservableProperty] private string _modlistHash = "—";

    [ObservableProperty] private string _modsConfigPath = "—";

    [ObservableProperty] private string _modsWarning = "";

    [ObservableProperty] private string _launchLog = "Ready to play.";

    [ObservableProperty] private string _lastHandoffPath = LauncherPaths.HandoffPath;

    /// <summary>Shows port / transport / advertise IP / handoff paths.</summary>
    [ObservableProperty] private bool _showAdvanced;

    [ObservableProperty] private string _modSearchQuery = "";

    [ObservableProperty] private string _modsEmptyMessage = "";

    [ObservableProperty] private bool _modsConfigMissing;

    // --- LAN mod sync (IModSyncClient; default LanModSyncClient) ---
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
    public ObservableCollection<ModListRow> FilteredModRows { get; } = new();
    public ObservableCollection<DiscoveryBeacon> DiscoveredHosts { get; } = new();
    public ObservableCollection<ModSyncDeltaRow> SyncMissing { get; } = new();
    public ObservableCollection<ModSyncDeltaRow> SyncMismatched { get; } = new();

    /// <summary>Calm transport picker labels — tokens match handoff / NetTransportFactory.</summary>
    public IReadOnlyList<string> TransportChoices { get; } = ["tcp", "litenetlib"];

    public bool IsHome => CurrentPage == LauncherNavPage.Home;
    public bool IsHost => CurrentPage == LauncherNavPage.Host;
    public bool IsJoin => CurrentPage == LauncherNavPage.Join;
    public bool IsMods => CurrentPage == LauncherNavPage.Mods;
    public bool IsLaunch => CurrentPage == LauncherNavPage.Launch;
    public bool HasFilteredMods => FilteredModRows.Count > 0;
    public bool ShowModsEmpty => !HasFilteredMods;

    partial void OnCurrentPageChanged(LauncherNavPage value)
    {
        OnPropertyChanged(nameof(IsHome));
        OnPropertyChanged(nameof(IsHost));
        OnPropertyChanged(nameof(IsJoin));
        OnPropertyChanged(nameof(IsMods));
        OnPropertyChanged(nameof(IsLaunch));
    }

    partial void OnModSearchQueryChanged(string value) => RebuildFilteredMods();

    [RelayCommand]
    private void ToggleAdvanced() => ShowAdvanced = !ShowAdvanced;

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
            InstallSummary = "RimWorld not found — set the install folder below.";
            return;
        }

        InstallPath = install.InstallDir;
        InstallSummary = $"Found RimWorld ({install.Source}, {install.VersionHint ?? "1.7"}).";
        StatusLine = "RimWorld found — you can host or join.";
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
                ModsConfigMissing = false;
                ApplyModList(fromCatalog, _catalog.ComputeActiveHash());
                ModsConfigPath = _orch.Mods.ResolveModsConfigPath() ?? "(catalog)";
                return;
            }

            ModsConfigPath = _orch.Mods.ResolveModsConfigPath() ?? "(not found)";
            ModlistHash = "—";
            ModsConfigMissing = true;
            ModsWarning = "";
            RebuildFilteredMods();
            return;
        }

        ModsConfigMissing = false;
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
            StatusLine = "Mods lined up for multiplayer.";
            LaunchLog = "Mods are ready for friends to match. A backup of your old list was saved.";
        }
        catch (Exception ex)
        {
            StatusLine = $"Could not fix mods: {ex.Message}";
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

        if (SelectedBeacon?.ModSyncPort is int msp && msp is >= 1 and <= 65535)
            SyncPort = msp.ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private async Task CompareWithHostAsync()
    {
        if (!_modSync.IsAvailable)
        {
            SyncStatusMessage = StubModSyncClient.UnavailableMessage;
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
            var warn = plan.Warnings.Count > 0 ? $" · note: {plan.Warnings[0]}" : "";
            SyncStatusMessage = plan.HashesMatch
                ? $"Match · {ShortHash(plan.LocalHash)}{warn}"
                : $"Diff · local {ShortHash(plan.LocalHash)} vs host {ShortHash(plan.RemoteHash)} · missing {plan.Missing.Count}, mismatched {plan.Mismatched.Count}{warn}";
            StatusLine = SyncStatusMessage;
            if (plan.Warnings.Count > 1)
                LaunchLog = string.Join("\n", plan.Warnings);
        }
        catch (Exception ex)
        {
            _lastSyncPlan = null;
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
            SyncStatusMessage = StubModSyncClient.UnavailableMessage;
            StatusLine = SyncStatusMessage;
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
        ModsConfigMissing = false;
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
        RebuildFilteredMods();
    }

    /// <summary>Design-capture helper: seed rows then refresh the filtered list.</summary>
    internal void SeedModRowsForCapture(IEnumerable<ModListRow> rows)
    {
        ModRows.Clear();
        foreach (var row in rows)
            ModRows.Add(row);
        ModsConfigMissing = false;
        RebuildFilteredMods();
    }

    private void RebuildFilteredMods()
    {
        FilteredModRows.Clear();
        var q = ModSearchQuery.Trim();
        IEnumerable<ModListRow> source = ModRows;
        if (q.Length > 0)
        {
            source = ModRows.Where(r =>
                r.PackageId.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Badge.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var row in source)
            FilteredModRows.Add(row);

        if (ModsConfigMissing && ModRows.Count == 0)
            ModsEmptyMessage = "ModsConfig not found — set install on Launch.";
        else if (ModRows.Count == 0)
            ModsEmptyMessage = "No mods listed yet — try Fix my mods, or set install on Launch.";
        else if (FilteredModRows.Count == 0)
            ModsEmptyMessage = $"No mods match “{q}”.";
        else
            ModsEmptyMessage = "";

        OnPropertyChanged(nameof(HasFilteredMods));
        OnPropertyChanged(nameof(ShowModsEmpty));
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
            StatusLine = "Restored your previous mod list.";
            LaunchLog = "Your pre-multiplayer mod list is back.";
        }
        catch (Exception ex)
        {
            StatusLine = $"Couldn’t restore mods: {ex.Message}";
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
                ShareMods = true,
                ModSyncPort = int.TryParse(SyncPort, out var msp) && msp is >= 1 and <= 65535
                    ? msp
                    : LauncherPaths.DefaultModSyncPort,
                Transport = Transport,
                PlayerDisplayName = PlayerDisplayName,
            };

            StatusLine = "Preparing host…";
            // PrepLocal / locate can touch disk; keep Avalonia responsive.
            var prep = await Task.Run(() =>
            {
                var mods = PrepLocal(options.PrepareMods, "host");
                var inst = _orch.Locate(options.InstallPath);
                return (mods.Ordered, mods.Hash, mods.Warnings, Install: inst);
            }).ConfigureAwait(true);

            var ordered = prep.Ordered;
            var hash = prep.Hash;
            var warnings = prep.Warnings;
            var install = prep.Install;
            var rwBuild = install?.VersionHint ?? "1.7";
            var bindHost = options.BindHost!;

            var handoff = HandoffWriter.Create(
                role: "host",
                host: "0.0.0.0",
                port: options.Port,
                modlistHash: hash,
                sessionName: options.SessionName,
                rwBuild: rwBuild,
                maxClients: options.MaxClients,
                transport: options.Transport,
                playerDisplayName: options.PlayerDisplayName);
            LastHandoffPath = HandoffWriter.Write(handoff);

            ActiveMods.Clear();
            foreach (var m in ordered)
                ActiveMods.Add(m);
            ModlistHash = hash;

            _hostCts = new CancellationTokenSource();
            var modShareNote = "";
            if (options.ShareMods)
            {
                if (install is null)
                    throw new InvalidOperationException("RimWorld not found — required to share mods over LAN.");
                StatusLine = "Sharing mods on LAN…";
                await _hostModShare.StartAsync(
                        ordered,
                        install.InstallDir,
                        rwBuild,
                        options.ModSyncPort,
                        cancellationToken: _hostCts.Token)
                    .ConfigureAwait(true);
                options = new HostSessionOptions
                {
                    SessionName = options.SessionName,
                    Port = options.Port,
                    DiscoveryPort = options.DiscoveryPort,
                    InstallPath = options.InstallPath,
                    ConfigPath = options.ConfigPath,
                    BindHost = options.BindHost,
                    PrepareMods = options.PrepareMods,
                    LaunchGame = options.LaunchGame,
                    MaxClients = options.MaxClients,
                    ShareMods = true,
                    ModSyncPort = _hostModShare.Port,
                    Transport = options.Transport,
                    PlayerDisplayName = options.PlayerDisplayName,
                };
                SyncPort = _hostModShare.Port.ToString(CultureInfo.InvariantCulture);
                modShareNote = $"\nMod sync TCP {bindHost}:{_hostModShare.Port} ({_hostModShare.Manifest?.Mods.Count ?? 0} packages)";
            }

            var beacon = _orch.CreateHostBeacon(options, hash, bindHost, rwBuild, _hostModShare.IsRunning ? _hostModShare.Port : null);
            _hostDiscovery = await _orch.BeginHostBroadcastAsync(beacon, options.DiscoveryPort, _hostCts.Token)
                .ConfigureAwait(true);

            LaunchResult? launch = null;
            if (options.LaunchGame)
            {
                if (install is null)
                    throw new InvalidOperationException("RimWorld not found. Set install path on Launch.");
                StatusLine = "Starting RimWorld…";
                launch = await Task.Run(() => ProcessLauncher.Launch(install)).ConfigureAwait(true);
            }

            IsHosting = true;
            HostStatus = "Friends can find you on Wi‑Fi.";
            CurrentPage = LauncherNavPage.Launch;
            var warn = warnings.Count == 0 ? "" : "\n" + string.Join("\n", warnings);
            LaunchLog =
                $"You’re hosting “{options.SessionName}”.\nFriends can join at {bindHost}.\n" +
                (launch is null ? "Start RimWorld when you’re ready." : "RimWorld is starting…") +
                warn;
            if (ShowAdvanced)
            {
                LaunchLog +=
                    $"\n\nAdvanced: port {port}, discovery {options.DiscoveryPort}, handoff {LastHandoffPath}, list {ShortHash(hash)}" +
                    modShareNote;
            }

            StatusLine = "You’re hosting — friends can find you on Wi‑Fi.";
        }
        catch (Exception ex)
        {
            await StopHostAsync().ConfigureAwait(true);
            StatusLine = $"Couldn’t start hosting: {ex.Message}";
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

        await _hostModShare.StopAsync().ConfigureAwait(true);

        _hostCts = null;
        IsHosting = false;
        HostStatus = "Ready when you are.";
        StatusLine = "Stopped inviting friends.";
    }

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        IsDiscovering = true;
        DiscoveredHosts.Clear();
        SelectedBeacon = null;
        StatusLine = "Looking for friends on Wi‑Fi…";
        try
        {
            var found = await SessionOrchestrator.DiscoverAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
            foreach (var b in found)
                DiscoveredHosts.Add(b);
            StatusLine = found.Count == 0
                ? "Nobody found yet — type a friend’s IP below."
                : $"Found {found.Count} friend{(found.Count == 1 ? "" : "s")} nearby.";
        }
        catch (Exception ex)
        {
            StatusLine = $"Couldn’t search Wi‑Fi: {ex.Message}";
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
        JoinPort = beacon.Port.ToString(CultureInfo.InvariantCulture);
        SessionName = beacon.Name;
        if (!string.IsNullOrWhiteSpace(beacon.Host))
            SyncHost = beacon.Host;
    }

    [RelayCommand]
    private async Task JoinSelectedAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(JoinIp))
                throw new InvalidOperationException("Pick a friend from the list or type their IP.");
            if (!int.TryParse(JoinPort, out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("That port number doesn’t look right.");

            StatusLine = "Preparing to join…";
            var joinIp = JoinIp.Trim();
            var sessionName = string.IsNullOrWhiteSpace(SessionName) ? "RimworldMP LAN" : SessionName.Trim();
            var installPath = InstallPath;
            var prepare = PrepareModsOnLaunch;
            var launchGame = LaunchGame;
            var hostHash = SelectedBeacon?.ModlistHash;
            var transport = Transport;
            var displayName = PlayerDisplayName;

            var result = await Task.Run(() => _orch.StartJoin(new JoinSessionOptions
            {
                Host = joinIp,
                Port = port,
                SessionName = sessionName,
                InstallPath = installPath,
                PrepareMods = prepare,
                LaunchGame = launchGame,
                HostModlistHash = hostHash,
                Transport = transport,
                PlayerDisplayName = displayName,
            })).ConfigureAwait(true);

            LastHandoffPath = result.HandoffPath;
            ModlistHash = result.ModlistHash;
            ActiveMods.Clear();
            foreach (var m in result.OrderedMods)
                ActiveMods.Add(m);

            CurrentPage = LauncherNavPage.Launch;
            var warn = result.Warnings.Count == 0 ? "" : "\n" + string.Join("\n", result.Warnings);
            LaunchLog =
                $"Joining your friend at {result.AdvertiseHost}.\n" +
                (result.HashMatchesHost ? "Mod lists look aligned." : "Mod lists may not match — try Fix my mods.") +
                (result.Launch is null ? "\nStart RimWorld when you’re ready." : "\nRimWorld is starting…") +
                warn;
            if (ShowAdvanced)
                LaunchLog += $"\n\nAdvanced: port {port}, handoff {result.HandoffPath}, list {ShortHash(result.ModlistHash)}";

            StatusLine = result.HashMatchesHost
                ? "Ready to join — RimWorld should pick it up."
                : "Joined with a mod-list warning — check Mods.";
        }
        catch (Exception ex)
        {
            StatusLine = $"Couldn’t join: {ex.Message}";
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

    public void Dispose()
    {
        DisposeAsyncCore().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        var task = DisposeAsyncCore();
        GC.SuppressFinalize(this);
        return new ValueTask(task);
    }

    private async Task DisposeAsyncCore()
    {
        try { _hostCts?.Cancel(); } catch { /* ignore */ }
        try { _syncCts?.Cancel(); } catch { /* ignore */ }
        _hostCts?.Dispose();
        _syncCts?.Dispose();
        _hostCts = null;
        _syncCts = null;
        if (_hostDiscovery is not null)
        {
            await _hostDiscovery.DisposeAsync().ConfigureAwait(false);
            _hostDiscovery = null;
        }

        await _hostModShare.DisposeAsync().ConfigureAwait(false);
    }
}

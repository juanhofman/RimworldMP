using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using RimworldMP.Launcher.Models;

namespace RimworldMP.Launcher.Services;

public sealed class PrepareModsResult
{
    public required IReadOnlyList<string> Ordered { get; init; }
    public required string Hash { get; init; }
    public required string BackupPath { get; init; }
    public required string ConfigPath { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class HostSessionOptions
{
    public string SessionName { get; init; } = "RimworldMP LAN";
    public int Port { get; init; } = LauncherPaths.DefaultGamePort;
    public int DiscoveryPort { get; init; } = LauncherPaths.DefaultDiscoveryPort;
    public string? InstallPath { get; init; }
    public string? ConfigPath { get; init; }
    public string? BindHost { get; init; }
    public bool PrepareMods { get; init; } = true;
    public bool LaunchGame { get; init; } = true;
    public int MaxClients { get; init; } = 4;
    /// <summary>Serve active mods over LAN TCP (default port 30557) for client sync.</summary>
    public bool ShareMods { get; init; }
    public int ModSyncPort { get; init; } = LauncherPaths.DefaultModSyncPort;
    /// <summary>Handoff transport token: <c>tcp</c> or <c>litenetlib</c> (null = omit / mod default).</summary>
    public string? Transport { get; init; }
    public string? PlayerDisplayName { get; init; }
}

public sealed class JoinSessionOptions
{
    public string SessionName { get; init; } = "RimworldMP LAN";
    public string Host { get; init; } = "";
    public int Port { get; init; } = LauncherPaths.DefaultGamePort;
    public string? InstallPath { get; init; }
    public string? ConfigPath { get; init; }
    public bool PrepareMods { get; init; } = true;
    public bool LaunchGame { get; init; } = true;
    public string? HostModlistHash { get; init; }
    public string? Transport { get; init; }
    public string? PlayerDisplayName { get; init; }
}

public sealed class SessionStartResult
{
    public required HandoffDocument Handoff { get; init; }
    public required string HandoffPath { get; init; }
    public required string ModlistHash { get; init; }
    public required IReadOnlyList<string> OrderedMods { get; init; }
    public RimWorldInstall? Install { get; init; }
    public LaunchResult? Launch { get; init; }
    public string? AdvertiseHost { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public bool HashMatchesHost { get; init; } = true;
}

/// <summary>Shared host/join/mods orchestration for CLI and Avalonia UI.</summary>
public sealed class SessionOrchestrator
{
    private readonly ModsConfigService _mods = new();

    public ModsConfigService Mods => _mods;

    public RimWorldInstall? Locate(string? path = null) => RimWorldLocator.TryLocate(path);

    public PrepareModsResult PrepareMods(string? configPath = null, string profileName = "default")
    {
        var (ordered, hash, backup) = _mods.PrepareForMp(configPath, profileName);
        var resolved = _mods.ResolveModsConfigPath(configPath) ?? "(unknown)";
        return new PrepareModsResult
        {
            Ordered = ordered,
            Hash = hash,
            BackupPath = backup,
            ConfigPath = resolved,
            Warnings = CollectModWarnings(ordered),
        };
    }

    public ModsConfigSnapshot? ShowMods(string? configPath = null)
    {
        var path = configPath ?? _mods.ResolveModsConfigPath();
        if (path is null || !File.Exists(path))
            return null;
        return _mods.Read(path);
    }

    public void RestoreMods(string? configPath = null)
    {
        var path = configPath ?? _mods.ResolveModsConfigPath()
            ?? throw new InvalidOperationException("ModsConfig path unknown.");
        _mods.RestoreLatestPreMp(path);
    }

    public async Task<SessionStartResult> StartHostAsync(
        HostSessionOptions options,
        Func<DiscoveryBeacon, CancellationToken, Task>? runBeacon = null,
        CancellationToken cancellationToken = default)
    {
        var (ordered, hash, warnings) = ResolveMods(options.PrepareMods, options.ConfigPath, "host");
        var install = RimWorldLocator.TryLocate(options.InstallPath);
        var rwBuild = install?.VersionHint ?? "1.7";
        var bindHost = options.BindHost ?? GetPrimaryLanIPv4() ?? "127.0.0.1";

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
        var handoffPath = HandoffWriter.Write(handoff);

        var beacon = CreateHostBeacon(options, hash, bindHost, rwBuild);

        LaunchResult? launch = null;
        if (options.LaunchGame)
        {
            if (install is null)
                throw new InvalidOperationException("RimWorld not found. Set install path or run locate first.");
            launch = ProcessLauncher.Launch(install);
        }

        if (runBeacon is not null)
            await runBeacon(beacon, cancellationToken).ConfigureAwait(false);

        return new SessionStartResult
        {
            Handoff = handoff,
            HandoffPath = handoffPath,
            ModlistHash = hash,
            OrderedMods = ordered,
            Install = install,
            Launch = launch,
            AdvertiseHost = bindHost,
            Warnings = warnings,
        };
    }

    public async Task<LanDiscovery> BeginHostBroadcastAsync(
        DiscoveryBeacon beacon,
        int discoveryPort,
        CancellationToken cancellationToken = default)
    {
        var disco = new LanDiscovery(discoveryPort);
        await disco.StartBroadcastAsync(beacon, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        return disco;
    }

    public DiscoveryBeacon CreateHostBeacon(
        HostSessionOptions options,
        string hash,
        string bindHost,
        string rwBuild,
        int? modSyncPort = null) =>
        new()
        {
            Name = options.SessionName,
            Port = options.Port,
            RwBuild = rwBuild,
            ModlistHash = hash,
            ModHashShort = hash.Length >= 8 ? hash[..8] : hash,
            Host = bindHost,
            ModSyncPort = modSyncPort ?? (options.ShareMods ? options.ModSyncPort : null),
        };

    public SessionStartResult StartJoin(JoinSessionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host IP/hostname is required.", nameof(options));

        var (ordered, hash, warnings) = ResolveMods(options.PrepareMods, options.ConfigPath, "join");
        var install = RimWorldLocator.TryLocate(options.InstallPath);
        var rwBuild = install?.VersionHint ?? "1.7";

        var hashMatches = string.IsNullOrWhiteSpace(options.HostModlistHash)
            || ModlistHasher.Matches(hash, options.HostModlistHash);

        var warningList = warnings.ToList();
        if (!hashMatches)
            warningList.Add("Local modlist hash does not match host beacon — join may be rejected.");

        var handoff = HandoffWriter.Create(
            role: "client",
            host: options.Host.Trim(),
            port: options.Port,
            modlistHash: hash,
            sessionName: options.SessionName,
            rwBuild: rwBuild,
            transport: options.Transport,
            playerDisplayName: options.PlayerDisplayName);
        var handoffPath = HandoffWriter.Write(handoff);

        LaunchResult? launch = null;
        if (options.LaunchGame)
        {
            if (install is null)
                throw new InvalidOperationException("RimWorld not found. Set install path or run locate first.");
            launch = ProcessLauncher.Launch(install);
        }

        return new SessionStartResult
        {
            Handoff = handoff,
            HandoffPath = handoffPath,
            ModlistHash = hash,
            OrderedMods = ordered,
            Install = install,
            Launch = launch,
            AdvertiseHost = options.Host,
            Warnings = warningList,
            HashMatchesHost = hashMatches,
        };
    }

    public static Task<IReadOnlyList<DiscoveryBeacon>> DiscoverAsync(
        TimeSpan duration,
        int discoveryPort = LauncherPaths.DefaultDiscoveryPort,
        CancellationToken cancellationToken = default) =>
        LanDiscovery.DiscoverOnceAsync(duration, discoveryPort, cancellationToken);

    public static string? GetPrimaryLanIPv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;
                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address))
                        return addr.Address.ToString();
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public static IReadOnlyList<string> CollectModWarnings(IReadOnlyList<string> ordered)
    {
        var list = new List<string>();
        if (ordered.Count == 0 || !ordered[0].Equals(LauncherPaths.HarmonyPackageId, StringComparison.OrdinalIgnoreCase))
            list.Add("Harmony (brrainz.harmony) is not first — install Workshop Harmony 3.");
        if (!ordered.Any(p => p.Equals(LauncherPaths.RimworldMpPackageId, StringComparison.OrdinalIgnoreCase)))
            list.Add("RimworldMP.Core missing from activeMods — copy mod/ into Mods.");
        return list;
    }

    private (IReadOnlyList<string> Ordered, string Hash, IReadOnlyList<string> Warnings) ResolveMods(
        bool prepare,
        string? configPath,
        string profile)
    {
        if (prepare)
        {
            var prepared = PrepareMods(configPath, profile);
            return (prepared.Ordered, prepared.Hash, prepared.Warnings);
        }

        var cfg = _mods.ResolveModsConfigPath(configPath);
        if (cfg is null || !File.Exists(cfg))
            throw new InvalidOperationException("No ModsConfig.xml; prepare mods first or enable Prepare mods.");
        var ordered = _mods.Read(cfg).ActiveMods;
        var hash = _mods.ComputeHash(ordered);
        return (ordered, hash, CollectModWarnings(ordered));
    }
}

using System.Globalization;
using System.Net;
using RimworldMP.Launcher.Models;
using RimworldMP.Launcher.Services;
using RimworldMP.Launcher.Services.ModSync;
using RimworldMP.ModSync;

namespace RimworldMP.Launcher.Cli;

/// <summary>Console fallback (`--cli` and subcommands).</summary>
public static class CliApp
{
    private static readonly string[] InteractiveLocate = ["locate"];
    private static readonly string[] InteractiveModsPrepare = ["mods", "prepare"];
    private static readonly string[] InteractiveHash = ["hash"];
    private static readonly string[] InteractiveHost = ["host", "--share-mods"];
    private static readonly string[] InteractiveJoin = ["join"];
    private static readonly string[] InteractiveModsRestore = ["mods", "restore"];
    private static readonly string[] InteractiveModsList = ["mods", "list"];
    private static readonly string[] InteractiveModsSync = ["mods", "sync"];

    public static async Task<int> RunAsync(string[] args)
    {
        ModlistHasher.AssertGoldenMatchesCoreExpectation();

        if (args.Length == 0)
            return await RunInteractiveAsync().ConfigureAwait(false);

        return await DispatchAsync(args).ConfigureAwait(false);
    }

    public static async Task<int> DispatchAsync(string[] args)
    {
        var cmd = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();
        try
        {
            return cmd switch
            {
                "help" or "-h" or "--help" => PrintHelp(),
                "locate" => CmdLocate(rest),
                "mods" => await CmdMods(rest).ConfigureAwait(false),
                "hash" => CmdHash(rest),
                "discover" => await CmdDiscoverAsync(rest).ConfigureAwait(false),
                "host" => await CmdHostAsync(rest).ConfigureAwait(false),
                "join" => await CmdJoinAsync(rest).ConfigureAwait(false),
                "handoff-path" => CmdHandoffPath(),
                "self-test" => CmdSelfTest(),
                _ => Fail($"Unknown command '{args[0]}'. Try: help"),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> RunInteractiveAsync()
    {
        Console.WriteLine("RimworldMP LAN Launcher (CLI)");
        Console.WriteLine("Local-only — no uploads. Type a number:");
        Console.WriteLine("  1) Locate RimWorld");
        Console.WriteLine("  2) Prepare mods (Harmony first + RimworldMP)");
        Console.WriteLine("  3) Show modlist hash");
        Console.WriteLine("  4) Host (beacon + share mods + launch)");
        Console.WriteLine("  5) Join (discover / manual IP)");
        Console.WriteLine("  6) Restore ModsConfig backup");
        Console.WriteLine("  7) List installed mods");
        Console.WriteLine("  8) Sync mods from host (LAN)");
        Console.WriteLine("  9) Help");
        Console.WriteLine("  0) Exit");
        Console.Write("> ");
        var line = Console.ReadLine()?.Trim();
        return line switch
        {
            "1" => await DispatchAsync(InteractiveLocate).ConfigureAwait(false),
            "2" => await DispatchAsync(InteractiveModsPrepare).ConfigureAwait(false),
            "3" => await DispatchAsync(InteractiveHash).ConfigureAwait(false),
            "4" => await DispatchAsync(InteractiveHost).ConfigureAwait(false),
            "5" => await DispatchAsync(InteractiveJoin).ConfigureAwait(false),
            "6" => await DispatchAsync(InteractiveModsRestore).ConfigureAwait(false),
            "7" => await DispatchAsync(InteractiveModsList).ConfigureAwait(false),
            "8" => await DispatchAsync(InteractiveModsSync).ConfigureAwait(false),
            "9" => PrintHelp(),
            "0" or null or "" => 0,
            _ => Fail("Unknown choice."),
        };
    }

    static int PrintHelp()
    {
        Console.WriteLine("""
            RimworldMP.Launcher — LAN setup helper (local-only)

            Usage:
              dotnet run --project tools/RimworldMP.Launcher              # Avalonia UI
              dotnet run --project tools/RimworldMP.Launcher -- --cli     # interactive CLI
              dotnet run --project tools/RimworldMP.Launcher -- <command> [options]

            Commands:
              locate [--path <dir>]          Find RimWorld 1.7 (Steam heuristics or manual)
              mods prepare [--profile name]  Backup ModsConfig, enforce Harmony→…→RimworldMP order
              mods restore                   Restore last pre-MP ModsConfig backup
              mods show                      Print active ordered packageIds + hash
              mods list [--path <rw-dir>]    Scan installed Mods + Workshop cache
              mods sync --host <ip> [opts]   Pull exact mods/versions from host over LAN
              hash                           Same as mods show (hash focus)
              discover [--seconds N]         Listen for LAN host beacons (UDP 30556)
              host [options]                 Beacon + write handoff + launch RimWorld
              join [options]                 Discover or manual IP, handoff + launch
              handoff-path                   Print launcher-handoff.json path
              self-test                      Verify ModlistHasher mirrors Core algorithm
              help

            Host options:
              --name <session>   --port <30555>  --discovery-port <30556>
              --path <rw-dir>    --config <ModsConfig.xml>
              --no-launch        --no-mods
              --bind-host <ip>   (default: primary LAN IPv4)
              --max-clients <n>  (default 4; written to handoff)
              --share-mods       Serve active mod files over TCP (default 30557)
              --mod-sync-port N  Override mod-sync listen port

            Join / mods sync options:
              --ip / --host <ip> --port <30555>  --seconds <3>
              --mod-sync-port N  (default 30557, or from beacon)
              --path <rw-dir>    --config <ModsConfig.xml>
              --no-launch        --no-mods
              --name <session>

            Mod sync notes:
              Pulls exact host mods over LAN (TCP 30557). Local Steam Workshop cache is a
              secondary hash-match source. SteamCMD / live Workshop download is NOT supported.

            Docs: docs/launcher-mvp.md · docs/launcher-handoff.md · docs/mod-manager-lan-sync.md
            Handoff: see `handoff-path` (Windows LocalAppData / Linux ~/.local/share/RimworldMP/)
            """);
        return 0;
    }

    static int CmdHandoffPath()
    {
        Console.WriteLine(LauncherPaths.HandoffPath);
        return 0;
    }

    static int CmdSelfTest()
    {
        ModlistHasher.AssertGoldenMatchesCoreExpectation();
        var golden = ModlistHasher.GoldenHarmonyRimworldMpHash;
        Console.WriteLine("ok: ModlistHasher self-check passed");
        Console.WriteLine($"golden(brrainz.harmony\\nRimworldMP.Core) = {golden}");
        return 0;
    }

    static int CmdLocate(string[] args)
    {
        var path = GetOpt(args, "--path");
        var install = RimWorldLocator.TryLocate(path);
        if (install is null)
        {
            Console.WriteLine("RimWorld not found.");
            Console.WriteLine("Pass --path <install-dir> (folder containing RimWorldWin64.exe / RimWorldLinux).");
            return 2;
        }

        Console.WriteLine($"source:     {install.Source}");
        Console.WriteLine($"install:    {install.InstallDir}");
        Console.WriteLine($"executable: {install.ExecutablePath}");
        Console.WriteLine($"version:    {install.VersionHint ?? "(unknown)"}");
        return 0;
    }

    static async Task<int> CmdMods(string[] args)
    {
        if (args.Length == 0)
            return Fail("mods requires: prepare | restore | show | list | sync");

        var sub = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();
        var orch = new SessionOrchestrator();
        var configPath = GetOpt(rest, "--config");

        return sub switch
        {
            "prepare" => CmdModsPrepare(orch, rest, configPath),
            "restore" => CmdModsRestore(orch, configPath),
            "show" => CmdModsShow(orch, configPath),
            "list" => CmdModsList(rest),
            "sync" => await CmdModsSyncAsync(rest).ConfigureAwait(false),
            _ => Fail($"Unknown mods subcommand '{sub}'."),
        };
    }

    static int CmdModsList(string[] args)
    {
        var path = GetOpt(args, "--path");
        var install = RimWorldLocator.TryLocate(path);
        var installDir = install?.InstallDir ?? path;
        if (string.IsNullOrWhiteSpace(installDir))
            return Fail("RimWorld not found. Pass --path <install-dir>.");

        var installed = ModInventory.ScanInstalled(installDir);
        Console.WriteLine($"install: {installDir}");
        Console.WriteLine($"mods root: {Path.Combine(installDir, "Mods")}");
        Console.WriteLine($"found: {installed.Count}");
        Console.WriteLine("order emphasis: Harmony first, then RimworldMP.Core.");
        var i = 1;
        foreach (var m in installed)
        {
            var ws = m.WorkshopId ?? "-";
            var hash = m.ContentHash.Length >= 8 ? m.ContentHash[..8] : m.ContentHash;
            Console.WriteLine($"  {i++,3}. {m.PackageId}  v={m.Version}  workshop={ws}  hash={hash}  {m.Name}");
            Console.WriteLine($"       path={m.LocalPath}");
        }

        return 0;
    }

    static async Task<int> CmdModsSyncAsync(string[] args)
    {
        var host = GetOpt(args, "--host") ?? GetOpt(args, "--ip");
        var modSyncPort = GetOptInt(args, "--mod-sync-port", LauncherPaths.DefaultModSyncPort);
        var discoPort = GetOptInt(args, "--discovery-port", LauncherPaths.DefaultDiscoveryPort);
        var seconds = GetOptInt(args, "--seconds", 3);
        var path = GetOpt(args, "--path");

        if (string.IsNullOrWhiteSpace(host))
        {
            Console.WriteLine($"No --host; discovering for {seconds}s…");
            var beacons = await SessionOrchestrator.DiscoverAsync(TimeSpan.FromSeconds(seconds), discoPort)
                .ConfigureAwait(false);
            if (beacons.Count == 0)
                return Fail("No LAN hosts found. Re-run with --host <ip>.");

            for (var i = 0; i < beacons.Count; i++)
            {
                var b = beacons[i];
                var msp = b.ModSyncPort ?? LauncherPaths.DefaultModSyncPort;
                Console.WriteLine($"  [{i + 1}] {b.Name} {b.Host}:{b.Port} modSync={msp} hash={b.ModHashShort}…");
            }

            Console.Write("Select host number (or Enter for 1): ");
            var choice = Console.ReadLine()?.Trim();
            var idx = 0;
            if (!string.IsNullOrEmpty(choice) && (!int.TryParse(choice, out var n) || n < 1 || n > beacons.Count))
                return Fail("Invalid selection.");
            if (!string.IsNullOrEmpty(choice))
                idx = int.Parse(choice, CultureInfo.InvariantCulture) - 1;

            var selected = beacons[idx];
            host = selected.Host;
            if (selected.ModSyncPort is int advertised)
                modSyncPort = advertised;
            Console.WriteLine($"Selected {selected.Name} at {host} mod-sync :{modSyncPort}");
        }

        var install = RimWorldLocator.TryLocate(path);
        var installDir = install?.InstallDir ?? path;
        if (string.IsNullOrWhiteSpace(installDir))
            return Fail("RimWorld not found. Pass --path <install-dir> before mods sync.");

        // Honour --path: do not silently fall back to a different Steam install.
        var syncClient = new LanModSyncClient(resolveInstallDir: () => installDir);
        var plan = await syncClient.CompareAsync(new ModSyncHostTarget
        {
            Host = host!,
            Port = modSyncPort,
        }).ConfigureAwait(false);

        Console.WriteLine($"install={installDir}");
        Console.WriteLine($"local={plan.LocalHash}");
        Console.WriteLine($"host ={plan.RemoteHash}");
        Console.WriteLine($"missing={plan.Missing.Count} mismatched={plan.Mismatched.Count} extraLocal={plan.ExtraLocal.Count}");
        foreach (var w in plan.Warnings)
            Console.Error.WriteLine($"warning: {w}");

        if (HasFlag(args, "--no-mods"))
        {
            Console.WriteLine("compare-only (--no-mods): skipping LAN file pull / ModsConfig write.");
            return plan.HashesMatch ? 0 : 2;
        }

        var progress = new Progress<ModSyncProgress>(p =>
            Console.WriteLine($"  [{p.Fraction:P0}] {p.Message ?? p.Phase}"));
        var result = await syncClient.SyncAsync(plan, progress).ConfigureAwait(false);
        if (!result.Success)
            return Fail(result.Error ?? "Sync failed.");

        Console.WriteLine($"ok hash={result.ResultingHash}");
        return 0;
    }

    static int CmdModsPrepare(SessionOrchestrator orch, string[] args, string? configPath)
    {
        var profile = GetOpt(args, "--profile") ?? "default";
        var result = orch.PrepareMods(configPath, profile);
        Console.WriteLine($"ModsConfig: {result.ConfigPath}");
        Console.WriteLine($"backup:     {result.BackupPath}");
        Console.WriteLine($"profile:    {profile}");
        Console.WriteLine("activeMods (enforced order):");
        for (var i = 0; i < result.Ordered.Count; i++)
            Console.WriteLine($"  {i + 1,3}. {result.Ordered[i]}");
        Console.WriteLine($"modlistHash: {result.Hash}");
        foreach (var w in result.Warnings)
            Console.Error.WriteLine($"warning: {w}");
        return 0;
    }

    static int CmdModsRestore(SessionOrchestrator orch, string? configPath)
    {
        orch.RestoreMods(configPath);
        var path = orch.Mods.ResolveModsConfigPath(configPath) ?? "(resolved)";
        Console.WriteLine($"Restored pre-MP backup → {path}");
        return 0;
    }

    static int CmdModsShow(SessionOrchestrator orch, string? configPath)
    {
        var snap = orch.ShowMods(configPath);
        if (snap is null)
            return Fail($"ModsConfig.xml not found ({configPath ?? orch.Mods.ResolveModsConfigPath()}).");

        Console.WriteLine($"ModsConfig: {snap.Path}");
        Console.WriteLine($"version:    {snap.Version}");
        Console.WriteLine("activeMods:");
        for (var i = 0; i < snap.ActiveMods.Count; i++)
            Console.WriteLine($"  {i + 1,3}. {snap.ActiveMods[i]}");
        Console.WriteLine($"modlistHash: {orch.Mods.ComputeHash(snap.ActiveMods)}");
        return 0;
    }

    static int CmdHash(string[] args)
    {
        var orch = new SessionOrchestrator();
        var configPath = GetOpt(args, "--config");
        return CmdModsShow(orch, configPath);
    }

    static async Task<int> CmdDiscoverAsync(string[] args)
    {
        var seconds = GetOptInt(args, "--seconds", 3);
        var discoPort = GetOptInt(args, "--discovery-port", LauncherPaths.DefaultDiscoveryPort);
        Console.WriteLine($"Listening for RimworldMP beacons on UDP {discoPort} for {seconds}s…");
        var beacons = await SessionOrchestrator.DiscoverAsync(TimeSpan.FromSeconds(seconds), discoPort).ConfigureAwait(false);
        if (beacons.Count == 0)
        {
            Console.WriteLine("No hosts found. Use join --ip <address> for manual connect.");
            return 2;
        }

        var i = 1;
        foreach (var b in beacons)
        {
            Console.WriteLine($"{i++}. {b.Name}  {b.Host}:{b.Port}  rw={b.RwBuild}  hash={b.ModHashShort}…");
            Console.WriteLine($"    modlistHash={b.ModlistHash}");
        }

        return 0;
    }

    static async Task<int> CmdHostAsync(string[] args)
    {
        var orch = new SessionOrchestrator();
        var shareMods = HasFlag(args, "--share-mods");
        var modSyncPort = GetOptInt(args, "--mod-sync-port", LauncherPaths.DefaultModSyncPort);
        var options = new HostSessionOptions
        {
            SessionName = GetOpt(args, "--name") ?? "RimworldMP LAN",
            Port = GetOptInt(args, "--port", LauncherPaths.DefaultGamePort),
            DiscoveryPort = GetOptInt(args, "--discovery-port", LauncherPaths.DefaultDiscoveryPort),
            LaunchGame = !HasFlag(args, "--no-launch"),
            PrepareMods = !HasFlag(args, "--no-mods"),
            InstallPath = GetOpt(args, "--path"),
            ConfigPath = GetOpt(args, "--config"),
            BindHost = GetOpt(args, "--bind-host") ?? SessionOrchestrator.GetPrimaryLanIPv4() ?? "127.0.0.1",
            MaxClients = GetOptInt(args, "--max-clients", 4),
            ShareMods = shareMods,
            ModSyncPort = modSyncPort,
        };

        var (ordered, hash, warnings) = options.PrepareMods
            ? Prep(orch, options.ConfigPath, "host")
            : LoadExisting(orch, options.ConfigPath);

        foreach (var w in warnings)
            Console.Error.WriteLine($"warning: {w}");

        var install = RimWorldLocator.TryLocate(options.InstallPath);
        var rwBuild = install?.VersionHint ?? "1.7";
        var bindHost = options.BindHost ?? "127.0.0.1";

        var handoff = HandoffWriter.Create(
            role: "host",
            host: "0.0.0.0",
            port: options.Port,
            modlistHash: hash,
            sessionName: options.SessionName,
            rwBuild: rwBuild,
            maxClients: options.MaxClients);
        var handoffPath = HandoffWriter.Write(handoff);
        Console.WriteLine($"handoff: {handoffPath}");
        Console.WriteLine($"modlistHash: {hash}");
        Console.WriteLine($"advertise: {bindHost}:{options.Port} (game) · discovery UDP {options.DiscoveryPort}");

        await using var share = new HostModShareService();
        share.Log += msg => Console.WriteLine($"  [modsync] {msg}");
        if (shareMods)
        {
            if (install is null)
                return Fail("RimWorld not found (needed to share Mods). Pass --path <install-dir>.");
            try
            {
                await share.StartAsync(ordered, install.InstallDir, rwBuild, modSyncPort, IPAddress.Any)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Fail($"Cannot share mods: {ex.Message}");
            }

            modSyncPort = share.Port;
            Console.WriteLine($"mod-share: TCP {bindHost}:{modSyncPort} ({share.Manifest?.Mods.Count ?? 0} packages, excl. Ludeon official)");
        }

        var beacon = orch.CreateHostBeacon(options, hash, bindHost, rwBuild, shareMods ? modSyncPort : null);
        await using var disco = await orch.BeginHostBroadcastAsync(beacon, options.DiscoveryPort).ConfigureAwait(false);
        Console.WriteLine("Broadcasting LAN beacon (Ctrl+C to stop after launch)…");

        if (options.LaunchGame)
        {
            if (install is null)
                return Fail("RimWorld not found. Pass --path <install-dir> or run locate first.");
            var launched = ProcessLauncher.Launch(install);
            Console.WriteLine($"launched pid={launched.ProcessId} {launched.ExecutablePath}");
        }
        else
        {
            Console.WriteLine("(--no-launch) Start RimWorld manually; mod should read handoff on load.");
        }

        Console.WriteLine("Beacon running. Press Enter to stop.");
        Console.ReadLine();
        return 0;
    }

    static async Task<int> CmdJoinAsync(string[] args)
    {
        var port = GetOptInt(args, "--port", LauncherPaths.DefaultGamePort);
        var discoPort = GetOptInt(args, "--discovery-port", LauncherPaths.DefaultDiscoveryPort);
        var seconds = GetOptInt(args, "--seconds", 3);
        var name = GetOpt(args, "--name") ?? "RimworldMP LAN";
        var ip = GetOpt(args, "--ip");
        string? hostHash = null;

        if (string.IsNullOrWhiteSpace(ip))
        {
            Console.WriteLine($"No --ip given; discovering for {seconds}s…");
            var beacons = await SessionOrchestrator.DiscoverAsync(TimeSpan.FromSeconds(seconds), discoPort).ConfigureAwait(false);
            if (beacons.Count == 0)
                return Fail("No LAN hosts found. Re-run with --ip <host>.");

            for (var i = 0; i < beacons.Count; i++)
            {
                var b = beacons[i];
                Console.WriteLine($"  [{i + 1}] {b.Name} {b.Host}:{b.Port} hash={b.ModHashShort}…");
            }

            Console.Write("Select host number (or Enter for 1): ");
            var choice = Console.ReadLine()?.Trim();
            var idx = 0;
            if (!string.IsNullOrEmpty(choice) && (!int.TryParse(choice, out var n) || n < 1 || n > beacons.Count))
                return Fail("Invalid selection.");
            if (!string.IsNullOrEmpty(choice))
                idx = int.Parse(choice, CultureInfo.InvariantCulture) - 1;

            var selected = beacons[idx];
            ip = selected.Host;
            port = selected.Port;
            name = selected.Name;
            hostHash = selected.ModlistHash;
            Console.WriteLine($"Selected {name} at {ip}:{port}");
            Console.WriteLine($"host modlistHash: {selected.ModlistHash}");
        }

        var orch = new SessionOrchestrator();
        try
        {
            var result = orch.StartJoin(new JoinSessionOptions
            {
                SessionName = name,
                Host = ip!,
                Port = port,
                InstallPath = GetOpt(args, "--path"),
                ConfigPath = GetOpt(args, "--config"),
                PrepareMods = !HasFlag(args, "--no-mods"),
                LaunchGame = !HasFlag(args, "--no-launch"),
                HostModlistHash = hostHash,
            });

            foreach (var w in result.Warnings)
                Console.Error.WriteLine($"warning: {w}");

            Console.WriteLine($"handoff: {result.HandoffPath}");
            Console.WriteLine($"modlistHash: {result.ModlistHash}");
            Console.WriteLine($"join target: {ip}:{port}");
            if (result.Launch is not null)
                Console.WriteLine($"launched pid={result.Launch.ProcessId} {result.Launch.ExecutablePath}");
            else if (HasFlag(args, "--no-launch"))
                Console.WriteLine("(--no-launch) Start RimWorld manually; use DevMode join if handoff not wired yet.");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    static (IReadOnlyList<string> Ordered, string Hash, IReadOnlyList<string> Warnings) Prep(
        SessionOrchestrator orch, string? config, string profile)
    {
        var r = orch.PrepareMods(config, profile);
        return (r.Ordered, r.Hash, r.Warnings);
    }

    static (IReadOnlyList<string> Ordered, string Hash, IReadOnlyList<string> Warnings) LoadExisting(
        SessionOrchestrator orch, string? config)
    {
        var snap = orch.ShowMods(config)
            ?? throw new InvalidOperationException("No ModsConfig.xml; omit --no-mods or run mods prepare first.");
        var hash = orch.Mods.ComputeHash(snap.ActiveMods);
        return (snap.ActiveMods, hash, SessionOrchestrator.CollectModWarnings(snap.ActiveMods));
    }

    static string? GetOpt(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 >= args.Length)
                throw new ArgumentException($"Missing value for {name}");
            return args[i + 1];
        }

        return null;
    }

    static int GetOptInt(string[] args, string name, int defaultValue)
    {
        var raw = GetOpt(args, name);
        if (raw is null)
            return defaultValue;
        if (!int.TryParse(raw, out var v))
            throw new ArgumentException($"Invalid int for {name}: {raw}");
        return v;
    }

    static bool HasFlag(string[] args, string name) =>
        args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

    static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        return 1;
    }
}

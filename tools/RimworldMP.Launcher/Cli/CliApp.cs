using RimworldMP.Launcher.Models;
using RimworldMP.Launcher.Services;

namespace RimworldMP.Launcher.Cli;

/// <summary>Console fallback (`--cli` and subcommands).</summary>
public static class CliApp
{
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
                "mods" => CmdMods(rest),
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
        Console.WriteLine("  4) Host (beacon + handoff + launch)");
        Console.WriteLine("  5) Join (discover / manual IP)");
        Console.WriteLine("  6) Restore ModsConfig backup");
        Console.WriteLine("  7) Help");
        Console.WriteLine("  0) Exit");
        Console.Write("> ");
        var line = Console.ReadLine()?.Trim();
        return line switch
        {
            "1" => await DispatchAsync(new[] { "locate" }).ConfigureAwait(false),
            "2" => await DispatchAsync(new[] { "mods", "prepare" }).ConfigureAwait(false),
            "3" => await DispatchAsync(new[] { "hash" }).ConfigureAwait(false),
            "4" => await DispatchAsync(new[] { "host" }).ConfigureAwait(false),
            "5" => await DispatchAsync(new[] { "join" }).ConfigureAwait(false),
            "6" => await DispatchAsync(new[] { "mods", "restore" }).ConfigureAwait(false),
            "7" => PrintHelp(),
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

            Join options:
              --ip <host>        --port <30555>  --seconds <3>
              --path <rw-dir>    --config <ModsConfig.xml>
              --no-launch        --no-mods
              --name <session>

            Docs: docs/launcher-mvp.md · docs/launcher-handoff.md
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

    static int CmdMods(string[] args)
    {
        if (args.Length == 0)
            return Fail("mods requires: prepare | restore | show");

        var sub = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();
        var orch = new SessionOrchestrator();
        var configPath = GetOpt(rest, "--config");

        return sub switch
        {
            "prepare" => CmdModsPrepare(orch, rest, configPath),
            "restore" => CmdModsRestore(orch, configPath),
            "show" => CmdModsShow(orch, configPath),
            _ => Fail($"Unknown mods subcommand '{sub}'."),
        };
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

        var beacon = orch.CreateHostBeacon(options, hash, bindHost, rwBuild);
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

        _ = ordered;
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
                idx = int.Parse(choice) - 1;

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

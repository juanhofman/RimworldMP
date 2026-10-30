using System.Net.Sockets;
using RimworldMP.ModSync;
using RimworldMP.ModSync.Protocol;
using RimworldMP.ModSync.Providers;

namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Real <see cref="IModSyncClient"/>: compare + pull exact mods from a host ModSync TCP server (port 30557).
/// </summary>
public sealed class LanModSyncClient : IModSyncClient
{
    private readonly ModsConfigService _mods;
    private readonly Func<string?> _resolveInstallDir;
    private readonly object _gate = new();
    private ModSyncHostTarget? _lastTarget;
    private SessionModManifest? _lastHostManifest;

    public LanModSyncClient(ModsConfigService? mods = null, Func<string?>? resolveInstallDir = null)
    {
        _mods = mods ?? new ModsConfigService();
        _resolveInstallDir = resolveInstallDir ?? (() => RimWorldLocator.TryLocate()?.InstallDir);
    }

    public bool IsAvailable => true;

    public async Task<ModSyncPlan> CompareAsync(ModSyncHostTarget target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.Host))
            throw new ArgumentException("Host is required.", nameof(target));

        var installDir = _resolveInstallDir();
        if (string.IsNullOrWhiteSpace(installDir))
        {
            throw new InvalidOperationException(
                "RimWorld install not found — set the install path before comparing mods (UI Launch path or CLI --path).");
        }

        var localEntries = ModInventory.ScanInstalled(installDir);
        var localMap = localEntries.ToDictionary(m => m.PackageId, StringComparer.OrdinalIgnoreCase);
        var localActive = GetActivePackageIds();
        var localHash = ModlistHasher.Compute(localActive);

        // Fetch host manifest over LAN control channel
        var client = new ClientModSync(ResolveModsRoot(installDir));
        var hostManifest = await client.FetchManifestAsync(target.Host.Trim(), target.Port, cancellationToken)
            .ConfigureAwait(false);

        lock (_gate)
        {
            _lastTarget = target;
            _lastHostManifest = hostManifest;
        }

        var remoteOrder = hostManifest.OrderedPackageIds;
        var remoteHash = hostManifest.ModlistHash;
        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(target.ExpectedModlistHash) &&
            !ModlistHasher.Matches(remoteHash, target.ExpectedModlistHash))
        {
            warnings.Add(
                $"Discovery beacon modlist hash ({Short(target.ExpectedModlistHash)}) disagrees with live share server " +
                $"({Short(remoteHash)}). Using the live share manifest; re-check the host if this is unexpected.");
        }

        if (hostManifest.Mods.Count > 0)
        {
            warnings.Add(
                "Missing / mismatched packages are acquired over LAN from the host (or local Workshop cache when hashes match). " +
                SteamCmdProviderStub.StubLimitation);
        }

        var missing = new List<ModSyncDelta>();
        var mismatched = new List<ModSyncDelta>();
        var hostIds = new HashSet<string>(remoteOrder, StringComparer.OrdinalIgnoreCase);

        foreach (var needed in hostManifest.Mods)
        {
            if (!localMap.TryGetValue(needed.PackageId, out var local))
            {
                missing.Add(new ModSyncDelta
                {
                    PackageId = needed.PackageId,
                    DisplayName = needed.Name,
                    Reason = "not installed locally — will pull over LAN (not SteamCMD)",
                    ByteSize = needed.ByteSize,
                });
                continue;
            }

            if (!string.Equals(local.ContentHash, needed.ContentHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(local.Version ?? "", needed.Version ?? "", StringComparison.Ordinal))
            {
                mismatched.Add(new ModSyncDelta
                {
                    PackageId = needed.PackageId,
                    DisplayName = needed.Name,
                    Reason = $"version/content mismatch (local v={local.Version}, host v={needed.Version})",
                    ByteSize = needed.ByteSize,
                });
            }
        }

        // Official / listed but not in shareable Mods list (e.g. Ludeon) — treat as missing only if not installed
        foreach (var id in remoteOrder)
        {
            if (ModInventory.IsOfficialContent(id))
                continue;
            if (hostManifest.Mods.Any(m => m.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!localMap.ContainsKey(id) && !localActive.Any(a => a.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                missing.Add(new ModSyncDelta
                {
                    PackageId = id,
                    Reason = "listed on host but not present locally (and not in host share set — install manually)",
                });
            }
        }

        var extra = localActive
            .Where(id => !hostIds.Contains(id))
            .Select(id => new ModSyncDelta
            {
                PackageId = id,
                Reason = "enabled locally but not on host modlist",
            })
            .ToList();

        // Order-only mismatch when sets match but hash differs
        if (missing.Count == 0 && mismatched.Count == 0 && !ModlistHasher.Matches(localHash, remoteHash))
        {
            mismatched.Add(new ModSyncDelta
            {
                PackageId = "(order)",
                Reason = "package set matches shareable mods but ordered ModlistHash differs",
            });
        }

        return new ModSyncPlan
        {
            LocalHash = localHash,
            RemoteHash = remoteHash,
            HashesMatch = ModlistHasher.Matches(localHash, remoteHash) && missing.Count == 0 && mismatched.Count == 0,
            Missing = missing,
            Mismatched = mismatched,
            ExtraLocal = extra,
            RemoteOrder = remoteOrder.ToList(),
            Warnings = warnings,
        };
    }

    public async Task<ModSyncResult> SyncAsync(
        ModSyncPlan plan,
        IProgress<ModSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ModSyncHostTarget? target;
        SessionModManifest? cachedManifest;
        lock (_gate)
        {
            target = _lastTarget;
            cachedManifest = _lastHostManifest;
        }

        if (target is null || string.IsNullOrWhiteSpace(target.Host))
        {
            return new ModSyncResult
            {
                Success = false,
                Error = "No host target — run Compare first.",
            };
        }

        var installDir = _resolveInstallDir();
        if (string.IsNullOrWhiteSpace(installDir))
        {
            return new ModSyncResult
            {
                Success = false,
                Error = "RimWorld install not found — set path before syncing mods.",
            };
        }

        var modsRoot = ResolveModsRoot(installDir);
        Directory.CreateDirectory(modsRoot);

        var local = ModInventory.ScanInstalled(installDir);
        // Workshop cache only — SteamCMD stub is registered so limitation messaging stays visible in logs,
        // but ClientModSync skips unimplemented providers (never fakes a download).
        var providers = new IModAcquisitionProvider[]
        {
            new WorkshopCacheProvider(installDir),
            new SteamCmdProviderStub(),
        };

        var sync = new ClientModSync(modsRoot, providers);
        var lastFraction = 0.0;
        sync.Log += msg => progress?.Report(new ModSyncProgress
        {
            Phase = "log",
            Fraction = lastFraction,
            Message = msg,
        });
        var bridge = new Progress<(string Phase, string? PackageId, double Fraction)>(p =>
        {
            lastFraction = p.Fraction;
            progress?.Report(new ModSyncProgress
            {
                Phase = p.Phase,
                CurrentPackageId = p.PackageId,
                Fraction = p.Fraction,
                Message = p.PackageId is null ? p.Phase : $"{p.Phase}: {p.PackageId}",
            });
        });

        try
        {
            progress?.Report(new ModSyncProgress { Phase = "connect", Fraction = 0, Message = $"Connecting {target.Host}:{target.Port}" });

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(target.Host.Trim(), target.Port, cancellationToken).ConfigureAwait(false);
            await using var stream = tcp.GetStream();

            // Hello
            await ModSyncFrameIo.WriteFrameAsync(stream, ModSyncOpcode.Hello, ModSyncFrameIo.EncodeHello(), cancellationToken)
                .ConfigureAwait(false);
            var (op, payload) = await ModSyncFrameIo.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
            if (op == ModSyncOpcode.Error)
                throw new InvalidOperationException(ModSyncFrameIo.DecodeUtf8(payload));
            if (op != ModSyncOpcode.HelloAck)
                throw new InvalidOperationException($"Expected HelloAck, got {op}");
            ModSyncFrameIo.ValidateHello(payload);

            SessionModManifest hostManifest;
            if (cachedManifest is not null &&
                ModlistHasher.Matches(cachedManifest.ModlistHash, plan.RemoteHash))
            {
                hostManifest = cachedManifest;
            }
            else
            {
                await ModSyncFrameIo.WriteFrameAsync(stream, ModSyncOpcode.RequestManifest, ReadOnlyMemory<byte>.Empty, cancellationToken)
                    .ConfigureAwait(false);
                (op, payload) = await ModSyncFrameIo.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (op == ModSyncOpcode.Error)
                    throw new InvalidOperationException(ModSyncFrameIo.DecodeUtf8(payload));
                if (op != ModSyncOpcode.Manifest)
                    throw new InvalidOperationException($"Expected Manifest, got {op}");
                hostManifest = ModSyncFrameIo.DecodeManifest(payload);
            }

            var outcome = await sync.SyncAgainstManifestAsync(stream, hostManifest, local, bridge, cancellationToken)
                .ConfigureAwait(false);

            if (!outcome.HashesMatch)
            {
                return new ModSyncResult
                {
                    Success = false,
                    Error =
                        $"Modlist hash still mismatches after sync (local={Short(outcome.LocalModlistHash)}, " +
                        $"host={Short(hostManifest.ModlistHash)}). Re-run Compare, or install missing packages manually. " +
                        SteamCmdProviderStub.StubLimitation,
                    ResultingHash = outcome.LocalModlistHash,
                    AppliedPackageIds = hostManifest.OrderedPackageIds.ToList(),
                };
            }

            // Apply host load order so join ModlistHash matches.
            var cfg = _mods.ResolveModsConfigPath();
            if (cfg is null)
            {
                return new ModSyncResult
                {
                    Success = false,
                    Error = "ModsConfig.xml not found after sync — cannot apply host load order.",
                    ResultingHash = outcome.LocalModlistHash,
                    AppliedPackageIds = hostManifest.OrderedPackageIds.ToList(),
                };
            }

            if (File.Exists(cfg))
                _mods.Backup(cfg, "pre-sync");
            _mods.WriteActiveMods(cfg, hostManifest.OrderedPackageIds, version: "1.7");

            var resulting = ModlistHasher.Compute(hostManifest.OrderedPackageIds);
            progress?.Report(new ModSyncProgress { Phase = "done", Fraction = 1, Message = "Sync complete" });

            return new ModSyncResult
            {
                Success = true,
                ResultingHash = resulting,
                AppliedPackageIds = hostManifest.OrderedPackageIds.ToList(),
            };
        }
        catch (Exception ex)
        {
            progress?.Report(new ModSyncProgress { Phase = "error", Fraction = 0, Message = ex.Message });
            return new ModSyncResult
            {
                Success = false,
                Error = ex.Message,
            };
        }
    }

    private IReadOnlyList<string> GetActivePackageIds()
    {
        var path = _mods.ResolveModsConfigPath();
        if (path is null || !File.Exists(path))
            return Array.Empty<string>();
        return _mods.Read(path).ActiveMods;
    }

    private static string ResolveModsRoot(string installDir) =>
        Path.Combine(installDir, "Mods");

    private static string Short(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
            return "(empty)";
        return hash.Length <= 12 ? hash : hash[..12] + "…";
    }
}

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RimworldMP.Core.Commands;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Sync;

/// <summary>
/// Structured local-only desync dump: RNG counters/seed, tick/protocol/peer ids,
/// short modlist hash, last N commands, and an optional recent-batch ring buffer.
/// Files stay under the caller's log directory — never uploaded.
/// </summary>
public static class DesyncDump
{
    public const int DefaultCommandHistory = 128;
    public const int DefaultBatchHistory = 64;
    public const int BatchHexPreviewBytes = 64;
    public const int ModlistHashShortChars = 12;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string DefaultLogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RimworldMP", "desync");

    public static string ShortModlistHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
            return "—";
        return hash.Length <= ModlistHashShortChars ? hash : hash[..ModlistHashShortChars];
    }

    /// <summary>Backward-compatible entry: returns the primary <c>.log</c> path.</summary>
    public static string Write(
        string logDirectory,
        long tick,
        string localHashHex,
        string remoteHashHex,
        long rngSeed,
        long rngDraws,
        IReadOnlyList<MpCommand> recentCommands,
        IReadOnlyDictionary<string, long>? rngCounters = null,
        byte? protocolVersion = null,
        DesyncDumpContext? context = null)
    {
        var paths = WriteAll(
            logDirectory,
            tick,
            localHashHex,
            remoteHashHex,
            rngSeed,
            rngDraws,
            recentCommands,
            rngCounters,
            protocolVersion ?? NetProtocol.Version,
            context);
        return paths.LogPath;
    }

    public static DesyncDumpPaths WriteAll(
        string logDirectory,
        long tick,
        string localHashHex,
        string remoteHashHex,
        long rngSeed,
        long rngDraws,
        IReadOnlyList<MpCommand>? recentCommands,
        IReadOnlyDictionary<string, long>? rngCounters = null,
        byte protocolVersion = NetProtocol.Version,
        DesyncDumpContext? context = null)
    {
        Directory.CreateDirectory(logDirectory);
        var inv = CultureInfo.InvariantCulture;
        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss", inv);
        var baseName = string.Create(inv, $"desync-tick{tick}-{stamp}");
        var logPath = Path.Combine(logDirectory, baseName + ".log");
        var jsonPath = Path.Combine(logDirectory, baseName + ".cmds.json");
        string? batchesJsonPath = null;

        var cmdLimit = context?.CommandHistoryLimit > 0
            ? context.CommandHistoryLimit
            : DefaultCommandHistory;
        var batchLimit = context?.BatchHistoryLimit > 0
            ? context.BatchHistoryLimit
            : DefaultBatchHistory;

        var cmds = TakeLast(recentCommands ?? Array.Empty<MpCommand>(), cmdLimit);
        var batches = TakeLast(context?.RecentBatches ?? Array.Empty<DesyncBatchSnapshot>(), batchLimit);

        var sb = new StringBuilder();
        sb.AppendLine("=== RimworldMP desync dump ===");
        sb.AppendLine("section=header");
        sb.AppendLine("RimworldMP desync detected");
        sb.AppendLine(inv, $"utc={DateTime.UtcNow:O}");
        sb.AppendLine("localOnly=true");
        sb.AppendLine("upload=never");
        sb.AppendLine("note=dumps stay on this machine; RimworldMP never uploads them");

        sb.AppendLine();
        sb.AppendLine("section=session");
        sb.AppendLine(inv, $"protocol={protocolVersion}");
        sb.AppendLine(inv, $"tick={tick}");
        if (context?.LocalPeerId is int localPeer)
            sb.AppendLine(inv, $"localPeerId={localPeer}");
        if (context?.PeerIds is { Count: > 0 } peers)
            sb.AppendLine(inv, $"peerIds={string.Join(',', peers.OrderBy(p => p))}");
        var modlist = context?.ModlistHash;
        if (!string.IsNullOrEmpty(modlist))
        {
            sb.AppendLine(inv, $"modlistHashShort={ShortModlistHash(modlist)}");
            sb.AppendLine(inv, $"modlistHash={modlist}");
        }
        else
        {
            sb.AppendLine("modlistHashShort=—");
        }

        sb.AppendLine();
        sb.AppendLine("section=fingerprint");
        sb.AppendLine(inv, $"localHash={localHashHex}");
        sb.AppendLine(inv, $"remoteHash={remoteHashHex}");
        sb.AppendLine("note=compare uses published end-of-tick checkpoint hash");

        sb.AppendLine();
        sb.AppendLine("section=rng");
        sb.AppendLine(inv, $"rngSeed={rngSeed}");
        sb.AppendLine(inv, $"rngDraws={rngDraws}");
        if (context?.RngOpinion is ulong opinion)
            sb.AppendLine(inv, $"rngOpinion=0x{opinion:x16}");
        if (context?.SessionTickAtDump is long sessionTick)
            sb.AppendLine(inv, $"sessionTickAtDump={sessionTick}");
        if (rngCounters is { Count: > 0 })
        {
            sb.AppendLine("rngCounters:");
            foreach (var (key, value) in rngCounters.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                sb.AppendLine(inv, $"  {key}={value}");
        }
        else
        {
            sb.AppendLine("rngCounters: (none)");
        }

        sb.AppendLine();
        sb.AppendLine("section=recentCommands");
        sb.AppendLine(inv, $"recentCommands={cmds.Count}");
        sb.AppendLine(inv, $"recentCommandsLimit={cmdLimit}");
        foreach (var cmd in cmds)
        {
            sb.AppendLine(
                inv,
                $"  tick={cmd.Tick} op={cmd.Opcode}({(ushort)cmd.Opcode}) player={cmd.PlayerId} payloadHex={Convert.ToHexString(cmd.Payload)}");
        }

        sb.AppendLine();
        sb.AppendLine("section=recentBatches");
        sb.AppendLine(inv, $"recentBatches={batches.Count}");
        sb.AppendLine(inv, $"recentBatchesLimit={batchLimit}");
        foreach (var batch in batches)
        {
            sb.AppendLine(
                inv,
                $"  tick={batch.Tick} bytes={batch.ByteLength} cmds={batch.CommandCount} hexPreview={batch.PayloadHexPreview}");
        }

        sb.AppendLine();
        sb.AppendLine("section=files");
        sb.AppendLine(inv, $"cmdsJson={jsonPath}");
        if (batches.Count > 0)
        {
            batchesJsonPath = Path.Combine(logDirectory, baseName + ".batches.json");
            sb.AppendLine(inv, $"batchesJson={batchesJsonPath}");
        }

        File.WriteAllText(logPath, sb.ToString());

        var cmdDto = cmds.Select(c => new
        {
            tick = c.Tick,
            opcode = c.Opcode.ToString(),
            opcodeValue = (ushort)c.Opcode,
            playerId = c.PlayerId,
            payloadHex = Convert.ToHexString(c.Payload),
        });
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(cmdDto, JsonOpts));

        if (batchesJsonPath is not null)
        {
            var batchDto = batches.Select(b => new
            {
                tick = b.Tick,
                byteLength = b.ByteLength,
                commandCount = b.CommandCount,
                payloadHexPreview = b.PayloadHexPreview,
            });
            File.WriteAllText(batchesJsonPath, JsonSerializer.Serialize(batchDto, JsonOpts));
        }

        return new DesyncDumpPaths
        {
            LogPath = logPath,
            CommandsJsonPath = jsonPath,
            BatchesJsonPath = batchesJsonPath,
        };
    }

    /// <summary>Build a ring-buffer snapshot entry from a raw <see cref="CommandBatch"/> frame.</summary>
    public static DesyncBatchSnapshot SnapshotBatch(long tick, byte[] batchBytes, int hexPreviewBytes = BatchHexPreviewBytes)
    {
        ArgumentNullException.ThrowIfNull(batchBytes);
        var cmdCount = 0;
        if (batchBytes.Length >= 12)
            cmdCount = BinaryPrimitives.ReadInt32LittleEndian(batchBytes.AsSpan(8));
        var take = Math.Min(Math.Max(0, hexPreviewBytes), batchBytes.Length);
        var preview = take == 0 ? string.Empty : Convert.ToHexString(batchBytes.AsSpan(0, take));
        if (batchBytes.Length > take)
            preview += "…";
        return new DesyncBatchSnapshot
        {
            Tick = tick,
            ByteLength = batchBytes.Length,
            CommandCount = cmdCount,
            PayloadHexPreview = preview,
        };
    }

    private static List<T> TakeLast<T>(IReadOnlyList<T> source, int limit)
    {
        if (limit <= 0 || source.Count == 0)
            return new List<T>();
        if (source.Count <= limit)
            return source.ToList();
        var list = new List<T>(limit);
        for (var i = source.Count - limit; i < source.Count; i++)
            list.Add(source[i]);
        return list;
    }
}

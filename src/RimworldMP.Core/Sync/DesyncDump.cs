using System.Text;
using System.Text.Json;
using RimworldMP.Core.Commands;

namespace RimworldMP.Core.Sync;

/// <summary>Structured desync dump: RNG counters + last N commands with payload hex.</summary>
public static class DesyncDump
{
    public const int DefaultCommandHistory = 128;

    public static string Write(
        string logDirectory,
        long tick,
        string localHashHex,
        string remoteHashHex,
        long rngSeed,
        long rngDraws,
        IReadOnlyList<MpCommand> recentCommands,
        IReadOnlyDictionary<string, long>? rngCounters = null,
        byte? protocolVersion = null)
    {
        Directory.CreateDirectory(logDirectory);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var baseName = $"desync-tick{tick}-{stamp}";
        var logPath = Path.Combine(logDirectory, baseName + ".log");
        var jsonPath = Path.Combine(logDirectory, baseName + ".cmds.json");

        var sb = new StringBuilder();
        sb.AppendLine("RimworldMP desync detected");
        sb.AppendLine($"utc={DateTime.UtcNow:O}");
        if (protocolVersion is not null)
            sb.AppendLine($"protocol={protocolVersion.Value}");
        sb.AppendLine($"tick={tick}");
        sb.AppendLine($"localHash={localHashHex}");
        sb.AppendLine($"remoteHash={remoteHashHex}");
        sb.AppendLine($"rngSeed={rngSeed}");
        sb.AppendLine($"rngDraws={rngDraws}");
        sb.AppendLine("note=worldFingerprint slot reserved; Verse map/world samples need RIMWORLD_DIR");
        if (rngCounters is { Count: > 0 })
        {
            sb.AppendLine("rngCounters:");
            foreach (var (key, value) in rngCounters.OrderBy(kv => kv.Key))
                sb.AppendLine($"  {key}={value}");
        }

        sb.AppendLine($"recentCommands={recentCommands.Count}");
        foreach (var cmd in recentCommands)
        {
            sb.AppendLine(
                $"  tick={cmd.Tick} op={cmd.Opcode}({(ushort)cmd.Opcode}) player={cmd.PlayerId} payloadHex={Convert.ToHexString(cmd.Payload)}");
        }

        sb.AppendLine($"cmdsJson={jsonPath}");
        File.WriteAllText(logPath, sb.ToString());

        var dto = recentCommands.Select(c => new
        {
            tick = c.Tick,
            opcode = c.Opcode.ToString(),
            opcodeValue = (ushort)c.Opcode,
            playerId = c.PlayerId,
            payloadHex = Convert.ToHexString(c.Payload),
        });
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        return logPath;
    }
}

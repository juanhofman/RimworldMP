using System.Text;
using RimworldMP.Core.Commands;

namespace RimworldMP.Core.Sync;

public sealed class DesyncInfo
{
    public required long Tick { get; init; }
    public required string LocalHashHex { get; init; }
    public required string RemoteHashHex { get; init; }
    public required string LogPath { get; init; }
}

/// <summary>
/// Compares periodic state hashes across peers. On mismatch: freeze session and write a dump file.
/// </summary>
public sealed class DesyncDetector
{
    private readonly StateHasher _hasher;
    private readonly SyncedRand _rand;
    private readonly string _logDirectory;
    private readonly object _gate = new();
    private bool _frozen;
    private DesyncInfo? _last;

    public DesyncDetector(StateHasher hasher, SyncedRand rand, string? logDirectory = null)
    {
        _hasher = hasher;
        _rand = rand;
        _logDirectory = logDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RimworldMP", "desync");
    }

    public bool IsFrozen
    {
        get { lock (_gate) return _frozen; }
    }

    public DesyncInfo? LastDesync
    {
        get { lock (_gate) return _last; }
    }

    public string LogDirectory => _logDirectory;

    public byte[] SnapshotHash(long tick)
    {
        _hasher.AddTick(tick);
        _hasher.AddDrawCount(_rand.DrawCount);
        return _hasher.CurrentHash();
    }

    public string SnapshotHashHex(long tick) => Convert.ToHexString(SnapshotHash(tick)).ToLowerInvariant();

    /// <summary>
    /// Compare a remote peer hash for the same tick. Returns true if still in sync.
    /// </summary>
    public bool CheckRemote(long tick, string remoteHashHex, IReadOnlyList<MpCommand>? recentCommands = null)
    {
        var local = SnapshotHashHex(tick);
        if (string.Equals(local, remoteHashHex, StringComparison.OrdinalIgnoreCase))
            return true;

        Freeze(tick, local, remoteHashHex, recentCommands);
        return false;
    }

    public void Freeze(long tick, string localHashHex, string remoteHashHex, IReadOnlyList<MpCommand>? recentCommands = null)
    {
        lock (_gate)
        {
            if (_frozen)
                return;
            _frozen = true;

            Directory.CreateDirectory(_logDirectory);
            var fileName = $"desync-tick{tick}-{DateTime.UtcNow:yyyyMMddTHHmmss}.log";
            var path = Path.Combine(_logDirectory, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("RimworldMP desync detected");
            sb.AppendLine($"utc={DateTime.UtcNow:O}");
            sb.AppendLine($"tick={tick}");
            sb.AppendLine($"localHash={localHashHex}");
            sb.AppendLine($"remoteHash={remoteHashHex}");
            sb.AppendLine($"rngSeed={_rand.Seed}");
            sb.AppendLine($"rngDraws={_rand.DrawCount}");
            if (recentCommands is { Count: > 0 })
            {
                sb.AppendLine("recentCommands:");
                foreach (var cmd in recentCommands)
                    sb.AppendLine($"  tick={cmd.Tick} op={cmd.Opcode} player={cmd.PlayerId} payloadBytes={cmd.Payload.Length}");
            }

            File.WriteAllText(path, sb.ToString());
            _last = new DesyncInfo
            {
                Tick = tick,
                LocalHashHex = localHashHex,
                RemoteHashHex = remoteHashHex,
                LogPath = path,
            };
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _frozen = false;
            _last = null;
            _hasher.Reset();
        }
    }
}

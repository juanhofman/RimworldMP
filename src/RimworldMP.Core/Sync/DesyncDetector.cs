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
/// Checkpoint hashes are ephemeral — they must not mutate the rolling <see cref="StateHasher"/>.
/// </summary>
public sealed class DesyncDetector
{
    private readonly StateHasher _hasher;
    private readonly SyncedRand _rand;
    private readonly string _logDirectory;
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _rngCounters = new(StringComparer.Ordinal);
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

    public void IncrementCounter(string name, long delta = 1)
    {
        lock (_gate)
        {
            _rngCounters.TryGetValue(name, out var cur);
            _rngCounters[name] = cur + delta;
        }
    }

    public IReadOnlyDictionary<string, long> SnapshotCounters()
    {
        lock (_gate)
            return new Dictionary<string, long>(_rngCounters);
    }

    /// <summary>
    /// Non-mutating checkpoint: SHA256(rollingState ‖ tick ‖ drawCount).
    /// </summary>
    public byte[] SnapshotHash(long tick)
    {
        var state = _hasher.CurrentHash();
        Span<byte> buf = stackalloc byte[32 + 8 + 8];
        state.CopyTo(buf);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(buf[32..], tick);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(buf[40..], _rand.DrawCount);
        return System.Security.Cryptography.SHA256.HashData(buf);
    }

    public string SnapshotHashHex(long tick) => Convert.ToHexString(SnapshotHash(tick)).ToLowerInvariant();

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

            var cmds = recentCommands ?? Array.Empty<MpCommand>();
            var path = DesyncDump.Write(
                _logDirectory,
                tick,
                localHashHex,
                remoteHashHex,
                _rand.Seed,
                _rand.DrawCount,
                cmds,
                new Dictionary<string, long>(_rngCounters));

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
            _rngCounters.Clear();
            _hasher.Reset();
        }
    }
}

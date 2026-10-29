using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using RimworldMP.Core.Commands;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Sync;

public sealed class DesyncInfo
{
    public required long Tick { get; init; }
    public required string LocalHashHex { get; init; }
    public required string RemoteHashHex { get; init; }
    public required string LogPath { get; init; }
    public string? CommandsJsonPath { get; init; }
    public string? BatchesJsonPath { get; init; }
    public string? ModlistHashShort { get; init; }
}

/// <summary>
/// Compares periodic state fingerprints across peers. On mismatch: freeze session and write a dump.
/// Checkpoint hashes are ephemeral — they must not mutate the rolling <see cref="StateHasher"/>.
/// P0 fingerprint includes protocol version, tick, RNG seed/draw counters, named counters, and the
/// rolling command-stream hash. A 32-byte world-fingerprint slot is reserved (zeros until
/// RIMWORLD_DIR can sample Verse map/world state — see docs/p0-sim-correctness.md).
/// Dump layout: docs/desync-dumps.md (local-only; never uploaded).
/// </summary>
public sealed class DesyncDetector
{
    private readonly StateHasher _hasher;
    private readonly SyncedRand _rand;
    private readonly string _logDirectory;
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _rngCounters = new(StringComparer.Ordinal);
    private byte[] _worldFingerprint = new byte[32];
    private bool _frozen;
    private DesyncInfo? _last;

    public DesyncDetector(StateHasher hasher, SyncedRand rand, string? logDirectory = null)
    {
        _hasher = hasher;
        _rand = rand;
        _logDirectory = logDirectory ?? DesyncDump.DefaultLogDirectory;
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

    /// <summary>
    /// Optional Verse-world / map fingerprint (32 bytes). Callers with <c>RIMWORLD_DIR</c> should
    /// populate this from pawn/thing/RNG-opinion samples; until then the reserved zeros remain.
    /// </summary>
    public void SetWorldFingerprint(ReadOnlySpan<byte> fingerprint32)
    {
        if (fingerprint32.Length != 32)
            throw new ArgumentException("World fingerprint must be exactly 32 bytes.", nameof(fingerprint32));
        lock (_gate)
            fingerprint32.CopyTo(_worldFingerprint);
    }

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
    /// Non-mutating checkpoint fingerprint:
    /// SHA256(protocol ‖ tick ‖ rngSeed ‖ drawCount ‖ namedCounters ‖ rollingCmdHash ‖ worldFingerprint).
    /// </summary>
    public byte[] SnapshotHash(long tick)
    {
        using var inc = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> num = stackalloc byte[8];

        inc.AppendData([(byte)NetProtocol.Version]);

        BinaryPrimitives.WriteInt64LittleEndian(num, tick);
        inc.AppendData(num);

        BinaryPrimitives.WriteInt64LittleEndian(num, _rand.Seed);
        inc.AppendData(num);

        BinaryPrimitives.WriteInt64LittleEndian(num, _rand.DrawCount);
        inc.AppendData(num);

        IReadOnlyDictionary<string, long> counters;
        byte[] world;
        lock (_gate)
        {
            counters = new Dictionary<string, long>(_rngCounters);
            world = (byte[])_worldFingerprint.Clone();
        }

        foreach (var (key, value) in counters.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            inc.AppendData(Encoding.UTF8.GetBytes(key));
            BinaryPrimitives.WriteInt64LittleEndian(num, value);
            inc.AppendData(num);
        }

        inc.AppendData(_hasher.CurrentHash());
        inc.AppendData(world);
        return inc.GetHashAndReset();
    }

    public string SnapshotHashHex(long tick) => Convert.ToHexString(SnapshotHash(tick)).ToLowerInvariant();

    public bool CheckRemote(
        long tick,
        string remoteHashHex,
        IReadOnlyList<MpCommand>? recentCommands = null,
        DesyncDumpContext? dumpContext = null)
    {
        var local = SnapshotHashHex(tick);
        if (string.Equals(local, remoteHashHex, StringComparison.OrdinalIgnoreCase))
            return true;

        Freeze(tick, local, remoteHashHex, recentCommands, dumpContext);
        return false;
    }

    public void Freeze(
        long tick,
        string localHashHex,
        string remoteHashHex,
        IReadOnlyList<MpCommand>? recentCommands = null,
        DesyncDumpContext? dumpContext = null)
    {
        lock (_gate)
        {
            if (_frozen)
                return;
            _frozen = true;

            var cmds = recentCommands ?? Array.Empty<MpCommand>();
            var paths = DesyncDump.WriteAll(
                _logDirectory,
                tick,
                localHashHex,
                remoteHashHex,
                _rand.Seed,
                _rand.DrawCount,
                cmds,
                new Dictionary<string, long>(_rngCounters),
                protocolVersion: NetProtocol.Version,
                context: dumpContext);

            _last = new DesyncInfo
            {
                Tick = tick,
                LocalHashHex = localHashHex,
                RemoteHashHex = remoteHashHex,
                LogPath = paths.LogPath,
                CommandsJsonPath = paths.CommandsJsonPath,
                BatchesJsonPath = paths.BatchesJsonPath,
                ModlistHashShort = DesyncDump.ShortModlistHash(dumpContext?.ModlistHash),
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
            Array.Clear(_worldFingerprint);
            _hasher.Reset();
        }
    }
}

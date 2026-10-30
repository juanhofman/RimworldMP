using RimworldMP.Core.Commands;

namespace RimworldMP.Core.Sync;

/// <summary>
/// Optional session metadata folded into a local-only desync dump.
/// Never leaves the machine — RimworldMP does not upload dumps.
/// </summary>
public sealed class DesyncDumpContext
{
    public int? LocalPeerId { get; init; }
    public IReadOnlyList<int>? PeerIds { get; init; }
    public string? ModlistHash { get; init; }
    public IReadOnlyList<DesyncBatchSnapshot>? RecentBatches { get; init; }
    public int CommandHistoryLimit { get; init; } = DesyncDump.DefaultCommandHistory;
    public int BatchHistoryLimit { get; init; } = DesyncDump.DefaultBatchHistory;
    /// <summary>Live session tick when the dump was written (may exceed the desync tick after a late freeze announce).</summary>
    public long? SessionTickAtDump { get; init; }
    /// <summary>SyncedRand OpinionHash at dump time (diagnostic; checkpoint compare uses published hash).</summary>
    public ulong? RngOpinion { get; init; }
}

/// <summary>Compact view of one recorded command-batch ring entry.</summary>
public sealed class DesyncBatchSnapshot
{
    public required long Tick { get; init; }
    public required int ByteLength { get; init; }
    public required int CommandCount { get; init; }
    /// <summary>Truncated hex of the raw batch frame (tick + count + cmds), for offline diffing.</summary>
    public required string PayloadHexPreview { get; init; }
}

/// <summary>Result paths written by <see cref="DesyncDump"/> (all under a local directory).</summary>
public sealed class DesyncDumpPaths
{
    public required string LogPath { get; init; }
    public required string CommandsJsonPath { get; init; }
    public string? BatchesJsonPath { get; init; }
}

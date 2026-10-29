using RimworldMP.Core.Commands;
using RimworldMP.Core.Join;
using RimworldMP.Core.Sync;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Tests.DeterminismHarness;

/// <summary>
/// Forced fingerprint mismatch must produce a local dump with the documented sections.
/// See docs/desync-dumps.md.
/// </summary>
public class DesyncDumpTests
{
    [Fact]
    public void Forced_fingerprint_mismatch_writes_dump_with_expected_sections()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rimworldmp-desync-rich", Guid.NewGuid().ToString("N"));
        var rand = new SyncedRand(99);
        rand.Next(); // bump draw count
        var hasher = new StateHasher();
        hasher.AddUtf8("peer-a-only");
        var detector = new DesyncDetector(hasher, rand, dir);
        detector.IncrementCounter("Rand.Value", 3);
        detector.IncrementCounter("ticks", 60);

        var cmds = new List<MpCommand>
        {
            new(CommandOpcode.Pause, 58, 0, CommandPayloads.Pause(true)),
            new(CommandOpcode.Draft, 59, 1, CommandPayloads.Draft(42, true)),
            new(CommandOpcode.BillSet, 60, 0, CommandPayloads.BillSet(mapId: 1, 1, 0, "CookMeal", 0, 1)),
        };

        var emptyBatch = CommandBatch.Empty(58).ToBytes();
        var draftBatch = new CommandBatch(59, new[] { cmds[1] }).ToBytes();
        var batches = new[]
        {
            DesyncDump.SnapshotBatch(58, emptyBatch),
            DesyncDump.SnapshotBatch(59, draftBatch),
        };

        const string modlist = "f2046ab21e526aae4469bdf1418310b553a2c34b9b9b617cc148a00d76e963f6";
        var ctx = new DesyncDumpContext
        {
            LocalPeerId = 0,
            PeerIds = new[] { 0, 1 },
            ModlistHash = modlist,
            RecentBatches = batches,
        };

        var ok = detector.CheckRemote(
            60,
            "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef",
            cmds,
            ctx);

        Assert.False(ok);
        Assert.True(detector.IsFrozen);
        Assert.NotNull(detector.LastDesync);
        Assert.True(File.Exists(detector.LastDesync!.LogPath));

        var text = File.ReadAllText(detector.LastDesync.LogPath);
        AssertExpectedSections(text);

        Assert.Contains("protocol=3", text);
        Assert.Contains("tick=60", text);
        Assert.Contains("localPeerId=0", text);
        Assert.Contains("peerIds=0,1", text);
        Assert.Contains($"modlistHashShort={DesyncDump.ShortModlistHash(modlist)}", text);
        Assert.Contains($"modlistHash={modlist}", text);
        Assert.Contains("rngSeed=99", text);
        Assert.Contains("rngDraws=1", text);
        Assert.Contains("Rand.Value=3", text);
        Assert.Contains("ticks=60", text);
        Assert.Contains("recentCommands=3", text);
        Assert.Contains("op=BillSet", text);
        Assert.Contains("recentBatches=2", text);
        Assert.Contains("localOnly=true", text);
        Assert.Contains("upload=never", text);
        Assert.Equal(DesyncDump.ShortModlistHash(modlist), detector.LastDesync.ModlistHashShort);

        Assert.True(File.Exists(detector.LastDesync.CommandsJsonPath!));
        Assert.True(File.Exists(detector.LastDesync.BatchesJsonPath!));
        Assert.Contains("BillSet", File.ReadAllText(detector.LastDesync.CommandsJsonPath!));
        Assert.Contains("\"tick\": 59", File.ReadAllText(detector.LastDesync.BatchesJsonPath!));
    }

    [Fact]
    public void DesyncDump_WriteAll_omits_batches_json_when_ring_empty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rimworldmp-desync-empty-ring", Guid.NewGuid().ToString("N"));
        var paths = DesyncDump.WriteAll(
            dir,
            tick: 1,
            localHashHex: "aa",
            remoteHashHex: "bb",
            rngSeed: 1,
            rngDraws: 0,
            recentCommands: Array.Empty<MpCommand>(),
            protocolVersion: NetProtocol.Version,
            context: new DesyncDumpContext { LocalPeerId = 1, ModlistHash = "abcd" });

        Assert.True(File.Exists(paths.LogPath));
        Assert.True(File.Exists(paths.CommandsJsonPath));
        Assert.Null(paths.BatchesJsonPath);
        var text = File.ReadAllText(paths.LogPath);
        Assert.Contains("section=recentBatches", text);
        Assert.Contains("recentBatches=0", text);
        Assert.DoesNotContain("batchesJson=", text);
    }

    [Fact]
    public void CommandLog_SnapshotRecent_feeds_dump_ring()
    {
        var log = new CommandLog(capacity: 8);
        for (var t = 1; t <= 5; t++)
            log.Record(t, CommandBatch.Empty(t).ToBytes());

        var snap = log.SnapshotRecent(3);
        Assert.Equal(3, snap.Count);
        Assert.Equal(3, snap[0].Tick);
        Assert.Equal(5, snap[2].Tick);
        Assert.Equal(0, snap[0].CommandCount);

        var dir = Path.Combine(Path.GetTempPath(), "rimworldmp-desync-ring", Guid.NewGuid().ToString("N"));
        var paths = DesyncDump.WriteAll(
            dir,
            5,
            "aa",
            "bb",
            7,
            2,
            recentCommands: new[] { new MpCommand(CommandOpcode.Pause, 5, 0, CommandPayloads.Pause(false)) },
            rngCounters: new Dictionary<string, long> { ["ticks"] = 5 },
            context: new DesyncDumpContext
            {
                LocalPeerId = 0,
                PeerIds = new[] { 0 },
                ModlistHash = "modhashlongenough",
                RecentBatches = snap,
            });

        var text = File.ReadAllText(paths.LogPath);
        AssertExpectedSections(text);
        Assert.Contains("recentBatches=3", text);
        Assert.NotNull(paths.BatchesJsonPath);
    }

    [Fact]
    public void ShortModlistHash_truncates_to_twelve()
    {
        Assert.Equal("—", DesyncDump.ShortModlistHash(null));
        Assert.Equal("—", DesyncDump.ShortModlistHash(""));
        Assert.Equal("abc", DesyncDump.ShortModlistHash("abc"));
        Assert.Equal("0123456789ab", DesyncDump.ShortModlistHash("0123456789abcdef"));
    }

    private static void AssertExpectedSections(string text)
    {
        Assert.Contains("section=header", text);
        Assert.Contains("section=session", text);
        Assert.Contains("section=fingerprint", text);
        Assert.Contains("section=rng", text);
        Assert.Contains("section=recentCommands", text);
        Assert.Contains("section=recentBatches", text);
        Assert.Contains("section=files", text);
        Assert.Contains("desync detected", text, StringComparison.OrdinalIgnoreCase);
    }
}

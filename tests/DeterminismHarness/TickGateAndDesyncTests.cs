using RimworldMP.Core.Commands;
using RimworldMP.Core.Sync;

namespace RimworldMP.Tests.DeterminismHarness;

public class TickGateAndDesyncTests
{
    [Fact]
    public void TickGate_stalls_until_batch_present()
    {
        var gate = new TickGate();
        Assert.False(gate.TryBeginTick(out _));

        gate.EnqueueBatch(CommandBatch.Empty(1));
        Assert.True(gate.TryBeginTick(out var batch));
        Assert.Equal(1, batch.Tick);
        Assert.Equal(2, gate.NextTick);
        Assert.False(gate.TryBeginTick(out _));
    }

    [Fact]
    public void DesyncDetector_writes_log_and_freezes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rimworldmp-desync-tests", Guid.NewGuid().ToString("N"));
        var rand = new SyncedRand(1);
        var hasher = new StateHasher();
        var detector = new DesyncDetector(hasher, rand, dir);

        hasher.AddUtf8("local-only-state");
        var ok = detector.CheckRemote(12, "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef");
        Assert.False(ok);
        Assert.True(detector.IsFrozen);
        Assert.NotNull(detector.LastDesync);
        Assert.True(File.Exists(detector.LastDesync!.LogPath));

        var text = File.ReadAllText(detector.LastDesync.LogPath);
        Assert.Contains("desync detected", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tick=12", text);
    }

    [Fact]
    public void StateHasher_is_deterministic_for_same_inputs()
    {
        var a = new StateHasher();
        var b = new StateHasher();
        var cmd = new MpCommand(CommandOpcode.Pause, 3, 0, CommandPayloads.Pause(true));
        a.AddCommand(cmd);
        a.AddTick(3);
        a.AddDrawCount(7);
        b.AddCommand(cmd);
        b.AddTick(3);
        b.AddDrawCount(7);
        Assert.Equal(a.CurrentHashHex(), b.CurrentHashHex());
    }

    [Fact]
    public void SnapshotHash_does_not_mutate_rolling_hasher()
    {
        var rand = new SyncedRand(42);
        var hasher = new StateHasher();
        hasher.AddUtf8("base");
        var detector = new DesyncDetector(hasher, rand, Path.Combine(Path.GetTempPath(), "rimworldmp-snap"));

        var before = hasher.CurrentHashHex();
        var snap1 = detector.SnapshotHashHex(10);
        var mid = hasher.CurrentHashHex();
        var snap2 = detector.SnapshotHashHex(10);
        var after = hasher.CurrentHashHex();

        Assert.Equal(before, mid);
        Assert.Equal(mid, after);
        Assert.Equal(snap1, snap2);
    }

    [Fact]
    public void Matching_remote_hash_does_not_freeze()
    {
        var rand = new SyncedRand(7);
        var hasher = new StateHasher();
        hasher.AddUtf8("same");
        var detector = new DesyncDetector(hasher, rand, Path.Combine(Path.GetTempPath(), "rimworldmp-match"));
        var local = detector.SnapshotHashHex(5);
        Assert.True(detector.CheckRemote(5, local));
        Assert.False(detector.IsFrozen);
    }
}

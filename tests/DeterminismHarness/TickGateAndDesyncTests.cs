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
}

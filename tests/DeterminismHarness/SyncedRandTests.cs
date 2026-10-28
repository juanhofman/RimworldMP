using RimworldMP.Core.Sync;

namespace RimworldMP.Tests.DeterminismHarness;

public class SyncedRandTests
{
    [Fact]
    public void Same_seed_and_tick_produces_identical_streams()
    {
        var a = new SyncedRand(12345);
        var b = new SyncedRand(12345);
        a.ReseedForTick(10);
        b.ReseedForTick(10);

        for (var i = 0; i < 64; i++)
            Assert.Equal(a.Next(), b.Next());

        Assert.Equal(a.DrawCount, b.DrawCount);
    }

    [Fact]
    public void Different_ticks_diverge()
    {
        var a = new SyncedRand(99);
        var b = new SyncedRand(99);
        a.ReseedForTick(1);
        b.ReseedForTick(2);
        Assert.NotEqual(a.Next(), b.Next());
    }
}

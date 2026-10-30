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
        Assert.Equal(a.OpinionHash, b.OpinionHash);
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

    [Fact]
    public void Empty_int_range_returns_min_like_Verse_not_throw()
    {
        var rng = new SyncedRand(1);
        Assert.Equal(5, rng.Next(5, 5));
        Assert.Equal(5, rng.Next(5, 4));
        Assert.Equal(0, rng.Next(0));
        Assert.Equal(7, rng.RangeInclusive(7, 6)); // maxInclusive < min → Next(7,7) → 7
    }

    [Fact]
    public void Chance_Bool_Range_Element_are_deterministic()
    {
        var a = new SyncedRand(7);
        var b = new SyncedRand(7);
        a.ReseedForTick(3);
        b.ReseedForTick(3);

        Assert.Equal(a.Chance(0.3f), b.Chance(0.3f));
        Assert.Equal(a.Bool(), b.Bool());
        Assert.Equal(a.Range(1.0f, 5.0f), b.Range(1.0f, 5.0f));
        Assert.Equal(a.RangeInclusive(1, 10), b.RangeInclusive(1, 10));
        var items = new[] { "a", "b", "c", "d" };
        Assert.Equal(a.Element(items), b.Element(items));
        Assert.Equal(a.DrawCount, b.DrawCount);
        Assert.Equal(a.OpinionHash, b.OpinionHash);
    }

    [Fact]
    public void PushPop_state_restores_stream()
    {
        var rng = new SyncedRand(55);
        rng.ReseedForTick(1);
        var first = rng.Next();
        rng.PushState(1234);
        Assert.Equal(1, rng.StateStackDepth);
        _ = rng.Next();
        _ = rng.Next();
        rng.PopState();
        Assert.Equal(0, rng.StateStackDepth);
        var second = rng.Next();
        var mirror = new SyncedRand(55);
        mirror.ReseedForTick(1);
        Assert.Equal(first, mirror.Next());
        Assert.Equal(second, mirror.Next());
        Assert.Equal(rng.OpinionHash, mirror.OpinionHash);
    }

    [Fact]
    public void Draw_order_affects_desync_fingerprint()
    {
        var seed = 4242L;
        var tick = 60L;

        string Fingerprint(Action<SyncedRand> draws)
        {
            var rand = new SyncedRand(seed);
            var hasher = new StateHasher();
            hasher.AddUtf8("cmd-stream");
            var detector = new DesyncDetector(hasher, rand, Path.Combine(Path.GetTempPath(), "rimworldmp-rand-order"));
            rand.ReseedForTick(tick);
            draws(rand);
            detector.IncrementCounter("Rand.Value", rand.DrawCount);
            return detector.SnapshotHashHex(tick);
        }

        var a = Fingerprint(r =>
        {
            _ = r.NextFloat();
            _ = r.Chance(0.25f);
            _ = r.RangeInclusive(1, 5);
        });
        var b = Fingerprint(r =>
        {
            _ = r.NextFloat();
            _ = r.Chance(0.25f);
            _ = r.RangeInclusive(1, 5);
        });
        Assert.Equal(a, b);

        // Int draw before float draws → different Random API consumption + OpinionHash.
        // (Chance vs NextFloat alone both call NextDouble, so swapping those would not diverge.)
        var reordered = Fingerprint(r =>
        {
            _ = r.RangeInclusive(1, 5);
            _ = r.NextFloat();
            _ = r.Chance(0.25f);
        });
        Assert.NotEqual(a, reordered);

        var extra = Fingerprint(r =>
        {
            _ = r.NextFloat();
            _ = r.Chance(0.25f);
            _ = r.RangeInclusive(1, 5);
            _ = r.Bool();
        });
        Assert.NotEqual(a, extra);
    }
}

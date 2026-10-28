namespace RimworldMP.Core.Sync;

/// <summary>
/// Deterministic RNG stream for the sim path. Seeded from session seed; optionally reseeded per tick.
/// Harmony patches on Verse.Rand should forward critical draws here when a session is active.
/// </summary>
public sealed class SyncedRand
{
    private readonly object _gate = new();
    private Random _rng;
    private long _seed;
    private long _drawCount;

    public SyncedRand(long sessionSeed)
    {
        _seed = sessionSeed;
        _rng = new Random(HashSeed(sessionSeed, 0));
    }

    public long Seed => _seed;
    public long DrawCount
    {
        get { lock (_gate) return _drawCount; }
    }

    public void ReseedForTick(long tick)
    {
        lock (_gate)
        {
            _rng = new Random(HashSeed(_seed, tick));
        }
    }

    public int Next()
    {
        lock (_gate)
        {
            _drawCount++;
            return _rng.Next();
        }
    }

    public int Next(int maxExclusive)
    {
        lock (_gate)
        {
            _drawCount++;
            return _rng.Next(maxExclusive);
        }
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        lock (_gate)
        {
            _drawCount++;
            return _rng.Next(minInclusive, maxExclusive);
        }
    }

    public double NextDouble()
    {
        lock (_gate)
        {
            _drawCount++;
            return _rng.NextDouble();
        }
    }

    public float NextFloat() => (float)NextDouble();

    public void ResetDrawCount()
    {
        lock (_gate) _drawCount = 0;
    }

    private static int HashSeed(long sessionSeed, long tick)
    {
        unchecked
        {
            ulong x = (ulong)sessionSeed;
            x ^= (ulong)tick * 0x9E3779B97F4A7C15UL;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (int)x ^ (int)(x >> 32);
        }
    }
}

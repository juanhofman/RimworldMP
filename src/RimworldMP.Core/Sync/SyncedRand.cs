namespace RimworldMP.Core.Sync;

/// <summary>
/// Deterministic RNG stream for the sim path. Seeded from session seed; optionally reseeded per tick.
/// Harmony patches on Verse.Rand should forward critical draws here when a session is active.
/// Wrappers mirror common Verse.Rand entry points so uncovered call sites have a synced home.
/// <see cref="OpinionHash"/> fingerprints draw <em>values</em> (order-sensitive), not only draw count.
/// </summary>
public sealed class SyncedRand
{
    private readonly object _gate = new();
    private readonly Stack<(Random Rng, long DrawCount, ulong Opinion)> _stateStack = new();
    private Random _rng;
    private long _seed;
    private long _drawCount;
    private ulong _opinion;

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

    /// <summary>
    /// Rolling fingerprint of draw results (order-sensitive). Peers that draw in different
    /// orders diverge here even when <see cref="DrawCount"/> matches.
    /// </summary>
    public ulong OpinionHash
    {
        get { lock (_gate) return _opinion; }
    }

    public int StateStackDepth
    {
        get { lock (_gate) return _stateStack.Count; }
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
            var v = _rng.Next();
            MixOpinion(unchecked((ulong)(uint)v));
            return v;
        }
    }

    public int Next(int maxExclusive)
    {
        lock (_gate)
        {
            _drawCount++;
            var v = _rng.Next(maxExclusive);
            MixOpinion(unchecked((ulong)(uint)v));
            return v;
        }
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        lock (_gate)
        {
            _drawCount++;
            var v = _rng.Next(minInclusive, maxExclusive);
            MixOpinion(unchecked((ulong)(uint)v));
            return v;
        }
    }

    public double NextDouble()
    {
        lock (_gate)
        {
            _drawCount++;
            var v = _rng.NextDouble();
            MixOpinion(BitConverter.DoubleToUInt64Bits(v));
            return v;
        }
    }

    public float NextFloat() => (float)NextDouble();

    /// <summary>Verse.Rand.Chance equivalent — counts as one draw.</summary>
    public bool Chance(float probability)
    {
        if (probability <= 0f)
            return false;
        if (probability >= 1f)
            return true;
        return NextFloat() < probability;
    }

    /// <summary>Verse.Rand.Bool equivalent.</summary>
    public bool Bool() => Chance(0.5f);

    /// <summary>Inclusive float range (one draw).</summary>
    public float Range(float minInclusive, float maxInclusive)
    {
        if (minInclusive >= maxInclusive)
            return minInclusive;
        return minInclusive + NextFloat() * (maxInclusive - minInclusive);
    }

    /// <summary>Inclusive int range via <see cref="Next(int, int)"/> (maxExclusive = maxInclusive + 1).</summary>
    public int RangeInclusive(int minInclusive, int maxInclusive) =>
        Next(minInclusive, maxInclusive + 1);

    /// <summary>Pick one element; empty throws. Counts as one draw.</summary>
    public T Element<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            throw new InvalidOperationException("SyncedRand.Element requires a non-empty list.");
        return items[Next(items.Count)];
    }

    /// <summary>
    /// Push current RNG state (Verse.Rand.PushState analogue). Nested with PopState.
    /// Does not consume a draw; subsequent draws use a derived seed until pop.
    /// </summary>
    public void PushState(int? seedOverride = null)
    {
        lock (_gate)
        {
            _stateStack.Push((_rng, _drawCount, _opinion));
            var pushSeed = seedOverride ?? HashSeed(_seed, _drawCount ^ (_stateStack.Count * 0x9E3779B9));
            _rng = new Random(pushSeed);
        }
    }

    public void PopState()
    {
        lock (_gate)
        {
            if (_stateStack.Count == 0)
                throw new InvalidOperationException("SyncedRand.PopState with empty stack.");
            var (rng, draws, opinion) = _stateStack.Pop();
            _rng = rng;
            _drawCount = draws;
            _opinion = opinion;
        }
    }

    public void ResetDrawCount()
    {
        lock (_gate)
        {
            _drawCount = 0;
            _opinion = 0;
        }
    }

    /// <summary>Replace the session seed (e.g. after JoinAck delivers the host seed).</summary>
    public void ResetSession(long sessionSeed)
    {
        lock (_gate)
        {
            _seed = sessionSeed;
            _drawCount = 0;
            _opinion = 0;
            _stateStack.Clear();
            _rng = new Random(HashSeed(sessionSeed, 0));
        }
    }

    private void MixOpinion(ulong sample)
    {
        // SplitMix64-style mix so draw order changes the fingerprint.
        unchecked
        {
            _opinion ^= sample + 0x9E3779B97F4A7C15UL + (_opinion << 6) + (_opinion >> 2);
            _opinion = (_opinion ^ (_opinion >> 30)) * 0xBF58476D1CE4E5B9UL;
            _opinion = (_opinion ^ (_opinion >> 27)) * 0x94D049BB133111EBUL;
            _opinion ^= _opinion >> 31;
        }
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

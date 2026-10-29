namespace RimworldMP.Net;

/// <summary>
/// Exponential backoff for LiteNetLib client connect / reconnect attempts.
/// Pure schedule helper — safe to unit-test offline without a live host.
/// </summary>
public sealed class ReconnectPolicy
{
    public static ReconnectPolicy Default { get; } = new();

    /// <summary>Disabled policy (single attempt, no delay).</summary>
    public static ReconnectPolicy None { get; } = new()
    {
        MaxAttempts = 1,
        InitialDelay = TimeSpan.Zero,
        MaxDelay = TimeSpan.Zero,
        Multiplier = 1.0,
        JitterFraction = 0,
    };

    /// <summary>Total connect attempts (including the first). Clamped to ≥ 1.</summary>
    public int MaxAttempts { get; set; } = 5;

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Exponential multiplier applied after each failed attempt.</summary>
    public double Multiplier { get; set; } = 2.0;

    /// <summary>0–1 fraction of delay used as symmetric random jitter (offline-deterministic when 0).</summary>
    public double JitterFraction { get; set; } = 0.1;

    /// <summary>
    /// Delay before attempt <paramref name="attemptIndex"/> (0-based; delay before first attempt is zero).
    /// </summary>
    public TimeSpan DelayBeforeAttempt(int attemptIndex, Random? rng = null)
    {
        if (attemptIndex <= 0)
            return TimeSpan.Zero;

        var attempts = Math.Max(1, MaxAttempts);
        if (attemptIndex >= attempts)
            attemptIndex = attempts - 1;

        var ms = InitialDelay.TotalMilliseconds * Math.Pow(Math.Max(1.0, Multiplier), attemptIndex - 1);
        ms = Math.Min(ms, MaxDelay.TotalMilliseconds);
        if (ms <= 0)
            return TimeSpan.Zero;

        var jitter = Math.Clamp(JitterFraction, 0, 1);
        if (jitter > 0)
        {
            rng ??= Random.Shared;
            var factor = 1.0 + ((rng.NextDouble() * 2) - 1) * jitter;
            ms *= factor;
            ms = Math.Max(0, Math.Min(ms, MaxDelay.TotalMilliseconds * (1 + jitter)));
        }

        return TimeSpan.FromMilliseconds(ms);
    }

    /// <summary>Enumerate delays for attempts 0..MaxAttempts-1 (deterministic when <see cref="JitterFraction"/> is 0).</summary>
    public IEnumerable<TimeSpan> EnumerateDelays()
    {
        var n = Math.Max(1, MaxAttempts);
        for (var i = 0; i < n; i++)
            yield return DelayBeforeAttempt(i, rng: null);
    }
}

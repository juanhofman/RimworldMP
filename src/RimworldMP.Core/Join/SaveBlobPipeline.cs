namespace RimworldMP.Core.Join;

/// <summary>
/// Offline-safe scaffolding for JIP save blob create → chunk → reassemble → load.
/// Live Verse load still requires <c>RIMWORLD_DIR</c> / game-ref <c>VerseSaveLoad</c>.
/// </summary>
public static class SaveBlobPipeline
{
    /// <summary>
    /// Host: capture via <see cref="ISaveLoad"/> (or empty lobby hello) and wrap a join point.
    /// </summary>
    public static JoinPoint Create(
        ISaveLoad? saveLoad,
        long nextTick,
        long sessionSeed,
        long commandWatermark,
        string modlistHash)
    {
        var blob = saveLoad?.CaptureSaveBlob() ?? Array.Empty<byte>();
        return new JoinPoint(nextTick, sessionSeed, commandWatermark, modlistHash, blob);
    }

    /// <summary>
    /// Client: load a completed join point. Empty blob = lobby hello (no-op).
    /// Non-empty requires an <see cref="ISaveLoad"/> — fails loud otherwise.
    /// </summary>
    public static void Load(JoinPoint joinPoint, ISaveLoad? loader = null)
    {
        ArgumentNullException.ThrowIfNull(joinPoint);
        GameLoad.ApplyJoinPointBlob(joinPoint.SaveBlob, loader ?? GameLoad.Current);
    }

    /// <summary>
    /// Round-trip helper for harnesses: create → chunk → reassemble → load.
    /// Throws on incomplete reassembly or missing loader for non-empty blobs.
    /// </summary>
    public static JoinPoint RoundTrip(
        ISaveLoad saveLoad,
        long nextTick,
        long sessionSeed,
        long commandWatermark,
        string modlistHash,
        ISaveLoad? loadTarget = null)
    {
        ArgumentNullException.ThrowIfNull(saveLoad);
        var created = Create(saveLoad, nextTick, sessionSeed, commandWatermark, modlistHash);
        var transfer = new JoinPointTransfer();
        transfer.AcceptHeader(created.ToHeaderBytes());
        foreach (var chunk in created.EnumerateChunks())
            transfer.AcceptChunk(chunk);
        var completed = transfer.CompleteOrThrow();
        Load(completed, loadTarget ?? saveLoad);
        return completed;
    }
}

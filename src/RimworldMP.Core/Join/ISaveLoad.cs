namespace RimworldMP.Core.Join;

/// <summary>
/// Host capture / client load of a RimWorld freeze-frame for join-in-progress.
/// Transfer-only is not enough: a non-empty save blob must be loaded via this hook.
/// </summary>
public interface ISaveLoad
{
    /// <summary>
    /// Host: serialize the current game. Return null or empty for lobby-only hello
    /// (no colony loaded yet).
    /// </summary>
    byte[]? CaptureSaveBlob();

    /// <summary>
    /// Client: load a freeze-frame. Must not be a silent no-op when <paramref name="blob"/>
    /// is non-empty — implementors must apply bytes or throw.
    /// </summary>
    void LoadSaveBlob(byte[] blob);
}

/// <summary>Thrown when a join point carries save bytes but no <see cref="ISaveLoad"/> is registered.</summary>
public sealed class MissingSaveLoadException : InvalidOperationException
{
    public MissingSaveLoadException(int blobLength)
        : base(
            $"RimworldMP JIP received {blobLength} save byte(s) but no ISaveLoad is registered. " +
            "Wire GameLoad / VerseSaveLoad before accepting mid-session joins; transfer-only is not supported.")
    {
        BlobLength = blobLength;
    }

    public int BlobLength { get; }
}

/// <summary>In-memory save/load for DeterminismHarness and offline stubs.</summary>
public sealed class MemorySaveLoad : ISaveLoad
{
    public byte[]? NextCapture { get; set; }
    public byte[]? LastLoaded { get; private set; }
    public int LoadCount { get; private set; }

    public byte[]? CaptureSaveBlob() => NextCapture;

    public void LoadSaveBlob(byte[] blob)
    {
        ArgumentNullException.ThrowIfNull(blob);
        LastLoaded = blob.ToArray();
        LoadCount++;
    }
}

/// <summary>Process-wide save/load hook used by lobby + <see cref="MpSession"/>.</summary>
public static class GameLoad
{
    /// <summary>Active adapter (Verse when game refs exist; MemorySaveLoad in harness).</summary>
    public static ISaveLoad? Current { get; set; }

    /// <summary>
    /// Apply a received join-point blob. Empty blobs are lobby hellos (no load).
    /// Non-empty blobs require <see cref="Current"/> or an explicit <paramref name="loader"/>.
    /// </summary>
    public static void ApplyJoinPointBlob(byte[] saveBlob, ISaveLoad? loader = null)
    {
        ArgumentNullException.ThrowIfNull(saveBlob);
        if (saveBlob.Length == 0)
            return;

        var effective = loader ?? Current;
        if (effective is null)
            throw new MissingSaveLoadException(saveBlob.Length);

        effective.LoadSaveBlob(saveBlob);
    }
}

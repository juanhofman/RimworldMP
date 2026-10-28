using RimworldMP.Core.Join;

#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP.Join;

/// <summary>
/// Verse-backed <see cref="ISaveLoad"/> for join-in-progress. Compiled only with game refs;
/// signatures need a live <c>RIMWORLD_DIR</c> install to validate against RimWorld 1.7.
/// </summary>
public sealed class VerseSaveLoad : ISaveLoad
{
#if !RIMWORLDMP_NO_GAME_REFS
    public byte[]? CaptureSaveBlob()
    {
        // Mid-session JIP: write a temp save under the game save folder, then read bytes.
        // Empty return = lobby hello (no Game yet). Validate SaveGame / GenFilePaths on install.
        if (Current.Game is null)
            return Array.Empty<byte>();

        var name = $"RimworldMP_JIP_{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        try
        {
            GameDataSaveLoader.SaveGame(name);
        }
        catch (Exception ex)
        {
            Log.Error($"[RimworldMP] JIP capture SaveGame failed: {ex}");
            throw;
        }

        var path = GenFilePaths.FilePathForSavedGame(name);
        if (!File.Exists(path))
        {
            // Fallback: some builds append .rws explicitly in the path helper already.
            var alt = path.EndsWith(".rws", StringComparison.OrdinalIgnoreCase) ? path : path + ".rws";
            if (!File.Exists(alt))
                throw new InvalidOperationException($"JIP capture wrote no save file at '{path}'.");
            path = alt;
        }

        return File.ReadAllBytes(path);
    }

    public void LoadSaveBlob(byte[] blob)
    {
        ArgumentNullException.ThrowIfNull(blob);
        if (blob.Length == 0)
            return;

        Directory.CreateDirectory(GenFilePaths.SaveDataFolderPath);
        var name = "RimworldMP_JIP_incoming";
        var path = GenFilePaths.FilePathForSavedGame(name);
        File.WriteAllBytes(path, blob);
        try
        {
            GameDataSaveLoader.LoadGame(name);
        }
        catch (Exception ex)
        {
            Log.Error($"[RimworldMP] JIP LoadGame failed: {ex}");
            throw;
        }
    }
#else
    public byte[]? CaptureSaveBlob() =>
        throw new InvalidOperationException("VerseSaveLoad requires RIMWORLD_DIR / game refs.");

    public void LoadSaveBlob(byte[] blob) =>
        throw new InvalidOperationException(
            $"VerseSaveLoad requires RIMWORLD_DIR / game refs (blob={blob?.Length ?? 0}B).");
#endif
}

/// <summary>Factory for the in-mod JIP loader under NO_GAME_REFS vs live Verse.</summary>
public static class GameLoadHooks
{
    public static ISaveLoad? TryCreateDefault()
    {
#if !RIMWORLDMP_NO_GAME_REFS
        return new VerseSaveLoad();
#else
        // Offline / CI: no Verse. Callers must register MemorySaveLoad when testing non-empty blobs.
        return null;
#endif
    }
}

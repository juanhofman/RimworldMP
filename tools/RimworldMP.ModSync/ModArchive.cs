using System.IO.Compression;

namespace RimworldMP.ModSync;

/// <summary>Zip a mod folder for LAN transfer; extract under a sandboxed Mods destination.</summary>
public static class ModArchive
{
    public static byte[] CreateZipBytes(string modRoot)
    {
        if (!Directory.Exists(modRoot))
            throw new DirectoryNotFoundException(modRoot);

        var rootFull = Path.GetFullPath(modRoot);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.EnumerateFiles(rootFull, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(rootFull, file).Replace('\\', '/');
                if (rel.Contains("..", StringComparison.Ordinal))
                    continue;
                zip.CreateEntryFromFile(file, rel, CompressionLevel.Optimal);
            }
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Extract zip into <paramref name="modsRoot"/>/<paramref name="folderName"/>,
    /// replacing any previous folder. Path-sandbox enforced.
    /// </summary>
    public static string ExtractZipToMods(byte[] zipBytes, string modsRoot, string folderName)
    {
        Directory.CreateDirectory(modsRoot);
        var safeName = PathSandbox.SafeFolderName(folderName);
        var dest = PathSandbox.RequireUnderRoot(modsRoot, safeName);

        if (Directory.Exists(dest))
            Directory.Delete(dest, recursive: true);
        Directory.CreateDirectory(dest);

        using var ms = new MemoryStream(zipBytes, writable: false);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
            {
                // directory entry
                PathSandbox.EnsureEntryRelativeSafe(entry.FullName);
                var dirPath = PathSandbox.RequireUnderRoot(dest, entry.FullName.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(dirPath);
                continue;
            }

            PathSandbox.EnsureEntryRelativeSafe(entry.FullName);
            var outPath = PathSandbox.RequireUnderRoot(dest, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            var parent = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            using var entryStream = entry.Open();
            using var fileStream = File.Create(outPath);
            entryStream.CopyTo(fileStream);
        }

        return dest;
    }
}

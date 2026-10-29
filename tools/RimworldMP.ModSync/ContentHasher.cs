using System.Security.Cryptography;
using System.Text;

namespace RimworldMP.ModSync;

/// <summary>
/// Deterministic content fingerprint for a mod folder: sorted relative paths + file SHA256 digests.
/// </summary>
public static class ContentHasher
{
    private static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "obj",
        "__MACOSX",
    };

    public static string HashDirectory(string modRoot)
    {
        if (!Directory.Exists(modRoot))
            throw new DirectoryNotFoundException(modRoot);

        var rootFull = Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var entries = new List<(string Rel, string FileHash)>();

        foreach (var file in Directory.EnumerateFiles(rootFull, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(rootFull, file).Replace('\\', '/');
            if (ShouldSkip(rel))
                continue;
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            entries.Add((rel, fileHash));
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Rel, b.Rel));

        var sb = new StringBuilder();
        foreach (var (rel, fileHash) in entries)
        {
            sb.Append(rel);
            sb.Append('\n');
            sb.Append(fileHash);
            sb.Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    public static long MeasureByteSize(string modRoot)
    {
        if (!Directory.Exists(modRoot))
            return 0;
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(modRoot, file).Replace('\\', '/');
            if (ShouldSkip(rel))
                continue;
            total += new FileInfo(file).Length;
        }

        return total;
    }

    private static bool ShouldSkip(string relativePath)
    {
        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (SkipDirNames.Contains(p))
                return true;
        }

        // Skip transient OS junk
        var name = Path.GetFileName(relativePath);
        if (name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

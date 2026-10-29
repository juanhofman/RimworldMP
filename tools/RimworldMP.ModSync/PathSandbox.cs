namespace RimworldMP.ModSync;

/// <summary>Reject path traversal; keep all writes under an allowed Mods root.</summary>
public static class PathSandbox
{
    public static string RequireUnderRoot(string rootDir, string relativeOrChild)
    {
        if (string.IsNullOrWhiteSpace(rootDir))
            throw new ArgumentException("Mods root is required.", nameof(rootDir));
        if (string.IsNullOrWhiteSpace(relativeOrChild))
            throw new ArgumentException("Path is required.", nameof(relativeOrChild));

        var rootFull = Path.GetFullPath(rootDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        // Disallow absolute inputs that escape; treat as relative segment only.
        var candidate = relativeOrChild;
        if (Path.IsPathRooted(candidate))
            throw new InvalidOperationException($"Absolute paths are not allowed: {relativeOrChild}");

        if (candidate.Contains("..", StringComparison.Ordinal) ||
            candidate.Contains(':', StringComparison.Ordinal) ||
            candidate.StartsWith('~'))
            throw new InvalidOperationException($"Path traversal rejected: {relativeOrChild}");

        var combined = Path.GetFullPath(Path.Combine(rootDir, candidate));
        var combinedWithSep = combined.EndsWith(Path.DirectorySeparatorChar)
            ? combined
            : combined + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(combined.TrimEnd(Path.DirectorySeparatorChar),
                rootFull.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase) &&
            !combinedWithSep.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path escapes Mods root: {relativeOrChild}");
        }

        return combined;
    }

    /// <summary>Sanitize a packageId into a safe folder name under Mods.</summary>
    public static string SafeFolderName(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("packageId required.", nameof(packageId));

        var s = packageId.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        s = s.Replace("..", "_", StringComparison.Ordinal);
        if (s is "." or ".." || s.Length == 0)
            throw new InvalidOperationException("Invalid packageId for folder name.");
        return s;
    }

    public static void EnsureEntryRelativeSafe(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            throw new InvalidOperationException("Empty archive entry.");
        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains("://", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unsafe archive entry: {entryName}");
        foreach (var part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is "." or "..")
                throw new InvalidOperationException($"Path traversal in archive entry: {entryName}");
        }
    }
}

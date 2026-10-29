using System.Xml.Linq;

namespace RimworldMP.ModSync;

/// <summary>Parse RimWorld mod About/About.xml metadata.</summary>
public static class AboutXmlParser
{
    public sealed record AboutInfo(
        string PackageId,
        string Name,
        string Version,
        string? WorkshopUrl);

    public static AboutInfo? TryParse(string aboutXmlPath)
    {
        if (!File.Exists(aboutXmlPath))
            return null;

        try
        {
            var doc = XDocument.Load(aboutXmlPath);
            var root = doc.Root;
            if (root is null)
                return null;

            var packageId = root.Element("packageId")?.Value?.Trim() ?? string.Empty;
            if (packageId.Length == 0)
                return null;

            var name = root.Element("name")?.Value?.Trim() ?? packageId;
            var version = root.Element("modVersion")?.Value?.Trim()
                ?? root.Element("version")?.Value?.Trim()
                ?? string.Empty;
            var workshop = root.Element("url")?.Value?.Trim()
                ?? root.Descendants("steamWorkshopUrl").FirstOrDefault()?.Value?.Trim();

            return new AboutInfo(packageId, name, version, workshop);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Extract numeric Workshop file id from a steam:// or community URL when present.</summary>
    public static string? TryExtractWorkshopId(string? urlOrId)
    {
        if (string.IsNullOrWhiteSpace(urlOrId))
            return null;

        var s = urlOrId.Trim();
        if (ulong.TryParse(s, out _))
            return s;

        // steam://url/CommunityFilePage/2009463077
        var idx = s.LastIndexOf('/');
        if (idx >= 0 && idx < s.Length - 1)
        {
            var tail = s[(idx + 1)..];
            if (ulong.TryParse(tail, out _))
                return tail;
        }

        // https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077
        const string marker = "id=";
        var idIdx = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idIdx >= 0)
        {
            var rest = s[(idIdx + marker.Length)..];
            var end = rest.IndexOfAny(new[] { '&', '#', ' ' });
            var token = end < 0 ? rest : rest[..end];
            if (ulong.TryParse(token, out _))
                return token;
        }

        return null;
    }
}

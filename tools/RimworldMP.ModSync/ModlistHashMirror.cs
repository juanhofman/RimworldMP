using System.Security.Cryptography;
using System.Text;

namespace RimworldMP.ModSync;

/// <summary>
/// Byte-identical mirror of <c>RimworldMP.Core.Session.ModlistHash</c>
/// (and launcher <c>ModlistHasher</c>). Keep in sync if Core changes.
/// </summary>
public static class ModlistHashMirror
{
    public const string GoldenHarmonyRimworldMp =
        "f2046ab21e526aae4469bdf1418310b553a2c34b9b9b617cc148a00d76e963f6";

    public static string Compute(IEnumerable<string> packageIdsInLoadOrder)
    {
        var joined = string.Join('\n', packageIdsInLoadOrder.Select(p => p.Trim()).Where(p => p.Length > 0));
        var bytes = Encoding.UTF8.GetBytes(joined);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static bool Matches(string localHash, string remoteHash) =>
        string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase);
}

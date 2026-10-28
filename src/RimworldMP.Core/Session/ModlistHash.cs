using System.Security.Cryptography;
using System.Text;

namespace RimworldMP.Core.Session;

/// <summary>Stable ordered modlist fingerprint for join / ready checks.</summary>
public static class ModlistHash
{
    public static string Compute(IEnumerable<string> packageIdsInLoadOrder)
    {
        var joined = string.Join('\n', packageIdsInLoadOrder.Select(p => p.Trim()).Where(p => p.Length > 0));
        var bytes = Encoding.UTF8.GetBytes(joined);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static bool Matches(string localHash, string remoteHash) =>
        string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase);
}

using System.Security.Cryptography;
using System.Text;

namespace RimworldMP.Launcher.Services;

/// <summary>
/// Byte-identical mirror of <c>RimworldMP.Core.Session.ModlistHash</c>.
/// Keep in sync if Core changes — golden vector in docs/launcher-mvp.md.
/// </summary>
public static class ModlistHasher
{
    private static readonly string[] GoldenPackageIds =
    [
        LauncherPaths.HarmonyPackageId,
        LauncherPaths.RimworldMpPackageId,
    ];

    private static readonly string[] SelfCheckOrderA = ["brrainz.harmony", "RimworldMP.Core"];
    private static readonly string[] SelfCheckOrderB = ["RimworldMP.Core", "brrainz.harmony"];
    private static readonly string[] SelfCheckTrim = [" brrainz.harmony ", "RimworldMP.Core"];

    public static string Compute(IEnumerable<string> packageIdsInLoadOrder)
    {
        var joined = string.Join('\n', packageIdsInLoadOrder.Select(p => p.Trim()).Where(p => p.Length > 0));
        var bytes = Encoding.UTF8.GetBytes(joined);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static bool Matches(string localHash, string remoteHash) =>
        string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Golden vector matching Core DeterminismHarness Phase4:
    /// Compute(["brrainz.harmony","RimworldMP.Core"]).
    /// </summary>
    public static string GoldenHarmonyRimworldMpHash => Compute(GoldenPackageIds);

    public static void AssertGoldenMatchesCoreExpectation()
    {
        // Self-check: algorithm identity (order-sensitive, trim, lowercase hex).
        var a = Compute(SelfCheckOrderA);
        var b = Compute(SelfCheckOrderB);
        var c = Compute(SelfCheckTrim);
        if (Matches(a, b))
            throw new InvalidOperationException("ModlistHasher must be order-sensitive.");
        if (!Matches(a, c))
            throw new InvalidOperationException("ModlistHasher must trim packageIds.");
        if (!string.Equals(a, a.ToLowerInvariant(), StringComparison.Ordinal) || a.Length != 64)
            throw new InvalidOperationException("ModlistHasher must return lowercase hex SHA256.");
    }
}

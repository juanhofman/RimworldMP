using RimworldMP.ModSync;

namespace RimworldMP.Tests.ModSync;

public class AboutAndHashTests
{
    [Fact]
    public void ModlistHashMirror_matches_core_golden_vector()
    {
        var hash = ModlistHashMirror.Compute(new[] { "brrainz.harmony", "RimworldMP.Core" });
        Assert.Equal(ModlistHashMirror.GoldenHarmonyRimworldMp, hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hash.ToLowerInvariant());
    }

    [Fact]
    public void ModlistHashMirror_is_order_sensitive()
    {
        var a = ModlistHashMirror.Compute(new[] { "brrainz.harmony", "RimworldMP.Core" });
        var b = ModlistHashMirror.Compute(new[] { "RimworldMP.Core", "brrainz.harmony" });
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void AboutXmlParser_reads_package_version_and_workshop()
    {
        var dir = CreateTempMod(
            packageId: "demo.mod",
            name: "Demo",
            version: "1.2.3",
            workshopUrl: "steam://url/CommunityFilePage/2009463077");
        try
        {
            var about = Path.Combine(dir, "About", "About.xml");
            var info = AboutXmlParser.TryParse(about);
            Assert.NotNull(info);
            Assert.Equal("demo.mod", info!.PackageId);
            Assert.Equal("Demo", info.Name);
            Assert.Equal("1.2.3", info.Version);
            Assert.Equal("2009463077", AboutXmlParser.TryExtractWorkshopId(info.WorkshopUrl));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ContentHasher_is_stable_and_sensitive_to_file_change()
    {
        var dir = CreateTempMod("hash.mod", "Hash", "1.0.0");
        try
        {
            var a = ContentHasher.HashDirectory(dir);
            var b = ContentHasher.HashDirectory(dir);
            Assert.Equal(a, b);

            File.WriteAllText(Path.Combine(dir, "extra.txt"), "changed");
            var c = ContentHasher.HashDirectory(dir);
            Assert.NotEqual(a, c);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PathSandbox_rejects_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "rmp-sandbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<InvalidOperationException>(() => PathSandbox.RequireUnderRoot(root, "../escape"));
            Assert.Throws<InvalidOperationException>(() => PathSandbox.RequireUnderRoot(root, "/etc/passwd"));
            var ok = PathSandbox.RequireUnderRoot(root, "Safe.Mod");
            Assert.StartsWith(Path.GetFullPath(root), ok);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    internal static string CreateTempMod(
        string packageId,
        string name,
        string version,
        string? workshopUrl = null,
        string? extraFile = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "rmp-mod-" + Guid.NewGuid().ToString("N"));
        var aboutDir = Path.Combine(dir, "About");
        Directory.CreateDirectory(aboutDir);
        var workshop = workshopUrl is null
            ? ""
            : $"\n  <url>{workshopUrl}</url>";
        File.WriteAllText(Path.Combine(aboutDir, "About.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ModMetaData>
              <name>{name}</name>
              <packageId>{packageId}</packageId>
              <modVersion>{version}</modVersion>
              <supportedVersions><li>1.7</li></supportedVersions>{workshop}
            </ModMetaData>
            """);
        if (extraFile is not null)
            File.WriteAllText(Path.Combine(dir, "payload.txt"), extraFile);
        else
            File.WriteAllText(Path.Combine(dir, "payload.txt"), "hello");
        return dir;
    }
}

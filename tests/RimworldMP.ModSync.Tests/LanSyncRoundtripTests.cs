using System.Net;
using RimworldMP.ModSync;

namespace RimworldMP.Tests.ModSync;

public class LanSyncRoundtripTests
{
    [Fact]
    public async Task Host_to_client_loopback_sync_matches_content_and_modlist_hash()
    {
        var hostMod = AboutAndHashTests.CreateTempMod("lan.beta", "Beta", "3.1.0", extraFile: "beta-payload");
        var hostModsRoot = Path.Combine(Path.GetTempPath(), "rmp-host-mods-" + Guid.NewGuid().ToString("N"));
        var clientModsRoot = Path.Combine(Path.GetTempPath(), "rmp-client-mods-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(hostModsRoot);
        Directory.CreateDirectory(clientModsRoot);
        var hostedPath = Path.Combine(hostModsRoot, "lan.beta");
        CopyDir(hostMod, hostedPath);

        try
        {
            var installed = ModInventory.ScanInstalled(null, new[] { hostModsRoot });
            var beta = Assert.Single(installed, m => m.PackageId == "lan.beta");
            var ordered = new[] { "lan.beta" };
            var manifest = new SessionModManifest
            {
                ModlistHash = ModlistHashMirror.Compute(ordered),
                OrderedPackageIds = ordered,
                Mods = new[] { beta },
            };

            await using var server = new HostModServer(manifest, port: 0, bindAddress: IPAddress.Loopback);
            await server.StartAsync();
            Assert.True(server.Port > 0);

            var client = new ClientModSync(clientModsRoot);
            var result = await client.SyncFromHostAsync(
                "127.0.0.1",
                server.Port,
                localInstalled: Array.Empty<ModManifestEntry>(),
                progress: null);

            Assert.Contains("lan.beta", result.Downloaded);
            Assert.True(result.HashesMatch);
            Assert.Equal(manifest.ModlistHash, result.LocalModlistHash);

            var clientInstalled = ModInventory.ScanInstalled(null, new[] { clientModsRoot });
            var got = Assert.Single(clientInstalled);
            Assert.Equal(beta.ContentHash, got.ContentHash);
            Assert.Equal("3.1.0", got.Version);

            var again = await client.SyncFromHostAsync("127.0.0.1", server.Port, clientInstalled);
            Assert.Empty(again.Downloaded);
            Assert.Contains("lan.beta", again.AlreadyMatched);
        }
        finally
        {
            TryDelete(hostMod);
            TryDelete(hostModsRoot);
            TryDelete(clientModsRoot);
        }
    }

    [Fact]
    public void Archive_extract_rejects_traversal_entries()
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var e = zip.CreateEntry("../evil.txt");
            using var w = new StreamWriter(e.Open());
            w.Write("nope");
        }

        var modsRoot = Path.Combine(Path.GetTempPath(), "rmp-zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modsRoot);
        try
        {
            Assert.ThrowsAny<Exception>(() =>
                ModArchive.ExtractZipToMods(ms.ToArray(), modsRoot, "Safe.Mod"));
        }
        finally
        {
            TryDelete(modsRoot);
        }
    }

    private static void CopyDir(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

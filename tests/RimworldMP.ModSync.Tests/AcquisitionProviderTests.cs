using RimworldMP.ModSync;
using RimworldMP.ModSync.Providers;

namespace RimworldMP.Tests.ModSync;

public class AcquisitionProviderTests
{
    [Fact]
    public async Task SteamCmd_stub_never_resolves_and_documents_limitation()
    {
        var stub = new SteamCmdProviderStub();
        Assert.False(stub.IsImplemented);
        Assert.Equal(SteamCmdProviderStub.StubName, stub.Name);
        Assert.Contains("SteamCMD", stub.LimitationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not supported", stub.LimitationMessage, StringComparison.OrdinalIgnoreCase);

        var path = await stub.TryResolveLocalFolderAsync(new ModManifestEntry
        {
            PackageId = "demo.mod",
            Name = "Demo",
            Version = "1.0.0",
            ContentHash = new string('a', 64),
            ByteSize = 1,
        });
        Assert.Null(path);
    }

    [Fact]
    public void Acquisition_failure_message_cites_steamcmd_limitation()
    {
        var msg = ClientModSync.FormatAcquisitionFailure("demo.mod", "Host missing files for demo.mod");
        Assert.Contains("demo.mod", msg, StringComparison.Ordinal);
        Assert.Contains("Host missing files", msg, StringComparison.Ordinal);
        Assert.Contains("SteamCMD", msg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("downloading from Steam", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Client_skips_unimplemented_providers_before_lan()
    {
        var hostMod = AboutAndHashTests.CreateTempMod("acq.skip", "Skip", "1.0.0", extraFile: "payload");
        var hostModsRoot = Path.Combine(Path.GetTempPath(), "rmp-acq-host-" + Guid.NewGuid().ToString("N"));
        var clientModsRoot = Path.Combine(Path.GetTempPath(), "rmp-acq-client-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(hostModsRoot);
        Directory.CreateDirectory(clientModsRoot);
        var hostedPath = Path.Combine(hostModsRoot, "acq.skip");
        CopyDir(hostMod, hostedPath);

        try
        {
            var installed = ModInventory.ScanInstalled(null, new[] { hostModsRoot });
            var entry = Assert.Single(installed, m => m.PackageId == "acq.skip");
            var ordered = new[] { "acq.skip" };
            var manifest = new SessionModManifest
            {
                ModlistHash = ModlistHashMirror.Compute(ordered),
                OrderedPackageIds = ordered,
                Mods = new[] { entry },
            };

            await using var server = new HostModServer(manifest, port: 0, bindAddress: System.Net.IPAddress.Loopback);
            await server.StartAsync();

            var logs = new List<string>();
            var client = new ClientModSync(clientModsRoot, new IModAcquisitionProvider[] { new SteamCmdProviderStub() });
            client.Log += logs.Add;

            var result = await client.SyncFromHostAsync("127.0.0.1", server.Port, Array.Empty<ModManifestEntry>());
            Assert.Contains("acq.skip", result.Downloaded);
            Assert.Contains(logs, l => l.Contains("steamcmd-unavailable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(logs, l => l.Contains("not implemented", StringComparison.OrdinalIgnoreCase)
                                       || l.Contains("not supported", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(hostMod);
            TryDelete(hostModsRoot);
            TryDelete(clientModsRoot);
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

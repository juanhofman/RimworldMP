using System.Net;
using RimworldMP.ModSync;
using RimworldMP.ModSync.Protocol;

namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Host-side LAN mod share lifecycle used by CLI <c>host --share-mods</c> and Avalonia Host.
/// Serves only active non-official mods over TCP (default 30557).
/// </summary>
public sealed class HostModShareService : IAsyncDisposable
{
    private HostModServer? _server;

    public int Port { get; private set; } = ModSyncProtocol.DefaultPort;
    public bool IsRunning => _server is not null;
    public SessionModManifest? Manifest { get; private set; }

    public event Action<string>? Log;

    public async Task StartAsync(
        IReadOnlyList<string> orderedPackageIds,
        string rimWorldInstallDir,
        string? rwBuild = "1.7",
        int port = ModSyncProtocol.DefaultPort,
        IPAddress? bindAddress = null,
        CancellationToken cancellationToken = default)
    {
        await StopAsync().ConfigureAwait(false);

        var installed = ModInventory.ScanInstalled(rimWorldInstallDir);
        var manifest = ModInventory.BuildSessionManifest(orderedPackageIds, installed, rwBuild);
        Manifest = manifest;

        _server = new HostModServer(manifest, port, bindAddress ?? IPAddress.Any);
        _server.Log += msg => Log?.Invoke(msg);
        await _server.StartAsync(cancellationToken).ConfigureAwait(false);
        Port = _server.Port;
        Log?.Invoke($"Sharing {manifest.Mods.Count} mods on TCP :{Port}");
    }

    public async Task StopAsync()
    {
        if (_server is null)
            return;
        await _server.DisposeAsync().ConfigureAwait(false);
        _server = null;
        Manifest = null;
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}

using System.Net;
using System.Net.Sockets;
using RimworldMP.ModSync.Protocol;

namespace RimworldMP.ModSync;

/// <summary>
/// Host-side LAN mod share server. Serves only mods in the active session manifest
/// over length-prefixed TCP frames. Defaults to binding loopback+LAN (IPAddress.Any);
/// not intended for WAN exposure.
/// </summary>
public sealed class HostModServer : IAsyncDisposable
{
    private readonly SessionModManifest _manifest;
    private readonly Dictionary<string, ModManifestEntry> _byId;
    private readonly IPAddress _bindAddress;
    private int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public HostModServer(
        SessionModManifest manifest,
        int port = ModSyncProtocol.DefaultPort,
        IPAddress? bindAddress = null)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _port = port;
        _bindAddress = bindAddress ?? IPAddress.Any;
        _byId = manifest.Mods.ToDictionary(m => m.PackageId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Bound TCP port (updated after Start when port was 0 / ephemeral).</summary>
    public int Port => _port;
    public IPAddress BindAddress => _bindAddress;
    public SessionModManifest Manifest => _manifest;

    public event Action<string>? Log;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null)
            throw new InvalidOperationException("Already started.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(_bindAddress, _port);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Log?.Invoke($"ModSync host listening on {_bindAddress}:{_port} ({_manifest.Mods.Count} shareable mods)");
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _listener?.Stop(); } catch { /* ignore */ }
        _listener = null;
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); } catch { /* ignore */ }
        }

        _cts?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { break; }

            if (client is null)
                continue;

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleClientAsync(client, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"client error: {ex.Message}");
                }
                finally
                {
                    try { client.Dispose(); } catch { /* ignore */ }
                }
            }, CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        await using var stream = client.GetStream();

        // Hello exchange
        var (op, payload) = await ModSyncFrameIo.ReadFrameAsync(stream, ct).ConfigureAwait(false);
        if (op != ModSyncOpcode.Hello)
        {
            await WriteErrorAsync(stream, "Expected Hello", ct).ConfigureAwait(false);
            return;
        }

        ModSyncFrameIo.ValidateHello(payload);
        await ModSyncFrameIo.WriteFrameAsync(stream, ModSyncOpcode.HelloAck, ModSyncFrameIo.EncodeHello(), ct)
            .ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            (op, payload) = await ModSyncFrameIo.ReadFrameAsync(stream, ct).ConfigureAwait(false);
            switch (op)
            {
                case ModSyncOpcode.RequestManifest:
                    await ModSyncFrameIo.WriteFrameAsync(
                            stream,
                            ModSyncOpcode.Manifest,
                            ModSyncFrameIo.EncodeManifest(_manifest),
                            ct)
                        .ConfigureAwait(false);
                    break;

                case ModSyncOpcode.RequestMod:
                {
                    var packageId = ModSyncFrameIo.DecodeUtf8(payload).Trim();
                    if (!_byId.TryGetValue(packageId, out var entry))
                    {
                        await WriteErrorAsync(stream, $"Mod not in active share list: {packageId}", ct)
                            .ConfigureAwait(false);
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(entry.LocalPath) || !Directory.Exists(entry.LocalPath))
                    {
                        await WriteErrorAsync(stream, $"Host missing files for {packageId}", ct)
                            .ConfigureAwait(false);
                        break;
                    }

                    Log?.Invoke($"serving {packageId} from {entry.LocalPath}");
                    var zip = ModArchive.CreateZipBytes(entry.LocalPath);
                    var blob = ModSyncFrameIo.EncodeModBlob(packageId, zip);
                    await ModSyncFrameIo.WriteFrameAsync(stream, ModSyncOpcode.ModBlob, blob, ct)
                        .ConfigureAwait(false);
                    break;
                }

                default:
                    await WriteErrorAsync(stream, $"Unknown opcode {(byte)op}", ct).ConfigureAwait(false);
                    return;
            }
        }
    }

    private static Task WriteErrorAsync(NetworkStream stream, string message, CancellationToken ct) =>
        ModSyncFrameIo.WriteFrameAsync(stream, ModSyncOpcode.Error, ModSyncFrameIo.EncodeUtf8(message), ct);
}

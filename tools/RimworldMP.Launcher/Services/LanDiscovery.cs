using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using RimworldMP.Launcher.Models;

namespace RimworldMP.Launcher.Services;

/// <summary>
/// UDP broadcast LAN discovery on a side port (default 30556).
/// Local-only: broadcast / listen; no WAN advertise, no uploads.
/// </summary>
public sealed class LanDiscovery : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly int _discoveryPort;
    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private Task? _listenTask;
    private UdpClient? _udp;

    public LanDiscovery(int discoveryPort = LauncherPaths.DefaultDiscoveryPort)
    {
        _discoveryPort = discoveryPort;
    }

    public event Action<DiscoveryBeacon, IPEndPoint>? OnBeacon;

    public async Task StartBroadcastAsync(DiscoveryBeacon beacon, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        Stop();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _udp = new UdpClient();
        _udp.EnableBroadcast = true;
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(beacon, JsonOptions));
        var targets = BuildBroadcastTargets(_discoveryPort);
        var token = _cts.Token;

        _broadcastTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var endpoint in targets)
                {
                    try
                    {
                        await _udp.SendAsync(payload, payload.Length, endpoint).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException) { return; }
                    catch (SocketException)
                    {
                        // Transient / unsupported broadcast on this NIC — try other targets
                    }
                }

                try
                {
                    await Task.Delay(interval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }, CancellationToken.None);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StartListenAsync(CancellationToken cancellationToken = default)
    {
        Stop();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _udp = new UdpClient();
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, _discoveryPort));
        var token = _cts.Token;

        _listenTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udp.ReceiveAsync(token).ConfigureAwait(false);
                    var text = Encoding.UTF8.GetString(result.Buffer);
                    DiscoveryBeacon? beacon;
                    try
                    {
                        beacon = JsonSerializer.Deserialize<DiscoveryBeacon>(text, JsonOptions);
                    }
                    catch
                    {
                        continue;
                    }

                    if (beacon is null)
                        continue;
                    if (!string.Equals(beacon.MagicToken, DiscoveryBeacon.Magic, StringComparison.Ordinal))
                        continue;

                    // Fill host from packet source if beacon omitted it
                    if (string.IsNullOrWhiteSpace(beacon.Host))
                    {
                        beacon = new DiscoveryBeacon
                        {
                            MagicToken = beacon.MagicToken,
                            Protocol = beacon.Protocol,
                            Name = beacon.Name,
                            Port = beacon.Port,
                            RwBuild = beacon.RwBuild,
                            ModHashShort = beacon.ModHashShort,
                            ModlistHash = beacon.ModlistHash,
                            Host = result.RemoteEndPoint.Address.ToString(),
                        };
                    }

                    OnBeacon?.Invoke(beacon, result.RemoteEndPoint);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
            }
        }, CancellationToken.None);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _udp?.Dispose(); } catch { /* ignore */ }
        _udp = null;
        _cts = null;
        _broadcastTask = null;
        _listenTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_broadcastTask is not null)
        {
            try { await _broadcastTask.ConfigureAwait(false); } catch { /* ignore */ }
        }

        if (_listenTask is not null)
        {
            try { await _listenTask.ConfigureAwait(false); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Global broadcast + per-NIC directed broadcast + loopback unicast (same-machine / restricted VMs).
    /// </summary>
    private static List<IPEndPoint> BuildBroadcastTargets(int port)
    {
        var list = new List<IPEndPoint>
        {
            new(IPAddress.Broadcast, port),
            new(IPAddress.Loopback, port),
        };

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (ua.IPv4Mask is null)
                        continue;

                    var ipBytes = ua.Address.GetAddressBytes();
                    var maskBytes = ua.IPv4Mask.GetAddressBytes();
                    if (ipBytes.Length != 4 || maskBytes.Length != 4)
                        continue;

                    var bcast = new byte[4];
                    for (var i = 0; i < 4; i++)
                        bcast[i] = (byte)(ipBytes[i] | (byte)~maskBytes[i]);
                    list.Add(new IPEndPoint(new IPAddress(bcast), port));
                }
            }
        }
        catch
        {
            // Stick with global + loopback
        }

        return list
            .GroupBy(e => e.ToString())
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>Listen for a short window and return unique beacons keyed by host:port.</summary>
    public static async Task<IReadOnlyList<DiscoveryBeacon>> DiscoverOnceAsync(
        TimeSpan duration,
        int discoveryPort = LauncherPaths.DefaultDiscoveryPort,
        CancellationToken cancellationToken = default)
    {
        await using var disco = new LanDiscovery(discoveryPort);
        var found = new Dictionary<string, DiscoveryBeacon>(StringComparer.OrdinalIgnoreCase);
        disco.OnBeacon += (b, _) =>
        {
            var key = $"{b.Host}:{b.Port}";
            found[key] = b;
        };
        await disco.StartListenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // return what we have
        }

        disco.Stop();
        return found.Values.ToList();
    }
}

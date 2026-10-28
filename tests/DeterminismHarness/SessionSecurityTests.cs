using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Tests.DeterminismHarness;

public class SessionSecurityTests
{
    [Fact]
    public async Task Host_ignores_client_injected_full_batches()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 1);
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 1);
        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        // Malicious/buggy client sends a full batch for a future tick directly.
        var injected = CommandBatch.Empty(99);
        clientTransport.SendReliable(
            NetPeerId.Host,
            Envelope(NetMessageType.CommandBatch, injected.ToBytes()));

        await Task.Delay(50);

        Assert.False(host.TickGate.HasBatch(99));

        host.HostEmitNextBatch();
        Assert.True(host.TryAdvanceSim());
        Assert.Equal(1, host.Tick);
        Assert.False(host.TickGate.HasBatch(99));
    }

    [Fact]
    public async Task Lan_join_ack_syncs_session_seed_from_host()
    {
        var port = FreePort();
        await using var host = new MpSession(new LanTcpTransport(), isHost: true, sessionSeed: 555);
        // Client starts with a deliberately wrong seed; JoinAck must overwrite it.
        await using var client = new MpSession(new LanTcpTransport(), isHost: false, sessionSeed: 111);

        await host.StartAsync(new NetEndpoint("127.0.0.1", port));
        await client.StartAsync(new NetEndpoint("127.0.0.1", port));
        await Task.Delay(100);
        await TestHelpers.WaitForJipAsync(client);

        Assert.Equal(555, host.SessionSeed);
        Assert.Equal(555, client.SessionSeed);
        Assert.Equal(555, client.Rand.Seed);
        Assert.Equal(1, client.LocalPlayerId);
    }

    private static byte[] Envelope(NetMessageType type, ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[1 + payload.Length];
        buffer[0] = (byte)type;
        payload.CopyTo(buffer.AsSpan(1));
        return buffer;
    }

    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

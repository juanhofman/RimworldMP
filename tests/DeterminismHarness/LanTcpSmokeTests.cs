using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimworldMP.Net;

namespace RimworldMP.Tests.DeterminismHarness;

public class LanTcpSmokeTests
{
    [Fact]
    public async Task Host_accepts_one_client_and_exchanges_batch()
    {
        var port = GetFreePort();
        await using var host = new MpSession(new LanTcpTransport(), isHost: true, sessionSeed: 99);
        await using var client = new MpSession(new LanTcpTransport(), isHost: false, sessionSeed: 99);

        await host.StartAsync(new NetEndpoint("127.0.0.1", port));
        await client.StartAsync(new NetEndpoint("127.0.0.1", port));
        await Task.Delay(100);
        await TestHelpers.WaitForJipAsync(client);

        host.SubmitCommand(CommandOpcode.EmptyBatch);
        host.SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(true));
        host.HostEmitNextBatch();

        Assert.True(host.TryAdvanceSim());

        var clientOk = false;
        for (var i = 0; i < 30 && !clientOk; i++)
        {
            clientOk = client.TryAdvanceSim();
            if (!clientOk)
                await Task.Delay(20);
        }

        Assert.True(clientOk);
        Assert.Equal(host.Tick, client.Tick);
        Assert.False(host.IsFrozen);
        Assert.False(client.IsFrozen);
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

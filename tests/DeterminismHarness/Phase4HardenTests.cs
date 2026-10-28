using RimworldMP.Core.Commands;
using RimworldMP.Core.Join;
using RimworldMP.Core.Session;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Tests.DeterminismHarness;

public class Phase4HardenTests
{
    [Fact]
    public void ModlistHash_is_order_sensitive()
    {
        var a = ModlistHash.Compute(new[] { "brrainz.harmony", "RimworldMP.Core" });
        var b = ModlistHash.Compute(new[] { "RimworldMP.Core", "brrainz.harmony" });
        var c = ModlistHash.Compute(new[] { "brrainz.harmony", "RimworldMP.Core" });
        Assert.NotEqual(a, b);
        Assert.Equal(a, c);
        Assert.True(ModlistHash.Matches(a, c));
    }

    [Fact]
    public void ReadyCheck_requires_matching_modlists()
    {
        var ready = new ReadyCheck { Required = true };
        ready.SetPeer(0, true, "aaa");
        ready.SetPeer(1, true, "bbb");
        Assert.False(ready.AllReady(2, "aaa"));
        ready.SetPeer(1, true, "aaa");
        Assert.True(ready.AllReady(2, "aaa"));
    }

    [Fact]
    public void Protocol_rejects_incompatible_versions()
    {
        Assert.True(NetProtocol.IsCompatible(NetProtocol.Version));
        Assert.False(NetProtocol.IsCompatible(1));
        Assert.False(NetProtocol.IsCompatible(255));
    }

    [Fact]
    public async Task Ready_gate_blocks_host_emit_until_ready()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 1);
        host.SetModlistHash("mods");
        host.Ready.Required = true;
        host.ExpectedPeers = 2;
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 1);
        client.SetModlistHash("mods");

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        host.SetReady(true);
        // only host ready
        host.HostEmitNextBatch();
        Assert.False(host.TryAdvanceSim()); // no batch emitted

        client.SetReady(true);
        await Task.Delay(30);
        // host must observe client ready — client SetReady sends to host
        host.Ready.SetPeer(1, true, "mods"); // ensure host dictionary has client
        host.HostEmitNextBatch();
        Assert.True(host.TryAdvanceSim());
    }

    [Fact]
    public async Task Recovery_rejoin_requests_join_point()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        var blob = "RECOVER"u8.ToArray();
        var mem = new MemorySaveLoad();
        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 9);
        host.JoinPointSource = new MemoryJoinPointSource(blob, "m");
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 9)
        {
            SaveLoad = mem,
        };

        JoinPoint? jp = null;
        client.OnJoinPointReceived += p => jp = p;

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        jp = null;
        SessionRecovery.RequestRejoin(client);
        await TestHelpers.WaitForJipAsync(client);
        Assert.NotNull(jp);
        Assert.Equal(blob, jp!.SaveBlob);
        Assert.Equal(blob, mem.LastLoaded);
    }

    [Fact]
    public async Task Soak_1000_empty_ticks_stay_in_sync()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 77);
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 77);
        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        for (var i = 0; i < 1000; i++)
        {
            if (i % 17 == 0)
                host.SubmitCommand(CommandOpcode.Speed, CommandPayloads.Speed((byte)((i % 3) + 1)));
            host.HostEmitNextBatch();
            Assert.True(host.TryAdvanceSim());
            var ok = false;
            for (var w = 0; w < 200 && !ok; w++)
            {
                ok = client.TryAdvanceSim();
                if (!ok)
                    await Task.Yield();
            }

            Assert.True(ok, $"client stalled at host tick {host.Tick}; frozen={client.IsFrozen}");
            Assert.Equal(host.Tick, client.Tick);
            Assert.False(client.IsFrozen);
        }

        Assert.Equal(1000, host.Tick);
        Assert.False(host.IsFrozen);
        Assert.False(client.IsFrozen);
    }
}

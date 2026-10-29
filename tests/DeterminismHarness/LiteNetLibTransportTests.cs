using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Tests.DeterminismHarness;

public class LiteNetLibTransportTests
{
    [Fact]
    public void ReconnectPolicy_delay_schedule_is_deterministic_without_jitter()
    {
        var policy = new ReconnectPolicy
        {
            MaxAttempts = 4,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(2),
            Multiplier = 2,
            JitterFraction = 0,
        };

        var delays = policy.EnumerateDelays().ToArray();
        Assert.Equal(4, delays.Length);
        Assert.Equal(TimeSpan.Zero, delays[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(100), delays[1]);
        Assert.Equal(TimeSpan.FromMilliseconds(200), delays[2]);
        Assert.Equal(TimeSpan.FromMilliseconds(400), delays[3]);
        Assert.Equal(TimeSpan.Zero, ReconnectPolicy.None.DelayBeforeAttempt(1));
    }

    [Fact]
    public void NetTransportFactory_parses_handoff_aliases()
    {
        Assert.True(NetTransportFactory.TryParse("tcp", out var tcp));
        Assert.Equal(NetTransportKind.Tcp, tcp);
        Assert.True(NetTransportFactory.TryParse("litenetlib", out var lnl));
        Assert.Equal(NetTransportKind.LiteNetLib, lnl);
        Assert.True(NetTransportFactory.TryParse("udp", out var udp));
        Assert.Equal(NetTransportKind.LiteNetLib, udp);
        Assert.False(NetTransportFactory.TryParse("steam", out _));
        Assert.IsType<LanTcpTransport>(NetTransportFactory.Create(NetTransportKind.Tcp));
        Assert.IsType<LiteNetLibTransport>(NetTransportFactory.Create(NetTransportKind.LiteNetLib));
    }

    [Fact]
    public void Handshake_v4_trailer_roundtrips_and_v3_body_still_parses()
    {
        var v4 = HandshakeCodec.BuildHandshakePayload(
            4, "mods", NetTransportKind.LiteNetLib, HandshakeCodec.FlagPacingCapable);
        Assert.Equal("mods", HandshakeCodec.ReadModlistHash(v4));
        Assert.True(HandshakeCodec.TryReadTrailer(v4, out var kind, out var flags));
        Assert.Equal(NetTransportKind.LiteNetLib, kind);
        Assert.Equal(HandshakeCodec.FlagPacingCapable, flags);

        var v3 = HandshakeCodec.BuildHandshakePayload(3, "mods");
        Assert.Equal("mods", HandshakeCodec.ReadModlistHash(v3));
        Assert.False(HandshakeCodec.TryReadTrailer(v3, out _, out _));
        Assert.True(NetProtocol.IsCompatible(3));
        Assert.True(NetProtocol.IsCompatible(4));
        Assert.False(NetProtocol.IsCompatible(2));
        Assert.Equal(4, NetProtocol.Version);
        Assert.Equal(3, NetProtocol.MinSupportedVersion);
    }

    [Fact]
    public void PayloadPacing_resolves_smaller_chunks_for_litenetlib_v4()
    {
        Assert.Equal(
            NetProtocol.LiteNetLibJoinPointChunkBytes,
            PayloadPacing.ResolveChunkBytes(NetTransportKind.LiteNetLib, remoteVersion: 4));
        Assert.Equal(
            NetProtocol.JoinPointChunkBytes,
            PayloadPacing.ResolveChunkBytes(NetTransportKind.Tcp, remoteVersion: 4));
        Assert.Equal(
            NetProtocol.JoinPointChunkBytes,
            PayloadPacing.ResolveChunkBytes(NetTransportKind.LiteNetLib, remoteVersion: 3));
        Assert.True(PayloadPacing.InterChunkDelay(NetTransportKind.LiteNetLib) > TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, PayloadPacing.InterChunkDelay(NetTransportKind.Tcp));
    }

    [Fact]
    public async Task LiteNetLib_host_accepts_client_and_exchanges_batch()
    {
        if (!LiteNetLibAvailable(out var reason))
        {
            // Prefer real loopback; only skip if the native/UDP stack cannot bind.
            Assert.Fail($"LiteNetLib unavailable: {reason}");
            return;
        }

        var port = FreeUdpPort();
        await using var host = new MpSession(
            new LiteNetLibTransport { MaxClients = 4, ExpectedModlistHash = "lnl" },
            isHost: true,
            sessionSeed: 42);
        host.SetModlistHash("lnl");
        await using var client = new MpSession(
            new LiteNetLibTransport
            {
                LocalModlistHash = "lnl",
                ReconnectPolicy = ReconnectPolicy.None,
            },
            isHost: false,
            sessionSeed: 1);
        client.SetModlistHash("lnl");

        await host.StartAsync(new NetEndpoint("127.0.0.1", port));
        await client.StartAsync(new NetEndpoint("127.0.0.1", port));
        await Task.Delay(150);
        await TestHelpers.WaitForJipAsync(client);

        Assert.Equal(42, client.SessionSeed);
        Assert.Equal(1, client.LocalPlayerId);

        host.SubmitCommand(CommandOpcode.EmptyBatch);
        host.SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(true));
        host.HostEmitNextBatch();
        Assert.True(host.TryAdvanceSim());

        var clientOk = false;
        for (var i = 0; i < 40 && !clientOk; i++)
        {
            clientOk = client.TryAdvanceSim();
            if (!clientOk)
                await Task.Delay(25);
        }

        Assert.True(clientOk);
        Assert.Equal(host.Tick, client.Tick);
        Assert.False(host.IsFrozen);
        Assert.False(client.IsFrozen);
    }

    [Fact]
    public async Task LiteNetLib_rejects_modlist_mismatch()
    {
        var port = FreeUdpPort();
        var hostLan = new LiteNetLibTransport { MaxClients = 4, ExpectedModlistHash = "host-hash" };
        await using var host = new MpSession(hostLan, isHost: true, sessionSeed: 7);
        host.SetModlistHash("host-hash");
        await host.StartAsync(new NetEndpoint("127.0.0.1", port));

        var clientLan = new LiteNetLibTransport
        {
            LocalModlistHash = "client-different",
            ReconnectPolicy = ReconnectPolicy.None,
        };
        await using var client = new MpSession(clientLan, isHost: false, sessionSeed: 1);
        client.SetModlistHash("client-different");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.StartAsync(new NetEndpoint("127.0.0.1", port)));
        Assert.Contains("Modlist", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(JoinRejectReason.ModlistMismatch, clientLan.LastJoinRejectReason);
    }

    [Fact]
    public async Task SessionFacade_selects_litenetlib_from_MpConfig()
    {
        MpConfig.ResetToDefaults();
        MpConfig.TransportKind = NetTransportKind.LiteNetLib;
        MpConfig.ModlistHash = "cfg";
        var port = FreeUdpPort();

        try
        {
            var host = await SessionFacade.HostLobbyAsync(port: port, sessionSeed: 9);
            Assert.IsType<LiteNetLibTransport>(host.Transport);

            var client = await SessionFacade.JoinLobbyAsync("127.0.0.1", port: port);
            Assert.IsType<LiteNetLibTransport>(client.Transport);
            await Task.Delay(120);
            await TestHelpers.WaitForJipAsync(client);
            Assert.Equal(9, client.SessionSeed);

            await client.StopAsync();
            await client.DisposeAsync();
            await host.StopAsync();
            await host.DisposeAsync();
            SessionFacade.Clear();
        }
        finally
        {
            MpConfig.ResetToDefaults();
            SessionFacade.Clear();
        }
    }

    [Fact]
    public void MpConfig_default_transport_is_tcp_fallback()
    {
        MpConfig.ResetToDefaults();
        Assert.Equal(NetTransportKind.Tcp, MpConfig.TransportKind);
        Assert.IsType<LanTcpTransport>(NetTransportFactory.Create(MpConfig.TransportKind));
    }

    private static bool LiteNetLibAvailable(out string reason)
    {
        try
        {
            var port = FreeUdpPort();
            using var probe = new System.Net.Sockets.UdpClient(port);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    private static int FreeUdpPort()
    {
        using var udp = new System.Net.Sockets.UdpClient(0);
        return ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }
}

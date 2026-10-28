using RimworldMP.Core.Commands;
using RimworldMP.Core.Join;
using RimworldMP.Core.Lobby;
using RimworldMP.Core.Session;
using RimworldMP.Core.Sync;
using RimworldMP.Core.World;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Tests.DeterminismHarness;

public class P0SimCorrectnessTests
{
    public P0SimCorrectnessTests()
    {
        ZoneWorld.Current.Reset();
        MpConfig.ResetToDefaults();
        GameLoad.Current = null;
    }

    [Fact]
    public void SnapshotHash_includes_protocol_tick_and_rng_counters()
    {
        var rand = new SyncedRand(99);
        var hasher = new StateHasher();
        hasher.AddUtf8("cmds");
        var detector = new DesyncDetector(hasher, rand, Path.Combine(Path.GetTempPath(), "rimworldmp-p0-hash"));
        detector.IncrementCounter("Rand.Value", 3);
        detector.IncrementCounter("ticks", 1);

        var a = detector.SnapshotHashHex(60);
        var b = detector.SnapshotHashHex(60);
        Assert.Equal(a, b);

        // Different tick ⇒ different fingerprint.
        Assert.NotEqual(a, detector.SnapshotHashHex(61));

        // Extra RNG counter ⇒ different fingerprint.
        detector.IncrementCounter("Rand.Range");
        Assert.NotEqual(a, detector.SnapshotHashHex(60));
    }

    [Fact]
    public void SnapshotHash_world_fingerprint_slot_changes_hash()
    {
        var rand = new SyncedRand(1);
        var hasher = new StateHasher();
        var detector = new DesyncDetector(hasher, rand, Path.Combine(Path.GetTempPath(), "rimworldmp-p0-world"));
        var before = detector.SnapshotHashHex(1);
        var fp = new byte[32];
        fp[0] = 0xAB;
        detector.SetWorldFingerprint(fp);
        Assert.NotEqual(before, detector.SnapshotHashHex(1));
    }

    [Fact]
    public void JobGive_and_ZoneCells_carry_map_id()
    {
        var jobBytes = CommandPayloads.JobGive(
            mapId: 7,
            pawnLoadId: 3,
            cellX: 10,
            cellZ: 20,
            jobDefName: "HaulToCell",
            targetThingId: 99,
            count: 5,
            queue: 1);
        var job = CommandPayloads.ReadJobGive(jobBytes);
        Assert.Equal(7, job.MapId);
        Assert.Equal(3, job.PawnLoadId);
        Assert.Equal(99, job.TargetThingId);
        Assert.Equal(5, job.Count);
        Assert.Equal(1, job.Queue);
        Assert.Equal("HaulToCell", job.JobDefName);

        var zoneBytes = CommandPayloads.ZoneCells(1, mapId: 4, new[] { 1, 2, 3, 4 });
        var zone = CommandPayloads.ReadZoneCells(zoneBytes);
        Assert.Equal(1, zone.ZoneKind);
        Assert.Equal(4, zone.MapId);
        Assert.Equal(new[] { 1, 2, 3, 4 }, zone.CellsPacked);

        ZoneWorld.Current.ApplyOpcode(CommandOpcode.ZoneAdd, 1, 4, new[] { 1, 2 });
        Assert.Equal(1, ZoneWorld.Current.CellCount(1, mapId: 4));
        Assert.Equal(0, ZoneWorld.Current.CellCount(1, mapId: 9));
    }

    [Fact]
    public void Jip_non_empty_blob_fails_loudly_without_loader()
    {
        var jp = new JoinPoint(1, 2, 0, "m", "SAVE"u8.ToArray());
        var session = new MpSession(new LoopbackTransport(), isHost: false, sessionSeed: 2);
        var ex = Assert.Throws<MissingSaveLoadException>(() => session.ApplyJoinPoint(jp));
        Assert.Equal(4, ex.BlobLength);
    }

    [Fact]
    public void Jip_loads_via_ISaveLoad_when_registered()
    {
        var mem = new MemorySaveLoad();
        var jp = new JoinPoint(5, 9, 4, "mods", "BLOBDATA"u8.ToArray());
        var session = new MpSession(new LoopbackTransport(), isHost: false, sessionSeed: 1)
        {
            SaveLoad = mem,
        };
        session.ApplyJoinPoint(jp);
        Assert.Equal("BLOBDATA"u8.ToArray(), mem.LastLoaded);
        Assert.Equal(1, mem.LoadCount);
        Assert.Equal(9, session.SessionSeed);
        Assert.Equal(4, session.Tick);
    }

    [Fact]
    public void Jip_empty_blob_does_not_require_loader()
    {
        var session = new MpSession(new LoopbackTransport(), isHost: false, sessionSeed: 3);
        session.ApplyJoinPoint(new JoinPoint(1, 3, 0, "m", Array.Empty<byte>()));
        Assert.Equal(3, session.SessionSeed);
    }

    [Fact]
    public void Launcher_handoff_round_trips_and_ack()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rimworldmp-handoff", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "launcher-handoff.json");
        var doc = new LauncherHandoffDocument
        {
            Role = "client",
            Host = "192.168.1.9",
            Port = 30555,
            ModlistHash = "abc",
            AutoStartLobby = false,
            MaxClients = 4,
            SessionName = "Night",
            LauncherPid = 42,
        };
        LauncherHandoff.Write(doc, path);
        var read = LauncherHandoff.TryRead(path);
        Assert.NotNull(read);
        Assert.Equal("client", read!.Role);
        Assert.Equal("192.168.1.9", read.Host);
        Assert.Equal("abc", read.ModlistHash);
        Assert.Equal(4, read.MaxClients);
        LauncherHandoff.Acknowledge(path);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".ack"));
    }

    [Fact]
    public void MpConfig_max_clients_default_is_above_one()
    {
        Assert.True(MpConfig.DefaultMaxClients > 1);
        Assert.Equal(4, MpConfig.DefaultMaxClients);
        Assert.Equal(4, new LanTcpTransport().MaxClients);
    }

    [Fact]
    public void Protocol_v3_hard_gate_helpers()
    {
        Assert.Equal(3, NetProtocol.Version);
        Assert.Equal("RMP3"u8.ToArray(), NetProtocol.HandshakeMagic);
        var payload = LanTcpTransport.BuildHandshakePayload(NetProtocol.Version, "deadbeef");
        Assert.Equal("deadbeef", LanTcpTransport.ReadHandshakeModlistHash(payload));
        var reject = LanTcpTransport.BuildJoinReject(JoinRejectReason.ModlistMismatch, "nope");
        Assert.Equal((byte)JoinRejectReason.ModlistMismatch, reject[0]);
    }

    [Fact]
    public async Task Lan_rejects_modlist_hash_mismatch_before_session()
    {
        var port = FreePort();
        var hostLan = new LanTcpTransport { MaxClients = 4, ExpectedModlistHash = "host-hash" };
        await using var host = new MpSession(hostLan, isHost: true, sessionSeed: 7);
        host.SetModlistHash("host-hash");
        await host.StartAsync(new NetEndpoint("127.0.0.1", port));

        var clientLan = new LanTcpTransport { LocalModlistHash = "client-different" };
        await using var client = new MpSession(clientLan, isHost: false, sessionSeed: 1);
        client.SetModlistHash("client-different");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.StartAsync(new NetEndpoint("127.0.0.1", port)));
        Assert.Contains("Modlist", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(JoinRejectReason.ModlistMismatch, clientLan.LastJoinRejectReason);
    }

    [Fact]
    public async Task Lan_accepts_matching_modlist_hash()
    {
        var port = FreePort();
        const string hash = "same-mods";
        await using var host = new MpSession(
            new LanTcpTransport { ExpectedModlistHash = hash, MaxClients = 4 },
            isHost: true,
            sessionSeed: 11);
        host.SetModlistHash(hash);
        await using var client = new MpSession(
            new LanTcpTransport { LocalModlistHash = hash },
            isHost: false,
            sessionSeed: 0);
        client.SetModlistHash(hash);

        await host.StartAsync(new NetEndpoint("127.0.0.1", port));
        await client.StartAsync(new NetEndpoint("127.0.0.1", port));
        await Task.Delay(80);
        await TestHelpers.WaitForJipAsync(client);
        Assert.Equal(11, client.SessionSeed);
        Assert.Equal(1, client.LocalPlayerId);
    }

    [Fact]
    public async Task Jip_transfer_invokes_save_load()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        var blob = "SAVEBLOB-P0"u8.ToArray();
        var mem = new MemorySaveLoad();
        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 42);
        host.JoinPointSource = new MemoryJoinPointSource(blob, "mods-hash");
        host.SetModlistHash("mods-hash");
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 1)
        {
            SaveLoad = mem,
        };

        await host.StartAsync(new NetEndpoint("loop", 0));
        host.HostEmitNextBatch();
        host.TryAdvanceSim();
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        Assert.Equal(blob, mem.LastLoaded);
        Assert.False(client.IsJoinInProgress);
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

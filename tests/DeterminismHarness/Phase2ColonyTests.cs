using RimworldMP.Core.Commands;
using RimworldMP.Core.Join;
using RimworldMP.Core.Session;
using RimworldMP.Core.Sync;
using RimworldMP.Core.World;
using RimworldMP.Net;

namespace RimworldMP.Tests.DeterminismHarness;

public class Phase2ColonyTests
{
    public Phase2ColonyTests()
    {
        ColonyWorld.Current.Reset();
        ZoneWorld.Current.Reset();
    }

    [Fact]
    public void Phase2_payloads_round_trip()
    {
        var bill = CommandPayloads.ReadBillSet(CommandPayloads.BillSet(10, 2, "Make_MealSimple", 1, 5));
        Assert.Equal((10, 2, (byte)1, 5, "Make_MealSimple"), bill);

        Assert.Equal("MicroelectronicsBasics", CommandPayloads.ReadResearchStart(CommandPayloads.ResearchStart("MicroelectronicsBasics")));

        var trade = CommandPayloads.ReadTradeAccept(CommandPayloads.TradeAccept(3, 99L));
        Assert.Equal((3, 99L), trade);

        var caravan = CommandPayloads.ReadCaravanForm(CommandPayloads.CaravanForm(42, new[] { 1, 2, 3 }));
        Assert.Equal(42, caravan.TileId);
        Assert.Equal(new[] { 1, 2, 3 }, caravan.PawnThingIds);

        var zoneCreate = CommandPayloads.ReadZoneCreate(CommandPayloads.ZoneCreate(1, 0, "Stock"));
        Assert.Equal((byte)1, zoneCreate.ZoneKind);
        Assert.Equal("Stock", zoneCreate.Label);
    }

    [Fact]
    public void Colony_and_zone_shadow_stores_apply_commands()
    {
        ColonyWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.BillSet, 1, 0, CommandPayloads.BillSet(1, 0, "Cut_StoneBlocks", 0, 3)));
        ColonyWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.ResearchStart, 1, 0, CommandPayloads.ResearchStart("ComplexFurniture")));
        ColonyWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.TradeAccept, 1, 0, CommandPayloads.TradeAccept(9, 123)));
        var cidPayload = CommandPayloads.CaravanForm(7, new[] { 100, 101 });
        ColonyWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.CaravanForm, 1, 0, cidPayload));

        Assert.Equal(1, ColonyWorld.Current.BillCount);
        Assert.Equal("ComplexFurniture", ColonyWorld.Current.CurrentResearch);
        Assert.Equal(1, ColonyWorld.Current.TradeCount);
        Assert.Equal(1, ColonyWorld.Current.CaravanCount);

        ZoneWorld.Current.Create(1, 0, "A");
        ZoneWorld.Current.AddCells(1, 0, new[] { 1, 2, 3, 4 });
        Assert.Equal(2, ZoneWorld.Current.CellCount(1));
        ZoneWorld.Current.RemoveCells(1, 0, new[] { 1, 2 });
        Assert.Equal(1, ZoneWorld.Current.CellCount(1));
    }

    [Fact]
    public void JoinPoint_chunks_round_trip()
    {
        var blob = Enumerable.Range(0, 300_000).Select(i => (byte)(i % 251)).ToArray();
        var jp = new JoinPoint(nextTick: 50, sessionSeed: 9, commandWatermark: 49, modlistHash: "abc", saveBlob: blob);
        var header = JoinPoint.ReadHeader(jp.ToHeaderBytes());
        Assert.Equal(50, header.NextTick);
        Assert.Equal(blob.Length, header.BlobLength);
        Assert.Equal("abc", header.ModlistHash);

        var transfer = new JoinPointTransfer();
        transfer.AcceptHeader(jp.ToHeaderBytes());
        foreach (var chunk in jp.EnumerateChunks())
            transfer.AcceptChunk(chunk);
        var completed = transfer.TryComplete();
        Assert.NotNull(completed);
        Assert.Equal(blob, completed!.SaveBlob);
        Assert.Equal(49, completed.CommandWatermark);
    }

    [Fact]
    public async Task Jip_transfers_blob_and_clears_pending()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        var blob = "SAVEBLOB"u8.ToArray();
        var mem = new MemorySaveLoad();
        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 42);
        host.JoinPointSource = new MemoryJoinPointSource(blob, "mods-hash");
        host.SetModlistHash("mods-hash");
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 1)
        {
            SaveLoad = mem,
        };

        JoinPoint? received = null;
        client.OnJoinPointReceived += jp => received = jp;

        await host.StartAsync(new NetEndpoint("loop", 0));
        // Advance host a bit so watermark > 0
        host.HostEmitNextBatch();
        host.TryAdvanceSim();
        host.JoinPointSource = new MemoryJoinPointSource(blob, "mods-hash");

        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        Assert.False(client.IsJoinInProgress);
        Assert.NotNull(received);
        Assert.Equal(blob, received!.SaveBlob);
        Assert.Equal(blob, mem.LastLoaded);
        Assert.Equal(42, client.SessionSeed);
        Assert.Equal("mods-hash", received.ModlistHash);
    }

    [Fact]
    public void Desync_dump_writes_log_and_cmds_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rimworldmp-p2-desync", Guid.NewGuid().ToString("N"));
        var rand = new SyncedRand(3);
        var hasher = new StateHasher();
        hasher.AddUtf8("x");
        var detector = new DesyncDetector(hasher, rand, dir);
        detector.IncrementCounter("Rand.Value");
        detector.IncrementCounter("Rand.Range");

        var cmds = new[]
        {
            new MpCommand(CommandOpcode.BillSet, 10, 0, CommandPayloads.BillSet(1, 0, "CookMeal", 0, 1)),
            new MpCommand(CommandOpcode.ResearchStart, 10, 1, CommandPayloads.ResearchStart("Smithing")),
        };
        Assert.False(detector.CheckRemote(10, "00"));
        Assert.True(detector.IsFrozen);
        Assert.True(File.Exists(detector.LastDesync!.LogPath));
        var json = Path.ChangeExtension(detector.LastDesync.LogPath, ".cmds.json");
        Assert.True(File.Exists(json));
        var text = File.ReadAllText(detector.LastDesync.LogPath);
        Assert.Contains("rngCounters", text);
        // Freeze path uses empty recent unless passed — exercise dump helper directly too
        var path = DesyncDump.Write(dir, 11, "aa", "bb", 3, 0, cmds, detector.SnapshotCounters());
        Assert.True(File.Exists(path));
        Assert.Contains("BillSet", File.ReadAllText(Path.ChangeExtension(path, ".cmds.json")));
    }
}

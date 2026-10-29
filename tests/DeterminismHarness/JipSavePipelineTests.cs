using RimworldMP.Core.Join;
using RimworldMP.Core.Session;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Tests.DeterminismHarness;

public class JipSavePipelineTests
{
    public JipSavePipelineTests()
    {
        GameLoad.Current = null;
    }

    [Fact]
    public void SaveBlobPipeline_round_trip_create_chunk_load()
    {
        var capture = new MemorySaveLoad { NextCapture = Enumerable.Range(0, 50_000).Select(i => (byte)(i % 251)).ToArray() };
        var loadTarget = new MemorySaveLoad();
        var completed = SaveBlobPipeline.RoundTrip(capture, nextTick: 10, sessionSeed: 3, commandWatermark: 9, "mods", loadTarget);
        Assert.Equal(capture.NextCapture, completed.SaveBlob);
        Assert.Equal(capture.NextCapture, loadTarget.LastLoaded);
        Assert.Equal(1, loadTarget.LoadCount);
    }

    [Fact]
    public void SaveBlobPipeline_empty_blob_skips_load()
    {
        var capture = new MemorySaveLoad { NextCapture = Array.Empty<byte>() };
        var loadTarget = new MemorySaveLoad();
        SaveBlobPipeline.RoundTrip(capture, 1, 1, 0, "m", loadTarget);
        Assert.Null(loadTarget.LastLoaded);
        Assert.Equal(0, loadTarget.LoadCount);
    }

    [Fact]
    public void SaveBlobPipeline_fails_loud_without_loader_for_non_empty()
    {
        var jp = SaveBlobPipeline.Create(
            new MemorySaveLoad { NextCapture = "SAVE"u8.ToArray() },
            2, 2, 1, "m");
        Assert.Throws<MissingSaveLoadException>(() => SaveBlobPipeline.Load(jp, loader: null));
    }

    [Fact]
    public void JoinPointTransfer_fails_loud_on_duplicate_chunk()
    {
        var blob = Enumerable.Range(0, NetProtocol.JoinPointChunkBytes + 10).Select(i => (byte)i).ToArray();
        var jp = new JoinPoint(1, 1, 0, "m", blob);
        var transfer = new JoinPointTransfer();
        transfer.AcceptHeader(jp.ToHeaderBytes());
        var chunks = jp.EnumerateChunks().ToList();
        transfer.AcceptChunk(chunks[0]);
        Assert.Throws<InvalidOperationException>(() => transfer.AcceptChunk(chunks[0]));
    }

    [Fact]
    public void JoinPointTransfer_CompleteOrThrow_fails_when_chunk_missing()
    {
        var blob = Enumerable.Range(0, NetProtocol.JoinPointChunkBytes + 50).Select(i => (byte)(i % 200)).ToArray();
        var jp = new JoinPoint(5, 9, 4, "m", blob);
        var transfer = new JoinPointTransfer();
        transfer.AcceptHeader(jp.ToHeaderBytes());
        var chunks = jp.EnumerateChunks().ToList();
        Assert.True(chunks.Count >= 2);
        transfer.AcceptChunk(chunks[0]);
        // Skip chunks[1]
        var ex = Assert.Throws<InvalidOperationException>(() => transfer.CompleteOrThrow());
        Assert.Contains("incomplete", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JoinPointTransfer_CompleteOrThrow_fails_on_overflow_chunk()
    {
        var jp = new JoinPoint(1, 1, 0, "m", new byte[16]);
        var transfer = new JoinPointTransfer();
        transfer.AcceptHeader(jp.ToHeaderBytes());
        // Craft a chunk that claims index 0 of 1 but carries too many bytes.
        var bad = new byte[8 + 32];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bad, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(4), 1);
        Assert.Throws<InvalidOperationException>(() => transfer.AcceptChunk(bad));
    }

    [Fact]
    public void JoinPointTransfer_reassembles_out_of_order_chunks()
    {
        var blob = Enumerable.Range(0, NetProtocol.JoinPointChunkBytes * 2 + 7).Select(i => (byte)(i % 251)).ToArray();
        var jp = new JoinPoint(3, 8, 2, "hash", blob);
        var transfer = new JoinPointTransfer();
        transfer.AcceptHeader(jp.ToHeaderBytes());
        var chunks = jp.EnumerateChunks().ToList();
        Assert.Equal(3, chunks.Count);
        // Deliver reverse order — indexes still unique and complete.
        transfer.AcceptChunk(chunks[2]);
        transfer.AcceptChunk(chunks[0]);
        transfer.AcceptChunk(chunks[1]);
        var completed = transfer.CompleteOrThrow();
        Assert.Equal(blob, completed.SaveBlob);
    }

    [Fact]
    public async Task Session_jip_create_from_ISaveLoad_capture()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        var blob = "PIPELINE-BLOB"u8.ToArray();
        var hostSave = new MemorySaveLoad { NextCapture = blob };
        var clientSave = new MemorySaveLoad();

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 77)
        {
            SaveLoad = hostSave,
        };
        host.SetModlistHash("mods");
        // No JoinPointSource — HostSendJoinPoint falls back to SaveLoad.CaptureSaveBlob.
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 1)
        {
            SaveLoad = clientSave,
        };

        JoinPoint? received = null;
        client.OnJoinPointReceived += jp => received = jp;

        await host.StartAsync(new NetEndpoint("loop", 0));
        host.HostEmitNextBatch();
        host.TryAdvanceSim();
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        Assert.NotNull(received);
        Assert.Equal(blob, received!.SaveBlob);
        Assert.Equal(blob, clientSave.LastLoaded);
        Assert.False(client.IsJoinInProgress);
    }
}

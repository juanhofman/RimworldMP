using System.Net.Sockets;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimworldMP.Core.Sync;
using RimworldMP.Launcher.Services;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Tests.DeterminismHarness;

/// <summary>
/// CI-focused Core / Net / launcher-mirror invariants (no RimWorld install).
/// Complements phase suites with golden vectors and hard-gate reject paths.
/// </summary>
public class CiImportantPathTests
{
    /// <summary>
    /// Golden vector from docs/launcher-mvp.md — must stay locked across Core + launcher mirror.
    /// </summary>
    public const string GoldenHarmonyRimworldMp =
        "f2046ab21e526aae4469bdf1418310b553a2c34b9b9b617cc148a00d76e963f6";

    [Fact]
    public void ModlistHash_golden_is_stable()
    {
        var hash = ModlistHash.Compute(new[] { "brrainz.harmony", "RimworldMP.Core" });
        Assert.Equal(GoldenHarmonyRimworldMp, hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hash.ToLowerInvariant());

        // Trim + empty-skip must not change golden.
        var trimmed = ModlistHash.Compute(new[] { " brrainz.harmony ", "", "RimworldMP.Core" });
        Assert.Equal(GoldenHarmonyRimworldMp, trimmed);
    }

    [Fact]
    public void Launcher_ModlistHasher_mirrors_Core_golden()
    {
        var core = ModlistHash.Compute(new[] { "brrainz.harmony", "RimworldMP.Core" });
        var launcher = ModlistHasher.Compute(new[] { "brrainz.harmony", "RimworldMP.Core" });
        Assert.Equal(core, launcher);
        Assert.Equal(GoldenHarmonyRimworldMp, launcher);
        Assert.Equal(GoldenHarmonyRimworldMp, ModlistHasher.GoldenHarmonyRimworldMpHash);
        ModlistHasher.AssertGoldenMatchesCoreExpectation();
        Assert.True(ModlistHash.Matches(core, launcher));
        Assert.True(ModlistHasher.Matches(core, launcher));
    }

    [Fact]
    public void Protocol_v4_hard_gate_helpers_and_rejects()
    {
        Assert.Equal(4, NetProtocol.Version);
        Assert.Equal(3, NetProtocol.MinSupportedVersion);
        Assert.True(NetProtocol.IsCompatible(3));
        Assert.True(NetProtocol.IsCompatible(4));
        Assert.False(NetProtocol.IsCompatible(2));
        Assert.False(NetProtocol.IsCompatible(1));

        var reject = LanTcpTransport.BuildJoinReject(JoinRejectReason.ProtocolMismatch, "bad proto");
        Assert.Equal((byte)JoinRejectReason.ProtocolMismatch, reject[0]);

        var modReject = LanTcpTransport.BuildJoinReject(JoinRejectReason.ModlistMismatch, "mods");
        Assert.Equal((byte)JoinRejectReason.ModlistMismatch, modReject[0]);

        var full = LanTcpTransport.BuildJoinReject(JoinRejectReason.LobbyFull, "full");
        Assert.Equal((byte)JoinRejectReason.LobbyFull, full[0]);
    }

    [Fact]
    public async Task Lan_rejects_protocol_mismatch_before_session()
    {
        var port = FreePort();
        var hostLan = new LanTcpTransport { MaxClients = 4, ExpectedModlistHash = "same" };
        await using var host = new MpSession(hostLan, isHost: true, sessionSeed: 3);
        host.SetModlistHash("same");
        await host.StartAsync(new NetEndpoint("127.0.0.1", port));

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        var stream = client.GetStream();
        // Magically valid RMP3 framing but protocol version 1 (below MinSupportedVersion).
        var handshake = LanTcpTransport.BuildHandshakePayload(version: 1, modlistHash: "same");
        await FrameCodec.WriteFrameAsync(stream, NetMessageType.Handshake, handshake);

        var (type, payload) = await FrameCodec.ReadFrameAsync(stream);
        Assert.Equal(NetMessageType.JoinReject, type);
        Assert.Equal((byte)JoinRejectReason.ProtocolMismatch, payload[0]);
    }

    [Fact]
    public async Task Lan_rejects_lobby_full()
    {
        var port = FreePort();
        // MpSession clamps MaxClients < 1 up to MpConfig — use a filled 1-slot lobby.
        var hostLan = new LanTcpTransport { MaxClients = 1, ExpectedModlistHash = "mods" };
        await using var host = new MpSession(hostLan, isHost: true, sessionSeed: 5);
        host.SetModlistHash("mods");
        await host.StartAsync(new NetEndpoint("127.0.0.1", port));

        await using var occupant = new MpSession(
            new LanTcpTransport { LocalModlistHash = "mods" },
            isHost: false,
            sessionSeed: 1);
        occupant.SetModlistHash("mods");
        await occupant.StartAsync(new NetEndpoint("127.0.0.1", port));
        await TestHelpers.WaitForJipAsync(occupant);

        var lateLan = new LanTcpTransport { LocalModlistHash = "mods" };
        await using var late = new MpSession(lateLan, isHost: false, sessionSeed: 1);
        late.SetModlistHash("mods");

        Exception? ex = null;
        try
        {
            await late.StartAsync(new NetEndpoint("127.0.0.1", port));
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        Assert.NotNull(ex);
        // Prefer typed LobbyFull reject. Some Windows stacks reset the socket before the reject
        // frame is observed — accept IOException only when the reason was not captured.
        if (lateLan.LastJoinRejectReason is { } reason)
        {
            Assert.Equal(JoinRejectReason.LobbyFull, reason);
            if (ex is InvalidOperationException ioe)
                Assert.Contains("Lobby", ioe.Message, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.True(
                ex is IOException or InvalidOperationException,
                $"Expected LobbyFull reject or connection reset, got {ex.GetType()}: {ex.Message}");
        }
    }

    [Fact]
    public void CommandBatch_roundtrips_key_opcodes()
    {
        var cmds = new[]
        {
            new MpCommand(CommandOpcode.Draft, 100, 1, CommandPayloads.Draft(42, true)),
            new MpCommand(CommandOpcode.JobGive, 100, 1,
                CommandPayloads.JobGive(mapId: 2, pawnLoadId: 42, cellX: 10, cellZ: 11, jobDefName: "Goto")),
            new MpCommand(CommandOpcode.ZoneAdd, 100, 0,
                CommandPayloads.ZoneCells(zoneKind: 1, mapId: 2, cellsPacked: new[] { 10, 11, 12, 13 })),
            new MpCommand(CommandOpcode.Pause, 100, 0, CommandPayloads.Pause(true)),
            new MpCommand(CommandOpcode.Speed, 100, 0, CommandPayloads.Speed(3)),
        };

        var batch = new CommandBatch(100, cmds);
        var restored = CommandBatch.FromBytes(batch.ToBytes());
        Assert.Equal(100, restored.Tick);
        Assert.Equal(5, restored.Commands.Count);

        Assert.Equal(CommandOpcode.Draft, restored.Commands[0].Opcode);
        var draft = CommandPayloads.ReadDraft(restored.Commands[0].Payload);
        Assert.Equal(42, draft.PawnLoadId);
        Assert.True(draft.Drafted);

        Assert.Equal(CommandOpcode.JobGive, restored.Commands[1].Opcode);
        var job = CommandPayloads.ReadJobGive(restored.Commands[1].Payload);
        Assert.Equal(2, job.MapId);
        Assert.Equal("Goto", job.JobDefName);

        Assert.Equal(CommandOpcode.ZoneAdd, restored.Commands[2].Opcode);
        var zone = CommandPayloads.ReadZoneCells(restored.Commands[2].Payload);
        Assert.Equal(2, zone.MapId);
        Assert.Equal(new[] { 10, 11, 12, 13 }, zone.CellsPacked);

        Assert.Equal(CommandOpcode.Pause, restored.Commands[3].Opcode);
        Assert.True(CommandPayloads.ReadPause(restored.Commands[3].Payload));

        Assert.Equal(CommandOpcode.Speed, restored.Commands[4].Opcode);
        Assert.Equal((byte)3, CommandPayloads.ReadSpeed(restored.Commands[4].Payload));
    }

    [Fact]
    public void Desync_fingerprint_changes_when_rng_counters_change()
    {
        var rand = new SyncedRand(12345);
        var hasher = new StateHasher();
        hasher.AddProtocolVersion(NetProtocol.Version);
        var detector = new DesyncDetector(hasher, rand, Path.Combine(Path.GetTempPath(), "rimworldmp-ci-desync"));

        var baseline = detector.SnapshotHashHex(50);
        Assert.Equal(baseline, detector.SnapshotHashHex(50));

        detector.IncrementCounter("Rand.Value", 1);
        var afterValue = detector.SnapshotHashHex(50);
        Assert.NotEqual(baseline, afterValue);

        detector.IncrementCounter("Rand.RangeInclusive", 2);
        var afterRange = detector.SnapshotHashHex(50);
        Assert.NotEqual(afterValue, afterRange);
        Assert.NotEqual(baseline, afterRange);

        // Same counter totals ⇒ same fingerprint across fresh detectors.
        var rand2 = new SyncedRand(12345);
        var hasher2 = new StateHasher();
        hasher2.AddProtocolVersion(NetProtocol.Version);
        var detector2 = new DesyncDetector(hasher2, rand2, Path.Combine(Path.GetTempPath(), "rimworldmp-ci-desync-b"));
        detector2.IncrementCounter("Rand.Value", 1);
        detector2.IncrementCounter("Rand.RangeInclusive", 2);
        Assert.Equal(afterRange, detector2.SnapshotHashHex(50));
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

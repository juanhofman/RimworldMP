using RimworldMP.Core.Join;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Lobby;
using RimworldMP.Core.Session;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;
using RimworldMP.UI;

namespace RimworldMP.Tests.DeterminismHarness;

public class SessionQolTests
{
    public SessionQolTests()
    {
        MpConfig.ResetToDefaults();
        SessionMessages.ResetToEnglish();
        SessionFacade.Clear();
    }

    [Fact]
    public void SessionMessages_en_defaults_cover_mismatch_keys()
    {
        Assert.Contains("Protocol mismatch", SessionMessages.ProtocolMismatch(2, 3, 3), StringComparison.Ordinal);
        Assert.Contains("Modlist hash mismatch", SessionMessages.ModlistMismatch("aaa", "bbb"), StringComparison.Ordinal);
        Assert.Contains("DLC mismatch", SessionMessages.DlcMismatch("dlc-a", "dlc-b"), StringComparison.Ordinal);
        Assert.Contains("Lobby full", SessionMessages.LobbyFull(4), StringComparison.Ordinal);
        Assert.Equal(JoinRejectReason.DlcMismatch, (JoinRejectReason)4);
        Assert.Contains("frozen", SessionMessages.Get("RMP.DesyncHeadline"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Continue", SessionMessages.Get("RMP.DesyncBodyShort"), StringComparison.Ordinal);
        Assert.Contains("local only", SessionMessages.Format("RMP.DesyncBodyDetail", 1, "a", "b", "", "/tmp/x"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resync", SessionMessages.Get("RMP.BtnContinueHost"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionMessages_optional_nl_strings_overlay()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "RimworldMP.Core", "Session", "Locale", "SessionMessages.nl.strings"));
        if (!File.Exists(path))
        {
            // Fallback: walk up from cwd
            path = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "src", "RimworldMP.Core", "Session", "Locale", "SessionMessages.nl.strings"));
        }

        Assert.True(File.Exists(path), $"Missing NL strings at {path}");
        var loaded = SessionMessages.LoadOptional(path, SessionMessages.CultureNl);
        Assert.True(loaded >= 10);
        Assert.Equal(SessionMessages.CultureNl, SessionMessages.Culture);
        Assert.Contains("Protocolversie", SessionMessages.ProtocolMismatch(2, 3, 3), StringComparison.Ordinal);
        SessionMessages.ResetToEnglish();
        Assert.Contains("Protocol mismatch", SessionMessages.ProtocolMismatch(2, 3, 3), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_syncs_host_to_client_without_entering_sim_hasher()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 11);
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 11);
        host.SetDisplayName("Ada");
        client.SetDisplayName("Bea");

        SessionChatLine? clientLine = null;
        client.OnChatLine += line => clientLine = line;

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        var hashBefore = host.Hasher.CurrentHashHex();
        host.SendChat("hello colony");
        await Task.Delay(20);

        Assert.NotNull(clientLine);
        Assert.Equal(SessionChatKind.Chat, clientLine!.Value.Kind);
        Assert.Equal("hello colony", clientLine.Value.Text);
        Assert.Contains(host.Chat.Snapshot(), l => l.Text == "hello colony");
        // Chat must not mutate sim hasher (out-of-band session message).
        Assert.Equal(hashBefore, host.Hasher.CurrentHashHex());
    }

    [Fact]
    public async Task Display_names_propagate_via_player_info()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 3);
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 3);

        await host.StartAsync(new NetEndpoint("loop", 0));
        host.SetDisplayName("ColonyHost");
        client.SetDisplayName("Joiner");
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);
        await Task.Delay(30);

        Assert.Equal("ColonyHost", host.Roster.GetName(0, isHostSeat: true));
        Assert.Equal("Joiner", host.Roster.GetName(1));
        Assert.Equal("ColonyHost", client.Roster.GetName(0, isHostSeat: true));
        Assert.Equal("Joiner", client.LocalDisplayName);
    }

    [Fact]
    public async Task Host_kick_marks_client_and_clears_ready()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 5);
        host.Ready.Required = true;
        host.ExpectedPeers = 2;
        host.PauseOnPeerLeave = true;
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 5);

        var kicked = false;
        client.OnKicked += () => kicked = true;

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        host.SetReady(true);
        client.SetReady(true);
        await Task.Delay(20);
        host.Ready.SetPeer(1, true, host.ModlistHash);
        Assert.True(host.IsLobbyReady);

        Assert.True(host.HostKickPeer(1, "afk"));
        await Task.Delay(30);

        Assert.True(kicked);
        Assert.True(client.WasKicked);
        Assert.False(host.Ready.Snapshot().ContainsKey(1));
        Assert.Contains(host.Chat.Snapshot(), l => l.Kind == SessionChatKind.System && l.NoticeKind == SystemNoticeKind.PeerKicked);
        Assert.False(host.IsLobbyReady);
    }

    [Fact]
    public async Task Ready_gate_blocks_unpause_command()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 7);
        host.Ready.Required = true;
        host.ExpectedPeers = 2;
        host.SetModlistHash("mods");
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 7);
        client.SetModlistHash("mods");

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        host.SetReady(true);
        // Client not ready → unpause blocked
        Assert.False(host.CanStartOrUnpause);
        host.SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(false));
        host.HostEmitNextBatch();
        Assert.False(host.TryAdvanceSim()); // no batch — emit blocked by ready gate

        client.SetReady(true);
        await Task.Delay(20);
        host.Ready.SetPeer(1, true, "mods");
        Assert.True(host.CanStartOrUnpause);
        host.SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(false));
        host.HostEmitNextBatch();
        Assert.True(host.TryAdvanceSim());
    }

    [Fact]
    public async Task PumpPausedCommands_flushes_unpause_without_DoSingleTick()
    {
        // Mirrors live bug: after Begin play the game stays paused, DoSingleTick never runs,
        // staged Pause(false) must still flush via PumpPausedCommands (UIRoot OnGUI path).
        var hostTransport = new LoopbackTransport();
        var sink = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, sink);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 11);
        host.Ready.Required = true;
        host.SetModlistHash("mods");
        await host.StartAsync(new NetEndpoint("loop", 0));
        // Host seat is ready from ctor; refresh hash without needing a real client peer.
        host.Ready.SetPeer(0, ready: true, modlistHash: "mods");
        host.LockExpectedPeersToPresent();
        Assert.Equal(1, host.ExpectedPeers);
        Assert.True(host.CanStartOrUnpause);

        host.SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(false));
        Assert.True(host.HasStagedCommands);
        Assert.True(host.PumpPausedCommands());
        Assert.False(host.HasStagedCommands);
        Assert.True(host.Tick >= 1);
    }

    [Fact]
    public async Task Recovery_rejoin_after_freeze_restores_join_point()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        var blob = "RECOVER-QOL"u8.ToArray();
        var mem = new MemorySaveLoad();
        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 19);
        host.JoinPointSource = new MemoryJoinPointSource(blob, "m");
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 19)
        {
            SaveLoad = mem,
        };

        JoinPoint? jp = null;
        client.OnJoinPointReceived += p => jp = p;

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        client.Desync.Freeze(1, "aaa", "bbb", Array.Empty<MpCommand>());
        Assert.True(client.IsFrozen);

        jp = null;
        SessionRecovery.RequestRejoin(client);
        await TestHelpers.WaitForJipAsync(client);

        Assert.False(client.IsFrozen);
        Assert.NotNull(jp);
        Assert.Equal(blob, jp!.SaveBlob);
        Assert.Contains(client.Chat.Snapshot(), l => l.NoticeKind == SystemNoticeKind.Recovery);
    }

    [Fact]
    public async Task SoftRecover_host_clears_freeze_and_resyncs_client()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        var blob = "SOFT"u8.ToArray();
        var mem = new MemorySaveLoad();
        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 33)
        {
            JoinPointSource = new MemoryJoinPointSource(blob, "m"),
        };
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 33)
        {
            SaveLoad = mem,
        };

        JoinPoint? jp = null;
        client.OnJoinPointReceived += p => jp = p;

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        host.Desync.Freeze(60, "aaa", "bbb", Array.Empty<MpCommand>());
        client.Desync.Freeze(60, "bbb", "aaa", Array.Empty<MpCommand>());
        Assert.True(host.IsFrozen);
        Assert.True(client.IsFrozen);

        jp = null;
        SessionRecovery.SoftRecover(host);
        Assert.False(host.IsFrozen);

        SessionRecovery.SoftRecover(client);
        await TestHelpers.WaitForJipAsync(client);
        Assert.False(client.IsFrozen);
        Assert.NotNull(jp);
        Assert.Equal(blob, jp!.SaveBlob);
    }

    [Fact]
    public async Task Host_resync_peer_helper_sends_recovery_point()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        var blob = "RESYNC"u8.ToArray();
        var mem = new MemorySaveLoad();
        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 21);
        host.JoinPointSource = new MemoryJoinPointSource(blob, "m");
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 21) { SaveLoad = mem };

        JoinPoint? jp = null;
        client.OnJoinPointReceived += p => jp = p;

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        jp = null;
        Assert.True(SessionRecovery.TryHostResyncPeer(host, new NetPeerId(1)));
        await TestHelpers.WaitForJipAsync(client);
        Assert.NotNull(jp);
        Assert.Equal(blob, jp!.SaveBlob);
        Assert.Contains(host.Chat.Snapshot(), l => l.NoticeKind == SystemNoticeKind.Recovery);
    }

    [Fact]
    public void Handoff_player_display_name_roundtrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rimworldmp-session-qol", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "launcher-handoff.json");
        LauncherHandoff.Write(new LauncherHandoffDocument
        {
            Role = "client",
            Host = "10.0.0.2",
            Port = 30555,
            PlayerDisplayName = "Ada Lovelace",
            AutoStartLobby = false,
        }, path);

        var doc = LauncherHandoff.TryRead(path);
        Assert.NotNull(doc);
        Assert.Equal("Ada Lovelace", doc!.PlayerDisplayName);
    }

    [Fact]
    public async Task Presenter_exposes_kick_chat_and_display_name()
    {
        var transport = new LoopbackTransport();
        await using var session = new MpSession(transport, isHost: true, sessionSeed: 2);
        session.Ready.Required = true;
        SessionFacade.Attach(session);
        await session.StartAsync(new NetEndpoint("loop", 0));
        session.SetDisplayName("Hosty");
        session.Ready.SetPeer(0, true, session.ModlistHash);
        session.Ready.SetPeer(1, false, "other");
        session.Roster.SetName(1, "Guest");
        session.SendChat("hi");

        var vm = LobbyPresenter.Capture(session);
        Assert.True(vm.CanKick);
        Assert.True(vm.CanChat);
        Assert.True(vm.CanToggleReady);
        Assert.Equal("Hosty", vm.LocalDisplayName);
        Assert.Contains(vm.Peers, p => p.PlayerId == 1 && p.CanKick && p.DisplayName.Contains("Guest", StringComparison.Ordinal));
        Assert.Contains(vm.ChatLines, c => !c.IsSystem && c.Text == "hi");
    }
}

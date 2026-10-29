using RimworldMP.Core.Lobby;
using RimworldMP.Core.Session;
using RimworldMP.Core.Sync;
using RimworldMP.Core.World;
using RimworldMP.Lobby;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;
using RimworldMP.UI;

namespace RimworldMP.Tests.DeterminismHarness;

public class LobbyUiTests
{
    public LobbyUiTests()
    {
        MpConfig.ResetToDefaults();
        HandoffUiTracker.Reset();
        SessionFacade.Clear();
        ZoneWorld.Current.Reset();
        ColonyWorld.Current.Reset();
    }

    [Fact]
    public void Palette_matches_design_brief_hex()
    {
        Assert.Equal("RimworldMP", MpUiPalette.BrandName);
        Assert.Equal("#0E1114", MpUiPalette.InkHex);
        Assert.Equal("#7EB0C8", MpUiPalette.SteelHex);
        Assert.Equal("#E07A3D", MpUiPalette.EmberHex);
        Assert.False(LobbyUi.IsVerseUiAvailable); // harness builds with NO_GAME_REFS
        LobbyUi.Open(); // stub no-op
    }

    [Fact]
    public void Presenter_idle_snapshot_exposes_brand_and_cta_flags()
    {
        HandoffUiTracker.RecordMissing("/tmp/launcher-handoff.json");
        var vm = LobbyPresenter.Capture(session: null, configuredPort: 30555, localModlistOverride: "abc");

        Assert.Equal(MpUiPalette.BrandName, vm.Brand);
        Assert.False(vm.InSession);
        Assert.True(vm.CanHost);
        Assert.True(vm.CanJoin);
        Assert.False(vm.CanDisconnect);
        Assert.Equal(30555, vm.Port);
        Assert.Contains("v3", vm.ProtocolLabel, StringComparison.Ordinal);
        Assert.Equal(HandoffUiKind.Missing, vm.Handoff.Kind);
        Assert.Empty(vm.Peers);
        Assert.Contains("handoff", vm.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Presenter_host_session_lists_local_peer_and_lan_status()
    {
        var port = FreePort();
        const string hash = "lobby-ui-mods";
        await using var host = new MpSession(
            new LanTcpTransport { ExpectedModlistHash = hash, MaxClients = 4 },
            isHost: true,
            sessionSeed: 21);
        host.SetModlistHash(hash);
        host.Ready.Required = true;
        SessionFacade.Attach(host);
        await host.StartAsync(new NetEndpoint("127.0.0.1", port));
        host.SetReady(true);

        var vm = LobbyPresenter.Capture(host);
        Assert.True(vm.InSession);
        Assert.True(vm.IsHost);
        Assert.True(vm.CanDisconnect);
        Assert.False(vm.CanHost);
        Assert.Contains("Hosting", vm.LanStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(vm.Peers, p => p.IsLocal && p.IsHostSeat);
        Assert.All(vm.Peers, p => Assert.True(p.ModlistMatch));
        Assert.Equal(hash, vm.LocalModlistHash);
    }

    [Fact]
    public async Task Presenter_marks_modlist_mismatch_on_peer_row()
    {
        var transport = new LoopbackTransport();
        await using var session = new MpSession(transport, isHost: true, sessionSeed: 3);
        session.SetModlistHash("host-hash");
        session.Ready.Required = true;
        SessionFacade.Attach(session);
        await session.StartAsync(new NetEndpoint("loop", 0));
        session.Ready.SetPeer(0, ready: true, modlistHash: "host-hash");
        session.Ready.SetPeer(1, ready: false, modlistHash: "other-hash");

        var vm = LobbyPresenter.Capture(session);
        Assert.Equal(2, vm.Peers.Count);
        var remote = Assert.Single(vm.Peers, p => p.PlayerId == 1);
        Assert.False(remote.ModlistMatch);
        Assert.Contains("MISMATCH", remote.ModlistLabel, StringComparison.Ordinal);
        Assert.False(remote.Ready);
    }

    [Fact]
    public void Desync_overlay_model_shortens_hashes()
    {
        var info = new DesyncInfo
        {
            Tick = 120,
            LocalHashHex = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            RemoteHashHex = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            LogPath = "/tmp/rimworldmp/desync/desync-tick120.log",
            ModlistHashShort = "f2046ab21e52",
        };
        var model = DesyncOverlayModel.From(info);
        Assert.NotNull(model);
        Assert.Equal(120, model!.Tick);
        Assert.Equal("0123456789ab…", model.LocalHashShort);
        Assert.Contains("frozen", model.Headline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("120", model.Body, StringComparison.Ordinal);
        Assert.Contains("local only", model.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(info.LogPath, model.Body, StringComparison.Ordinal);
        Assert.Contains("f2046ab21e52", model.Body, StringComparison.Ordinal);
        Assert.Equal(info.LogPath, model.LogPath);
    }

    [Fact]
    public async Task Handoff_consume_updates_tracker_config_only()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rimworldmp-lobby-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "launcher-handoff.json");
        LauncherHandoff.Write(new LauncherHandoffDocument
        {
            Role = "host",
            Port = 30555,
            ModlistHash = "deadbeef",
            AutoStartLobby = false,
            SessionName = "UI Test",
            MaxClients = 3,
        }, path);

        var found = await MpLobbyActions.TryConsumeLauncherHandoffAsync(path);
        Assert.True(found);
        Assert.Equal("deadbeef", MpConfig.ModlistHash);
        Assert.Equal(3, MpConfig.MaxClients);
        Assert.Equal(HandoffUiKind.AppliedConfigOnly, HandoffUiTracker.Current.Kind);
        Assert.Contains("config only", HandoffUiTracker.Current.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".ack"));
    }

    [Fact]
    public async Task Handoff_missing_records_missing_kind()
    {
        var path = Path.Combine(Path.GetTempPath(), "rimworldmp-lobby-ui-missing", Guid.NewGuid().ToString("N"), "nope.json");
        var found = await MpLobbyActions.TryConsumeLauncherHandoffAsync(path);
        Assert.False(found);
        Assert.Equal(HandoffUiKind.Missing, HandoffUiTracker.Current.Kind);
    }

    [Fact]
    public async Task Presenter_frozen_status_surfaces_desync_copy()
    {
        var transport = new LoopbackTransport();
        await using var session = new MpSession(transport, isHost: true, sessionSeed: 1);
        await session.StartAsync(new NetEndpoint("loop", 0));
        session.Desync.Freeze(60, "aaaaaaaaaaaa", "bbbbbbbbbbbb", Array.Empty<Core.Commands.MpCommand>());

        var vm = LobbyPresenter.Capture(session);
        Assert.True(session.IsFrozen);
        Assert.True(vm.IsFrozen);
        Assert.NotNull(vm.Desync);
        Assert.Contains("DESYNC", vm.StatusLine, StringComparison.Ordinal);
        Assert.Equal(NetProtocol.Version, (byte)3);
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

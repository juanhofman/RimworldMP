#if !RIMWORLDMP_NO_GAME_REFS
using RimworldMP.Core.Session;
using RimworldMP.Net.Protocol;
using UnityEngine;
using Verse;

namespace RimworldMP.Lobby;

/// <summary>
/// Non-DevMode host/join window. Opened from the main-menu / pause-menu buttons and F10.
/// Exact Verse Window layout APIs should be re-checked under RIMWORLD_DIR.
/// </summary>
public sealed class Dialog_RimworldMPLobby : Window
{
    private string _host = "127.0.0.1";
    private string _port = NetProtocol.DefaultPort.ToString();
    private string _modlistHash = MpConfig.ModlistHash;
    private string _status = "Idle";

    public Dialog_RimworldMPLobby()
    {
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        draggable = true;
    }

    public override Vector2 InitialSize => new(420f, 360f);

    public override void DoWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        Text.Font = GameFont.Medium;
        listing.Label("RimworldMP");
        Text.Font = GameFont.Small;
        listing.GapLine();

        listing.Label($"Protocol v{NetProtocol.Version} · max clients default {MpConfig.DefaultMaxClients}");
        listing.Label($"Session: {(SessionFacade.IsInSession ? (SessionFacade.IsHost ? "hosting" : "joined") : "none")}");
        listing.Gap();

        listing.Label("Host IP (for join)");
        _host = listing.TextEntry(_host);
        listing.Label("Port");
        _port = listing.TextEntry(_port);
        listing.Label("Modlist hash (hard-gated at join)");
        _modlistHash = listing.TextEntry(_modlistHash);
        listing.Gap();

        if (listing.ButtonText("Host lobby"))
        {
            if (!int.TryParse(_port, out var port))
                port = MpConfig.DefaultPort;
            MpConfig.ModlistHash = _modlistHash ?? string.Empty;
            _ = MpLobbyActions.HostAsync(port: port, modlistHash: _modlistHash);
            _status = $"Hosting on {port}…";
            Messages.Message($"RimworldMP: hosting on port {port}", MessageTypeDefOf.TaskCompletion, false);
        }

        if (listing.ButtonText("Join lobby"))
        {
            if (!int.TryParse(_port, out var port))
                port = MpConfig.DefaultPort;
            MpConfig.ModlistHash = _modlistHash ?? string.Empty;
            _ = MpLobbyActions.JoinAsync(_host, port: port, modlistHash: _modlistHash);
            _status = $"Joining {_host}:{port}…";
            Messages.Message($"RimworldMP: joining {_host}:{port}", MessageTypeDefOf.TaskCompletion, false);
        }

        if (listing.ButtonText("Stop session"))
        {
            _ = MpLobbyActions.StopAsync();
            _status = "Stopped";
            Messages.Message("RimworldMP: session stopped", MessageTypeDefOf.TaskCompletion, false);
        }

        listing.Gap();
        listing.Label(_status);
        listing.Label("Tip: F10 toggles this window. Launcher may auto-start via launcher-handoff.json.");
        listing.End();
    }

    public static void Open()
    {
        Find.WindowStack?.Add(new Dialog_RimworldMPLobby());
    }
}
#endif

#if !RIMWORLDMP_NO_GAME_REFS
using RimworldMP.Core.Session;
using RimworldMP.Lobby;
using RimworldMP.Net.Protocol;
using UnityEngine;
using Verse;

namespace RimworldMP.UI;

/// <summary>
/// Release lobby window — brand hero, peers (kick/ready), chat, host/join/disconnect, handoff status.
/// Layout uses Immediate GUI within Verse <see cref="Window"/> limits; re-check under RIMWORLD_DIR.
/// </summary>
public sealed class Dialog_RimworldMPLobby : Window
{
    private string _host = "127.0.0.1";
    private string _port = NetProtocol.DefaultPort.ToString();
    private string _modlistHash = MpConfig.ModlistHash;
    private string _displayName = MpConfig.PlayerDisplayName;
    private string _chatDraft = string.Empty;
    private Vector2 _peerScroll;
    private Vector2 _chatScroll;
    private float _pulseT;
    private string? _actionFlash;

    public Dialog_RimworldMPLobby()
    {
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        draggable = true;
        forcePause = false;
    }

    public override Vector2 InitialSize => new(540f, 640f);

    public override void PreOpen()
    {
        base.PreOpen();
        _port = MpConfig.DefaultPort.ToString();
        if (string.IsNullOrEmpty(_modlistHash))
            _modlistHash = MpConfig.ModlistHash;
        if (string.IsNullOrEmpty(_displayName))
            _displayName = MpConfig.PlayerDisplayName;
        var handoff = HandoffUiTracker.Current;
        if (!string.IsNullOrEmpty(handoff.Host) && handoff.Kind == HandoffUiKind.AutoJoined)
            _host = handoff.Host!;
        if (handoff.Port is int p && p > 0)
            _port = p.ToString();
        if (!string.IsNullOrEmpty(handoff.ModlistHash))
            _modlistHash = handoff.ModlistHash!;
        if (!string.IsNullOrEmpty(handoff.SessionName) && string.IsNullOrEmpty(_displayName))
            _displayName = handoff.SessionName!;
    }

    public override void DoWindowContents(Rect inRect)
    {
        _pulseT += Time.unscaledDeltaTime;
        var vm = LobbyPresenter.Capture(localModlistOverride: _modlistHash);

        Widgets.DrawBoxSolid(inRect, ToColor(MpUiPalette.Ink));

        var y = inRect.y + 6f;
        var x = inRect.x + 12f;
        var w = inRect.width - 24f;

        Text.Font = GameFont.Medium;
        GUI.color = ToColor(MpUiPalette.Ember);
        Widgets.Label(new Rect(x, y, w, 32f), vm.Brand);
        GUI.color = Color.white;
        y += 30f;
        Text.Font = GameFont.Tiny;
        GUI.color = ToColor(MpUiPalette.Steel);
        Widgets.Label(new Rect(x, y, w, 18f), vm.Subtitle);
        GUI.color = Color.white;
        y += 22f;

        DrawHairline(x, y, w, ToColor(MpUiPalette.Steel) * 0.35f);
        y += 10f;

        Text.Font = GameFont.Small;
        GUI.color = vm.WasKicked || vm.IsFrozen ? ToColor(MpUiPalette.Danger) : ToColor(MpUiPalette.Soft);
        Widgets.Label(new Rect(x, y, w, 22f), $"Session · {vm.SessionRoleLabel}");
        GUI.color = Color.white;
        y += 20f;

        GUI.color = ToColor(MpUiPalette.Steel);
        Widgets.Label(new Rect(x, y, w, 18f), $"{vm.ProtocolLabel}  ·  {vm.LanStatus}");
        y += 18f;
        Widgets.Label(new Rect(x, y, w, 18f), $"Endpoint  {vm.EndpointLabel}");
        y += 18f;
        Widgets.Label(new Rect(x, y, w, 18f), $"Modlist   {vm.LocalModlistShort}");
        GUI.color = Color.white;
        y += 22f;

        if (vm.InSession && vm.IsHost && !vm.IsFrozen)
        {
            var pulse = 0.55f + 0.45f * Mathf.Sin(_pulseT * 2.4f);
            var live = ToColor(MpUiPalette.Ok);
            live.a = pulse;
            Widgets.DrawBoxSolid(new Rect(x, y, 8f, 8f), live);
            GUI.color = ToColor(MpUiPalette.Ok);
            Widgets.Label(new Rect(x + 14f, y - 4f, w - 14f, 18f), "LIVE");
            GUI.color = Color.white;
            y += 20f;
        }

        y = DrawHandoffBlock(x, y, w, vm.Handoff);
        y += 8f;

        if (!vm.InSession)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = ToColor(MpUiPalette.Dim);
            Widgets.Label(new Rect(x, y, w, 16f), "DISPLAY NAME");
            GUI.color = Color.white;
            y += 16f;
            _displayName = Widgets.TextField(new Rect(x, y, w, 28f), _displayName);
            y += 34f;

            GUI.color = ToColor(MpUiPalette.Dim);
            Widgets.Label(new Rect(x, y, w, 16f), "HOST IP (join)");
            GUI.color = Color.white;
            y += 16f;
            _host = Widgets.TextField(new Rect(x, y, w, 28f), _host);
            y += 34f;

            GUI.color = ToColor(MpUiPalette.Dim);
            Widgets.Label(new Rect(x, y, w * 0.45f, 16f), "PORT");
            Widgets.Label(new Rect(x + w * 0.5f, y, w * 0.5f, 16f), "MODLIST HASH");
            GUI.color = Color.white;
            y += 16f;
            _port = Widgets.TextField(new Rect(x, y, w * 0.42f, 28f), _port);
            _modlistHash = Widgets.TextField(new Rect(x + w * 0.48f, y, w * 0.52f, 28f), _modlistHash);
            y += 36f;
        }
        else if (!string.IsNullOrEmpty(vm.LocalDisplayName))
        {
            Text.Font = GameFont.Tiny;
            GUI.color = ToColor(MpUiPalette.Steel);
            Widgets.Label(new Rect(x, y, w, 16f), $"Playing as {vm.LocalDisplayName}");
            GUI.color = Color.white;
            y += 20f;
        }

        Text.Font = GameFont.Small;
        GUI.color = ToColor(MpUiPalette.Steel);
        Widgets.Label(new Rect(x, y, w, 20f), vm.InSession ? $"Peers ({vm.Peers.Count})" : "Peers — join a session");
        GUI.color = Color.white;
        y += 22f;

        var peerAreaH = Mathf.Min(120f, Mathf.Max(48f, inRect.yMax - y - 220f));
        var peerOuter = new Rect(x, y, w, peerAreaH);
        Widgets.DrawBoxSolid(peerOuter, ToColor(MpUiPalette.Panel));
        if (vm.Peers.Count == 0)
        {
            GUI.color = ToColor(MpUiPalette.Dim);
            Widgets.Label(peerOuter.ContractedBy(10f), "No peers yet.");
            GUI.color = Color.white;
        }
        else
        {
            var rowH = 28f;
            var viewH = vm.Peers.Count * rowH + 8f;
            var view = new Rect(0f, 0f, peerOuter.width - 16f, viewH);
            Widgets.BeginScrollView(peerOuter, ref _peerScroll, view);
            var rowY = 4f;
            foreach (var peer in vm.Peers)
            {
                DrawPeerRow(new Rect(8f, rowY, view.width - 8f, rowH - 2f), peer, vm.CanKick);
                rowY += rowH;
            }

            Widgets.EndScrollView();
        }

        y += peerAreaH + 10f;

        // Chat panel (in-session)
        if (vm.CanChat || vm.ChatLines.Count > 0)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = ToColor(MpUiPalette.Steel);
            Widgets.Label(new Rect(x, y, w, 16f), "CHAT");
            GUI.color = Color.white;
            y += 16f;
            var chatH = 72f;
            var chatOuter = new Rect(x, y, w, chatH);
            Widgets.DrawBoxSolid(chatOuter, ToColor(MpUiPalette.Panel));
            var chatViewH = Mathf.Max(chatH, vm.ChatLines.Count * 16f + 8f);
            Widgets.BeginScrollView(chatOuter, ref _chatScroll, new Rect(0f, 0f, chatOuter.width - 16f, chatViewH));
            var cy = 4f;
            foreach (var line in vm.ChatLines)
            {
                GUI.color = line.IsSystem ? ToColor(MpUiPalette.Warn) : ToColor(MpUiPalette.Soft);
                Widgets.Label(new Rect(6f, cy, chatOuter.width - 24f, 16f), $"{line.Prefix}: {line.Text}");
                cy += 16f;
            }

            Widgets.EndScrollView();
            GUI.color = Color.white;
            y += chatH + 6f;

            if (vm.CanChat)
            {
                _chatDraft = Widgets.TextField(new Rect(x, y, w * 0.72f, 28f), _chatDraft);
                if (DrawSteelButton(new Rect(x + w * 0.74f, y, w * 0.26f, 28f), "Send") && !string.IsNullOrWhiteSpace(_chatDraft))
                {
                    SessionFacade.Current?.SendChat(_chatDraft);
                    _chatDraft = string.Empty;
                    _actionFlash = "Chat sent";
                }

                y += 34f;
            }
        }

        var btnH = 32f;
        var gap = 8f;
        if (vm.CanHost || vm.CanJoin)
        {
            var half = (w - gap) * 0.5f;
            if (DrawEmberButton(new Rect(x, y, half, btnH), "Host"))
                DoHost();
            if (DrawSteelButton(new Rect(x + half + gap, y, half, btnH), "Join"))
                DoJoin();
            y += btnH + gap;
        }

        if (vm.CanToggleReady)
        {
            var readyLabel = vm.LocalReady ? "Unready" : "Ready up";
            if (Widgets.ButtonText(new Rect(x, y, w * 0.48f, btnH), readyLabel))
            {
                SessionFacade.Current?.SetReady(!vm.LocalReady);
                _actionFlash = vm.LocalReady ? "Marked not ready" : "Marked ready";
            }

            if (vm.CanDisconnect && DrawDangerButton(new Rect(x + w * 0.52f, y, w * 0.48f, btnH), "Disconnect"))
                DoDisconnect();
            y += btnH + gap;
        }
        else if (vm.CanDisconnect)
        {
            if (DrawDangerButton(new Rect(x, y, w, btnH), "Disconnect"))
                DoDisconnect();
            y += btnH + gap;
        }

        if (!vm.InSession)
        {
            if (DrawSteelButton(new Rect(x, y, w, btnH), "Check launcher handoff"))
            {
                _ = MpLobbyActions.TryConsumeLauncherHandoffAsync();
                _actionFlash = "Checked launcher-handoff.json";
            }

            y += btnH + gap;
        }

        Text.Font = GameFont.Tiny;
        GUI.color = vm.WasKicked || vm.IsFrozen ? ToColor(MpUiPalette.Danger) : ToColor(MpUiPalette.Soft);
        var footer = string.IsNullOrEmpty(_actionFlash) ? vm.StatusLine : _actionFlash;
        Widgets.Label(new Rect(x, Mathf.Min(y, inRect.yMax - 36f), w, 34f), footer);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private float DrawHandoffBlock(float x, float y, float w, HandoffUiSnapshot handoff)
    {
        var tint = handoff.IsError
            ? MpUiPalette.Danger
            : handoff.IsOk
                ? MpUiPalette.Ok
                : handoff.Kind == HandoffUiKind.Missing
                    ? MpUiPalette.Dim
                    : MpUiPalette.Steel;
        GUI.color = ToColor(tint);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(x, y, w, 16f), "LAUNCHER HANDOFF");
        GUI.color = Color.white;
        y += 16f;
        Text.Font = GameFont.Tiny;
        GUI.color = ToColor(MpUiPalette.Soft);
        Widgets.Label(new Rect(x, y, w, 32f), handoff.Detail);
        GUI.color = Color.white;
        return y + 34f;
    }

    private void DrawPeerRow(Rect rect, LobbyPeerRow peer, bool hostCanKick)
    {
        Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.03f));
        Text.Font = GameFont.Tiny;
        GUI.color = ToColor(MpUiPalette.Soft);
        var nameW = hostCanKick && peer.CanKick ? rect.width * 0.32f : rect.width * 0.38f;
        Widgets.Label(new Rect(rect.x + 4f, rect.y + 4f, nameW, rect.height), peer.DisplayName);

        GUI.color = peer.Ready ? ToColor(MpUiPalette.Ok) : ToColor(MpUiPalette.Warn);
        Widgets.Label(new Rect(rect.x + rect.width * 0.36f, rect.y + 4f, rect.width * 0.20f, rect.height), peer.ReadyLabel);

        GUI.color = peer.ModlistMatch ? ToColor(MpUiPalette.Steel) : ToColor(MpUiPalette.Danger);
        Widgets.Label(new Rect(rect.x + rect.width * 0.56f, rect.y + 4f, rect.width * 0.24f, rect.height), peer.ModlistLabel);

        if (hostCanKick && peer.CanKick)
        {
            var kickRect = new Rect(rect.xMax - 52f, rect.y + 2f, 48f, rect.height - 4f);
            if (DrawDangerButton(kickRect, "Kick"))
            {
                SessionFacade.Current?.HostKickPeer(peer.PlayerId);
                _actionFlash = $"Kicked {peer.DisplayName}";
            }
        }

        GUI.color = Color.white;
    }

    private void DoHost()
    {
        if (!int.TryParse(_port, out var port))
            port = MpConfig.DefaultPort;
        MpConfig.ModlistHash = _modlistHash ?? string.Empty;
        MpConfig.PlayerDisplayName = _displayName ?? string.Empty;
        MpConfig.ReadyRequired = true;
        _ = MpLobbyActions.HostAsync(port: port, modlistHash: _modlistHash);
        _actionFlash = $"Hosting on {port}…";
        Messages.Message($"RimworldMP: hosting on port {port}", MessageTypeDefOf.TaskCompletion, false);
    }

    private void DoJoin()
    {
        if (!int.TryParse(_port, out var port))
            port = MpConfig.DefaultPort;
        MpConfig.ModlistHash = _modlistHash ?? string.Empty;
        MpConfig.PlayerDisplayName = _displayName ?? string.Empty;
        _ = MpLobbyActions.JoinAsync(_host, port: port, modlistHash: _modlistHash);
        _actionFlash = $"Joining {_host}:{port}…";
        Messages.Message($"RimworldMP: joining {_host}:{port}", MessageTypeDefOf.TaskCompletion, false);
    }

    private void DoDisconnect()
    {
        _ = MpLobbyActions.StopAsync();
        _actionFlash = "Disconnected";
        Messages.Message("RimworldMP: session stopped", MessageTypeDefOf.TaskCompletion, false);
    }

    private static bool DrawEmberButton(Rect rect, string label)
    {
        var prev = GUI.color;
        GUI.color = ToColor(MpUiPalette.Ember);
        var hit = Widgets.ButtonText(rect, label);
        GUI.color = prev;
        return hit;
    }

    private static bool DrawSteelButton(Rect rect, string label)
    {
        var prev = GUI.color;
        GUI.color = ToColor(MpUiPalette.Steel);
        var hit = Widgets.ButtonText(rect, label);
        GUI.color = prev;
        return hit;
    }

    private static bool DrawDangerButton(Rect rect, string label)
    {
        var prev = GUI.color;
        GUI.color = ToColor(MpUiPalette.Danger);
        var hit = Widgets.ButtonText(rect, label);
        GUI.color = prev;
        return hit;
    }

    private static void DrawHairline(float x, float y, float w, Color c)
    {
        Widgets.DrawBoxSolid(new Rect(x, y, w, 1f), c);
    }

    private static Color ToColor(MpUiPalette.Rgba c) => new(c.R, c.G, c.B, c.A);

    public static void Open()
    {
        var stack = Find.WindowStack;
        if (stack is null)
            return;
        var existing = stack.WindowOfType<Dialog_RimworldMPLobby>();
        if (existing is not null)
        {
            existing.Close(doCloseSound: false);
            return;
        }

        stack.Add(new Dialog_RimworldMPLobby());
    }
}
#endif

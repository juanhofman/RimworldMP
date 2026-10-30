#if !RIMWORLDMP_NO_GAME_REFS
using RimworldMP.Core.Session;
using RimworldMP.Lobby;
using RimworldMP.Net.Protocol;
using UnityEngine;
using Verse;

namespace RimworldMP.UI;

/// <summary>
/// Release lobby window — brand hero, peers (kick/ready), chat, begin/abort, host/join, handoff.
/// Layout uses <see cref="MpImmediateGui"/> within Verse <see cref="Window"/> limits; re-check under RIMWORLD_DIR.
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

    public override Vector2 InitialSize =>
        new(LobbyLayoutMetrics.WindowWidth, LobbyLayoutMetrics.WindowHeight);

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

        MpImmediateGui.DrawInkBackdrop(inRect);
        MpImmediateGui.DrawEmberAccentBar(inRect);

        var x = inRect.x + LobbyLayoutMetrics.ContentPadX + LobbyLayoutMetrics.AccentBarWidth;
        var y = inRect.y + LobbyLayoutMetrics.ContentPadTop;
        var w = inRect.width - LobbyLayoutMetrics.ContentPadX * 2f - LobbyLayoutMetrics.AccentBarWidth;

        y = MpImmediateGui.DrawBrandHero(x, y, w, vm.Brand, vm.Subtitle);

        var roleTint = vm.WasKicked || vm.IsFrozen ? MpUiPalette.Danger : MpUiPalette.Soft;
        y = MpImmediateGui.DrawSoftLine(
            x, y, w,
            MpImmediateGui.Tf("RMP.LobbySession", vm.SessionRoleLabel),
            GameFont.Small,
            roleTint);
        y = MpImmediateGui.DrawMetaLine(x, y, w, vm.ProtocolLabel);
        y = MpImmediateGui.DrawMetaLine(x, y, w, vm.LanStatus);
        y = MpImmediateGui.DrawMetaLine(x, y, w, MpImmediateGui.Tf("RMP.MetaEndpoint", vm.EndpointLabel));
        y = MpImmediateGui.DrawMetaLine(x, y, w, MpImmediateGui.Tf("RMP.MetaModlist", vm.LocalModlistShort));

        if (vm.InSession)
            y = MpImmediateGui.DrawMetaLine(
                x, y, w, vm.ReadySummary,
                vm.AllModsMatch && vm.LobbyReady ? MpUiPalette.Ok : MpUiPalette.Warn);

        y += 4f;

        if (vm.InSession && vm.IsHost && !vm.IsFrozen)
            y = MpImmediateGui.DrawLivePulse(x, y, w, _pulseT);

        y = DrawHandoffBlock(x, y, w, vm.Handoff);
        y += 6f;

        if (!vm.InSession)
        {
            y = DrawIdleFields(x, y, w);
        }
        else if (!string.IsNullOrEmpty(vm.LocalDisplayName))
        {
            y = MpImmediateGui.DrawMetaLine(
                x, y, w,
                MpImmediateGui.Tf("RMP.PlayingAs", vm.LocalDisplayName),
                MpUiPalette.Steel);
            y += 4f;
        }

        y = DrawPeers(x, y, w, inRect.yMax, vm);
        y = DrawChat(x, y, w, vm);
        y = DrawActions(x, y, w, vm);

        Text.Font = GameFont.Tiny;
        GUI.color = vm.WasKicked || vm.IsFrozen
            ? MpImmediateGui.ToColor(MpUiPalette.Danger)
            : MpImmediateGui.ToColor(MpUiPalette.Soft);
        var footer = string.IsNullOrEmpty(_actionFlash) ? vm.StatusLine : _actionFlash;
        Widgets.Label(
            new Rect(x, Mathf.Min(y, inRect.yMax - LobbyLayoutMetrics.FooterHeight), w, LobbyLayoutMetrics.FooterHeight),
            footer);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private float DrawIdleFields(float x, float y, float w)
    {
        y = MpImmediateGui.DrawSectionLabel(x, y, w, MpImmediateGui.T("RMP.DisplayNameLabel"), MpUiPalette.Dim);
        _displayName = Widgets.TextField(new Rect(x, y, w, LobbyLayoutMetrics.FieldHeight), _displayName);
        y += LobbyLayoutMetrics.FieldGap;

        y = MpImmediateGui.DrawSectionLabel(x, y, w, MpImmediateGui.T("RMP.HostIpLabel"), MpUiPalette.Dim);
        _host = Widgets.TextField(new Rect(x, y, w, LobbyLayoutMetrics.FieldHeight), _host);
        y += LobbyLayoutMetrics.FieldGap;

        y = MpImmediateGui.DrawSectionLabel(x, y, w * 0.45f, MpImmediateGui.T("RMP.PortLabel"), MpUiPalette.Dim);
        MpImmediateGui.DrawSectionLabel(x + w * 0.5f, y - LobbyLayoutMetrics.FieldLabelHeight, w * 0.5f,
            MpImmediateGui.T("RMP.ModlistHashLabel"), MpUiPalette.Dim);
        _port = Widgets.TextField(new Rect(x, y, w * 0.42f, LobbyLayoutMetrics.FieldHeight), _port);
        _modlistHash = Widgets.TextField(
            new Rect(x + w * 0.48f, y, w * 0.52f, LobbyLayoutMetrics.FieldHeight), _modlistHash);
        return y + LobbyLayoutMetrics.FieldGap;
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
        y = MpImmediateGui.DrawSectionLabel(x, y, w, MpImmediateGui.T("RMP.HandoffHeader"), tint);
        Text.Font = GameFont.Tiny;
        GUI.color = MpImmediateGui.ToColor(MpUiPalette.Soft);
        Widgets.Label(new Rect(x, y, w, 32f), handoff.Detail);
        GUI.color = Color.white;
        return y + 34f;
    }

    private float DrawPeers(float x, float y, float w, float yMax, LobbyViewModel vm)
    {
        var header = vm.InSession
            ? MpImmediateGui.Tf("RMP.PeersHeader", vm.Peers.Count)
            : MpImmediateGui.T("RMP.PeersEmptyHint");
        y = MpImmediateGui.DrawSoftLine(x, y, w, header, GameFont.Small, MpUiPalette.Steel);

        // Column captions — readable, not DebugOutput.
        y = MpImmediateGui.DrawSectionLabel(x + 4f, y, w * 0.36f, MpImmediateGui.T("RMP.ColName"), MpUiPalette.Dim);
        MpImmediateGui.DrawSectionLabel(x + w * 0.36f, y - LobbyLayoutMetrics.FieldLabelHeight, w * 0.20f,
            MpImmediateGui.T("RMP.ColReady"), MpUiPalette.Dim);
        MpImmediateGui.DrawSectionLabel(x + w * 0.56f, y - LobbyLayoutMetrics.FieldLabelHeight, w * 0.24f,
            MpImmediateGui.T("RMP.ColMods"), MpUiPalette.Dim);

        var reserve = 200f;
        var peerAreaH = Mathf.Min(
            LobbyLayoutMetrics.PeerAreaMax,
            Mathf.Max(LobbyLayoutMetrics.PeerAreaMin, yMax - y - reserve));
        var peerOuter = new Rect(x, y, w, peerAreaH);
        MpImmediateGui.DrawPanel(peerOuter);

        if (vm.Peers.Count == 0)
        {
            GUI.color = MpImmediateGui.ToColor(MpUiPalette.Dim);
            Text.Font = GameFont.Tiny;
            Widgets.Label(peerOuter.ContractedBy(10f), MpImmediateGui.T("RMP.PeersNone"));
            GUI.color = Color.white;
        }
        else
        {
            var rowH = LobbyLayoutMetrics.PeerRowHeight;
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

        return y + peerAreaH + LobbyLayoutMetrics.SectionGap;
    }

    private void DrawPeerRow(Rect rect, LobbyPeerRow peer, bool hostCanKick)
    {
        Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.03f));
        Text.Font = GameFont.Tiny;
        GUI.color = MpImmediateGui.ToColor(MpUiPalette.Soft);
        var nameW = hostCanKick && peer.CanKick ? rect.width * 0.32f : rect.width * 0.38f;
        Widgets.Label(new Rect(rect.x + 4f, rect.y + 4f, nameW, rect.height), peer.DisplayName);

        GUI.color = MpImmediateGui.ToColor(peer.Ready ? MpUiPalette.Ok : MpUiPalette.Warn);
        Widgets.Label(new Rect(rect.x + rect.width * 0.36f, rect.y + 4f, rect.width * 0.20f, rect.height), peer.ReadyLabel);

        GUI.color = MpImmediateGui.ToColor(peer.ModlistMatch ? MpUiPalette.Steel : MpUiPalette.Danger);
        Widgets.Label(new Rect(rect.x + rect.width * 0.56f, rect.y + 4f, rect.width * 0.24f, rect.height), peer.ModlistLabel);

        if (hostCanKick && peer.CanKick)
        {
            var kickRect = new Rect(
                rect.xMax - LobbyLayoutMetrics.KickButtonWidth - 4f,
                rect.y + 2f,
                LobbyLayoutMetrics.KickButtonWidth,
                rect.height - 4f);
            if (MpImmediateGui.DangerButton(kickRect, MpImmediateGui.T("RMP.BtnKick")))
            {
                SessionFacade.Current?.HostKickPeer(peer.PlayerId);
                _actionFlash = MpImmediateGui.Tf("RMP.FlashKicked", peer.DisplayName);
            }
        }

        GUI.color = Color.white;
    }

    private float DrawChat(float x, float y, float w, LobbyViewModel vm)
    {
        if (!vm.CanChat && vm.ChatLines.Count == 0)
            return y;

        y = MpImmediateGui.DrawSectionLabel(x, y, w, MpImmediateGui.T("RMP.ChatHeader"));
        var chatH = LobbyLayoutMetrics.ChatAreaHeight;
        var chatOuter = new Rect(x, y, w, chatH);
        MpImmediateGui.DrawPanel(chatOuter);
        var chatViewH = Mathf.Max(chatH, vm.ChatLines.Count * LobbyLayoutMetrics.ChatLineHeight + 8f);
        Widgets.BeginScrollView(chatOuter, ref _chatScroll, new Rect(0f, 0f, chatOuter.width - 16f, chatViewH));
        var cy = 4f;
        foreach (var line in vm.ChatLines)
        {
            GUI.color = MpImmediateGui.ToColor(line.IsSystem ? MpUiPalette.Warn : MpUiPalette.Soft);
            Widgets.Label(
                new Rect(6f, cy, chatOuter.width - 24f, LobbyLayoutMetrics.ChatLineHeight),
                $"{line.Prefix}: {line.Text}");
            cy += LobbyLayoutMetrics.ChatLineHeight;
        }

        Widgets.EndScrollView();
        GUI.color = Color.white;
        y += chatH + 6f;

        if (vm.CanChat)
        {
            _chatDraft = Widgets.TextField(new Rect(x, y, w * 0.72f, LobbyLayoutMetrics.FieldHeight), _chatDraft);
            if (MpImmediateGui.SteelButton(
                    new Rect(x + w * 0.74f, y, w * 0.26f, LobbyLayoutMetrics.FieldHeight),
                    MpImmediateGui.T("RMP.ChatSend"))
                && !string.IsNullOrWhiteSpace(_chatDraft))
            {
                SessionFacade.Current?.SendChat(_chatDraft);
                _chatDraft = string.Empty;
                _actionFlash = MpImmediateGui.T("RMP.ChatSent");
            }

            y += LobbyLayoutMetrics.FieldGap;
        }

        return y;
    }

    private float DrawActions(float x, float y, float w, LobbyViewModel vm)
    {
        var btnH = LobbyLayoutMetrics.ButtonHeight;
        var gap = LobbyLayoutMetrics.ButtonGap;

        if (vm.CanHost || vm.CanJoin)
        {
            var half = (w - gap) * 0.5f;
            if (MpImmediateGui.EmberButton(new Rect(x, y, half, btnH), MpImmediateGui.T("RMP.BtnHost")))
                DoHost();
            if (MpImmediateGui.SteelButton(new Rect(x + half + gap, y, half, btnH), MpImmediateGui.T("RMP.BtnJoin")))
                DoJoin();
            y += btnH + gap;
        }

        if (vm.CanBeginPlay)
        {
            if (MpImmediateGui.OkButton(new Rect(x, y, w, btnH), MpImmediateGui.T("RMP.BtnBeginPlay")))
                DoBeginPlay();
            y += btnH + gap;
        }

        if (vm.CanToggleReady)
        {
            var readyLabel = vm.LocalReady
                ? MpImmediateGui.T("RMP.BtnUnready")
                : MpImmediateGui.T("RMP.BtnReady");
            if (Widgets.ButtonText(new Rect(x, y, w * 0.48f, btnH), readyLabel))
            {
                SessionFacade.Current?.SetReady(!vm.LocalReady);
                _actionFlash = vm.LocalReady
                    ? MpImmediateGui.T("RMP.FlashUnready")
                    : MpImmediateGui.T("RMP.FlashReady");
            }

            if (vm.CanAbort && MpImmediateGui.DangerButton(
                    new Rect(x + w * 0.52f, y, w * 0.48f, btnH),
                    MpImmediateGui.T("RMP.BtnAbort")))
                DoAbort();
            y += btnH + gap;
        }
        else if (vm.CanAbort)
        {
            if (MpImmediateGui.DangerButton(new Rect(x, y, w, btnH), MpImmediateGui.T("RMP.BtnAbort")))
                DoAbort();
            y += btnH + gap;
        }

        if (!vm.InSession)
        {
            if (MpImmediateGui.SteelButton(new Rect(x, y, w, btnH), MpImmediateGui.T("RMP.BtnHandoff")))
            {
                _ = MpLobbyActions.TryConsumeLauncherHandoffAsync();
                _actionFlash = MpImmediateGui.T("RMP.FlashHandoffChecked");
            }

            y += btnH + gap;
        }

        return y;
    }

    private void DoHost()
    {
        if (!int.TryParse(_port, out var port))
            port = MpConfig.DefaultPort;
        MpConfig.ModlistHash = _modlistHash ?? string.Empty;
        MpConfig.PlayerDisplayName = _displayName ?? string.Empty;
        MpConfig.ReadyRequired = true;
        _ = MpLobbyActions.HostAsync(port: port, modlistHash: _modlistHash);
        _actionFlash = MpImmediateGui.Tf("RMP.FlashHosting", port);
        Messages.Message(MpImmediateGui.Tf("RMP.MsgHosting", port), MessageTypeDefOf.TaskCompletion, false);
    }

    private void DoJoin()
    {
        if (!int.TryParse(_port, out var port))
            port = MpConfig.DefaultPort;
        MpConfig.ModlistHash = _modlistHash ?? string.Empty;
        MpConfig.PlayerDisplayName = _displayName ?? string.Empty;
        _ = MpLobbyActions.JoinAsync(_host, port: port, modlistHash: _modlistHash);
        _actionFlash = MpImmediateGui.Tf("RMP.FlashJoining", _host, port);
        Messages.Message(MpImmediateGui.Tf("RMP.MsgJoining", _host, port), MessageTypeDefOf.TaskCompletion, false);
    }

    private void DoAbort()
    {
        _ = MpLobbyActions.StopAsync();
        _actionFlash = MpImmediateGui.T("RMP.FlashAborted");
        Messages.Message(MpImmediateGui.T("RMP.MsgStopped"), MessageTypeDefOf.TaskCompletion, false);
    }

    private void DoBeginPlay()
    {
        var session = SessionFacade.Current;
        if (session is null || !session.CanStartOrUnpause)
            return;

        session.BroadcastSystemNotice(
            SystemNoticeKind.Info,
            SessionMessages.Get("RMP.BeginPlayNotice"));
        _actionFlash = MpImmediateGui.T("RMP.FlashBeginPlay");
        Messages.Message(MpImmediateGui.T("RMP.FlashBeginPlay"), MessageTypeDefOf.TaskCompletion, false);
        Close(doCloseSound: true);
    }

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

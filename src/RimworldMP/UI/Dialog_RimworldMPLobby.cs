#if !RIMWORLDMP_NO_GAME_REFS
using System.Globalization;
using RimworldMP.Core.Session;
using RimworldMP.Lobby;
using RimworldMP.Net.Protocol;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimworldMP.UI;

/// <summary>
/// Release lobby window — brand hero, peers (kick/ready), chat, begin/abort, host/join, handoff.
/// Actions are bottom-anchored so Unready / Abort are never clipped; peers + chat share a mid band.
/// </summary>
public sealed class Dialog_RimworldMPLobby : Window
{
    private string _host = "127.0.0.1";
    private string _port = NetProtocol.DefaultPort.ToString(CultureInfo.InvariantCulture);
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

    protected override float Margin => LobbyLayoutMetrics.WindowMargin;

    public override void PreOpen()
    {
        base.PreOpen();
        _port = MpConfig.DefaultPort.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(_modlistHash))
            _modlistHash = MpConfig.ModlistHash;
        if (string.IsNullOrEmpty(_displayName))
            _displayName = MpConfig.PlayerDisplayName;
        var handoff = HandoffUiTracker.Current;
        if (!string.IsNullOrEmpty(handoff.Host) && handoff.Kind == HandoffUiKind.AutoJoined)
            _host = handoff.Host!;
        if (handoff.Port is int p && p > 0)
            _port = p.ToString(CultureInfo.InvariantCulture);
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
        var w = inRect.width - LobbyLayoutMetrics.ContentPadX * 2f - LobbyLayoutMetrics.AccentBarWidth;
        var y = inRect.y + LobbyLayoutMetrics.ContentPadTop;

        var actionsH = EstimateActionsHeight(vm);
        var footerY = inRect.yMax - LobbyLayoutMetrics.ContentPadBottom - LobbyLayoutMetrics.FooterHeight;
        var actionsY = footerY - actionsH;
        var contentBottom = actionsY - LobbyLayoutMetrics.SectionGap;

        y = MpImmediateGui.DrawBrandHero(x, y, w, vm.Brand, vm.Subtitle);

        var roleTint = vm.WasKicked || vm.IsFrozen ? MpUiPalette.Danger : MpUiPalette.Soft;
        var primary = MpImmediateGui.Tf("RMP.LobbySession", vm.SessionRoleLabel);
        if (vm.InSession)
            primary = $"{primary}  ·  {vm.ReadySummary}";
        var secondary = $"{vm.ProtocolLabel}  ·  {vm.LanStatus}  ·  {vm.EndpointLabel}";
        y = MpImmediateGui.DrawStatusStrip(x, y, w, primary, secondary, roleTint);

        if (vm.InSession && vm.IsHost && !vm.IsFrozen)
            y = MpImmediateGui.DrawLivePulse(x, y, w, _pulseT);

        y = DrawHandoffBlock(x, y, w, vm.Handoff);

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
        }

        DrawMidBand(x, y, w, contentBottom - y, vm);
        DrawActions(x, actionsY, w, vm);

        Text.Font = GameFont.Tiny;
        GUI.color = vm.WasKicked || vm.IsFrozen
            ? MpImmediateGui.ToColor(MpUiPalette.Danger)
            : MpImmediateGui.ToColor(MpUiPalette.Soft);
        var footer = string.IsNullOrEmpty(_actionFlash) ? vm.StatusLine : _actionFlash;
        Widgets.Label(new Rect(x, footerY, w, LobbyLayoutMetrics.FooterHeight), footer);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private static float EstimateActionsHeight(LobbyViewModel vm)
    {
        var btnH = LobbyLayoutMetrics.ButtonHeight;
        var gap = LobbyLayoutMetrics.ButtonGap;
        var h = 0f;
        if (vm.CanHost || vm.CanJoin)
            h += btnH + gap;
        if (vm.CanBeginPlay)
            h += btnH + gap;
        if (vm.CanToggleReady || vm.CanAbort)
            h += btnH + gap;
        if (!vm.InSession)
            h += btnH + gap;
        return Mathf.Max(h, btnH);
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
        Widgets.Label(new Rect(x, y, w, 20f), handoff.Detail);
        GUI.color = Color.white;
        return y + 22f;
    }

    private void DrawMidBand(float x, float y, float w, float height, LobbyViewModel vm)
    {
        if (height < 40f)
            return;

        var showChat = vm.CanChat || vm.ChatLines.Count > 0;
        if (vm.InSession && showChat)
        {
            var gap = 10f;
            var leftW = w * 0.48f;
            var rightW = w - leftW - gap;
            DrawPeersColumn(x, y, leftW, height, vm);
            DrawChatColumn(x + leftW + gap, y, rightW, height, vm);
        }
        else
        {
            var peerH = Mathf.Min(LobbyLayoutMetrics.PeerAreaMax, Mathf.Max(LobbyLayoutMetrics.PeerAreaMin, height * 0.45f));
            var afterPeers = DrawPeersColumn(x, y, w, peerH, vm);
            if (showChat)
                DrawChatColumn(x, afterPeers, w, Mathf.Max(40f, height - (afterPeers - y)), vm);
        }
    }

    private float DrawPeersColumn(float x, float y, float w, float height, LobbyViewModel vm)
    {
        var header = vm.InSession
            ? MpImmediateGui.Tf("RMP.PeersHeader", vm.Peers.Count)
            : MpImmediateGui.T("RMP.PeersEmptyHint");
        y = MpImmediateGui.DrawSoftLine(x, y, w, header, GameFont.Tiny, MpUiPalette.Steel);

        MpImmediateGui.DrawSectionLabel(x + 4f, y, w * 0.40f, MpImmediateGui.T("RMP.ColName"), MpUiPalette.Dim);
        MpImmediateGui.DrawSectionLabel(x + w * 0.40f, y, w * 0.24f, MpImmediateGui.T("RMP.ColReady"), MpUiPalette.Dim);
        MpImmediateGui.DrawSectionLabel(x + w * 0.64f, y, w * 0.24f, MpImmediateGui.T("RMP.ColMods"), MpUiPalette.Dim);
        y += LobbyLayoutMetrics.FieldLabelHeight;

        var headerUsed = LobbyLayoutMetrics.MetaLineHeight + LobbyLayoutMetrics.FieldLabelHeight + 4f;
        var peerOuter = new Rect(x, y, w, Mathf.Max(32f, height - headerUsed));
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

        return peerOuter.yMax + LobbyLayoutMetrics.SectionGap;
    }

    private void DrawPeerRow(Rect rect, LobbyPeerRow peer, bool hostCanKick)
    {
        Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.04f));
        Text.Font = GameFont.Tiny;
        GUI.color = MpImmediateGui.ToColor(MpUiPalette.Soft);
        var nameW = hostCanKick && peer.CanKick ? rect.width * 0.34f : rect.width * 0.40f;
        Widgets.Label(new Rect(rect.x + 4f, rect.y + 4f, nameW, rect.height), peer.DisplayName);

        GUI.color = MpImmediateGui.ToColor(peer.Ready ? MpUiPalette.Ok : MpUiPalette.Warn);
        Widgets.Label(new Rect(rect.x + rect.width * 0.40f, rect.y + 4f, rect.width * 0.24f, rect.height), peer.ReadyLabel);

        GUI.color = MpImmediateGui.ToColor(peer.ModlistMatch ? MpUiPalette.Steel : MpUiPalette.Danger);
        Widgets.Label(new Rect(rect.x + rect.width * 0.64f, rect.y + 4f, rect.width * 0.22f, rect.height), peer.ModlistLabel);

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

    private float DrawChatColumn(float x, float y, float w, float height, LobbyViewModel vm)
    {
        y = MpImmediateGui.DrawSectionLabel(x, y, w, MpImmediateGui.T("RMP.ChatHeader"));
        var inputH = vm.CanChat ? LobbyLayoutMetrics.FieldHeight + 4f : 0f;
        var chatH = Mathf.Max(LobbyLayoutMetrics.ChatAreaMin, height - LobbyLayoutMetrics.FieldLabelHeight - inputH - 4f);
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
        y += chatH + 4f;

        if (vm.CanChat)
        {
            _chatDraft = Widgets.TextField(new Rect(x, y, w * 0.70f, LobbyLayoutMetrics.FieldHeight), _chatDraft);
            if (MpImmediateGui.SteelButton(
                    new Rect(x + w * 0.72f, y, w * 0.28f, LobbyLayoutMetrics.FieldHeight),
                    MpImmediateGui.T("RMP.ChatSend"))
                && !string.IsNullOrWhiteSpace(_chatDraft))
            {
                SessionFacade.Current?.SendChat(_chatDraft);
                _chatDraft = string.Empty;
                _actionFlash = MpImmediateGui.T("RMP.ChatSent");
            }

            y += LobbyLayoutMetrics.FieldHeight + 2f;
        }

        return y;
    }

    private void DrawActions(float x, float y, float w, LobbyViewModel vm)
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
            if (MpImmediateGui.SteelButton(new Rect(x, y, w * 0.48f, btnH), readyLabel))
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
        }
        else if (vm.CanAbort)
        {
            if (MpImmediateGui.DangerButton(new Rect(x, y, w, btnH), MpImmediateGui.T("RMP.BtnAbort")))
                DoAbort();
        }

        if (!vm.InSession)
        {
            if (MpImmediateGui.SteelButton(new Rect(x, y, w, btnH), MpImmediateGui.T("RMP.BtnHandoff")))
            {
                _ = MpLobbyActions.TryConsumeLauncherHandoffAsync();
                _actionFlash = MpImmediateGui.T("RMP.FlashHandoffChecked");
            }
        }
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

using RimworldMP.Core.Session;
using RimworldMP.Core.Sync;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.UI;

/// <summary>
/// Builds a <see cref="LobbyViewModel"/> from session + handoff tracker.
/// Safe under <c>RIMWORLDMP_NO_GAME_REFS</c> — no Verse types.
/// </summary>
public static class LobbyPresenter
{
    public static LobbyViewModel Capture(
        MpSession? session = null,
        HandoffUiSnapshot? handoff = null,
        int? configuredPort = null,
        string? localModlistOverride = null)
    {
        session ??= SessionFacade.Current;
        var handoffSnap = handoff ?? HandoffUiTracker.Current;
        var port = configuredPort ?? MpConfig.DefaultPort;
        var localHash = localModlistOverride
            ?? session?.ModlistHash
            ?? MpConfig.ModlistHash
            ?? string.Empty;

        var inSession = session is { IsActive: true };
        var isHost = session?.IsHost == true;
        var frozen = session?.IsFrozen == true;
        var jip = session?.IsJoinInProgress == true;
        var readyRequired = session?.Ready.Required == true || MpConfig.ReadyRequired;
        var localId = session?.LocalPlayerId ?? -1;
        var tick = session?.Tick ?? -1;
        var maxClients = MpConfig.MaxClients;
        var kicked = session?.WasKicked == true;
        var expectedPeers = session?.ExpectedPeers ?? Math.Max(1, MpConfig.MaxClients);
        DesyncInfo? desyncInfo = session?.Desync.LastDesync;
        var desync = DesyncOverlayModel.From(desyncInfo);

        string endpointLabel;
        string lanStatus;
        int connected = 0;
        if (session?.Transport is ISessionLanTransport lan)
        {
            connected = lan.ConnectedPeers.Count;
            if (lan.Endpoint is { } ep)
            {
                endpointLabel = isHost
                    ? SessionMessages.Format("RMP.EndpointHost", ep.Port)
                    : ep.ToString();
                port = ep.Port;
            }
            else
            {
                endpointLabel = isHost
                    ? SessionMessages.Format("RMP.EndpointHost", port)
                    : SessionMessages.Format("RMP.EndpointUnset", port);
            }

            var transportTag = lan is LiteNetLibTransport ? "LiteNetLib" : "TCP";
            lanStatus = !lan.IsRunning
                ? SessionMessages.Get("RMP.LanIdle")
                : isHost
                    ? SessionMessages.Format("RMP.LanHosting", transportTag, connected, lan.MaxClients)
                    : lan.LastJoinRejectReason is { } reject
                        ? FormatJoinReject(reject, lan.LastJoinRejectMessage)
                        : SessionMessages.Format("RMP.LanConnected", localId, transportTag);
            maxClients = lan.MaxClients > 0 ? lan.MaxClients : maxClients;
        }
        else if (inSession)
        {
            endpointLabel = isHost
                ? SessionMessages.Format("RMP.EndpointLoopHost", port)
                : SessionMessages.Format("RMP.EndpointLoopClient", port);
            lanStatus = SessionMessages.Get("RMP.LanNonLan");
            connected = session!.Ready.Snapshot().Count;
        }
        else
        {
            endpointLabel = SessionMessages.Format("RMP.EndpointDefault", port);
            lanStatus = SessionMessages.Get("RMP.LanNotConnected");
        }

        var peers = BuildPeers(session, localHash, localId, isHost);
        if (connected == 0 && peers.Count > 0)
            connected = peers.Count;

        var lobbyReady = session?.IsLobbyReady ?? !readyRequired;
        var localReady = false;
        if (session is not null && session.Ready.Snapshot().TryGetValue(localId, out var self))
            localReady = self.Ready;

        var readyCount = peers.Count(p => p.Ready);
        var allModsMatch = peers.Count == 0 || peers.All(p => p.ModlistMatch);
        var readySummary = SessionMessages.Format(
            "RMP.ReadySummary",
            readyCount,
            Math.Max(expectedPeers, peers.Count),
            allModsMatch
                ? SessionMessages.Get("RMP.ReadySummaryMatch")
                : SessionMessages.Get("RMP.ReadySummaryMismatch"));

        var localName = session?.LocalDisplayName
            ?? (string.IsNullOrWhiteSpace(MpConfig.PlayerDisplayName)
                ? SessionMessages.Get("RMP.DisplayNameYou")
                : MpConfig.PlayerDisplayName);

        var roleLabel = !inSession
            ? SessionMessages.Get("RMP.RoleIdle")
            : kicked
                ? SessionMessages.Get("RMP.RoleKicked")
                : frozen
                    ? SessionMessages.Get("RMP.RoleFrozen")
                    : jip
                        ? SessionMessages.Get("RMP.RoleJip")
                        : isHost
                            ? SessionMessages.Get("RMP.RoleHosting")
                            : SessionMessages.Get("RMP.RoleJoined");

        var chatLines = BuildChat(session);
        var status = BuildStatusLine(inSession, frozen, jip, lobbyReady, readyRequired, kicked, handoffSnap, desync);
        var canBeginPlay = inSession && isHost && !frozen && !kicked && lobbyReady;

        return new LobbyViewModel
        {
            Brand = MpUiPalette.BrandName,
            Subtitle = SessionMessages.Get("RMP.LobbySubtitle"),
            SessionRoleLabel = roleLabel,
            InSession = inSession,
            IsHost = isHost,
            IsFrozen = frozen,
            IsJoinInProgress = jip,
            LobbyReady = lobbyReady,
            ReadyRequired = readyRequired,
            LocalReady = localReady,
            LocalPlayerId = localId,
            LocalDisplayName = localName,
            Tick = tick,
            Port = port,
            LanStatus = lanStatus,
            EndpointLabel = endpointLabel,
            LocalModlistHash = localHash,
            LocalModlistShort = ShortHash(localHash),
            ProtocolLabel = SessionMessages.Format("RMP.ProtocolLabel", NetProtocol.Version, maxClients),
            MaxClients = maxClients,
            ConnectedPeerCount = connected,
            ReadyCount = readyCount,
            ExpectedPeers = expectedPeers,
            ReadySummary = readySummary,
            AllModsMatch = allModsMatch,
            Peers = peers,
            ChatLines = chatLines,
            Handoff = handoffSnap,
            Desync = desync,
            StatusLine = status,
            CanHost = !inSession,
            CanJoin = !inSession,
            CanDisconnect = inSession,
            CanToggleReady = inSession && !frozen && !kicked,
            CanKick = inSession && isHost && !frozen,
            CanChat = inSession && !kicked,
            CanBeginPlay = canBeginPlay,
            CanAbort = inSession && !kicked,
            WasKicked = kicked,
        };
    }

    private static IReadOnlyList<LobbyPeerRow> BuildPeers(
        MpSession? session,
        string localHash,
        int localId,
        bool isHost)
    {
        if (session is null || !session.IsActive)
            return Array.Empty<LobbyPeerRow>();

        var readySnap = session.Ready.Snapshot();
        var ids = new SortedSet<int>();
        foreach (var id in readySnap.Keys)
            ids.Add(id);
        foreach (var id in session.Roster.Snapshot().Keys)
            ids.Add(id);
        if (localId >= 0)
            ids.Add(localId);
        if (session.Transport is ISessionLanTransport lan)
        {
            foreach (var peer in lan.ConnectedPeers)
                ids.Add(peer.Value);
            if (isHost)
                ids.Add(0);
        }

        if (ids.Count == 0 && localId >= 0)
            ids.Add(localId);

        var rows = new List<LobbyPeerRow>(ids.Count);
        foreach (var id in ids)
        {
            readySnap.TryGetValue(id, out var peerReady);
            var hash = string.IsNullOrEmpty(peerReady.ModlistHash)
                ? (id == localId ? localHash : string.Empty)
                : peerReady.ModlistHash;
            var match = string.IsNullOrEmpty(hash) || ModlistHash.Matches(localHash, hash);
            var ready = peerReady.Ready || (id == localId && isHost && !session.Ready.Required);
            var isLocal = id == localId;
            var hostSeat = id == 0;
            var rosterName = session.Roster.GetName(id, isHostSeat: hostSeat);
            string displayName;
            if (isLocal)
            {
                displayName = hostSeat
                    ? $"{rosterName} · {SessionMessages.Get("RMP.DisplayNameYouHost")}"
                    : $"{rosterName} · {SessionMessages.Get("RMP.DisplayNameYou")}";
            }
            else
            {
                displayName = rosterName;
            }

            rows.Add(new LobbyPeerRow(
                PlayerId: id,
                DisplayName: displayName,
                IsLocal: isLocal,
                IsHostSeat: hostSeat,
                Ready: ready,
                ModlistHash: hash,
                ModlistShort: ShortHash(hash),
                ModlistMatch: match,
                ReadyLabel: ready
                    ? SessionMessages.Get("RMP.PeerReady")
                    : SessionMessages.Get("RMP.PeerNotReady"),
                ModlistLabel: string.IsNullOrEmpty(hash)
                    ? SessionMessages.Get("RMP.ModsUnknown")
                    : match
                        ? SessionMessages.Get("RMP.ModsOk")
                        : SessionMessages.Get("RMP.ModsMismatch"),
                CanKick: isHost && !isLocal && !hostSeat));
        }

        return rows;
    }

    private static IReadOnlyList<LobbyChatLine> BuildChat(MpSession? session)
    {
        if (session is null)
            return Array.Empty<LobbyChatLine>();

        var snap = session.Chat.Snapshot();
        if (snap.Count == 0)
            return Array.Empty<LobbyChatLine>();

        var start = Math.Max(0, snap.Count - LobbyLayoutMetrics.ChatHistoryLines);
        var lines = new List<LobbyChatLine>(snap.Count - start);
        for (var i = start; i < snap.Count; i++)
        {
            var line = snap[i];
            if (line.Kind == SessionChatKind.System)
            {
                lines.Add(new LobbyChatLine(true, SessionMessages.Get("RMP.SystemPrefix"), line.Text, "[sys]"));
            }
            else
            {
                var author = string.IsNullOrEmpty(line.DisplayName)
                    ? SessionMessages.DefaultDisplayName(line.PlayerId, line.PlayerId == 0)
                    : line.DisplayName;
                lines.Add(new LobbyChatLine(false, author, line.Text, author));
            }
        }

        return lines;
    }

    private static string FormatJoinReject(JoinRejectReason reason, string? message)
    {
        var detail = string.IsNullOrWhiteSpace(message) ? reason.ToString() : message!;
        return reason switch
        {
            JoinRejectReason.ProtocolMismatch => SessionMessages.JoinRejected("protocol", detail),
            JoinRejectReason.ModlistMismatch => SessionMessages.JoinRejected("modlist", detail),
            JoinRejectReason.DlcMismatch => SessionMessages.JoinRejected("DLC", detail),
            JoinRejectReason.LobbyFull => SessionMessages.JoinRejected("lobby", detail),
            _ => SessionMessages.JoinRejected(reason.ToString(), detail),
        };
    }

    private static string BuildStatusLine(
        bool inSession,
        bool frozen,
        bool jip,
        bool lobbyReady,
        bool readyRequired,
        bool kicked,
        HandoffUiSnapshot handoff,
        DesyncOverlayModel? desync)
    {
        if (kicked)
            return SessionMessages.Format("RMP.YouWereKicked", SessionMessages.Get("RMP.KickDefaultReason"));
        if (frozen && desync is not null)
            return SessionMessages.Format("RMP.StatusDesync", desync.Tick, desync.LogPath);
        if (jip)
            return SessionMessages.Get("RMP.StatusJip");
        if (inSession && readyRequired && !lobbyReady)
            return SessionMessages.Get("RMP.ReadyWaiting");
        if (inSession && lobbyReady)
            return SessionMessages.Get("RMP.StatusLiveReady");
        if (inSession)
            return SessionMessages.Get("RMP.StatusLive");
        if (handoff.IsError)
            return SessionMessages.Format("RMP.StatusHandoffError", handoff.Detail);
        if (handoff.IsOk)
            return handoff.Detail;
        if (handoff.Kind == HandoffUiKind.Missing)
            return handoff.Detail;
        return SessionMessages.Get("RMP.StatusIdleHint");
    }

    public static string ShortHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
            return SessionMessages.Get("RMP.HashUnset");
        return hash.Length <= 10 ? hash : hash[..10] + "…";
    }
}

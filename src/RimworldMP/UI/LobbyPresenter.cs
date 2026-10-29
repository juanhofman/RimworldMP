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
        DesyncInfo? desyncInfo = session?.Desync.LastDesync;
        var desync = DesyncOverlayModel.From(desyncInfo);

        string endpointLabel;
        string lanStatus;
        int connected = 0;
        if (session?.Transport is LanTcpTransport lan)
        {
            connected = lan.ConnectedPeers.Count;
            if (lan.Endpoint is { } ep)
            {
                endpointLabel = isHost ? $"0.0.0.0:{ep.Port}" : ep.ToString();
                port = ep.Port;
            }
            else
            {
                endpointLabel = isHost ? $"0.0.0.0:{port}" : $"—:{port}";
            }

            lanStatus = !lan.IsRunning
                ? "LAN idle"
                : isHost
                    ? $"Hosting · {connected} peer(s) · max {lan.MaxClients}"
                    : lan.LastJoinRejectReason is { } reject
                        ? $"Join rejected: {reject}"
                        : $"Connected as P{localId}";
            maxClients = lan.MaxClients > 0 ? lan.MaxClients : maxClients;
        }
        else if (inSession)
        {
            endpointLabel = isHost ? $"loopback host · port {port}" : $"loopback client · port {port}";
            lanStatus = "In-session (non-LAN transport)";
            connected = session!.Ready.Snapshot().Count;
        }
        else
        {
            endpointLabel = $"— · default port {port}";
            lanStatus = "Not connected";
        }

        var peers = BuildPeers(session, localHash, localId, isHost);
        if (connected == 0 && peers.Count > 0)
            connected = peers.Count;

        var lobbyReady = session?.IsLobbyReady ?? !readyRequired;
        var localReady = false;
        if (session is not null && session.Ready.Snapshot().TryGetValue(localId, out var self))
            localReady = self.Ready;

        var roleLabel = !inSession
            ? "Idle"
            : frozen
                ? "Frozen (desync)"
                : jip
                    ? "Join-in-progress…"
                    : isHost
                        ? "Hosting"
                        : "Joined";

        var status = BuildStatusLine(inSession, frozen, jip, lobbyReady, readyRequired, handoffSnap, desync);

        return new LobbyViewModel
        {
            Brand = MpUiPalette.BrandName,
            Subtitle = "Shared colony · LAN · in-game host",
            SessionRoleLabel = roleLabel,
            InSession = inSession,
            IsHost = isHost,
            IsFrozen = frozen,
            IsJoinInProgress = jip,
            LobbyReady = lobbyReady,
            ReadyRequired = readyRequired,
            LocalReady = localReady,
            LocalPlayerId = localId,
            Tick = tick,
            Port = port,
            LanStatus = lanStatus,
            EndpointLabel = endpointLabel,
            LocalModlistHash = localHash,
            LocalModlistShort = ShortHash(localHash),
            ProtocolLabel = $"Protocol v{NetProtocol.Version} · max {maxClients}",
            MaxClients = maxClients,
            ConnectedPeerCount = connected,
            Peers = peers,
            Handoff = handoffSnap,
            Desync = desync,
            StatusLine = status,
            CanHost = !inSession,
            CanJoin = !inSession,
            CanDisconnect = inSession,
            CanToggleReady = inSession && !frozen,
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
        if (localId >= 0)
            ids.Add(localId);
        if (session.Transport is LanTcpTransport lan)
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
            rows.Add(new LobbyPeerRow(
                PlayerId: id,
                DisplayName: hostSeat
                    ? (isLocal ? "You (host)" : "Host")
                    : (isLocal ? $"You (P{id})" : $"Player {id}"),
                IsLocal: isLocal,
                IsHostSeat: hostSeat,
                Ready: ready,
                ModlistHash: hash,
                ModlistShort: ShortHash(hash),
                ModlistMatch: match,
                ReadyLabel: ready ? "Ready" : "Not ready",
                ModlistLabel: string.IsNullOrEmpty(hash)
                    ? "hash ?"
                    : match
                        ? "mods OK"
                        : "mods MISMATCH"));
        }

        return rows;
    }

    private static string BuildStatusLine(
        bool inSession,
        bool frozen,
        bool jip,
        bool lobbyReady,
        bool readyRequired,
        HandoffUiSnapshot handoff,
        DesyncOverlayModel? desync)
    {
        if (frozen && desync is not null)
            return $"DESYNC at tick {desync.Tick} — sim frozen. Dump: {desync.LogPath}";
        if (jip)
            return "Receiving join-in-progress save…";
        if (inSession && readyRequired && !lobbyReady)
            return "Waiting for all peers ready with matching modlist…";
        if (inSession)
            return "Session live. Toggle ready if gated; disconnect to leave.";
        if (handoff.IsError)
            return $"Handoff error: {handoff.Detail}";
        if (handoff.IsOk)
            return handoff.Detail;
        if (handoff.Kind == HandoffUiKind.Missing)
            return handoff.Detail;
        return "Host or join a LAN lobby. F10 toggles this window.";
    }

    public static string ShortHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
            return "(unset)";
        return hash.Length <= 10 ? hash : hash[..10] + "…";
    }
}

using System.Buffers.Binary;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Join;
using RimworldMP.Core.Sync;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Session;

/// <summary>
/// In-game host/client lockstep session with Phase 2 join-in-progress support.
/// </summary>
public sealed class MpSession : IAsyncDisposable
{
    public const int HashIntervalTicks = 60;

    private readonly List<MpCommand> _hostStaging = new();
    private readonly List<MpCommand> _recentApplied = new();
    private readonly object _applyGate = new();
    private readonly JoinPointTransfer _jipReceive = new();
    private readonly CommandLog _commandLog = new();
    private readonly ReadyCheck _ready = new();
    private readonly PlayerRoster _roster = new();
    private readonly SessionChat _chat = new();
    private readonly Dictionary<long, string> _pendingRemoteHashes = new();
    private readonly HashSet<int> _departedPeers = new();
    private int _localPlayerId;
    private long _sessionSeed;
    private bool _wired;
    private bool _jipPending;
    private bool _kicked;
    private string _modlistHash = "unknown";
    private string _localDisplayName = string.Empty;
    /// <summary>Defaults to 1 so a solo host can Begin play / unpause; Begin play locks to present peers.</summary>
    private int _expectedPeers = 1;

    public MpSession(INetTransport transport, bool isHost, long? sessionSeed = null)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        IsHost = isHost;
        _sessionSeed = sessionSeed ?? DeriveSeed();
        Rand = new SyncedRand(_sessionSeed);
        Hasher = new StateHasher();
        Desync = new DesyncDetector(Hasher, Rand);
        TickGate = new TickGate();
        _ready.SetPeer(0, ready: isHost, modlistHash: _modlistHash);
    }

    public INetTransport Transport { get; }
    public bool IsHost { get; }
    public long Tick { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsFrozen => Desync.IsFrozen;
    public int LocalPlayerId => _localPlayerId;
    public long SessionSeed => _sessionSeed;
    public string ModlistHash => _modlistHash;
    public SyncedRand Rand { get; }
    public StateHasher Hasher { get; }
    public DesyncDetector Desync { get; }
    public TickGate TickGate { get; }
    public CommandLog CommandLog => _commandLog;
    public IJoinPointSource? JoinPointSource { get; set; }
    /// <summary>JIP save capture/load. Required when a join point carries a non-empty blob.</summary>
    public ISaveLoad? SaveLoad { get; set; }
    public bool IsJoinInProgress => _jipPending;
    public bool WasKicked => _kicked;
    public ReadyCheck Ready => _ready;
    public PlayerRoster Roster => _roster;
    public SessionChat Chat => _chat;
    public string LocalDisplayName =>
        string.IsNullOrEmpty(_localDisplayName)
            ? _roster.GetName(_localPlayerId, isHostSeat: IsHost)
            : _localDisplayName;
    public bool PauseOnPeerLeave { get; set; } = true;
    public int ExpectedPeers
    {
        get => _expectedPeers;
        set => _expectedPeers = Math.Max(1, value);
    }

    public bool IsLobbyReady => _ready.AllReady(_expectedPeers, _modlistHash);

    /// <summary>Hard gate: ready-check must pass before host emits ticks / unpause.</summary>
    public bool CanStartOrUnpause => _ready.CanStartOrUnpause(_expectedPeers, _modlistHash);

    /// <summary>Host staging queue has commands waiting for the next emitted batch.</summary>
    public bool HasStagedCommands
    {
        get { lock (_applyGate) return _hostStaging.Count > 0; }
    }

    /// <summary>
    /// Lock the ready gate to whoever is already in the ready snapshot (at least 1).
    /// Call from Begin play so solo host is not stuck waiting for a phantom 2nd peer.
    /// </summary>
    public void LockExpectedPeersToPresent()
    {
        ExpectedPeers = Math.Max(1, _ready.Snapshot().Count);
    }

    /// <summary>
    /// While RimWorld is paused, <c>DoSingleTick</c> does not run — flush staged host commands
    /// (unpause/speed) and apply one admitted batch. No-op when there is nothing to do.
    /// </summary>
    public bool PumpPausedCommands()
    {
        if (!IsActive || IsFrozen || _jipPending || _kicked)
            return false;

        var emitted = false;
        if (IsHost && HasStagedCommands)
        {
            HostEmitNextBatch();
            emitted = true;
        }

        return TryAdvanceSim() || emitted;
    }

    public event Action<CommandBatch>? OnBatchApply;
    public event Action<DesyncInfo>? OnDesync;
    public event Action<JoinPoint>? OnJoinPointReceived;
    public event Action<int>? OnPeerLeft;
    public event Action<SessionChatLine>? OnChatLine;
    public event Action? OnKicked;

    public void SetModlistHash(string hash)
    {
        _modlistHash = hash ?? "unknown";
        _ready.SetPeer(_localPlayerId, _ready.Snapshot().TryGetValue(_localPlayerId, out var cur) && cur.Ready, _modlistHash);
    }

    public void SetReady(bool ready)
    {
        _ready.SetPeer(_localPlayerId, ready, _modlistHash);
        var payload = ReadyPayload(_localPlayerId, ready, _modlistHash);
        if (IsHost)
            Transport.BroadcastReliable(Envelope(NetMessageType.ReadyStatus, payload));
        else
            Transport.SendReliable(NetPeerId.Host, Envelope(NetMessageType.ReadyStatus, payload));
    }

    /// <summary>Set local display name and announce to peers (lobby + handoff).</summary>
    public void SetDisplayName(string? displayName)
    {
        _localDisplayName = PlayerRoster.Sanitize(displayName);
        if (_localDisplayName.Length == 0)
            _localDisplayName = SessionMessages.DefaultDisplayName(_localPlayerId, IsHost);
        _roster.SetName(_localPlayerId, _localDisplayName);
        if (!IsActive || !Transport.IsRunning)
            return;
        var payload = PlayerInfoPayload(_localPlayerId, _localDisplayName);
        TrySend(IsHost ? null : NetPeerId.Host, Envelope(NetMessageType.PlayerInfo, payload), broadcast: IsHost);
    }

    /// <summary>Synced chat (session control message — not hashed into sim state).</summary>
    public void SendChat(string text)
    {
        if (!IsActive || _kicked)
            return;
        var cleaned = PlayerRoster.Sanitize(text);
        if (cleaned.Length == 0)
            cleaned = text?.Trim() ?? string.Empty;
        if (cleaned.Length == 0)
            return;
        if (cleaned.Length > 240)
            cleaned = cleaned[..240];

        var payload = ChatPayload(_localPlayerId, cleaned);
        if (IsHost)
        {
            TrySend(null, Envelope(NetMessageType.Chat, payload), broadcast: true);
            // Host echoes locally; clients wait for host rebroadcast.
            AcceptChat(_localPlayerId, cleaned);
        }
        else
        {
            TrySend(NetPeerId.Host, Envelope(NetMessageType.Chat, payload), broadcast: false);
        }
    }

    public void BroadcastSystemNotice(SystemNoticeKind kind, string text)
    {
        if (!IsActive || !IsHost)
            return;
        var payload = SystemNoticePayload(kind, text);
        if (Transport.IsRunning)
            TrySend(null, Envelope(NetMessageType.SystemNotice, payload), broadcast: true);
        AcceptSystemNotice(kind, text, playerId: -1);
    }

    /// <summary>Host: kick a peer, announce, and disconnect the transport socket when possible.</summary>
    public bool HostKickPeer(int playerId, string? reason = null)
    {
        if (!IsHost || !IsActive || playerId <= 0)
            return false;

        var reasonText = string.IsNullOrWhiteSpace(reason)
            ? SessionMessages.Get("RMP.KickDefaultReason")
            : reason!.Trim();
        if (reasonText.Length > 200)
            reasonText = reasonText[..200];

        var peer = new NetPeerId(playerId);
        var kickPayload = Utf16Payload(reasonText);
        try
        {
            Transport.SendReliable(peer, Envelope(NetMessageType.KickNotice, kickPayload));
        }
        catch
        {
            // Peer may already be gone.
        }

        if (TryMarkDeparted(playerId))
        {
            SessionRecovery.HandlePeerDeparted(this, playerId, kicked: true);
            OnPeerLeft?.Invoke(playerId);
            if (PauseOnPeerLeave)
                SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(true));
        }

        DisconnectTransportPeer(peer);
        return true;
    }

    public void RequestRecoveryRejoin() => SessionRecovery.RequestRejoin(this);

    public async Task StartAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (IsActive)
            throw new InvalidOperationException("Session already active.");

        WireTransport();

        if (IsHost)
        {
            _localPlayerId = 0;
            if (Transport is ISessionLanTransport hostLan)
            {
                hostLan.HostSessionSeed = _sessionSeed;
                hostLan.ExpectedModlistHash = _modlistHash;
                if (hostLan.MaxClients < 1)
                    hostLan.MaxClients = MpConfig.MaxClients;
            }

            await Transport.StartHostAsync(endpoint, cancellationToken).ConfigureAwait(false);
            SeedHasherIdentity();
            TickGate.EnqueueBatch(CommandBatch.Empty(0));
            TickGate.TryBeginTick(out _);
            Tick = 0;
            EnsureLocalDisplayName();
            _roster.SetName(_localPlayerId, _localDisplayName);
        }
        else
        {
            if (Transport is ISessionLanTransport clientLanPre)
                clientLanPre.LocalModlistHash = _modlistHash;

            await Transport.StartClientAsync(endpoint, cancellationToken).ConfigureAwait(false);
            if (Transport is ISessionLanTransport clientLan)
            {
                _localPlayerId = clientLan.AssignedPlayerId ?? 1;
                if (clientLan.JoinedSessionSeed is long seed)
                    ApplySessionSeed(seed);
                else
                    SeedHasherIdentity();
            }
            else
            {
                _localPlayerId = 1;
                SeedHasherIdentity();
            }

            Tick = 0;
            EnsureLocalDisplayName();
            _roster.SetName(_localPlayerId, _localDisplayName);
        }

        IsActive = true;

        // Announce name after the session is active so PlayerInfo is actually sent.
        SetDisplayName(_localDisplayName);
        if (!IsHost)
            RequestJoinPoint();
    }

    public void ApplySessionSeed(long seed)
    {
        _sessionSeed = seed;
        Rand.ResetSession(seed);
        Hasher.Reset();
        Desync.Reset();
        SeedHasherIdentity();
    }

    private void SeedHasherIdentity()
    {
        Hasher.AddProtocolVersion(NetProtocol.Version);
        Hasher.AddSessionSeed(_sessionSeed);
        Hasher.AddUtf8(_modlistHash);
    }

    public void SubmitCommand(CommandOpcode opcode, byte[]? payload = null)
    {
        if (!IsActive)
            throw new InvalidOperationException("Session is not active.");
        if (IsFrozen || _jipPending || _kicked)
            return;

        // Ready-check hard gate: block unpause / speed-up until lobby is ready.
        if (_ready.Required && !CanStartOrUnpause && IsUnpauseOrSpeedUp(opcode, payload))
        {
            _chat.AppendSystem(
                SystemNoticeKind.ReadyChanged,
                SessionMessages.Get("RMP.ReadyBlockedUnpause"),
                _localPlayerId,
                LocalDisplayName,
                Tick);
            return;
        }

        var cmd = new MpCommand(opcode, tick: 0, _localPlayerId, payload);
        if (IsHost)
        {
            lock (_applyGate)
                _hostStaging.Add(cmd);
        }
        else
        {
            Transport.SendReliable(NetPeerId.Host, Envelope(NetMessageType.CommandBatch, cmd.ToBytes()));
        }
    }

    public void HostEmitNextBatch()
    {
        if (!IsHost || !IsActive || IsFrozen)
            return;
        if (_ready.Required && !IsLobbyReady)
            return;

        long tick;
        CommandBatch batch;
        lock (_applyGate)
        {
            tick = Tick + 1;
            var stamped = new List<MpCommand>(_hostStaging.Count);
            foreach (var staged in _hostStaging)
                stamped.Add(new MpCommand(staged.Opcode, tick, staged.PlayerId, staged.Payload));
            _hostStaging.Clear();
            batch = new CommandBatch(tick, stamped);
        }

        var bytes = batch.ToBytes();
        TickGate.EnqueueBatch(batch);
        _commandLog.Record(tick, bytes);
        Transport.BroadcastReliable(Envelope(NetMessageType.CommandBatch, bytes));
    }

    public bool TryAdvanceSim()
    {
        if (!IsActive || IsFrozen || _jipPending)
            return false;

        if (!TickGate.TryBeginTick(out var batch))
            return false;

        lock (_applyGate)
        {
            foreach (var cmd in batch.Commands)
            {
                Hasher.AddCommand(cmd);
                _recentApplied.Add(cmd);
                if (_recentApplied.Count > 256)
                    _recentApplied.RemoveAt(0);
            }

            Rand.ReseedForTick(batch.Tick);
            Desync.IncrementCounter("ticks");
            Tick = batch.Tick;
            OnBatchApply?.Invoke(batch);

            if (Tick > 0 && Tick % HashIntervalTicks == 0)
                PublishHash(Tick);

            // Compare any remote hash that arrived before we reached this tick.
            if (_pendingRemoteHashes.Remove(Tick, out var remoteHex))
                EvaluateRemoteHash(Tick, remoteHex);
        }

        return true;
    }

    public void AdvanceTickStub()
    {
        if (IsHost)
            HostEmitNextBatch();
        TryAdvanceSim();
    }

    public void RequestJoinPoint()
    {
        _jipPending = true;
        _jipReceive.Reset();
        Transport.SendReliable(NetPeerId.Host, Envelope(NetMessageType.JoinPointRequest, JoinPointTransfer.RequestBytes()));
    }

    /// <summary>Host: push a join point to one peer (or build from <see cref="JoinPointSource"/>).</summary>
    public void HostSendJoinPoint(NetPeerId peer, JoinPoint? joinPoint = null)
    {
        if (!IsHost)
            throw new InvalidOperationException("Only the host can send a join point.");

        if (joinPoint is null)
        {
            joinPoint = JoinPointSource?.TryCreateJoinPoint(Tick + 1, _sessionSeed, Tick);
            if (joinPoint is null)
            {
                var blob = (SaveLoad ?? GameLoad.Current)?.CaptureSaveBlob() ?? Array.Empty<byte>();
                joinPoint = new JoinPoint(Tick + 1, _sessionSeed, Tick, _modlistHash, blob);
            }
        }

        JoinPointTransfer.SendToPeer(Transport, peer, joinPoint, Envelope);

        // Catch-up batches after the save watermark.
        foreach (var batchBytes in _commandLog.BatchesAfter(joinPoint.CommandWatermark))
            Transport.SendReliable(peer, Envelope(NetMessageType.CommandBatch, batchBytes));
    }

    /// <summary>
    /// Apply a received join point locally. Non-empty save blobs require <see cref="SaveLoad"/>
    /// (or <see cref="GameLoad.Current"/>) — transfer-only is rejected loudly.
    /// </summary>
    public void ApplyJoinPoint(JoinPoint joinPoint)
    {
        GameLoad.ApplyJoinPointBlob(joinPoint.SaveBlob, SaveLoad ?? GameLoad.Current);

        _modlistHash = joinPoint.ModlistHash;
        ApplySessionSeed(joinPoint.SessionSeed);
        TickGate.Reset(startingTick: joinPoint.CommandWatermark + 1);
        Tick = joinPoint.CommandWatermark;
        _jipPending = false;
        OnJoinPointReceived?.Invoke(joinPoint);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsActive)
            return;
        // Unwire before tearing down transport so Stop's OnPeerLeave does not re-enter session QoL.
        IsActive = false;
        UnwireTransport();
        await Transport.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private void WireTransport()
    {
        if (_wired)
            return;
        Transport.OnReceive += HandleReceive;
        Transport.OnPeerJoin += HandlePeerJoin;
        Transport.OnPeerLeave += HandlePeerLeave;
        _wired = true;
    }

    private void UnwireTransport()
    {
        if (!_wired)
            return;
        Transport.OnReceive -= HandleReceive;
        Transport.OnPeerJoin -= HandlePeerJoin;
        Transport.OnPeerLeave -= HandlePeerLeave;
        _wired = false;
    }

    private void HandlePeerJoin(NetPeerId peer)
    {
        if (!IsHost || peer.Value == 0)
            return;

        // Share host display name with the new peer.
        if (!string.IsNullOrEmpty(_localDisplayName))
        {
            try
            {
                Transport.SendReliable(peer, Envelope(NetMessageType.PlayerInfo, PlayerInfoPayload(_localPlayerId, _localDisplayName)));
            }
            catch
            {
                // ignore
            }
        }

        var joinText = SessionMessages.Format("RMP.PeerJoined", _roster.GetName(peer.Value));
        BroadcastSystemNotice(SystemNoticeKind.PeerJoined, joinText);

        // Mid-session join: push freeze-frame automatically when a source is configured
        // or when we already have simulation progress.
        if (JoinPointSource is not null || Tick > 0)
            HostSendJoinPoint(peer);
    }

    private void HandlePeerLeave(NetPeerId peer)
    {
        if (!IsActive)
            return;

        if (peer.Value == 0 && !IsHost)
        {
            if (TryMarkDeparted(0))
            {
                SessionRecovery.HandlePeerDeparted(this, 0, kicked: false);
                OnPeerLeft?.Invoke(0);
            }

            return;
        }

        if (!IsHost || peer.Value == 0)
            return;

        if (!TryMarkDeparted(peer.Value))
            return;

        SessionRecovery.HandlePeerDeparted(this, peer.Value, kicked: false);
        OnPeerLeft?.Invoke(peer.Value);

        var leavePayload = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(leavePayload, peer.Value);
        TrySend(null, Envelope(NetMessageType.PeerLeaveNotice, leavePayload), broadcast: true);

        if (PauseOnPeerLeave)
            SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(true));
    }

    private void HandleReceive(NetPeerId peer, ReadOnlyMemory<byte> data)
    {
        if (data.Length < 1)
            return;

        var type = (NetMessageType)data.Span[0];
        var payload = data.Span[1..];

        switch (type)
        {
            case NetMessageType.CommandBatch:
                HandleCommandBatch(peer, payload);
                break;
            case NetMessageType.StateHash:
                HandleStateHash(payload);
                break;
            case NetMessageType.DesyncFreeze:
                HandleRemoteFreeze(payload);
                break;
            case NetMessageType.JoinPointRequest:
                if (IsHost)
                    HostSendJoinPoint(peer);
                break;
            case NetMessageType.JoinPointHeader:
                _jipReceive.AcceptHeader(payload);
                break;
            case NetMessageType.JoinPointChunk:
                _jipReceive.AcceptChunk(payload);
                break;
            case NetMessageType.JoinPointDone:
            {
                // Fail loud on incomplete/corrupt reassembly; do not treat as empty lobby.
                var jp = _jipReceive.CompleteOrThrow();
                ApplyJoinPoint(jp);
                break;
            }
            case NetMessageType.ReadyStatus:
                HandleReadyStatus(payload);
                break;
            case NetMessageType.PeerLeaveNotice:
                if (payload.Length >= 4)
                {
                    var left = BinaryPrimitives.ReadInt32LittleEndian(payload);
                    if (!IsHost && TryMarkDeparted(left))
                    {
                        SessionRecovery.HandlePeerDeparted(this, left, kicked: false);
                        OnPeerLeft?.Invoke(left);
                        if (PauseOnPeerLeave)
                            SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(true));
                    }
                }
                break;
            case NetMessageType.Chat:
                HandleChat(peer, payload);
                break;
            case NetMessageType.SystemNotice:
                HandleSystemNotice(payload);
                break;
            case NetMessageType.PlayerInfo:
                HandlePlayerInfo(peer, payload);
                break;
            case NetMessageType.KickNotice:
                HandleKickNotice(payload);
                break;
        }
    }

    private void HandleReadyStatus(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6)
            return;
        var playerId = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var ready = payload[4] != 0;
        var hashLen = payload[5];
        if (payload.Length < 6 + hashLen)
            return;
        var hash = hashLen == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(payload.Slice(6, hashLen));
        _ready.SetPeer(playerId, ready, hash);
        if (IsHost)
            Transport.BroadcastReliable(Envelope(NetMessageType.ReadyStatus, payload.ToArray()));
    }

    private static byte[] ReadyPayload(int playerId, bool ready, string modlistHash)
    {
        var hashBytes = System.Text.Encoding.UTF8.GetBytes(modlistHash ?? string.Empty);
        if (hashBytes.Length > 255)
            hashBytes = hashBytes.AsSpan(0, 255).ToArray();
        var buf = new byte[6 + hashBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, playerId);
        buf[4] = ready ? (byte)1 : (byte)0;
        buf[5] = (byte)hashBytes.Length;
        hashBytes.CopyTo(buf.AsSpan(6));
        return buf;
    }

    private static byte[] Envelope(NetMessageType type, ReadOnlyMemory<byte> payload)
    {
        var buffer = new byte[1 + payload.Length];
        buffer[0] = (byte)type;
        payload.Span.CopyTo(buffer.AsSpan(1));
        return buffer;
    }

    private void HandleCommandBatch(NetPeerId peer, ReadOnlySpan<byte> payload)
    {
        if (IsHost)
        {
            if (peer.Value == 0)
                return;

            try
            {
                var cmd = MpCommand.ReadFrom(payload, out var consumed);
                if (consumed == payload.Length && cmd.Tick == 0)
                {
                    lock (_applyGate)
                        _hostStaging.Add(new MpCommand(cmd.Opcode, 0, peer.Value, cmd.Payload));
                }
            }
            catch
            {
                // ignore malformed proposals
            }

            return;
        }

        try
        {
            var batch = CommandBatch.FromBytes(payload);
            TickGate.EnqueueBatch(batch);
            _commandLog.Record(batch.Tick, payload.ToArray());
        }
        catch
        {
            // ignore malformed
        }
    }

    private void HandleStateHash(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8 + 32)
            return;

        var tick = BinaryPrimitives.ReadInt64LittleEndian(payload);
        var remoteHex = Convert.ToHexString(payload.Slice(8, 32)).ToLowerInvariant();

        lock (_applyGate)
        {
            if (tick > Tick)
            {
                _pendingRemoteHashes[tick] = remoteHex;
                return;
            }

            if (tick < Tick)
                return; // stale
        }

        EvaluateRemoteHash(tick, remoteHex);
    }

    private void EvaluateRemoteHash(long tick, string remoteHex)
    {
        List<MpCommand> recent;
        lock (_applyGate)
            recent = _recentApplied.ToList();

        if (!Desync.CheckRemote(tick, remoteHex, recent, BuildDumpContext()))
        {
            AnnounceFreeze();
            var info = Desync.LastDesync;
            if (info is not null)
                OnDesync?.Invoke(info);
        }
    }

    private void HandleRemoteFreeze(ReadOnlySpan<byte> payload)
    {
        var tick = payload.Length >= 8 ? BinaryPrimitives.ReadInt64LittleEndian(payload) : Tick;
        var remoteHex = payload.Length > 8 ? Convert.ToHexString(payload[8..]).ToLowerInvariant() : "remote-freeze";
        List<MpCommand> recent;
        lock (_applyGate)
            recent = _recentApplied.ToList();
        Desync.Freeze(tick, Desync.SnapshotHashHex(tick), remoteHex, recent, BuildDumpContext());
        var info = Desync.LastDesync;
        if (info is not null)
            OnDesync?.Invoke(info);
    }

    /// <summary>Local-only dump metadata: peer ids, modlist hash short, optional batch ring.</summary>
    private DesyncDumpContext BuildDumpContext()
    {
        var peerIds = new SortedSet<int> { _localPlayerId };
        foreach (var id in _ready.Snapshot().Keys)
            peerIds.Add(id);
        if (Transport is LanTcpTransport lan)
        {
            foreach (var peer in lan.ConnectedPeers)
                peerIds.Add(peer.Value);
        }

        return new DesyncDumpContext
        {
            LocalPeerId = _localPlayerId,
            PeerIds = peerIds.ToList(),
            ModlistHash = _modlistHash,
            RecentBatches = _commandLog.SnapshotRecent(),
        };
    }

    private void PublishHash(long tick)
    {
        var hash = Desync.SnapshotHash(tick);
        var buf = new byte[8 + hash.Length];
        BinaryPrimitives.WriteInt64LittleEndian(buf, tick);
        hash.CopyTo(buf.AsSpan(8));
        Transport.BroadcastReliable(Envelope(NetMessageType.StateHash, buf));
    }

    private void AnnounceFreeze()
    {
        var info = Desync.LastDesync;
        if (info is null)
            return;

        var hashBytes = Convert.FromHexString(info.LocalHashHex);
        var buf = new byte[8 + hashBytes.Length];
        BinaryPrimitives.WriteInt64LittleEndian(buf, info.Tick);
        hashBytes.CopyTo(buf.AsSpan(8));
        Transport.BroadcastReliable(Envelope(NetMessageType.DesyncFreeze, buf));
    }

    private void HandleChat(NetPeerId peer, ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6)
            return;
        var playerId = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]);
        if (payload.Length < 6 + len)
            return;
        var text = len == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(payload.Slice(6, len));

        // Clients may only chat as themselves; host rebroadcasts.
        if (IsHost)
        {
            if (peer.Value != 0 && playerId != peer.Value)
                playerId = peer.Value;
            Transport.BroadcastReliable(Envelope(NetMessageType.Chat, ChatPayload(playerId, text)));
        }

        AcceptChat(playerId, text);
    }

    private void HandleSystemNotice(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 3)
            return;
        var kind = (SystemNoticeKind)payload[0];
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[1..]);
        if (payload.Length < 3 + len)
            return;
        var text = len == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(payload.Slice(3, len));
        AcceptSystemNotice(kind, text, playerId: -1);
    }

    private void HandlePlayerInfo(NetPeerId peer, ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6)
            return;
        var playerId = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]);
        if (payload.Length < 6 + len)
            return;
        var name = len == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(payload.Slice(6, len));
        if (IsHost && peer.Value != 0)
            playerId = peer.Value;
        _roster.SetName(playerId, name);
        if (IsHost)
            Transport.BroadcastReliable(Envelope(NetMessageType.PlayerInfo, PlayerInfoPayload(playerId, _roster.GetName(playerId))));
    }

    private void HandleKickNotice(ReadOnlySpan<byte> payload)
    {
        if (IsHost)
            return;
        var reason = ReadUtf16Payload(payload);
        if (string.IsNullOrEmpty(reason))
            reason = SessionMessages.Get("RMP.KickDefaultReason");
        _kicked = true;
        var text = SessionMessages.Format("RMP.YouWereKicked", reason);
        AcceptSystemNotice(SystemNoticeKind.PeerKicked, text, _localPlayerId);
        OnKicked?.Invoke();
    }

    private void AcceptChat(int playerId, string text)
    {
        var name = _roster.GetName(playerId, isHostSeat: playerId == 0);
        var line = new SessionChatLine(
            SessionChatKind.Chat,
            playerId,
            name,
            text,
            Tick,
            DateTimeOffset.UtcNow);
        _chat.Append(line);
        OnChatLine?.Invoke(line);
    }

    private void AcceptSystemNotice(SystemNoticeKind kind, string text, int playerId)
    {
        var name = playerId >= 0 ? _roster.GetName(playerId, isHostSeat: playerId == 0) : string.Empty;
        var line = new SessionChatLine(
            SessionChatKind.System,
            playerId,
            name,
            text,
            Tick,
            DateTimeOffset.UtcNow,
            kind);
        _chat.Append(line);
        OnChatLine?.Invoke(line);
    }

    private void EnsureLocalDisplayName()
    {
        if (string.IsNullOrEmpty(_localDisplayName))
            _localDisplayName = SessionMessages.DefaultDisplayName(_localPlayerId, IsHost);
    }

    private bool TryMarkDeparted(int playerId)
    {
        lock (_applyGate)
            return _departedPeers.Add(playerId);
    }

    private void DisconnectTransportPeer(NetPeerId peer)
    {
        switch (Transport)
        {
            case LanTcpTransport lan:
                lan.DisconnectPeer(peer);
                break;
            case LiteNetLibTransport lnl:
                lnl.DisconnectPeer(peer);
                break;
            case LoopbackTransport loop:
                loop.DisconnectPeer(peer);
                break;
        }
    }

    private void TrySend(NetPeerId? peer, byte[] envelope, bool broadcast)
    {
        if (!Transport.IsRunning)
            return;
        try
        {
            if (broadcast)
                Transport.BroadcastReliable(envelope);
            else if (peer is { } p)
                Transport.SendReliable(p, envelope);
        }
        catch
        {
            // Unpaired loopback / already-closed peer — QoL messages are best-effort.
        }
    }

    private static bool IsUnpauseOrSpeedUp(CommandOpcode opcode, byte[]? payload)
    {
        if (opcode == CommandOpcode.Speed)
        {
            var speed = CommandPayloads.ReadSpeed(payload ?? Array.Empty<byte>());
            return speed > 0;
        }

        if (opcode == CommandOpcode.Pause)
            return !CommandPayloads.ReadPause(payload ?? Array.Empty<byte>());

        return false;
    }

    private static byte[] ChatPayload(int playerId, string text)
    {
        var textBytes = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty);
        if (textBytes.Length > 240)
            textBytes = textBytes.AsSpan(0, 240).ToArray();
        var buf = new byte[6 + textBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, playerId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), (ushort)textBytes.Length);
        textBytes.CopyTo(buf.AsSpan(6));
        return buf;
    }

    private static byte[] SystemNoticePayload(SystemNoticeKind kind, string text)
    {
        var textBytes = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty);
        if (textBytes.Length > 240)
            textBytes = textBytes.AsSpan(0, 240).ToArray();
        var buf = new byte[3 + textBytes.Length];
        buf[0] = (byte)kind;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(1), (ushort)textBytes.Length);
        textBytes.CopyTo(buf.AsSpan(3));
        return buf;
    }

    private static byte[] PlayerInfoPayload(int playerId, string name)
    {
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(PlayerRoster.Sanitize(name));
        var buf = new byte[6 + nameBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, playerId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(6));
        return buf;
    }

    private static byte[] Utf16Payload(string text)
    {
        var textBytes = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty);
        if (textBytes.Length > 200)
            textBytes = textBytes.AsSpan(0, 200).ToArray();
        var buf = new byte[2 + textBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, (ushort)textBytes.Length);
        textBytes.CopyTo(buf.AsSpan(2));
        return buf;
    }

    private static string ReadUtf16Payload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2)
            return string.Empty;
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + len || len == 0)
            return string.Empty;
        return System.Text.Encoding.UTF8.GetString(payload.Slice(2, len));
    }

    private static long DeriveSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        Random.Shared.NextBytes(bytes);
        return BitConverter.ToInt64(bytes);
    }
}

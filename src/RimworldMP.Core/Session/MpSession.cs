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
    private readonly Dictionary<long, string> _pendingRemoteHashes = new();
    private int _localPlayerId;
    private long _sessionSeed;
    private bool _wired;
    private bool _jipPending;
    private string _modlistHash = "unknown";
    private int _expectedPeers = 2;

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
    public ReadyCheck Ready => _ready;
    public bool PauseOnPeerLeave { get; set; } = true;
    public int ExpectedPeers
    {
        get => _expectedPeers;
        set => _expectedPeers = Math.Max(1, value);
    }

    public bool IsLobbyReady => _ready.AllReady(_expectedPeers, _modlistHash);

    public event Action<CommandBatch>? OnBatchApply;
    public event Action<DesyncInfo>? OnDesync;
    public event Action<JoinPoint>? OnJoinPointReceived;
    public event Action<int>? OnPeerLeft;

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
            // Request JIP immediately — host may reply with a freeze-frame or an empty lobby hello.
            RequestJoinPoint();
        }

        IsActive = true;
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
        if (IsFrozen || _jipPending)
            return;

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
        await Transport.StopAsync(cancellationToken).ConfigureAwait(false);
        IsActive = false;
        UnwireTransport();
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private void WireTransport()
    {
        if (_wired)
            return;
        Transport.OnReceive += HandleReceive;
        Transport.OnPeerJoin += HandlePeerJoin;
        _wired = true;
    }

    private void UnwireTransport()
    {
        if (!_wired)
            return;
        Transport.OnReceive -= HandleReceive;
        Transport.OnPeerJoin -= HandlePeerJoin;
        _wired = false;
    }

    private void HandlePeerJoin(NetPeerId peer)
    {
        if (!IsHost || peer.Value == 0)
            return;
        // Mid-session join: push freeze-frame automatically when a source is configured
        // or when we already have simulation progress.
        if (JoinPointSource is not null || Tick > 0)
            HostSendJoinPoint(peer);
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
                    OnPeerLeft?.Invoke(left);
                    if (PauseOnPeerLeave)
                        SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(true));
                }
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

    private static long DeriveSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        Random.Shared.NextBytes(bytes);
        return BitConverter.ToInt64(bytes);
    }
}

using System.Buffers.Binary;
using System.Collections.Concurrent;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Sync;
using RimworldMP.Net;
using RimworldMP.Net.Protocol;

namespace RimworldMP.Core.Session;

/// <summary>
/// In-game host/client lockstep session. Host collects commands, assigns ticks, broadcasts batches;
/// every peer (including host) applies the same ordered stream behind <see cref="TickGate"/>.
/// </summary>
public sealed class MpSession : IAsyncDisposable
{
    public const int HashIntervalTicks = 60;

    private readonly List<MpCommand> _hostStaging = new();
    private readonly List<MpCommand> _recentApplied = new();
    private readonly object _applyGate = new();
    private int _localPlayerId;
    private readonly long _sessionSeed;
    private bool _wired;

    public MpSession(INetTransport transport, bool isHost, long? sessionSeed = null)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        IsHost = isHost;
        _sessionSeed = sessionSeed ?? DeriveSeed();
        Rand = new SyncedRand(_sessionSeed);
        Hasher = new StateHasher();
        Desync = new DesyncDetector(Hasher, Rand);
        TickGate = new TickGate();
    }

    public INetTransport Transport { get; }
    public bool IsHost { get; }
    public long Tick { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsFrozen => Desync.IsFrozen;
    public int LocalPlayerId => _localPlayerId;
    public long SessionSeed => _sessionSeed;
    public SyncedRand Rand { get; }
    public StateHasher Hasher { get; }
    public DesyncDetector Desync { get; }
    public TickGate TickGate { get; }

    /// <summary>Invoked after a batch is admitted (before the tick body continues).</summary>
    public event Action<CommandBatch>? OnBatchApply;

    /// <summary>Invoked when desync freeze engages.</summary>
    public event Action<DesyncInfo>? OnDesync;

    public async Task StartAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (IsActive)
            throw new InvalidOperationException("Session already active.");

        WireTransport();

        if (IsHost)
        {
            _localPlayerId = 0;
            await Transport.StartHostAsync(endpoint, cancellationToken).ConfigureAwait(false);
            // Consume a synthetic tick-0 empty batch so the first real tick is 1.
            TickGate.EnqueueBatch(CommandBatch.Empty(0));
            TickGate.TryBeginTick(out _);
            Tick = 0;
        }
        else
        {
            await Transport.StartClientAsync(endpoint, cancellationToken).ConfigureAwait(false);
            // Phase 1: first client is player 1 (JoinAck assigns the same id on the wire).
            _localPlayerId = 1;
            Tick = 0;
        }

        IsActive = true;
    }

    /// <summary>
    /// Queue a player command. Host stages it for the next emitted tick; clients send a proposal to the host.
    /// </summary>
    public void SubmitCommand(CommandOpcode opcode, byte[]? payload = null)
    {
        if (!IsActive)
            throw new InvalidOperationException("Session is not active.");
        if (IsFrozen)
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

    /// <summary>
    /// Host: seal the next tick's command batch and broadcast. Call once per sim step before <see cref="TryAdvanceSim"/>.
    /// </summary>
    public void HostEmitNextBatch()
    {
        if (!IsHost || !IsActive || IsFrozen)
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

        TickGate.EnqueueBatch(batch);
        Transport.BroadcastReliable(Envelope(NetMessageType.CommandBatch, batch.ToBytes()));
    }

    /// <summary>
    /// Admit the next tick's batch when ready. Returns false when stalled (waiting / frozen).
    /// </summary>
    public bool TryAdvanceSim()
    {
        if (!IsActive || IsFrozen)
            return false;

        if (!TickGate.TryBeginTick(out var batch))
            return false;

        lock (_applyGate)
        {
            foreach (var cmd in batch.Commands)
            {
                Hasher.AddCommand(cmd);
                _recentApplied.Add(cmd);
                if (_recentApplied.Count > 128)
                    _recentApplied.RemoveAt(0);
            }

            Rand.ReseedForTick(batch.Tick);
            Tick = batch.Tick;
            OnBatchApply?.Invoke(batch);

            if (Tick > 0 && Tick % HashIntervalTicks == 0)
                PublishHash(Tick);
        }

        return true;
    }

    /// <summary>Convenience for harnesses: host emit + try advance.</summary>
    public void AdvanceTickStub()
    {
        if (IsHost)
            HostEmitNextBatch();
        TryAdvanceSim();
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

    private void HandlePeerJoin(NetPeerId peer) => _ = peer;

    private void HandleReceive(NetPeerId peer, ReadOnlyMemory<byte> data)
    {
        if (data.Length < 1)
            return;

        // Wire format from LanTcpTransport OnReceive and session-layer envelopes:
        // [NetMessageType][payload…]
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
        }
    }

    private static byte[] Envelope(NetMessageType type, ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[1 + payload.Length];
        buffer[0] = (byte)type;
        payload.CopyTo(buffer.AsSpan(1));
        return buffer;
    }

    private void HandleCommandBatch(NetPeerId peer, ReadOnlySpan<byte> payload)
    {
        if (IsHost && peer.Value != 0)
        {
            // Client proposals are a single MpCommand (tick==0).
            try
            {
                var cmd = MpCommand.ReadFrom(payload, out var consumed);
                if (consumed == payload.Length && cmd.Tick == 0)
                {
                    lock (_applyGate)
                        _hostStaging.Add(new MpCommand(cmd.Opcode, 0, peer.Value, cmd.Payload));
                    return;
                }
            }
            catch
            {
                // fall through
            }
        }

        try
        {
            TickGate.EnqueueBatch(CommandBatch.FromBytes(payload));
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

        List<MpCommand> recent;
        lock (_applyGate)
            recent = _recentApplied.ToList();

        if (!Desync.CheckRemote(tick, remoteHex, recent))
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
        Desync.Freeze(tick, Desync.SnapshotHashHex(tick), remoteHex, null);
        var info = Desync.LastDesync;
        if (info is not null)
            OnDesync?.Invoke(info);
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

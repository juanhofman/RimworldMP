using RimworldMP.Net;

namespace RimworldMP.Core.Session;

/// <summary>
/// In-game host/client session stub. No lockstep yet — holds transport + tick counter only.
/// </summary>
public sealed class MpSession
{
    public MpSession(INetTransport transport, bool isHost)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        IsHost = isHost;
    }

    public INetTransport Transport { get; }
    public bool IsHost { get; }
    public long Tick { get; private set; }
    public bool IsActive { get; private set; }

    public async Task StartAsync(NetEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (IsActive)
            throw new InvalidOperationException("Session already active.");

        if (IsHost)
            await Transport.StartHostAsync(endpoint, cancellationToken).ConfigureAwait(false);
        else
            await Transport.StartClientAsync(endpoint, cancellationToken).ConfigureAwait(false);

        Tick = 0;
        IsActive = true;
    }

    /// <summary>
    /// Advances the local tick counter. Phase 0 does not wait for command batches.
    /// </summary>
    public void AdvanceTickStub()
    {
        if (!IsActive)
            throw new InvalidOperationException("Session is not active.");
        Tick++;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsActive)
            return;
        await Transport.StopAsync(cancellationToken).ConfigureAwait(false);
        IsActive = false;
    }
}

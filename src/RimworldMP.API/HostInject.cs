namespace RimworldMP.API;

/// <summary>
/// Host-only compute + inject-on-all-peers helpers.
/// Use when a mod must perform nondeterministic I/O (HTTP, disk, LLM) on the host,
/// then apply a deterministic result everywhere via a registered SyncMethod.
/// </summary>
public static class HostInject
{
    /// <summary>
    /// Runs <paramref name="hostWork"/> only when this peer is the in-game host
    /// (or when not in a session — offline single-player / CI stubs).
    /// </summary>
    /// <returns>True when the work ran.</returns>
    public static bool TryRunHostOnly(Action hostWork)
    {
        ArgumentNullException.ThrowIfNull(hostWork);
        if (MP.IsInSession && !MP.IsHost)
            return false;
        hostWork();
        return true;
    }

    /// <summary>
    /// Host computes <typeparamref name="T"/>, then every peer (including host) applies it
    /// through <paramref name="applyOnAllPeers"/>. When not in session, compute+apply run locally.
    /// </summary>
    /// <remarks>
    /// <paramref name="applyOnAllPeers"/> must be a registered SyncMethod body (or call one).
    /// In a live session the host path is: compute locally → enqueue sync apply.
    /// Clients skip compute and wait for the synced apply from the command stream.
    /// This stub invokes apply immediately after host compute so offline examples and harness
    /// tests can exercise the pattern without Core command wiring.
    /// </remarks>
    public static bool HostComputeAndInject<T>(Func<T> hostCompute, Action<T> applyOnAllPeers)
    {
        ArgumentNullException.ThrowIfNull(hostCompute);
        ArgumentNullException.ThrowIfNull(applyOnAllPeers);

        if (!MP.IsInSession)
        {
            applyOnAllPeers(hostCompute());
            return true;
        }

        if (!MP.IsHost)
            return false;

        var value = hostCompute();
        applyOnAllPeers(value);
        return true;
    }

    /// <summary>
    /// Same as <see cref="HostComputeAndInject{T}"/> but also enqueues a raw opcode payload
    /// so Core can re-broadcast the result. Prefer SyncMethod registration when possible.
    /// </summary>
    public static bool HostComputeInjectCommand<T>(
        Func<T> hostCompute,
        Func<T, byte[]?> serializePayload,
        ushort opcode,
        Action<T>? localPreviewApply = null)
    {
        ArgumentNullException.ThrowIfNull(hostCompute);
        ArgumentNullException.ThrowIfNull(serializePayload);

        if (MP.IsInSession && !MP.IsHost)
            return false;

        var value = hostCompute();
        localPreviewApply?.Invoke(value);
        MP.ExecuteCommand(opcode, serializePayload(value));
        return true;
    }
}

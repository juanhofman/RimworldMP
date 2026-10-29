using RimworldMP.API;

namespace RimworldMP.Examples.CompatHostIo;

/// <summary>
/// Host-only I/O pattern: host computes a result, then all peers apply via a registered SyncMethod.
/// Compiles against <c>RimworldMP.API</c> only (NO_GAME_REFS / no Verse).
/// </summary>
public static class CompatHostIoMod
{
    /// <summary>Last injected seed — deterministic apply target for demos/tests.</summary>
    public static int LastAppliedSeed { get; private set; }

    /// <summary>Register SyncMethod apply body. Call once at mod load.</summary>
    public static void Initialize()
    {
        MP.RegisterSyncMethodsFromType(typeof(CompatHostIoMod));
    }

    /// <summary>Host-only nondeterministic work (HTTP, disk, LLM, <see cref="Random"/>, etc.).</summary>
    public static int HostComputeSeed() => Random.Shared.Next();

    /// <summary>Deterministic apply on every peer — must not redo host I/O.</summary>
    [SyncMethod("apply_host_seed")]
    public static void ApplyHostComputedSeed(int seed)
    {
        LastAppliedSeed = seed;
        // Optional: also mirror through a Core opcode when a session is bound.
        if (MP.IsInSession)
            MP.ExecuteCommand(opcode: 1, payload: BitConverter.GetBytes(seed));
    }

    /// <summary>
    /// Full host→inject flow. Clients in-session skip compute and wait for the synced apply
    /// (stub: <see cref="HostInject.HostComputeAndInject{T}"/> no-ops on non-host peers).
    /// </summary>
    public static bool RunHostIoFlow()
    {
        return HostInject.HostComputeAndInject(
            hostCompute: HostComputeSeed,
            applyOnAllPeers: ApplyHostComputedSeed);
    }
}

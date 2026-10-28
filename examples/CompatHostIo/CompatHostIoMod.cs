using System.Reflection;
using RimworldMP.API;

namespace RimworldMP.Examples.CompatHostIo;

/// <summary>
/// Host-only I/O pattern: host computes a result, then all peers apply via a registered sync method.
/// </summary>
public static class CompatHostIoMod
{
    public static void Initialize()
    {
        MP.RegisterSyncMethod(typeof(CompatHostIoMod).GetMethod(nameof(ApplyHostComputedSeed), BindingFlags.Public | BindingFlags.Static)!);
    }

    public static int HostComputeSeed()
    {
        // Host-only nondeterministic work belongs here (HTTP, disk, etc.).
        return Random.Shared.Next();
    }

    public static void ApplyHostComputedSeed(int seed)
    {
        // Deterministic apply on every peer.
        _ = seed;
        if (MP.IsInSession)
            MP.ExecuteCommand(opcode: 1, payload: BitConverter.GetBytes(seed)); // EmptyBatch-ish demo only
    }
}

#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.Core.Session;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Lockstep tick gate: host emits the next batch, then every peer stalls until that batch is present.
/// When frozen (desync), the sim does not advance.
/// </summary>
/// <remarks>UNVERIFIED (RW 1.7): TickManager.DoSingleTick still the per-tick entry under Waylith.</remarks>
[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
internal static class TickManager_DoSingleTick_Patch
{
    private static bool Prefix()
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true; // offline / no session — vanilla

        if (session.IsFrozen)
            return false;

        if (session.IsHost)
            session.HostEmitNextBatch();

        if (!session.TryAdvanceSim())
            return false; // stall until batch arrives

        return true;
    }

    private static void Postfix()
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return;
        // Batch apply hooks run inside TryAdvanceSim via OnBatchApply (wired in mod entry).
        _ = session;
    }
}
#endif

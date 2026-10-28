#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Placeholder tick hook for future command-batch gating. Phase 0: no behavior change.
/// </summary>
[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
internal static class TickManager_DoSingleTick_Patch
{
    // Intentionally empty — Prefix/Postfix arrive with Phase 1 lockstep.
}
#endif

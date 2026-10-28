#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using Verse;
using Verse.AI;

namespace RimworldMP.Patches;

/// <summary>
/// Ordered job gives → lockstep JobGive commands with map id + thickened target payload.
/// Patches Pawn_JobTracker.TryTakeOrderedJob; applicator re-enters with CommandApplyReentry set.
/// </summary>
[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
internal static class Pawn_JobTracker_TryTakeOrderedJob_Patch
{
    private static bool Prefix(Pawn_JobTracker __instance, Job job, ref bool __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;
        if (CommandApplyReentry.IsApplying)
            return true;
        if (job?.def is null)
            return true;

        var pawn = __instance.pawn;
        if (pawn is null)
            return true;

        var target = job.targetA;
        var cell = target.IsValid ? target.Cell : pawn.Position;
        var mapId = pawn.Map?.uniqueID ?? 0;
        var targetThingId = target.HasThing ? target.Thing.thingIDNumber : -1;
        var count = job.count;

        session.SubmitCommand(
            CommandOpcode.JobGive,
            CommandPayloads.JobGive(
                mapId,
                pawn.thingIDNumber,
                cell.x,
                cell.z,
                job.def.defName,
                targetThingId,
                count,
                queue: 0));

        // Optimistic UI accept; sim commits only when the shared batch applies.
        __result = true;
        return false;
    }
}
#endif

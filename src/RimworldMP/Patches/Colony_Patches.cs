#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimWorld;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Phase 2 colony command capture: bills, research, trade accept, caravan form (subset).
/// Payloads always carry map/thing ids — never rely on Find.CurrentMap at apply time.
/// </summary>
[HarmonyPatch]
internal static class Colony_Patches
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject))]
    [HarmonyPrefix]
    private static bool Research_SetCurrentProject_Prefix(ResearchProjectDef project)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        if (project is null)
            return true;
        var mapId = Find.CurrentMap?.uniqueID ?? 0;
        session.SubmitCommand(CommandOpcode.ResearchStart, CommandPayloads.ResearchStart(mapId, project.defName));
        return false;
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
    [HarmonyPrefix]
    private static bool BillStack_AddBill_Prefix(BillStack __instance, Bill bill)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        if (bill?.recipe is null)
            return true;

        var thing = __instance.billGiver as Thing;
        var benchId = thing?.thingIDNumber ?? -1;
        var mapId = thing?.Map?.uniqueID ?? Find.CurrentMap?.uniqueID ?? -1;
        var slot = __instance.Count;
        byte mode = 0;
        var count = 1;
        if (bill is Bill_Production prod)
        {
            mode = prod.repeatMode == BillRepeatModeDefOf.TargetCount ? (byte)1
                : prod.repeatMode == BillRepeatModeDefOf.Forever ? (byte)2 : (byte)0;
            count = prod.repeatCount;
        }

        session.SubmitCommand(
            CommandOpcode.BillSet,
            CommandPayloads.BillSet(mapId, benchId, slot, bill.recipe.defName, mode, count));
        return false;
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.Delete))]
    [HarmonyPrefix]
    private static bool BillStack_Delete_Prefix(BillStack __instance, Bill bill)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        var thing = __instance.billGiver as Thing;
        var benchId = thing?.thingIDNumber ?? -1;
        var mapId = thing?.Map?.uniqueID ?? Find.CurrentMap?.uniqueID ?? -1;
        var slot = __instance.IndexOf(bill);
        if (slot < 0)
            return true;
        session.SubmitCommand(CommandOpcode.BillDelete, CommandPayloads.BillDelete(mapId, benchId, slot));
        return false;
    }
}
#endif

#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimWorld;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Basic zone mutations → ZoneAdd / ZoneRemove / ZoneClear commands with explicit map id.
/// Narrow hooks on Zone.AddCell / Zone.RemoveCell; full Designator coverage is later.
/// </summary>
[HarmonyPatch]
internal static class Zone_Patches
{
    [HarmonyPatch(typeof(Zone), nameof(Zone.AddCell))]
    [HarmonyPrefix]
    private static bool AddCell_Prefix(Zone __instance, IntVec3 c)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;
        if (CommandApplyReentry.IsApplying)
            return true;

        var mapId = __instance.Map?.uniqueID ?? 0;
        session.SubmitCommand(
            CommandOpcode.ZoneAdd,
            CommandPayloads.ZoneCells(ZoneKind(__instance), mapId, new[] { c.x, c.z }));
        return false;
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.RemoveCell))]
    [HarmonyPrefix]
    private static bool RemoveCell_Prefix(Zone __instance, IntVec3 c)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;
        if (CommandApplyReentry.IsApplying)
            return true;

        var mapId = __instance.Map?.uniqueID ?? 0;
        session.SubmitCommand(
            CommandOpcode.ZoneRemove,
            CommandPayloads.ZoneCells(ZoneKind(__instance), mapId, new[] { c.x, c.z }));
        return false;
    }

    private static byte ZoneKind(Zone zone)
    {
        return zone switch
        {
            Zone_Stockpile => 1,
            Zone_Growing => 2,
            _ => 0,
        };
    }
}
#endif

#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimWorld;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Policy capture for work priorities + timetable (confirmed 1.7 setters).
/// Outfit / food / drug / medical / forbid remain apply-path first where capture is ambiguous.
/// </summary>
[HarmonyPatch]
internal static class Policy_Patches
{
    private static readonly AccessTools.FieldRef<Pawn_WorkSettings, Pawn> WorkPawn =
        AccessTools.FieldRefAccess<Pawn_WorkSettings, Pawn>("pawn");

    private static readonly AccessTools.FieldRef<Pawn_TimetableTracker, Pawn> TimetablePawn =
        AccessTools.FieldRefAccess<Pawn_TimetableTracker, Pawn>("pawn");

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority))]
    [HarmonyPrefix]
    private static bool WorkSettings_SetPriority_Prefix(Pawn_WorkSettings __instance, WorkTypeDef w, int priority)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        if (w is null) return true;
        var pawn = WorkPawn(__instance);
        if (pawn is null) return true;
        var prio = (byte)Math.Clamp(priority, 0, 255);
        session.SubmitCommand(
            CommandOpcode.WorkPrioritySet,
            CommandPayloads.WorkPrioritySet(pawn.thingIDNumber, w.defName, prio));
        return false;
    }

    [HarmonyPatch(typeof(Pawn_TimetableTracker), nameof(Pawn_TimetableTracker.SetAssignment))]
    [HarmonyPrefix]
    private static bool Timetable_SetAssignment_Prefix(
        Pawn_TimetableTracker __instance,
        int hour,
        TimeAssignmentDef ta)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        var pawn = TimetablePawn(__instance);
        if (pawn is null || ta is null) return true;

        // Emit a full 24-hour snapshot with the pending hour applied (opcode carries all hours).
        var hours = new byte[24];
        for (var h = 0; h < 24; h++)
        {
            var a = h == hour ? ta : __instance.GetAssignment(h);
            hours[h] = EncodeTimeAssignment(a);
        }

        session.SubmitCommand(CommandOpcode.TimetableSet, CommandPayloads.TimetableSet(pawn.thingIDNumber, hours));
        return false;
    }

    private static byte EncodeTimeAssignment(TimeAssignmentDef? def)
    {
        if (def is null) return 0;
        if (def == TimeAssignmentDefOf.Anything) return 0;
        if (def == TimeAssignmentDefOf.Work) return 1;
        if (def == TimeAssignmentDefOf.Joy) return 2;
        if (def == TimeAssignmentDefOf.Sleep) return 3;
        if (def == TimeAssignmentDefOf.Meditate) return 4;
        return 0;
    }
}
#endif

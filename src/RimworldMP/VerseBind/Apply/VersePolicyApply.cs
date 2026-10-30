using RimworldMP.Core.Commands;
using RimworldMP.Core.World;

#if !RIMWORLDMP_NO_GAME_REFS
using RimWorld;
using Verse;
#endif

namespace RimworldMP.VerseBind.Apply;

/// <summary>
/// Policy / designator / forbid apply. Offline: <see cref="PolicyWorld"/> only.
/// Live Verse: work priority + timetable confirmed on 1.7; other policy opcodes stay shadow-only.
/// </summary>
public static class VersePolicyApply
{
    public static void Apply(MpCommand cmd)
    {
        PolicyWorld.Current.ApplyCommand(cmd);
#if !RIMWORLDMP_NO_GAME_REFS
        switch (cmd.Opcode)
        {
            case CommandOpcode.WorkPrioritySet:
                ApplyWorkPriority(cmd.Payload);
                break;
            case CommandOpcode.TimetableSet:
                ApplyTimetable(cmd.Payload);
                break;
            // Food / outfit / drug / medical / hostility / designator / forbid: shadow until capture complete.
        }
#else
        _ = cmd;
#endif
    }

#if !RIMWORLDMP_NO_GAME_REFS
    private static void ApplyWorkPriority(byte[] payload)
    {
        var (pawnId, workType, priority) = CommandPayloads.ReadWorkPrioritySet(payload);
        var pawn = VerseThingLookup.FindPawn(pawnId);
        var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType);
        if (pawn?.workSettings is null || def is null) return;
        pawn.workSettings.SetPriority(def, priority);
    }

    private static void ApplyTimetable(byte[] payload)
    {
        var (pawnId, hours) = CommandPayloads.ReadTimetableSet(payload);
        var pawn = VerseThingLookup.FindPawn(pawnId);
        if (pawn?.timetable is null || hours.Length < 24) return;
        for (var h = 0; h < 24; h++)
            pawn.timetable.SetAssignment(h, DecodeTimeAssignment(hours[h]));
    }

    private static TimeAssignmentDef DecodeTimeAssignment(byte code) => code switch
    {
        1 => TimeAssignmentDefOf.Work,
        2 => TimeAssignmentDefOf.Joy,
        3 => TimeAssignmentDefOf.Sleep,
        4 => TimeAssignmentDefOf.Meditate,
        _ => TimeAssignmentDefOf.Anything,
    };
#endif
}

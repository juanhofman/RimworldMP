using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;

#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP.CommandApply;

/// <summary>
/// Applies admitted lockstep commands to the local sim.
/// Without game refs this is a no-op recorder; with refs it mutates Verse state.
/// </summary>
public static class CommandApplicator
{
    public static void Apply(CommandBatch batch)
    {
        foreach (var cmd in batch.Commands)
            ApplyOne(cmd);
    }

    public static void ApplyOne(MpCommand cmd)
    {
        using var _ = CommandApplyReentry.Rent();
        switch (cmd.Opcode)
        {
            case CommandOpcode.Pause:
                ApplyPause(CommandPayloads.ReadPause(cmd.Payload));
                break;
            case CommandOpcode.Speed:
                ApplySpeed(CommandPayloads.ReadSpeed(cmd.Payload));
                break;
            case CommandOpcode.Draft:
            case CommandOpcode.Undraft:
            {
                var (pawnId, drafted) = CommandPayloads.ReadDraft(cmd.Payload);
                ApplyDraft(pawnId, drafted);
                break;
            }
            case CommandOpcode.JobGive:
            {
                var (pawnId, x, z, job) = CommandPayloads.ReadJobGive(cmd.Payload);
                ApplyJobGive(pawnId, x, z, job);
                break;
            }
            case CommandOpcode.ZoneAdd:
            case CommandOpcode.ZoneRemove:
            case CommandOpcode.ZoneClear:
            {
                var (kind, cells) = CommandPayloads.ReadZoneCells(cmd.Payload);
                ApplyZone(cmd.Opcode, kind, cells);
                break;
            }
            case CommandOpcode.RestrictAreaSet:
            {
                var (pawnId, areaId) = CommandPayloads.ReadRestrictArea(cmd.Payload);
                ApplyRestrictArea(pawnId, areaId);
                break;
            }
        }
    }

    private static void ApplyPause(bool paused)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var tm = Find.TickManager;
        if (tm is null)
            return;
        if (paused)
            tm.CurTimeSpeed = TimeSpeed.Paused;
        else if (tm.CurTimeSpeed == TimeSpeed.Paused)
            tm.CurTimeSpeed = TimeSpeed.Normal;
#else
        _ = paused;
#endif
    }

    private static void ApplySpeed(byte speedIndex)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var tm = Find.TickManager;
        if (tm is null)
            return;
        tm.CurTimeSpeed = speedIndex switch
        {
            0 => TimeSpeed.Paused,
            1 => TimeSpeed.Normal,
            2 => TimeSpeed.Fast,
            3 => TimeSpeed.Superfast,
            4 => TimeSpeed.Ultrafast,
            _ => TimeSpeed.Normal,
        };
#else
        _ = speedIndex;
#endif
    }

    private static void ApplyDraft(int pawnLoadId, bool drafted)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var pawn = FindPawn(pawnLoadId);
        if (pawn?.drafter is null)
            return;
        // Direct set bypasses UI patches that would re-enqueue commands.
        pawn.drafter.Drafted = drafted;
#else
        _ = (pawnLoadId, drafted);
#endif
    }

    private static void ApplyJobGive(int pawnLoadId, int cellX, int cellZ, string jobDefName)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var pawn = FindPawn(pawnLoadId);
        if (pawn is null || pawn.Map is null)
            return;
        var def = DefDatabase<JobDef>.GetNamedSilentFail(jobDefName);
        if (def is null)
            return;
        var cell = new IntVec3(cellX, 0, cellZ);
        var job = JobMaker.MakeJob(def, cell);
        pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
#else
        _ = (pawnLoadId, cellX, cellZ, jobDefName);
#endif
    }

    private static void ApplyZone(CommandOpcode op, byte zoneKind, int[] cellsPacked)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        // Phase 1 stub: log only — full zone mutation needs Designator/ZoneManager wiring per zoneKind.
        Log.Message($"[RimworldMP] Zone cmd {op} kind={zoneKind} cells={cellsPacked.Length / 2} (apply stub)");
#else
        _ = (op, zoneKind, cellsPacked);
#endif
    }

    private static void ApplyRestrictArea(int pawnLoadId, int areaId)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var pawn = FindPawn(pawnLoadId);
        if (pawn?.playerSettings is null || pawn.Map is null)
            return;
        Area? area = null;
        if (areaId >= 0)
        {
            foreach (var a in pawn.Map.areaManager.AllAreas)
            {
                if (a.ID == areaId)
                {
                    area = a;
                    break;
                }
            }
        }

        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
#else
        _ = (pawnLoadId, areaId);
#endif
    }

#if !RIMWORLDMP_NO_GAME_REFS
    private static Pawn? FindPawn(int loadId)
    {
        // Prefer thingIDNumber match across maps.
        foreach (var map in Find.Maps)
        {
            foreach (var pawn in map.mapPawns.AllPawns)
            {
                if (pawn.thingIDNumber == loadId)
                    return pawn;
            }
        }

        return null;
    }
#endif
}

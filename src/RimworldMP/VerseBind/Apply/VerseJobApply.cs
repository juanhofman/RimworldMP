using RimworldMP.Core.Commands;

#if !RIMWORLDMP_NO_GAME_REFS
using RimWorld;
using Verse;
using Verse.AI;
#endif

namespace RimworldMP.VerseBind.Apply;

/// <summary>JobGive apply via JobMaker + TryTakeOrderedJob.</summary>
public static class VerseJobApply
{
    public static void Apply(CommandPayloads.JobGivePayload job)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        // UNVERIFIED (RW 1.7): JobMaker.MakeJob overloads, Job.count, TryTakeOrderedJob(Job, JobTag).
        var pawn = VerseThingLookup.FindPawn(job.PawnLoadId);
        if (pawn is null) return;
        var map = VerseThingLookup.FindMap(job.MapId) ?? pawn.Map;
        if (map is null) return;
        var def = DefDatabase<JobDef>.GetNamedSilentFail(job.JobDefName);
        if (def is null) return;

        Job made;
        if (job.TargetThingId >= 0)
        {
            var thing = VerseThingLookup.FindThingOnMap(map, job.TargetThingId)
                ?? VerseThingLookup.FindThing(job.TargetThingId);
            made = thing is not null
                ? JobMaker.MakeJob(def, thing)
                : JobMaker.MakeJob(def, new IntVec3(job.CellX, 0, job.CellZ));
        }
        else
        {
            made = JobMaker.MakeJob(def, new IntVec3(job.CellX, 0, job.CellZ));
        }

        if (job.Count >= 0)
            made.count = job.Count;

        // queue flag reserved until RIMWORLD_DIR validates RequestQueueJob / overload.
        pawn.jobs?.TryTakeOrderedJob(made, JobTag.Misc);
#else
        _ = job;
#endif
    }
}

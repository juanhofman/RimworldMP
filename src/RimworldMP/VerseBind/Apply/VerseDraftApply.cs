#if !RIMWORLDMP_NO_GAME_REFS
using RimWorld;
using Verse;
#endif

namespace RimworldMP.VerseBind.Apply;

/// <summary>Draft / undraft apply via <c>Pawn_DraftController.Drafted</c>.</summary>
public static class VerseDraftApply
{
    public static void Apply(int pawnLoadId, bool drafted)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        // UNVERIFIED (RW 1.7): drafter field + Drafted setter on Pawn_DraftController.
        var pawn = VerseThingLookup.FindPawn(pawnLoadId);
        if (pawn?.drafter is null) return;
        pawn.drafter.Drafted = drafted;
#else
        _ = (pawnLoadId, drafted);
#endif
    }
}

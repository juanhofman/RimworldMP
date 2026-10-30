#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP.VerseBind.Apply;

/// <summary>Allowed-area restriction apply.</summary>
public static class VerseRestrictAreaApply
{
    public static void Apply(int pawnLoadId, int areaId)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        // UNVERIFIED (RW 1.7): AreaRestrictionInPawnCurrentMap setter + Area.ID.
        var pawn = VerseThingLookup.FindPawn(pawnLoadId);
        if (pawn?.playerSettings is null || pawn.Map is null) return;
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
}

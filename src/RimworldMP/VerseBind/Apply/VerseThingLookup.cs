#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP.VerseBind.Apply;

/// <summary>
/// Map / pawn / thing resolution for applicators. Game-ref only;
/// NO_GAME_REFS builds never call these (shadow stores own offline state).
/// </summary>
internal static class VerseThingLookup
{
#if !RIMWORLDMP_NO_GAME_REFS
    // UNVERIFIED (RW 1.7): Map.uniqueID / Thing.thingIDNumber / mapPawns.AllPawns / listerThings.AllThings.
    public static Map? FindMap(int mapId)
    {
        foreach (var map in Find.Maps)
        {
            if (map.uniqueID == mapId)
                return map;
        }

        return null;
    }

    public static Pawn? FindPawn(int loadId)
    {
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

    public static Thing? FindThing(int id)
    {
        foreach (var map in Find.Maps)
        {
            var t = FindThingOnMap(map, id);
            if (t is not null)
                return t;
        }

        return null;
    }

    public static Thing? FindThingOnMap(Map map, int id)
    {
        foreach (var t in map.listerThings.AllThings)
        {
            if (t.thingIDNumber == id)
                return t;
        }

        return null;
    }
#endif
}

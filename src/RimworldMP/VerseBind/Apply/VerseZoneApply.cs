using RimworldMP.Core.Commands;
using RimworldMP.Core.World;

#if !RIMWORLDMP_NO_GAME_REFS
using RimWorld;
using Verse;
#endif

namespace RimworldMP.VerseBind.Apply;

/// <summary>Zone cell mutations — shadow store always; Verse when game refs exist.</summary>
public static class VerseZoneApply
{
    public static void Apply(CommandOpcode op, byte zoneKind, int mapId, int[] cellsPacked)
    {
        ZoneWorld.Current.ApplyOpcode(op, zoneKind, mapId, cellsPacked);
#if !RIMWORLDMP_NO_GAME_REFS
        // UNVERIFIED (RW 1.7): zoneManager.AllZones / RegisterZone / Zone_Stockpile / Zone_Growing / AddCell.
        var map = VerseThingLookup.FindMap(mapId);
        if (map?.zoneManager is null) return;
        Zone? zone = null;
        foreach (var z in map.zoneManager.AllZones)
        {
            if (ZoneKind(z) == zoneKind)
            {
                zone = z;
                break;
            }
        }

        if (zone is null && op == CommandOpcode.ZoneAdd)
        {
            zone = zoneKind switch
            {
                1 => new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager),
                2 => new Zone_Growing(map.zoneManager),
                _ => null,
            };
            if (zone is not null)
                map.zoneManager.RegisterZone(zone);
        }

        if (zone is null) return;

        if (op == CommandOpcode.ZoneClear)
        {
            zone.Delete();
            return;
        }

        for (var i = 0; i + 1 < cellsPacked.Length; i += 2)
        {
            var cell = new IntVec3(cellsPacked[i], 0, cellsPacked[i + 1]);
            if (op == CommandOpcode.ZoneAdd)
                zone.AddCell(cell);
            else if (op == CommandOpcode.ZoneRemove)
                zone.RemoveCell(cell);
        }
#endif
    }

#if !RIMWORLDMP_NO_GAME_REFS
    private static byte ZoneKind(Zone zone) => zone switch
    {
        Zone_Stockpile => 1,
        Zone_Growing => 2,
        _ => 0,
    };
#endif
}

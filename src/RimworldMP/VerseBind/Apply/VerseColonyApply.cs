using RimworldMP.Core.Commands;

#if !RIMWORLDMP_NO_GAME_REFS
using RimWorld;
using RimWorld.Planet;
using Verse;
#endif

namespace RimworldMP.VerseBind.Apply;

/// <summary>
/// Bills / research / trade / caravan Verse commit shapes.
/// Trade deal commit and caravan formation stay scaffolded until install validates APIs —
/// prefer incomplete stubs over inventing wrong Harmony capture Prefixes.
/// </summary>
public static class VerseColonyApply
{
    public static void Apply(MpCommand cmd)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        switch (cmd.Opcode)
        {
            case CommandOpcode.ResearchStart:
                ApplyResearchStart(cmd.Payload);
                break;
            case CommandOpcode.ResearchStop:
                ApplyResearchStop();
                break;
            case CommandOpcode.BillSet:
                ApplyBillSet(cmd.Payload);
                break;
            case CommandOpcode.BillDelete:
                ApplyBillDelete(cmd.Payload);
                break;
            case CommandOpcode.BillReorder:
                ApplyBillReorder(cmd.Payload);
                break;
            case CommandOpcode.TradeAccept:
                ApplyTradeAccept(cmd.Payload);
                break;
            case CommandOpcode.TradeCancel:
                // SCAFFOLD: no Verse cancel path until TradeDeal / session API confirmed.
                break;
            case CommandOpcode.CaravanForm:
                ApplyCaravanForm(cmd.Payload);
                break;
            case CommandOpcode.CaravanAddPawn:
            case CommandOpcode.CaravanRemovePawn:
            case CommandOpcode.CaravanDisband:
                // SCAFFOLD: Caravan / Dialog_FormCaravan APIs need RIMWORLD_DIR.
                break;
        }
#else
        // Shadow path already applied via ColonyWorld; payload decode is authoritative offline.
        _ = cmd;
#endif
    }

#if !RIMWORLDMP_NO_GAME_REFS
    private static void ApplyResearchStart(byte[] payload)
    {
        // UNVERIFIED (RW 1.7): ResearchManager.SetCurrentProject(ResearchProjectDef).
        var (_, name) = CommandPayloads.ReadResearchStart(payload);
        var project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
        if (project is not null)
            Find.ResearchManager.SetCurrentProject(project);
    }

    private static void ApplyResearchStop()
    {
        // Confirmed 1.7: ResearchManager.StopProject(ResearchProjectDef); currentProj is private.
        var rm = Find.ResearchManager;
        if (rm is null) return;
        foreach (var proj in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
        {
            if (rm.IsCurrentProject(proj))
            {
                rm.StopProject(proj);
                return;
            }
        }
    }

    private static void ApplyBillSet(byte[] payload)
    {
        // UNVERIFIED (RW 1.7): Building_WorkTable, BillStack, BillUtility.MakeNewBill, Bill_Production.
        var bill = CommandPayloads.ReadBillSet(payload);
        var map = VerseThingLookup.FindMap(bill.MapId);
        var bench = (map is not null
            ? VerseThingLookup.FindThingOnMap(map, bill.BenchThingId)
            : VerseThingLookup.FindThing(bill.BenchThingId)) as Building_WorkTable;
        var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(bill.RecipeDef);
        if (bench?.BillStack is null || recipe is null) return;

        while (bench.BillStack.Count <= bill.BillSlot)
            bench.BillStack.AddBill(BillUtility.MakeNewBill(recipe));
        if (bill.BillSlot >= 0 && bill.BillSlot < bench.BillStack.Count)
        {
            var existing = bench.BillStack[bill.BillSlot];
            if (existing is Bill_Production prod)
            {
                prod.repeatMode = bill.RepeatMode switch
                {
                    1 => BillRepeatModeDefOf.TargetCount,
                    2 => BillRepeatModeDefOf.Forever,
                    _ => BillRepeatModeDefOf.RepeatCount,
                };
                prod.repeatCount = bill.Count;
            }
        }
    }

    private static void ApplyBillDelete(byte[] payload)
    {
        // UNVERIFIED (RW 1.7): BillStack.Delete(Bill).
        var (mapId, benchId, slot) = CommandPayloads.ReadBillDelete(payload);
        var map = VerseThingLookup.FindMap(mapId);
        var bench = (map is not null
            ? VerseThingLookup.FindThingOnMap(map, benchId)
            : VerseThingLookup.FindThing(benchId)) as Building_WorkTable;
        if (bench?.BillStack is null) return;
        if (slot >= 0 && slot < bench.BillStack.Count)
            bench.BillStack.Delete(bench.BillStack[slot]);
    }

    private static void ApplyBillReorder(byte[] payload)
    {
        var (mapId, benchId, fromSlot, toSlot) = CommandPayloads.ReadBillReorder(payload);
        var map = VerseThingLookup.FindMap(mapId);
        var bench = (map is not null
            ? VerseThingLookup.FindThingOnMap(map, benchId)
            : VerseThingLookup.FindThing(benchId)) as Building_WorkTable;
        if (bench?.BillStack is null) return;
        if (fromSlot < 0 || fromSlot >= bench.BillStack.Count) return;
        var offset = toSlot - fromSlot;
        if (offset == 0) return;
        bench.BillStack.Reorder(bench.BillStack[fromSlot], offset);
    }

    private static void ApplyTradeAccept(byte[] payload)
    {
        // Confirmed 1.7: TradeDeal.TryExecute(out bool). Prefer active TradeSession.deal.
        var (mapId, traderId, fingerprint) = CommandPayloads.ReadTradeAccept(payload);
        _ = VerseThingLookup.FindMap(mapId);
        _ = VerseThingLookup.FindThing(traderId);
        if (TradeSession.Active && TradeSession.deal is not null)
        {
            _ = TradeSession.deal.TryExecute(out _);
            return;
        }

        Log.Warning(
            $"[RimworldMP] TradeAccept map={mapId} trader={traderId} fingerprint={fingerprint}: no active TradeSession.deal");
    }

    private static void ApplyCaravanForm(byte[] payload)
    {
        // Confirmed 1.7: CaravanMaker.MakeCaravan(pawns, faction, PlanetTile, addToWorldPawns).
        var (mapId, tileId, pawnIds) = CommandPayloads.ReadCaravanForm(payload);
        _ = VerseThingLookup.FindMap(mapId);
        var pawns = new List<Pawn>(pawnIds.Length);
        foreach (var id in pawnIds)
        {
            var p = VerseThingLookup.FindPawn(id);
            if (p is not null)
                pawns.Add(p);
        }

        if (pawns.Count == 0)
        {
            Log.Warning($"[RimworldMP] CaravanForm map={mapId} tile={tileId}: no pawns resolved");
            return;
        }

        var faction = pawns[0].Faction ?? Faction.OfPlayer;
        var tile = new PlanetTile(tileId);
        _ = CaravanMaker.MakeCaravan(pawns, faction, tile, addToWorldPawnsIfNotAlready: true);
    }
#endif
}

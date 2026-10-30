using RimworldMP.Core.Commands;

#if !RIMWORLDMP_NO_GAME_REFS
using RimWorld;
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
                // SCAFFOLD: confirm ResearchManager.Stop / ClearCurrentProject (or equivalent) on 1.7.
                ApplyResearchStop();
                break;
            case CommandOpcode.BillSet:
                ApplyBillSet(cmd.Payload);
                break;
            case CommandOpcode.BillDelete:
                ApplyBillDelete(cmd.Payload);
                break;
            case CommandOpcode.BillReorder:
                // SCAFFOLD: BillStack reorder API not confirmed — shadow store only until install.
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
        // SCAFFOLD / UNVERIFIED: older community sources used currentProj = null; confirm 1.7 API.
        // Do not invent a confident wrong call — leave no-op until install.
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

    private static void ApplyTradeAccept(byte[] payload)
    {
        // SCAFFOLD: Deal commit needs live TradeDeal / Verse trader APIs — do not invent Prefix.
        var (mapId, traderId, fingerprint) = CommandPayloads.ReadTradeAccept(payload);
        _ = VerseThingLookup.FindMap(mapId);
        _ = VerseThingLookup.FindThing(traderId);
        Log.Message(
            $"[RimworldMP] TradeAccept map={mapId} trader={traderId} fingerprint={fingerprint} (deal commit stub)");
    }

    private static void ApplyCaravanForm(byte[] payload)
    {
        // SCAFFOLD: Caravan formation needs Dialog_FormCaravan / CaravanMaker — do not invent Prefix.
        var (mapId, tile, pawns) = CommandPayloads.ReadCaravanForm(payload);
        _ = VerseThingLookup.FindMap(mapId);
        Log.Message($"[RimworldMP] CaravanForm map={mapId} tile={tile} pawns={pawns.Length}");
    }
#endif
}

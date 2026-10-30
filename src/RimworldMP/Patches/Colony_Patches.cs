#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Phase 2 colony command capture: bills + research + trade accept + caravan form.
/// Payloads always carry map/thing ids — never rely on Find.CurrentMap at apply time.
/// </summary>
/// <remarks>
/// Confirmed on RW 1.7.4990: SetCurrentProject / StopProject, BillStack.AddBill/Delete/Reorder,
/// TradeDeal.TryExecute, CaravanMaker.MakeCaravan.
/// </remarks>
[HarmonyPatch]
internal static class Colony_Patches
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject))]
    [HarmonyPrefix]
    private static bool Research_SetCurrentProject_Prefix(ResearchProjectDef proj)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        if (proj is null)
            return true;
        var mapId = Find.CurrentMap?.uniqueID ?? 0;
        session.SubmitCommand(CommandOpcode.ResearchStart, CommandPayloads.ResearchStart(mapId, proj.defName));
        return false;
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.StopProject))]
    [HarmonyPrefix]
    private static bool Research_StopProject_Prefix(ResearchProjectDef proj)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        _ = proj;
        var mapId = Find.CurrentMap?.uniqueID ?? 0;
        session.SubmitCommand(CommandOpcode.ResearchStop, CommandPayloads.ResearchStop(mapId));
        return false;
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
    [HarmonyPrefix]
    private static bool BillStack_AddBill_Prefix(BillStack __instance, Bill bill)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        if (bill?.recipe is null)
            return true;

        var thing = __instance.billGiver as Thing;
        var benchId = thing?.thingIDNumber ?? -1;
        var mapId = thing?.Map?.uniqueID ?? Find.CurrentMap?.uniqueID ?? -1;
        var slot = __instance.Count;
        byte mode = 0;
        var count = 1;
        if (bill is Bill_Production prod)
        {
            mode = prod.repeatMode == BillRepeatModeDefOf.TargetCount ? (byte)1
                : prod.repeatMode == BillRepeatModeDefOf.Forever ? (byte)2 : (byte)0;
            count = prod.repeatCount;
        }

        session.SubmitCommand(
            CommandOpcode.BillSet,
            CommandPayloads.BillSet(mapId, benchId, slot, bill.recipe.defName, mode, count));
        return false;
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.Delete))]
    [HarmonyPrefix]
    private static bool BillStack_Delete_Prefix(BillStack __instance, Bill bill)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        var thing = __instance.billGiver as Thing;
        var benchId = thing?.thingIDNumber ?? -1;
        var mapId = thing?.Map?.uniqueID ?? Find.CurrentMap?.uniqueID ?? -1;
        var slot = __instance.IndexOf(bill);
        if (slot < 0)
            return true;
        session.SubmitCommand(CommandOpcode.BillDelete, CommandPayloads.BillDelete(mapId, benchId, slot));
        return false;
    }

    [HarmonyPatch(typeof(BillStack), nameof(BillStack.Reorder))]
    [HarmonyPrefix]
    private static bool BillStack_Reorder_Prefix(BillStack __instance, Bill bill, int offset)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        var thing = __instance.billGiver as Thing;
        var benchId = thing?.thingIDNumber ?? -1;
        var mapId = thing?.Map?.uniqueID ?? Find.CurrentMap?.uniqueID ?? -1;
        var from = __instance.IndexOf(bill);
        if (from < 0)
            return true;
        var to = from + offset;
        session.SubmitCommand(CommandOpcode.BillReorder, CommandPayloads.BillReorder(mapId, benchId, from, to));
        return false;
    }

    [HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
    [HarmonyPrefix]
    private static bool TradeDeal_TryExecute_Prefix(ref bool __result, ref bool actuallyTraded)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;

        // ITrader is not always a Thing (settlement traders); prefer Thing id, else price-seed.
        var traderThing = TradeSession.trader as Thing;
        var mapId = traderThing?.Map?.uniqueID ?? Find.CurrentMap?.uniqueID ?? 0;
        var traderId = traderThing?.thingIDNumber
                       ?? TradeSession.trader?.RandomPriceFactorSeed
                       ?? -1;
        var fingerprint = ((long)(TradeSession.playerNegotiator?.thingIDNumber ?? 0) << 32)
                          ^ (uint)traderId;
        session.SubmitCommand(CommandOpcode.TradeAccept, CommandPayloads.TradeAccept(mapId, traderId, fingerprint));
        actuallyTraded = true;
        __result = true;
        return false;
    }

    [HarmonyPatch(typeof(CaravanMaker), nameof(CaravanMaker.MakeCaravan))]
    [HarmonyPrefix]
    private static bool CaravanMaker_MakeCaravan_Prefix(
        IEnumerable<Pawn> pawns,
        Faction faction,
        PlanetTile startingTile,
        bool addToWorldPawnsIfNotAlready,
        ref Caravan __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return true;
        _ = (faction, addToWorldPawnsIfNotAlready);

        var mapId = Find.CurrentMap?.uniqueID ?? 0;
        var tile = startingTile.tileId;
        var ids = pawns.Select(static p => p.thingIDNumber).ToArray();
        session.SubmitCommand(CommandOpcode.CaravanForm, CommandPayloads.CaravanForm(mapId, tile, ids));
        __result = null!;
        return false;
    }
}
#endif

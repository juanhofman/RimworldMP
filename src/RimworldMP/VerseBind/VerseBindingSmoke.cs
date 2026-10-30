using RimworldMP.Core.Commands;
using RimworldMP.Core.Join;
using RimworldMP.Join;
using RimworldMP.UI;
using RimworldMP.VerseBind.Apply;

namespace RimworldMP.VerseBind;

/// <summary>
/// Offline smoke for binding stubs: every registered apply/UI entry that is safe under
/// <c>RIMWORLDMP_NO_GAME_REFS</c> must run without throwing.
/// </summary>
public static class VerseBindingSmoke
{
    /// <summary>
    /// Invoke NO_GAME_REFS-safe stub surfaces. Does not call <see cref="VerseSaveLoad"/> capture/load
    /// (those fail loud by design when game refs are absent).
    /// </summary>
    public static void RunNoThrowSmoke()
    {
        _ = VerseBindingRegistry.IsGameRefsCompiled;
        _ = VerseBindingRegistry.All.Count;

        VerseTickApply.ApplyPause(paused: true);
        VerseTickApply.ApplyPause(paused: false);
        VerseTickApply.ApplySpeed(0);
        VerseTickApply.ApplySpeed(1);
        VerseTickApply.ApplySpeed(4);

        VerseDraftApply.Apply(pawnLoadId: 1, drafted: true);
        VerseDraftApply.Apply(pawnLoadId: 1, drafted: false);

        VerseJobApply.Apply(new CommandPayloads.JobGivePayload(
            MapId: 0,
            PawnLoadId: 1,
            CellX: 0,
            CellZ: 0,
            JobDefName: "Goto",
            TargetThingId: -1,
            Count: -1,
            Queue: 0));

        VerseZoneApply.Apply(CommandOpcode.ZoneAdd, zoneKind: 1, mapId: 0, cellsPacked: [1, 2]);
        VerseZoneApply.Apply(CommandOpcode.ZoneRemove, zoneKind: 1, mapId: 0, cellsPacked: [1, 2]);
        VerseZoneApply.Apply(CommandOpcode.ZoneClear, zoneKind: 1, mapId: 0, cellsPacked: []);

        VerseRestrictAreaApply.Apply(pawnLoadId: 1, areaId: -1);

        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.ResearchStart, 1, 0, CommandPayloads.ResearchStart(0, "SmokeResearch")));
        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.ResearchStop, 1, 0, CommandPayloads.ResearchStop(0)));
        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.BillSet, 1, 0, CommandPayloads.BillSet(0, 1, 0, "Make_Smoke", 0, 1)));
        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.BillDelete, 1, 0, CommandPayloads.BillDelete(0, 1, 0)));
        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.BillReorder, 1, 0, CommandPayloads.BillReorder(0, 1, 0, 1)));
        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.TradeAccept, 1, 0, CommandPayloads.TradeAccept(0, 1, 0)));
        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.TradeCancel, 1, 0, CommandPayloads.TradeCancel(0, 1)));
        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.CaravanForm, 1, 0, CommandPayloads.CaravanForm(0, 0, [1])));
        VerseColonyApply.Apply(new MpCommand(
            CommandOpcode.CaravanDisband, 1, 0, CommandPayloads.CaravanDisband(1)));

        VersePolicyApply.Apply(new MpCommand(
            CommandOpcode.WorkPrioritySet, 1, 0, CommandPayloads.WorkPrioritySet(1, "Smoke", 1)));
        VersePolicyApply.Apply(new MpCommand(
            CommandOpcode.ForbidSet, 1, 0, CommandPayloads.ForbidSet(0, 1, true)));

        LobbyUi.Open();
        _ = LobbyUi.IsVerseUiAvailable;

        // Factory returns null offline — must not throw.
        var load = GameLoadHooks.TryCreateDefault();
        _ = load;
        _ = GameLoad.Current;
    }
}

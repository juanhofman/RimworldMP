namespace RimworldMP.VerseBind;

/// <summary>
/// Catalog of every Verse / Harmony binding the install fill-in pass must confirm.
/// Always compiles under <c>RIMWORLDMP_NO_GAME_REFS</c> — no Verse types here.
/// </summary>
public static class VerseBindingRegistry
{
    /// <summary>True when this assembly was built with game assembly references.</summary>
    public static bool IsGameRefsCompiled =>
#if RIMWORLDMP_NO_GAME_REFS
        false;
#else
        true;
#endif

    /// <summary>Ids the harness requires to be present (stable contract).</summary>
    public static IReadOnlyList<string> RequiredIds { get; } =
    [
        VerseBindingIds.TickDoSingleTick,
        VerseBindingIds.TickTogglePaused,
        VerseBindingIds.TickCurTimeSpeedSetter,
        VerseBindingIds.ApplyPause,
        VerseBindingIds.ApplySpeed,
        VerseBindingIds.DraftSetter,
        VerseBindingIds.ApplyDraft,
        VerseBindingIds.JobTryTakeOrdered,
        VerseBindingIds.ApplyJobGive,
        VerseBindingIds.ZoneAddCell,
        VerseBindingIds.ZoneRemoveCell,
        VerseBindingIds.ApplyZone,
        VerseBindingIds.RestrictAreaSetter,
        VerseBindingIds.ApplyRestrictArea,
        VerseBindingIds.ResearchSetCurrent,
        VerseBindingIds.BillStackAdd,
        VerseBindingIds.BillStackDelete,
        VerseBindingIds.ApplyResearch,
        VerseBindingIds.ApplyBill,
        VerseBindingIds.ApplyTrade,
        VerseBindingIds.ApplyCaravan,
        VerseBindingIds.CaptureTrade,
        VerseBindingIds.CaptureCaravan,
        VerseBindingIds.CapturePolicy,
        VerseBindingIds.ApplyPolicy,
        VerseBindingIds.RandValue,
        VerseBindingIds.RandRangeInt,
        VerseBindingIds.RandRangeFloat,
        VerseBindingIds.RandChance,
        VerseBindingIds.RandBool,
        VerseBindingIds.LobbyUiRootOnGui,
        VerseBindingIds.LobbyUiOpen,
        VerseBindingIds.JipSaveLoad,
        VerseBindingIds.DlcLoadGame,
    ];

    public static IReadOnlyList<VerseBindingHook> All { get; } = Build();

    public static VerseBindingHook? Find(string id)
    {
        foreach (var hook in All)
        {
            if (string.Equals(hook.Id, id, StringComparison.Ordinal))
                return hook;
        }

        return null;
    }

    public static IEnumerable<VerseBindingHook> WhereVerification(VerseBindingVerification status)
    {
        foreach (var hook in All)
        {
            if (hook.Verification == status)
                yield return hook;
        }
    }

    private static IReadOnlyList<VerseBindingHook> Build() =>
    [
        // —— Lockstep / time ——
        Hook(
            VerseBindingIds.TickDoSingleTick,
            VerseBindingKind.HarmonyPrefix,
            "Verse.TickManager",
            "DoSingleTick()",
            "src/RimworldMP/Patches/TickManager_DoSingleTick_Patch.cs",
            VerseBindingVerification.Documented,
            "With RIMWORLD_DIR: host+client session advances only when batch present; desync freeze stalls. World fingerprint sampled every 60 ticks.",
            "Confirmed RW 1.7.4990 Waylith: DoSingleTick present."),
        Hook(
            VerseBindingIds.TickTogglePaused,
            VerseBindingKind.HarmonyPrefix,
            "Verse.TickManager",
            "TogglePaused()",
            "src/RimworldMP/Patches/TickManager_TimeControls_Patch.cs",
            VerseBindingVerification.Documented,
            "Local pause submits CommandOpcode.Pause; applicator sets CurTimeSpeed on all peers."),
        Hook(
            VerseBindingIds.TickCurTimeSpeedSetter,
            VerseBindingKind.HarmonySetterPrefix,
            "Verse.TickManager",
            "CurTimeSpeed.set",
            "src/RimworldMP/Patches/TickManager_TimeControls_Patch.cs",
            VerseBindingVerification.Documented,
            "Speed key submits CommandOpcode.Speed; re-entry via CommandApplyReentry allowed."),
        Hook(
            VerseBindingIds.ApplyPause,
            VerseBindingKind.ApplyPath,
            "Verse.TickManager",
            "CurTimeSpeed",
            "src/RimworldMP/VerseBind/Apply/VerseTickApply.cs",
            VerseBindingVerification.Documented,
            "Harness: VerseTickApply.ApplyPause no-throw; live: pause opcode mutates TickManager."),
        Hook(
            VerseBindingIds.ApplySpeed,
            VerseBindingKind.ApplyPath,
            "Verse.TickManager",
            "CurTimeSpeed",
            "src/RimworldMP/VerseBind/Apply/VerseTickApply.cs",
            VerseBindingVerification.Documented,
            "Harness: ApplySpeed indices 0–4; live: confirm TimeSpeed.Ultrafast still exists."),

        // —— Draft / jobs / zones / areas ——
        Hook(
            VerseBindingIds.DraftSetter,
            VerseBindingKind.HarmonySetterPrefix,
            "RimWorld.Pawn_DraftController",
            "Drafted.set",
            "src/RimworldMP/Patches/Draft_Patches.cs",
            VerseBindingVerification.Documented,
            "Draft UI submits Draft/Undraft; apply sets drafter.Drafted on all peers.",
            "Confirmed RW 1.7.4990: RimWorld.Pawn_DraftController.Drafted setter."),
        Hook(
            VerseBindingIds.ApplyDraft,
            VerseBindingKind.ApplyPath,
            "RimWorld.Pawn_DraftController",
            "Drafted",
            "src/RimworldMP/VerseBind/Apply/VerseDraftApply.cs",
            VerseBindingVerification.Documented,
            "Harness: VerseDraftApply.Apply no-throw under stubs."),
        Hook(
            VerseBindingIds.JobTryTakeOrdered,
            VerseBindingKind.HarmonyPrefix,
            "Verse.AI.Pawn_JobTracker",
            "TryTakeOrderedJob(Job, JobTag?, bool)",
            "src/RimworldMP/Patches/JobGive_Patches.cs",
            VerseBindingVerification.Documented,
            "Ordered job → JobGive payload with mapId; optimistic __result=true. pawn via AccessTools FieldRef (protected).",
            "Confirmed RW 1.7.4990 overload; Harmony Prefix uses Job subset args."),
        Hook(
            VerseBindingIds.ApplyJobGive,
            VerseBindingKind.ApplyPath,
            "Verse.AI.JobMaker",
            "MakeJob / TryTakeOrderedJob",
            "src/RimworldMP/VerseBind/Apply/VerseJobApply.cs",
            VerseBindingVerification.Documented,
            "Harness: VerseJobApply.Apply no-throw; live: JobDef resolve + ordered job."),
        Hook(
            VerseBindingIds.ZoneAddCell,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Zone",
            "AddCell(IntVec3)",
            "src/RimworldMP/Patches/Zone_Patches.cs",
            VerseBindingVerification.Documented,
            "Zone paint → ZoneAdd with mapId + cells."),
        Hook(
            VerseBindingIds.ZoneRemoveCell,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Zone",
            "RemoveCell(IntVec3)",
            "src/RimworldMP/Patches/Zone_Patches.cs",
            VerseBindingVerification.Documented,
            "Zone erase → ZoneRemove."),
        Hook(
            VerseBindingIds.ApplyZone,
            VerseBindingKind.ApplyPath,
            "Verse.ZoneManager",
            "RegisterZone / AddCell / RemoveCell / Delete",
            "src/RimworldMP/VerseBind/Apply/VerseZoneApply.cs",
            VerseBindingVerification.Documented,
            "Zone_Stockpile(DefaultStockpile, ZoneManager) / Zone_Growing(ZoneManager) ctors confirmed 1.7."),
        Hook(
            VerseBindingIds.RestrictAreaSetter,
            VerseBindingKind.HarmonySetterPrefix,
            "RimWorld.Pawn_PlayerSettings",
            "AreaRestrictionInPawnCurrentMap.set",
            "src/RimworldMP/Patches/RestrictArea_Patches.cs",
            VerseBindingVerification.Documented,
            "Area restrict UI → RestrictAreaSet. pawn via AccessTools FieldRef (private)."),
        Hook(
            VerseBindingIds.ApplyRestrictArea,
            VerseBindingKind.ApplyPath,
            "RimWorld.Pawn_PlayerSettings",
            "AreaRestrictionInPawnCurrentMap",
            "src/RimworldMP/VerseBind/Apply/VerseRestrictAreaApply.cs",
            VerseBindingVerification.Documented,
            "Harness: VerseRestrictAreaApply.Apply no-throw."),

        // —— Colony ——
        Hook(
            VerseBindingIds.ResearchSetCurrent,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.ResearchManager",
            "SetCurrentProject(ResearchProjectDef)",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Documented,
            "Research UI → ResearchStart."),
        Hook(
            VerseBindingIds.BillStackAdd,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.BillStack",
            "AddBill(Bill)",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Documented,
            "Bill add → BillSet."),
        Hook(
            VerseBindingIds.BillStackDelete,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.BillStack",
            "Delete(Bill)",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Documented,
            "Bill delete → BillDelete."),
        Hook(
            VerseBindingIds.ApplyResearch,
            VerseBindingKind.ApplyPath,
            "RimWorld.ResearchManager",
            "SetCurrentProject / StopProject",
            "src/RimworldMP/VerseBind/Apply/VerseColonyApply.cs",
            VerseBindingVerification.Documented,
            "Start + Stop via IsCurrentProject scan (currentProj private)."),
        Hook(
            VerseBindingIds.ApplyBill,
            VerseBindingKind.ApplyPath,
            "RimWorld.BillStack",
            "AddBill / Delete / Reorder / Bill_Production",
            "src/RimworldMP/VerseBind/Apply/VerseColonyApply.cs",
            VerseBindingVerification.Documented,
            "BillSet/Delete/Reorder shapes confirmed on 1.7."),
        Hook(
            VerseBindingIds.ApplyTrade,
            VerseBindingKind.ApplyPath,
            "RimWorld.TradeDeal",
            "TryExecute(bool&)",
            "src/RimworldMP/VerseBind/Apply/VerseColonyApply.cs",
            VerseBindingVerification.Documented,
            "Applies via TradeSession.deal.TryExecute when session active."),
        Hook(
            VerseBindingIds.ApplyCaravan,
            VerseBindingKind.ApplyPath,
            "RimWorld.Planet.CaravanMaker",
            "MakeCaravan(pawns, faction, PlanetTile, bool)",
            "src/RimworldMP/VerseBind/Apply/VerseColonyApply.cs",
            VerseBindingVerification.Documented,
            "CaravanForm commits via CaravanMaker.MakeCaravan; add/remove/disband still shadow."),
        Hook(
            VerseBindingIds.CaptureTrade,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.TradeDeal",
            "TryExecute(bool&)",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Documented,
            "Prefix → TradeAccept; ITrader may not be a Thing."),
        Hook(
            VerseBindingIds.CaptureCaravan,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.Planet.CaravanMaker",
            "MakeCaravan(...)",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Documented,
            "Prefix → CaravanForm with PlanetTile.tileId + pawn ids."),
        Hook(
            VerseBindingIds.CapturePolicy,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.Pawn_WorkSettings / Pawn_TimetableTracker",
            "SetPriority / SetAssignment",
            "src/RimworldMP/Patches/Policy_Patches.cs",
            VerseBindingVerification.Documented,
            "Work + timetable capture confirmed; outfit/food/drug/medical/forbid still shadow-only."),
        Hook(
            VerseBindingIds.ApplyPolicy,
            VerseBindingKind.ApplyPath,
            "RimWorld.Pawn_WorkSettings / Pawn_TimetableTracker",
            "VersePolicyApply → PolicyWorld + SetPriority/SetAssignment",
            "src/RimworldMP/VerseBind/Apply/VersePolicyApply.cs",
            VerseBindingVerification.Documented,
            "Work + timetable Verse commit; remaining policy opcodes shadow-only."),

        // —— Rand ——
        Hook(
            VerseBindingIds.RandValue,
            VerseBindingKind.HarmonyGetterPrefix,
            "Verse.Rand",
            "Value.get",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Documented,
            "Session active → SyncedRand.NextFloat + desync counter."),
        Hook(
            VerseBindingIds.RandRangeInt,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Rand",
            "Range(int,int)",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Documented,
            "Session active → SyncedRand.Next."),
        Hook(
            VerseBindingIds.RandRangeFloat,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Rand",
            "Range(float,float)",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Documented,
            "Session active → SyncedRand.Range."),
        Hook(
            VerseBindingIds.RandChance,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Rand",
            "Chance(float)",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Documented,
            "Confirmed RW 1.7.4990: Rand.Chance(float) present."),
        Hook(
            VerseBindingIds.RandBool,
            VerseBindingKind.HarmonyGetterPrefix,
            "Verse.Rand",
            "Bool.get",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Documented,
            "Confirmed RW 1.7.4990: Rand.Bool getter present."),

        // —— Lobby / UI / JIP / DLC ——
        Hook(
            VerseBindingIds.LobbyUiRootOnGui,
            VerseBindingKind.HarmonyPostfix,
            "Verse.UIRoot",
            "UIRootOnGUI()",
            "src/RimworldMP/Patches/LobbyUi_Patches.cs",
            VerseBindingVerification.Documented,
            "F10 + corner brand button + desync overlay; Screen.width/height (Verse.UI dropped screenWidth)."),
        Hook(
            VerseBindingIds.LobbyUiOpen,
            VerseBindingKind.UiEntry,
            "RimworldMP.UI.Dialog_RimworldMPLobby",
            "Open()",
            "src/RimworldMP/UI/LobbyUi.cs",
            VerseBindingVerification.Documented,
            "Harness: LobbyUi.Open no-op; IsVerseUiAvailable false under NO_GAME_REFS. DebugAction → LudeonTK."),
        Hook(
            VerseBindingIds.JipSaveLoad,
            VerseBindingKind.SaveLoad,
            "Verse.GameDataSaveLoader",
            "SaveGame / LoadGame + GenFilePaths",
            "src/RimworldMP/Join/VerseSaveLoad.cs",
            VerseBindingVerification.Documented,
            "Confirmed RW 1.7.4990 SaveGame/LoadGame + GenFilePaths; live JIP still needs play smoke."),
        Hook(
            VerseBindingIds.DlcLoadGame,
            VerseBindingKind.HarmonyPostfix,
            "Verse.Game",
            "LoadGame()",
            "src/RimworldMP/Patches/Dlc_Patches.cs",
            VerseBindingVerification.Scaffold,
            "Placeholder Postfix only; real Royalty/Ideology/Biotech/Anomaly targets need DLC assemblies — do not invent."),
    ];

    private static VerseBindingHook Hook(
        string id,
        VerseBindingKind kind,
        string targetType,
        string targetMember,
        string sourceFile,
        VerseBindingVerification verification,
        string acceptanceTest,
        string notes = "") =>
        new(id, kind, targetType, targetMember, sourceFile, verification, acceptanceTest, notes);
}

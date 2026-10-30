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
            VerseBindingVerification.Unverified,
            "With RIMWORLD_DIR: host+client session advances only when batch present; desync freeze stalls.",
            "Common global tick hook; confirm Waylith still calls DoSingleTick from Game.UpdatePlay."),
        Hook(
            VerseBindingIds.TickTogglePaused,
            VerseBindingKind.HarmonyPrefix,
            "Verse.TickManager",
            "TogglePaused()",
            "src/RimworldMP/Patches/TickManager_TimeControls_Patch.cs",
            VerseBindingVerification.Unverified,
            "Local pause submits CommandOpcode.Pause; applicator sets CurTimeSpeed on all peers."),
        Hook(
            VerseBindingIds.TickCurTimeSpeedSetter,
            VerseBindingKind.HarmonySetterPrefix,
            "Verse.TickManager",
            "CurTimeSpeed.set",
            "src/RimworldMP/Patches/TickManager_TimeControls_Patch.cs",
            VerseBindingVerification.Unverified,
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
            VerseBindingVerification.Unverified,
            "Draft UI submits Draft/Undraft; apply sets drafter.Drafted on all peers.",
            "Widely patched by community mods (e.g. Achtung); still confirm 1.7."),
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
            "TryTakeOrderedJob(...)",
            "src/RimworldMP/Patches/JobGive_Patches.cs",
            VerseBindingVerification.Unverified,
            "Ordered job → JobGive payload with mapId; optimistic __result=true.",
            "UNVERIFIED: 1.7 overload may include JobTag / requestQueue — Harmony subset args often OK, confirm."),
        Hook(
            VerseBindingIds.ApplyJobGive,
            VerseBindingKind.ApplyPath,
            "Verse.AI.JobMaker",
            "MakeJob / TryTakeOrderedJob",
            "src/RimworldMP/VerseBind/Apply/VerseJobApply.cs",
            VerseBindingVerification.Unverified,
            "Harness: VerseJobApply.Apply no-throw; live: JobDef resolve + ordered job."),
        Hook(
            VerseBindingIds.ZoneAddCell,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Zone",
            "AddCell(IntVec3)",
            "src/RimworldMP/Patches/Zone_Patches.cs",
            VerseBindingVerification.Unverified,
            "Zone paint → ZoneAdd with mapId + cells."),
        Hook(
            VerseBindingIds.ZoneRemoveCell,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Zone",
            "RemoveCell(IntVec3)",
            "src/RimworldMP/Patches/Zone_Patches.cs",
            VerseBindingVerification.Unverified,
            "Zone erase → ZoneRemove."),
        Hook(
            VerseBindingIds.ApplyZone,
            VerseBindingKind.ApplyPath,
            "Verse.ZoneManager",
            "RegisterZone / AddCell / RemoveCell / Delete",
            "src/RimworldMP/VerseBind/Apply/VerseZoneApply.cs",
            VerseBindingVerification.Unverified,
            "Harness: shadow ZoneWorld + stub apply no-throw."),
        Hook(
            VerseBindingIds.RestrictAreaSetter,
            VerseBindingKind.HarmonySetterPrefix,
            "RimWorld.Pawn_PlayerSettings",
            "AreaRestrictionInPawnCurrentMap.set",
            "src/RimworldMP/Patches/RestrictArea_Patches.cs",
            VerseBindingVerification.Unverified,
            "Area restrict UI → RestrictAreaSet."),
        Hook(
            VerseBindingIds.ApplyRestrictArea,
            VerseBindingKind.ApplyPath,
            "RimWorld.Pawn_PlayerSettings",
            "AreaRestrictionInPawnCurrentMap",
            "src/RimworldMP/VerseBind/Apply/VerseRestrictAreaApply.cs",
            VerseBindingVerification.Unverified,
            "Harness: VerseRestrictAreaApply.Apply no-throw."),

        // —— Colony ——
        Hook(
            VerseBindingIds.ResearchSetCurrent,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.ResearchManager",
            "SetCurrentProject(ResearchProjectDef)",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Unverified,
            "Research UI → ResearchStart."),
        Hook(
            VerseBindingIds.BillStackAdd,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.BillStack",
            "AddBill(Bill)",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Unverified,
            "Bill add → BillSet."),
        Hook(
            VerseBindingIds.BillStackDelete,
            VerseBindingKind.HarmonyPrefix,
            "RimWorld.BillStack",
            "Delete(Bill)",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Unverified,
            "Bill delete → BillDelete."),
        Hook(
            VerseBindingIds.ApplyResearch,
            VerseBindingKind.ApplyPath,
            "RimWorld.ResearchManager",
            "SetCurrentProject / (stop API TBD)",
            "src/RimworldMP/VerseBind/Apply/VerseColonyApply.cs",
            VerseBindingVerification.Unverified,
            "Start path documented; ResearchStop intentionally scaffold (no guessed clear API)."),
        Hook(
            VerseBindingIds.ApplyBill,
            VerseBindingKind.ApplyPath,
            "RimWorld.BillStack",
            "AddBill / Delete / Bill_Production",
            "src/RimworldMP/VerseBind/Apply/VerseColonyApply.cs",
            VerseBindingVerification.Unverified,
            "BillSet/Delete shapes present; BillReorder scaffold until stack reorder API confirmed."),
        Hook(
            VerseBindingIds.ApplyTrade,
            VerseBindingKind.ApplyPath,
            "RimWorld.TradeDeal (TBD)",
            "TryExecute / similar — SCAFFOLD",
            "src/RimworldMP/VerseBind/Apply/VerseColonyApply.cs",
            VerseBindingVerification.Scaffold,
            "Offline shadow only; live deal commit logs stub. Do not invent Prefix."),
        Hook(
            VerseBindingIds.ApplyCaravan,
            VerseBindingKind.ApplyPath,
            "RimWorld.Planet.CaravanMaker (TBD)",
            "Form / add / remove / disband — SCAFFOLD",
            "src/RimworldMP/VerseBind/Apply/VerseColonyApply.cs",
            VerseBindingVerification.Scaffold,
            "Offline shadow only; CaravanForm logs stub. Do not invent Prefix."),
        Hook(
            VerseBindingIds.CaptureTrade,
            VerseBindingKind.HarmonyPrefix,
            "(TBD) TradeDeal / Dialog_Trade",
            "(TBD) accept path",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Scaffold,
            "No capture Prefix until RIMWORLD_DIR names the accept method."),
        Hook(
            VerseBindingIds.CaptureCaravan,
            VerseBindingKind.HarmonyPrefix,
            "(TBD) Dialog_FormCaravan / CaravanFormingUtility",
            "(TBD) form path",
            "src/RimworldMP/Patches/Colony_Patches.cs",
            VerseBindingVerification.Scaffold,
            "No capture Prefix until install validates form API."),
        Hook(
            VerseBindingIds.CapturePolicy,
            VerseBindingKind.HarmonyPrefix,
            "(TBD) work/timetable/outfit/food/drug/medical/forbid",
            "(TBD) setters",
            "src/RimworldMP/Patches/",
            VerseBindingVerification.Scaffold,
            "Policy opcodes shadow-apply only; no guessed Harmony capture."),
        Hook(
            VerseBindingIds.ApplyPolicy,
            VerseBindingKind.ApplyPath,
            "(TBD) Pawn_WorkSettings / Schedule / …",
            "VersePolicyApply → PolicyWorld",
            "src/RimworldMP/VerseBind/Apply/VersePolicyApply.cs",
            VerseBindingVerification.Scaffold,
            "Harness: PolicyWorld apply no-throw; Verse commit deferred."),

        // —— Rand ——
        Hook(
            VerseBindingIds.RandValue,
            VerseBindingKind.HarmonyGetterPrefix,
            "Verse.Rand",
            "Value.get",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Unverified,
            "Session active → SyncedRand.NextFloat + desync counter."),
        Hook(
            VerseBindingIds.RandRangeInt,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Rand",
            "Range(int,int)",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Unverified,
            "Session active → SyncedRand.Next."),
        Hook(
            VerseBindingIds.RandRangeFloat,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Rand",
            "Range(float,float)",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Unverified,
            "Session active → SyncedRand.Range."),
        Hook(
            VerseBindingIds.RandChance,
            VerseBindingKind.HarmonyPrefix,
            "Verse.Rand",
            "Chance(float)",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Unverified,
            "Confirm Chance(float) still exists on 1.7; else retarget."),
        Hook(
            VerseBindingIds.RandBool,
            VerseBindingKind.HarmonyGetterPrefix,
            "Verse.Rand",
            "Bool.get",
            "src/RimworldMP/Patches/Rand_Patches.cs",
            VerseBindingVerification.Unverified,
            "Confirm Bool getter on 1.7."),

        // —— Lobby / UI / JIP / DLC ——
        Hook(
            VerseBindingIds.LobbyUiRootOnGui,
            VerseBindingKind.HarmonyPostfix,
            "Verse.UIRoot",
            "UIRootOnGUI()",
            "src/RimworldMP/Patches/LobbyUi_Patches.cs",
            VerseBindingVerification.Unverified,
            "F10 + corner brand button + desync overlay; avoid fragile MainMenuDrawer sigs."),
        Hook(
            VerseBindingIds.LobbyUiOpen,
            VerseBindingKind.UiEntry,
            "RimworldMP.UI.Dialog_RimworldMPLobby",
            "Open()",
            "src/RimworldMP/UI/LobbyUi.cs",
            VerseBindingVerification.Documented,
            "Harness: LobbyUi.Open no-op; IsVerseUiAvailable false under NO_GAME_REFS."),
        Hook(
            VerseBindingIds.JipSaveLoad,
            VerseBindingKind.SaveLoad,
            "Verse.GameDataSaveLoader",
            "SaveGame / LoadGame + GenFilePaths",
            "src/RimworldMP/Join/VerseSaveLoad.cs",
            VerseBindingVerification.Unverified,
            "Live JIP capture/load; offline GameLoadHooks.TryCreateDefault() returns null (fail-loud if blob without loader)."),
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

namespace RimworldMP.VerseBind;

/// <summary>Stable ids for <see cref="VerseBindingRegistry"/> — harness asserts this catalog.</summary>
public static class VerseBindingIds
{
    // Lockstep / time
    public const string TickDoSingleTick = "tick.do-single-tick";
    public const string TickTogglePaused = "tick.toggle-paused";
    public const string TickCurTimeSpeedSetter = "tick.cur-time-speed-setter";
    public const string ApplyPause = "apply.pause";
    public const string ApplySpeed = "apply.speed";

    // Draft / jobs / zones / areas
    public const string DraftSetter = "draft.drafted-setter";
    public const string ApplyDraft = "apply.draft";
    public const string JobTryTakeOrdered = "job.try-take-ordered";
    public const string ApplyJobGive = "apply.job-give";
    public const string ZoneAddCell = "zone.add-cell";
    public const string ZoneRemoveCell = "zone.remove-cell";
    public const string ApplyZone = "apply.zone";
    public const string RestrictAreaSetter = "area.restriction-setter";
    public const string ApplyRestrictArea = "apply.restrict-area";

    // Colony
    public const string ResearchSetCurrent = "colony.research-set-current";
    public const string BillStackAdd = "colony.bill-stack-add";
    public const string BillStackDelete = "colony.bill-stack-delete";
    public const string ApplyResearch = "apply.research";
    public const string ApplyBill = "apply.bill";
    public const string ApplyTrade = "apply.trade";
    public const string ApplyCaravan = "apply.caravan";
    public const string CaptureTrade = "capture.trade";
    public const string CaptureCaravan = "capture.caravan";
    public const string CapturePolicy = "capture.policy";
    public const string ApplyPolicy = "apply.policy";

    // Rand
    public const string RandValue = "rand.value";
    public const string RandRangeInt = "rand.range-int";
    public const string RandRangeFloat = "rand.range-float";
    public const string RandChance = "rand.chance";
    public const string RandBool = "rand.bool";

    // Lobby / UI / JIP / DLC
    public const string LobbyUiRootOnGui = "lobby.ui-root-on-gui";
    public const string LobbyUiOpen = "lobby.ui-open";
    public const string JipSaveLoad = "jip.verse-save-load";
    public const string DlcLoadGame = "dlc.game-load-game";
}

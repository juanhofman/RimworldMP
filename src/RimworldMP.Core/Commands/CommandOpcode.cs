namespace RimworldMP.Core.Commands;

/// <summary>
/// Compact command opcodes for the lockstep queue.
/// Catalog covers Phase 1–3 shared-colony surfaces. Bump <c>NetProtocol.Version</c> on breaking payload changes.
/// </summary>
public enum CommandOpcode : ushort
{
    None = 0,
    EmptyBatch = 1,

    // Time controls
    Pause = 10,
    Speed = 11,

    // Pawn orders
    Draft = 20,
    Undraft = 21,
    JobGive = 22,

    // Zones / designators
    ZoneAdd = 30,
    ZoneRemove = 31,
    ZoneClear = 32,
    ZoneCreate = 33,
    DesignatorCell = 34,
    ForbidSet = 35,

    // Areas / schedules / policies
    RestrictAreaSet = 40,
    WorkPrioritySet = 41,
    TimetableSet = 42,
    FoodRestrictionAssign = 43,
    OutfitAssign = 44,
    DrugPolicyAssign = 45,
    MedicalCareSet = 46,
    HostilityResponseSet = 47,

    // Bills / research
    BillSet = 50,
    BillDelete = 51,
    BillReorder = 52,
    ResearchStart = 60,
    ResearchStop = 61,

    // Trade / caravan
    TradeAccept = 70,
    TradeCancel = 71,
    CaravanForm = 80,
    CaravanAddPawn = 81,
    CaravanRemovePawn = 82,
    CaravanDisband = 83,

    /// <summary>Scheduled SyncMethod invoke (API registry + SyncMethodCodec payload).</summary>
    SyncMethodInvoke = 90,

    // Phase 3 — DLC surfaces (structured; apply stubs without DLC DLLs)
    RoyaltyTitleChange = 100,
    RoyaltyPermitUse = 101,
    IdeologyBeliefChange = 110,
    IdeologyRitualStart = 111,
    BiotechGrowthVatAssign = 120,
    BiotechGeneInstall = 121,
    AnomalyEntityContain = 130,
    AnomalyStudy = 131,
}

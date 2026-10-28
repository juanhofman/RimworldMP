namespace RimworldMP.Core.Commands;

/// <summary>Compact command opcodes for the lockstep queue. Protocol v2 values — bump NetProtocol.Version on breaking changes.</summary>
public enum CommandOpcode : ushort
{
    None = 0,
    EmptyBatch = 1,

    Pause = 10,
    Speed = 11,

    Draft = 20,
    Undraft = 21,
    JobGive = 22,

    ZoneAdd = 30,
    ZoneRemove = 31,
    ZoneClear = 32,
    ZoneCreate = 33,

    RestrictAreaSet = 40,

    // Phase 2 — colony systems
    BillSet = 50,
    BillDelete = 51,
    ResearchStart = 60,
    TradeAccept = 70,
    CaravanForm = 80,
    CaravanAddPawn = 81,
    CaravanRemovePawn = 82,

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

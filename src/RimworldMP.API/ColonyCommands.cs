namespace RimworldMP.API;

/// <summary>
/// Author-facing helpers that enqueue shared-colony lockstep commands by opcode.
/// Payloads mirror <c>RimworldMP.Core.Commands.CommandPayloads</c> (keep in sync).
/// Prefer these or <see cref="MP.CallSyncMethod"/> over raw <see cref="MP.ExecuteCommand"/>.
/// </summary>
public static class ColonyCommands
{
    // Keep opcode numbers aligned with CommandOpcode.
    public const ushort Pause = 10;
    public const ushort Speed = 11;
    public const ushort Draft = 20;
    public const ushort Undraft = 21;
    public const ushort JobGive = 22;
    public const ushort ZoneAdd = 30;
    public const ushort ZoneRemove = 31;
    public const ushort ZoneClear = 32;
    public const ushort ZoneCreate = 33;
    public const ushort DesignatorCell = 34;
    public const ushort ForbidSet = 35;
    public const ushort RestrictAreaSet = 40;
    public const ushort WorkPrioritySet = 41;
    public const ushort TimetableSet = 42;
    public const ushort FoodRestrictionAssign = 43;
    public const ushort OutfitAssign = 44;
    public const ushort DrugPolicyAssign = 45;
    public const ushort MedicalCareSet = 46;
    public const ushort HostilityResponseSet = 47;
    public const ushort BillSet = 50;
    public const ushort BillDelete = 51;
    public const ushort BillReorder = 52;
    public const ushort ResearchStart = 60;
    public const ushort ResearchStop = 61;
    public const ushort TradeAccept = 70;
    public const ushort TradeCancel = 71;
    public const ushort CaravanForm = 80;
    public const ushort CaravanAddPawn = 81;
    public const ushort CaravanRemovePawn = 82;
    public const ushort CaravanDisband = 83;

    public static void Submit(ushort opcode, byte[]? payload = null) =>
        MP.ExecuteCommand(opcode, payload);
}

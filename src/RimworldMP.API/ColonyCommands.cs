namespace RimworldMP.API;

/// <summary>
/// Author-facing helpers that enqueue shared-colony lockstep commands by opcode.
/// Values mirror Core <c>CommandOpcode</c>. Prefer these or <see cref="MP.CallSyncMethod"/> over raw opcodes.
/// </summary>
public static class ColonyCommands
{
    /// <summary>Pause sim (<c>CommandOpcode.Pause</c>).</summary>
    public const ushort Pause = 10;
    /// <summary>Set game speed index.</summary>
    public const ushort Speed = 11;
    /// <summary>Draft a pawn.</summary>
    public const ushort Draft = 20;
    /// <summary>Undraft a pawn.</summary>
    public const ushort Undraft = 21;
    /// <summary>Give an ordered job (map/pawn/cell/target payload).</summary>
    public const ushort JobGive = 22;
    /// <summary>Add cells to a zone.</summary>
    public const ushort ZoneAdd = 30;
    /// <summary>Remove cells from a zone.</summary>
    public const ushort ZoneRemove = 31;
    /// <summary>Clear a zone.</summary>
    public const ushort ZoneClear = 32;
    /// <summary>Create a zone.</summary>
    public const ushort ZoneCreate = 33;
    /// <summary>Designator cell paint.</summary>
    public const ushort DesignatorCell = 34;
    /// <summary>Forbid / unforbid a thing.</summary>
    public const ushort ForbidSet = 35;
    /// <summary>Assign pawn allowed area.</summary>
    public const ushort RestrictAreaSet = 40;
    /// <summary>Set work type priority for a pawn.</summary>
    public const ushort WorkPrioritySet = 41;
    /// <summary>Set 24-hour timetable for a pawn.</summary>
    public const ushort TimetableSet = 42;
    /// <summary>Assign food restriction policy.</summary>
    public const ushort FoodRestrictionAssign = 43;
    /// <summary>Assign outfit policy.</summary>
    public const ushort OutfitAssign = 44;
    /// <summary>Assign drug policy.</summary>
    public const ushort DrugPolicyAssign = 45;
    /// <summary>Set medical care category.</summary>
    public const ushort MedicalCareSet = 46;
    /// <summary>Set hostility response mode.</summary>
    public const ushort HostilityResponseSet = 47;
    /// <summary>Set or add a bill on a workbench.</summary>
    public const ushort BillSet = 50;
    /// <summary>Delete a bill slot.</summary>
    public const ushort BillDelete = 51;
    /// <summary>Reorder bill slots.</summary>
    public const ushort BillReorder = 52;
    /// <summary>Start a research project.</summary>
    public const ushort ResearchStart = 60;
    /// <summary>Stop current research.</summary>
    public const ushort ResearchStop = 61;
    /// <summary>Accept a trade deal (fingerprint).</summary>
    public const ushort TradeAccept = 70;
    /// <summary>Cancel a pending trade.</summary>
    public const ushort TradeCancel = 71;
    /// <summary>Form a caravan.</summary>
    public const ushort CaravanForm = 80;
    /// <summary>Add a pawn to a caravan.</summary>
    public const ushort CaravanAddPawn = 81;
    /// <summary>Remove a pawn from a caravan.</summary>
    public const ushort CaravanRemovePawn = 82;
    /// <summary>Disband a caravan.</summary>
    public const ushort CaravanDisband = 83;

    /// <summary>Enqueue a colony command via <see cref="MP.ExecuteCommand"/>.</summary>
    public static void Submit(ushort opcode, byte[]? payload = null) =>
        MP.ExecuteCommand(opcode, payload);
}

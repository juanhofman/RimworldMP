using RimworldMP.API;
using RimworldMP.Core.Commands;

namespace RimworldMP.Compat;

/// <summary>
/// Thin helpers so DLC-facing mods enqueue structured Phase 3 commands without copying opcodes.
/// </summary>
public static class DlcHooks
{
    public static void RoyaltyTitleChange(int pawnThingId, string titleDef) =>
        MP.ExecuteCommand((ushort)CommandOpcode.RoyaltyTitleChange, CommandPayloads.PawnDef(pawnThingId, titleDef));

    public static void RoyaltyPermitUse(int pawnThingId, string permitDef) =>
        MP.ExecuteCommand((ushort)CommandOpcode.RoyaltyPermitUse, CommandPayloads.PawnDef(pawnThingId, permitDef));

    public static void IdeologyBeliefChange(string ideoId, string preceptDef, byte severity) =>
        MP.ExecuteCommand((ushort)CommandOpcode.IdeologyBeliefChange, CommandPayloads.IdeologyBelief(ideoId, preceptDef, severity));

    public static void IdeologyRitualStart(string ritualDef, int mapId) =>
        MP.ExecuteCommand((ushort)CommandOpcode.IdeologyRitualStart, CommandPayloads.RitualStart(ritualDef, mapId));

    public static void BiotechGrowthVatAssign(int vatThingId, int pawnThingId) =>
        MP.ExecuteCommand((ushort)CommandOpcode.BiotechGrowthVatAssign, CommandPayloads.GrowthVatAssign(vatThingId, pawnThingId));

    public static void BiotechGeneInstall(int pawnThingId, string geneDef) =>
        MP.ExecuteCommand((ushort)CommandOpcode.BiotechGeneInstall, CommandPayloads.PawnDef(pawnThingId, geneDef));

    public static void AnomalyEntityContain(int entityThingId, int chamberThingId) =>
        MP.ExecuteCommand((ushort)CommandOpcode.AnomalyEntityContain, CommandPayloads.EntityContain(entityThingId, chamberThingId));

    public static void AnomalyStudy(int pawnThingId, string studyDef) =>
        MP.ExecuteCommand((ushort)CommandOpcode.AnomalyStudy, CommandPayloads.PawnDef(pawnThingId, studyDef));
}

using RimworldMP.API;
using RimworldMP.Core.Commands;
using RimworldMP.Core.World;
using RimworldMP.VerseBind.Apply;

namespace RimworldMP.CommandApply;

/// <summary>
/// Applies admitted lockstep commands to the local sim / shadow stores.
/// Deterministic apply paths must not read <c>Find.CurrentMap</c> — map id comes from payloads.
/// Verse mutations live in <c>RimworldMP.VerseBind.Apply</c> (game-ref shapes + NO_GAME_REFS stubs).
/// </summary>
public static class CommandApplicator
{
    public static void Apply(CommandBatch batch)
    {
        foreach (var cmd in batch.Commands)
            ApplyOne(cmd);
    }

    public static void ApplyOne(MpCommand cmd)
    {
        using var _ = CommandApplyReentry.Rent();
        switch (cmd.Opcode)
        {
            case CommandOpcode.Pause:
                VerseTickApply.ApplyPause(CommandPayloads.ReadPause(cmd.Payload));
                break;
            case CommandOpcode.Speed:
                VerseTickApply.ApplySpeed(CommandPayloads.ReadSpeed(cmd.Payload));
                break;
            case CommandOpcode.Draft:
            case CommandOpcode.Undraft:
            {
                var (pawnId, drafted) = CommandPayloads.ReadDraft(cmd.Payload);
                VerseDraftApply.Apply(pawnId, drafted);
                break;
            }
            case CommandOpcode.JobGive:
            {
                var job = CommandPayloads.ReadJobGive(cmd.Payload);
                VerseJobApply.Apply(job);
                break;
            }
            case CommandOpcode.ZoneAdd:
            case CommandOpcode.ZoneRemove:
            case CommandOpcode.ZoneClear:
            {
                var (kind, mapId, cells) = CommandPayloads.ReadZoneCells(cmd.Payload);
                VerseZoneApply.Apply(cmd.Opcode, kind, mapId, cells);
                break;
            }
            case CommandOpcode.ZoneCreate:
            {
                var (kind, mapId, label) = CommandPayloads.ReadZoneCreate(cmd.Payload);
                ZoneWorld.Current.Create(kind, mapId, label);
                break;
            }
            case CommandOpcode.RestrictAreaSet:
            {
                var (pawnId, areaId) = CommandPayloads.ReadRestrictArea(cmd.Payload);
                VerseRestrictAreaApply.Apply(pawnId, areaId);
                break;
            }
            case CommandOpcode.DesignatorCell:
            case CommandOpcode.ForbidSet:
            case CommandOpcode.WorkPrioritySet:
            case CommandOpcode.TimetableSet:
            case CommandOpcode.FoodRestrictionAssign:
            case CommandOpcode.OutfitAssign:
            case CommandOpcode.DrugPolicyAssign:
            case CommandOpcode.MedicalCareSet:
            case CommandOpcode.HostilityResponseSet:
                VersePolicyApply.Apply(cmd);
                break;
            case CommandOpcode.BillSet:
            case CommandOpcode.BillDelete:
            case CommandOpcode.BillReorder:
            case CommandOpcode.ResearchStart:
            case CommandOpcode.ResearchStop:
            case CommandOpcode.TradeAccept:
            case CommandOpcode.TradeCancel:
            case CommandOpcode.CaravanForm:
            case CommandOpcode.CaravanAddPawn:
            case CommandOpcode.CaravanRemovePawn:
            case CommandOpcode.CaravanDisband:
                ColonyWorld.Current.ApplyCommand(cmd);
                VerseColonyApply.Apply(cmd);
                break;
            case CommandOpcode.SyncMethodInvoke:
                MP.ApplySyncMethodPayload(cmd.Payload);
                break;
            case CommandOpcode.RoyaltyTitleChange:
            case CommandOpcode.RoyaltyPermitUse:
            case CommandOpcode.IdeologyBeliefChange:
            case CommandOpcode.IdeologyRitualStart:
            case CommandOpcode.BiotechGrowthVatAssign:
            case CommandOpcode.BiotechGeneInstall:
            case CommandOpcode.AnomalyEntityContain:
            case CommandOpcode.AnomalyStudy:
                DlcWorld.Current.ApplyCommand(cmd);
                break;
        }
    }
}

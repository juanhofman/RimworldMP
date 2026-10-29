using RimworldMP.API;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.World;

namespace RimworldMP.Tests.DeterminismHarness;

/// <summary>Serialize + shadow-apply coverage for the full Phase 1–3 colony command catalog.</summary>
public class CommandCatalogTests
{
    public CommandCatalogTests()
    {
        ColonyWorld.Current.Reset();
        ZoneWorld.Current.Reset();
        PolicyWorld.Current.Reset();
        DlcWorld.Current.Reset();
    }

    [Fact]
    public void ColonyCommands_opcode_constants_match_enum()
    {
        Assert.Equal((ushort)CommandOpcode.Pause, ColonyCommands.Pause);
        Assert.Equal((ushort)CommandOpcode.BillSet, ColonyCommands.BillSet);
        Assert.Equal((ushort)CommandOpcode.TimetableSet, ColonyCommands.TimetableSet);
        Assert.Equal((ushort)CommandOpcode.CaravanDisband, ColonyCommands.CaravanDisband);
        Assert.Equal((ushort)CommandOpcode.SyncMethodInvoke, MP.SyncMethodInvokeOpcode);
    }

    [Fact]
    public void Policy_and_schedule_payloads_round_trip_and_apply()
    {
        var hours = Enumerable.Range(0, 24).Select(i => (byte)(i % 4)).ToArray();
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.WorkPrioritySet, 1, 0,
            CommandPayloads.WorkPrioritySet(5, "Cooking", 2)));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.TimetableSet, 1, 0,
            CommandPayloads.TimetableSet(5, hours)));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.FoodRestrictionAssign, 1, 0,
            CommandPayloads.PolicyAssign(5, "Lavish")));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.OutfitAssign, 1, 0,
            CommandPayloads.PolicyAssign(5, "Soldier")));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.DrugPolicyAssign, 1, 0,
            CommandPayloads.PolicyAssign(5, "NoDrugs")));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.MedicalCareSet, 1, 0,
            CommandPayloads.MedicalCareSet(5, 3)));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.HostilityResponseSet, 1, 0,
            CommandPayloads.HostilityResponseSet(5, 1)));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.ForbidSet, 1, 0,
            CommandPayloads.ForbidSet(2, 99, true)));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.DesignatorCell, 1, 0,
            CommandPayloads.DesignatorCell(1, 2, 10, 11, -1)));

        Assert.True(PolicyWorld.Current.TryGetWorkPriority(5, "Cooking", out var pri));
        Assert.Equal(2, pri);
        Assert.True(PolicyWorld.Current.TryGetTimetable(5, out var gotHours));
        Assert.Equal(hours, gotHours);
        Assert.True(PolicyWorld.Current.TryGetFood(5, out var food));
        Assert.Equal("Lavish", food);
        Assert.True(PolicyWorld.Current.TryGetOutfit(5, out var outfit));
        Assert.Equal("Soldier", outfit);
        Assert.True(PolicyWorld.Current.IsForbidden(2, 99));
        Assert.Equal(1, PolicyWorld.Current.DesignatorEventCount);
    }

    [Fact]
    public void Extended_colony_opcodes_apply_to_shadow_store()
    {
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.BillSet, 1, 0,
            CommandPayloads.BillSet(1, 10, 0, "Make_Meal", 0, 2)));
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.BillReorder, 1, 0,
            CommandPayloads.BillReorder(1, 10, 0, 1)));
        Assert.True(ColonyWorld.Current.TryGetBill(1, 10, 1, out var bill));
        Assert.Equal("Make_Meal", bill.RecipeDef);

        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.ResearchStart, 1, 0,
            CommandPayloads.ResearchStart(1, "Smithing")));
        Assert.Equal("Smithing", ColonyWorld.Current.CurrentResearch);
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.ResearchStop, 1, 0,
            CommandPayloads.ResearchStop(1)));
        Assert.Null(ColonyWorld.Current.CurrentResearch);

        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.TradeAccept, 1, 0,
            CommandPayloads.TradeAccept(1, 8, 55)));
        Assert.Equal(1, ColonyWorld.Current.TradeCount);
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.TradeCancel, 1, 0,
            CommandPayloads.TradeCancel(1, 8)));
        Assert.Equal(0, ColonyWorld.Current.TradeCount);

        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.CaravanForm, 1, 0,
            CommandPayloads.CaravanForm(1, 42, new[] { 1, 2 })));
        Assert.Equal(1, ColonyWorld.Current.CaravanCount);
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.CaravanDisband, 1, 0,
            CommandPayloads.CaravanDisband(1)));
        Assert.Equal(0, ColonyWorld.Current.CaravanCount);
    }

    [Fact]
    public void Catalog_opcodes_are_all_handled_by_applicator_without_throw()
    {
        // Smoke: every non-meta opcode that carries a minimal valid payload applies cleanly offline.
        var samples = new (CommandOpcode Op, byte[] Payload)[]
        {
            (CommandOpcode.Pause, CommandPayloads.Pause(true)),
            (CommandOpcode.Speed, CommandPayloads.Speed(2)),
            (CommandOpcode.Draft, CommandPayloads.Draft(1, true)),
            (CommandOpcode.Undraft, CommandPayloads.Draft(1, false)),
            (CommandOpcode.JobGive, CommandPayloads.JobGive(1, 1, 0, 0, "Goto")),
            (CommandOpcode.ZoneAdd, CommandPayloads.ZoneCells(1, 1, new[] { 0, 0 })),
            (CommandOpcode.ZoneRemove, CommandPayloads.ZoneCells(1, 1, new[] { 0, 0 })),
            (CommandOpcode.ZoneClear, CommandPayloads.ZoneCells(1, 1, Array.Empty<int>())),
            (CommandOpcode.ZoneCreate, CommandPayloads.ZoneCreate(1, 1, "Z")),
            (CommandOpcode.DesignatorCell, CommandPayloads.DesignatorCell(0, 1, 0, 0)),
            (CommandOpcode.ForbidSet, CommandPayloads.ForbidSet(1, 1, false)),
            (CommandOpcode.RestrictAreaSet, CommandPayloads.RestrictArea(1, 0)),
            (CommandOpcode.WorkPrioritySet, CommandPayloads.WorkPrioritySet(1, "Hauling", 1)),
            (CommandOpcode.TimetableSet, CommandPayloads.TimetableSet(1, new byte[24])),
            (CommandOpcode.FoodRestrictionAssign, CommandPayloads.PolicyAssign(1, "Simple")),
            (CommandOpcode.OutfitAssign, CommandPayloads.PolicyAssign(1, "Worker")),
            (CommandOpcode.DrugPolicyAssign, CommandPayloads.PolicyAssign(1, "Social")),
            (CommandOpcode.MedicalCareSet, CommandPayloads.MedicalCareSet(1, 1)),
            (CommandOpcode.HostilityResponseSet, CommandPayloads.HostilityResponseSet(1, 0)),
            (CommandOpcode.BillSet, CommandPayloads.BillSet(1, 1, 0, "Cut", 0, 1)),
            (CommandOpcode.BillDelete, CommandPayloads.BillDelete(1, 1, 0)),
            (CommandOpcode.BillReorder, CommandPayloads.BillReorder(1, 1, 0, 1)),
            (CommandOpcode.ResearchStart, CommandPayloads.ResearchStart(1, "X")),
            (CommandOpcode.ResearchStop, CommandPayloads.ResearchStop(1)),
            (CommandOpcode.TradeAccept, CommandPayloads.TradeAccept(1, 1, 1)),
            (CommandOpcode.TradeCancel, CommandPayloads.TradeCancel(1, 1)),
            (CommandOpcode.CaravanForm, CommandPayloads.CaravanForm(1, 1, new[] { 1 })),
            (CommandOpcode.CaravanAddPawn, CommandPayloads.CaravanPawn(1, 2)),
            (CommandOpcode.CaravanRemovePawn, CommandPayloads.CaravanPawn(1, 2)),
            (CommandOpcode.CaravanDisband, CommandPayloads.CaravanDisband(1)),
            (CommandOpcode.RoyaltyTitleChange, CommandPayloads.PawnDef(1, "Title")),
            (CommandOpcode.RoyaltyPermitUse, CommandPayloads.PawnDef(1, "Permit")),
            (CommandOpcode.IdeologyBeliefChange, CommandPayloads.IdeologyBelief("ideo", "precept", 1)),
            (CommandOpcode.IdeologyRitualStart, CommandPayloads.RitualStart("ritual", 1)),
            (CommandOpcode.BiotechGrowthVatAssign, CommandPayloads.GrowthVatAssign(1, 2)),
            (CommandOpcode.BiotechGeneInstall, CommandPayloads.PawnDef(1, "Gene")),
            (CommandOpcode.AnomalyEntityContain, CommandPayloads.EntityContain(1, 2)),
            (CommandOpcode.AnomalyStudy, CommandPayloads.PawnDef(1, "Study")),
        };

        foreach (var (op, payload) in samples)
            CommandApplicator.ApplyOne(new MpCommand(op, 1, 0, payload));
    }
}

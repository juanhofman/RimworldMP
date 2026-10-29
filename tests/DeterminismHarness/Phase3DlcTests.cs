using RimworldMP.Compat;
using RimworldMP.Core.Commands;
using RimworldMP.Core.World;
using RimworldMP.API;

namespace RimworldMP.Tests.DeterminismHarness;

[Collection("MpApi")]
public class Phase3DlcTests
{
    public Phase3DlcTests() => DlcWorld.Current.Reset();

    [Fact]
    public void Dlc_payloads_and_shadow_store()
    {
        DlcWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.RoyaltyTitleChange, 1, 0, CommandPayloads.PawnDef(5, "Freeholder")));
        DlcWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.IdeologyBeliefChange, 1, 0, CommandPayloads.IdeologyBelief("ideo1", "Precept_Cannibalism", 2)));
        DlcWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.IdeologyRitualStart, 1, 0, CommandPayloads.RitualStart("Funeral", 0)));
        DlcWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.BiotechGrowthVatAssign, 1, 0, CommandPayloads.GrowthVatAssign(9, 8)));
        DlcWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.BiotechGeneInstall, 1, 0, CommandPayloads.PawnDef(8, "Hair_GrayOnly")));
        DlcWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.AnomalyEntityContain, 1, 0, CommandPayloads.EntityContain(3, 4)));
        DlcWorld.Current.ApplyCommand(new MpCommand(CommandOpcode.AnomalyStudy, 1, 0, CommandPayloads.PawnDef(1, "CubeInterest")));

        Assert.Equal(7, DlcWorld.Current.EventCount);
        var events = DlcWorld.Current.SnapshotEvents();
        Assert.Contains(events, e => e.Contains("royalty.title"));
        Assert.Contains(events, e => e.Contains("ideo.ritual"));
        Assert.Contains(events, e => e.Contains("biotech.gene"));
        Assert.Contains(events, e => e.Contains("anomaly.contain"));
    }

    [Fact]
    public void Compat_sim_patterns_stable_order()
    {
        var items = new[] { 3, 1, 2 };
        Assert.Equal(new[] { 1, 2, 3 }, SimPatterns.StableOrder(items, x => x).ToArray());
        Assert.True(SimPatterns.ShouldRunHostOnly(true));
        Assert.False(SimPatterns.ShouldRunHostOnly(false));
    }

    [Fact]
    public void DlcHooks_enqueue_via_api_bind()
    {
        var seen = new List<(ushort op, byte[]? payload)>();
        MP.Bind(() => true, () => true, () => 0, (op, p) => seen.Add((op, p)), () => { }, () => 1);
        DlcHooks.RoyaltyPermitUse(2, "Permit_CallLaborers");
        DlcHooks.AnomalyStudy(4, "Study_Fleshbeast");
        Assert.Equal(2, seen.Count);
        Assert.Equal((ushort)CommandOpcode.RoyaltyPermitUse, seen[0].op);
        Assert.Equal((ushort)CommandOpcode.AnomalyStudy, seen[1].op);
    }
}

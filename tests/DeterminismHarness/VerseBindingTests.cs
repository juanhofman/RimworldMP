using RimworldMP.Core.World;
using RimworldMP.VerseBind;

namespace RimworldMP.Tests.DeterminismHarness;

public class VerseBindingTests
{
    public VerseBindingTests()
    {
        ZoneWorld.Current.Reset();
        ColonyWorld.Current.Reset();
        PolicyWorld.Current.Reset();
    }

    [Fact]
    public void Registry_lists_all_required_hook_ids()
    {
        Assert.NotEmpty(VerseBindingRegistry.RequiredIds);
        Assert.Equal(VerseBindingRegistry.RequiredIds.Count, VerseBindingRegistry.RequiredIds.Distinct(StringComparer.Ordinal).Count());

        var present = VerseBindingRegistry.All.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in VerseBindingRegistry.RequiredIds)
            Assert.True(present.Contains(id), $"Missing binding id: {id}");
    }

    [Fact]
    public void Registry_ids_are_unique_and_hooks_are_well_formed()
    {
        var ids = VerseBindingRegistry.All.Select(h => h.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        foreach (var hook in VerseBindingRegistry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(hook.Id));
            Assert.False(string.IsNullOrWhiteSpace(hook.TargetType));
            Assert.False(string.IsNullOrWhiteSpace(hook.TargetMember));
            Assert.False(string.IsNullOrWhiteSpace(hook.SourceFile));
            Assert.False(string.IsNullOrWhiteSpace(hook.AcceptanceTest));
            Assert.StartsWith("src/", hook.SourceFile, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Registry_marks_trade_caravan_policy_capture_as_scaffold()
    {
        Assert.Equal(
            VerseBindingVerification.Scaffold,
            VerseBindingRegistry.Find(VerseBindingIds.CaptureTrade)?.Verification);
        Assert.Equal(
            VerseBindingVerification.Scaffold,
            VerseBindingRegistry.Find(VerseBindingIds.CaptureCaravan)?.Verification);
        Assert.Equal(
            VerseBindingVerification.Scaffold,
            VerseBindingRegistry.Find(VerseBindingIds.CapturePolicy)?.Verification);
        Assert.Equal(
            VerseBindingVerification.Scaffold,
            VerseBindingRegistry.Find(VerseBindingIds.ApplyTrade)?.Verification);
        Assert.Equal(
            VerseBindingVerification.Scaffold,
            VerseBindingRegistry.Find(VerseBindingIds.ApplyCaravan)?.Verification);
        Assert.Equal(
            VerseBindingVerification.Scaffold,
            VerseBindingRegistry.Find(VerseBindingIds.DlcLoadGame)?.Verification);
    }

    [Fact]
    public void Harness_builds_without_game_refs()
    {
        Assert.False(VerseBindingRegistry.IsGameRefsCompiled);
    }

    [Fact]
    public void Binding_stubs_do_not_throw()
    {
        var ex = Record.Exception(VerseBindingSmoke.RunNoThrowSmoke);
        Assert.Null(ex);
    }

    [Fact]
    public void Find_returns_null_for_unknown_id()
    {
        Assert.Null(VerseBindingRegistry.Find("does.not.exist"));
    }

    [Fact]
    public void WhereVerification_filters_scaffold_hooks()
    {
        var scaffold = VerseBindingRegistry.WhereVerification(VerseBindingVerification.Scaffold).ToList();
        Assert.Contains(scaffold, h => h.Id == VerseBindingIds.CaptureTrade);
        Assert.DoesNotContain(scaffold, h => h.Id == VerseBindingIds.ApplyPause);
    }
}

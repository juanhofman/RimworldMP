using RimworldMP.Core.Commands;

namespace RimworldMP.Tests.DeterminismHarness;

public class CommandSerializationTests
{
    [Fact]
    public void MpCommand_round_trips()
    {
        var original = new MpCommand(CommandOpcode.JobGive, 42, 1, CommandPayloads.JobGive(7, 10, 20, "Goto"));
        var bytes = original.ToBytes();
        var restored = MpCommand.ReadFrom(bytes, out var consumed);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(original.Opcode, restored.Opcode);
        Assert.Equal(original.Tick, restored.Tick);
        Assert.Equal(original.PlayerId, restored.PlayerId);
        Assert.Equal(original.Payload, restored.Payload);
    }

    [Fact]
    public void CommandBatch_round_trips_including_empty()
    {
        var empty = CommandBatch.Empty(5);
        var restoredEmpty = CommandBatch.FromBytes(empty.ToBytes());
        Assert.Equal(5, restoredEmpty.Tick);
        Assert.Empty(restoredEmpty.Commands);

        var batch = new CommandBatch(9, new[]
        {
            new MpCommand(CommandOpcode.Pause, 9, 0, CommandPayloads.Pause(true)),
            new MpCommand(CommandOpcode.Draft, 9, 1, CommandPayloads.Draft(3, true)),
        });
        var restored = CommandBatch.FromBytes(batch.ToBytes());
        Assert.Equal(9, restored.Tick);
        Assert.Equal(2, restored.Commands.Count);
        Assert.Equal(CommandOpcode.Draft, restored.Commands[1].Opcode);
    }

    [Fact]
    public void Payloads_round_trip()
    {
        Assert.True(CommandPayloads.ReadPause(CommandPayloads.Pause(true)));
        Assert.Equal((byte)3, CommandPayloads.ReadSpeed(CommandPayloads.Speed(3)));

        var draft = CommandPayloads.ReadDraft(CommandPayloads.Draft(99, false));
        Assert.Equal(99, draft.PawnLoadId);
        Assert.False(draft.Drafted);

        var job = CommandPayloads.ReadJobGive(CommandPayloads.JobGive(1, 2, 3, "HaulToCell"));
        Assert.Equal((1, 2, 3, "HaulToCell"), job);

        var zone = CommandPayloads.ReadZoneCells(CommandPayloads.ZoneCells(1, new[] { 4, 5, 6, 7 }));
        Assert.Equal(1, zone.ZoneKind);
        Assert.Equal(new[] { 4, 5, 6, 7 }, zone.CellsPacked);

        var area = CommandPayloads.ReadRestrictArea(CommandPayloads.RestrictArea(8, 9));
        Assert.Equal((8, 9), area);
    }
}

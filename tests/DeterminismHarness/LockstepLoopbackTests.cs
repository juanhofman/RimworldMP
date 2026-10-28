using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimworldMP.Net;

namespace RimworldMP.Tests.DeterminismHarness;

public class LockstepLoopbackTests
{
    [Fact]
    public async Task Host_and_client_apply_same_command_stream()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        const long seed = 424242;
        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: seed);
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: seed);

        var hostApplied = new List<CommandOpcode>();
        var clientApplied = new List<CommandOpcode>();
        host.OnBatchApply += b => hostApplied.AddRange(b.Commands.Select(c => c.Opcode));
        client.OnBatchApply += b => clientApplied.AddRange(b.Commands.Select(c => c.Opcode));

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));

        host.SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(true));
        host.SubmitCommand(CommandOpcode.Speed, CommandPayloads.Speed(2));
        host.HostEmitNextBatch();

        // Allow loopback delivery
        await Task.Delay(50);

        Assert.True(host.TryAdvanceSim());
        // Client may need a moment for the batch to land
        var advanced = false;
        for (var i = 0; i < 20 && !advanced; i++)
        {
            advanced = client.TryAdvanceSim();
            if (!advanced)
                await Task.Delay(10);
        }

        Assert.True(advanced);
        Assert.Equal(host.Tick, client.Tick);
        Assert.Equal(hostApplied, clientApplied);
        Assert.Equal(host.Rand.DrawCount, client.Rand.DrawCount);
    }

    [Fact]
    public async Task Client_proposal_is_staged_by_host()
    {
        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 7);
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 7);

        CommandBatch? seen = null;
        host.OnBatchApply += b => seen = b;

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));

        client.SubmitCommand(CommandOpcode.Draft, CommandPayloads.Draft(5, true));
        await Task.Delay(50);
        host.HostEmitNextBatch();
        Assert.True(host.TryAdvanceSim());
        Assert.NotNull(seen);
        Assert.Contains(seen!.Commands, c => c.Opcode == CommandOpcode.Draft && c.PlayerId == 1);
    }
}

using System.Reflection;
using RimworldMP.API;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using RimworldMP.Net;

namespace RimworldMP.Tests.DeterminismHarness;

[Collection("MpApi")]
public class SyncMethodTests : IDisposable
{
    private static int _applyCount;
    private static string? _lastMessage;
    private static int _lastInt;

    public SyncMethodTests()
    {
        MP.ResetForTests();
        SessionFacade.Clear();
        _applyCount = 0;
        _lastMessage = null;
        _lastInt = 0;
    }

    public void Dispose()
    {
        MP.ResetForTests();
        SessionFacade.Clear();
        GC.SuppressFinalize(this);
    }

    // Not a test — SyncMethod target invoked via reflection.
#pragma warning disable xUnit1013
    public static void ApplySyncedToast(string message, int value)
    {
        _applyCount++;
        _lastMessage = message;
        _lastInt = value;
    }
#pragma warning restore xUnit1013

    [Fact]
    public void Codec_round_trips_supported_arg_types()
    {
        var key = "RimworldMP.Tests.DeterminismHarness.SyncMethodTests::ApplySyncedToast";
        var payload = SyncMethodCodec.Encode(key, new object?[]
        {
            null,
            true,
            42,
            99L,
            1.5f,
            2.5d,
            "hi",
            new byte[] { 1, 2, 3 },
        });
        var (decodedKey, args) = SyncMethodCodec.Decode(payload);
        Assert.Equal(key, decodedKey);
        Assert.Null(args[0]);
        Assert.Equal(true, args[1]);
        Assert.Equal(42, args[2]);
        Assert.Equal(99L, args[3]);
        Assert.Equal(1.5f, args[4]);
        Assert.Equal(2.5d, args[5]);
        Assert.Equal("hi", args[6]);
        Assert.Equal(new byte[] { 1, 2, 3 }, (byte[])args[7]!);
    }

    [Fact]
    public void InvokeRegistered_runs_registered_method()
    {
        var method = typeof(SyncMethodTests).GetMethod(nameof(ApplySyncedToast), BindingFlags.Public | BindingFlags.Static)!;
        MP.RegisterSyncMethod(method);
        MP.InvokeRegistered(SyncMethodCodec.MethodKey(method), new object?[] { "offline", 7 });
        Assert.Equal(1, _applyCount);
        Assert.Equal("offline", _lastMessage);
        Assert.Equal(7, _lastInt);
    }

    [Fact]
    public void ApplySyncMethodPayload_fails_loud_when_unregistered()
    {
        var payload = SyncMethodCodec.Encode("Missing::Method", new object?[] { "x" });
        Assert.Throws<InvalidOperationException>(() => MP.ApplySyncMethodPayload(payload));
    }

    [Fact]
    public async Task SyncMethod_roundtrips_via_command_schedule()
    {
        var method = typeof(SyncMethodTests).GetMethod(nameof(ApplySyncedToast), BindingFlags.Public | BindingFlags.Static)!;
        MP.RegisterSyncMethod(method);

        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 11);
        await using var client = new MpSession(clientTransport, isHost: false, sessionSeed: 11);
        host.OnBatchApply += CommandApplicator.Apply;
        client.OnBatchApply += CommandApplicator.Apply;

        await host.StartAsync(new NetEndpoint("loop", 0));
        await client.StartAsync(new NetEndpoint("loop", 0));
        await TestHelpers.WaitForJipAsync(client);

        Assert.Equal(0, _applyCount);
        var payload = SyncMethodCodec.Encode(
            MP.KeyOf(method),
            new object?[] { "lockstep-hi", 42 });
        host.SubmitCommand(CommandOpcode.SyncMethodInvoke, payload);
        Assert.Equal(0, _applyCount);

        host.HostEmitNextBatch();
        Assert.True(host.TryAdvanceSim());
        Assert.Equal(1, _applyCount);
        Assert.Equal("lockstep-hi", _lastMessage);
        Assert.Equal(42, _lastInt);

        var clientAdvanced = false;
        for (var i = 0; i < 40 && !clientAdvanced; i++)
        {
            clientAdvanced = client.TryAdvanceSim();
            if (!clientAdvanced)
                await Task.Delay(10);
        }

        Assert.True(clientAdvanced);
        Assert.Equal(2, _applyCount);
        Assert.Equal(host.Tick, client.Tick);
        Assert.Equal((ushort)CommandOpcode.SyncMethodInvoke, MP.SyncMethodInvokeOpcode);
    }

    [Fact]
    public async Task CallSyncMethod_in_session_schedules_not_invokes()
    {
        var method = typeof(SyncMethodTests).GetMethod(nameof(ApplySyncedToast), BindingFlags.Public | BindingFlags.Static)!;
        MP.RegisterSyncMethod(method);

        var hostTransport = new LoopbackTransport();
        var clientTransport = new LoopbackTransport();
        LoopbackTransport.Pair(hostTransport, clientTransport);

        await using var host = new MpSession(hostTransport, isHost: true, sessionSeed: 3);
        host.OnBatchApply += CommandApplicator.Apply;
        SessionFacade.Attach(host);
        MP.Bind(
            () => SessionFacade.IsInSession,
            () => SessionFacade.IsHost,
            () => SessionFacade.LocalPlayerId,
            (opcode, payload) => SessionFacade.Submit((CommandOpcode)opcode, payload),
            () => { },
            tick: () => SessionFacade.Current?.Tick ?? -1);

        await host.StartAsync(new NetEndpoint("loop", 0));
        try
        {
            MP.CallSyncMethod(method, "scheduled", 9);
            Assert.Equal(0, _applyCount);
            host.HostEmitNextBatch();
            Assert.True(host.TryAdvanceSim());
            Assert.Equal(1, _applyCount);
            Assert.Equal("scheduled", _lastMessage);
            Assert.Equal(9, _lastInt);
        }
        finally
        {
            SessionFacade.Clear();
        }
    }

    [Fact]
    public void CommandApplicator_applies_SyncMethodInvoke_opcode()
    {
        var method = typeof(SyncMethodTests).GetMethod(nameof(ApplySyncedToast), BindingFlags.Public | BindingFlags.Static)!;
        MP.RegisterSyncMethod(method);
        var payload = SyncMethodCodec.Encode(MP.KeyOf(method), new object?[] { "direct", 3 });
        CommandApplicator.ApplyOne(new MpCommand(CommandOpcode.SyncMethodInvoke, 1, 0, payload));
        Assert.Equal(1, _applyCount);
        Assert.Equal("direct", _lastMessage);
        Assert.Equal(3, _lastInt);
    }
}

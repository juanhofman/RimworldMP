using System.Reflection;
using RimworldMP.API;
using RimworldMP.Examples.CompatHello;
using RimworldMP.Examples.CompatHostIo;

namespace RimworldMP.Tests.DeterminismHarness;

/// <summary>Harness coverage for the public Compat/API registration surface (no Verse).</summary>
[Collection("MpApi")]
public class ApiRegistrationTests : IDisposable
{
    public ApiRegistrationTests() => MP.ResetForTests();

    public void Dispose()
    {
        MP.ResetForTests();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RegisterSyncMethod_manual_and_attribute_scan()
    {
        var method = typeof(SampleSync).GetMethod(nameof(SampleSync.ApplyValue))!;
        MP.RegisterSyncMethod(method);
        Assert.True(MP.IsSyncMethodRegistered(method));
        Assert.Contains(MP.KeyOf(method), MP.RegisteredSyncMethodKeys);

        MP.ResetForTests();
        var n = MP.RegisterSyncMethodsFromType(typeof(SampleSync));
        Assert.Equal(2, n);
        Assert.True(MP.IsSyncMethodRegistered(MP.KeyOf(method)));
        Assert.True(MP.IsSyncMethodRegistered(typeof(SampleSync).FullName + "::custom_apply"));
    }

    [Fact]
    public void TryInvokeSyncMethod_runs_registered_body()
    {
        MP.RegisterSyncMethodsFromType(typeof(SampleSync));
        SampleSync.Last = 0;
        Assert.True(MP.TryInvokeSyncMethod(MP.KeyOf(typeof(SampleSync).GetMethod(nameof(SampleSync.ApplyValue))!), null, 42));
        Assert.Equal(42, SampleSync.Last);
    }

    [Fact]
    public void RegisterSyncMethod_delegate_overload()
    {
        Action<int> d = SampleSync.ApplyValue;
        MP.RegisterSyncMethod(d);
        Assert.True(MP.IsSyncMethodRegistered(d.Method));
    }

    [Fact]
    public void FieldWatch_begin_end_detects_change_and_emits()
    {
        var target = new SampleSync { Watched = 1 };
        var prop = typeof(SampleSync).GetProperty(nameof(SampleSync.Watched))!;
        FieldWatch.Register(prop);

        (string key, object? before, object? after)? emitted = null;
        FieldWatch.BindEmitter((k, b, a) => emitted = (k, b, a));

        FieldWatch.WatchBegin(target, prop);
        target.Watched = 1;
        Assert.False(FieldWatch.WatchEnd(target, prop));
        Assert.Null(emitted);

        FieldWatch.WatchBegin(target, prop);
        target.Watched = 9;
        Assert.True(FieldWatch.WatchEnd(target, prop));
        Assert.NotNull(emitted);
        Assert.Equal(1, emitted!.Value.before);
        Assert.Equal(9, emitted.Value.after);
        Assert.True(FieldWatch.IsRegistered(prop));
        Assert.Contains(FieldWatch.KeyOf(prop), FieldWatch.RegisteredFieldKeys);
    }

    [Fact]
    public void FieldWatch_using_scope_and_RegisterFromType()
    {
        var n = FieldWatch.RegisterFromType(typeof(SampleSync));
        Assert.True(n >= 1);
        var target = new SampleSync { Watched = 3 };
        var prop = typeof(SampleSync).GetProperty(nameof(SampleSync.Watched))!;
        using (FieldWatch.Watch(target, prop))
            target.Watched = 4;
        Assert.Equal(4, target.Watched);
    }

    [Fact]
    public void HostInject_offline_compute_and_inject()
    {
        var applied = 0;
        Assert.True(HostInject.HostComputeAndInject(() => 7, v => applied = v));
        Assert.Equal(7, applied);

        Assert.True(HostInject.TryRunHostOnly(() => applied = 11));
        Assert.Equal(11, applied);
    }

    [Fact]
    public void HostInject_client_in_session_skips_compute()
    {
        MP.Bind(
            isInSession: () => true,
            isHost: () => false,
            localPlayerId: () => 2,
            submit: (_, _) => { },
            raiseDesync: () => { },
            tick: () => 10);

        Assert.False(HostInject.TryRunHostOnly(() => throw new InvalidOperationException("must not run")));
        Assert.False(HostInject.HostComputeAndInject(() => 1, _ => throw new InvalidOperationException("must not apply")));
        Assert.True(MP.IsInSession);
        Assert.False(MP.IsHost);
        Assert.Equal(10, MP.Tick);
    }

    [Fact]
    public void HostInject_host_enqueues_command()
    {
        var seen = new List<(ushort op, byte[]? payload)>();
        MP.Bind(
            () => true,
            () => true,
            () => 0,
            (op, p) => seen.Add((op, p)),
            () => { },
            () => 3);

        Assert.True(HostInject.HostComputeInjectCommand(
            hostCompute: () => 99,
            serializePayload: v => BitConverter.GetBytes(v),
            opcode: 5,
            localPreviewApply: v => Assert.Equal(99, v)));

        Assert.Single(seen);
        Assert.Equal((ushort)5, seen[0].op);
        Assert.Equal(99, BitConverter.ToInt32(seen[0].payload!));
    }

    [Fact]
    public void CompatHello_example_registers_and_watches()
    {
        var mod = CompatHelloMod.Initialize();
        Assert.True(MP.IsSyncMethodRegistered(typeof(CompatHelloMod).GetMethod(nameof(CompatHelloMod.ApplySyncedToast))!));
        Assert.True(mod.SimulateDialogOk("greet"));
        Assert.Equal("greet", mod.ToastPrefix);
    }

    [Fact]
    public void CompatHostIo_example_host_flow()
    {
        CompatHostIoMod.Initialize();
        Assert.Contains(
            MP.RegisteredSyncMethodKeys,
            k => k.EndsWith("::apply_host_seed", StringComparison.Ordinal));

        Assert.True(CompatHostIoMod.RunHostIoFlow());

        var seen = new List<(ushort, byte[]?)>();
        MP.Bind(() => true, () => true, () => 0, (op, p) => seen.Add((op, p)), () => { }, () => 1);
        CompatHostIoMod.ApplyHostComputedSeed(123);
        Assert.Equal(123, CompatHostIoMod.LastAppliedSeed);
        Assert.Single(seen);
    }

    [Fact]
    public void OnDesync_raises_via_DesyncRaise()
    {
        var hits = 0;
        MP.Bind(() => false, () => false, () => -1, (_, _) => { }, () => { });
        MP.OnDesync += () => hits++;
        MP.DesyncRaise!();
        Assert.Equal(1, hits);
    }
}

internal sealed class SampleSync
{
    public static int Last;

    [SyncField]
    public int Watched { get; set; }

    [SyncMethod]
    public static void ApplyValue(int value) => Last = value;

    [SyncMethod("custom_apply")]
    public static void ApplyCustom(int value) => Last = value;
}

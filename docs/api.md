# RimworldMP.API

Public surface for third-party mods. Reference `RimworldMP.API` only — do not soft-depend on Core/Net unless you need internals.

**Full guide:** [mod-author-api.md](./mod-author-api.md) · **Examples:** [`examples/`](../examples/)

## Session flags

| Member | Meaning |
| --- | --- |
| `MP.Enabled` | Assemblies loaded |
| `MP.IsInSession` | Lockstep session active |
| `MP.IsHost` | This peer is the in-game host |
| `MP.LocalPlayerId` | Player id, or `-1` offline |
| `MP.Tick` | Current lockstep tick |

## SyncMethod registration + schedule

```csharp
[SyncMethod]
public static void ApplyHostResult(int value) { /* deterministic */ }

MP.RegisterSyncMethodsFromType(typeof(MyMod));
// or
var method = typeof(MyMod).GetMethod(nameof(MyMod.ApplyHostResult))!;
MP.RegisterSyncMethod(method);
MP.CallSyncMethod(method, arg0);   // schedules SyncMethodInvoke (opcode 90)
```

In-session, `CallSyncMethod` enqueues a lockstep command; the method runs on every peer when the batch applies (`CommandApplicator` → `MP.ApplySyncMethodPayload`). Offline (no session), it invokes immediately. Supported arg types: `null`, `bool`, `int`, `long`, `float`, `double`, `string`, `byte[]` (`SyncMethodCodec`).

Keys: `MP.RegisteredSyncMethodKeys`, `MP.IsSyncMethodRegistered(...)`, `MP.TryInvokeSyncMethod(...)`, `MP.KeyOf(...)`. Host-only I/O should compute on the host then inject a synced result at tick T via `CallSyncMethod`.

## Colony command opcodes

`ColonyCommands` exposes ushort constants aligned with Core `CommandOpcode` (pause/speed, draft/jobs, zones/designators, restrict/work/timetable/policies, bills/research, trade/caravan). Build payloads with Core `CommandPayloads`, then:

```csharp
MP.ExecuteCommand(ColonyCommands.BillSet, payloadBytes);
```

Full catalog + Verse-blocked apply notes: [feature-completeness.md](./feature-completeness.md).

## Field watch

```csharp
[SyncField]
public int AreaId { get; set; }

FieldWatch.RegisterFromType(typeof(MySettings));
using (FieldWatch.Watch(this, prop)) { /* dialog */ }
```

## Host-only + inject

```csharp
HostInject.HostComputeAndInject(HostCompute, ApplyHostResult);
HostInject.TryRunHostOnly(() => { /* HTTP / disk */ });
```

## Commands / desync

```csharp
MP.ExecuteCommand(opcode, payloadBytes);
MP.OnDesync += () => { /* freeze UI */ };
```

Opcodes live in Core (`CommandOpcode`). Prefer SyncMethod / Compat `DlcHooks` over raw opcodes when possible.

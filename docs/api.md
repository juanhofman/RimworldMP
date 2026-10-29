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

## SyncMethod

```csharp
[SyncMethod]
public static void ApplyHostResult(int value) { /* deterministic */ }

MP.RegisterSyncMethodsFromType(typeof(MyMod));
// or
MP.RegisterSyncMethod(typeof(MyMod).GetMethod(nameof(MyMod.ApplyHostResult))!);
```

Keys: `MP.RegisteredSyncMethodKeys`, `MP.IsSyncMethodRegistered(...)`, `MP.TryInvokeSyncMethod(...)`.

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

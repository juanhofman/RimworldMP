# Mod author API — RimworldMP

How third-party mods opt into multiplayer-safe sync without soft-depending on Core or Net.

**Assembly:** `RimworldMP.API`  
**Namespace:** `RimworldMP.API`  
**Quick reference:** [api.md](./api.md) · **Examples:** [`examples/`](../examples/)

## Principles

1. Anything that mutates shared sim state from UI or network must go through a **SyncMethod** / lockstep command — never a raw button click into Verse mutators.
2. Host-only I/O (HTTP, disk, LLM, `System.Random`) stays on the host; **inject** a deterministic result for all peers.
3. Fields edited in dialogs need **FieldWatch** begin/end so deltas can become commands.
4. Reference **only** `RimworldMP.API` from Workshop mods unless you truly need Core opcodes.

## Session flags

| Member | Meaning |
| --- | --- |
| `MP.Enabled` | API assembly loaded |
| `MP.IsInSession` | Lockstep session active |
| `MP.IsHost` | This peer is the in-game host |
| `MP.LocalPlayerId` | Player id, or `-1` offline |
| `MP.Tick` | Current lockstep tick, or `-1` |

```csharp
if (!MP.IsInSession)
{
    // Single-player / offline path — safe to call local logic directly.
}
```

## Registering SyncMethods

### Attribute + type scan (preferred)

```csharp
using RimworldMP.API;

public static class MyModSync
{
    public static void Init() => MP.RegisterSyncMethodsFromType(typeof(MyModSync));

    [SyncMethod]
    public static void ApplyRename(int thingId, string name)
    {
        // Deterministic apply only — no Random, no HTTP, no HashSet-order side effects.
    }
}
```

### Manual registration

```csharp
MP.RegisterSyncMethod(typeof(MyModSync).GetMethod(nameof(MyModSync.ApplyRename))!);
// or method group:
MP.RegisterSyncMethod(MyModSync.ApplyRename);
```

### Assembly scan

```csharp
MP.RegisterSyncMethodsFromAssembly(typeof(MyModSync).Assembly);
```

Discover registrations: `MP.RegisteredSyncMethodKeys`, `MP.IsSyncMethodRegistered(...)`, `MP.KeyOf(method)`.

## Field watch

```csharp
public sealed class MySettings
{
    [SyncField("allowed_area")]
    public int AllowedAreaId { get; set; }

    public void OnDialogOpen()
    {
        FieldWatch.RegisterFromType(typeof(MySettings));
        var prop = typeof(MySettings).GetProperty(nameof(AllowedAreaId))!;
        FieldWatch.WatchBegin(this, prop);
    }

    public void OnDialogOk()
    {
        var prop = typeof(MySettings).GetProperty(nameof(AllowedAreaId))!;
        if (FieldWatch.WatchEnd(this, prop))
        {
            // Value changed — enqueue SyncMethod / command (Core binds emitter in-session).
            MyModSync.ApplyArea(AllowedAreaId);
        }
    }
}
```

`using (FieldWatch.Watch(target, member)) { /* dialog mutations */ }` is equivalent.

**Stub status:** watch snapshots and change detection work offline. Live command emission is bound by the main mod via `FieldWatch.BindEmitter` when a session starts.

## Host-only + inject

```csharp
// 1) Host computes nondeterministically
// 2) All peers apply deterministically via SyncMethod

HostInject.HostComputeAndInject(
    hostCompute: () => File.ReadAllText(path).GetHashCode(),
    applyOnAllPeers: MyModSync.ApplyFileHash);

// Or gate manually:
if (HostInject.TryRunHostOnly(() => FetchRemote()))
{
    // host-only side effects done; inject via SyncMethod / ExecuteCommand
}
```

Clients in-session return `false` from host helpers and wait for the synced apply from the command stream. Offline / no session: compute+apply run locally so CI and single-player keep working.

## Raw commands

```csharp
MP.ExecuteCommand(opcode, payloadBytes);
```

Opcodes live in Core (`CommandOpcode`). Prefer SyncMethod registration and Compat `DlcHooks` over inventing opcodes.

## Desync

```csharp
MP.OnDesync += () => { /* freeze UI; dump path is logged by Core */ };
```

## Testing without RimWorld

| What | How |
| --- | --- |
| Build API + examples | `dotnet build RimworldMP.sln -c Release` (no `RIMWORLD_DIR`) |
| Example projects | `examples/CompatHello`, `examples/CompatHostIo` define `NO_GAME_REFS` |
| Registration harness | `dotnet test tests/DeterminismHarness -c Release` — see `ApiRegistrationTests` |
| Reset between tests | `MP.ResetForTests()` |

Examples never reference Verse/Harmony. The main mod uses `RIMWORLDMP_NO_GAME_REFS` when game DLLs are missing; that is separate from example `NO_GAME_REFS`.

## Limitations (honest)

- SyncMethod registration is an **opt-in surface**. Core apply wiring for arbitrary third-party methods is still stub-level; authors should also enqueue known opcodes where possible.
- FieldWatch change emission requires a session binder; offline it only detects changes.
- No Workshop-wide compatibility promise — XML/UI-only mods are usually fine; sim-touching C# needs this API or Harmony patches.
- Do not use `System.Random`, wall-clock seeds, or unordered collections on the apply path.
- Dedicated/headless host, Steam networking, and per-map async ticks are out of scope.
- Live Verse proof still needs `RIMWORLD_DIR` and a RimWorld 1.7 install.

## Checklist

- [ ] Project references `RimworldMP.API` only (unless Core is required)
- [ ] Mutating entry points marked `[SyncMethod]` and registered at load
- [ ] Dialog fields marked `[SyncField]` and wrapped in FieldWatch
- [ ] Host I/O goes through `HostInject` / host gate + synced apply
- [ ] DeterminismHarness / example project builds green without game refs

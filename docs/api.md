# RimworldMP.API

Public surface for third-party mods. Reference `RimworldMP.API` only — do not soft-depend on Core/Net unless you need internals.

## Session flags

| Member | Meaning |
| --- | --- |
| `MP.Enabled` | Assemblies loaded |
| `MP.IsInSession` | Lockstep session active |
| `MP.IsHost` | This peer is the in-game host |
| `MP.LocalPlayerId` | Player id, or `-1` offline |
| `MP.Tick` | Current lockstep tick |

## Commands

```csharp
MP.ExecuteCommand(opcode, payloadBytes);
```

Opcodes live in Core (`CommandOpcode`). Prefer documented helpers / SyncMethod registration over raw opcodes when possible.

## SyncMethod registration

```csharp
MP.RegisterSyncMethod(typeof(MyMod).GetMethod(nameof(MyMod.ApplyHostResult))!);
```

Registrations are discoverable via `MP.RegisteredSyncMethodKeys`. Host-only I/O should compute on the host then inject a synced result at tick T.

## Desync

```csharp
MP.OnDesync += () => { /* freeze UI, show dump path from logs */ };
```

Local dump layout (never uploaded): [desync-dumps.md](./desync-dumps.md).

Dumps include RNG counters and last-N commands (`.log` + `.cmds.json`) under the local RimworldMP desync folder.

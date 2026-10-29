# Track A P1 — SyncMethod, colony applicators, Rand, JIP scaffolding

Offline / `NO_GAME_REFS` work against G7–G9 (+ JIP create/load scaffolding) from the theory checkup.

## SyncMethod (execute, not register-only)

| Piece | Detail |
| --- | --- |
| Opcode | `CommandOpcode.SyncMethodInvoke = 90` (`MP.SyncMethodInvokeOpcode`) |
| Codec | `SyncMethodCodec` — key + typed args (null/bool/int/long/float/double/string/byte[]) |
| Schedule | `MP.CallSyncMethod(method, args…)` → command queue when in session; immediate offline |
| Apply | `CommandApplicator` → `MP.ApplySyncMethodPayload` |
| Harness | `SyncMethodTests` — codec, offline invoke, lockstep host/client roundtrip |

Authors still `RegisterSyncMethod` at mod init; all peers must register the same keys.

## Colony applicators (map/thing ids)

Payloads for bills / research / trade / caravan now carry **map id** (+ existing thing ids). Shadow store (`ColonyStateStore`) records map-scoped bills/trades/caravans with **ordered** caravan pawn lists (no HashSet iteration for sim). Verse apply stubs use `FindMap(mapId)` — never `Find.CurrentMap` on the apply path. Trade deal commit / CaravanMaker remain stubs until `RIMWORLD_DIR`.

## Rand path

`SyncedRand` wrappers: `Chance`, `Bool`, `Range(float)`, `RangeInclusive`, `Element`, `PushState`/`PopState`. Harmony prefixes (game refs) cover Value / Range / Chance / Bool and increment named desync counters. Harness proves **draw order** changes `DesyncDetector.SnapshotHash` even at equal conceptual intent.

## JIP create/load scaffolding

| Piece | Detail |
| --- | --- |
| `SaveBlobPipeline` | Create from `ISaveLoad.CaptureSaveBlob` → chunk → `CompleteOrThrow` → load |
| Fail-loud | Duplicate chunk, missing chunk, length mismatch, Done without complete, non-empty blob without loader |
| Session | `JoinPointDone` uses `CompleteOrThrow` (no silent “empty lobby” on incomplete transfer) |
| Host fallback | No `JoinPointSource` → capture via `SaveLoad` / `GameLoad.Current` |

## Still needs `RIMWORLD_DIR`

1. Live `VerseSaveLoad` vs `GameDataSaveLoader` / `GenFilePaths` on 1.7.
2. TradeAccept / CaravanForm Verse commits (beyond shadow + log stub).
3. Remaining `Verse.Rand` APIs (Gaussian, MTB, Seed push/pop on Verse’s own stack).
4. Harmony type-check for Chance/Bool signatures on the installed 1.7 build.
5. Two-client smoke with SyncMethod + colony UI.

## Harness

`dotnet test tests/DeterminismHarness -c Release` (CI forces `RIMWORLDMP_NO_GAME_REFS=true`).

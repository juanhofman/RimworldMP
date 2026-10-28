# Architecture

Canonical plan: Project store `docs/multiplayer-architecture.md`.

Locks: RimWorld **1.7** · `net10.0` · Harmony **3** · shared colony · LAN-first TCP · in-game host · PackageId `RimworldMP.Core` · MIT

## Phase status

| Phase | Status |
| --- | --- |
| 0 Skeleton | Done |
| 1 Smallest playable MP | Done (merged) |
| 2 Colony playable | Done (merged) |
| 3 Broader compatibility | Done (merged) |
| 4 Harden | In progress on `phase-4-harden` |
| 5 Release-ready | Next |

### Phase 2 highlights

- Bills / research / trade / caravan opcodes + shadow stores + Harmony stubs
- Join-in-progress chunked transfer (`JoinPoint*`), command log catch-up
- Desync dumps with RNG counters + `.cmds.json`
- `RimworldMP.API` SyncMethod registration + `docs/api.md` + example compat stubs
- Protocol **v2** (`RMP2` handshake); LAN TCP retained (LiteNetLib deferred)

# Architecture

Canonical plan lives in the RimworldMP Project store (`docs/multiplayer-architecture.md`). Summary of locks:

- RimWorld **1.7 only** · `net10.0` · Harmony **3**
- Shared **single colony** · **LAN-first** `INetTransport` · **in-game host only**
- PackageId `RimworldMP.Core` · MIT

## Phase 1 status

Implemented in-repo (compiles without game via `RIMWORLDMP_NO_GAME_REFS`):

| Area | Status |
| --- | --- |
| LAN TCP `INetTransport` + handshake | Done (`LanTcpTransport`, protocol v1 frames) |
| Host lobby + one client join | Done (`SessionFacade` / `MpLobbyActions`) |
| Tick gate + command batches | Done (`TickGate`, `MpSession`) |
| Pause / speed / draft / job / zone / restrict opcodes | Done (serialize + host stage + apply stubs) |
| Seeded `SyncedRand` + Rand Harmony stubs | Done (patches need `RIMWORLD_DIR`) |
| Hash desync → freeze + log path | Done (`DesyncDetector`) |
| Determinism harness | Done (`tests/DeterminismHarness`) |

Still needs a real RimWorld 1.7 install (`RIMWORLD_DIR`) to:

- Compile / run Verse Harmony patches and command applicators against live types
- Smoke-test host+client in-game (DevMode lobby actions)
- Validate zone apply beyond log stubs and long-session desync exit criteria

Join-in-progress save transfer, bills/research/trade, and LiteNetLib remain Phase 2.

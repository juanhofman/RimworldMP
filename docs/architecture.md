# Architecture

Canonical plan: Project store `docs/multiplayer-architecture.md`.

Locks: RimWorld **1.7** · `net10.0` · Harmony **3** (`Lib.Harmony.Ref` `3.0.0-rc.3` / Workshop ≥ v3.0.1) · shared colony · LAN-first TCP · in-game host · PackageId `RimworldMP.Core` · MIT

## Phase status (v0.6.0)

| Phase | Status |
| --- | --- |
| 0 Skeleton | Done |
| 1 Smallest playable MP | Done ([PR #1](https://github.com/juanhofman/RimworldMP/pull/1)) |
| 2 Colony playable | Done ([PR #2](https://github.com/juanhofman/RimworldMP/pull/2)) |
| 3 Broader compatibility | Done ([PR #3](https://github.com/juanhofman/RimworldMP/pull/3)) |
| 4 Harden | Done ([PR #4](https://github.com/juanhofman/RimworldMP/pull/4)) |
| 5 Release-ready scaffold | Done ([PR #5](https://github.com/juanhofman/RimworldMP/pull/5)) — v0.5.0 |
| P0 sim-correctness | Done ([PR #8](https://github.com/juanhofman/RimworldMP/pull/8)) — see [p0-sim-correctness.md](./p0-sim-correctness.md) |
| LAN launcher MVP | Done ([PR #7](https://github.com/juanhofman/RimworldMP/pull/7)) — see [launcher-mvp.md](./launcher-mvp.md) |
| v0.6.0 packaging | UI + launcher release — [release-notes-v0.6.0.md](./release-notes-v0.6.0.md) |

Protocol: magic `RMP3`, version **3** (modlist hard-gate). LiteNetLib / Steam / dedicated host deferred by design. MaxClients default **4**. Launcher handoff: [launcher-handoff.md](./launcher-handoff.md). **Known limitation:** untested in live RW 1.7 without game refs.

# Architecture

Canonical plan: Project store `docs/multiplayer-architecture.md`.

Locks: RimWorld **1.7** · `net10.0` · Harmony **3** (`Lib.Harmony.Ref` `3.0.0-rc.3` / Workshop ≥ v3.0.1) · shared colony · LAN-first TCP · in-game host · PackageId `RimworldMP.Core` · MIT

## Phase status (v0.7.0)

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
| SyncMethod + colony catalog | Done ([PR #17](https://github.com/juanhofman/RimworldMP/pull/17)) |
| Compat API + examples | Done ([PR #18](https://github.com/juanhofman/RimworldMP/pull/18)) |
| Session QoL | Done ([PR #19](https://github.com/juanhofman/RimworldMP/pull/19)) |
| LiteNetLib transport | Done ([PR #16](https://github.com/juanhofman/RimworldMP/pull/16)) |
| Code quality (zero-warning) | Done ([PR #20](https://github.com/juanhofman/RimworldMP/pull/20)) |
| v0.7.0 packaging | Offline feature cut (GitHub **pre-release**) — [release-notes-v0.7.0.md](./release-notes-v0.7.0.md) |

Protocol: magic `RMP3`, version **4** (MinSupported **3**; modlist hard-gate). Game transport: **TCP default** or opt-in **LiteNetLib** UDP — [transport-litenetlib.md](./transport-litenetlib.md). Steam / dedicated host still deferred. MaxClients default **4**. Launcher handoff: [launcher-handoff.md](./launcher-handoff.md). **Known limitation:** requires RimWorld 1.7 for live play; Verse apply still scaffolded.

**Feature completeness (maintained):** [feature-completeness.md](./feature-completeness.md) — lockstep, commands, JIP, launcher, mod sync, transports, API, DLC, desync, QoL.

# RimworldMP 0.7.0 — Offline feature cut

**PackageId:** `RimworldMP.Core` · **License:** MIT · **Target:** RimWorld **1.7** only · **GitHub:** **pre-release**

## Highlights

| Area | Included |
| --- | --- |
| SyncMethod | Opcode `90` schedule + applicator invoke; `[SyncMethod]` scan; `ColonyCommands` — [#17](https://github.com/juanhofman/RimworldMP/pull/17) |
| Colony catalog | Full offline designator/policy/schedule/bill/research/trade/caravan opcodes + shadow stores |
| Compat API | Public `[SyncMethod]` / `[SyncField]`, `FieldWatch`, `HostInject`; examples CompatHello / CompatHostIo — [#18](https://github.com/juanhofman/RimworldMP/pull/18) |
| Session QoL | Chat, display names, host kick, ready hard gate, recovery polish, EN/NL — [#19](https://github.com/juanhofman/RimworldMP/pull/19) |
| Transport | LAN TCP default + **LiteNetLib** UDP opt-in (`INetTransport`); protocol **v4** / MinSupported **3** — [#16](https://github.com/juanhofman/RimworldMP/pull/16) |
| Desync | Richer local dumps (tick/protocol/peer/modlist/RNG/last-N cmds); RNG opinion hash |
| Quality | Zero-warning builds (`TreatWarningsAsErrors`, Recommended analyzers) — [#20](https://github.com/juanhofman/RimworldMP/pull/20) |
| Prior (0.6) | Avalonia LAN launcher, ModSync, P0 sim-correctness, in-mod lobby |

## Install (short)

1. Install **Harmony 3** (Workshop `2009463077` or ≥ v3.0.1).
2. Build Release (or use a release zip) and copy `mod/` → `RimWorld/Mods/RimworldMP`.
3. Optional: run `tools/RimworldMP.Launcher` to prepare mods, host/join, and launch.
4. In mod list: **Harmony**, then **RimworldMP**. Same ordered modlist on every peer.
5. Optional LiteNetLib: set `MpConfig.TransportKind` / handoff `transport` — see [transport-litenetlib.md](./transport-litenetlib.md).

Full steps: [install-play-guide.md](./install-play-guide.md) · Smoke: [smoke-checklist.md](./smoke-checklist.md) · Matrix: [feature-completeness.md](./feature-completeness.md)

## Known limitations

- **Requires a RimWorld 1.7 install for live play.** Verse apply paths (draft/job/zone/bill/research/Rand/lobby Immediate GUI, trade/caravan commit, world fingerprint fill) remain **scaffolded**. Compiling full Harmony patches and proving multi-hour desync-free co-op needs `RIMWORLD_DIR` / game DLLs.
- Without game refs the solution builds under `RIMWORLDMP_NO_GAME_REFS` stubs (CI / VMs) — offline harness only, not for serious play.
- Steam networking and dedicated/headless host remain out of scope per prefs.
- This tag is published as a GitHub **pre-release** until live binding smoke lands.

## Changelog lineage

Phases 1–5 scaffold (**0.5.0**) → UI + launcher (**0.6.0**) → SyncMethod / Compat / Session QoL / LiteNetLib / quality (**0.7.0**). See root [CHANGELOG.md](../CHANGELOG.md).

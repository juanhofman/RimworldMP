# Changelog

## Unreleased

Post-0.7.0 work. See [docs/feature-completeness.md](docs/feature-completeness.md). Stay on **0.7.0** (do not bump VERSION for binding scaffold).

### Added
- **Verse binding scaffold:** `VerseBind/` apply shapes + `VerseBindingRegistry` catalog, checklist ([docs/verse-binding-checklist.md](docs/verse-binding-checklist.md)); NO_GAME_REFS stubs keep harness green; trade/caravan/policy/DLC capture left Scaffold (no invented Prefixes); DeterminismHarness `VerseBindingTests`

## 0.7.0 — Offline feature cut (2026-10-30)

Deliberate packaging of the offline feature slice landed after 0.6.0. Semver **0.7.0**; PackageId remains **`RimworldMP.Core`**.

**Requires a RimWorld 1.7 install for live play; Verse apply paths remain scaffolded.** Published as a GitHub **pre-release** until live binding smoke.

### Added
- **LiteNetLib** `INetTransport` (LAN reliable UDP) selectable via `MpConfig.TransportKind` / handoff `transport`; TCP remains default fallback ([docs/transport-litenetlib.md](docs/transport-litenetlib.md)) — [#16](https://github.com/juanhofman/RimworldMP/pull/16)
- Protocol **v4** (magic still `RMP3`, MinSupported **3**): optional handshake trailer + paced JIP chunk sizing for LiteNetLib
- Client connect reconnect/backoff (`ReconnectPolicy`) for transient UDP failures
- **Session QoL:** synced chat / system notices (out-of-band), player display names + handoff `playerDisplayName`, host kick, ready-check hard gate on unpause, recovery polish, EN/NL mismatch strings ([docs/session-qol.md](docs/session-qol.md)). Lobby UI exposes kick / ready / chat. DeterminismHarness `SessionQolTests` — [#19](https://github.com/juanhofman/RimworldMP/pull/19)
- **SyncMethod execution** via `CommandOpcode.SyncMethodInvoke` (90): `MP.CallSyncMethod` / `SyncMethodCodec` / applicator invoke ([docs/api.md](docs/api.md), [docs/mod-author-api.md](docs/mod-author-api.md)) — [#17](https://github.com/juanhofman/RimworldMP/pull/17)
- **Full colony command catalog** offline: designators/forbid, work/timetable/food/outfit/drug/medical/hostility policies, bill reorder, research stop, trade cancel, caravan disband; shadow stores + `ColonyCommands` API constants
- **Public Compat/API for mod authors:** `[SyncMethod]` / `[SyncField]`, `FieldWatch`, `HostInject`, attribute/type/assembly registration ([docs/mod-author-api.md](docs/mod-author-api.md)). Examples `CompatHello` / `CompatHostIo` — [#18](https://github.com/juanhofman/RimworldMP/pull/18)
- **RNG opinion hash** in desync fingerprint (draw-order sensitive); Chance/Bool wrappers + Harmony stubs
- **JIP** `SaveBlobPipeline` + fail-loud chunk reassembly (`CompleteOrThrow`)
- **Feature completeness matrix** + command catalog / Verse-blocked notes ([docs/feature-completeness.md](docs/feature-completeness.md), [docs/track-a-p1-syncmethod.md](docs/track-a-p1-syncmethod.md))
- **Richer local desync dumps:** tick/protocol/peer ids, short modlist hash, RNG seed/counters, last-N commands, optional batch ring; never uploaded ([docs/desync-dumps.md](docs/desync-dumps.md))
- **Zero-warning builds:** `TreatWarningsAsErrors` / `AnalysisLevel=latest` / `AnalysisMode=Recommended` in `Directory.Build.props`; CI builds with warnings-as-errors ([docs/code-quality.md](docs/code-quality.md)) — [#20](https://github.com/juanhofman/RimworldMP/pull/20)
- DeterminismHarness: Session QoL / LiteNetLib / SyncMethod / catalog / JIP / draw-order / API registration / desync dump tests

### Notes / known limitations
- **Requires RimWorld 1.7 install for live play.** Verse/Harmony apply paths (draft/job/zone/bill/research/Rand/lobby Immediate GUI, trade/caravan commit, world fingerprint) remain scaffolded under `RIMWORLDMP_NO_GAME_REFS` until `RIMWORLD_DIR` binding smoke.
- Steam networking and dedicated/headless host remain out of scope per prefs.

## 0.6.0 — UI + launcher release (2026-10-29)

Workshop-style packaging for the LAN launcher + P0 sim stack. Semver **0.6.0**; PackageId remains **`RimworldMP.Core`**.

### Added
- **LAN launcher MVP** (`tools/RimworldMP.Launcher`): locate RimWorld, prepare Harmony → RimworldMP mod order + hash, UDP host discovery, write `launcher-handoff.json`, launch game ([docs/launcher-mvp.md](docs/launcher-mvp.md), [docs/launcher-handoff.md](docs/launcher-handoff.md))
- **Mod manager + LAN mod sync** (`tools/RimworldMP.ModSync` + Avalonia Mods hooks): host share TCP **30557**, client compare/sync with content-hash verify, Workshop cache secondary, SteamCMD stub only ([docs/mod-manager-lan-sync.md](docs/mod-manager-lan-sync.md))
- **P0 sim-correctness:** desync fingerprint folds protocol + tick + RNG seed/draws + named counters + reserved world hash slot; JIP `ISaveLoad` / `VerseSaveLoad` (fails loud if blob present without loader); TCP join hard-gate on protocol + modlist hash (`RMP3` / v3); JobGive/zone payloads carry `mapId` (no `Find.CurrentMap`); MaxClients default **4** via `MpConfig`; non-DevMode lobby (F10 / corner button) + launcher handoff ([docs/p0-sim-correctness.md](docs/p0-sim-correctness.md))
- Release packaging: `mod/About` Preview/About placeholders, install + smoke docs refresh, [release notes](docs/release-notes-v0.6.0.md)

### Fixed
- Pin compile-time Harmony to NuGet `Lib.Harmony.Ref` `3.0.0-rc.3` (matches HarmonyRimWorld v3.0.1); stop using `Lib.Harmony` `3.0.0-rc.2`

### Notes / known limitations
- **Untested in live RimWorld 1.7** — Verse/Harmony apply paths and JIP save load need `RIMWORLD_DIR` / game DLLs
- LiteNetLib / Steam / dedicated host remain out of scope
- Upcoming launcher / in-mod UI polish may land on `main` after this packaging cut; rebase tags as needed

## 0.5.0 — Release-ready scaffold (2026-10-28)

First tagged release of the from-scratch RimworldMP lockstep stack for RimWorld **1.7**.

### Added
- Phases 1–4: LAN TCP lobby, lockstep commands, JIP, colony + DLC surfaces, Compat helpers, ready/modlist checks, recovery rejoin, soak harness
- Docs: install/play guide, smoke checklist, API, per-phase notes
- Example compat stubs (`CompatHello`, `CompatHostIo`)

### Notes
- In-game Harmony patches and Verse apply paths require `RIMWORLD_DIR` / game DLLs
- LiteNetLib / Steam / dedicated host remain out of scope
- Protocol magic `RMP2`, version **2**

## 0.1.0 — Phase 0/1 foundation

Scaffold + smallest playable MP primitives (merged via PR #1).

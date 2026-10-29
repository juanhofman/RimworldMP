# Changelog

## Unreleased

<<<<<<< HEAD
Stay on **0.6.0** until a deliberate 0.7 cut (LiteNetLib / session QoL / packaging). See [docs/feature-completeness.md](docs/feature-completeness.md).

### Added
- **SyncMethod execution** via `CommandOpcode.SyncMethodInvoke` (90): `MP.CallSyncMethod` / `SyncMethodCodec` / applicator invoke ([docs/api.md](docs/api.md), [docs/mod-author-api.md](docs/mod-author-api.md))
- **Full colony command catalog** offline: designators/forbid, work/timetable/food/outfit/drug/medical/hostility policies, bill reorder, research stop, trade cancel, caravan disband; shadow stores + `ColonyCommands` API constants
- **Public Compat/API for mod authors:** `[SyncMethod]` / `[SyncField]`, `FieldWatch`, `HostInject`, attribute/type/assembly registration ([docs/mod-author-api.md](docs/mod-author-api.md)). Examples `CompatHello` / `CompatHostIo`
- **RNG opinion hash** in desync fingerprint (draw-order sensitive); Chance/Bool wrappers + Harmony stubs
- **JIP** `SaveBlobPipeline` + fail-loud chunk reassembly (`CompleteOrThrow`)
- **Feature completeness matrix** + command catalog / Verse-blocked notes ([docs/feature-completeness.md](docs/feature-completeness.md), [docs/track-a-p1-syncmethod.md](docs/track-a-p1-syncmethod.md))
- **Richer local desync dumps:** tick/protocol/peer ids, short modlist hash, RNG seed/counters, last-N commands, optional batch ring; never uploaded ([docs/desync-dumps.md](docs/desync-dumps.md))
- **Zero-warning builds:** `TreatWarningsAsErrors` / `AnalysisLevel=latest` in `Directory.Build.props`; CI builds with warnings-as-errors
- DeterminismHarness: SyncMethod / catalog / JIP / draw-order / API registration / desync dump tests

### In flight
=======
<<<<<<< HEAD
Stay on **0.6.0** until a coherent 0.7 feature slice lands (SyncMethod applicators, LiteNetLib, session QoL chat/kick). See [docs/feature-completeness.md](docs/feature-completeness.md).

### Added
- **Public Compat/API for mod authors:** `[SyncMethod]` / `[SyncField]`, `FieldWatch` begin/end stubs, `HostInject` host-only+inject helpers, attribute/type/assembly registration, XML docs on `RimworldMP.API` ([docs/mod-author-api.md](docs/mod-author-api.md)). Examples `CompatHello` / `CompatHostIo` build with `NO_GAME_REFS`. DeterminismHarness `ApiRegistrationTests` covers registration, watch, and host-inject. `TreatWarningsAsErrors` enabled repo-wide; no obsolete Compat attribute duplicates.
=======
Stay on **0.6.0** until a coherent 0.7 feature slice lands (SyncMethod applicators, session QoL chat/kick, Compat API expansions). See [docs/feature-completeness.md](docs/feature-completeness.md).

### Added
- **LiteNetLib** `INetTransport` (LAN reliable UDP) selectable via `MpConfig.TransportKind` / handoff `transport`; TCP remains default fallback ([docs/transport-litenetlib.md](docs/transport-litenetlib.md))
- Protocol **v4** (magic still `RMP3`, MinSupported **3**): optional handshake trailer + paced JIP chunk sizing for LiteNetLib
- Client connect reconnect/backoff (`ReconnectPolicy`) for transient UDP failures
>>>>>>> 2abba58 (Add LiteNetLib LAN transport behind INetTransport.)
- **Feature completeness matrix:** Feature → Done / Partial / Blocked on `RIMWORLD_DIR` / Won’t do per prefs ([docs/feature-completeness.md](docs/feature-completeness.md))
- **Richer local desync dumps:** tick/protocol/peer ids, short modlist hash, RNG seed/counters, last-N commands, optional batch ring; never uploaded ([docs/desync-dumps.md](docs/desync-dumps.md)). Freeze overlay shows dump path. DeterminismHarness `DesyncDumpTests` covers forced fingerprint mismatch sections.

### In flight (not on `main` yet / incomplete)
<<<<<<< HEAD
- SyncMethod colony applicators and RNG thicken (API registration + invoke helpers exist; Core→Verse apply pipeline incomplete)
>>>>>>> 2ccd06b (Add LiteNetLib LAN transport behind INetTransport.)
- LiteNetLib LAN transport behind `INetTransport`
=======
- SyncMethod colony applicators and RNG thicken (registration exists; invoke/apply pipeline incomplete)
- Compat API expansions + richer example mods
>>>>>>> 2abba58 (Add LiteNetLib LAN transport behind INetTransport.)
- Session QoL: chat, host kick, ready polish

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

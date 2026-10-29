# RimworldMP

[![CI](https://github.com/juanhofman/RimworldMP/actions/workflows/ci.yml/badge.svg)](https://github.com/juanhofman/RimworldMP/actions/workflows/ci.yml)

From-scratch **RimWorld 1.7** multiplayer: **shared colony**, **LAN-first**, **in-game host**.

Not a fork of Zetrith/RWMT. **v0.6.0** (UI + launcher release) + LiteNetLib LAN option: Avalonia LAN launcher with mod manager + LAN mod sync, protocol **v4** lockstep (MinSupported **3**) with modlist join hard-gate, TCP default / LiteNetLib opt-in, P0 sim-correctness, in-mod lobby (F10 / button / launcher handoff), [local desync dumps](docs/desync-dumps.md). PackageId **`RimworldMP.Core`**. License **MIT**.

**Known limitation:** untested in live RimWorld 1.7 — full Verse patches need `RIMWORLD_DIR` / game DLLs. CI (no game install): [`docs/ci.md`](docs/ci.md).

## Locked scope

| Item | Choice |
| --- | --- |
| Game | RimWorld **1.7 only** (`.NET 10` / `net10.0`) |
| Co-op | Shared **single colony** |
| Net | **LAN TCP** (default) or **LiteNetLib** UDP behind `INetTransport` |
| Host | **In-game host only** |
| PackageId | `RimworldMP.Core` |
| License | MIT |
| Harmony | Hard dep on Workshop Harmony **3** (≥ v3.0.1 / lib `3.0.0-rc.3`); compile with `Lib.Harmony.Ref` — do **not** bundle `0Harmony.dll` |

## Quick start

```bash
dotnet build RimworldMP.sln -c Release
dotnet test tests/DeterminismHarness -c Release
dotnet build tools/RimworldMP.Launcher/RimworldMP.Launcher.sln -c Release

# Full Verse patches:
export RIMWORLD_DIR="/path/to/RimWorld"
dotnet build RimworldMP.sln -c Release
# Copy mod/ → RimWorld/Mods/RimworldMP ; enable Harmony then RimworldMP
```

Play guide: [`docs/install-play-guide.md`](docs/install-play-guide.md) · Release notes: [`docs/release-notes-v0.6.0.md`](docs/release-notes-v0.6.0.md) · Smoke: [`docs/smoke-checklist.md`](docs/smoke-checklist.md) · Launcher: [`docs/launcher-mvp.md`](docs/launcher-mvp.md) · P0: [`docs/p0-sim-correctness.md`](docs/p0-sim-correctness.md)

## Layout

```
src/           RimworldMP, API, Core, Net, Compat
mod/           Workshop-style mod folder (About + 1.7/Assemblies)
tools/         RimworldMP.Launcher (LAN locate / mods / host / join)
examples/      CompatHello, CompatHostIo
tests/         DeterminismHarness
docs/          architecture, API, install, release notes, phase notes
```

## Docs

- [`docs/architecture.md`](docs/architecture.md)
<<<<<<< HEAD
- [`docs/api.md`](docs/api.md) · [`docs/mod-author-api.md`](docs/mod-author-api.md)
=======
- [`docs/transport-litenetlib.md`](docs/transport-litenetlib.md)
- [`docs/api.md`](docs/api.md)
>>>>>>> 2abba58 (Add LiteNetLib LAN transport behind INetTransport.)
- [`docs/install-play-guide.md`](docs/install-play-guide.md)
- [`examples/`](examples/) — CompatHello / CompatHostIo (`NO_GAME_REFS`)
- [`CHANGELOG.md`](CHANGELOG.md)

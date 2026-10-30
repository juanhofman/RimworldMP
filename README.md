# RimworldMP

[![CI](https://github.com/juanhofman/RimworldMP/actions/workflows/ci.yml/badge.svg)](https://github.com/juanhofman/RimworldMP/actions/workflows/ci.yml)

From-scratch **RimWorld 1.7** multiplayer: **shared colony**, **LAN-first**, **in-game host**.

Not a fork of Zetrith/RWMT. **v0.7.0** (offline feature cut, GitHub **pre-release**): SyncMethod + full colony catalog, Compat API, Session QoL, LiteNetLib LAN transport, richer [desync dumps](docs/desync-dumps.md), zero-warning builds — on top of the Avalonia LAN launcher / ModSync / P0 stack from 0.6. Protocol **v4** (MinSupported **3**); TCP default / LiteNetLib opt-in. PackageId **`RimworldMP.Core`**. License **MIT**.

**Status:** GitHub **pre-release** — offline CI green; **not** proven in live RimWorld 1.7. Verse/Harmony apply remains scaffolded ([verse-binding-checklist.md](docs/verse-binding-checklist.md)); needs `RIMWORLD_DIR` / game DLLs for live play. Workshop live download / SteamCMD is unsupported (LAN ModSync + local Workshop cache only). CI: [`docs/ci.md`](docs/ci.md).

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

Play guide: [`docs/install-play-guide.md`](docs/install-play-guide.md) · Release notes: [`docs/release-notes-v0.7.0.md`](docs/release-notes-v0.7.0.md) · Smoke: [`docs/smoke-checklist.md`](docs/smoke-checklist.md) · Completeness: [`docs/feature-completeness.md`](docs/feature-completeness.md) · Launcher: [`docs/launcher-mvp.md`](docs/launcher-mvp.md)

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
- [`docs/transport-litenetlib.md`](docs/transport-litenetlib.md)
- [`docs/api.md`](docs/api.md) · [`docs/mod-author-api.md`](docs/mod-author-api.md)
- [`docs/install-play-guide.md`](docs/install-play-guide.md)
- [`examples/`](examples/) — CompatHello / CompatHostIo (`NO_GAME_REFS`)
- [`CHANGELOG.md`](CHANGELOG.md)

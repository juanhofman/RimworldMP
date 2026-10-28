# RimworldMP

From-scratch **RimWorld 1.7** multiplayer: **shared colony**, **LAN-first**, **in-game host**.

Not a fork of Zetrith/RWMT. **v0.5.0** ships Phases 1–5 as a release-ready scaffold: LAN TCP lockstep, colony + DLC command surfaces, join-in-progress, Compat API, ready/modlist checks, and a green offline harness. Full in-game proof still needs `RIMWORLD_DIR`.

## Locked scope

| Item | Choice |
| --- | --- |
| Game | RimWorld **1.7 only** (`.NET 10` / `net10.0`) |
| Co-op | Shared **single colony** |
| Net | **LAN TCP** behind `INetTransport` |
| Host | **In-game host only** |
| PackageId | `RimworldMP.Core` |
| License | MIT |
| Harmony | Hard dependency on Harmony **3** — do **not** bundle `0Harmony.dll` |

## Quick start

```bash
dotnet build RimworldMP.sln -c Release
dotnet test tests/DeterminismHarness -c Release

# Full Verse patches:
export RIMWORLD_DIR="/path/to/RimWorld"
dotnet build RimworldMP.sln -c Release
# Copy mod/ → RimWorld/Mods/RimworldMP ; enable Harmony then RimworldMP
```

Play guide: [`docs/install-play-guide.md`](docs/install-play-guide.md) · Smoke: [`docs/smoke-checklist.md`](docs/smoke-checklist.md) · Release notes: [`docs/RELEASE_NOTES_0.5.0.md`](docs/RELEASE_NOTES_0.5.0.md)

## Layout

```
src/           RimworldMP, API, Core, Net, Compat
mod/           Workshop-style mod folder (1.7/Assemblies)
examples/      CompatHello, CompatHostIo
tests/         DeterminismHarness
docs/          architecture, API, phase notes, install guide
```

## Docs

- [`docs/architecture.md`](docs/architecture.md)
- [`docs/api.md`](docs/api.md)
- [`CHANGELOG.md`](CHANGELOG.md)

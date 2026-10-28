# RimworldMP

From-scratch **RimWorld 1.7** multiplayer: **shared colony**, **LAN-first**, **in-game host**.

Not a fork of Zetrith/RWMT. Deterministic lockstep co-op. This repo currently ships **Phase 2** — colony-playable scaffolding (bills/research/trade/caravans, JIP, API) (LAN TCP lobby, command stream, tick gate, seeded Rand hooks, desync freeze + log). Full in-game verification needs RimWorld assemblies via `RIMWORLD_DIR`.

## Locked scope

| Item | Choice |
| --- | --- |
| Game | RimWorld **1.7 only** (`.NET 10` / `net10.0`) |
| Co-op | Shared **single colony** |
| Net | **LAN TCP** behind `INetTransport` (LiteNetLib later) |
| Host | **In-game host only** (no dedicated soon) |
| PackageId | `RimworldMP.Core` |
| License | MIT |
| Harmony | **Hard dependency** on Harmony **3** — do **not** bundle `0Harmony.dll` |

## Layout

```
src/
  RimworldMP/        # Mod entry + Harmony patches + lobby actions
  RimworldMP.API/    # Public sync API surface
  RimworldMP.Core/   # Session, commands, tick gate, RNG, desync
  RimworldMP.Net/    # INetTransport + LAN TCP + loopback
mod/
  About/About.xml
  LoadFolders.xml
  1.7/Assemblies/    # build output
tests/
  DeterminismHarness/  # xUnit: serialize, RNG, tick gate, loopback, LAN smoke
```

## Build

Requires [.NET SDK 10](https://dotnet.microsoft.com/download).

```bash
# API + Core + Net + stubbed main assembly (no RimWorld install):
dotnet build RimworldMP.sln

# Full game integration (Harmony patches against Verse):
export RIMWORLD_DIR="/path/to/RimWorld"   # folder with Assembly-CSharp.dll, Waylith.dll, UnityStubs.dll
dotnet build RimworldMP.sln

# Determinism harness (no game required):
dotnet test tests/DeterminismHarness
```

Without `RIMWORLD_DIR` (or `refs/RimWorld/`), the main project compiles under `RIMWORLDMP_NO_GAME_REFS` — networking/session/command code still builds; Verse Harmony patches are excluded.

Copy or symlink `mod/` into your RimWorld `Mods` folder. Enable **Harmony** above **RimworldMP** in the mod list.

### `RIMWORLD_DIR` requirements

| Need | Why |
| --- | --- |
| `Assembly-CSharp.dll` | Verse / RimWorld types for patches + command apply |
| `Waylith.dll` | 1.7 engine ref |
| `UnityStubs.dll` | `UnityEngine` stub surface — keep `using UnityEngine;`, do not add old Unity DLLs |
| Workshop **Harmony 3** | Hard dependency; load first; never ship `0Harmony.dll` |

Optional: drop those three game DLLs under `refs/RimWorld/` instead of setting `RIMWORLD_DIR` (gitignored).

## Phase 1 smoke test (once game refs exist)

1. Build with `RIMWORLD_DIR` set; install `mod/` + Harmony 3.
2. Launch **two** RimWorld 1.7 instances (or two machines on LAN) with the **same** modlist.
3. On host: DevMode → **RimworldMP: Host lobby (LAN)** (TCP port `30555`).
4. On client: DevMode → **RimworldMP: Join lobby localhost** (or join the host LAN IP — use a temporary debug action / future UI; default join target is `127.0.0.1`).
5. Start a **shared** crashlanded colony on the host first (join-in-progress save transfer is Phase 2 — both peers should load the same save / start together for now).
6. Exercise pause/speed, draft, ordered jobs, zones, restrict areas. Confirm both sims stay in lockstep.
7. On hash mismatch: sim freezes; desync dump under `%LocalAppData%/RimworldMP/desync/` (or equivalent).

LAN join to a remote host: call `MpLobbyActions.JoinAsync("<host-ip>")` from a debug action once you extend the join helper, or temporarily edit the localhost action.

## Harmony

Install [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) from the Workshop (v3.x for RimWorld 1.7). This mod declares a hard dependency and does not ship `0Harmony.dll`.

## Docs

See `docs/architecture.md`, `docs/api.md`, and `docs/phase-2-smoke-test.md`.

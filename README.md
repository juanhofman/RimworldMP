# RimworldMP

From-scratch **RimWorld 1.7** multiplayer: **shared colony**, **LAN-first**, **in-game host**.

Not a fork of Zetrith/RWMT. Deterministic lockstep is the long-term plan; this repo currently ships a **Phase 0 skeleton** (mod metadata, project layout, `INetTransport` + LAN placeholder). No full lockstep yet.

## Locked scope

| Item | Choice |
| --- | --- |
| Game | RimWorld **1.7 only** (`.NET 10` / `net10.0`) |
| Co-op | Shared **single colony** |
| Net | **LAN** behind `INetTransport` (TCP / LiteNetLib later) |
| Host | **In-game host only** (no dedicated soon) |
| PackageId | `RimworldMP.Core` |
| License | MIT |
| Harmony | **Hard dependency** on Harmony **3** — do **not** bundle `0Harmony.dll` |

## Layout

```
src/
  RimworldMP/        # Mod entry + Harmony bootstrap
  RimworldMP.API/    # Public sync API surface (stubs)
  RimworldMP.Core/   # Session / command / tick stubs
  RimworldMP.Net/    # INetTransport + LAN placeholder
mod/
  About/About.xml
  LoadFolders.xml
  1.7/Assemblies/    # build output
```

## Build

Requires [.NET SDK 10](https://dotnet.microsoft.com/download).

```bash
# API + Core + Net compile without RimWorld installed:
dotnet build RimworldMP.sln -p:BuildGameProject=false

# Full mod (needs game refs):
export RIMWORLD_DIR="/path/to/RimWorld"   # folder containing Assembly-CSharp.dll, Waylith.dll, UnityStubs.dll
dotnet build RimworldMP.sln
```

Copy or symlink `mod/` into your RimWorld `Mods` folder (or set your IDE output path). Enable **Harmony** above **RimworldMP** in the mod list.

Optional: drop game DLLs under `refs/RimWorld/` instead of setting `RIMWORLD_DIR` (those DLLs are gitignored).

## Harmony

Install [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) from the Workshop (v3.x for RimWorld 1.7). This mod declares a hard dependency and does not ship `0Harmony.dll`.

## Docs

See `docs/architecture.md` for the design plan.

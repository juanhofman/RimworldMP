# Install & play — RimWorld 1.7 (Workshop-style folder)

## Install

1. Install **Harmony 3** for RimWorld 1.7 (Workshop id `2009463077` or [GitHub release](https://github.com/pardeike/HarmonyRimWorld/releases) ≥ **v3.0.1**, which ships Harmony lib `3.0.0-rc.3`). This repo compiles against NuGet `Lib.Harmony.Ref` `3.0.0-rc.3` and does not ship `0Harmony.dll`.
2. Build this repo (see README) **or** copy a release `mod/` folder.
3. Copy / symlink the repo `mod/` directory to:
   - Steam: `…/RimWorld/Mods/RimworldMP`
   - Or your user Mods folder if you use one
4. Ensure `mod/1.7/Assemblies/` contains at least:
   - `RimworldMP.dll`
   - `RimworldMP.API.dll`
   - `RimworldMP.Core.dll`
   - `RimworldMP.Net.dll`
   - `RimworldMP.Compat.dll`
5. In the mod list: enable **Harmony**, then **RimworldMP**. Same ordered modlist on every peer.

### Full patches build

```bash
export RIMWORLD_DIR="/path/to/RimWorld"   # Assembly-CSharp.dll, Waylith.dll, UnityStubs.dll
dotnet build RimworldMP.sln -c Release
```

Without `RIMWORLD_DIR`, assemblies still build under `RIMWORLDMP_NO_GAME_REFS` (stubs; not for serious play).

## Host / join on LAN

1. Launch RimWorld 1.7 twice (or two PCs). Identical game build + DLC + modlist.
2. Enable DevMode on both.
3. **Host:** DevMode → RimworldMP → **Host lobby (LAN)** (listens `0.0.0.0:30555`).
4. Load or start the shared colony on the host.
5. **Client:** DevMode → **Join lobby localhost** (same machine) or call `MpLobbyActions.JoinAsync("<host-lan-ip>")`.
6. Client receives a join-in-progress freeze-frame when mid-session; load the save blob when game wiring is present, then catch up.
7. Optional: set matching modlist hashes and `session.SetReady(true)` on both before unpausing (Phase 4 ready gate).

## After join

Exercise pause/speed, draft, jobs, zones, bills, research. On desync: both freeze; dumps under `%LocalAppData%/RimworldMP/desync/` (`.log` + `.cmds.json`). Soft recovery: `SessionRecovery.RequestRejoin(session)` on the client.

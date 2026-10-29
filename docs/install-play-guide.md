# Install & play — RimWorld 1.7 (Workshop-style folder)

**Version:** v0.6.0 · **PackageId:** `RimworldMP.Core` · **License:** MIT

**Known limitation:** this stack is **untested in live RimWorld 1.7**. Full Harmony/Verse patches need a game install (`RIMWORLD_DIR`).

## Install the mod

1. Install **Harmony 3** for RimWorld 1.7 (Workshop id `2009463077` or [GitHub release](https://github.com/pardeike/HarmonyRimWorld/releases) ≥ **v3.0.1**, which ships Harmony lib `3.0.0-rc.3`). This repo compiles against NuGet `Lib.Harmony.Ref` `3.0.0-rc.3` and does not ship `0Harmony.dll`.
2. Build this repo (see below) **or** copy a release `mod/` folder.
3. Copy / symlink the repo `mod/` directory to:
   - Steam: `…/RimWorld/Mods/RimworldMP`
   - Or your user Mods folder if you use one
4. Ensure `mod/1.7/Assemblies/` contains at least:
   - `RimworldMP.dll`
   - `RimworldMP.API.dll`
   - `RimworldMP.Core.dll`
   - `RimworldMP.Net.dll`
   - `RimworldMP.Compat.dll`
5. Ensure `mod/About/` has `About.xml` (PackageId `RimworldMP.Core`) plus optional `Preview.png` / `About.png`.
6. In the mod list: enable **Harmony**, then **RimworldMP**. Same ordered modlist on every peer.

### Build

```bash
dotnet build RimworldMP.sln -c Release
dotnet test tests/DeterminismHarness -c Release
```

### Full patches build

```bash
export RIMWORLD_DIR="/path/to/RimWorld"   # Assembly-CSharp.dll, Waylith.dll, UnityStubs.dll
dotnet build RimworldMP.sln -c Release
```

Without `RIMWORLD_DIR`, assemblies still build under `RIMWORLDMP_NO_GAME_REFS` (stubs; not for serious play).

## LAN launcher (preferred setup)

External helper — local only; no telemetry. See [launcher-mvp.md](./launcher-mvp.md).

```bash
dotnet build tools/RimworldMP.Launcher/RimworldMP.Launcher.sln -c Release
dotnet run --project tools/RimworldMP.Launcher -- locate
dotnet run --project tools/RimworldMP.Launcher -- mods prepare
dotnet run --project tools/RimworldMP.Launcher -- hash   # peers must match
```

**Host**

```bash
dotnet run --project tools/RimworldMP.Launcher -- host --name "Colony Night"
```

Writes `launcher-handoff.json` (`role=host`, port **30555**), broadcasts on **30556**, launches RimWorld. Stop the beacon with Enter in the launcher terminal.

**Join**

```bash
dotnet run --project tools/RimworldMP.Launcher -- join
# or: ... -- join --ip 192.168.1.20
```

Handoff contract: [launcher-handoff.md](./launcher-handoff.md). Restore ModsConfig after a session with the launcher’s restore command when needed.

## Host / join in-game

Peers must share the same RimWorld **1.7** build, DLC, and **ordered modlist** (hash hard-gated at TCP join; protocol **v3** / magic `RMP3`). Default max clients: **4** (`MpConfig.MaxClients`).

### Preferred (no DevMode)

1. Use the LAN launcher to write `launcher-handoff.json`, **or**
2. In-game: click the **RimworldMP** button (lower-left) or press **F10** → Host / Join with matching modlist hash.

### DevMode fallback

1. Launch RimWorld 1.7 twice (or two PCs). Identical game build + DLC + modlist.
2. Enable DevMode on both.
3. **Host:** DevMode → RimworldMP → **Host lobby (LAN)** (listens `0.0.0.0:30555`).
4. Load or start the shared colony on the host.
5. **Client:** DevMode → **Join lobby localhost** (same machine) or call `MpLobbyActions.JoinAsync("<host-lan-ip>")`.
6. Client receives a join-in-progress freeze-frame when mid-session; `VerseSaveLoad` loads the save blob when game refs exist (fails loudly if bytes arrive without a loader).
7. Optional: set matching modlist hashes and `session.SetReady(true)` on both before unpausing (Phase 4 ready gate).

## After join

Exercise pause/speed, draft, jobs, zones, bills, research. On desync: both freeze; dumps under `%LocalAppData%/RimworldMP/desync/` (`.log` + `.cmds.json`). Soft recovery: `SessionRecovery.RequestRejoin(session)` on the client.

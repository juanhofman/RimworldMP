# Phase 1 — LAN host + client smoke test

Prerequisites:

- RimWorld **1.7** install with `Assembly-CSharp.dll`, `Waylith.dll`, `UnityStubs.dll`
- `export RIMWORLD_DIR=/path/to/RimWorld` (or copy those DLLs to `refs/RimWorld/`)
- Workshop **Harmony 3** enabled and loading **before** RimworldMP
- Matching game build + modlist on both peers

## Build & install

```bash
export RIMWORLD_DIR="/path/to/RimWorld"
dotnet build RimworldMP.sln -c Release
# symlink or copy ./mod into RimWorld/Mods/RimworldMP
```

## Same-machine / LAN

1. Start RimWorld instance A → DevMode → **RimworldMP: Host lobby (LAN)** (listens on `0.0.0.0:30555`).
2. Start RimWorld instance B (second process or second PC) → join:
   - Localhost: DevMode → **RimworldMP: Join lobby localhost**
   - LAN: temporarily call `MpLobbyActions.JoinAsync("<host-lan-ip>")` (extend debug action as needed)
3. Load the **same** colony save on both (JIP is Phase 2). Prefer: host creates/loads, client loads identical save, then both enter play.
4. Verify:
   - Pause / speed changes on either peer apply on both
   - Draft / ordered move-job / zone cell edits / restrict-area changes enqueue as commands
   - After ~60 ticks, peers exchange state hashes; on mismatch both freeze and a dump appears under the desync log directory
5. Stop: DevMode → **RimworldMP: Stop lobby / session**

## Without the game

```bash
dotnet test tests/DeterminismHarness
```

Covers command serialization, seeded RNG parity, tick-gate stall, desync log write, loopback lockstep, and LAN TCP handshake smoke on loopback.

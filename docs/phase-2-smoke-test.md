# Phase 2 — Colony + JIP smoke test

Prerequisites: RimWorld **1.7**, Harmony 3, `RIMWORLD_DIR` build, matching modlists.

## Lobby + JIP

1. Host: DevMode → **RimworldMP: Host lobby (LAN)** (`0.0.0.0:30555`).
2. Start / load a colony on the host; play a few minutes (bills, research).
3. Client: DevMode → **Join lobby localhost** (or LAN IP).
4. Client receives a **join point** (save blob + watermark + modlist hash). With game refs, load the blob then resume ticks. Without full save-load wiring, DevMode logs the JIP metadata.
5. Confirm both peers share seed/tick; client catches up on post-watermark batches.

## Colony systems

On either peer, exercise:

- Bill add/delete on a workbench
- Research project selection
- Trade accept (fingerprint command)
- Caravan form (subset)
- Zone create/add/remove (shadow store + Verse apply when refs present)

## Desync dumps

On hash mismatch: sim freezes; `%LocalAppData%/RimworldMP/desync/` gets `.log` (RNG counters + last N cmds) and `.cmds.json`.

## Offline harness

```bash
dotnet test tests/DeterminismHarness
```

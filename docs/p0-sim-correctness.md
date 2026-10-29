# P0 sim-correctness notes

Implements the P0 gap list from the Project store theory checkup (desync truth, JIP load, join gate, map-safe jobs/zones, MaxClients, non-DevMode entry). Protocol magic **`RMP3`**, version **4** (MinSupported **3**). LiteNetLib: [transport-litenetlib.md](./transport-litenetlib.md).

## What landed (offline / NO_GAME_REFS)

| Item | Status |
| --- | --- |
| Desync fingerprint | SHA256(protocol ‖ tick ‖ rngSeed ‖ drawCount ‖ namedCounters ‖ rollingCmdHash ‖ worldFingerprint32). World slot reserved (zeros) until Verse samples exist. |
| JIP load | `ISaveLoad` / `GameLoad` / `MemorySaveLoad`; `VerseSaveLoad` when game refs exist. Non-empty blob + missing loader → `MissingSaveLoadException`. |
| Join hard gate | Handshake carries modlist hash; host rejects with `JoinReject` on protocol / modlist / lobby-full before JoinAck. |
| JobGive / zones | Payloads carry `mapId`; JobGive also has targetThingId, count, queue. Apply uses map id — no `Find.CurrentMap`. |
| MaxClients | Default **4** (`MpConfig` + `LanTcpTransport`). |
| Non-DevMode entry | Corner button + F10 lobby window; consumes `launcher-handoff.json` ([launcher-handoff.md](./launcher-handoff.md)). DevMode actions kept. |
| Harness | New `P0SimCorrectnessTests` + updated Phase 2/4 JIP tests. |

## Still needs RimWorld install (`RIMWORLD_DIR`)

1. Validate `VerseSaveLoad` against live `GameDataSaveLoader` / `GenFilePaths` on 1.7.
2. Populate `DesyncDetector.SetWorldFingerprint` from map/world/pawn samples (RNG opinions already partially counted via Harmony Rand patches).
3. Confirm lobby `UIRoot.UIRootOnGUI` + `Window` / `Listing_Standard` signatures under Harmony 3 type checks.
4. Two-client 30+ min smoke: draft / jobs / zones with thickened payloads.
5. Full `Verse.Rand` coverage beyond Value/Range/Chance/Bool (see [track-a-p1-syncmethod.md](./track-a-p1-syncmethod.md)).
6. Live Verse trade/caravan commits and `VerseSaveLoad` validation.

## Config knobs

- `MpConfig.MaxClients` (default 4)
- `MpConfig.ModlistHash` (handshake gate)
- `MpConfig.DefaultPort` (30555)
- `MpConfig.ReadyRequired` (opt-in ready gate)
- `MpConfig.TransportKind` (`Tcp` default / `LiteNetLib`)

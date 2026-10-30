# Desync dumps (local-only)

When peers disagree on a periodic state fingerprint, RimworldMP **freezes** the simulation and writes a diagnostic dump on **this machine only**. Dumps are never uploaded — there is no cloud / telemetry path.

## Where files land

| Platform | Directory |
| --- | --- |
| Default | `%LocalAppData%/RimworldMP/desync/` (Windows) / `~/.local/share/RimworldMP/desync/` (Linux) / equivalent `LocalApplicationData` |
| Override | Pass a custom directory into `DesyncDetector` (tests / harness do this) |

Each freeze produces a basename `desync-tick{N}-{utcStamp}`:

| File | Contents |
| --- | --- |
| `.log` | Human-readable sections (see below) |
| `.cmds.json` | Last N applied commands (opcode, player, payload hex) |
| `.batches.json` | Optional — recent command-batch ring entries (when the ring is non-empty) |

The in-game freeze overlay shows the **`.log` path** (body + footer). Open that file first.

## Log sections

Marked with `section=…` so harnesses and humans can grep:

1. **header** — `localOnly=true`, `upload=never`, UTC stamp  
2. **session** — `protocol`, `tick`, `localPeerId`, `peerIds`, `modlistHashShort` (+ full `modlistHash` when known)  
3. **fingerprint** — `localHash` / `remoteHash` from the published end-of-tick checkpoint (not a live mid-tick snapshot)  
4. **rng** — `rngSeed`, `rngDraws`, named `rngCounters`  
5. **recentCommands** — last N applied `MpCommand`s (default 128)  
6. **recentBatches** — optional ring of recent batch frames (tick / byte length / cmd count / hex preview; default 64)  
7. **files** — absolute paths of the JSON sidecars  

## Sample layout

```text
=== RimworldMP desync dump ===
section=header
RimworldMP desync detected
utc=2026-10-29T19:45:00.0000000Z
localOnly=true
upload=never
note=dumps stay on this machine; RimworldMP never uploads them

section=session
protocol=3
tick=120
localPeerId=0
peerIds=0,1
modlistHashShort=f2046ab21e52
modlistHash=f2046ab21e526aae4469bdf1418310b553a2c34b9b9b617cc148a00d76e963f6

section=fingerprint
localHash=…
remoteHash=…
note=worldFingerprint slot reserved; Verse map/world samples need RIMWORLD_DIR

section=rng
rngSeed=123456789
rngDraws=42
rngCounters:
  Rand.Value=3
  ticks=120

section=recentCommands
recentCommands=2
recentCommandsLimit=128
  tick=119 op=Draft(…) player=1 payloadHex=…
  tick=120 op=BillSet(…) player=0 payloadHex=…

section=recentBatches
recentBatches=2
recentBatchesLimit=64
  tick=119 bytes=40 cmds=1 hexPreview=…
  tick=120 bytes=12 cmds=0 hexPreview=…

section=files
cmdsJson=/…/desync-tick120-….cmds.json
batchesJson=/…/desync-tick120-….batches.json
```

## Offline harness

```bash
dotnet test tests/DeterminismHarness --filter DesyncDumpTests
```

`DesyncDumpTests.Forced_fingerprint_mismatch_writes_dump_with_expected_sections` forces a hash mismatch and asserts every `section=` marker plus peer / modlist / RNG / command / batch fields.

## Related

- Fingerprint formula: [p0-sim-correctness.md](./p0-sim-correctness.md)
- Soft recovery after freeze: [phase-4-harden.md](./phase-4-harden.md)
- Core types: `DesyncDetector`, `DesyncDump`, `DesyncDumpContext` under `src/RimworldMP.Core/Sync/`

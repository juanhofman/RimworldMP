# RimworldMP 0.5.0

Release-ready **scaffold** for RimWorld 1.7 shared-colony LAN multiplayer.

## What you get

| Area | Included |
| --- | --- |
| Net | LAN TCP `INetTransport`, protocol v2 (`RMP2`) |
| Lockstep | Tick gate, command batches, seeded RNG hooks |
| Colony | Bills, research, trade, caravans (subset), zones |
| JIP | Chunked join point + command-log catch-up |
| DLC surfaces | Royalty / Ideology / Biotech / Anomaly command APIs |
| Compat | `RimworldMP.Compat` helpers + example stubs |
| Harden | Ready/modlist checks, recovery rejoin, soak tests |
| Desync | Freeze + RNG counters + last-N cmds JSON |

## What still needs a RimWorld install

- Compiling/running Verse + DLC Harmony patches against live DLLs
- Loading JIP save blobs into the game
- Proving multi-hour desync-free co-op

## Install

See [install-play-guide.md](./install-play-guide.md) and [smoke-checklist.md](./smoke-checklist.md).

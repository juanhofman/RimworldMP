# RimworldMP 0.6.0 — UI + launcher release

**PackageId:** `RimworldMP.Core` · **License:** MIT · **Target:** RimWorld **1.7** only

## Highlights

| Area | Included |
| --- | --- |
| Launcher | External LAN helper: locate game, Harmony-first mod order + hash, host beacon / join discovery, `launcher-handoff.json`, launch |
| Net | LAN TCP `INetTransport`, protocol **v3** (`RMP3`), modlist hash hard-gate at join |
| Lockstep | Tick gate, command batches, seeded RNG hooks, MaxClients default **4** |
| P0 | Desync fingerprint, map-safe job/zone payloads, JIP save-load hooks |
| Lobby | In-mod Host/Join (F10 / corner button) + launcher handoff consumption path |
| Colony | Bills, research, trade, caravans (subset), zones (Phases 2–4) |
| Compat | `RimworldMP.Compat` helpers + example stubs |
| Harden | Ready checks, recovery rejoin, soak harness, desync dumps |
| Packaging | Workshop-style `mod/About` (Preview + About images), install guide, smoke checklist |

## Install (short)

1. Install **Harmony 3** (Workshop `2009463077` or ≥ v3.0.1).
2. Build Release (or use a release zip) and copy `mod/` → `RimWorld/Mods/RimworldMP`.
3. Optional: run `tools/RimworldMP.Launcher` to prepare mods, host/join, and launch.
4. In mod list: **Harmony**, then **RimworldMP**. Same ordered modlist on every peer.

Full steps: [install-play-guide.md](./install-play-guide.md) · Smoke: [smoke-checklist.md](./smoke-checklist.md)

## Known limitations

- **Untested in live RimWorld 1.7.** Compiling/running Verse + DLC Harmony patches and proving multi-hour desync-free co-op requires a real game install (`RIMWORLD_DIR`).
- Without game refs the solution builds under `RIMWORLDMP_NO_GAME_REFS` stubs (CI / VMs) — not for serious play.
- Steam networking, LiteNetLib transport, and dedicated/headless host are out of scope for this release.

## Changelog lineage

Phases 1–5 scaffold (**0.5.0**) → Harmony pin + P0 sim-correctness + launcher MVP → **0.6.0** packaging. See root [CHANGELOG.md](../CHANGELOG.md).

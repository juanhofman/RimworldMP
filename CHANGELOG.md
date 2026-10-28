# Changelog

## Unreleased

### Fixed
- Pin compile-time Harmony to NuGet `Lib.Harmony.Ref` `3.0.0-rc.3` (matches HarmonyRimWorld v3.0.1); stop using `Lib.Harmony` `3.0.0-rc.2`

## 0.5.0 — Release-ready scaffold (2026-10-28)

First tagged release of the from-scratch RimworldMP lockstep stack for RimWorld **1.7**.

### Added
- Phases 1–4: LAN TCP lobby, lockstep commands, JIP, colony + DLC surfaces, Compat helpers, ready/modlist checks, recovery rejoin, soak harness
- Docs: install/play guide, smoke checklist, API, per-phase notes
- Example compat stubs (`CompatHello`, `CompatHostIo`)

### Notes
- In-game Harmony patches and Verse apply paths require `RIMWORLD_DIR` / game DLLs
- LiteNetLib / Steam / dedicated host remain out of scope
- Protocol magic `RMP2`, version **2**

## 0.1.0 — Phase 0/1 foundation

Scaffold + smallest playable MP primitives (merged via PR #1).

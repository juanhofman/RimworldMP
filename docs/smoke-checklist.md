# Smoke checklist (v0.7.0)

**Known limitation:** requires RimWorld 1.7 for live play; Verse apply still scaffolded. Offline items are the CI gate; in-game items need `RIMWORLD_DIR` + Harmony 3.

## Offline (no RimWorld)

- [ ] `dotnet build RimworldMP.sln -c Release` succeeds (NO_GAME_REFS warning OK)
- [ ] `dotnet test tests/DeterminismHarness -c Release` — all green (SyncMethod, command catalog, JIP fail-loud, RNG opinion)
- [ ] Review [feature-completeness.md](./feature-completeness.md) for Verse-blocked apply gaps
- [ ] `dotnet build tools/RimworldMP.Launcher/RimworldMP.Launcher.sln -c Release` succeeds
- [ ] `dotnet run --project tools/RimworldMP.Launcher -- help` prints usage
- [ ] `mod/1.7/Assemblies/` contains Core/API/Net/Compat/main DLLs
- [ ] `mod/About/About.xml` has `packageId` **RimworldMP.Core** and `modVersion` **0.7.0**
- [ ] `mod/About/Preview.png` and `About.png` present
- [ ] Root `VERSION` is `0.7.0`; `LICENSE` is MIT

## Launcher (needs RimWorld install path; no deep play)

- [ ] `locate` / `locate --path` finds install
- [ ] `mods prepare` backs up ModsConfig and enables Harmony then `RimworldMP.Core`
- [ ] `hash` stable for unchanged mod order; peers can compare
- [ ] `host` writes `launcher-handoff.json` with `role=host` and starts UDP beacon (30556)
- [ ] `join --ip <host>` writes client handoff

## In-game (needs RIMWORLD_DIR build + Harmony 3)

- [ ] Mod loads without red errors; Harmony above RimworldMP
- [ ] Host lobby opens on port 30555 (handoff or F10 / button)
- [ ] Client joins (localhost / LAN IP); seed matches host
- [ ] Join rejects mismatched protocol or modlist hash
- [ ] Mid-session join receives JoinPoint metadata / blob
- [ ] Pause/speed sync both ways
- [ ] Draft + ordered job give (map-safe payloads)
- [ ] Zone add/remove
- [ ] Bill add/delete + research select
- [ ] Ready + modlist hash reject mismatched peer (if enabled)
- [ ] Forced desync dump writes log + cmds json (+ optional batches json); see [desync-dumps.md](./desync-dumps.md)
- [ ] Client recovery rejoin requests a new JoinPoint

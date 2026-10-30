# Smoke checklist (v0.7.0)

**Known limitation:** requires RimWorld 1.7 for live play; Verse apply still scaffolded ([verse-binding-checklist.md](./verse-binding-checklist.md)). Offline items are the CI gate; in-game items need `RIMWORLD_DIR` + Harmony 3. GitHub release is a **pre-release**.

## Offline (no RimWorld)

- [ ] `dotnet build RimworldMP.sln -c Release` succeeds (NO_GAME_REFS warning OK; TreatWarningsAsErrors)
- [ ] `dotnet test tests/DeterminismHarness -c Release` — all green (SyncMethod, command catalog, JIP fail-loud, RNG opinion, Session QoL, LiteNetLib, VerseBind registry)
- [ ] `dotnet test tests/RimworldMP.ModSync.Tests -c Release` — all green (loopback LAN sync + acquisition stub messaging)
- [ ] Review [feature-completeness.md](./feature-completeness.md) for Verse-blocked apply gaps
- [ ] `dotnet build tools/RimworldMP.Launcher/RimworldMP.Launcher.sln -c Release` succeeds
- [ ] `dotnet run --project tools/RimworldMP.Launcher -- help` prints usage (notes SteamCMD unsupported)
- [ ] `dotnet run --project tools/RimworldMP.Launcher -- self-test` — ModlistHasher golden OK
- [ ] Without RimWorld: `mods list` / `mods sync` fail loud with install-path guidance (exit ≠ 0)
- [ ] `mod/1.7/Assemblies/` contains Core/API/Net/Compat/main DLLs
- [ ] `mod/About/About.xml` has `packageId` **RimworldMP.Core** and `modVersion` **0.7.0**
- [ ] `mod/About/Preview.png` and `About.png` present
- [ ] Root `VERSION` is `0.7.0`; `LICENSE` is MIT
- [ ] EN + NL keyed session strings present under `mod/Languages/*/Keyed/RimworldMP_Session.xml` (incl. desync keys)

## Launcher (needs RimWorld install path; no deep play)

- [ ] `locate` / `locate --path` finds install
- [ ] `mods prepare` backs up ModsConfig and enables Harmony then `RimworldMP.Core`
- [ ] `hash` stable for unchanged mod order; peers can compare
- [ ] `host --share-mods` writes `launcher-handoff.json` with `role=host`, starts UDP beacon (30556) + mod-sync TCP (30557)
- [ ] `mods sync --host <ip> --path <rw-dir>` pulls over LAN (or reports clear SteamCMD-unavailable errors)
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
- [ ] Ready + chat + kick + display names (Session QoL)
- [ ] Forced desync dump writes log + cmds json (+ optional batches json); freeze overlay shows EN/NL copy; see [desync-dumps.md](./desync-dumps.md)
- [ ] Client recovery rejoin requests a new JoinPoint
- [ ] Fill-in smoke rows in [verse-binding-checklist.md](./verse-binding-checklist.md) as bindings go live

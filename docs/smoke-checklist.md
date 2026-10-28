# Smoke checklist (v0.5.0)

## Offline (no RimWorld)

- [ ] `dotnet build RimworldMP.sln -c Release` succeeds (NO_GAME_REFS warning OK)
- [ ] `dotnet test tests/DeterminismHarness -c Release` — all green
- [ ] `mod/1.7/Assemblies/` contains Core/API/Net/Compat/main DLLs

## In-game (needs RIMWORLD_DIR build + Harmony 3)

- [ ] Mod loads without red errors; Harmony above RimworldMP
- [ ] Host lobby opens on port 30555
- [ ] Client joins (localhost / LAN IP); seed matches host
- [ ] Mid-session join receives JoinPoint metadata / blob
- [ ] Pause/speed sync both ways
- [ ] Draft + ordered job give
- [ ] Zone add/remove
- [ ] Bill add/delete + research select
- [ ] Ready + modlist hash reject mismatched peer (if enabled)
- [ ] Forced desync dump writes log + cmds json
- [ ] Client recovery rejoin requests a new JoinPoint

# Continuous integration

GitHub Actions workflow: [`.github/workflows/ci.yml`](../.github/workflows/ci.yml).

Runs on **push** and **pull_request** to `main` (Ubuntu, .NET **10.0.x**), without a RimWorld install (`RIMWORLDMP_NO_GAME_REFS` / empty `RIMWORLD_DIR`).

| Step | What |
| --- | --- |
| Restore + build | `RimworldMP.sln` with **TreatWarningsAsErrors** (`Directory.Build.props`: AnalysisLevel latest, WarningLevel 5) |
| DeterminismHarness | Lockstep, serialize, hash, modlist, JIP, LAN hard-gates, CI important-path suite |
| ModSync tests | Manifest parse, content hash, path sandbox, loopback host→client sync |
| Launcher build | `tools/RimworldMP.Launcher` headless `dotnet build` |
| Launcher self-test | `ModlistHasher` golden / Core-mirror check |

NuGet packages are cached. Any test failure fails the workflow. `dotnet format --verify-no-changes` is not enabled (no repo format config).

Local parity:

```bash
export RIMWORLDMP_NO_GAME_REFS=true
export RIMWORLDMP_NO_GAME_REFS=true
dotnet build RimworldMP.sln -c Release -p:TreatWarningsAsErrors=true
dotnet test tests/DeterminismHarness -c Release
dotnet test tests/RimworldMP.ModSync.Tests -c Release
dotnet build tools/RimworldMP.Launcher -c Release
dotnet run --project tools/RimworldMP.Launcher -c Release -- self-test
```


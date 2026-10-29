# Code quality policy

RimworldMP builds **warning-free** under `TreatWarningsAsErrors` for every project in `RimworldMP.sln` (Core, Net, API, Compat, mod stubs, ModSync, launcher, examples, tests).

## Gates (durable)

Configured in [`Directory.Build.props`](../Directory.Build.props):

| Setting | Value | Purpose |
| --- | --- | --- |
| `TreatWarningsAsErrors` | `true` | Any CS/CA warning fails the build |
| `WarningLevel` | `5` | Full compiler warning surface |
| `EnableNETAnalyzers` | `true` | SDK / CA analyzers |
| `AnalysisLevel` | `latest` | Match installed .NET SDK (net10 / C# 13) |
| `AnalysisMode` | `Recommended` | Useful CA set without `All` noise bombs |
| `EnforceCodeStyleInBuild` | `false` | IDE style-as-error deferred until curated |
| `Nullable` | `enable` | Already on for all projects |

CI (`.github/workflows/ci.yml`) builds with `-p:TreatWarningsAsErrors=true` and `RIMWORLDMP_NO_GAME_REFS=true`. See [ci.md](ci.md).

## Stub / no-game-refs builds

Set `RIMWORLDMP_NO_GAME_REFS=true` (env or MSBuild) for intentional stub builds (CI, VMs without RimWorld). That:

- Forces `HasRimWorldRefs=false` even if local refs exist
- Emits an informational **Message** (not a Warning) so TreatWarningsAsErrors stays green

Building without refs **and** without `RIMWORLDMP_NO_GAME_REFS` still emits a **Warning** (fails under TreatWarningsAsErrors) so accidental stub builds are obvious.

## EditorConfig

[`.editorconfig`](../.editorconfig) suppresses intentional noise:

- **CA1707** — underscored xUnit / harness method names
- **CA1822** — instance service methods kept for orchestrator / DI ergonomics
- **tests/** — CA1861 / CA1305 / CA1307 / CA1711 / CA1816 (harness conventions)

Do not broaden suppressions casually; prefer fixing call sites (InvariantCulture, `ThrowIfNull`, dispose patterns).

## Package hygiene

- Target **net10.0**, `LangVersion=latest`
- Avalonia launcher stays on latest **11.3.x** (FluentTheme / classic desktop APIs); do not jump to Avalonia 12 without an explicit migration PR
- Bump NuGet patch/minor regularly; keep `Lib.Harmony.Ref` pinned to the RimWorld 1.7 Harmony 3 compile ref

## Local check

```bash
export RIMWORLDMP_NO_GAME_REFS=true
dotnet build RimworldMP.sln -c Release
dotnet test tests/DeterminismHarness -c Release
dotnet test tests/RimworldMP.ModSync.Tests -c Release
dotnet build tools/RimworldMP.Launcher -c Release
dotnet run --project tools/RimworldMP.Launcher -c Release -- self-test
```

Expect **0 Warning(s)** / **0 Error(s)**.

## When feature PRs land

Rebase this quality branch (or re-run a clean pass) on the new `main` tip before merging. Feature work that introduces CA warnings under Recommended mode must fix or narrowly suppress them in the same PR.

# Reference stubs

Do **not** commit Ludeon game assemblies.

To build the full `RimworldMP` game project (Harmony patches + command applicators against Verse):

1. Set `RIMWORLD_DIR` to your RimWorld **1.7** install directory (the folder that contains `Assembly-CSharp.dll`, `Waylith.dll`, and `UnityStubs.dll`), **or**
2. Copy those three DLLs into `refs/RimWorld/` (gitignored).

| DLL | Role |
| --- | --- |
| `Assembly-CSharp.dll` | Verse / RimWorld API surface |
| `Waylith.dll` | 1.7 engine |
| `UnityStubs.dll` | Unity compatibility stubs (keep `using UnityEngine;`) |

Without them, the solution still builds API / Core / Net / DeterminismHarness, and the main project compiles under `RIMWORLDMP_NO_GAME_REFS` (patches excluded). Harmony **3** remains a hard runtime dependency and must not be bundled as `0Harmony.dll`.

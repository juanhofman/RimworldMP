# Reference stubs

Do **not** commit Ludeon game assemblies.

To build the full `RimworldMP` game project:

1. Set `RIMWORLD_DIR` to your RimWorld install directory (the folder that contains `Assembly-CSharp.dll`, `Waylith.dll`, and `UnityStubs.dll`), **or**
2. Copy those three DLLs into `refs/RimWorld/` (gitignored).

Without them, the solution still builds API / Core / Net, and the main project compiles under `RIMWORLDMP_NO_GAME_REFS`.

# RimworldMP example compat mods

Small third-party style projects that reference **only** `RimworldMP.API`.

| Project | Pattern |
| --- | --- |
| [`CompatHello`](CompatHello/) | `[SyncMethod]` + `[SyncField]` + `FieldWatch` |
| [`CompatHostIo`](CompatHostIo/) | Host-only compute → `HostInject` → SyncMethod apply |

## Build without RimWorld

These projects set `NO_GAME_REFS` and do **not** reference Verse, Unity, or Harmony. They build whenever the solution builds:

```bash
dotnet build examples/CompatHello/CompatHello.csproj -c Release
dotnet build examples/CompatHostIo/CompatHostIo.csproj -c Release
# or
dotnet build RimworldMP.sln -c Release
```

No stubs under `refs/` are required for examples. Live game integration belongs in the main `RimworldMP` mod (`RIMWORLD_DIR`).

See [`docs/mod-author-api.md`](../docs/mod-author-api.md).

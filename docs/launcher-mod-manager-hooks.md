# Launcher mod manager + LAN sync hooks

The Avalonia **Mods** screen is the first-class mod manager surface. LAN file transfer / host sync is owned by a parallel **mod-sync** implementation. This document lists the stable hook points so that work can land without fighting the UI layout.

## Ownership

| Area | Owner |
| --- | --- |
| Avalonia Mods / Host / Join / Launch UI | Launcher UI (`tools/RimworldMP.Launcher/Gui/**`, `ViewModels/**`) |
| `IModCatalog` / `IModSyncClient` contracts + stubs | Launcher UI (this PR) |
| Real Workshop/local scanning, LAN transfer server/client | Mod-sync agent (`Services/ModSync/**` impl or `tools/RimworldMP.ModSync/`) |
| Handoff JSON protocol 1 | Shared — additive fields only |

## Interfaces

```
tools/RimworldMP.Launcher/Services/ModSync/
  IModCatalog.cs          # local inventory + active hash
  IModSyncClient.cs       # CompareAsync / SyncAsync + progress DTOs
  StubModCatalog.cs       # ModsConfig active list only
  StubModSyncClient.cs    # IsAvailable=false; Sync reports error
  ModSyncServices.cs      # Catalog / Sync composition root
```

### Register a real backend

```csharp
ModSyncServices.Catalog = new YourModCatalog(...);
ModSyncServices.Sync = new YourLanModSyncClient(...);
```

Do this before the window is constructed (e.g. early in `Program.Main` / `App` init), or inject via `new MainViewModel(catalog, sync)`.

`MainViewModel` already binds:

| UI control | Command / property | Interface |
| --- | --- | --- |
| Mod list + Harmony badges | `ModRows`, `PrepareMods`, `RefreshModsSnapshot` | `IModCatalog` + `ModsConfigService` |
| Hash display | `ModlistHash` | `IModCatalog.ComputeActiveHash` / Core-identical hasher |
| Sync host / port | `SyncHost`, `SyncPort` (default **30557**) | `ModSyncHostTarget` |
| **Compare** | `CompareWithHostCommand` | `IModSyncClient.CompareAsync` |
| **Sync from host** | `SyncFromHostCommand` | `IModSyncClient.SyncAsync` |
| Progress bar | `SyncProgressFraction`, `SyncProgressLabel` | `IProgress<ModSyncProgress>` |
| Missing / mismatched lists | `SyncMissing`, `SyncMismatched` | `ModSyncPlan` deltas |
| Use Join IP | `UseJoinTargetForSyncCommand` | copies Join beacon / IP |

When `IModSyncClient.IsAvailable` is `false`, buttons still run (stub messaging) so layout can be demoed; replace the stub to enable real sync.

## Ports (convention)

| Port | Role |
| --- | --- |
| **30555** | Game MP TCP |
| **30556** | Launcher UDP discovery beacon |
| **30557** | Suggested LAN **mod sync / control** (not implemented here) |

Change the sync port in the UI or pass `ModSyncHostTarget.Port`. Keep game and discovery ports unchanged unless bumping handoff/discovery protocols.

## Suggested mod-sync fill-in order

1. Expand `IModCatalog` implementation: Workshop + `Mods/` folder scan, `About.xml` packageIds, folder paths.
2. Host-side advertise of ordered packageId list + content hashes (not full archives in the UDP beacon).
3. `CompareAsync` → populate `Missing` / `Mismatched` / `ExtraLocal`.
4. `SyncAsync` with chunked LAN transfer + `ModSyncProgress` (fraction + current packageId).
5. Optional: apply remote load order via existing `ModsConfigService.WriteActiveMods` / `PrepareForMp`.

## Do not

- Rename `IModSyncClient` / `IModCatalog` members without updating this doc and `MainViewModel`.
- Put bulk mod payloads on discovery UDP **30556**.
- Upload mods off-LAN or add telemetry.
- Edit `src/**` or `mod/About/**` from the launcher UI track.

## Related docs

- [launcher-mvp.md](./launcher-mvp.md) — flows, CLI, hash golden vector
- [launcher-handoff.md](./launcher-handoff.md) — protocol 1 handoff schema

# Launcher mod manager + LAN sync hooks

The Avalonia **Mods** screen is the first-class mod manager surface. LAN file transfer / host sync is owned by a parallel **mod-sync** implementation. This document lists the stable hook points so that work can land without fighting the UI layout.

## Ownership

| Area | Owner |
| --- | --- |
| Avalonia Mods / Host / Join / Launch UI | Launcher UI (`tools/RimworldMP.Launcher/Gui/**`, `ViewModels/**`) |
| `IModCatalog` / `IModSyncClient` contracts + stubs | Launcher UI (this PR) |
| Real Workshop/local scanning, LAN transfer server/client | **Shipped:** `InstalledModCatalog` + `LanModSyncClient` + `tools/RimworldMP.ModSync/` (see [mod-manager-lan-sync.md](./mod-manager-lan-sync.md)) |
| Handoff JSON protocol 1 | Shared — additive fields only |

## Interfaces

```
tools/RimworldMP.Launcher/Services/ModSync/
  IModCatalog.cs          # local inventory + active hash
  IModSyncClient.cs       # CompareAsync / SyncAsync + progress DTOs
  InstalledModCatalog.cs  # Mods + Workshop scan (default)
  LanModSyncClient.cs     # Compare/Sync over TCP 30557 (default)
  HostModShareService.cs  # Host TCP share lifecycle
  StubModCatalog.cs       # ModsConfig active list only (tests)
  StubModSyncClient.cs    # IsAvailable=false (tests / UI layout)
  ModSyncServices.cs      # Catalog / Sync composition root
```

### Register backends

```csharp
// Default (Program.Main):
ModSyncServices.UseLanBackends();

// Or manual:
ModSyncServices.Catalog = new InstalledModCatalog(...);
ModSyncServices.Sync = new LanModSyncClient(...);
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

When `IModSyncClient.IsAvailable` is `false` (stubs), buttons still run with `StubModSyncClient.UnavailableMessage`. Production registration sets `IsAvailable=true` via `LanModSyncClient`.

### Workshop / SteamCMD messaging

- Acquisition order: **LAN host** → **local Workshop cache** → (documented non-goal) SteamCMD.
- `SteamCmdProviderStub` exposes `IsImplemented=false` + `LimitationMessage`; `ClientModSync` skips it and never pretends to download.
- Host share errors for a package are wrapped with that limitation text so the Mods screen / CLI fail loud instead of silent.

## Ports (convention)

| Port | Role |
| --- | --- |
| **30555** | Game MP TCP |
| **30556** | Launcher UDP discovery beacon (`modSyncPort` optional field) |
| **30557** | LAN **mod sync** TCP (`HostModServer` / `LanModSyncClient`) |

Change the sync port in the UI or pass `ModSyncHostTarget.Port`. Keep game and discovery ports unchanged unless bumping handoff/discovery protocols.

## Do not

- Rename `IModSyncClient` / `IModCatalog` members without updating this doc and `MainViewModel`.
- Put bulk mod payloads on discovery UDP **30556**.
- Upload mods off-LAN or add telemetry.
- Edit `src/**` or `mod/About/**` from the launcher UI track.

## Related docs

- [launcher-mvp.md](./launcher-mvp.md) — flows, CLI, hash golden vector
- [launcher-handoff.md](./launcher-handoff.md) — protocol 1 handoff schema

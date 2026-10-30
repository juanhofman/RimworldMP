# RimworldMP LAN Launcher

External **local-only** helper for RimWorld **1.7**: locate the game, pin Harmony → RimworldMP mod order, LAN host/join discovery, write handoff JSON, launch the process.

**Default UX:** Avalonia 11 desktop UI (RimWorld-like wood + parchment). **CLI** remains a full fallback (`--cli` or any subcommand).

Lockstep / sim stays **inside** the mod. This tool does **not** upload ModsConfig, saves, or telemetry.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- RimWorld 1.7 install (Steam or manual folder) to launch the game
- Workshop **Harmony 3** (`brrainz.harmony`) installed
- This repo’s `mod/` copied/symlinked into RimWorld `Mods` (PackageId `RimworldMP.Core`)

## Build & run

```bash
dotnet build tools/RimworldMP.Launcher/RimworldMP.Launcher.sln -c Release

# Avalonia UI (needs a display on Linux)
dotnet run --project tools/RimworldMP.Launcher -c Release

# Interactive CLI menu
dotnet run --project tools/RimworldMP.Launcher -c Release -- --cli

# Direct CLI commands (unchanged)
dotnet run --project tools/RimworldMP.Launcher -c Release -- help
dotnet run --project tools/RimworldMP.Launcher -c Release -- self-test
```

Headless / no `DISPLAY`: the process falls back to CLI help instead of starting Avalonia.

## UI screens

| Screen | Job |
| --- | --- |
| **Home** | Brand hero **RimworldMP** + Host / Join CTAs |
| **Host** | Session name, port, advertise IP, max clients, beacon + handoff + launch |
| **Join** | LAN discovery list + manual IP, handoff + launch |
| **Mods** | Ordered list (Harmony-first badges), hash, prepare/restore, **Sync from host** hooks |
| **Launch** | Install path, status log, handoff path |

Visual direction: wood/ink brown + parchment, amber colonial buttons, Cinzel / Barlow Condensed / Source Sans 3 — see [ui-design-brief.md](./ui-design-brief.md). Screenshots: optional in CI; Project store `media/launcher-ui/`.

LAN **mod file sync** is live via `LanModSyncClient` / `InstalledModCatalog` (TCP **30557**). Workshop live download / SteamCMD is **not** supported — LAN host transfer + local Workshop cache only. Hooks: [launcher-mod-manager-hooks.md](./launcher-mod-manager-hooks.md) · [mod-manager-lan-sync.md](./mod-manager-lan-sync.md).

## User steps (CLI or UI)

### One-time

1. Install Harmony 3 from the Steam Workshop; copy `mod/` → `Mods` as **RimworldMP**.
2. Locate the game (UI → Launch, or `locate [--path …]`).
3. Prepare mods (UI → Mods → Prepare for MP, or `mods prepare`) — backs up `ModsConfig.xml`, enforces Harmony first + `RimworldMP.Core`.
4. Confirm hash (`hash` or Mods screen). Both peers need the **same ordered packageId list**.

### Host

UI: **Host** → Start hosting. CLI:

```bash
dotnet run --project tools/RimworldMP.Launcher -c Release -- host --name "Colony Night"
```

1. ModsConfig backup + MP order (unless `--no-mods`)
2. Writes `launcher-handoff.json` with `role=host`, port **30555** (optional `maxClients`)
3. UDP broadcast beacon every 1s on **30556**
4. Launches RimWorld (unless `--no-launch`)
5. UI: Stop beacon on Host; CLI: Press Enter to stop

In-game: handoff consumer auto-hosts when wired; else DevMode / F10 lobby.

### Join

UI: **Join** → Scan LAN or enter IP → Join session. CLI:

```bash
dotnet run --project tools/RimworldMP.Launcher -c Release -- join
dotnet run --project tools/RimworldMP.Launcher -c Release -- join --ip 192.168.1.20
```

### Restore ModsConfig

UI: Mods → Restore backup. CLI: `mods restore`.

## Ports

| Port | Use |
| --- | --- |
| **30555** | Game MP TCP |
| **30556** | Launcher UDP discovery beacon |
| **30557** | Suggested LAN mod-sync control (hooks only; see mod-manager-hooks doc) |

## Handoff file

See [launcher-handoff.md](./launcher-handoff.md).

| OS | Path |
| --- | --- |
| Windows | `%LocalAppData%\RimworldMP\launcher-handoff.json` |
| Linux | `~/.local/share/RimworldMP/launcher-handoff.json` |

## Modlist hash

Mirrors `src/RimworldMP.Core/Session/ModlistHash.cs` exactly (UTF-8, `\n`-joined trimmed packageIds, lowercase SHA256 hex).

```bash
dotnet run --project tools/RimworldMP.Launcher -c Release -- self-test
```

Golden vector `["brrainz.harmony","RimworldMP.Core"]` →
`f2046ab21e526aae4469bdf1418310b553a2c34b9b9b617cc148a00d76e963f6`.

## Privacy

- Discovery binds/broadcasts on the LAN only
- No accounts, crash-report webhooks, or cloud sync
- Join IPs are user-entered or taken from local UDP beacons
- Backups stay under the RimworldMP data folder

## Known gaps

- Real LAN mod transfer behind `IModSyncClient` still stubbed ([hooks](./launcher-mod-manager-hooks.md))
- Discovery: UDP broadcast only (no mDNS)
- Windows/Linux primary (macOS path probe is best-effort)
- Does not Workshop-subscribe missing mods
- `-mod=` launch args not used — ModsConfig backup/restore is the identity path
- UI screenshots not checked into the repo

## Layout

```
tools/RimworldMP.Launcher/
  Program.cs                 # UI vs --cli / subcommands
  Cli/CliApp.cs
  Gui/                       # Avalonia 11 App, Theme, MainWindow
  ViewModels/
  Services/                  # locate, mods, discovery, handoff, launch
  Services/ModSync/          # IModCatalog, IModSyncClient + stubs
  Models/
  Assets/Fonts/              # Cinzel, Barlow Condensed, Source Sans 3 (OFL)
```

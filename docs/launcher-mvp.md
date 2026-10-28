# RimworldMP LAN Launcher (MVP)

External **local-only** helper for RimWorld **1.7**: locate the game, pin Harmony → RimworldMP mod order, LAN host/join discovery, write handoff JSON, launch the process.

Lockstep / sim stays **inside** the mod. This tool does **not** upload ModsConfig, saves, or telemetry.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- RimWorld 1.7 install (Steam or manual folder)
- Workshop **Harmony 3** (`brrainz.harmony`) installed
- This repo’s `mod/` copied/symlinked into RimWorld `Mods` (PackageId `RimworldMP.Core`)

## Build & run

```bash
dotnet build tools/RimworldMP.Launcher/RimworldMP.Launcher.sln
dotnet run --project tools/RimworldMP.Launcher -- help
```

Interactive menu (no args):

```bash
dotnet run --project tools/RimworldMP.Launcher
```

## User steps

### One-time

1. Install Harmony 3 from the Steam Workshop; copy `mod/` → `Mods` as **RimworldMP**.
2. Locate the game:

```bash
dotnet run --project tools/RimworldMP.Launcher -- locate
# or:
dotnet run --project tools/RimworldMP.Launcher -- locate --path "/path/to/RimWorld"
```

3. Prepare mods (backs up `ModsConfig.xml`, enforces Harmony first + `RimworldMP.Core` enabled):

```bash
dotnet run --project tools/RimworldMP.Launcher -- mods prepare
```

4. Confirm hash:

```bash
dotnet run --project tools/RimworldMP.Launcher -- hash
```

Both peers need the **same ordered packageId list** (same hash).

### Host

```bash
dotnet run --project tools/RimworldMP.Launcher -- host --name "Colony Night"
```

What happens:

1. ModsConfig backup + MP order (unless `--no-mods`)
2. Writes `launcher-handoff.json` with `role=host`, port **30555**
3. UDP broadcast beacon every 1s on **30556**
4. Launches RimWorld (unless `--no-launch`)
5. Press Enter in the launcher to stop the beacon

In-game: when handoff consumption is wired, lobby auto-hosts. Until then use DevMode → **RimworldMP: Host lobby (LAN)**.

### Join

```bash
# Discover LAN hosts, then launch:
dotnet run --project tools/RimworldMP.Launcher -- join

# Or manual IP:
dotnet run --project tools/RimworldMP.Launcher -- join --ip 192.168.1.20
```

### Restore ModsConfig after a session

```bash
dotnet run --project tools/RimworldMP.Launcher -- mods restore
```

## Ports

| Port | Use |
| --- | --- |
| **30555** | Game MP TCP (`LanTcpTransport` / `NetProtocol.DefaultPort`) |
| **30556** | Launcher UDP discovery beacon only |

## Handoff file

See [launcher-handoff.md](./launcher-handoff.md).

| OS | Path |
| --- | --- |
| Windows | `%LocalAppData%\RimworldMP\launcher-handoff.json` |
| Linux | `~/.local/share/RimworldMP/launcher-handoff.json` |

## Modlist hash

Mirrors `src/RimworldMP.Core/Session/ModlistHash.cs` exactly (UTF-8, `\n`-joined trimmed packageIds, lowercase SHA256 hex).

```bash
dotnet run --project tools/RimworldMP.Launcher -- self-test
```

Golden vector `["brrainz.harmony","RimworldMP.Core"]` →
`f2046ab21e526aae4469bdf1418310b553a2c34b9b9b617cc148a00d76e963f6`.

If Core’s algorithm changes, rebase onto `main` and remirror `tools/RimworldMP.Launcher/Services/ModlistHasher.cs`.

## Privacy

- Discovery binds/broadcasts on the LAN only
- No accounts, crash-report webhooks, or cloud sync
- Join IPs are user-entered or taken from local UDP beacons
- Backups stay under the RimworldMP data folder

## Known gaps (MVP)

- In-mod handoff reader / auto Host-Join still needed (DevMode fallback works today)
- No Avalonia UI (console is enough for MVP)
- Discovery uses UDP broadcast + directed broadcast + loopback unicast; no mDNS
- Windows/Linux primary (macOS path probe is best-effort)
- Does not Workshop-subscribe missing mods
- `-mod=` launch args not used yet — ModsConfig backup/restore is the identity path
- Beacon continues until Enter; no tray/minimize UX
- Hash compare UI vs host beacon is print-only (join prints host hash; no auto-diff apply)

## Layout

```
tools/RimworldMP.Launcher/
  RimworldMP.Launcher.sln
  Program.cs                 # CLI + interactive menu
  Services/
    RimWorldLocator.cs
    ModsConfigService.cs
    ModlistHasher.cs         # mirror of Core.ModlistHash
    LanDiscovery.cs
    HandoffWriter.cs
    ProcessLauncher.cs
    LauncherPaths.cs
  Models/
    HandoffDocument.cs
    DiscoveryBeacon.cs
```

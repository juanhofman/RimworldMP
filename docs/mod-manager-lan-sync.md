# Mod manager + LAN mod sync

Pull the **exact** active mods (packageId + version + content hash) from a host over LAN so both peers share the same `ModlistHash` before launch. No cloud upload; no Steam web scraping.

## User flow

1. **Host** prepares mods (Harmony first → RimworldMP → others), then hosts with mod share:
   - UI: **Host** starts discovery **and** a mod-sync TCP server (port **30557**).
   - CLI: `dotnet run --project tools/RimworldMP.Launcher -- host --share-mods --no-launch`
2. **Client** opens **Mods**, enters host IP (or **Use Join IP**), clicks **Compare**, then **Sync from host**.
   - CLI: `dotnet run --project tools/RimworldMP.Launcher -- mods sync --host <ip>`
3. Client verifies content hashes, writes ModsConfig to the host load order, then **Launch**.

## Ports

| Port | Role |
| --- | --- |
| **30555** | Game MP TCP |
| **30556** | UDP discovery beacon (may advertise `modSyncPort`) |
| **30557** | LAN mod-sync TCP (length-prefixed frames, magic `RMPS`) |

## Download sources (honest)

| Priority | Source | Notes |
| --- | --- | --- |
| 1 | **LAN from host** | Required path. Host zips each active non-Ludeon mod folder. |
| 2 | Local Steam Workshop cache | `steamapps/workshop/content/294100/<id>` when host provides `workshopId` and content hash matches |
| 3 | SteamCMD provider | **Stub only** — not implemented |

**Not supported:** live Workshop download without Steam / SteamCMD; scraping Steam community pages; WAN expose of the share server.

## Security

- Default bind is LAN-facing (`IPAddress.Any` on the host machine) — intended for trusted LAN only; do not port-forward 30557.
- Host serves **only** mods in the active MP list (official Ludeon packages are never transferred).
- Client extracts under `<RimWorld>/Mods/<safePackageId>` with path-traversal rejection on zip entries.
- No telemetry; no exfil of saves or ModsConfig off-machine.

## Architecture

```
tools/RimworldMP.ModSync/          # library: manifest, hash, HostModServer, ClientModSync
tools/RimworldMP.Launcher/
  Services/ModSync/
    IModCatalog / IModSyncClient   # stable UI contracts
    InstalledModCatalog            # real scan
    LanModSyncClient               # real Compare/Sync
    HostModShareService            # host lifecycle
    Stub*                          # tests / UI-only
```

Register backends early: `ModSyncServices.UseLanBackends()` (called from `Program.Main`).

Modlist hash mirrors Core: UTF-8, `\n`-joined trimmed packageIds, lowercase SHA256. Golden: `brrainz.harmony` + `RimworldMP.Core` → `f2046ab21e526aae4469bdf1418310b553a2c34b9b9b617cc148a00d76e963f6`.

## CLI cheat sheet

```bash
dotnet run --project tools/RimworldMP.Launcher -- mods list --path /path/to/RimWorld
dotnet run --project tools/RimworldMP.Launcher -- host --share-mods --path /path/to/RimWorld --no-launch
dotnet run --project tools/RimworldMP.Launcher -- mods sync --host 192.168.1.20 --path /path/to/RimWorld
```

## Tests

```bash
dotnet test tests/RimworldMP.ModSync.Tests/RimworldMP.ModSync.Tests.csproj
```

Covers About.xml parse, content hash stability, path sandbox, and loopback host→client sync roundtrip.

# Launcher handoff schema (protocol 1)

The LAN launcher writes a **local-only** JSON file that RimworldMP reads once at load / main menu, then renames to `.ack` (or deletes). No network upload.

## Path

| OS | Path |
| --- | --- |
| Windows | `%LocalAppData%\RimworldMP\launcher-handoff.json` |
| Linux | `~/.local/share/RimworldMP/launcher-handoff.json` (or `$XDG_DATA_HOME/RimworldMP/`) |
| macOS (stretch) | `~/Library/Application Support/RimworldMP/launcher-handoff.json` — MVP focuses Win/Linux |

Ack path: same file with `.ack` appended (`launcher-handoff.json.ack`).

Print the resolved path:

```bash
dotnet run --project tools/RimworldMP.Launcher -- handoff-path
```

## Schema (protocol `1`)

```json
{
  "protocol": 1,
  "role": "host",
  "host": "0.0.0.0",
  "port": 30555,
  "modlistHash": "lowercase-hex-sha256…",
  "sessionName": "Colony Night",
  "autoStartLobby": true,
  "launcherPid": 12345,
  "rwBuild": "1.7",
  "createdUtc": "2026-10-28T00:00:00.0000000Z"
}
```

| Field | Type | Notes |
| --- | --- | --- |
| `protocol` | int | Must be `1` for this document |
| `role` | string | `"host"` or `"client"` |
| `host` | string | Host: bind hint (`0.0.0.0`). Client: host IPv4 to join |
| `port` | int | Game MP TCP port (default **30555**). Discovery uses **30556** and is launcher-only |
| `modlistHash` | string | `ModlistHash.Compute(ordered packageIds)` — see below |
| `sessionName` | string | Display / beacon name |
| `autoStartLobby` | bool | When true, mod should Host/Join without DevMode |
| `launcherPid` | int | Writer PID (debug) |
| `rwBuild` | string? | Best-effort RimWorld version hint |
| `createdUtc` | string | ISO-8601 UTC |

## Modlist hash (must match Core)

Identical to `RimworldMP.Core.Session.ModlistHash.Compute`:

1. Take active mods in **load order**.
2. `Trim` each `packageId`; drop empties.
3. Join with `\n` (UTF-8).
4. SHA256 → **lowercase** hex.

Golden vector: `["brrainz.harmony","RimworldMP.Core"]` → run `dotnet run --project tools/RimworldMP.Launcher -- self-test`.

## Consumer notes (in-mod; owned by MP workers)

- Read once; on success rename to `.ack` so a relaunch without the launcher does not re-auto-connect.
- If missing / stale / `protocol` unsupported → fall back to DevMode lobby actions.
- Never treat handoff as authenticated; LAN trust model only.

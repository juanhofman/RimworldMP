# Launcher handoff schema (protocol 1)

Stable local-only contract between the external **LAN launcher** (`tools/RimworldMP.Launcher/**`) and the in-mod lobby. No network upload.

The launcher writes the JSON; RimworldMP reads it once at load / main menu, then renames to `.ack`.

## Path

| OS | Path |
| --- | --- |
| Windows | `%LocalAppData%\RimworldMP\launcher-handoff.json` |
| Linux | `~/.local/share/RimworldMP/launcher-handoff.json` (or `$XDG_DATA_HOME/RimworldMP/`; Core uses `LocalApplicationData`) |
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
  "maxClients": 4,
  "rwBuild": "1.7",
  "createdUtc": "2026-10-28T00:00:00.0000000Z"
}
```

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `protocol` | int | yes | Must be `1` for this document |
| `role` | string | yes | `"host"` or `"client"` |
| `host` | string | role-dependent | Host: bind hint (`0.0.0.0`). Client: host IPv4/hostname to join |
| `port` | int | no | Game MP TCP port (default **30555**). Discovery uses **30556** and is launcher-only |
| `modlistHash` | string | recommended | `ModlistHash.Compute(ordered packageIds)` — hard-gated at TCP join (protocol v3) |
| `sessionName` | string | no | Display / beacon name |
| `autoStartLobby` | bool | no | Default `true`. When false, config is applied but host/join is not started |
| `launcherPid` | int | no | Writer PID (debug) |
| `maxClients` | int | no | Host cap; in-mod default **4** (`MpConfig.DefaultMaxClients`) |
| `rwBuild` | string? | no | Best-effort RimWorld version hint |
| `createdUtc` | string | no | ISO-8601 UTC |

## Modlist hash (must match Core)

Identical to `RimworldMP.Core.Session.ModlistHash.Compute`:

1. Take active mods in **load order**.
2. `Trim` each `packageId`; drop empties.
3. Join with `\n` (UTF-8).
4. SHA256 → **lowercase** hex.

Golden vector: `["brrainz.harmony","RimworldMP.Core"]` → run `dotnet run --project tools/RimworldMP.Launcher -- self-test`.

## Mod consumer behavior

1. After Harmony bootstrap, `MpLobbyActions.TryConsumeLauncherHandoffAsync` reads the file once.
2. Applies `modlistHash` / `port` / `maxClients` into `MpConfig`.
3. Renames to `.ack` so a relaunch without the launcher does not re-auto-connect.
4. If `autoStartLobby`: host → `HostAsync`, client → `JoinAsync(host)`.
5. If missing / stale / `protocol` unsupported → fall back to in-mod lobby UI (corner button / **F10**) or DevMode actions.

Never treat handoff as authenticated; LAN trust model only.

## Stability

- Do **not** rename fields or change `protocol: 1` semantics without bumping `protocol` and updating both launcher and mod.
- Additive optional fields are OK for `protocol: 1` (`maxClients`, `rwBuild`, `createdUtc`, …).
- Hash algorithm must stay byte-identical to Core `ModlistHash.Compute`.

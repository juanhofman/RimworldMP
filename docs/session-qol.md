# Session QoL (LAN co-op)

Offline-testable session quality-of-life for lobby + soft recovery. Touches **UI / session** layers only (no LiteNetLib / SyncMethod applicator changes).

## Features

| Feature | API / surface | Notes |
| --- | --- | --- |
| Synced chat / system notices | `MpSession.SendChat`, `BroadcastSystemNotice`; `NetMessageType.Chat` / `SystemNotice` | **Not** lockstep `CommandOpcode`s — out-of-band so chat never enters the sim hasher |
| Player display names | `MpSession.SetDisplayName`, `PlayerRoster`; handoff `playerDisplayName` | Announced via `NetMessageType.PlayerInfo` |
| Host kick | `MpSession.HostKickPeer(playerId, reason?)` | Sends `KickNotice`, system notice, pauses if configured, then `DisconnectPeer` on LAN/loopback |
| Ready-check hard gate | `ReadyCheck.Required`, `MpSession.CanStartOrUnpause` | Blocks `HostEmitNextBatch` **and** unpause / speed-up `SubmitCommand` until all expected peers are ready with matching modlist. Lobby UI sets `MpConfig.ReadyRequired = true` on Host |
| Rejoin / recovery | `SessionRecovery.RequestRejoin`, `TryHostResyncPeer`, `HandlePeerDeparted` | Soft recover after freeze; peer-leave clears ready + system notice |
| Localized mismatch copy | `SessionMessages` + `mod/Languages/*/Keyed/RimworldMP_Session.xml` | EN defaults in Core; optional NL XML + `SessionMessages.nl.strings` via `LoadOptional` |
| Lobby UI | `LobbyPresenter` / `Dialog_RimworldMPLobby` | Kick buttons (host), ready toggle, chat send/history, display-name field |

## Protocol additions (still v3 framing)

New `NetMessageType` values (15–18): `Chat`, `SystemNotice`, `PlayerInfo`, `KickNotice`.  
`JoinRejectReason.DlcMismatch = 4` reserved for DLC fingerprint hard-rejects.

`LanTcpTransport.IsKnownType` upper bound includes `KickNotice`. `DisconnectPeer` lives on concrete transports (LAN + loopback) — **not** on `INetTransport`, to avoid LiteNetLib interface wars.

## Handoff

Additive protocol-1 field:

```json
"playerDisplayName": "Ada"
```

Applied into `MpConfig.PlayerDisplayName` by `MpLobbyActions.TryConsumeLauncherHandoffAsync`.

## Strings

| Source | Use |
| --- | --- |
| `SessionMessages` EN dictionary | Harness / headless |
| `src/RimworldMP.Core/Session/Locale/SessionMessages.nl.strings` | `SessionMessages.LoadOptional(path, "nl")` |
| `mod/Languages/English/Keyed/RimworldMP_Session.xml` | In-game Verse keyed (when game refs present) |
| `mod/Languages/Dutch/Keyed/RimworldMP_Session.xml` | Optional NL in-game |

Keys: `RMP.ProtocolMismatch`, `RMP.ModlistMismatch`, `RMP.DlcMismatch`, `RMP.LobbyFull`, peer/kick/ready/recovery/display-name helpers.

## Tests

`tests/DeterminismHarness/SessionQolTests.cs` covers chat sync, display names, kick, ready unpause gate, recovery rejoin polish, and localized mismatch strings.

## Non-goals

- LiteNetLib transport swap
- SyncMethod / command applicator changes
- Live RimWorld Immediate GUI verification (needs `RIMWORLD_DIR`)

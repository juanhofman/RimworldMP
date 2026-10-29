# LiteNetLib transport (LAN reliable UDP)

RimworldMP game sessions speak through `INetTransport`. **TCP remains the default and fallback** (`LanTcpTransport`). LiteNetLib is an opt-in LAN reliable-UDP implementation behind the same interface.

## Ports (do not collide)

| Port | Role |
| --- | --- |
| **30555** | Game MP — TCP *or* LiteNetLib UDP (same number, different socket type) |
| **30556** | Launcher UDP discovery only |
| **30557** | ModSync TCP only |

LiteNetLib binds **UDP 30555** (or `MpConfig.DefaultPort`). It never listens on 30556/30557.

## How to select the transport

### In-process / session (`MpConfig`)

```csharp
MpConfig.TransportKind = NetTransportKind.LiteNetLib; // or NetTransportKind.Tcp (default)
await SessionFacade.HostLobbyAsync(...);
// client:
await SessionFacade.JoinLobbyAsync(host, ...);
```

Or pass an explicit instance:

```csharp
await SessionFacade.HostLobbyAsync(transport: new LiteNetLibTransport { ... });
await SessionFacade.JoinLobbyAsync(host, transport: new LiteNetLibTransport { ... });
```

`NetTransportFactory.Create(kind)` / `TryParse("tcp"|"litenetlib"|"udp"|"lnl")` are shared helpers.

**Both peers must use the same kind.** TCP and LiteNetLib cannot interop on the wire (different sockets).

### Launcher handoff (additive `protocol: 1` field)

Optional JSON field on `launcher-handoff.json`:

```json
{
  "protocol": 1,
  "role": "client",
  "host": "192.168.1.20",
  "port": 30555,
  "transport": "litenetlib",
  "modlistHash": "…"
}
```

| Value | Effect |
| --- | --- |
| omitted / `"tcp"` | `MpConfig.TransportKind = Tcp` (default) |
| `"litenetlib"` / `"udp"` / `"lnl"` | `MpConfig.TransportKind = LiteNetLib` |

`MpLobbyActions.TryConsumeLauncherHandoffAsync` applies the field before auto host/join. No handoff protocol bump — additive only.

## Protocol gating (v4)

| Item | Value |
| --- | --- |
| Magic | still `RMP3` (v3 wire identity) |
| `NetProtocol.Version` | **4** |
| `MinSupportedVersion` | **3** (v3 TCP peers still accepted) |

v4 handshake may append a trailer after the modlist hash: `[u8 transportKind][u8 flags]` (`FlagPacingCapable`). v3 peers ignore unknown trailer bytes. Join still hard-gates on protocol range + modlist hash + lobby cap.

## JIP pacing

LiteNetLib uses `PayloadPacing`:

- Preferred chunk size `LiteNetLibJoinPointChunkBytes` (32 KiB) when both sides are v4+
- Small inter-chunk delay so large save blobs do not flood the reliable window
- Chunk size is advertised in an optional JoinPoint header trailer; v3 readers keep the 256 KiB default

## Reconnect / backoff

`LiteNetLibTransport.ReconnectPolicy` (default: 5 attempts, 100 ms → ×2, cap 5 s, light jitter):

- Applied on **initial** `StartClientAsync` for transient UDP failures / timeouts
- **Not** retried on hard `JoinReject` (protocol / modlist / lobby full)
- `TryReconnectAsync()` stops and re-joins the last endpoint; session code should then call `SessionRecovery.RequestRejoin` for a fresh join point

TCP path is unchanged (no UDP reconnect policy).

## Tests

`tests/DeterminismHarness/LiteNetLibTransportTests.cs` — real 127.0.0.1 UDP loopback (host↔client batch exchange, modlist reject, `MpConfig` selection) plus offline schedule / handshake / pacing unit checks.

```bash
dotnet test tests/DeterminismHarness -c Release --filter LiteNetLib
```

# Phase 4 — Harden

## Ready + modlist checks

```csharp
session.SetModlistHash(ModlistHash.Compute(packageIdsInOrder));
session.Ready.Required = true;
session.ExpectedPeers = 2;
session.SetReady(true);
```

Host will not emit tick batches while `Ready.Required && !IsLobbyReady`.

## Recovery rejoin

After a desync freeze, clients can soft-recover:

```csharp
SessionRecovery.RequestRejoin(session); // clears freeze flag + requests fresh JoinPoint
```

Host should keep `JoinPointSource` populated (latest autosave bytes).

## Protocol

- Magic `RMP2`, `Version = 2`, `MinSupportedVersion = 2`
- Incompatible peers are disconnected at handshake

## Soak

`Soak_1000_empty_ticks_stay_in_sync` in DeterminismHarness.

## QoL

- `PauseOnPeerLeave` (default true) submits a Pause command when a peer-leave notice arrives

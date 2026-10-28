# DeterminismHarness

xUnit scaffolding for Phase 1 lockstep invariants (no RimWorld install required):

- Command / batch / payload serialization
- `SyncedRand` parity for identical seeds
- `TickGate` stall behavior
- `DesyncDetector` freeze + log file
- Loopback host/client command stream
- LAN TCP handshake + batch exchange on loopback

```bash
dotnet test tests/DeterminismHarness
```

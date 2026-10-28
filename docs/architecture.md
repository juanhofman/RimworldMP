# Architecture

Canonical plan lives in the RimworldMP Project store (`docs/multiplayer-architecture.md`). Summary of locks:

- RimWorld **1.7 only** · `net10.0` · Harmony **3**
- Shared **single colony** · **LAN-first** `INetTransport` · **in-game host only**
- PackageId `RimworldMP.Core` · MIT

This repository is Phase 0: scaffold + transport/session stubs. Full lockstep is not implemented yet.

# Phase 3 — DLC + Compat

## DLC command surfaces

Opcodes (Core) + helpers (`RimworldMP.Compat.DlcHooks`):

| DLC | Commands |
| --- | --- |
| Royalty | `RoyaltyTitleChange`, `RoyaltyPermitUse` |
| Ideology | `IdeologyBeliefChange`, `IdeologyRitualStart` |
| Biotech | `BiotechGrowthVatAssign`, `BiotechGeneInstall` |
| Anomaly | `AnomalyEntityContain`, `AnomalyStudy` |

Shadow apply: `DlcWorld` / `DlcStateStore`. Live Verse/DLC method patches require `RIMWORLD_DIR` + DLC assemblies.

## Compat project

`RimworldMP.Compat` (optional Workshop companion later):

- `SimPatterns.StableOrder` — deterministic iteration
- `DlcHooks.*` — enqueue DLC commands through `MP.ExecuteCommand`

Canonical author surface: [`mod-author-api.md`](./mod-author-api.md) (`[SyncMethod]`, `[SyncField]`, `FieldWatch`, `HostInject` in `RimworldMP.API`).

## Out of scope here

- Per-map async scheduling / bandwidth cuts (not justified yet)
- Dedicated/headless host (locked out)
- Steam networking

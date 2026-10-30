# RimworldMP — Feature completeness matrix

Snapshot of **what ships on `main`** vs prefs and known blockers. Maintained as other agents land PRs; bump the **Audited** tip when updating.

| Field | Value |
| --- | --- |
| **Audited tip** | `2cafa5b` (VerseBind scaffold #22; product-polish pending) |
| **Tagged release** | **0.7.0** (`VERSION` / `About.xml` `modVersion`) — GitHub **pre-release** |
| **Toward next?** | Live RimWorld 1.7 binding smoke (`RIMWORLD_DIR`); fill-in map [verse-binding-checklist.md](./verse-binding-checklist.md) |
| **Prefs** | RimWorld **1.7 only** · shared colony · LAN-first · **in-game host only** · no dedicated server required |

Status vocabulary: **Done** · **Partial** · **Blocked on RIMWORLD_DIR** · **Won’t do per prefs**.

---

## Matrix

| Feature | Status | What’s on `main` | Gaps / next |
| --- | --- | --- | --- |
| **Lockstep** | **Done** (offline) / **Blocked on RIMWORLD_DIR** (live proof) | `TickGate`, host command batches, client stall, pause/speed opcodes, DeterminismHarness soak | Live `TickManager` Harmony path unproven without game DLLs |
| **Commands** | **Partial** (offline catalog **Done**) | Full Phase 1–3 opcode catalog + map/thing ids; policies/schedules/designators/forbid; shadow stores (`ColonyWorld` / `PolicyWorld` / `ZoneWorld` / `DlcWorld`); harness serialize/apply | Verse apply stubby for trade/caravan/policies; live apply needs `RIMWORLD_DIR` |
| **SyncMethod** | **Done** (offline) | `MP.CallSyncMethod` → opcode `90` → applicator invoke; `SyncMethodCodec`; `[SyncMethod]` attribute scan; `ColonyCommands` | Live author mods still need game session |
| **JIP** | **Partial** / **Blocked on RIMWORLD_DIR** | Chunked transfer + paced LiteNetLib chunks + `CompleteOrThrow` fail-loud; `SaveBlobPipeline` / `ISaveLoad` / `MemorySaveLoad`; join hard-gate (`RMP3` v4 / MinSupported 3) | `VerseSaveLoad` SaveGame/LoadGame needs install |
| **Launcher** | **Done** | Avalonia 11 UI + CLI: locate RW, mod prepare/hash, UDP discovery, host/join, `launcher-handoff.json`, process launch ([launcher-mvp.md](./launcher-mvp.md)) | Avalonia visual QA optional; launching game binary needs a real install |
| **Mod sync** | **Done** | `RimworldMP.ModSync` host share TCP **30557**, client compare/pull + content-hash verify, Workshop **cache** secondary; SteamCMD stub fail-loud ([mod-manager-lan-sync.md](./mod-manager-lan-sync.md)) | Live Workshop **download** / SteamCMD: intentionally unsupported |
| **Transports** | **Done** (LAN) (+ **Won’t do** rows) | `INetTransport`: **Loopback** + **LAN TCP** (default) + **LiteNetLib** UDP (`MpConfig.TransportKind` / handoff `transport`, port **30555**) — [transport-litenetlib.md](./transport-litenetlib.md) | **Steam** / **dedicated host**: **Won’t do per prefs** |
| **API** | **Done** (offline) / **Partial** (live watches) | `[SyncMethod]` / `[SyncField]`, `FieldWatch`, `HostInject`, attribute registration, `CallSyncMethod` schedule, XML docs; examples + `ApiRegistrationTests`; [mod-author-api.md](./mod-author-api.md) | Live field-watch → command emission still stub-bound |
| **DLC hooks** | **Partial** / **Blocked on RIMWORLD_DIR** | Phase 3 opcodes + `DlcHooks.*` + `DlcStateStore` shadow apply | Live DLC Harmony targets need DLC assemblies |
| **Desync** | **Done** (offline) / **Blocked on RIMWORLD_DIR** (world hash samples) | Fingerprint folds protocol/tick/RNG seed/**drawCount**/**OpinionHash**/counters/cmd hash + world slot; local-only dumps ([desync-dumps.md](./desync-dumps.md)) | Live world-fingerprint fill + multi-peer proof need install |
| **QoL** | **Done** (offline) / **Blocked on RIMWORLD_DIR** (live Immediate GUI) | Lobby UI (F10 / button), synced **chat** / system notices, display names + handoff `playerDisplayName`, host **kick**, ready hard gate (emit + unpause), recovery polish, EN/NL mismatch strings ([session-qol.md](./session-qol.md)) | Live lobby layout / kick buttons need `RIMWORLD_DIR` |

### Prefs / explicit non-goals

| Item | Status |
| --- | --- |
| RimWorld **1.7 only** (no 1.6) | **Done** (`About.xml` `supportedVersions`) |
| Shared single colony (not multi-faction) | **Done** (architecture lock) |
| LAN-first play | **Done** (TCP default + LiteNetLib opt-in + launcher discovery + mod sync) |
| In-game host only | **Done** |
| Dedicated / headless server | **Won’t do per prefs** |
| Steam networking as primary transport | **Won’t do per prefs** (LAN-first) |
| Cloud mod distribution | **Won’t do per prefs** (LAN file transfer) |

---

## Command catalog (offline / NO_GAME_REFS)

| Area | Opcodes | Serialize + schedule | Shadow apply | Verse apply |
| --- | --- | --- | --- | --- |
| Time | `Pause`, `Speed` | Yes | N/A | **Blocked** — TickManager |
| Draft | `Draft`, `Undraft` | Yes | N/A | **Blocked** — drafter |
| Jobs | `JobGive` | Yes | Payload decode | **Blocked** — JobMaker |
| Zones | `ZoneAdd/Remove/Clear/Create` | Yes | `ZoneWorld` | **Blocked** — ZoneManager |
| Designators | `DesignatorCell`, `ForbidSet` | Yes | `PolicyWorld` | **Blocked** |
| Areas | `RestrictAreaSet` | Yes | N/A | **Blocked** |
| Schedules / policies | `WorkPrioritySet`, `TimetableSet`, `FoodRestrictionAssign`, `OutfitAssign`, `DrugPolicyAssign`, `MedicalCareSet`, `HostilityResponseSet` | Yes | `PolicyWorld` | **Blocked** |
| Bills | `BillSet`, `BillDelete`, `BillReorder` | Yes | `ColonyWorld` | **Partial stub** |
| Research | `ResearchStart`, `ResearchStop` | Yes | `ColonyWorld` | **Partial stub** |
| Trade | `TradeAccept`, `TradeCancel` | Yes | `ColonyWorld` | **Stub** |
| Caravan | `CaravanForm/Add/Remove/Disband` | Yes | `ColonyWorld` | **Stub** |
| SyncMethod | `SyncMethodInvoke` (90) | Yes | Registry invoke | Author method |
| DLC | 100–131 | Yes | `DlcWorld` | **Blocked** |

Public API: `MP.CallSyncMethod` / `SyncMethodCodec` / `ColonyCommands.*` — see [api.md](./api.md), [track-a-p1-syncmethod.md](./track-a-p1-syncmethod.md).

---

## Version policy

- **Current release line:** `0.7.0` — offline feature cut (SyncMethod, Compat API, Session QoL, LiteNetLib, quality); GitHub **pre-release** (see [CHANGELOG](../CHANGELOG.md), [release-notes-v0.7.0.md](./release-notes-v0.7.0.md)).
- **Requires RimWorld 1.7 install for live play; Verse apply still scaffolded.**
- When bumping: keep `VERSION`, `mod/About/About.xml` `modVersion`, and CHANGELOG in lockstep.

---

## Still Verse-blocked (install required)

Fill-in map: [verse-binding-checklist.md](./verse-binding-checklist.md) (`VerseBindingRegistry`).

1. Harmony prefixes that touch Verse types (draft/job/zone/bill/research/Rand/lobby UI) — shapes isolated; signatures **Unverified**.
2. Trade deal commit, caravan formation, policy pawn settings — **Scaffold** (no invented Prefixes).
3. World fingerprint samples into desync hash.
4. Live 2P smoke 30+ min.
5. Confirm `Rand.Chance` / `Bool` signatures on installed 1.7.

Session QoL (chat / kick / ready / i18n) is offline-testable — see [session-qol.md](./session-qol.md). LiteNetLib LAN transport is selectable — see [transport-litenetlib.md](./transport-litenetlib.md).

---

## How to update this doc

1. Fast-forward `main`; set **Audited tip** to the merge SHA.
2. Adjust only rows whose status or notes changed; keep the four status words.
3. If a PR ships a version bump, keep `VERSION`, `About.xml` `modVersion`, and CHANGELOG in lockstep.
4. Mirror the file to the Project store path `docs/feature-completeness.md` when agents maintain both.

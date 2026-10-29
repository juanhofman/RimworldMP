# RimworldMP — Feature completeness matrix

Snapshot of **what ships on `main`** vs prefs and known blockers. Maintained as other agents land PRs; bump the **Audited** tip when updating.

| Field | Value |
| --- | --- |
| **Audited tip** | `dev-compat-api-examples` (Compat/API public surface; rebase tip after merge) |
| **Tagged release** | **0.6.0** (`VERSION` / `About.xml` `modVersion`) |
| **Toward 0.7.0?** | **Not yet** — see [Version policy](#version-policy) |
| **Prefs** | RimWorld **1.7 only** · shared colony · LAN-first · **in-game host only** · no dedicated server required |

Status vocabulary: **Done** · **Partial** · **Blocked on RIMWORLD_DIR** · **Won’t do per prefs**.

---

## Matrix

| Feature | Status | What’s on `main` | Gaps / next |
| --- | --- | --- | --- |
| **Lockstep** | **Done** (offline) / **Blocked on RIMWORLD_DIR** (live proof) | `TickGate`, host command batches, client stall, pause/speed opcodes, DeterminismHarness soak | Live `TickManager` Harmony path unproven without game DLLs |
| **Commands** | **Partial** | Opcodes for draft/job/zones/restrict + Phase 2 colony (bills/research/trade/caravan) + map-safe payloads (`mapId`); shadow stores + harness | Verse apply still stubby for trade/caravan; SyncMethod→apply wiring in flight (RMP-22); live apply needs `RIMWORLD_DIR` |
| **JIP** | **Partial** / **Blocked on RIMWORLD_DIR** | Join-point transfer (`RMP3` v3), `ISaveLoad` / `MemorySaveLoad`, join hard-gate on protocol + modlist hash | `VerseSaveLoad` SaveGame/LoadGame needs install; mid-session JIP unproven in live RW |
| **Launcher** | **Done** | Avalonia 11 UI + CLI: locate RW, mod prepare/hash, UDP discovery, host/join, `launcher-handoff.json`, process launch ([launcher-mvp.md](./launcher-mvp.md)) | Avalonia visual QA optional; launching game binary needs a real install |
| **Mod sync** | **Done** | `RimworldMP.ModSync` host share TCP **30557**, client compare/pull + content-hash verify, Workshop **cache** secondary, Mods UI/CLI hooks ([mod-manager-lan-sync.md](./mod-manager-lan-sync.md)) | Live Workshop **download** / SteamCMD: stub only — prefer LAN transfer (by design) |
| **Transports** | **Partial** (+ **Won’t do** rows) | `INetTransport`: **Loopback** + **LAN TCP** (`LanTcpTransport`, port **30555**) | **LiteNetLib** not implemented (RMP-23 in flight). **Steam net** / **dedicated/headless host**: **Won’t do per prefs** |
| **API** | **Partial** | `RimworldMP.API`: `[SyncMethod]` / `[SyncField]`, `FieldWatch`, `HostInject`, attribute/type/assembly registration + `TryInvokeSyncMethod`, XML docs; examples `CompatHello` / `CompatHostIo` (`NO_GAME_REFS`); harness `ApiRegistrationTests`; [mod-author-api.md](./mod-author-api.md) | Core→Verse **applicator** pipeline still incomplete (RMP-22); live session command emission for field watches stub-bound |
| **DLC hooks** | **Partial** / **Blocked on RIMWORLD_DIR** | Phase 3 opcodes + `DlcHooks.*` + `DlcStateStore` shadow apply | Live Royalty/Ideology/Biotech/Anomaly Harmony targets need DLC assemblies under `RIMWORLD_DIR` |
| **Desync** | **Done** (offline) / **Blocked on RIMWORLD_DIR** (world hash samples) | Fingerprint (protocol/tick/RNG/counters/cmd hash + reserved world slot), freeze, **local-only** dumps + overlay path ([desync-dumps.md](./desync-dumps.md)), harness coverage | Live world-fingerprint fill + multi-peer desync proof need game install |
| **QoL** | **Partial** | Lobby UI (F10 / button), ready gate + modlist peer display, `PauseOnPeerLeave`, recovery rejoin API, handoff status, desync freeze overlay | In-session **chat**, host **kick**, ready UX polish (RMP-27) |

### Prefs / explicit non-goals

| Item | Status |
| --- | --- |
| RimWorld **1.7 only** (no 1.6) | **Done** (`About.xml` `supportedVersions`) |
| Shared single colony (not multi-faction) | **Done** (architecture lock) |
| LAN-first play | **Done** (TCP + launcher discovery + mod sync) |
| In-game host only | **Done** |
| Dedicated / headless server | **Won’t do per prefs** |
| Steam networking as primary transport | **Won’t do per prefs** (LAN-first) |
| Cloud mod distribution | **Won’t do per prefs** (LAN file transfer) |

---

## Version policy

- **Current release line:** `0.6.0` — UI + launcher + P0 sim + ModSync (see [CHANGELOG](../CHANGELOG.md), [release-notes-v0.6.0.md](./release-notes-v0.6.0.md)).
- **Do not bump to 0.7.0** until a coherent slice of **code** features lands on `main` (candidates: SyncMethod applicators, LiteNetLib transport, session QoL chat/kick, Compat API expansions). Richer desync dumps alone (#14) are **Unreleased** polish, not a 0.7 cut.
- When bumping: keep `VERSION`, `mod/About/About.xml` `modVersion`, and CHANGELOG in lockstep.

---

## In-flight (not justifying 0.7.0 yet)

Tracked in Project notes as RMP-22…27 (excluding this matrix doc):

| Track | Intent |
| --- | --- |
| SyncMethod + colony applicators / RNG | Finish registration→deterministic invoke and thicken Verse apply paths |
| LiteNetLib LAN transport | Alternate `INetTransport` behind the same facade |
| Compat API + examples | **Landed** on `dev-compat-api-examples` (public API + examples + docs + harness); Core applicators remain separate |
| Session QoL | Chat, kick, ready polish |

Live RimWorld **1.7 smoke** remains queued until a Mac/PC with `RIMWORLD_DIR` is available.

---

## How to update this doc

1. Fast-forward `main`; set **Audited tip** to the merge SHA.
2. Adjust only rows whose status or notes changed; keep the four status words.
3. If a PR ships a 0.7-worthy slice, bump version trio + move CHANGELOG **Unreleased** → dated section in the same PR when possible.
4. Mirror the file to the Project store path `docs/feature-completeness.md` when agents maintain both.

# Verse binding checklist (RimWorld 1.7)

Offline scaffold so when `RIMWORLD_DIR` appears, work is **fill-in + smoke**, not redesign.

| Field | Value |
| --- | --- |
| **Catalog** | `RimworldMP.VerseBind.VerseBindingRegistry` |
| **Apply shapes** | `src/RimworldMP/VerseBind/Apply/` |
| **Capture patches** | `src/RimworldMP/Patches/` (compiled only with game refs) |
| **Harness** | `tests/DeterminismHarness/VerseBindingTests.cs` |
| **Legal** | Do **not** vendor or decompile proprietary game assemblies into the repo |

Verification vocabulary (also `VerseBindingVerification`):

| Status | Meaning |
| --- | --- |
| **Scaffold** | Intentional incomplete — no live Prefix / commit yet |
| **Unverified** | Best-effort target from public RW / Harmony docs; confirm on install |
| **Documented** | Shape isolated in-repo; install smoke still required |

---

## How to use with an install

1. Set `RIMWORLD_DIR` to the folder containing `Assembly-CSharp.dll`, `Waylith.dll`, `UnityStubs.dll`.
2. Build **without** `RIMWORLDMP_NO_GAME_REFS` (omit the force flag).
3. For each **Unverified** / **Scaffold** row: confirm type+member, adjust Signature, run acceptance test.
4. Prefer **incomplete + documented** over a silent wrong Harmony Prefix.
5. Flip registry `Verification` only after a live smoke passes; update this table in the same PR.

---

## Binding map

Canonical machine-readable list: `VerseBindingRegistry.All`. Summary:

### Lockstep / time

| Id | Kind | Target (best-effort) | File | Verif. | Acceptance |
| --- | --- | --- | --- | --- | --- |
| `tick.do-single-tick` | Prefix | `Verse.TickManager.DoSingleTick()` | `Patches/TickManager_DoSingleTick_Patch.cs` | Unverified | Host+client stall until batch; freeze blocks tick |
| `tick.toggle-paused` | Prefix | `TickManager.TogglePaused()` | `Patches/TickManager_TimeControls_Patch.cs` | Unverified | Pause → `CommandOpcode.Pause` |
| `tick.cur-time-speed-setter` | Setter Prefix | `TickManager.CurTimeSpeed.set` | same | Unverified | Speed → `CommandOpcode.Speed`; re-entry OK |
| `apply.pause` | Apply | `CurTimeSpeed` | `VerseBind/Apply/VerseTickApply.cs` | Documented | Stub no-throw; live pause mutates TM |
| `apply.speed` | Apply | `CurTimeSpeed` / `TimeSpeed` | same | Documented | Indices 0–4; confirm `Ultrafast` |

### Draft / jobs / zones / areas

| Id | Kind | Target | File | Verif. | Acceptance |
| --- | --- | --- | --- | --- | --- |
| `draft.drafted-setter` | Setter Prefix | `Pawn_DraftController.Drafted.set` | `Patches/Draft_Patches.cs` | Unverified | Draft UI → Draft/Undraft cmds |
| `apply.draft` | Apply | `drafter.Drafted` | `VerseBind/Apply/VerseDraftApply.cs` | Documented | Stub no-throw |
| `job.try-take-ordered` | Prefix | `Pawn_JobTracker.TryTakeOrderedJob` | `Patches/JobGive_Patches.cs` | Unverified | Ordered job → JobGive + mapId; **confirm overload** |
| `apply.job-give` | Apply | `JobMaker` / `TryTakeOrderedJob` | `VerseBind/Apply/VerseJobApply.cs` | Unverified | Stub no-throw; live JobDef resolve |
| `zone.add-cell` / `zone.remove-cell` | Prefix | `Zone.AddCell` / `RemoveCell` | `Patches/Zone_Patches.cs` | Unverified | Paint/erase → ZoneAdd/Remove |
| `apply.zone` | Apply | `ZoneManager` / stockpile/growing | `VerseBind/Apply/VerseZoneApply.cs` | Unverified | Shadow + Verse cells |
| `area.restriction-setter` | Setter Prefix | `Pawn_PlayerSettings.AreaRestrictionInPawnCurrentMap.set` | `Patches/RestrictArea_Patches.cs` | Unverified | Restrict → RestrictAreaSet |
| `apply.restrict-area` | Apply | same property | `VerseBind/Apply/VerseRestrictAreaApply.cs` | Unverified | Stub no-throw |

### Colony (bills / research / trade / caravan)

| Id | Kind | Target | File | Verif. | Acceptance |
| --- | --- | --- | --- | --- | --- |
| `colony.research-set-current` | Prefix | `ResearchManager.SetCurrentProject` | `Patches/Colony_Patches.cs` | Unverified | Research UI → ResearchStart |
| `colony.bill-stack-add` / `…-delete` | Prefix | `BillStack.AddBill` / `Delete` | same | Unverified | Bill UI → BillSet/Delete |
| `apply.research` | Apply | SetCurrentProject; **stop API TBD** | `VerseBind/Apply/VerseColonyApply.cs` | Unverified | Start works; ResearchStop scaffold (no guessed clear) |
| `apply.bill` | Apply | BillStack / Bill_Production | same | Unverified | BillSet/Delete shapes; BillReorder scaffold |
| `apply.trade` | Apply | TradeDeal (TBD) | same | **Scaffold** | Shadow + log stub; **no invented Prefix** |
| `apply.caravan` | Apply | CaravanMaker (TBD) | same | **Scaffold** | Shadow + log stub; **no invented Prefix** |
| `capture.trade` | Prefix | (TBD) | (none yet) | **Scaffold** | Name accept method under install |
| `capture.caravan` | Prefix | (TBD) | (none yet) | **Scaffold** | Name form method under install |
| `capture.policy` / `apply.policy` | Prefix / Apply | work/timetable/… (TBD) | `VersePolicyApply` + PolicyWorld | **Scaffold** | Shadow only until pawn settings APIs confirmed |

### Rand

| Id | Target | File | Verif. | Acceptance |
| --- | --- | --- | --- | --- |
| `rand.value` / `range-int` / `range-float` / `chance` / `bool` | `Verse.Rand` getters/methods | `Patches/Rand_Patches.cs` | Unverified | Session → SyncedRand + counters; **confirm Chance/Bool on 1.7** |

### Lobby / JIP / DLC

| Id | Kind | Target | File | Verif. | Acceptance |
| --- | --- | --- | --- | --- | --- |
| `lobby.ui-root-on-gui` | Postfix | `UIRoot.UIRootOnGUI` | `Patches/LobbyUi_Patches.cs` | Unverified | F10 + brand button + desync overlay |
| `lobby.ui-open` | UI | `Dialog_RimworldMPLobby.Open` | `UI/LobbyUi.cs` | Documented | Stub no-op; `IsVerseUiAvailable` false offline |
| `jip.verse-save-load` | SaveLoad | `GameDataSaveLoader.SaveGame/LoadGame` | `Join/VerseSaveLoad.cs` | Unverified | Live JIP blob; offline factory returns null (fail-loud if blob w/o loader) |
| `dlc.game-load-game` | Postfix | `Game.LoadGame` | `Patches/Dlc_Patches.cs` | **Scaffold** | Boundary only; real DLC targets need DLC DLLs |

---

## What remains strictly install-bound

1. Confirm every **Unverified** Harmony target signature under Harmony 3 injection checks.
2. Fill **Scaffold** trade / caravan / policy capture + Verse commit (no guessed Prefixes offline).
3. Confirm `Rand.Chance` / `Bool` (and optionally Gaussian/MTB) on 1.7.
4. Live `VerseSaveLoad` SaveGame/LoadGame + GenFilePaths paths.
5. World fingerprint samples into desync hash; live 2P smoke 30+ min.
6. DLC-specific Harmony targets once Royalty/Ideology/Biotech/Anomaly assemblies are present.

Offline already green: opcode catalog, shadow stores, SyncMethod, registry + stub smoke (`VerseBindingSmoke.RunNoThrowSmoke`).

---

## Related

- [feature-completeness.md](./feature-completeness.md) — matrix / Verse-blocked list
- [code-quality.md](./code-quality.md) — `RIMWORLDMP_NO_GAME_REFS` / TreatWarningsAsErrors
- [p0-sim-correctness.md](./p0-sim-correctness.md) — lockstep / JIP offline guarantees

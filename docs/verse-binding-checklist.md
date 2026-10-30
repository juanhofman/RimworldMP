# Verse binding checklist (RimWorld 1.7)

Offline scaffold so when `RIMWORLD_DIR` appears, work is **fill-in + smoke**, not redesign.

| Field | Value |
| --- | --- |
| **Catalog** | `RimworldMP.VerseBind.VerseBindingRegistry` |
| **Apply shapes** | `src/RimworldMP/VerseBind/Apply/` |
| **Capture patches** | `src/RimworldMP/Patches/` (compiled only with game refs) |
| **Harness** | `tests/DeterminismHarness/VerseBindingTests.cs` |
| **Legal** | Do **not** vendor or decompile proprietary game assemblies into the repo |
| **Live install** | Steam `1.7.4990 rev1069` (Waylith) — `RIMWORLD_DIR` = install root containing `Assembly-CSharp.dll`, `Waylith.dll`, `UnityStubs.dll` |

Verification vocabulary (also `VerseBindingVerification`):

| Status | Meaning |
| --- | --- |
| **Scaffold** | Intentional incomplete — no live Prefix / commit yet |
| **Unverified** | Best-effort target from public RW / Harmony docs; confirm on install |
| **Documented** | Shape isolated in-repo; compile/reflection confirmed on install; play smoke still strengthens proof |

---

## How to use with an install

1. Set `RIMWORLD_DIR` to the folder containing `Assembly-CSharp.dll`, `Waylith.dll`, `UnityStubs.dll` (1.7 Waylith: **install root**, not `RimWorldWin64_Data\Managed`).
2. Build **without** `RIMWORLDMP_NO_GAME_REFS` (omit the force flag).
3. For each **Unverified** / **Scaffold** row: confirm type+member, adjust Signature, run acceptance test.
4. Prefer **incomplete + documented** over a silent wrong Harmony Prefix.
5. Flip registry `Verification` only after signatures are confirmed on install; update this table in the same PR.

---

## Binding map (post 1.7.4990 fill-in)

Canonical machine-readable list: `VerseBindingRegistry.All`. Summary:

### Lockstep / time

| Id | Kind | Target | File | Verif. | Notes |
| --- | --- | --- | --- | --- | --- |
| `tick.do-single-tick` | Prefix | `TickManager.DoSingleTick()` | `TickManager_DoSingleTick_Patch.cs` | **Documented** | + world fingerprint sample /60 ticks |
| `tick.toggle-paused` | Prefix | `TogglePaused()` | `TickManager_TimeControls_Patch.cs` | **Documented** | |
| `tick.cur-time-speed-setter` | Setter Prefix | `CurTimeSpeed.set` | same | **Documented** | |
| `apply.pause` / `apply.speed` | Apply | `CurTimeSpeed` | `VerseTickApply.cs` | Documented | |

### Draft / jobs / zones / areas

| Id | Verif. | Notes |
| --- | --- | --- |
| draft / job / zone / restrict capture+apply | **Documented** | JobTracker.pawn protected → AccessTools; Zone_Stockpile ctor needs preset+manager; PlayerSettings.pawn private |

### Colony

| Id | Verif. | Notes |
| --- | --- | --- |
| research / bills (+ reorder) | **Documented** | `StopProject` confirmed |
| trade capture+apply | **Documented** | `TradeDeal.TryExecute` |
| caravan capture+apply | **Documented** | `CaravanMaker.MakeCaravan`; add/remove/disband still shadow |
| policy work+timetable | **Documented** | outfit/food/drug/medical/forbid still shadow |

### Rand / lobby / JIP

| Id | Verif. | Notes |
| --- | --- | --- |
| Rand Value/Range/Chance/Bool | **Documented** | Chance(float) + Bool getter confirmed |
| lobby UIRootOnGUI | **Documented** | `Screen.width/height` (no `Verse.UI.screenWidth`); DebugAction → `LudeonTK` |
| VerseSaveLoad | **Documented** | SaveGame/LoadGame + GenFilePaths confirmed |
| DLC LoadGame | **Scaffold** | needs DLC assembly targets |

---

## What remains install-bound / smoke-bound

1. Live Harmony injection checks in a running game (host lobby smoke).
2. Policy outfit/food/drug/medical/forbid capture Prefixes.
3. Caravan add/remove/disband Verse commit.
4. Live 2P smoke 30+ min + JIP blob round-trip in play.
5. DLC-specific Harmony targets once Royalty/Ideology/Biotech/Anomaly assemblies are targeted.

Offline already green: opcode catalog, shadow stores, SyncMethod, registry + stub smoke (`VerseBindingSmoke.RunNoThrowSmoke`).

---

## Related

- [feature-completeness.md](./feature-completeness.md) — matrix / Verse-blocked list
- [code-quality.md](./code-quality.md) — `RIMWORLDMP_NO_GAME_REFS` / TreatWarningsAsErrors
- [p0-sim-correctness.md](./p0-sim-correctness.md) — lockstep / JIP offline guarantees

# RimworldMP UI design brief (release)

**Goal:** LAN co-op UI that feels like **vanilla RimWorld menus** — colony dialogs, not a SaaS landing page. Brand **RimworldMP** stays clear on the launcher Home as a colonial menu title.

## Visual direction
- Atmosphere: warm **dark wood / ink brown** panels (`#1C1410`–`#2B2118`) with **parchment** content surfaces (`#E4D5B5` / `#D6C4A0`), thin hard borders (`#5C4330`) — Dialog_/Widgets feel.
- Accents: amber/orange **colonial buttons** (`#C97828`) like vanilla menu buttons; muted **gold/cream** labels (`#C4A574` / `#F2E6C9`). Dark ink text on parchment.
- Avoid: charcoal/steel cyber themes, purple gradients, blueprint grids, neon geometric display type, flat SaaS heroes.
- Typography: condensed menu display (**Barlow Condensed** / similar weight); colony title weight (**Cinzel**); readable body (**Source Sans 3**). No Inter/Roboto/Arial/system-only stacks when custom fonts are available. Avoid Space Grotesk / Syne cyber pairing.
- Background: subtle **parchment grain** and wood texture — not flat fill, not topographic/grid.
- Motion: 2–3 intentional transitions (panel fade, colonial CTA press, host “live” pulse). No glow spam.
- Cards: only for interactive host rows / mod rows; no decorative card chrome.
- Density: one job per screen (Host | Join | Mods | Launch).

## Surfaces
1. **Launcher** (external Avalonia): Host, Join (discovery + IP), Mods (Harmony-first order, hash), Launch — wood chrome + parchment content.
2. **In-mod lobby**: Host/Join status, peers, ready, modlist match, start/abort — readable on RimWorld’s Immediate GUI / Verse widgets without looking like DebugOutput (still game-native chrome).

## Constraints
- RW 1.7, shared colony, LAN, in-game host only.
- Local-only; no telemetry.
- No game install required to compile; Verse UI behind `#if` / NO_GAME_REFS stubs where needed.

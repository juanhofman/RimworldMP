#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Structural DLC entry hooks. Concrete Royalty/Ideology/Biotech/Anomaly type names vary by
/// installed DLC; these patches document the injection boundary and no-op when types are absent.
/// Full DLC method targets are applied when RIMWORLD_DIR + DLC assemblies are present.
/// </summary>
/// <remarks>
/// SCAFFOLD: Game.LoadGame Postfix is a safe boundary marker only — not a DLC command capture.
/// Prefer incomplete + documented over inventing Royalty/Ideology Prefixes without DLC DLLs.
/// </remarks>
[HarmonyPatch]
internal static class Dlc_Patches
{
    // Placeholder PatchAll-safe type. Real DLC targets register when RIMWORLD_DIR + DLC resolve.
    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    [HarmonyPostfix]
    private static void Game_LoadGame_Postfix()
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true } || CommandApplyReentry.IsApplying)
            return;
        // Opportunity to re-hash modlist / DLC set after load.
        session.Desync.IncrementCounter("game.LoadGame");
    }
}
#endif

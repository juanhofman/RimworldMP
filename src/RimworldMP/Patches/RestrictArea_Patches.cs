#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Allowed-area / restrict-area changes → RestrictAreaSet commands.
/// </summary>
[HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.AreaRestrictionInPawnCurrentMap), MethodType.Setter)]
internal static class Pawn_PlayerSettings_AreaRestriction_Patch
{
    private static bool Prefix(Pawn_PlayerSettings __instance, Area value)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;
        if (CommandApplyReentry.IsApplying)
            return true;

        var pawn = __instance.pawn;
        if (pawn is null)
            return true;

        var areaId = value?.ID ?? -1;
        session.SubmitCommand(CommandOpcode.RestrictAreaSet, CommandPayloads.RestrictArea(pawn.thingIDNumber, areaId));
        return false;
    }
}
#endif

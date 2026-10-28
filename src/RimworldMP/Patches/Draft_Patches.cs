#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Draft / undraft → lockstep commands. Structural patch on Pawn_DraftController.Drafted setter.
/// </summary>
[HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
internal static class Pawn_DraftController_Drafted_Patch
{
    private static bool Prefix(Pawn_DraftController __instance, bool value)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;
        if (CommandApplyReentry.IsApplying)
            return true;

        var pawn = __instance.pawn;
        if (pawn is null)
            return true;

        session.SubmitCommand(
            value ? CommandOpcode.Draft : CommandOpcode.Undraft,
            CommandPayloads.Draft(pawn.thingIDNumber, value));
        return false;
    }
}
#endif

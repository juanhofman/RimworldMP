#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.CommandApply;
using RimworldMP.Core.Commands;
using RimworldMP.Core.Session;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Convert local pause / speed changes into lockstep commands. Direct TickManager mutation is skipped
/// while a session is active; the command applicator reapplies on every peer.
/// </summary>
[HarmonyPatch]
internal static class TickManager_TimeControls_Patch
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TogglePaused))]
    [HarmonyPrefix]
    private static bool TogglePaused_Prefix(TickManager __instance)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        var willPause = !__instance.Paused;
        session.SubmitCommand(CommandOpcode.Pause, CommandPayloads.Pause(willPause));
        return false;
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.CurTimeSpeed), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool CurTimeSpeed_Setter_Prefix(TimeSpeed value)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        // Avoid re-entrancy when CommandApplicator sets CurTimeSpeed.
        if (CommandApplyReentry.IsApplying)
            return true;

        byte index = value switch
        {
            TimeSpeed.Paused => 0,
            TimeSpeed.Normal => 1,
            TimeSpeed.Fast => 2,
            TimeSpeed.Superfast => 3,
            TimeSpeed.Ultrafast => 4,
            _ => 1,
        };
        session.SubmitCommand(CommandOpcode.Speed, CommandPayloads.Speed(index));
        return false;
    }
}
#endif

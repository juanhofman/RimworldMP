using RimworldMP.Core.Commands;
using RimworldMP.Core.World;

namespace RimworldMP.VerseBind.Apply;

/// <summary>
/// Policy / designator / forbid apply. Offline: <see cref="PolicyWorld"/> only.
/// Live Verse pawn settings (timetable, food, outfit, …) need RIMWORLD_DIR — no guessed Prefixes.
/// </summary>
public static class VersePolicyApply
{
    public static void Apply(MpCommand cmd)
    {
        PolicyWorld.Current.ApplyCommand(cmd);
        // SCAFFOLD: Verse commit for WorkPriority / Timetable / Food / Outfit / Drug / Medical /
        // Hostility / DesignatorCell / ForbidSet is intentionally omitted until install validates
        // Pawn_WorkSettings / Schedule / OutfitTracker / ForbidUtility signatures.
        _ = cmd;
    }
}

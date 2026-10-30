#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP.VerseBind.Apply;

/// <summary>Pause / speed apply shapes for <c>TickManager</c>.</summary>
public static class VerseTickApply
{
    public static void ApplyPause(bool paused)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        // UNVERIFIED (RW 1.7): Find.TickManager, CurTimeSpeed, TimeSpeed enum members.
        var tm = Find.TickManager;
        if (tm is null) return;
        if (paused)
            tm.CurTimeSpeed = TimeSpeed.Paused;
        else if (tm.CurTimeSpeed == TimeSpeed.Paused)
            tm.CurTimeSpeed = TimeSpeed.Normal;
#else
        _ = paused;
#endif
    }

    public static void ApplySpeed(byte speedIndex)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        // UNVERIFIED (RW 1.7): TimeSpeed.Ultrafast still present under Waylith.
        var tm = Find.TickManager;
        if (tm is null) return;
        tm.CurTimeSpeed = speedIndex switch
        {
            0 => TimeSpeed.Paused,
            1 => TimeSpeed.Normal,
            2 => TimeSpeed.Fast,
            3 => TimeSpeed.Superfast,
            4 => TimeSpeed.Ultrafast,
            _ => TimeSpeed.Normal,
        };
#else
        _ = speedIndex;
#endif
    }
}

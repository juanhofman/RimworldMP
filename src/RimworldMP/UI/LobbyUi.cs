namespace RimworldMP.UI;

/// <summary>
/// Entry points for opening lobby / overlay. Real Verse windows live behind game-ref guards;
/// under <c>RIMWORLDMP_NO_GAME_REFS</c> these are no-op stubs so the harness still links.
/// </summary>
public static class LobbyUi
{
    /// <summary>Toggle / open the in-mod lobby window (no-op without game refs).</summary>
    public static void Open()
    {
#if !RIMWORLDMP_NO_GAME_REFS
        Dialog_RimworldMPLobby.Open();
#endif
    }

    /// <summary>True when a Verse WindowStack lobby can be shown.</summary>
    public static bool IsVerseUiAvailable
    {
        get
        {
#if RIMWORLDMP_NO_GAME_REFS
            return false;
#else
            return true;
#endif
        }
    }
}

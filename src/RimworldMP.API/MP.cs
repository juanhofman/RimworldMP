namespace RimworldMP.API;

/// <summary>
/// Public entry points for third-party mods. Phase 0 stubs — not wired to a live session yet.
/// </summary>
public static class MP
{
    /// <summary>True when RimworldMP assemblies are loaded (always true if this API is referenced at runtime).</summary>
    public static bool Enabled => true;

    /// <summary>True when a multiplayer session is active. Stub: always false in Phase 0.</summary>
    public static bool IsInSession => false;

    /// <summary>True when this peer is the in-game host. Stub: always false in Phase 0.</summary>
    public static bool IsHost => false;

    /// <summary>Local player id within the session, or -1 when not in session.</summary>
    public static int LocalPlayerId => -1;
}

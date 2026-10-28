using System.Reflection;
using RimworldMP.API;

namespace RimworldMP.Examples.CompatHello;

/// <summary>
/// Minimal MP-ready pattern: register a sync method and never mutate sim from raw UI clicks.
/// This example compiles against the API only (no Harmony / Verse required).
/// </summary>
public static class CompatHelloMod
{
    public static void Initialize()
    {
        MP.RegisterSyncMethod(typeof(CompatHelloMod).GetMethod(nameof(ApplySyncedToast), BindingFlags.Public | BindingFlags.Static)!);
    }

    /// <summary>Would be invoked on all peers at the committed tick in a full wiring.</summary>
    public static void ApplySyncedToast(string message)
    {
        // Third-party mods: put deterministic sim/UI feedback here.
        _ = message;
        _ = MP.IsInSession;
    }
}

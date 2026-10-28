#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.Core.Session;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Seeded Rand forwarding on critical paths. Structure + stubs: when a session is active,
/// Verse.Rand.Value / Range reads can be redirected to <see cref="Core.Sync.SyncedRand"/>.
/// Full coverage of every Rand call site is iterative; these patches establish the injection boundary.
///
/// Requires game refs (<c>RIMWORLD_DIR</c> / <c>refs/RimWorld</c>) to compile.
/// </summary>
[HarmonyPatch]
internal static class Rand_Patches
{
    [HarmonyPatch(typeof(Rand), nameof(Rand.Value), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool Value_Prefix(ref float __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        __result = session.Rand.NextFloat();
        return false;
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Range), typeof(int), typeof(int))]
    [HarmonyPrefix]
    private static bool RangeInt_Prefix(int min, int max, ref int __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        __result = session.Rand.Next(min, max);
        return false;
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Range), typeof(float), typeof(float))]
    [HarmonyPrefix]
    private static bool RangeFloat_Prefix(float min, float max, ref float __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        __result = min + session.Rand.NextFloat() * (max - min);
        return false;
    }
}
#endif

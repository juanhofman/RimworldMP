#if !RIMWORLDMP_NO_GAME_REFS
using HarmonyLib;
using RimworldMP.Core.Session;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Seeded Rand forwarding on critical paths. When a session is active, Verse.Rand draws are
/// redirected to <see cref="Core.Sync.SyncedRand"/>. Coverage expands iteratively; these
/// patches establish Value/Range/Chance/Bool injection. Remaining APIs (Gaussian, MTB, …)
/// still need RIMWORLD_DIR validation before patching.
/// </summary>
/// <remarks>
/// UNVERIFIED (RW 1.7): Chance(float) and Bool getter signatures — confirm on install before
/// adding Gaussian/MTB Prefixes (prefer incomplete + documented over silent wrong Prefix).
/// </remarks>
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
        session.Desync.IncrementCounter("Rand.Value");
        return false;
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Range), typeof(int), typeof(int))]
    [HarmonyPrefix]
    private static bool RangeInt_Prefix(int minInclusive, int maxExclusive, ref int __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        __result = session.Rand.Next(minInclusive, maxExclusive);
        session.Desync.IncrementCounter("Rand.Range");
        return false;
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Range), typeof(float), typeof(float))]
    [HarmonyPrefix]
    private static bool RangeFloat_Prefix(float minInclusive, float maxInclusive, ref float __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        __result = session.Rand.Range(minInclusive, maxInclusive);
        session.Desync.IncrementCounter("Rand.Range");
        return false;
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Chance), typeof(float))]
    [HarmonyPrefix]
    private static bool Chance_Prefix(float chance, ref bool __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        __result = session.Rand.Chance(chance);
        session.Desync.IncrementCounter("Rand.Chance");
        return false;
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Bool), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool Bool_Prefix(ref bool __result)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true;

        __result = session.Rand.Bool();
        session.Desync.IncrementCounter("Rand.Bool");
        return false;
    }
}
#endif

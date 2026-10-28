using System.Reflection;
using HarmonyLib;
using RimworldMP.Core.Session;
using RimworldMP.Net;

#if !RIMWORLDMP_NO_GAME_REFS
using Verse;
#endif

namespace RimworldMP;

/// <summary>
/// Mod entry. With game refs: subclasses Verse.Mod and applies Harmony. Without: static bootstrap for CI.
/// </summary>
#if RIMWORLDMP_NO_GAME_REFS
public sealed class RimworldMPMod
{
    public const string HarmonyId = "RimworldMP.Core";

    public static MpSession? Session { get; private set; }

    static RimworldMPMod()
    {
        var harmony = new Harmony(HarmonyId);
        // No Verse types to patch in stub mode.
        _ = harmony;
        Session = new MpSession(new LanTcpTransport(), isHost: true);
    }
}
#else
public sealed class RimworldMPMod : Mod
{
    public const string HarmonyId = "RimworldMP.Core";

    public static RimworldMPMod? Instance { get; private set; }
    public static MpSession? Session { get; private set; }

    public RimworldMPMod(ModContentPack content) : base(content)
    {
        Instance = this;
        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Session = new MpSession(new LanTcpTransport(), isHost: true);
        Log.Message("[RimworldMP] Phase 0 skeleton loaded (shared colony / LAN / in-game host). Lockstep not implemented yet.");
    }
}
#endif

using System.Reflection;
using RimworldMP.API;

namespace RimworldMP.Examples.CompatHello;

/// <summary>
/// Minimal MP-ready pattern: attribute-marked SyncMethod + FieldWatch around a fake dialog.
/// Compiles against <c>RimworldMP.API</c> only (no Harmony / Verse — NO_GAME_REFS friendly).
/// </summary>
public sealed class CompatHelloMod
{
    /// <summary>Demo sync field: UI may tweak this; watch begin/end turns deltas into sync work.</summary>
    [SyncField("toast_prefix")]
    public string ToastPrefix { get; set; } = "hello";

    /// <summary>Wire registrations. Call from your <c>Mod</c> constructor or static bootstrap.</summary>
    public static CompatHelloMod Initialize()
    {
        var mod = new CompatHelloMod();
        // Attribute scan — preferred for Workshop mods.
        MP.RegisterSyncMethodsFromType(typeof(CompatHelloMod));
        FieldWatch.RegisterFromType(typeof(CompatHelloMod));
        return mod;
    }

    /// <summary>Would be invoked on all peers at the committed tick in a full wiring.</summary>
    [SyncMethod]
    public static void ApplySyncedToast(string message)
    {
        // Third-party mods: put deterministic sim/UI feedback here.
        _ = message;
        _ = MP.IsInSession;
        _ = MP.Tick;
    }

    /// <summary>
    /// Fake "dialog OK" path: watch the field, mutate, end watch.
    /// In a live session Core would enqueue a command when WatchEnd reports a change.
    /// </summary>
    public bool SimulateDialogOk(string newPrefix)
    {
        var prop = typeof(CompatHelloMod).GetProperty(nameof(ToastPrefix))!;
        using (FieldWatch.Watch(this, prop))
            ToastPrefix = newPrefix;

        // Always apply via SyncMethod — never mutate shared sim from raw UI alone.
        ApplySyncedToast($"{ToastPrefix}: synced");
        return MP.IsSyncMethodRegistered(typeof(CompatHelloMod).GetMethod(nameof(ApplySyncedToast))!);
    }
}

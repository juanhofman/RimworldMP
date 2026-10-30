namespace RimworldMP.VerseBind;

/// <summary>How a Verse binding attaches to the game.</summary>
public enum VerseBindingKind
{
    /// <summary>Harmony Prefix on an instance/static method.</summary>
    HarmonyPrefix,

    /// <summary>Harmony Postfix.</summary>
    HarmonyPostfix,

    /// <summary>Harmony Prefix on a property setter.</summary>
    HarmonySetterPrefix,

    /// <summary>Harmony Prefix on a property getter.</summary>
    HarmonyGetterPrefix,

    /// <summary>Lockstep command applicator → Verse mutation (no capture).</summary>
    ApplyPath,

    /// <summary>JIP save capture / load.</summary>
    SaveLoad,

    /// <summary>Lobby / overlay UI entry (WindowStack or OnGUI).</summary>
    UiEntry,
}

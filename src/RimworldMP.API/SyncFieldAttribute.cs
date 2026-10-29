namespace RimworldMP.API;

/// <summary>
/// Marks a field or property whose value may change inside a UI dialog and must be
/// watched with <see cref="FieldWatch"/> so the delta can be turned into a lockstep command.
/// </summary>
/// <remarks>
/// Phase 2/3 ships registration + watch stubs only. Live begin/end plumbing is bound by
/// the main mod when a session is active; third-party mods should still call
/// <see cref="FieldWatch.WatchBegin"/> / <see cref="FieldWatch.WatchEnd"/> around dialogs.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class SyncFieldAttribute : Attribute
{
    /// <summary>Optional stable name for dumps and registry keys.</summary>
    public string? Name { get; }

    /// <summary>Creates a SyncField marker.</summary>
    /// <param name="name">Stable name override; omit to use the member name.</param>
    public SyncFieldAttribute(string? name = null) => Name = name;
}

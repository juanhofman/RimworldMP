namespace RimworldMP.API;

/// <summary>
/// Marks a method that must execute identically on every peer at the committed lockstep tick.
/// Prefer registering via <see cref="MP.RegisterSyncMethod(System.Reflection.MethodInfo)"/>
/// or <see cref="MP.RegisterSyncMethodsFromType(System.Type)"/> so Core can discover the method.
/// </summary>
/// <remarks>
/// The method body must be deterministic: no wall-clock, no local <c>System.Random</c>,
/// no unordered collection iteration that affects sim state, and no host-only I/O.
/// Host-only work belongs in <see cref="HostInject"/>; this attribute is for the apply path.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SyncMethodAttribute : Attribute
{
    /// <summary>
    /// Optional stable name used as the registry key suffix. When null, the method name is used.
    /// </summary>
    public string? Name { get; }

    /// <summary>Creates a SyncMethod marker, optionally with an explicit registry name.</summary>
    /// <param name="name">Stable name override; omit to use the CLR method name.</param>
    public SyncMethodAttribute(string? name = null) => Name = name;
}

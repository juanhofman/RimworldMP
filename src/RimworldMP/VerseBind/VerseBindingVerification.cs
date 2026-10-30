namespace RimworldMP.VerseBind;

/// <summary>
/// Confidence that a binding's target type/member signature matches RimWorld 1.7.
/// Prefer <see cref="Scaffold"/> or <see cref="Unverified"/> over inventing a silent wrong Prefix.
/// </summary>
public enum VerseBindingVerification
{
    /// <summary>Intentional incomplete placeholder — no live Harmony target yet.</summary>
    Scaffold,

    /// <summary>
    /// Best-effort from public RW / Harmony docs or older community sources;
    /// must be confirmed against installed 1.7 assemblies.
    /// </summary>
    Unverified,

    /// <summary>Shape is present in-repo; install smoke still required before claiming Done.</summary>
    Documented,
}

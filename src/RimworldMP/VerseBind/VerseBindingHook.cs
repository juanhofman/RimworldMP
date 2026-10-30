namespace RimworldMP.VerseBind;

/// <summary>One live hook the install fill-in pass must confirm or complete.</summary>
public sealed record VerseBindingHook(
    string Id,
    VerseBindingKind Kind,
    string TargetType,
    string TargetMember,
    string SourceFile,
    VerseBindingVerification Verification,
    string AcceptanceTest,
    string Notes = "");

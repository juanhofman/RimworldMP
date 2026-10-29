namespace RimworldMP.Launcher.ViewModels;

public sealed class ModListRow
{
    public int Index { get; init; }
    public required string PackageId { get; init; }
    public bool IsHarmony { get; init; }
    public bool IsRimworldMp { get; init; }
    public string Badge =>
        IsHarmony ? "Harmony · first" :
        IsRimworldMp ? "RimworldMP" :
        "";
}

public sealed class ModSyncDeltaRow
{
    public required string PackageId { get; init; }
    public string? DisplayName { get; init; }
    public required string Kind { get; init; }
    public string? Reason { get; init; }

    public static ModSyncDeltaRow From(Services.ModSync.ModSyncDelta d, string kind) => new()
    {
        PackageId = d.PackageId,
        DisplayName = d.DisplayName,
        Kind = kind,
        Reason = d.Reason,
    };
}

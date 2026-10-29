namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Composition root for catalog + sync.
/// Mod-sync agent: swap <see cref="Sync"/> / <see cref="Catalog"/> factories without changing UI bindings.
/// </summary>
public static class ModSyncServices
{
    private static IModCatalog? _catalog;
    private static IModSyncClient? _sync;

    public static IModCatalog Catalog
    {
        get => _catalog ??= new StubModCatalog();
        set => _catalog = value;
    }

    public static IModSyncClient Sync
    {
        get => _sync ??= new StubModSyncClient();
        set => _sync = value;
    }

    /// <summary>Reset to stubs (tests).</summary>
    public static void ResetToStubs(ModsConfigService? mods = null)
    {
        _catalog = new StubModCatalog(mods);
        _sync = new StubModSyncClient();
    }
}

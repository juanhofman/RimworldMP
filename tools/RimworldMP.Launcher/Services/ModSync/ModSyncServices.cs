namespace RimworldMP.Launcher.Services.ModSync;

/// <summary>
/// Composition root for catalog + sync.
/// Registers real LAN backends by default; call <see cref="ResetToStubs"/> for UI-only tests.
/// </summary>
public static class ModSyncServices
{
    private static IModCatalog? _catalog;
    private static IModSyncClient? _sync;

    public static IModCatalog Catalog
    {
        get => _catalog ??= new InstalledModCatalog();
        set => _catalog = value;
    }

    public static IModSyncClient Sync
    {
        get => _sync ??= new LanModSyncClient();
        set => _sync = value;
    }

    /// <summary>Ensure real backends are registered (idempotent). Call from <c>Program.Main</c>.</summary>
    public static void UseLanBackends(ModsConfigService? mods = null)
    {
        var m = mods ?? new ModsConfigService();
        _catalog = new InstalledModCatalog(m);
        _sync = new LanModSyncClient(m);
    }

    /// <summary>Reset to stubs (tests / UI layout without network).</summary>
    public static void ResetToStubs(ModsConfigService? mods = null)
    {
        _catalog = new StubModCatalog(mods);
        _sync = new StubModSyncClient();
    }
}

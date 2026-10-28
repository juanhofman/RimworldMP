namespace RimworldMP.CommandApply;

/// <summary>Re-entrancy flag so applicator-driven setters are not re-captured as commands.</summary>
internal static class CommandApplyReentry
{
    [ThreadStatic]
    private static int _depth;

    public static bool IsApplying => _depth > 0;

    public static void Enter() => _depth++;
    public static void Exit() => _depth--;

    public static Renter Rent() => new();

    internal readonly struct Renter : IDisposable
    {
        public Renter() => Enter();
        public void Dispose() => Exit();
    }
}

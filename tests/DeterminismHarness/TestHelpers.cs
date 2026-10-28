using RimworldMP.Core.Session;

namespace RimworldMP.Tests.DeterminismHarness;

internal static class TestHelpers
{
    public static async Task WaitForJipAsync(MpSession client, int attempts = 40, int delayMs = 25)
    {
        for (var i = 0; i < attempts && client.IsJoinInProgress; i++)
            await Task.Delay(delayMs);
    }
}

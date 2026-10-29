namespace RimworldMP.Tests.DeterminismHarness;

/// <summary>
/// Serializes tests that mutate static <c>RimworldMP.API.MP</c> / <c>FieldWatch</c> binders.
/// </summary>
[CollectionDefinition("MpApi")]
public sealed class MpApiCollection : ICollectionFixture<MpApiCollection.Marker>
{
    public sealed class Marker;
}

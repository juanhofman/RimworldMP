global using Xunit;

// SessionFacade / MpConfig / ZoneWorld / ColonyWorld are process-wide; parallel classes flake.
[assembly: CollectionBehavior(DisableTestParallelization = true)]


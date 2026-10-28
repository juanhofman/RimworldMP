namespace RimworldMP.Core.Commands;

/// <summary>Compact command opcodes for the future lockstep queue. Values are unstable until protocol v1.</summary>
public enum CommandOpcode : ushort
{
    None = 0,
    EmptyBatch = 1,
    Pause = 10,
    Speed = 11,
    // Draft / jobs / zones arrive in Phase 1.
}

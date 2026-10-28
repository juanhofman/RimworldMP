namespace RimworldMP.Core.Commands;

/// <summary>Compact command opcodes for the lockstep queue. Protocol v1 values — bump NetProtocol.Version on breaking changes.</summary>
public enum CommandOpcode : ushort
{
    None = 0,
    EmptyBatch = 1,

    Pause = 10,
    Speed = 11,

    Draft = 20,
    Undraft = 21,
    JobGive = 22,

    ZoneAdd = 30,
    ZoneRemove = 31,
    ZoneClear = 32,

    RestrictAreaSet = 40,
}

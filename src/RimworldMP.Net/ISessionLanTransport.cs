using RimworldMP.Net.Protocol;

namespace RimworldMP.Net;

/// <summary>
/// Shared host/client join surface for LAN transports (<see cref="LanTcpTransport"/>,
/// <see cref="LiteNetLibTransport"/>). Session wiring uses this instead of concrete types.
/// </summary>
public interface ISessionLanTransport : INetTransport
{
    int MaxClients { get; set; }
    string ExpectedModlistHash { get; set; }
    string LocalModlistHash { get; set; }
    long HostSessionSeed { get; set; }
    long? JoinedSessionSeed { get; }
    int? AssignedPlayerId { get; }
    JoinRejectReason? LastJoinRejectReason { get; }
    string? LastJoinRejectMessage { get; }
    NetEndpoint? Endpoint { get; }
    IReadOnlyCollection<NetPeerId> ConnectedPeers { get; }
}

namespace RimworldMP.UI;

/// <summary>Immutable lobby presentation snapshot (harness-friendly; no Verse types).</summary>
public sealed class LobbyViewModel
{
    public required string Brand { get; init; }
    public required string Subtitle { get; init; }
    public required string SessionRoleLabel { get; init; }
    public required bool InSession { get; init; }
    public required bool IsHost { get; init; }
    public required bool IsFrozen { get; init; }
    public required bool IsJoinInProgress { get; init; }
    public required bool LobbyReady { get; init; }
    public required bool ReadyRequired { get; init; }
    public required bool LocalReady { get; init; }
    public required int LocalPlayerId { get; init; }
    public required string LocalDisplayName { get; init; }
    public required long Tick { get; init; }
    public required int Port { get; init; }
    public required string LanStatus { get; init; }
    public required string EndpointLabel { get; init; }
    public required string LocalModlistHash { get; init; }
    public required string LocalModlistShort { get; init; }
    public required string ProtocolLabel { get; init; }
    public required int MaxClients { get; init; }
    public required int ConnectedPeerCount { get; init; }
    public required IReadOnlyList<LobbyPeerRow> Peers { get; init; }
    public required IReadOnlyList<LobbyChatLine> ChatLines { get; init; }
    public required HandoffUiSnapshot Handoff { get; init; }
    public required DesyncOverlayModel? Desync { get; init; }
    public required string StatusLine { get; init; }
    public required bool CanHost { get; init; }
    public required bool CanJoin { get; init; }
    public required bool CanDisconnect { get; init; }
    public required bool CanToggleReady { get; init; }
    public required bool CanKick { get; init; }
    public required bool CanChat { get; init; }
    public required bool WasKicked { get; init; }
}

public readonly record struct LobbyPeerRow(
    int PlayerId,
    string DisplayName,
    bool IsLocal,
    bool IsHostSeat,
    bool Ready,
    string ModlistHash,
    string ModlistShort,
    bool ModlistMatch,
    string ReadyLabel,
    string ModlistLabel,
    bool CanKick);

public readonly record struct LobbyChatLine(
    bool IsSystem,
    string Author,
    string Text,
    string Prefix);

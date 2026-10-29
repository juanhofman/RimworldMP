using System.Collections.Concurrent;

namespace RimworldMP.Core.Session;

/// <summary>Tracks peer readiness before unpausing / starting the shared sim.</summary>
public sealed class ReadyCheck
{
    private readonly ConcurrentDictionary<int, PeerReady> _peers = new();
    private readonly object _gate = new();
    private bool _required; // opt-in; enable for lobbies that want a ready gate

    public bool Required
    {
        get { lock (_gate) return _required; }
        set { lock (_gate) _required = value; }
    }

    public void Reset() => _peers.Clear();

    public void SetPeer(int playerId, bool ready, string modlistHash)
    {
        _peers[playerId] = new PeerReady(ready, modlistHash);
    }

    public void RemovePeer(int playerId) => _peers.TryRemove(playerId, out _);

    public bool AllReady(int expectedPeers, string hostModlistHash)
    {
        if (!_required)
            return true;
        if (_peers.Count < expectedPeers)
            return false;
        foreach (var peer in _peers.Values)
        {
            if (!peer.Ready)
                return false;
            if (!ModlistHash.Matches(hostModlistHash, peer.ModlistHash))
                return false;
        }

        return true;
    }

    /// <summary>True when the hard gate allows starting / unpausing the shared sim.</summary>
    public bool CanStartOrUnpause(int expectedPeers, string hostModlistHash) =>
        AllReady(expectedPeers, hostModlistHash);

    public IReadOnlyDictionary<int, PeerReady> Snapshot() =>
        new Dictionary<int, PeerReady>(_peers);

    public readonly record struct PeerReady(bool Ready, string ModlistHash);
}

using System.Collections.Concurrent;
using RimworldMP.Core.Commands;

namespace RimworldMP.Core.Sync;

/// <summary>
/// Ensures the sim only advances when the command batch for tick T is present.
/// Host emits batches; clients stall (return false from TryBeginTick) until the batch arrives.
/// </summary>
public sealed class TickGate
{
    private readonly ConcurrentDictionary<long, CommandBatch> _pending = new();
    private long _nextTick = 1;
    private readonly object _gate = new();

    public long NextTick
    {
        get { lock (_gate) return _nextTick; }
    }

    public int PendingCount => _pending.Count;

    public bool HasBatch(long tick) => _pending.ContainsKey(tick);

    public void EnqueueBatch(CommandBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        _pending[batch.Tick] = batch;
    }

    /// <summary>
    /// Returns true and the batch when tick <see cref="NextTick"/> is ready; advances the cursor.
    /// </summary>
    public bool TryBeginTick(out CommandBatch batch)
    {
        lock (_gate)
        {
            if (_pending.TryRemove(_nextTick, out batch!))
            {
                _nextTick++;
                return true;
            }

            batch = null!;
            return false;
        }
    }

    public void Reset(long startingTick = 1)
    {
        lock (_gate)
        {
            _pending.Clear();
            _nextTick = startingTick;
        }
    }
}

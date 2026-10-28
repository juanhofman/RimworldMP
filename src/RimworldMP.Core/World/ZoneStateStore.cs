using RimworldMP.Core.Commands;

namespace RimworldMP.Core.World;

/// <summary>
/// Deterministic zone cell store used when game refs are absent (harness) and as a shadow
/// model the applicator updates alongside Verse when refs exist.
/// </summary>
public sealed class ZoneStateStore
{
    private readonly Dictionary<int, ZoneRecord> _zones = new();
    private readonly object _gate = new();
    private int _nextId = 1;

    public int Count
    {
        get { lock (_gate) return _zones.Count; }
    }

    public int Create(byte zoneKind, int mapId, string label)
    {
        lock (_gate)
        {
            var id = _nextId++;
            _zones[id] = new ZoneRecord(id, zoneKind, mapId, label, new HashSet<(int X, int Z)>());
            return id;
        }
    }

    public void AddCells(byte zoneKind, int[] cellsPacked)
    {
        lock (_gate)
        {
            var zone = FindOrCreateDefault(zoneKind);
            for (var i = 0; i + 1 < cellsPacked.Length; i += 2)
                zone.Cells.Add((cellsPacked[i], cellsPacked[i + 1]));
        }
    }

    public void RemoveCells(byte zoneKind, int[] cellsPacked)
    {
        lock (_gate)
        {
            var zone = FindOrCreateDefault(zoneKind);
            for (var i = 0; i + 1 < cellsPacked.Length; i += 2)
                zone.Cells.Remove((cellsPacked[i], cellsPacked[i + 1]));
        }
    }

    public void Clear(byte zoneKind)
    {
        lock (_gate)
        {
            var zone = FindOrCreateDefault(zoneKind);
            zone.Cells.Clear();
        }
    }

    public int CellCount(byte zoneKind)
    {
        lock (_gate)
        {
            return _zones.Values.Where(z => z.Kind == zoneKind).Sum(z => z.Cells.Count);
        }
    }

    public void ApplyOpcode(CommandOpcode op, byte zoneKind, int[] cellsPacked)
    {
        switch (op)
        {
            case CommandOpcode.ZoneAdd:
                AddCells(zoneKind, cellsPacked);
                break;
            case CommandOpcode.ZoneRemove:
                RemoveCells(zoneKind, cellsPacked);
                break;
            case CommandOpcode.ZoneClear:
                Clear(zoneKind);
                break;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _zones.Clear();
            _nextId = 1;
        }
    }

    private ZoneRecord FindOrCreateDefault(byte zoneKind)
    {
        foreach (var z in _zones.Values)
        {
            if (z.Kind == zoneKind)
                return z;
        }

        var id = _nextId++;
        var created = new ZoneRecord(id, zoneKind, mapId: 0, label: $"zone-{zoneKind}", new HashSet<(int X, int Z)>());
        _zones[id] = created;
        return created;
    }

    private sealed class ZoneRecord
    {
        public ZoneRecord(int id, byte kind, int mapId, string label, HashSet<(int X, int Z)> cells)
        {
            Id = id;
            Kind = kind;
            MapId = mapId;
            Label = label;
            Cells = cells;
        }

        public int Id { get; }
        public byte Kind { get; }
        public int MapId { get; }
        public string Label { get; }
        public HashSet<(int X, int Z)> Cells { get; }
    }
}

/// <summary>Process-wide zone store for applicators / harness.</summary>
public static class ZoneWorld
{
    public static ZoneStateStore Current { get; } = new();
}

using RimworldMP.Core.Commands;

namespace RimworldMP.Core.World;

/// <summary>
/// Shadow colony state for bills / research / trade / caravans when game refs are absent,
/// and a deterministic mirror the applicator updates with Verse when refs exist.
/// </summary>
public sealed class ColonyStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(int Bench, int Slot), BillRecord> _bills = new();
    private string? _currentResearch;
    private readonly List<TradeRecord> _trades = new();
    private readonly Dictionary<int, CaravanRecord> _caravans = new();
    private int _nextCaravanId = 1;

    public int BillCount
    {
        get { lock (_gate) return _bills.Count; }
    }

    public string? CurrentResearch
    {
        get { lock (_gate) return _currentResearch; }
    }

    public int TradeCount
    {
        get { lock (_gate) return _trades.Count; }
    }

    public int CaravanCount
    {
        get { lock (_gate) return _caravans.Count; }
    }

    public void SetBill(int benchThingId, int billSlot, string recipeDef, byte repeatMode, int count)
    {
        lock (_gate)
            _bills[(benchThingId, billSlot)] = new BillRecord(benchThingId, billSlot, recipeDef, repeatMode, count);
    }

    public void DeleteBill(int benchThingId, int billSlot)
    {
        lock (_gate)
            _bills.Remove((benchThingId, billSlot));
    }

    public bool TryGetBill(int benchThingId, int billSlot, out BillRecord bill)
    {
        lock (_gate)
            return _bills.TryGetValue((benchThingId, billSlot), out bill!);
    }

    public void StartResearch(string projectDef)
    {
        lock (_gate)
            _currentResearch = projectDef;
    }

    public void AcceptTrade(int traderThingId, long dealFingerprint)
    {
        lock (_gate)
            _trades.Add(new TradeRecord(traderThingId, dealFingerprint));
    }

    public int FormCaravan(int tileId, int[] pawnThingIds)
    {
        lock (_gate)
        {
            var id = _nextCaravanId++;
            _caravans[id] = new CaravanRecord(id, tileId, new HashSet<int>(pawnThingIds));
            return id;
        }
    }

    public void AddCaravanPawn(int caravanId, int pawnThingId)
    {
        lock (_gate)
        {
            if (_caravans.TryGetValue(caravanId, out var c))
                c.Pawns.Add(pawnThingId);
        }
    }

    public void RemoveCaravanPawn(int caravanId, int pawnThingId)
    {
        lock (_gate)
        {
            if (_caravans.TryGetValue(caravanId, out var c))
                c.Pawns.Remove(pawnThingId);
        }
    }

    public void ApplyCommand(MpCommand cmd)
    {
        switch (cmd.Opcode)
        {
            case CommandOpcode.BillSet:
            {
                var (bench, slot, mode, count, recipe) = CommandPayloads.ReadBillSet(cmd.Payload);
                SetBill(bench, slot, recipe, mode, count);
                break;
            }
            case CommandOpcode.BillDelete:
            {
                var (bench, slot) = CommandPayloads.ReadBillDelete(cmd.Payload);
                DeleteBill(bench, slot);
                break;
            }
            case CommandOpcode.ResearchStart:
                StartResearch(CommandPayloads.ReadResearchStart(cmd.Payload));
                break;
            case CommandOpcode.TradeAccept:
            {
                var (trader, deal) = CommandPayloads.ReadTradeAccept(cmd.Payload);
                AcceptTrade(trader, deal);
                break;
            }
            case CommandOpcode.CaravanForm:
            {
                var (tile, pawns) = CommandPayloads.ReadCaravanForm(cmd.Payload);
                FormCaravan(tile, pawns);
                break;
            }
            case CommandOpcode.CaravanAddPawn:
            {
                var (cid, pawn) = CommandPayloads.ReadCaravanPawn(cmd.Payload);
                AddCaravanPawn(cid, pawn);
                break;
            }
            case CommandOpcode.CaravanRemovePawn:
            {
                var (cid, pawn) = CommandPayloads.ReadCaravanPawn(cmd.Payload);
                RemoveCaravanPawn(cid, pawn);
                break;
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _bills.Clear();
            _currentResearch = null;
            _trades.Clear();
            _caravans.Clear();
            _nextCaravanId = 1;
        }
    }

    public readonly record struct BillRecord(int BenchThingId, int BillSlot, string RecipeDef, byte RepeatMode, int Count);
    public readonly record struct TradeRecord(int TraderThingId, long DealFingerprint);

    public sealed class CaravanRecord
    {
        public CaravanRecord(int id, int tileId, HashSet<int> pawns)
        {
            Id = id;
            TileId = tileId;
            Pawns = pawns;
        }

        public int Id { get; }
        public int TileId { get; }
        public HashSet<int> Pawns { get; }
    }
}

public static class ColonyWorld
{
    public static ColonyStateStore Current { get; } = new();
}

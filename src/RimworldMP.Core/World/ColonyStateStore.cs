using RimworldMP.Core.Commands;

namespace RimworldMP.Core.World;

/// <summary>
/// Shadow colony state for bills / research / trade / caravans when game refs are absent,
/// and a deterministic mirror the applicator updates with Verse when refs exist.
/// All records carry map/thing ids from command payloads — never UI map affinity.
/// </summary>
public sealed class ColonyStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(int MapId, int Bench, int Slot), BillRecord> _bills = new();
    private string? _currentResearch;
    private int _researchMapId;
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

    public int ResearchMapId
    {
        get { lock (_gate) return _researchMapId; }
    }

    public int TradeCount
    {
        get { lock (_gate) return _trades.Count; }
    }

    public int CaravanCount
    {
        get { lock (_gate) return _caravans.Count; }
    }

    public void SetBill(int mapId, int benchThingId, int billSlot, string recipeDef, byte repeatMode, int count)
    {
        lock (_gate)
            _bills[(mapId, benchThingId, billSlot)] =
                new BillRecord(mapId, benchThingId, billSlot, recipeDef, repeatMode, count);
    }

    public void DeleteBill(int mapId, int benchThingId, int billSlot)
    {
        lock (_gate)
            _bills.Remove((mapId, benchThingId, billSlot));
    }

    public bool TryGetBill(int mapId, int benchThingId, int billSlot, out BillRecord bill)
    {
        lock (_gate)
            return _bills.TryGetValue((mapId, benchThingId, billSlot), out bill!);
    }

    public void StartResearch(int mapId, string projectDef)
    {
        lock (_gate)
        {
            _researchMapId = mapId;
            _currentResearch = projectDef;
        }
    }

    public void AcceptTrade(int mapId, int traderThingId, long dealFingerprint)
    {
        lock (_gate)
            _trades.Add(new TradeRecord(mapId, traderThingId, dealFingerprint));
    }

    public bool TryGetLastTrade(out TradeRecord trade)
    {
        lock (_gate)
        {
            if (_trades.Count == 0)
            {
                trade = default;
                return false;
            }

            trade = _trades[^1];
            return true;
        }
    }

    public int FormCaravan(int mapId, int tileId, int[] pawnThingIds)
    {
        lock (_gate)
        {
            var id = _nextCaravanId++;
            // Ordered list — HashSet iteration must never drive sim decisions.
            _caravans[id] = new CaravanRecord(id, mapId, tileId, pawnThingIds.ToList());
            return id;
        }
    }

    public bool TryGetCaravan(int caravanId, out CaravanRecord? caravan)
    {
        lock (_gate)
            return _caravans.TryGetValue(caravanId, out caravan);
    }

    public void AddCaravanPawn(int caravanId, int pawnThingId)
    {
        lock (_gate)
        {
            if (_caravans.TryGetValue(caravanId, out var c) && !c.PawnThingIds.Contains(pawnThingId))
                c.PawnThingIds.Add(pawnThingId);
        }
    }

    public void RemoveCaravanPawn(int caravanId, int pawnThingId)
    {
        lock (_gate)
        {
            if (_caravans.TryGetValue(caravanId, out var c))
                c.PawnThingIds.Remove(pawnThingId);
        }
    }

    public void ApplyCommand(MpCommand cmd)
    {
        switch (cmd.Opcode)
        {
            case CommandOpcode.BillSet:
            {
                var bill = CommandPayloads.ReadBillSet(cmd.Payload);
                SetBill(bill.MapId, bill.BenchThingId, bill.BillSlot, bill.RecipeDef, bill.RepeatMode, bill.Count);
                break;
            }
            case CommandOpcode.BillDelete:
            {
                var (mapId, bench, slot) = CommandPayloads.ReadBillDelete(cmd.Payload);
                DeleteBill(mapId, bench, slot);
                break;
            }
            case CommandOpcode.ResearchStart:
            {
                var (mapId, project) = CommandPayloads.ReadResearchStart(cmd.Payload);
                StartResearch(mapId, project);
                break;
            }
            case CommandOpcode.TradeAccept:
            {
                var (mapId, trader, deal) = CommandPayloads.ReadTradeAccept(cmd.Payload);
                AcceptTrade(mapId, trader, deal);
                break;
            }
            case CommandOpcode.CaravanForm:
            {
                var (mapId, tile, pawns) = CommandPayloads.ReadCaravanForm(cmd.Payload);
                FormCaravan(mapId, tile, pawns);
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
            case CommandOpcode.BillReorder:
            {
                var (mapId, bench, from, to) = CommandPayloads.ReadBillReorder(cmd.Payload);
                ReorderBill(mapId, bench, from, to);
                break;
            }
            case CommandOpcode.ResearchStop:
                lock (_gate)
                {
                    _ = CommandPayloads.ReadResearchStop(cmd.Payload);
                    _currentResearch = null;
                }

                break;
            case CommandOpcode.TradeCancel:
            {
                var (mapId, trader) = CommandPayloads.ReadTradeCancel(cmd.Payload);
                lock (_gate)
                    _trades.RemoveAll(t => t.MapId == mapId && t.TraderThingId == trader);
                break;
            }
            case CommandOpcode.CaravanDisband:
            {
                var id = CommandPayloads.ReadCaravanDisband(cmd.Payload);
                lock (_gate)
                    _caravans.Remove(id);
                break;
            }
        }
    }

    public void ReorderBill(int mapId, int benchThingId, int fromSlot, int toSlot)
    {
        lock (_gate)
        {
            if (!_bills.TryGetValue((mapId, benchThingId, fromSlot), out var bill))
                return;
            _bills.Remove((mapId, benchThingId, fromSlot));
            _bills[(mapId, benchThingId, toSlot)] = bill with { BillSlot = toSlot };
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _bills.Clear();
            _currentResearch = null;
            _researchMapId = 0;
            _trades.Clear();
            _caravans.Clear();
            _nextCaravanId = 1;
        }
    }

    public readonly record struct BillRecord(
        int MapId,
        int BenchThingId,
        int BillSlot,
        string RecipeDef,
        byte RepeatMode,
        int Count);

    public readonly record struct TradeRecord(int MapId, int TraderThingId, long DealFingerprint);

    public sealed class CaravanRecord
    {
        public CaravanRecord(int id, int mapId, int tileId, List<int> pawnThingIds)
        {
            Id = id;
            MapId = mapId;
            TileId = tileId;
            PawnThingIds = pawnThingIds;
        }

        public int Id { get; }
        public int MapId { get; }
        public int TileId { get; }
        /// <summary>Ordered pawn thing ids (insertion order). Do not swap for HashSet.</summary>
        public List<int> PawnThingIds { get; }
    }
}

public static class ColonyWorld
{
    public static ColonyStateStore Current { get; } = new();
}

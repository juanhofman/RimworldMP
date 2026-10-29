using RimworldMP.Core.Commands;

namespace RimworldMP.Core.World;

/// <summary>
/// Shadow store for schedules / work / food / outfit / drug / medical / hostility policies.
/// Offline-capable; Verse apply is stubbed until <c>RIMWORLD_DIR</c>.
/// </summary>
public sealed class PolicyStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(int Pawn, string Work), byte> _workPriorities = new();
    private readonly Dictionary<int, byte[]> _timetables = new();
    private readonly Dictionary<int, string> _food = new();
    private readonly Dictionary<int, string> _outfit = new();
    private readonly Dictionary<int, string> _drug = new();
    private readonly Dictionary<int, byte> _medical = new();
    private readonly Dictionary<int, byte> _hostility = new();
    private readonly Dictionary<(int Map, int Thing), bool> _forbid = new();
    private readonly List<string> _designatorLog = new();

    public int WorkPriorityCount
    {
        get { lock (_gate) return _workPriorities.Count; }
    }

    public int TimetableCount
    {
        get { lock (_gate) return _timetables.Count; }
    }

    public int DesignatorEventCount
    {
        get { lock (_gate) return _designatorLog.Count; }
    }

    public void ApplyCommand(MpCommand cmd)
    {
        switch (cmd.Opcode)
        {
            case CommandOpcode.WorkPrioritySet:
            {
                var (pawn, work, pri) = CommandPayloads.ReadWorkPrioritySet(cmd.Payload);
                lock (_gate)
                    _workPriorities[(pawn, work)] = pri;
                break;
            }
            case CommandOpcode.TimetableSet:
            {
                var (pawn, hours) = CommandPayloads.ReadTimetableSet(cmd.Payload);
                lock (_gate)
                    _timetables[pawn] = hours;
                break;
            }
            case CommandOpcode.FoodRestrictionAssign:
            {
                var (pawn, id) = CommandPayloads.ReadPolicyAssign(cmd.Payload);
                lock (_gate) _food[pawn] = id;
                break;
            }
            case CommandOpcode.OutfitAssign:
            {
                var (pawn, id) = CommandPayloads.ReadPolicyAssign(cmd.Payload);
                lock (_gate) _outfit[pawn] = id;
                break;
            }
            case CommandOpcode.DrugPolicyAssign:
            {
                var (pawn, id) = CommandPayloads.ReadPolicyAssign(cmd.Payload);
                lock (_gate) _drug[pawn] = id;
                break;
            }
            case CommandOpcode.MedicalCareSet:
            {
                var (pawn, care) = CommandPayloads.ReadMedicalCareSet(cmd.Payload);
                lock (_gate) _medical[pawn] = care;
                break;
            }
            case CommandOpcode.HostilityResponseSet:
            {
                var (pawn, mode) = CommandPayloads.ReadHostilityResponseSet(cmd.Payload);
                lock (_gate) _hostility[pawn] = mode;
                break;
            }
            case CommandOpcode.ForbidSet:
            {
                var (map, thing, forbidden) = CommandPayloads.ReadForbidSet(cmd.Payload);
                lock (_gate) _forbid[(map, thing)] = forbidden;
                break;
            }
            case CommandOpcode.DesignatorCell:
            {
                var (kind, map, x, z, thing) = CommandPayloads.ReadDesignatorCell(cmd.Payload);
                lock (_gate)
                    _designatorLog.Add($"kind={kind} map={map} cell={x},{z} thing={thing}");
                break;
            }
        }
    }

    public bool TryGetWorkPriority(int pawnThingId, string workTypeDef, out byte priority)
    {
        lock (_gate)
            return _workPriorities.TryGetValue((pawnThingId, workTypeDef), out priority);
    }

    public bool TryGetTimetable(int pawnThingId, out byte[] hours)
    {
        lock (_gate)
            return _timetables.TryGetValue(pawnThingId, out hours!);
    }

    public bool TryGetFood(int pawnThingId, out string policyId)
    {
        lock (_gate)
            return _food.TryGetValue(pawnThingId, out policyId!);
    }

    public bool TryGetOutfit(int pawnThingId, out string policyId)
    {
        lock (_gate)
            return _outfit.TryGetValue(pawnThingId, out policyId!);
    }

    public bool IsForbidden(int mapId, int thingId)
    {
        lock (_gate)
            return _forbid.TryGetValue((mapId, thingId), out var f) && f;
    }

    public void Reset()
    {
        lock (_gate)
        {
            _workPriorities.Clear();
            _timetables.Clear();
            _food.Clear();
            _outfit.Clear();
            _drug.Clear();
            _medical.Clear();
            _hostility.Clear();
            _forbid.Clear();
            _designatorLog.Clear();
        }
    }
}

public static class PolicyWorld
{
    public static PolicyStateStore Current { get; } = new();
}

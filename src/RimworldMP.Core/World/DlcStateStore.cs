using RimworldMP.Core.Commands;

namespace RimworldMP.Core.World;

/// <summary>Shadow DLC state for Royalty / Ideology / Biotech / Anomaly command surfaces.</summary>
public sealed class DlcStateStore
{
    private readonly object _gate = new();
    private readonly List<string> _events = new();

    public int EventCount
    {
        get { lock (_gate) return _events.Count; }
    }

    public IReadOnlyList<string> SnapshotEvents()
    {
        lock (_gate) return _events.ToList();
    }

    public void ApplyCommand(MpCommand cmd)
    {
        lock (_gate)
        {
            switch (cmd.Opcode)
            {
                case CommandOpcode.RoyaltyTitleChange:
                {
                    var (pawn, def) = CommandPayloads.ReadPawnDef(cmd.Payload);
                    _events.Add($"royalty.title pawn={pawn} def={def}");
                    break;
                }
                case CommandOpcode.RoyaltyPermitUse:
                {
                    var (pawn, def) = CommandPayloads.ReadPawnDef(cmd.Payload);
                    _events.Add($"royalty.permit pawn={pawn} def={def}");
                    break;
                }
                case CommandOpcode.IdeologyBeliefChange:
                {
                    var (ideo, precept, sev) = CommandPayloads.ReadIdeologyBelief(cmd.Payload);
                    _events.Add($"ideo.belief ideo={ideo} precept={precept} sev={sev}");
                    break;
                }
                case CommandOpcode.IdeologyRitualStart:
                {
                    var (ritual, map) = CommandPayloads.ReadRitualStart(cmd.Payload);
                    _events.Add($"ideo.ritual ritual={ritual} map={map}");
                    break;
                }
                case CommandOpcode.BiotechGrowthVatAssign:
                {
                    var (vat, pawn) = CommandPayloads.ReadGrowthVatAssign(cmd.Payload);
                    _events.Add($"biotech.vat vat={vat} pawn={pawn}");
                    break;
                }
                case CommandOpcode.BiotechGeneInstall:
                {
                    var (pawn, gene) = CommandPayloads.ReadPawnDef(cmd.Payload);
                    _events.Add($"biotech.gene pawn={pawn} gene={gene}");
                    break;
                }
                case CommandOpcode.AnomalyEntityContain:
                {
                    var (entity, chamber) = CommandPayloads.ReadEntityContain(cmd.Payload);
                    _events.Add($"anomaly.contain entity={entity} chamber={chamber}");
                    break;
                }
                case CommandOpcode.AnomalyStudy:
                {
                    var (pawn, def) = CommandPayloads.ReadPawnDef(cmd.Payload);
                    _events.Add($"anomaly.study pawn={pawn} def={def}");
                    break;
                }
            }
        }
    }

    public void Reset()
    {
        lock (_gate) _events.Clear();
    }
}

public static class DlcWorld
{
    public static DlcStateStore Current { get; } = new();
}

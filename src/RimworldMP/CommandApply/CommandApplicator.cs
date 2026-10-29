using RimworldMP.API;
using RimworldMP.Core.Commands;
using RimworldMP.Core.World;
// DlcWorld / ColonyWorld / ZoneWorld

#if !RIMWORLDMP_NO_GAME_REFS
using RimWorld;
using Verse;
#endif

namespace RimworldMP.CommandApply;

/// <summary>
/// Applies admitted lockstep commands to the local sim / shadow stores.
/// Deterministic apply paths must not read <c>Find.CurrentMap</c> — map id comes from payloads.
/// </summary>
public static class CommandApplicator
{
    public static void Apply(CommandBatch batch)
    {
        foreach (var cmd in batch.Commands)
            ApplyOne(cmd);
    }

    public static void ApplyOne(MpCommand cmd)
    {
        using var _ = CommandApplyReentry.Rent();
        switch (cmd.Opcode)
        {
            case CommandOpcode.Pause:
                ApplyPause(CommandPayloads.ReadPause(cmd.Payload));
                break;
            case CommandOpcode.Speed:
                ApplySpeed(CommandPayloads.ReadSpeed(cmd.Payload));
                break;
            case CommandOpcode.Draft:
            case CommandOpcode.Undraft:
            {
                var (pawnId, drafted) = CommandPayloads.ReadDraft(cmd.Payload);
                ApplyDraft(pawnId, drafted);
                break;
            }
            case CommandOpcode.JobGive:
            {
                var job = CommandPayloads.ReadJobGive(cmd.Payload);
                ApplyJobGive(job);
                break;
            }
            case CommandOpcode.ZoneAdd:
            case CommandOpcode.ZoneRemove:
            case CommandOpcode.ZoneClear:
            {
                var (kind, mapId, cells) = CommandPayloads.ReadZoneCells(cmd.Payload);
                ApplyZone(cmd.Opcode, kind, mapId, cells);
                break;
            }
            case CommandOpcode.ZoneCreate:
            {
                var (kind, mapId, label) = CommandPayloads.ReadZoneCreate(cmd.Payload);
                ZoneWorld.Current.Create(kind, mapId, label);
                break;
            }
            case CommandOpcode.RestrictAreaSet:
            {
                var (pawnId, areaId) = CommandPayloads.ReadRestrictArea(cmd.Payload);
                ApplyRestrictArea(pawnId, areaId);
                break;
            }
            case CommandOpcode.DesignatorCell:
            case CommandOpcode.ForbidSet:
            case CommandOpcode.WorkPrioritySet:
            case CommandOpcode.TimetableSet:
            case CommandOpcode.FoodRestrictionAssign:
            case CommandOpcode.OutfitAssign:
            case CommandOpcode.DrugPolicyAssign:
            case CommandOpcode.MedicalCareSet:
            case CommandOpcode.HostilityResponseSet:
                PolicyWorld.Current.ApplyCommand(cmd);
                // Verse policy apply requires RIMWORLD_DIR — shadow store is authoritative offline.
                break;
            case CommandOpcode.BillSet:
            case CommandOpcode.BillDelete:
            case CommandOpcode.BillReorder:
            case CommandOpcode.ResearchStart:
            case CommandOpcode.ResearchStop:
            case CommandOpcode.TradeAccept:
            case CommandOpcode.TradeCancel:
            case CommandOpcode.CaravanForm:
            case CommandOpcode.CaravanAddPawn:
            case CommandOpcode.CaravanRemovePawn:
            case CommandOpcode.CaravanDisband:
                ColonyWorld.Current.ApplyCommand(cmd);
                ApplyColonyGame(cmd);
                break;
            case CommandOpcode.SyncMethodInvoke:
                MP.ApplySyncMethodPayload(cmd.Payload);
                break;
            case CommandOpcode.RoyaltyTitleChange:
            case CommandOpcode.RoyaltyPermitUse:
            case CommandOpcode.IdeologyBeliefChange:
            case CommandOpcode.IdeologyRitualStart:
            case CommandOpcode.BiotechGrowthVatAssign:
            case CommandOpcode.BiotechGeneInstall:
            case CommandOpcode.AnomalyEntityContain:
            case CommandOpcode.AnomalyStudy:
                DlcWorld.Current.ApplyCommand(cmd);
                break;
        }
    }

    private static void ApplyPause(bool paused)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var tm = Find.TickManager;
        if (tm is null) return;
        if (paused)
            tm.CurTimeSpeed = TimeSpeed.Paused;
        else if (tm.CurTimeSpeed == TimeSpeed.Paused)
            tm.CurTimeSpeed = TimeSpeed.Normal;
#else
        _ = paused;
#endif
    }

    private static void ApplySpeed(byte speedIndex)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var tm = Find.TickManager;
        if (tm is null) return;
        tm.CurTimeSpeed = speedIndex switch
        {
            0 => TimeSpeed.Paused,
            1 => TimeSpeed.Normal,
            2 => TimeSpeed.Fast,
            3 => TimeSpeed.Superfast,
            4 => TimeSpeed.Ultrafast,
            _ => TimeSpeed.Normal,
        };
#else
        _ = speedIndex;
#endif
    }

    private static void ApplyDraft(int pawnLoadId, bool drafted)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var pawn = FindPawn(pawnLoadId);
        if (pawn?.drafter is null) return;
        pawn.drafter.Drafted = drafted;
#else
        _ = (pawnLoadId, drafted);
#endif
    }

    private static void ApplyJobGive(CommandPayloads.JobGivePayload job)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var pawn = FindPawn(job.PawnLoadId);
        if (pawn is null) return;
        var map = FindMap(job.MapId) ?? pawn.Map;
        if (map is null) return;
        var def = DefDatabase<JobDef>.GetNamedSilentFail(job.JobDefName);
        if (def is null) return;

        Job made;
        if (job.TargetThingId >= 0)
        {
            var thing = FindThingOnMap(map, job.TargetThingId) ?? FindThing(job.TargetThingId);
            made = thing is not null
                ? JobMaker.MakeJob(def, thing)
                : JobMaker.MakeJob(def, new IntVec3(job.CellX, 0, job.CellZ));
        }
        else
        {
            made = JobMaker.MakeJob(def, new IntVec3(job.CellX, 0, job.CellZ));
        }

        if (job.Count >= 0)
            made.count = job.Count;

        // Deterministic stub apply: ordered job path; queue flag reserved for request-queue when RIMWORLD_DIR validates APIs.
        pawn.jobs?.TryTakeOrderedJob(made, JobTag.Misc);
#else
        // Shadow-path: payload is fully decoded for harness determinism; no Verse commit.
        _ = job;
#endif
    }

    private static void ApplyZone(CommandOpcode op, byte zoneKind, int mapId, int[] cellsPacked)
    {
        ZoneWorld.Current.ApplyOpcode(op, zoneKind, mapId, cellsPacked);
#if !RIMWORLDMP_NO_GAME_REFS
        var map = FindMap(mapId);
        if (map?.zoneManager is null) return;
        Zone? zone = null;
        foreach (var z in map.zoneManager.AllZones)
        {
            if (ZoneKind(z) == zoneKind)
            {
                zone = z;
                break;
            }
        }

        if (zone is null && op == CommandOpcode.ZoneAdd)
        {
            zone = zoneKind switch
            {
                1 => new Zone_Stockpile(map.zoneManager),
                2 => new Zone_Growing(map.zoneManager),
                _ => null,
            };
            if (zone is not null)
                map.zoneManager.RegisterZone(zone);
        }

        if (zone is null) return;

        if (op == CommandOpcode.ZoneClear)
        {
            zone.Delete();
            return;
        }

        for (var i = 0; i + 1 < cellsPacked.Length; i += 2)
        {
            var cell = new IntVec3(cellsPacked[i], 0, cellsPacked[i + 1]);
            if (op == CommandOpcode.ZoneAdd)
                zone.AddCell(cell);
            else if (op == CommandOpcode.ZoneRemove)
                zone.RemoveCell(cell);
        }
#endif
    }

    private static void ApplyRestrictArea(int pawnLoadId, int areaId)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        var pawn = FindPawn(pawnLoadId);
        if (pawn?.playerSettings is null || pawn.Map is null) return;
        Area? area = null;
        if (areaId >= 0)
        {
            foreach (var a in pawn.Map.areaManager.AllAreas)
            {
                if (a.ID == areaId)
                {
                    area = a;
                    break;
                }
            }
        }

        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
#else
        _ = (pawnLoadId, areaId);
#endif
    }

    private static void ApplyColonyGame(MpCommand cmd)
    {
#if !RIMWORLDMP_NO_GAME_REFS
        switch (cmd.Opcode)
        {
            case CommandOpcode.ResearchStart:
            {
                var (_, name) = CommandPayloads.ReadResearchStart(cmd.Payload);
                var project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
                if (project is not null)
                    Find.ResearchManager.SetCurrentProject(project);
                break;
            }
            case CommandOpcode.BillSet:
            {
                var bill = CommandPayloads.ReadBillSet(cmd.Payload);
                var map = FindMap(bill.MapId);
                var bench = (map is not null
                    ? FindThingOnMap(map, bill.BenchThingId)
                    : FindThing(bill.BenchThingId)) as Building_WorkTable;
                var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(bill.RecipeDef);
                if (bench?.BillStack is null || recipe is null) return;
                // Structural apply: replace/add bill at slot when possible.
                while (bench.BillStack.Count <= bill.BillSlot)
                    bench.BillStack.AddBill(BillUtility.MakeNewBill(recipe));
                if (bill.BillSlot >= 0 && bill.BillSlot < bench.BillStack.Count)
                {
                    var existing = bench.BillStack[bill.BillSlot];
                    if (existing is Bill_Production prod)
                    {
                        prod.repeatMode = bill.RepeatMode switch
                        {
                            1 => BillRepeatModeDefOf.TargetCount,
                            2 => BillRepeatModeDefOf.Forever,
                            _ => BillRepeatModeDefOf.RepeatCount,
                        };
                        prod.repeatCount = bill.Count;
                    }
                }

                break;
            }
            case CommandOpcode.BillDelete:
            {
                var (mapId, benchId, slot) = CommandPayloads.ReadBillDelete(cmd.Payload);
                var map = FindMap(mapId);
                var bench = (map is not null ? FindThingOnMap(map, benchId) : FindThing(benchId)) as Building_WorkTable;
                if (bench?.BillStack is null) return;
                if (slot >= 0 && slot < bench.BillStack.Count)
                    bench.BillStack.Delete(bench.BillStack[slot]);
                break;
            }
            case CommandOpcode.TradeAccept:
            {
                // Deal commit needs live TradeDeal / Verse trader APIs — stub until RIMWORLD_DIR validates.
                var (mapId, traderId, fingerprint) = CommandPayloads.ReadTradeAccept(cmd.Payload);
                _ = FindMap(mapId);
                _ = FindThing(traderId);
                Log.Message(
                    $"[RimworldMP] TradeAccept map={mapId} trader={traderId} fingerprint={fingerprint} (deal commit stub)");
                break;
            }
            case CommandOpcode.CaravanForm:
            {
                // Caravan formation needs Dialog_FormCaravan / CaravanMaker — stub until RIMWORLD_DIR.
                var (mapId, tile, pawns) = CommandPayloads.ReadCaravanForm(cmd.Payload);
                _ = FindMap(mapId);
                Log.Message($"[RimworldMP] CaravanForm map={mapId} tile={tile} pawns={pawns.Length}");
                break;
            }
        }
#else
        // Shadow path already applied via ColonyWorld; payload decode is authoritative offline.
        _ = cmd;
#endif
    }

#if !RIMWORLDMP_NO_GAME_REFS
    private static byte ZoneKind(Zone zone) => zone switch
    {
        Zone_Stockpile => 1,
        Zone_Growing => 2,
        _ => 0,
    };

    private static Map? FindMap(int mapId)
    {
        foreach (var map in Find.Maps)
        {
            if (map.uniqueID == mapId)
                return map;
        }

        return null;
    }

    private static Pawn? FindPawn(int loadId)
    {
        foreach (var map in Find.Maps)
        {
            foreach (var pawn in map.mapPawns.AllPawns)
            {
                if (pawn.thingIDNumber == loadId)
                    return pawn;
            }
        }

        return null;
    }

    private static Thing? FindThing(int id)
    {
        foreach (var map in Find.Maps)
        {
            var t = FindThingOnMap(map, id);
            if (t is not null)
                return t;
        }

        return null;
    }

    private static Thing? FindThingOnMap(Map map, int id)
    {
        foreach (var t in map.listerThings.AllThings)
        {
            if (t.thingIDNumber == id)
                return t;
        }

        return null;
    }
#endif
}

using System.Buffers.Binary;
using System.Text;

namespace RimworldMP.Core.Commands;

/// <summary>Compact payload helpers for Phase 1–2 player commands.</summary>
public static class CommandPayloads
{
    public static byte[] Pause(bool paused) => new[] { paused ? (byte)1 : (byte)0 };

    public static bool ReadPause(ReadOnlySpan<byte> payload) => payload.Length > 0 && payload[0] != 0;

    public static byte[] Speed(byte speedIndex) => new[] { speedIndex };

    public static byte ReadSpeed(ReadOnlySpan<byte> payload) => payload.Length > 0 ? payload[0] : (byte)0;

    public static byte[] Draft(int pawnLoadId, bool drafted)
    {
        var buf = new byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnLoadId);
        buf[4] = drafted ? (byte)1 : (byte)0;
        return buf;
    }

    public static (int PawnLoadId, bool Drafted) ReadDraft(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 5)
            throw new InvalidOperationException("Draft payload truncated.");
        return (BinaryPrimitives.ReadInt32LittleEndian(payload), payload[4] != 0);
    }

    /// <summary>
    /// Job give: map + pawn + cell + optional Thing target + count + queue flag + def name.
    /// Map id is mandatory so apply never reads <c>Find.CurrentMap</c>.
    /// </summary>
    public static byte[] JobGive(
        int mapId,
        int pawnLoadId,
        int cellX,
        int cellZ,
        string jobDefName,
        int targetThingId = -1,
        int count = -1,
        byte queue = 0)
    {
        var nameBytes = Encoding.UTF8.GetBytes(jobDefName ?? string.Empty);
        // map(4) pawn(4) x(4) z(4) targetThing(4) count(4) queue(1) nameLen(2) name
        var buf = new byte[4 + 4 + 4 + 4 + 4 + 4 + 1 + 2 + nameBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), pawnLoadId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8), cellX);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(12), cellZ);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(16), targetThingId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(20), count);
        buf[24] = queue;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(25), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(27));
        return buf;
    }

    public static JobGivePayload ReadJobGive(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 27)
            throw new InvalidOperationException("JobGive payload truncated.");
        var mapId = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var pawn = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        var x = BinaryPrimitives.ReadInt32LittleEndian(payload[8..]);
        var z = BinaryPrimitives.ReadInt32LittleEndian(payload[12..]);
        var targetThingId = BinaryPrimitives.ReadInt32LittleEndian(payload[16..]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(payload[20..]);
        var queue = payload[24];
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[25..]);
        if (payload.Length < 27 + len)
            throw new InvalidOperationException("JobGive name truncated.");
        var name = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(27, len));
        return new JobGivePayload(mapId, pawn, x, z, targetThingId, count, queue, name);
    }

    public readonly record struct JobGivePayload(
        int MapId,
        int PawnLoadId,
        int CellX,
        int CellZ,
        int TargetThingId,
        int Count,
        byte Queue,
        string JobDefName);

    /// <summary>Zone cells: kind + map id + packed x/z pairs. Map id avoids UI-map affinity.</summary>
    public static byte[] ZoneCells(byte zoneKind, int mapId, int[] cellsPacked)
    {
        var buf = new byte[1 + 4 + 4 + cellsPacked.Length * 4];
        buf[0] = zoneKind;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(1), mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(5), cellsPacked.Length);
        for (var i = 0; i < cellsPacked.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(9 + i * 4), cellsPacked[i]);
        return buf;
    }

    public static (byte ZoneKind, int MapId, int[] CellsPacked) ReadZoneCells(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 9)
            throw new InvalidOperationException("Zone payload truncated.");
        var kind = payload[0];
        var mapId = BinaryPrimitives.ReadInt32LittleEndian(payload[1..]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(payload[5..]);
        if (count < 0 || payload.Length < 9 + count * 4)
            throw new InvalidOperationException("Zone cells truncated.");
        var cells = new int[count];
        for (var i = 0; i < count; i++)
            cells[i] = BinaryPrimitives.ReadInt32LittleEndian(payload[(9 + i * 4)..]);
        return (kind, mapId, cells);
    }

    /// <summary>Zone create: kind + map id + optional label.</summary>
    public static byte[] ZoneCreate(byte zoneKind, int mapId, string label)
    {
        var nameBytes = Encoding.UTF8.GetBytes(label ?? string.Empty);
        var buf = new byte[1 + 4 + 2 + nameBytes.Length];
        buf[0] = zoneKind;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(1), mapId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(5), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(7));
        return buf;
    }

    public static (byte ZoneKind, int MapId, string Label) ReadZoneCreate(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 7)
            throw new InvalidOperationException("ZoneCreate payload truncated.");
        var kind = payload[0];
        var mapId = BinaryPrimitives.ReadInt32LittleEndian(payload[1..]);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[5..]);
        if (payload.Length < 7 + len)
            throw new InvalidOperationException("ZoneCreate label truncated.");
        var label = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(7, len));
        return (kind, mapId, label);
    }

    public static byte[] RestrictArea(int pawnLoadId, int areaId)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnLoadId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), areaId);
        return buf;
    }

    public static (int PawnLoadId, int AreaId) ReadRestrictArea(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidOperationException("RestrictArea payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

    /// <summary>
    /// Bill set: map id + bench thing id + bill slot + recipe def + repeat mode + count.
    /// Map id is mandatory so apply never reads <c>Find.CurrentMap</c>.
    /// </summary>
    public static byte[] BillSet(
        int mapId,
        int benchThingId,
        int billSlot,
        string recipeDef,
        byte repeatMode,
        int count)
    {
        var nameBytes = Encoding.UTF8.GetBytes(recipeDef ?? string.Empty);
        // map(4) bench(4) slot(4) mode(1) count(4) nameLen(2) name
        var buf = new byte[4 + 4 + 4 + 1 + 4 + 2 + nameBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), benchThingId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8), billSlot);
        buf[12] = repeatMode;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(13), count);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(17), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(19));
        return buf;
    }

    public static BillSetPayload ReadBillSet(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 19)
            throw new InvalidOperationException("BillSet payload truncated.");
        var mapId = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var bench = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        var slot = BinaryPrimitives.ReadInt32LittleEndian(payload[8..]);
        var mode = payload[12];
        var count = BinaryPrimitives.ReadInt32LittleEndian(payload[13..]);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[17..]);
        if (payload.Length < 19 + len)
            throw new InvalidOperationException("BillSet recipe truncated.");
        var recipe = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(19, len));
        return new BillSetPayload(mapId, bench, slot, mode, count, recipe);
    }

    public readonly record struct BillSetPayload(
        int MapId,
        int BenchThingId,
        int BillSlot,
        byte RepeatMode,
        int Count,
        string RecipeDef);

    public static byte[] BillDelete(int mapId, int benchThingId, int billSlot)
    {
        var buf = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), benchThingId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8), billSlot);
        return buf;
    }

    public static (int MapId, int BenchThingId, int BillSlot) ReadBillDelete(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 12)
            throw new InvalidOperationException("BillDelete payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
    }

    /// <summary>Research start: map id (colony context) + project def name.</summary>
    public static byte[] ResearchStart(int mapId, string projectDef)
    {
        var nameBytes = Encoding.UTF8.GetBytes(projectDef ?? string.Empty);
        var buf = new byte[4 + 2 + nameBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(6));
        return buf;
    }

    public static (int MapId, string ProjectDef) ReadResearchStart(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6)
            throw new InvalidOperationException("ResearchStart payload truncated.");
        var mapId = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]);
        if (payload.Length < 6 + len)
            throw new InvalidOperationException("ResearchStart name truncated.");
        var name = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(6, len));
        return (mapId, name);
    }

    /// <summary>
    /// Trade accept: map id + trader thing id + deal fingerprint (hash of offered silver/goods).
    /// </summary>
    public static byte[] TradeAccept(int mapId, int traderThingId, long dealFingerprint)
    {
        var buf = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), traderThingId);
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(8), dealFingerprint);
        return buf;
    }

    public static (int MapId, int TraderThingId, long DealFingerprint) ReadTradeAccept(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 16)
            throw new InvalidOperationException("TradeAccept payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]),
            BinaryPrimitives.ReadInt64LittleEndian(payload[8..]));
    }

    /// <summary>Caravan form: departure map id + starting tile + packed pawn thing ids.</summary>
    public static byte[] CaravanForm(int mapId, int tileId, int[] pawnThingIds)
    {
        var buf = new byte[4 + 4 + 4 + pawnThingIds.Length * 4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), tileId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8), pawnThingIds.Length);
        for (var i = 0; i < pawnThingIds.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(12 + i * 4), pawnThingIds[i]);
        return buf;
    }

    public static (int MapId, int TileId, int[] PawnThingIds) ReadCaravanForm(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 12)
            throw new InvalidOperationException("CaravanForm payload truncated.");
        var mapId = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var tile = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(payload[8..]);
        if (count < 0 || payload.Length < 12 + count * 4)
            throw new InvalidOperationException("CaravanForm pawns truncated.");
        var pawns = new int[count];
        for (var i = 0; i < count; i++)
            pawns[i] = BinaryPrimitives.ReadInt32LittleEndian(payload[(12 + i * 4)..]);
        return (mapId, tile, pawns);
    }

    public static byte[] CaravanPawn(int caravanId, int pawnThingId)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(buf, caravanId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), pawnThingId);
        return buf;
    }

    public static (int CaravanId, int PawnThingId) ReadCaravanPawn(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidOperationException("CaravanPawn payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

    public static string ReadUtf8Prefixed(ReadOnlySpan<byte> payload, out int consumed)
    {
        if (payload.Length < 2)
            throw new InvalidOperationException("UTF8 prefix truncated.");
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + len)
            throw new InvalidOperationException("UTF8 body truncated.");
        consumed = 2 + len;
        return len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(2, len));
    }

    public static byte[] Utf8Prefixed(string value)
    {
        var nameBytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var buf = new byte[2 + nameBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(2));
        return buf;
    }

    /// <summary>DLC generic: pawn + def name (title, permit, gene, entity study, etc.).</summary>
    public static byte[] PawnDef(int pawnThingId, string defName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(defName ?? string.Empty);
        var buf = new byte[4 + 2 + nameBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnThingId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(6));
        return buf;
    }

    public static (int PawnThingId, string DefName) ReadPawnDef(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6)
            throw new InvalidOperationException("PawnDef payload truncated.");
        var pawn = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]);
        if (payload.Length < 6 + len)
            throw new InvalidOperationException("PawnDef name truncated.");
        var name = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(6, len));
        return (pawn, name);
    }

    public static byte[] IdeologyBelief(string ideoId, string preceptDef, byte severity)
    {
        var a = Encoding.UTF8.GetBytes(ideoId ?? string.Empty);
        var b = Encoding.UTF8.GetBytes(preceptDef ?? string.Empty);
        var buf = new byte[2 + a.Length + 2 + b.Length + 1];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, (ushort)a.Length);
        a.CopyTo(buf.AsSpan(2));
        var o = 2 + a.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(o), (ushort)b.Length);
        b.CopyTo(buf.AsSpan(o + 2));
        buf[^1] = severity;
        return buf;
    }

    public static (string IdeoId, string PreceptDef, byte Severity) ReadIdeologyBelief(ReadOnlySpan<byte> payload)
    {
        var ideo = ReadUtf8Prefixed(payload, out var c1);
        var precept = ReadUtf8Prefixed(payload[c1..], out var c2);
        if (payload.Length < c1 + c2 + 1)
            throw new InvalidOperationException("IdeologyBelief truncated.");
        return (ideo, precept, payload[c1 + c2]);
    }

    public static byte[] RitualStart(string ritualDef, int mapId)
    {
        var name = Utf8Prefixed(ritualDef);
        var buf = new byte[name.Length + 4];
        name.CopyTo(buf, 0);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(name.Length), mapId);
        return buf;
    }

    public static (string RitualDef, int MapId) ReadRitualStart(ReadOnlySpan<byte> payload)
    {
        var name = ReadUtf8Prefixed(payload, out var c);
        if (payload.Length < c + 4)
            throw new InvalidOperationException("RitualStart truncated.");
        return (name, BinaryPrimitives.ReadInt32LittleEndian(payload[c..]));
    }

    public static byte[] GrowthVatAssign(int vatThingId, int pawnThingId)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(buf, vatThingId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), pawnThingId);
        return buf;
    }

    public static (int VatThingId, int PawnThingId) ReadGrowthVatAssign(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidOperationException("GrowthVatAssign truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

    public static byte[] EntityContain(int entityThingId, int chamberThingId) =>
        GrowthVatAssign(entityThingId, chamberThingId);

    public static (int EntityThingId, int ChamberThingId) ReadEntityContain(ReadOnlySpan<byte> payload) =>
        ReadGrowthVatAssign(payload);

    /// <summary>Designator cell paint: designator kind + map id + cell + optional thing id.</summary>
    public static byte[] DesignatorCell(byte designatorKind, int mapId, int cellX, int cellZ, int thingId = -1)
    {
        var buf = new byte[1 + 4 + 4 + 4 + 4];
        buf[0] = designatorKind;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(1), mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(5), cellX);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(9), cellZ);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(13), thingId);
        return buf;
    }

    public static (byte Kind, int MapId, int CellX, int CellZ, int ThingId) ReadDesignatorCell(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 17)
            throw new InvalidOperationException("DesignatorCell payload truncated.");
        return (
            payload[0],
            BinaryPrimitives.ReadInt32LittleEndian(payload[1..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[5..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[9..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[13..]));
    }

    public static byte[] ForbidSet(int mapId, int thingId, bool forbidden)
    {
        var buf = new byte[9];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), thingId);
        buf[8] = forbidden ? (byte)1 : (byte)0;
        return buf;
    }

    public static (int MapId, int ThingId, bool Forbidden) ReadForbidSet(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 9)
            throw new InvalidOperationException("ForbidSet payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]),
            payload[8] != 0);
    }

    /// <summary>Work priority: pawn + workType def + priority 0–4.</summary>
    public static byte[] WorkPrioritySet(int pawnThingId, string workTypeDef, byte priority)
    {
        var name = Utf8Prefixed(workTypeDef);
        var buf = new byte[4 + name.Length + 1];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnThingId);
        name.CopyTo(buf.AsSpan(4));
        buf[^1] = priority;
        return buf;
    }

    public static (int PawnThingId, string WorkTypeDef, byte Priority) ReadWorkPrioritySet(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 7)
            throw new InvalidOperationException("WorkPrioritySet payload truncated.");
        var pawn = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var work = ReadUtf8Prefixed(payload[4..], out var c);
        if (payload.Length < 4 + c + 1)
            throw new InvalidOperationException("WorkPrioritySet priority truncated.");
        return (pawn, work, payload[4 + c]);
    }

    /// <summary>24-hour timetable: pawn + 24 assignment bytes (0=Anything …).</summary>
    public static byte[] TimetableSet(int pawnThingId, ReadOnlySpan<byte> hourAssignments24)
    {
        if (hourAssignments24.Length != 24)
            throw new ArgumentException("Timetable requires exactly 24 hour assignments.", nameof(hourAssignments24));
        var buf = new byte[4 + 24];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnThingId);
        hourAssignments24.CopyTo(buf.AsSpan(4));
        return buf;
    }

    public static (int PawnThingId, byte[] Hours) ReadTimetableSet(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 28)
            throw new InvalidOperationException("TimetableSet payload truncated.");
        var pawn = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var hours = payload.Slice(4, 24).ToArray();
        return (pawn, hours);
    }

    /// <summary>Assign named policy (food / outfit / drug) to a pawn.</summary>
    public static byte[] PolicyAssign(int pawnThingId, string policyId)
    {
        var name = Utf8Prefixed(policyId);
        var buf = new byte[4 + name.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnThingId);
        name.CopyTo(buf.AsSpan(4));
        return buf;
    }

    public static (int PawnThingId, string PolicyId) ReadPolicyAssign(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6)
            throw new InvalidOperationException("PolicyAssign payload truncated.");
        var pawn = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var policy = ReadUtf8Prefixed(payload[4..], out _);
        return (pawn, policy);
    }

    public static byte[] MedicalCareSet(int pawnThingId, byte careCategory)
    {
        var buf = new byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnThingId);
        buf[4] = careCategory;
        return buf;
    }

    public static (int PawnThingId, byte CareCategory) ReadMedicalCareSet(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 5)
            throw new InvalidOperationException("MedicalCareSet payload truncated.");
        return (BinaryPrimitives.ReadInt32LittleEndian(payload), payload[4]);
    }

    public static byte[] HostilityResponseSet(int pawnThingId, byte responseMode)
    {
        var buf = new byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnThingId);
        buf[4] = responseMode;
        return buf;
    }

    public static (int PawnThingId, byte ResponseMode) ReadHostilityResponseSet(ReadOnlySpan<byte> payload) =>
        ReadMedicalCareSet(payload);

    public static byte[] BillReorder(int mapId, int benchThingId, int fromSlot, int toSlot)
    {
        var buf = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), benchThingId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8), fromSlot);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(12), toSlot);
        return buf;
    }

    public static (int MapId, int BenchThingId, int FromSlot, int ToSlot) ReadBillReorder(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 16)
            throw new InvalidOperationException("BillReorder payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[12..]));
    }

    public static byte[] ResearchStop(int mapId)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        return buf;
    }

    public static int ReadResearchStop(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
            throw new InvalidOperationException("ResearchStop payload truncated.");
        return BinaryPrimitives.ReadInt32LittleEndian(payload);
    }

    public static byte[] TradeCancel(int mapId, int traderThingId)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(buf, mapId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), traderThingId);
        return buf;
    }

    public static (int MapId, int TraderThingId) ReadTradeCancel(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidOperationException("TradeCancel payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

    public static byte[] CaravanDisband(int caravanId)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, caravanId);
        return buf;
    }

    public static int ReadCaravanDisband(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
            throw new InvalidOperationException("CaravanDisband payload truncated.");
        return BinaryPrimitives.ReadInt32LittleEndian(payload);
    }
}

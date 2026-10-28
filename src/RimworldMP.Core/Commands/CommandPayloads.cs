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

    public static byte[] JobGive(int pawnLoadId, int cellX, int cellZ, string jobDefName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(jobDefName ?? string.Empty);
        var buf = new byte[4 + 4 + 4 + 2 + nameBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, pawnLoadId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), cellX);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8), cellZ);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(12), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(14));
        return buf;
    }

    public static (int PawnLoadId, int CellX, int CellZ, string JobDefName) ReadJobGive(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 14)
            throw new InvalidOperationException("JobGive payload truncated.");
        var pawn = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var x = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        var z = BinaryPrimitives.ReadInt32LittleEndian(payload[8..]);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[12..]);
        if (payload.Length < 14 + len)
            throw new InvalidOperationException("JobGive name truncated.");
        var name = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(14, len));
        return (pawn, x, z, name);
    }

    public static byte[] ZoneCells(byte zoneKind, int[] cellsPacked)
    {
        var buf = new byte[1 + 4 + cellsPacked.Length * 4];
        buf[0] = zoneKind;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(1), cellsPacked.Length);
        for (var i = 0; i < cellsPacked.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(5 + i * 4), cellsPacked[i]);
        return buf;
    }

    public static (byte ZoneKind, int[] CellsPacked) ReadZoneCells(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 5)
            throw new InvalidOperationException("Zone payload truncated.");
        var kind = payload[0];
        var count = BinaryPrimitives.ReadInt32LittleEndian(payload[1..]);
        if (count < 0 || payload.Length < 5 + count * 4)
            throw new InvalidOperationException("Zone cells truncated.");
        var cells = new int[count];
        for (var i = 0; i < count; i++)
            cells[i] = BinaryPrimitives.ReadInt32LittleEndian(payload[(5 + i * 4)..]);
        return (kind, cells);
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

    /// <summary>Bill set: bench thing id + bill slot + recipe def + repeat mode + count.</summary>
    public static byte[] BillSet(int benchThingId, int billSlot, string recipeDef, byte repeatMode, int count)
    {
        var nameBytes = Encoding.UTF8.GetBytes(recipeDef ?? string.Empty);
        var buf = new byte[4 + 4 + 1 + 4 + 2 + nameBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buf, benchThingId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), billSlot);
        buf[8] = repeatMode;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(9), count);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(13), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(15));
        return buf;
    }

    public static (int BenchThingId, int BillSlot, byte RepeatMode, int Count, string RecipeDef) ReadBillSet(
        ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 15)
            throw new InvalidOperationException("BillSet payload truncated.");
        var bench = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var slot = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        var mode = payload[8];
        var count = BinaryPrimitives.ReadInt32LittleEndian(payload[9..]);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[13..]);
        if (payload.Length < 15 + len)
            throw new InvalidOperationException("BillSet recipe truncated.");
        var recipe = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(15, len));
        return (bench, slot, mode, count, recipe);
    }

    public static byte[] BillDelete(int benchThingId, int billSlot)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(buf, benchThingId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), billSlot);
        return buf;
    }

    public static (int BenchThingId, int BillSlot) ReadBillDelete(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidOperationException("BillDelete payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }

    public static byte[] ResearchStart(string projectDef)
    {
        var nameBytes = Encoding.UTF8.GetBytes(projectDef ?? string.Empty);
        var buf = new byte[2 + nameBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, (ushort)nameBytes.Length);
        nameBytes.CopyTo(buf.AsSpan(2));
        return buf;
    }

    public static string ReadResearchStart(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2)
            throw new InvalidOperationException("ResearchStart payload truncated.");
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + len)
            throw new InvalidOperationException("ResearchStart name truncated.");
        return len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(2, len));
    }

    /// <summary>Trade accept: trader pawn/thing id + deal fingerprint (hash of offered silver/goods).</summary>
    public static byte[] TradeAccept(int traderThingId, long dealFingerprint)
    {
        var buf = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(buf, traderThingId);
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(4), dealFingerprint);
        return buf;
    }

    public static (int TraderThingId, long DealFingerprint) ReadTradeAccept(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 12)
            throw new InvalidOperationException("TradeAccept payload truncated.");
        return (
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt64LittleEndian(payload[4..]));
    }

    /// <summary>Caravan form: starting tile + packed pawn thing ids.</summary>
    public static byte[] CaravanForm(int tileId, int[] pawnThingIds)
    {
        var buf = new byte[4 + 4 + pawnThingIds.Length * 4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, tileId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), pawnThingIds.Length);
        for (var i = 0; i < pawnThingIds.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8 + i * 4), pawnThingIds[i]);
        return buf;
    }

    public static (int TileId, int[] PawnThingIds) ReadCaravanForm(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidOperationException("CaravanForm payload truncated.");
        var tile = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var count = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        if (count < 0 || payload.Length < 8 + count * 4)
            throw new InvalidOperationException("CaravanForm pawns truncated.");
        var pawns = new int[count];
        for (var i = 0; i < count; i++)
            pawns[i] = BinaryPrimitives.ReadInt32LittleEndian(payload[(8 + i * 4)..]);
        return (tile, pawns);
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

    private static byte[] Combine(byte[] a, byte[] b)
    {
        var buf = new byte[a.Length + b.Length];
        a.CopyTo(buf, 0);
        b.CopyTo(buf, a.Length);
        return buf;
    }
}

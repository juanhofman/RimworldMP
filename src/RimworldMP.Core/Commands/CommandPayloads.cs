using System.Buffers.Binary;
using System.Text;

namespace RimworldMP.Core.Commands;

/// <summary>Compact payload helpers for Phase 1 player commands.</summary>
public static class CommandPayloads
{
    public static byte[] Pause(bool paused)
    {
        return new[] { paused ? (byte)1 : (byte)0 };
    }

    public static bool ReadPause(ReadOnlySpan<byte> payload) => payload.Length > 0 && payload[0] != 0;

    public static byte[] Speed(byte speedIndex)
    {
        return new[] { speedIndex };
    }

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

    /// <summary>Job give: pawn + target cell + job def name.</summary>
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
        // cellsPacked: pairs of x,z flattened
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
}

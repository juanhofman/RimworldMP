using System.Buffers.Binary;
using System.Text;

namespace RimworldMP.API;

/// <summary>
/// Deterministic wire encoding for SyncMethod invoke payloads:
/// [u16 keyLen][key utf8][u16 argCount][args…].
/// Arg tags: Null, Bool, Int32, Int64, Single, Double, String, Bytes.
/// </summary>
public static class SyncMethodCodec
{
    /// <summary>Wire type tag for a SyncMethod argument.</summary>
#pragma warning disable CA1720 // Wire tags intentionally mirror CLR type names (stable protocol docs).
    public enum ArgTag : byte
    {
        /// <summary>Null reference.</summary>
        Null = 0,
        /// <summary>Boolean.</summary>
        Bool = 1,
        /// <summary>32-bit signed integer.</summary>
        Int32 = 2,
        /// <summary>64-bit signed integer.</summary>
        Int64 = 3,
        /// <summary>IEEE-754 single.</summary>
        Single = 4,
        /// <summary>IEEE-754 double.</summary>
        Double = 5,
        /// <summary>UTF-8 string (u16 length prefix).</summary>
        String = 6,
        /// <summary>Raw bytes (u16 length prefix).</summary>
        Bytes = 7,
    }
#pragma warning restore CA1720

    /// <summary>Default key form; prefer <see cref="MP.KeyOf"/> when attribute name overrides apply.</summary>
    public static string MethodKey(System.Reflection.MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return MP.KeyOf(method);
    }

    /// <summary>Encode a method key and argument list for <c>SyncMethodInvoke</c>.</summary>
    public static byte[] Encode(string methodKey, IReadOnlyList<object?> args)
    {
        ArgumentNullException.ThrowIfNull(methodKey);
        args ??= Array.Empty<object?>();

        var keyBytes = Encoding.UTF8.GetBytes(methodKey);
        if (keyBytes.Length > ushort.MaxValue)
            throw new InvalidOperationException("SyncMethod key too long.");

        using var ms = new MemoryStream(64 + keyBytes.Length);
        Span<byte> scratch = stackalloc byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(scratch, (ushort)keyBytes.Length);
        ms.Write(scratch[..2]);
        ms.Write(keyBytes);
        BinaryPrimitives.WriteUInt16LittleEndian(scratch, (ushort)args.Count);
        ms.Write(scratch[..2]);

        foreach (var arg in args)
            WriteArg(ms, arg);

        return ms.ToArray();
    }

    /// <summary>Decode a SyncMethod invoke payload into key + args.</summary>
    public static (string MethodKey, object?[] Args) Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
            throw new InvalidOperationException("SyncMethod payload truncated.");

        var keyLen = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + keyLen + 2)
            throw new InvalidOperationException("SyncMethod key truncated.");
        var key = keyLen == 0
            ? string.Empty
            : Encoding.UTF8.GetString(payload.Slice(2, keyLen));
        var o = 2 + keyLen;
        var argCount = BinaryPrimitives.ReadUInt16LittleEndian(payload[o..]);
        o += 2;
        var args = new object?[argCount];
        for (var i = 0; i < argCount; i++)
        {
            args[i] = ReadArg(payload[o..], out var consumed);
            o += consumed;
        }

        if (o != payload.Length)
            throw new InvalidOperationException("SyncMethod payload has trailing bytes.");
        return (key, args);
    }

    private static void WriteArg(MemoryStream ms, object? arg)
    {
        Span<byte> scratch = stackalloc byte[8];
        if (arg is null)
        {
            ms.WriteByte((byte)ArgTag.Null);
            return;
        }

        switch (arg)
        {
            case bool b:
                ms.WriteByte((byte)ArgTag.Bool);
                ms.WriteByte(b ? (byte)1 : (byte)0);
                break;
            case int i:
                ms.WriteByte((byte)ArgTag.Int32);
                BinaryPrimitives.WriteInt32LittleEndian(scratch, i);
                ms.Write(scratch[..4]);
                break;
            case long l:
                ms.WriteByte((byte)ArgTag.Int64);
                BinaryPrimitives.WriteInt64LittleEndian(scratch, l);
                ms.Write(scratch[..8]);
                break;
            case float f:
                ms.WriteByte((byte)ArgTag.Single);
                BinaryPrimitives.WriteInt32LittleEndian(scratch, BitConverter.SingleToInt32Bits(f));
                ms.Write(scratch[..4]);
                break;
            case double d:
                ms.WriteByte((byte)ArgTag.Double);
                BinaryPrimitives.WriteInt64LittleEndian(scratch, BitConverter.DoubleToInt64Bits(d));
                ms.Write(scratch[..8]);
                break;
            case string s:
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                if (bytes.Length > ushort.MaxValue)
                    throw new InvalidOperationException("SyncMethod string arg too long.");
                ms.WriteByte((byte)ArgTag.String);
                BinaryPrimitives.WriteUInt16LittleEndian(scratch, (ushort)bytes.Length);
                ms.Write(scratch[..2]);
                ms.Write(bytes);
                break;
            }
            case byte[] raw:
            {
                if (raw.Length > ushort.MaxValue)
                    throw new InvalidOperationException("SyncMethod bytes arg too long.");
                ms.WriteByte((byte)ArgTag.Bytes);
                BinaryPrimitives.WriteUInt16LittleEndian(scratch, (ushort)raw.Length);
                ms.Write(scratch[..2]);
                ms.Write(raw);
                break;
            }
            default:
                throw new NotSupportedException(
                    $"SyncMethod arg type '{arg.GetType().FullName}' is not supported. " +
                    "Use null/bool/int/long/float/double/string/byte[].");
        }
    }

    private static object? ReadArg(ReadOnlySpan<byte> payload, out int consumed)
    {
        if (payload.Length < 1)
            throw new InvalidOperationException("SyncMethod arg truncated.");
        var tag = (ArgTag)payload[0];
        switch (tag)
        {
            case ArgTag.Null:
                consumed = 1;
                return null;
            case ArgTag.Bool:
                if (payload.Length < 2)
                    throw new InvalidOperationException("SyncMethod bool truncated.");
                consumed = 2;
                return payload[1] != 0;
            case ArgTag.Int32:
                if (payload.Length < 5)
                    throw new InvalidOperationException("SyncMethod int truncated.");
                consumed = 5;
                return BinaryPrimitives.ReadInt32LittleEndian(payload[1..]);
            case ArgTag.Int64:
                if (payload.Length < 9)
                    throw new InvalidOperationException("SyncMethod long truncated.");
                consumed = 9;
                return BinaryPrimitives.ReadInt64LittleEndian(payload[1..]);
            case ArgTag.Single:
                if (payload.Length < 5)
                    throw new InvalidOperationException("SyncMethod float truncated.");
                consumed = 5;
                return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload[1..]));
            case ArgTag.Double:
                if (payload.Length < 9)
                    throw new InvalidOperationException("SyncMethod double truncated.");
                consumed = 9;
                return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(payload[1..]));
            case ArgTag.String:
            {
                if (payload.Length < 3)
                    throw new InvalidOperationException("SyncMethod string truncated.");
                var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[1..]);
                if (payload.Length < 3 + len)
                    throw new InvalidOperationException("SyncMethod string body truncated.");
                consumed = 3 + len;
                return len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(3, len));
            }
            case ArgTag.Bytes:
            {
                if (payload.Length < 3)
                    throw new InvalidOperationException("SyncMethod bytes truncated.");
                var len = BinaryPrimitives.ReadUInt16LittleEndian(payload[1..]);
                if (payload.Length < 3 + len)
                    throw new InvalidOperationException("SyncMethod bytes body truncated.");
                consumed = 3 + len;
                return len == 0 ? Array.Empty<byte>() : payload.Slice(3, len).ToArray();
            }
            default:
                throw new InvalidOperationException($"Unknown SyncMethod arg tag {(byte)tag}.");
        }
    }
}

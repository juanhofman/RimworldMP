#if !RIMWORLDMP_NO_GAME_REFS
using System.Buffers.Binary;
using System.Security.Cryptography;
using HarmonyLib;
using RimworldMP.Core.Session;
using Verse;

namespace RimworldMP.Patches;

/// <summary>
/// Lockstep tick gate: host emits the next batch, then every peer stalls until that batch is present.
/// When frozen (desync), the sim does not advance.
/// Postfix also samples world facts into the desync world-fingerprint slot (every 60 ticks).
/// </summary>
/// <remarks>Confirmed on RW 1.7.4990: <c>TickManager.DoSingleTick</c> present under Waylith.</remarks>
[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
internal static class TickManager_DoSingleTick_Patch
{
    private const int WorldSampleInterval = 60;

    private static bool Prefix()
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return true; // offline / no session — vanilla

        if (session.IsFrozen)
            return false;

        if (session.IsHost)
            session.HostEmitNextBatch();

        if (!session.TryAdvanceSim())
            return false; // stall until batch arrives

        return true;
    }

    private static void Postfix(TickManager __instance)
    {
        var session = SessionFacade.Current;
        if (session is not { IsActive: true })
            return;

        // Only sample after a real tick (Prefix allowed the original through).
        if (session.IsFrozen)
            return;

        var tick = __instance.TicksGame;
        if (tick % WorldSampleInterval != 0)
            return;

        Span<byte> fp = stackalloc byte[32];
        SampleWorldFingerprint(fp, __instance, session.Rand.OpinionHash);
        session.Desync.SetWorldFingerprint(fp);
    }

    private static void SampleWorldFingerprint(Span<byte> dest32, TickManager tm, ulong rngOpinion)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> num = stackalloc byte[8];

        BinaryPrimitives.WriteInt32LittleEndian(num, tm.TicksGame);
        hasher.AppendData(num[..4]);
        BinaryPrimitives.WriteInt32LittleEndian(num, tm.TicksAbs);
        hasher.AppendData(num[..4]);

        var maps = Find.Maps;
        BinaryPrimitives.WriteInt32LittleEndian(num, maps?.Count ?? 0);
        hasher.AppendData(num[..4]);
        if (maps is not null)
        {
            foreach (var map in maps.OrderBy(static m => m.uniqueID))
            {
                BinaryPrimitives.WriteInt32LittleEndian(num, map.uniqueID);
                hasher.AppendData(num[..4]);
                BinaryPrimitives.WriteInt32LittleEndian(num, map.mapPawns?.AllPawnsCount ?? 0);
                hasher.AppendData(num[..4]);
                BinaryPrimitives.WriteInt32LittleEndian(num, map.listerThings?.AllThings?.Count ?? 0);
                hasher.AppendData(num[..4]);
                BinaryPrimitives.WriteInt32LittleEndian(num, map.zoneManager?.AllZones?.Count ?? 0);
                hasher.AppendData(num[..4]);
            }
        }

        BinaryPrimitives.WriteUInt64LittleEndian(num, rngOpinion);
        hasher.AppendData(num);
        _ = hasher.GetCurrentHash(dest32);
    }
}
#endif

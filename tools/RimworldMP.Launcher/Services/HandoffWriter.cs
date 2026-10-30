using System.Text.Json;
using RimworldMP.Launcher.Models;

namespace RimworldMP.Launcher.Services;

public static class HandoffWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string Write(HandoffDocument document, string? path = null)
    {
        LauncherPaths.EnsureDataDirs();
        var target = path ?? LauncherPaths.HandoffPath;

        // Clear prior ack so the mod can consume a fresh handoff
        var ack = target + LauncherPaths.HandoffAckSuffix;
        if (File.Exists(ack))
            File.Delete(ack);

        var json = JsonSerializer.Serialize(document, JsonOptions);
        var tmp = target + ".tmp";
        File.WriteAllText(tmp, json);
        File.Copy(tmp, target, overwrite: true);
        File.Delete(tmp);
        return target;
    }

    public static HandoffDocument Create(
        string role,
        string host,
        int port,
        string modlistHash,
        string sessionName,
        string? rwBuild = null,
        bool autoStartLobby = true,
        int? maxClients = null,
        string? transport = null,
        string? playerDisplayName = null)
    {
        if (role is not ("host" or "client"))
            throw new ArgumentException("role must be \"host\" or \"client\".", nameof(role));

        var normalizedTransport = NormalizeTransport(transport);

        return new HandoffDocument
        {
            Protocol = 1,
            Role = role,
            Host = host,
            Port = port,
            ModlistHash = modlistHash,
            SessionName = sessionName,
            PlayerDisplayName = string.IsNullOrWhiteSpace(playerDisplayName) ? null : playerDisplayName.Trim(),
            AutoStartLobby = autoStartLobby,
            LauncherPid = Environment.ProcessId,
            MaxClients = maxClients,
            Transport = normalizedTransport,
            RwBuild = rwBuild,
            CreatedUtc = DateTime.UtcNow.ToString("o"),
        };
    }

    /// <summary>Normalize UI/CLI values to handoff tokens the mod accepts (<c>tcp</c> / <c>litenetlib</c>).</summary>
    public static string? NormalizeTransport(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "tcp" or "lan" or "lantcp" => "tcp",
            "litenetlib" or "udp" or "lnl" or "lite" => "litenetlib",
            _ => throw new ArgumentException(
                $"Unknown transport '{value}'. Use tcp or litenetlib.", nameof(value)),
        };
    }
}

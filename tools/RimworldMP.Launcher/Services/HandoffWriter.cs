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
        int? maxClients = null)
    {
        if (role is not ("host" or "client"))
            throw new ArgumentException("role must be \"host\" or \"client\".", nameof(role));

        return new HandoffDocument
        {
            Protocol = 1,
            Role = role,
            Host = host,
            Port = port,
            ModlistHash = modlistHash,
            SessionName = sessionName,
            AutoStartLobby = autoStartLobby,
            LauncherPid = Environment.ProcessId,
            MaxClients = maxClients,
            RwBuild = rwBuild,
            CreatedUtc = DateTime.UtcNow.ToString("o"),
        };
    }
}

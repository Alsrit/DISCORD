using System.Text.Json;
using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class ClientLogService(ClientPathService pathService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Write(string level, string category, string message, object? payload = null)
    {
        pathService.EnsureFolders();
        var entry = new ClientLogEntry(
            DateTimeOffset.UtcNow,
            level,
            category,
            message,
            JsonSerializer.Serialize(payload ?? new { }, JsonOptions));

        File.AppendAllLines(pathService.LogsPath, [JsonSerializer.Serialize(entry, JsonOptions)]);
    }

    public IReadOnlyCollection<ClientLogEntry> ReadRecent(int take = 100)
    {
        pathService.EnsureFolders();
        if (!File.Exists(pathService.LogsPath))
        {
            return [];
        }

        return File.ReadLines(pathService.LogsPath)
            .Reverse()
            .Take(take)
            .Select(line => JsonSerializer.Deserialize<ClientLogEntry>(line, JsonOptions))
            .OfType<ClientLogEntry>()
            .ToList();
    }
}

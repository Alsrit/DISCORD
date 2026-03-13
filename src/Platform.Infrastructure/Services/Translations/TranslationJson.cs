using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Infrastructure.Services.Translations;

internal static class TranslationJson
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Default);

    public static T Deserialize<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(json, Default);
            return result is null ? fallback : result;
        }
        catch
        {
            return fallback;
        }
    }
}

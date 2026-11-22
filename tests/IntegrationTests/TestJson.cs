using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntegrationTests;

/// <summary>Same wire format as the API: web defaults (camelCase) + enums as camelCase strings.</summary>
internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

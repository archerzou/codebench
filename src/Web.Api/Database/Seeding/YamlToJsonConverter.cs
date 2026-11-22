using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Web.Api.Database.Seeding;

/// <summary>
/// Converts a YAML document to a JSON tree, keeping scalar types: a <b>plain</b> scalar that looks like a
/// JSON number, <c>true</c>/<c>false</c> or <c>null</c>/<c>~</c> becomes that JSON type; anything quoted or
/// any other plain text stays a string. So <c>expected: 6</c> → <c>6</c>, <c>args: ["6"]</c> → <c>["6"]</c>,
/// <c>{ $gen: randomInts }</c> → <c>{"$gen":"randomInts"}</c>. (YamlDotNet's object deserializer would turn
/// every scalar into a string.)
/// </summary>
internal static partial class YamlToJsonConverter
{
    /// <exception cref="YamlException">The text is not valid YAML (has <c>Start.Line</c>).</exception>
    public static JsonNode? Convert(string yaml)
    {
        var stream = new YamlStream();
        using (var reader = new StringReader(yaml))
        {
            stream.Load(reader);
        }

        return stream.Documents.Count == 0 ? null : ToNode(stream.Documents[0].RootNode);
    }

    private static JsonNode? ToNode(YamlNode node) => node switch
    {
        YamlMappingNode mapping => ToObject(mapping),
        YamlSequenceNode sequence => new JsonArray([.. sequence.Children.Select(ToNode)]),
        YamlScalarNode scalar => ToValue(scalar),
        _ => throw new YamlException(node.Start, node.End, $"Unsupported YAML node '{node.NodeType}'.")
    };

    private static JsonObject ToObject(YamlMappingNode mapping)
    {
        var result = new JsonObject();
        foreach ((YamlNode key, YamlNode value) in mapping.Children)
        {
            if (key is not YamlScalarNode { Value: { } name })
            {
                throw new YamlException(key.Start, key.End, "Mapping keys must be plain text.");
            }

            if (!result.TryAdd(name, ToNode(value)))
            {
                throw new YamlException(key.Start, key.End, $"Duplicate key '{name}'.");
            }
        }

        return result;
    }

    private static JsonNode? ToValue(YamlScalarNode scalar)
    {
        // YAML content line breaks are LF; YamlDotNet keeps CRLF from Windows-edited files, which would make
        // starter/reference code differ by the machine that ran the seed.
        string value = (scalar.Value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);

        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(value);   // quoted or block (|, >) scalars are always text
        }

        return value switch
        {
            "" or "~" or "null" or "Null" or "NULL" => null,
            "true" or "True" or "TRUE" => JsonValue.Create(true),
            "false" or "False" or "FALSE" => JsonValue.Create(false),
            _ when JsonNumber().IsMatch(value) => JsonNode.Parse(value),
            _ => JsonValue.Create(value)
        };
    }

    // JSON number grammar: no leading zeros, no '+', no hex — "007" or "0x1F" stay strings.
    [GeneratedRegex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex JsonNumber();
}

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentValidation.Results;
using YamlDotNet.Core;

namespace Web.Api.Database.Seeding;

/// <summary>YAML text → validated <see cref="ProblemDefinition"/>; every failure becomes a <see cref="ProblemDefinitionException"/> naming the file.</summary>
internal static class ProblemDefinitionReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true
    };

    private static readonly ProblemDefinitionValidator Validator = new();

    public static ProblemDefinition Read(string yaml, string file)
    {
        JsonNode? root;
        try
        {
            root = YamlToJsonConverter.Convert(yaml);
        }
        catch (YamlException exception)
        {
            throw ProblemDefinitionException.For(file, $"invalid YAML at line {exception.Start.Line}: {exception.Message}", exception);
        }

        if (root is not JsonObject)
        {
            throw ProblemDefinitionException.For(file, "the document must be a mapping (slug:, title:, …)");
        }

        ProblemDefinition? definition;
        try
        {
            definition = root.Deserialize<ProblemDefinition>(JsonOptions);
        }
        catch (JsonException exception)
        {
            throw ProblemDefinitionException.For(file, $"unexpected shape{At(exception)}: {exception.Message}", exception);
        }

        if (definition is null)
        {
            throw ProblemDefinitionException.For(file, "the document is empty");
        }

        ValidationResult result = Validator.Validate(definition);
        if (!result.IsValid)
        {
            throw ProblemDefinitionException.For(file, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        }

        return definition;
    }

    private static string At(JsonException exception) =>
        string.IsNullOrEmpty(exception.Path) || exception.Path == "$" ? string.Empty : $" at '{exception.Path}'";
}

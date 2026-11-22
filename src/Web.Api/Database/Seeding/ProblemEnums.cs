namespace Web.Api.Database.Seeding;

/// <summary>
/// Maps YAML spellings to the domain enums: case-insensitive, dashes/underscores ignored
/// (<c>algorithms</c> → <c>Algorithms</c>, <c>unorderedNested</c> → <c>UnorderedNested</c>,
/// <c>secure-coding</c> → <c>SecureCoding</c>). Numeric strings are rejected.
/// </summary>
internal static class ProblemEnums
{
    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);

        return !normalized.All(char.IsAsciiDigit) &&
               Enum.TryParse(normalized, ignoreCase: true, out result) &&
               Enum.IsDefined(result);
    }

    public static TEnum Parse<TEnum>(string value)
        where TEnum : struct, Enum =>
        TryParse(value, out TEnum result)
            ? result
            : throw new ArgumentException($"'{value}' is not a valid {typeof(TEnum).Name}.", nameof(value));
}

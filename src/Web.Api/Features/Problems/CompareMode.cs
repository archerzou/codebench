namespace Web.Api.Features.Problems;

/// <summary>How a data-driven test compares the returned value with the expected one (guide 09).</summary>
public enum CompareMode
{
    Exact,
    Unordered,
    UnorderedNested,
    Approx
}

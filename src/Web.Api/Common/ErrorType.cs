namespace Web.Api.Common;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    Problem = 2,
    NotFound = 3,
    Conflict = 4,

    /// <summary>A dependency or configuration the request needs isn't available (503), e.g. an unset webhook secret.</summary>
    Unavailable = 5
}

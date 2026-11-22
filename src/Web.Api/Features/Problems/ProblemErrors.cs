using Web.Api.Common;

namespace Web.Api.Features.Problems;

public static class ProblemErrors
{
    /// <summary>Missing, or not visible to the caller (Draft/Archived, or a Pro track) — don't leak existence (EP-02).</summary>
    public static Error NotFound(string slug) => Error.NotFound(
        "Problems.NotFound",
        $"The problem '{slug}' was not found");

    public static Error NotFoundById(Guid problemId) => Error.NotFound(
        "Problems.NotFound",
        $"The problem with the Id = '{problemId}' was not found");

    public static readonly Error InvalidFilter = Error.Problem(
        "Problems.InvalidFilter",
        "Unknown track or difficulty. Use a name such as track=algorithms or difficulty=easy.");

    public static Error CannotPublishArchived(string slug) => Error.Conflict(
        "Problems.CannotPublishArchived",
        $"The problem '{slug}' is archived and cannot be published again");

    public static Error CannotArchiveDraft(string slug) => Error.Conflict(
        "Problems.CannotArchiveDraft",
        $"The problem '{slug}' is a draft; delete it instead of archiving it");
}

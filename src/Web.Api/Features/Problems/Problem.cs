using Web.Api.Common;

namespace Web.Api.Features.Problems;

/// <summary>
/// A practice problem. Lifecycle <c>Draft → Published → Archived</c> with guarded, idempotent transitions
/// (DM-02). <see cref="Version"/> counts publishes: 0 while never published, 1 after the first publish;
/// submissions record the version they ran against and bundles are keyed <c>{slug}/v{version}</c>.
/// Optimistic concurrency uses Postgres <c>xmin</c>, mapped as a shadow property (DM-07).
/// </summary>
public sealed class Problem : Entity
{
    private Problem()
    {
    }

    public Guid Id { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public Track Track { get; private set; }

    public Difficulty Difficulty { get; private set; }

    public GraderKind GraderKind { get; private set; }

    public bool IsFree { get; private set; }

    public ProblemStatus Status { get; private set; }

    public int Version { get; private set; }

    public string Signature { get; private set; } = string.Empty;

    public string StatementMarkdown { get; private set; } = string.Empty;

    public string StarterCode { get; private set; } = string.Empty;

    public string ReferenceSolution { get; private set; } = string.Empty;

    public ProblemLimits Limits { get; private set; } = ProblemLimits.Default;

    public IReadOnlyList<string> Tags { get; private set; } = [];

    public int SortOrder { get; private set; }

    public string? Schema { get; private set; }

    public string? SeedSql { get; private set; }

    public string? Model { get; private set; }

    public string? PlanRulesJson { get; private set; }

    public string? MetricsJson { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public static Problem CreateDraft(ProblemContent content, DateTime createdAt)
    {
        var problem = new Problem
        {
            Id = Guid.CreateVersion7(),
            Status = ProblemStatus.Draft,
            Version = 0,
            CreatedAt = createdAt
        };
        problem.Apply(content);
        problem.Raise(new ProblemCreatedDomainEvent(problem.Id, problem.Slug));

        return problem;
    }

    /// <summary>
    /// Draft → Published, incrementing <see cref="Version"/>. Publishing an already Published problem is a
    /// no-op (idempotent); an Archived problem cannot be republished.
    /// </summary>
    public Result Publish(DateTime publishedAt)
    {
        if (Status == ProblemStatus.Published)
        {
            return Result.Success();
        }

        if (Status == ProblemStatus.Archived)
        {
            return Result.Failure(ProblemErrors.CannotPublishArchived(Slug));
        }

        Status = ProblemStatus.Published;
        Version++;
        PublishedAt = publishedAt;
        Raise(new ProblemPublishedDomainEvent(Id, Slug, Version));

        return Result.Success();
    }

    /// <summary>Published → Archived. Idempotent; Drafts are deleted, not archived.</summary>
    public Result Archive()
    {
        if (Status == ProblemStatus.Archived)
        {
            return Result.Success();
        }

        if (Status == ProblemStatus.Draft)
        {
            return Result.Failure(ProblemErrors.CannotArchiveDraft(Slug));
        }

        Status = ProblemStatus.Archived;
        Raise(new ProblemArchivedDomainEvent(Id, Slug, Version));

        return Result.Success();
    }

    private void Apply(ProblemContent content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Signature);

        Slug = content.Slug;
        Title = content.Title;
        Track = content.Track;
        Difficulty = content.Difficulty;
        GraderKind = content.GraderKind;
        IsFree = content.IsFree;
        Signature = content.Signature;
        StatementMarkdown = content.StatementMarkdown;
        StarterCode = content.StarterCode;
        ReferenceSolution = content.ReferenceSolution;
        Limits = content.Limits with { };   // own copy: owned types are tracked by identity (see ProblemLimits.Default)
        Tags = [.. content.Tags];
        SortOrder = content.SortOrder;
        Schema = content.Schema;
        SeedSql = content.SeedSql;
        Model = content.Model;
        PlanRulesJson = content.PlanRulesJson;
        MetricsJson = content.MetricsJson;
    }
}

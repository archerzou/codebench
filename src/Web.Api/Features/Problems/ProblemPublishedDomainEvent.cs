using Web.Api.Common;

namespace Web.Api.Features.Problems;

/// <summary>Raised when a version goes live. In-process only (cache invalidation); the bundle is written by the publisher (2.4).</summary>
public sealed record ProblemPublishedDomainEvent(Guid ProblemId, string Slug, int Version) : IDomainEvent;

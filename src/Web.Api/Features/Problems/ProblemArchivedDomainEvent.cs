using Web.Api.Common;

namespace Web.Api.Features.Problems;

public sealed record ProblemArchivedDomainEvent(Guid ProblemId, string Slug, int Version) : IDomainEvent;

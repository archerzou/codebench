using Web.Api.Common;

namespace Web.Api.Features.Problems;

public sealed record ProblemCreatedDomainEvent(Guid ProblemId, string Slug) : IDomainEvent;

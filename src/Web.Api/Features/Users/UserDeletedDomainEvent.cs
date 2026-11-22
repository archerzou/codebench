using Web.Api.Common;

namespace Web.Api.Features.Users;

public sealed record UserDeletedDomainEvent(Guid UserId, string ExternalId) : IDomainEvent;

using Web.Api.Common;

namespace Web.Api.Features.Users;

/// <summary>Raised on a role change; the handler (3A) removes <c>permissions:{externalId}</c> and audits.</summary>
public sealed record UserRoleChangedDomainEvent(Guid UserId, string ExternalId, Role Role) : IDomainEvent;

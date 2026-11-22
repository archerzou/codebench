using Web.Api.Common;

namespace Web.Api.Features.Users;

public sealed record UserCreatedDomainEvent(Guid UserId, Role Role) : IDomainEvent;

namespace Web.Api.Authorization;

/// <summary>
/// Every permission name, <c>"{area}:{action}"</c> (AR-08, NM-07). Endpoints use these with
/// <c>.HasPermission(Permissions.X)</c>; <see cref="RolePermissionMatrix"/> grants them to roles.
/// </summary>
public static class Permissions
{
    public const string ProblemsRead = "problems:read";
    public const string ProblemsWrite = "problems:write";
    public const string ProblemsPublish = "problems:publish";
    public const string ProblemsDelete = "problems:delete";

    public const string TestsWrite = "tests:write";

    public const string SubmissionsCreate = "submissions:create";
    public const string SubmissionsReadOwn = "submissions:read-own";
    public const string SubmissionsReadAll = "submissions:read-all";

    public const string AdminAccess = "admin:access";
    public const string UsersManage = "users:manage";
    public const string StatsRead = "stats:read";

    public static readonly IReadOnlyList<string> All =
    [
        ProblemsRead, ProblemsWrite, ProblemsPublish, ProblemsDelete,
        TestsWrite,
        SubmissionsCreate, SubmissionsReadOwn, SubmissionsReadAll,
        AdminAccess, UsersManage, StatsRead
    ];
}

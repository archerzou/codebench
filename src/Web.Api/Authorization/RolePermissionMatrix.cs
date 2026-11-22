using Web.Api.Features.Users;

namespace Web.Api.Authorization;

/// <summary>
/// The role → permission matrix from guide 09, as code. <c>RoleSeeder</c> makes the
/// <c>role_permissions</c> table equal to this on every start; the database is what requests read (SC-03).
/// Each role includes everything the role below it has.
/// </summary>
public static class RolePermissionMatrix
{
    private static readonly string[] LearnerPermissions =
    [
        Permissions.ProblemsRead,
        Permissions.SubmissionsCreate,
        Permissions.SubmissionsReadOwn
    ];

    private static readonly string[] AuthorPermissions =
    [
        .. LearnerPermissions,
        Permissions.AdminAccess,
        Permissions.ProblemsWrite,
        Permissions.TestsWrite
    ];

    private static readonly string[] AdminPermissions =
    [
        .. AuthorPermissions,
        Permissions.ProblemsPublish,
        Permissions.ProblemsDelete,
        Permissions.UsersManage,
        Permissions.SubmissionsReadAll,
        Permissions.StatsRead
    ];

    public static IReadOnlyDictionary<Role, IReadOnlySet<string>> ByRole { get; } = new Dictionary<Role, IReadOnlySet<string>>
    {
        [Role.Learner] = new HashSet<string>(LearnerPermissions, StringComparer.Ordinal),
        [Role.Author] = new HashSet<string>(AuthorPermissions, StringComparer.Ordinal),
        [Role.Admin] = new HashSet<string>(AdminPermissions, StringComparer.Ordinal)
    };

    /// <summary>Every (role, permission) pair, i.e. the rows <c>role_permissions</c> must contain.</summary>
    public static IEnumerable<(Role Role, string Permission)> Rows =>
        ByRole.SelectMany(pair => pair.Value.Select(permission => (pair.Key, permission)));
}

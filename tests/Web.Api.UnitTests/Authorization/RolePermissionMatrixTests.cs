using System.Reflection;
using Web.Api.Authorization;
using Web.Api.Features.Users;

namespace Web.Api.UnitTests.Authorization;

public sealed class RolePermissionMatrixTests
{
    [Fact]
    public void ByRole_Should_MatchGuide09Table_When_Read()
    {
        RolePermissionMatrix.ByRole[Role.Learner].ShouldBe(
            ["problems:read", "submissions:create", "submissions:read-own"], ignoreOrder: true);

        RolePermissionMatrix.ByRole[Role.Author].ShouldBe(
            ["problems:read", "submissions:create", "submissions:read-own",
             "admin:access", "problems:write", "tests:write"], ignoreOrder: true);

        RolePermissionMatrix.ByRole[Role.Admin].ShouldBe(
            ["problems:read", "submissions:create", "submissions:read-own",
             "admin:access", "problems:write", "tests:write",
             "problems:publish", "problems:delete", "users:manage", "submissions:read-all", "stats:read"], ignoreOrder: true);
    }

    [Fact]
    public void ByRole_Should_IncludeEveryLowerRolePermission_When_RoleIsHigher()
    {
        RolePermissionMatrix.ByRole[Role.Learner].IsSubsetOf(RolePermissionMatrix.ByRole[Role.Author]).ShouldBeTrue();
        RolePermissionMatrix.ByRole[Role.Author].IsSubsetOf(RolePermissionMatrix.ByRole[Role.Admin]).ShouldBeTrue();
    }

    [Fact]
    public void ByRole_Should_CoverEveryRole_When_RoleIsAdded()
    {
        RolePermissionMatrix.ByRole.Keys.ShouldBe(Enum.GetValues<Role>(), ignoreOrder: true);
    }

    [Fact]
    public void Admin_Should_HoldEveryPermission_When_CatalogIsComplete()
    {
        RolePermissionMatrix.ByRole[Role.Admin].ShouldBe(Permissions.All, ignoreOrder: true);
    }

    [Fact]
    public void All_Should_ListEveryPermissionConstant_When_ConstantIsAdded()
    {
        string[] constants = [.. typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)];

        Permissions.All.ShouldBe(constants, ignoreOrder: true);
        Permissions.All.ShouldBeUnique();
    }

    [Fact]
    public void All_Should_FollowAreaActionFormat_When_Named()
    {
        foreach (string permission in Permissions.All)
        {
            permission.ShouldMatch("^[a-z]+:[a-z]+(-[a-z]+)*$");
        }
    }
}

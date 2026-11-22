using Web.Api.Database.Seeding;
using Web.Api.Features.Problems;

namespace Web.Api.UnitTests.Seeding;

public sealed class ProblemEnumsTests
{
    [Theory]
    [InlineData("algorithms", Track.Algorithms)]
    [InlineData("Database", Track.Database)]
    [InlineData("secure-coding", Track.SecureCoding)]
    [InlineData("test_writing", Track.TestWriting)]
    public void TryParse_Should_AcceptYamlSpellings_When_Valid(string value, Track expected)
    {
        ProblemEnums.TryParse(value, out Track track).ShouldBeTrue();
        track.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("quantum")]
    public void TryParse_Should_Reject_When_NotANamedMember(string value)
    {
        ProblemEnums.TryParse(value, out Track _).ShouldBeFalse();
    }
}

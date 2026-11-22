using System.Text.Json.Nodes;
using Web.Api.Database.Seeding;
using YamlDotNet.Core;

namespace Web.Api.UnitTests.Seeding;

public sealed class YamlToJsonConverterTests
{
    private static string Json(string yaml) => YamlToJsonConverter.Convert(yaml)!.ToJsonString();

    [Theory]
    [InlineData("v: 6", """{"v":6}""")]
    [InlineData("v: -1", """{"v":-1}""")]
    [InlineData("v: 16.5", """{"v":16.5}""")]
    [InlineData("v: 0.5", """{"v":0.5}""")]
    [InlineData("v: 1e3", """{"v":1e3}""")]
    [InlineData("v: true", """{"v":true}""")]
    [InlineData("v: False", """{"v":false}""")]
    [InlineData("v: null", """{"v":null}""")]
    [InlineData("v: ~", """{"v":null}""")]
    [InlineData("v:", """{"v":null}""")]
    public void Convert_Should_TypePlainScalars_When_TheyLookLikeJson(string yaml, string expected)
    {
        Json(yaml).ShouldBe(expected);
    }

    [Theory]
    [InlineData("""v: "6" """, """{"v":"6"}""")]
    [InlineData("v: '6'", """{"v":"6"}""")]
    [InlineData("""v: "true" """, """{"v":"true"}""")]
    [InlineData("v: 007", """{"v":"007"}""")]               // not a JSON number
    [InlineData("v: 0x1F", """{"v":"0x1F"}""")]
    [InlineData("v: banana", """{"v":"banana"}""")]
    [InlineData("v: 2026-05-18", """{"v":"2026-05-18"}""")]  // dates stay text
    public void Convert_Should_KeepStrings_When_QuotedOrNotNumeric(string yaml, string expected)
    {
        Json(yaml).ShouldBe(expected);
    }

    [Fact]
    public void Convert_Should_ConvertTestRow_When_FlowStyleWithGenerator()
    {
        const string Yaml = """
            - { name: Example, args: [[1, 2, 3, 4]], expected: 6 }
            - { name: Long, hidden: true, args: [{ $gen: randomInts, count: 200000, seed: 42 }], expected: { $ref: reference } }
            - { name: Mixed, args: ["local", 0.5, false], expected: 16.5 }
            """;

        Json(Yaml).ShouldBe(
            """[{"name":"Example","args":[[1,2,3,4]],"expected":6},""" +
            """{"name":"Long","hidden":true,"args":[{"$gen":"randomInts","count":200000,"seed":42}],"expected":{"$ref":"reference"}},""" +
            """{"name":"Mixed","args":["local",0.5,false],"expected":16.5}]""");
    }

    [Fact]
    public void Convert_Should_KeepBlockScalarText_When_Literal()
    {
        JsonNode? node = YamlToJsonConverter.Convert("""
            starter: |
              public class Solution
              {
                  public int X() => 1;
              }
            """);

        // A raw string literal has no final newline, so the block scalar has none either ("clip" chomping).
        node!["starter"]!.GetValue<string>().ShouldBe("public class Solution\n{\n    public int X() => 1;\n}");
    }

    [Fact]
    public void Convert_Should_NormaliseLineBreaks_When_FileHasWindowsLineEndings()
    {
        JsonNode? node = YamlToJsonConverter.Convert("starter: |\r\n  line 1\r\n  line 2\r\n");

        node!["starter"]!.GetValue<string>().ShouldBe("line 1\nline 2\n");
    }

    [Fact]
    public void Convert_Should_Throw_When_KeyIsDuplicated()
    {
        Should.Throw<YamlException>(() => YamlToJsonConverter.Convert("slug: a\nslug: b"));
    }

    [Fact]
    public void Convert_Should_Throw_When_YamlIsMalformed()
    {
        Should.Throw<YamlException>(() => YamlToJsonConverter.Convert("tests: [ { name: x"));
    }

    [Fact]
    public void Convert_Should_ReturnNull_When_DocumentIsEmpty()
    {
        YamlToJsonConverter.Convert(string.Empty).ShouldBeNull();
    }
}

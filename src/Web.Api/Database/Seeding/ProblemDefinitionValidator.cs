using System.Text.Json;
using FluentValidation;
using Web.Api.Features.Problems;

namespace Web.Api.Database.Seeding;

/// <summary>
/// Content rules for a seed file, checked before anything is written. Mirrors the entity and database
/// guards (TestCase expectation XOR, budgets) and adds authoring rules (kebab slug, at least one visible example).
/// </summary>
internal sealed class ProblemDefinitionValidator : AbstractValidator<ProblemDefinition>
{
    private const string KebabCase = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public ProblemDefinitionValidator()
    {
        RuleFor(d => d.Slug).NotEmpty().MaximumLength(64).Matches(KebabCase)
            .WithMessage("slug must be lowercase kebab-case (NM-09)");
        RuleFor(d => d.Title).NotEmpty().MaximumLength(200);
        RuleFor(d => d.Track).Must(BeEnum<Track>).WithMessage(d => $"unknown track '{d.Track}'");
        RuleFor(d => d.Difficulty).Must(BeEnum<Difficulty>).WithMessage(d => $"unknown difficulty '{d.Difficulty}'");
        RuleFor(d => d.Grader ?? d.Track).Must(BeEnum<GraderKind>).OverridePropertyName("grader")
            .WithMessage(d => $"unknown grader '{d.Grader ?? d.Track}'");
        RuleFor(d => d.Signature).NotEmpty().MaximumLength(500);
        RuleFor(d => d.Statement).NotEmpty();
        RuleFor(d => d.Starter).NotEmpty();
        RuleFor(d => d.Reference).NotEmpty()
            .When(d => !ProblemEnums.TryParse(d.Grader ?? d.Track, out GraderKind kind) || kind != GraderKind.Refactoring)
            .WithMessage("reference is required (only refactoring problems may omit it; their starter is the reference)");
        RuleFor(d => d.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(d => d.Tags).Must(t => t.Count <= 10).WithMessage("at most 10 tags");
        RuleForEach(d => d.Tags).NotEmpty().Matches(KebabCase).WithMessage("tags are lowercase kebab-case");

        RuleFor(d => d.Limits!.TimeoutSeconds).InclusiveBetween(1, 120).When(d => d.Limits is not null);
        RuleFor(d => d.Limits!.MemoryMb).InclusiveBetween(32, 4096).When(d => d.Limits is not null);

        RuleFor(d => d.PlanRules).Must(BeArrayOrAbsent).WithMessage("planRules must be a list");
        RuleFor(d => d.Metrics).Must(BeArrayOrAbsent).WithMessage("metrics must be a list");

        RuleFor(d => d.Tests).NotEmpty().WithMessage("at least one test is required");
        RuleFor(d => d.Tests).Must(t => t.Any(test => !test.Hidden)).When(d => d.Tests.Count > 0)
            .WithMessage("at least one visible test is required (shown as the example)");
        RuleFor(d => d.Tests)
            .Must(t => t.Select(test => test.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == t.Count)
            .WithMessage("test names must be unique (results are matched by name)");
        RuleForEach(d => d.Tests).SetValidator(new TestDefinitionValidator());
    }

    private static bool BeEnum<TEnum>(string? value)
        where TEnum : struct, Enum => ProblemEnums.TryParse<TEnum>(value, out _);

    private static bool BeArrayOrAbsent(JsonElement? value) => value is null || value.Value.ValueKind == JsonValueKind.Array;

    private sealed class TestDefinitionValidator : AbstractValidator<TestDefinition>
    {
        public TestDefinitionValidator()
        {
            RuleFor(t => t.Name).NotEmpty().MaximumLength(200);
            RuleFor(t => t.Args.ValueKind).Equal(JsonValueKind.Array).OverridePropertyName("args")
                .WithMessage(t => $"test '{t.Name}': args must be a list (one item per parameter)");
            RuleFor(t => t)
                .Must(t => t.Expected is not null != !string.IsNullOrWhiteSpace(t.ExpectedException))
                .OverridePropertyName("expected")
                .WithMessage(t => $"test '{t.Name}': needs exactly one of expected / expectedException");
            RuleFor(t => t.Compare).Must(c => c is null || ProblemEnums.TryParse<CompareMode>(c, out _))
                .WithMessage(t => $"test '{t.Name}': unknown compare '{t.Compare}' (exact, unordered, unorderedNested, approx)");
            RuleFor(t => t.TimeBudgetMs).GreaterThan(0).When(t => t.TimeBudgetMs is not null);
            RuleFor(t => t.AllocBudgetBytes).GreaterThanOrEqualTo(0).When(t => t.AllocBudgetBytes is not null);
        }
    }
}

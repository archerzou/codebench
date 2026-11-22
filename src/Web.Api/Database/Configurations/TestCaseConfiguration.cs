using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Api.Features.Problems;

namespace Web.Api.Database.Configurations;

internal sealed class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.HasKey(t => t.Id);

        builder.HasOne<Problem>().WithMany().HasForeignKey(t => t.ProblemId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => new { t.ProblemId, t.Name }).IsUnique();   // results are matched by test name
        builder.HasIndex(t => new { t.ProblemId, t.Order });

        builder.Property(t => t.ArgsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(t => t.ExpectedJson).HasColumnType("jsonb");
        builder.Property(t => t.ExpectedException).HasMaxLength(200);
        builder.Property(t => t.Compare).HasConversion<string>().HasMaxLength(32);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_test_cases_one_expectation", "(expected_json IS NULL) <> (expected_exception IS NULL)");
            t.HasCheckConstraint("ck_test_cases_order_non_negative", "\"order\" >= 0");
            t.HasCheckConstraint("ck_test_cases_budgets_positive",
                "(time_budget_ms IS NULL OR time_budget_ms > 0) AND (alloc_budget_bytes IS NULL OR alloc_budget_bytes >= 0)");
        });
    }
}

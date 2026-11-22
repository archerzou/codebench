using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Api.Features.Problems;

namespace Web.Api.Database.Configurations;

internal sealed class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    /// <summary>Shadow property over Postgres' <c>xmin</c> system column (DM-07); read with <c>EF.Property&lt;uint&gt;(p, RowVersion)</c>.</summary>
    internal const string RowVersion = "RowVersion";

    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Track).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Difficulty).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.GraderKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.Signature).HasMaxLength(500).IsRequired();

        builder.Property(p => p.StatementMarkdown).IsRequired();
        builder.Property(p => p.StarterCode).IsRequired();
        builder.Property(p => p.ReferenceSolution).IsRequired();

        builder.OwnsOne(p => p.Limits, limits => limits.ToJson());

        builder.PrimitiveCollection(p => p.Tags).HasColumnType("text[]").IsRequired();
        builder.Property(p => p.PlanRulesJson).HasColumnType("jsonb");
        builder.Property(p => p.MetricsJson).HasColumnType("jsonb");

        // The catalog lists Published problems by track, then sort order (GetProblems, 1.6).
        builder.HasIndex(p => new { p.Status, p.Track, p.SortOrder });

        builder.Property<uint>(RowVersion).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_problems_version_non_negative", "version >= 0");
            t.HasCheckConstraint("ck_problems_published_has_version", "status <> 'Published' OR version >= 1");
        });
    }
}

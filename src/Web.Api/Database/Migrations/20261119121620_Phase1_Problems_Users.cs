using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_Problems_Users : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                schema: "public",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_disabled",
                schema: "public",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "plan",
                schema: "public",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Free");

            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "public",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Learner");

            migrationBuilder.CreateTable(
                name: "problems",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    track = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    grader_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_free = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    signature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    statement_markdown = table.Column<string>(type: "text", nullable: false),
                    starter_code = table.Column<string>(type: "text", nullable: false),
                    reference_solution = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    schema = table.Column<string>(type: "text", nullable: true),
                    seed_sql = table.Column<string>(type: "text", nullable: true),
                    model = table.Column<string>(type: "text", nullable: true),
                    plan_rules_json = table.Column<string>(type: "jsonb", nullable: true),
                    metrics_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    limits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problems", x => x.id);
                    table.CheckConstraint("ck_problems_published_has_version", "status <> 'Published' OR version >= 1");
                    table.CheckConstraint("ck_problems_version_non_negative", "version >= 0");
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "public",
                columns: table => new
                {
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    permission = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role, x.permission });
                });

            migrationBuilder.CreateTable(
                name: "test_cases",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    problem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    args_json = table.Column<string>(type: "jsonb", nullable: false),
                    expected_json = table.Column<string>(type: "jsonb", nullable: true),
                    expected_exception = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    compare = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    time_budget_ms = table.Column<int>(type: "integer", nullable: true),
                    alloc_budget_bytes = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_cases", x => x.id);
                    table.CheckConstraint("ck_test_cases_budgets_positive", "(time_budget_ms IS NULL OR time_budget_ms > 0) AND (alloc_budget_bytes IS NULL OR alloc_budget_bytes >= 0)");
                    table.CheckConstraint("ck_test_cases_one_expectation", "(expected_json IS NULL) <> (expected_exception IS NULL)");
                    table.CheckConstraint("ck_test_cases_order_non_negative", "\"order\" >= 0");
                    table.ForeignKey(
                        name: "fk_test_cases_problems_problem_id",
                        column: x => x.problem_id,
                        principalSchema: "public",
                        principalTable: "problems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_problems_slug",
                schema: "public",
                table: "problems",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_problems_status_track_sort_order",
                schema: "public",
                table: "problems",
                columns: new[] { "status", "track", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_test_cases_problem_id_name",
                schema: "public",
                table: "test_cases",
                columns: new[] { "problem_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_test_cases_problem_id_order",
                schema: "public",
                table: "test_cases",
                columns: new[] { "problem_id", "order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "test_cases",
                schema: "public");

            migrationBuilder.DropTable(
                name: "problems",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "is_demo",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_disabled",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "plan",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "public",
                table: "users");
        }
    }
}

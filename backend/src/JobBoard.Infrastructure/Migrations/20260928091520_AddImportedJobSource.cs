using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobBoard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportedJobSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_id",
                table: "jobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_source_source_id",
                table: "jobs",
                columns: new[] { "source", "source_id" },
                unique: true,
                filter: "\"source\" IS NOT NULL AND \"source_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_jobs_source_source_id",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "source",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "source_id",
                table: "jobs");
        }
    }
}

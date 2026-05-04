using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLastRunFieldsFromPipelineSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRunAt",
                table: "pipeline_schedules");

            migrationBuilder.DropColumn(
                name: "LastRunStatus",
                table: "pipeline_schedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRunAt",
                table: "pipeline_schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastRunStatus",
                table: "pipeline_schedules",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}

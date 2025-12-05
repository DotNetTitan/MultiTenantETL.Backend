using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsScheduledFromPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsScheduled",
                table: "pipelines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsScheduled",
                table: "pipelines",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}

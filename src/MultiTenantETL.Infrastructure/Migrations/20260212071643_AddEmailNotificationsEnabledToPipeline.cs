using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailNotificationsEnabledToPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailNotificationsEnabled",
                table: "pipelines",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailNotificationsEnabled",
                table: "pipelines");
        }
    }
}

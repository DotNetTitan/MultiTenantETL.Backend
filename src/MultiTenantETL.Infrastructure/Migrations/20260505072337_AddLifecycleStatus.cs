using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_IsActive",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_IsActive_Name",
                table: "tenants");

            // Add new columns
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 1); // Default to Active

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 1); // Default to Active

            // Migrate data
            migrationBuilder.Sql("UPDATE users SET \"Status\" = 1 WHERE \"IsActive\" = true;");
            migrationBuilder.Sql("UPDATE users SET \"Status\" = 2 WHERE \"IsActive\" = false;");
            migrationBuilder.Sql("UPDATE tenants SET \"Status\" = 1 WHERE \"IsActive\" = true;");
            migrationBuilder.Sql("UPDATE tenants SET \"Status\" = 2 WHERE \"IsActive\" = false;");

            // Drop old columns
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "tenants");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Status",
                table: "tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Status_Name",
                table: "tenants",
                columns: new[] { "Status", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_Status",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_Status_Name",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "tenants");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_IsActive",
                table: "tenants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_IsActive_Name",
                table: "tenants",
                columns: new[] { "IsActive", "Name" });
        }
    }
}

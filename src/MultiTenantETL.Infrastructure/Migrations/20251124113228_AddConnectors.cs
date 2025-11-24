using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    IsSource = table.Column<bool>(type: "boolean", nullable: false),
                    IsDestination = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresCredentials = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    SchemaJson = table.Column<string>(type: "jsonb", nullable: true),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestResult = table.Column<string>(type: "text", nullable: true),
                    LastTestMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_connectors_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_connectors_Provider",
                table: "connectors",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_connectors_TenantId",
                table: "connectors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_connectors_TenantId_Name",
                table: "connectors",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_connectors_Type",
                table: "connectors",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connectors");
        }
    }
}

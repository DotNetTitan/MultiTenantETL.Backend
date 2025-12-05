using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <summary>
    /// Removes the transformations table as transformations are now embedded in Pipeline.FieldMappingsJson.
    /// 
    /// Data migration note: The transformation configurations were previously stored as standalone entities
    /// that could be referenced by pipelines. With the refactoring, transformations are now stored directly
    /// within field mappings in the pipeline's FieldMappingsJson column. Any existing transformation data
    /// in the transformations table is no longer used and will be dropped.
    /// </summary>
    /// <inheritdoc />
    public partial class RemoveTransformationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transformations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transformations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transformations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transformations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transformations_TenantId",
                table: "transformations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_transformations_TenantId_Name",
                table: "transformations",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_transformations_Type",
                table: "transformations",
                column: "Type");
        }
    }
}

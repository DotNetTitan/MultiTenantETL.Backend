using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SourceConnectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationConnectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FieldMappingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ScheduleJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsScheduled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunStatus = table.Column<string>(type: "text", nullable: true),
                    LastRunRecordsProcessed = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pipelines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pipelines_connectors_DestinationConnectorId",
                        column: x => x.DestinationConnectorId,
                        principalTable: "connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pipelines_connectors_SourceConnectorId",
                        column: x => x.SourceConnectorId,
                        principalTable: "connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_DestinationConnectorId",
                table: "pipelines",
                column: "DestinationConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_SourceConnectorId",
                table: "pipelines",
                column: "SourceConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_Status",
                table: "pipelines",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_TenantId",
                table: "pipelines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_TenantId_Name",
                table: "pipelines",
                columns: new[] { "TenantId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pipelines");
        }
    }
}

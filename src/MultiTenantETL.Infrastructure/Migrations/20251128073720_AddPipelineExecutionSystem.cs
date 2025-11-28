using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineExecutionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pipeline_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RecordsProcessed = table.Column<long>(type: "bigint", nullable: false),
                    RecordsSucceeded = table.Column<long>(type: "bigint", nullable: false),
                    RecordsFailed = table.Column<long>(type: "bigint", nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    BatchCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    SummaryJson = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    TriggeredBy = table.Column<string>(type: "text", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pipeline_executions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pipeline_executions_pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "execution_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchIndex = table.Column<int>(type: "integer", nullable: false),
                    RowsCount = table.Column<int>(type: "integer", nullable: false),
                    RowsSucceeded = table.Column<int>(type: "integer", nullable: false),
                    RowsFailed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckpointInfoJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_execution_batches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_execution_batches_pipeline_executions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "pipeline_executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "execution_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_execution_logs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_execution_logs_pipeline_executions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "pipeline_executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_batches_ExecutionId",
                table: "execution_batches",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_batches_ExecutionId_BatchIndex",
                table: "execution_batches",
                columns: new[] { "ExecutionId", "BatchIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_batches_TenantId",
                table: "execution_batches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_logs_ExecutionId",
                table: "execution_logs",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_logs_Level",
                table: "execution_logs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_execution_logs_TenantId",
                table: "execution_logs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_execution_logs_Timestamp",
                table: "execution_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_executions_PipelineId",
                table: "pipeline_executions",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_executions_StartTime",
                table: "pipeline_executions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_executions_Status",
                table: "pipeline_executions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_executions_TenantId",
                table: "pipeline_executions",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_batches");

            migrationBuilder.DropTable(
                name: "execution_logs");

            migrationBuilder.DropTable(
                name: "pipeline_executions");
        }
    }
}

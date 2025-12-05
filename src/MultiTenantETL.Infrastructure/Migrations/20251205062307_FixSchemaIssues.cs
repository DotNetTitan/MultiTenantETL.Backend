using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSchemaIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_Tenants_TenantId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_connectors_Tenants_TenantId",
                table: "connectors");

            migrationBuilder.DropForeignKey(
                name: "FK_execution_batches_Tenants_TenantId",
                table: "execution_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_execution_logs_Tenants_TenantId",
                table: "execution_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_pipeline_executions_Tenants_TenantId",
                table: "pipeline_executions");

            migrationBuilder.DropForeignKey(
                name: "FK_pipeline_schedules_Tenants_TenantId",
                table: "pipeline_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_pipelines_Tenants_TenantId",
                table: "pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_transformations_Tenants_TenantId",
                table: "transformations");

            migrationBuilder.DropForeignKey(
                name: "FK_users_Tenants_CurrentTenantId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenants_Tenants_TenantId",
                table: "UserTenants");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenants_users_UserId",
                table: "UserTenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tenants",
                table: "Tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserTenants",
                table: "UserTenants");

            migrationBuilder.RenameTable(
                name: "Tenants",
                newName: "tenants");

            migrationBuilder.RenameTable(
                name: "UserTenants",
                newName: "user_tenants");

            migrationBuilder.RenameIndex(
                name: "IX_UserTenants_TenantId",
                table: "user_tenants",
                newName: "IX_user_tenants_TenantId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenants",
                table: "tenants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_tenants",
                table: "user_tenants",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_IsActive",
                table: "tenants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_IsActive_Name",
                table: "tenants",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_tenants_TenantId_IsActive",
                table: "user_tenants",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_tenants_TenantId",
                table: "audit_logs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_connectors_tenants_TenantId",
                table: "connectors",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_execution_batches_tenants_TenantId",
                table: "execution_batches",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_execution_logs_tenants_TenantId",
                table: "execution_logs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pipeline_executions_tenants_TenantId",
                table: "pipeline_executions",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pipeline_schedules_tenants_TenantId",
                table: "pipeline_schedules",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pipelines_tenants_TenantId",
                table: "pipelines",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transformations_tenants_TenantId",
                table: "transformations",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_tenants_tenants_TenantId",
                table: "user_tenants",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_tenants_users_UserId",
                table: "user_tenants",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_tenants_CurrentTenantId",
                table: "users",
                column: "CurrentTenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_tenants_TenantId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_connectors_tenants_TenantId",
                table: "connectors");

            migrationBuilder.DropForeignKey(
                name: "FK_execution_batches_tenants_TenantId",
                table: "execution_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_execution_logs_tenants_TenantId",
                table: "execution_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_pipeline_executions_tenants_TenantId",
                table: "pipeline_executions");

            migrationBuilder.DropForeignKey(
                name: "FK_pipeline_schedules_tenants_TenantId",
                table: "pipeline_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_pipelines_tenants_TenantId",
                table: "pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_transformations_tenants_TenantId",
                table: "transformations");

            migrationBuilder.DropForeignKey(
                name: "FK_user_tenants_tenants_TenantId",
                table: "user_tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_user_tenants_users_UserId",
                table: "user_tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_users_tenants_CurrentTenantId",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenants",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_IsActive",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_IsActive_Name",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_Slug",
                table: "tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_tenants",
                table: "user_tenants");

            migrationBuilder.DropIndex(
                name: "IX_user_tenants_TenantId_IsActive",
                table: "user_tenants");

            migrationBuilder.RenameTable(
                name: "tenants",
                newName: "Tenants");

            migrationBuilder.RenameTable(
                name: "user_tenants",
                newName: "UserTenants");

            migrationBuilder.RenameIndex(
                name: "IX_user_tenants_TenantId",
                table: "UserTenants",
                newName: "IX_UserTenants_TenantId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tenants",
                table: "Tenants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserTenants",
                table: "UserTenants",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_Tenants_TenantId",
                table: "audit_logs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_connectors_Tenants_TenantId",
                table: "connectors",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_execution_batches_Tenants_TenantId",
                table: "execution_batches",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_execution_logs_Tenants_TenantId",
                table: "execution_logs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pipeline_executions_Tenants_TenantId",
                table: "pipeline_executions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pipeline_schedules_Tenants_TenantId",
                table: "pipeline_schedules",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pipelines_Tenants_TenantId",
                table: "pipelines",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transformations_Tenants_TenantId",
                table: "transformations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_Tenants_CurrentTenantId",
                table: "users",
                column: "CurrentTenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_Tenants_TenantId",
                table: "UserTenants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_users_UserId",
                table: "UserTenants",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTransformationsToTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transformations_pipelines_PipelineId",
                table: "transformations");

            migrationBuilder.DropIndex(
                name: "IX_transformations_PipelineId",
                table: "transformations");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "transformations");

            migrationBuilder.DropColumn(
                name: "PipelineId",
                table: "transformations");

            migrationBuilder.RenameColumn(
                name: "IsEnabled",
                table: "transformations",
                newName: "IsTemplate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsTemplate",
                table: "transformations",
                newName: "IsEnabled");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "transformations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PipelineId",
                table: "transformations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transformations_PipelineId",
                table: "transformations",
                column: "PipelineId");

            migrationBuilder.AddForeignKey(
                name: "FK_transformations_pipelines_PipelineId",
                table: "transformations",
                column: "PipelineId",
                principalTable: "pipelines",
                principalColumn: "Id");
        }
    }
}

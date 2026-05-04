using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertStatusToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "transformations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_transformations_PipelineId",
                table: "transformations",
                column: "PipelineId");

            migrationBuilder.AddForeignKey(
                name: "FK_transformations_pipelines_PipelineId",
                table: "transformations",
                column: "PipelineId",
                principalTable: "pipelines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transformations_pipelines_PipelineId",
                table: "transformations");

            migrationBuilder.DropIndex(
                name: "IX_transformations_PipelineId",
                table: "transformations");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "transformations");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "transformations");

            migrationBuilder.DropColumn(
                name: "PipelineId",
                table: "transformations");
        }
    }
}

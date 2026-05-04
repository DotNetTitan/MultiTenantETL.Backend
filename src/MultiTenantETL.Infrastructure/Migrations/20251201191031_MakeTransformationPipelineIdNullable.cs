using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeTransformationPipelineIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transformations_pipelines_PipelineId",
                table: "transformations");

            migrationBuilder.AlterColumn<Guid>(
                name: "PipelineId",
                table: "transformations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_transformations_pipelines_PipelineId",
                table: "transformations",
                column: "PipelineId",
                principalTable: "pipelines",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transformations_pipelines_PipelineId",
                table: "transformations");

            migrationBuilder.AlterColumn<Guid>(
                name: "PipelineId",
                table: "transformations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_transformations_pipelines_PipelineId",
                table: "transformations",
                column: "PipelineId",
                principalTable: "pipelines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

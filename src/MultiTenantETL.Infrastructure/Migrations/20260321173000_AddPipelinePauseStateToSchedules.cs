using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MultiTenantETL.Infrastructure.Persistence;

#nullable disable

namespace MultiTenantETL.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260321173000_AddPipelinePauseStateToSchedules")]
    public partial class AddPipelinePauseStateToSchedules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPausedByPipeline",
                table: "pipeline_schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPausedByPipeline",
                table: "pipeline_schedules");
        }
    }
}

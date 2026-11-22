using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planning.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedActivityImportance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsImportant",
                table: "PlannedActivity",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsImportant",
                table: "PlannedActivity");
        }
    }
}

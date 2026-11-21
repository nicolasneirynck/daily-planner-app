using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planning.Persistence.Migrations
{
    public partial class RemoveProductsAndProjects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Project");
            migrationBuilder.DropTable(name: "Product");
            migrationBuilder.DropTable(name: "Technician");

            migrationBuilder.Sql("""
                DELETE FROM "PlannedActivity"
                WHERE "UserId" IN (
                    SELECT ur."UserId"
                    FROM "UserRoles" AS ur
                    JOIN "Roles" AS r ON r."Id" = ur."RoleId"
                    WHERE r."Name" = 'Technician'
                );

                DELETE FROM "TimeBlock"
                WHERE "UserId" IN (
                    SELECT ur."UserId"
                    FROM "UserRoles" AS ur
                    JOIN "Roles" AS r ON r."Id" = ur."RoleId"
                    WHERE r."Name" = 'Technician'
                );

                DELETE FROM "Activity"
                WHERE "UserId" IN (
                    SELECT ur."UserId"
                    FROM "UserRoles" AS ur
                    JOIN "Roles" AS r ON r."Id" = ur."RoleId"
                    WHERE r."Name" = 'Technician'
                );

                DELETE FROM "Users"
                WHERE "Id" IN (
                    SELECT ur."UserId"
                    FROM "UserRoles" AS ur
                    JOIN "Roles" AS r ON r."Id" = ur."RoleId"
                    WHERE r."Name" = 'Technician'
                );

                DELETE FROM "Roles" WHERE "Name" = 'Technician';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "The removed product, project, technician, and identity data cannot be restored by this migration.");
        }
    }
}

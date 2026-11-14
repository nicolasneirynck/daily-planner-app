using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planning.Persistence.Migrations
{
    public partial class AddTimeBlockHoursAndMidday : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "TimeBlock",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "TimeBlock",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                DECLARE current_order integer;
                BEGIN
                    SELECT MAX("SortOrder") INTO current_order FROM "TimeBlock";

                    WHILE current_order >= 3 LOOP
                        UPDATE "TimeBlock"
                        SET "SortOrder" = current_order + 1
                        WHERE "SortOrder" = current_order;

                        current_order := current_order - 1;
                    END LOOP;
                END $$;

                UPDATE "TimeBlock"
                SET "StartTime" = TIME '06:00',
                    "EndTime" = TIME '23:00';

                INSERT INTO "TimeBlock" ("UserId", "Name", "SortOrder", "StartTime", "EndTime")
                SELECT users."Id", template."Name", template."SortOrder",
                       template."StartTime", template."EndTime"
                FROM "Users" AS users
                CROSS JOIN (VALUES
                    (1, 'Ochtend', TIME '06:00', TIME '09:00'),
                    (2, 'Voormiddag', TIME '09:00', TIME '12:00'),
                    (3, 'Middag', TIME '12:00', TIME '14:00'),
                    (4, 'Vroege namiddag', TIME '14:00', TIME '16:00'),
                    (5, 'Late namiddag', TIME '16:00', TIME '18:00'),
                    (6, 'Avond', TIME '18:00', TIME '23:00')
                ) AS template("SortOrder", "Name", "StartTime", "EndTime")
                WHERE TRUE
                ON CONFLICT ("UserId", "SortOrder") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "StartTime" = EXCLUDED."StartTime",
                    "EndTime" = EXCLUDED."EndTime",
                    "IsDeleted" = FALSE,
                    "UpdatedAt" = CURRENT_TIMESTAMP;
                """);

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "StartTime",
                table: "TimeBlock",
                type: "time without time zone",
                nullable: false,
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "EndTime",
                table: "TimeBlock",
                type: "time without time zone",
                nullable: false,
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "The midday block can contain activities and cannot be removed safely by this migration.");
        }
    }
}

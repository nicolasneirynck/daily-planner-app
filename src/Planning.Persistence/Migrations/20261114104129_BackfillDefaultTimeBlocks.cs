using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planning.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDefaultTimeBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "TimeBlock" ("UserId", "Name", "SortOrder")
                SELECT users."Id", template."Name", template."SortOrder"
                FROM "Users" AS users
                CROSS JOIN (VALUES
                    (1, 'Ochtend'),
                    (2, 'Voormiddag'),
                    (3, 'Vroege namiddag'),
                    (4, 'Late namiddag'),
                    (5, 'Avond')
                ) AS template("SortOrder", "Name")
                WHERE TRUE
                ON CONFLICT ("UserId", "SortOrder") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "IsDeleted" = FALSE,
                    "UpdatedAt" = CURRENT_TIMESTAMP;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Backfilled blocks cannot be distinguished safely from blocks added later.
        }
    }
}

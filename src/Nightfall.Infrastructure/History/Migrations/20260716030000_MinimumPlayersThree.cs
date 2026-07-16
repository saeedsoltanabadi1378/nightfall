using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nightfall.Infrastructure.History.Migrations;

[DbContext(typeof(NightfallDbContext))]
[Migration("20260716030000_MinimumPlayersThree")]
public sealed class MinimumPlayersThree : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE bot_settings
            SET "MinPlayers" = 3
            WHERE "MinPlayers" = 5;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE bot_settings
            SET "MinPlayers" = 5
            WHERE "MinPlayers" = 3;
            """);
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityColumnsToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Rows created before Identity existed get their lookup key here;
            // UPPER matches Identity's invariant upper-casing for ASCII emails.
            migrationBuilder.Sql("UPDATE Users SET NormalizedEmail = UPPER(Email);");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");
        }
    }
}

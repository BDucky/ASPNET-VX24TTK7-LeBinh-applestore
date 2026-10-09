using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PriceChangeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    OldPrice = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    NewPrice = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceChanges_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceChanges_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceChanges_ChangedAt",
                table: "PriceChanges",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChanges_ChangedByUserId",
                table: "PriceChanges",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceChanges_VariantId_ChangedAt",
                table: "PriceChanges",
                columns: new[] { "VariantId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceChanges");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Supplier = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    FormKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReceipts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StockReceiptLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiptId = table.Column<int>(type: "INTEGER", nullable: false),
                    VariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    OpeningStock = table.Column<int>(type: "INTEGER", nullable: false),
                    ClosingStock = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReceiptLines_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReceiptLines_StockReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "StockReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptLines_ReceiptId_VariantId",
                table: "StockReceiptLines",
                columns: new[] { "ReceiptId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptLines_VariantId",
                table: "StockReceiptLines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_CreatedAt",
                table: "StockReceipts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_CreatedByUserId",
                table: "StockReceipts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_FormKey",
                table: "StockReceipts",
                column: "FormKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockReceiptLines");

            migrationBuilder.DropTable(
                name: "StockReceipts");
        }
    }
}

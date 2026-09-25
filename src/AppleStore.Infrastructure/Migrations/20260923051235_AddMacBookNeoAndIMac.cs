using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMacBookNeoAndIMac : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "Name", "Slug", "Description", "BasePrice", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 26, 2, "MacBook Neo", "macbook-neo", "A more affordable fanless MacBook, Apple's newest entry point into the Mac lineup.", 799.00m, true, SeededAt, SeededAt },
                    { 27, 2, "iMac", "imac", "An all-in-one desktop Mac with the M4 chip, built into a thin, colorful display.", 1299.00m, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 26, 26, "MBNEO-256", 799.00m, 45, true, SeededAt, SeededAt },
                    { 27, 27, "IMAC-M4-256", 1299.00m, 20, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ProductId", "VariantId", "ImageUrl", "SortOrder" },
                values: new object[,]
                {
                    { 26, 26, null, "/img/products/macbook-neo.webp", 0 },
                    { 27, 27, null, "/img/products/imac.jpg", 0 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id BETWEEN 26 AND 27;");
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id BETWEEN 26 AND 27;");
            migrationBuilder.Sql("DELETE FROM Products WHERE Id BETWEEN 26 AND 27;");
        }
    }
}

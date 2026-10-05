using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppleTV4K : Migration
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
                    { 34, 7, "Apple TV 4K", "apple-tv-4k", "A compact set-top box for streaming, gaming, and the Home app, with the Siri Remote.", 149.00m, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 34, 34, "APPLETV4K-64", 149.00m, 45, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ProductId", "VariantId", "ImageUrl", "SortOrder" },
                values: new object[,]
                {
                    { 34, 34, null, "/img/products/apple-tv-4k.jpg", 0 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id = 34;");
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id = 34;");
            migrationBuilder.Sql("DELETE FROM Products WHERE Id = 34;");
        }
    }
}

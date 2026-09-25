using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIPhone18ProMaxAnd17e : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // iPhone Duo (the new foldable-style flagship) is deliberately left
            // out: live-researched Commons "iPhone Duo" photos all traced back to
            // recycled rumor-era stock images (EXIF shows a Windows Phone camera
            // model photographing an "Apple" device), not genuine photography.
            // Same standard as the iPhone 18 Pro Max / Mac Pro omissions already
            // in this catalog.
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "Name", "Slug", "Description", "BasePrice", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 24, 1, "iPhone 18 Pro Max", "iphone-18-pro-max", "The largest iPhone 18 Pro, with the latest chip and camera system in a bigger body.", 1399.00m, true, SeededAt, SeededAt },
                    { 25, 1, "iPhone 17e", "iphone-17e", "A lower-cost iPhone 17 built around the A19 chip, Apple's successor to the SE line.", 599.00m, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 24, 24, "IP18PROMAX-256", 1399.00m, 35, true, SeededAt, SeededAt },
                    { 25, 25, "IP17E-128", 599.00m, 55, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ProductId", "VariantId", "ImageUrl", "SortOrder" },
                values: new object[,]
                {
                    { 24, 24, null, "/img/products/iphone-18-pro-max.jpg", 0 },
                    { 25, 25, null, "/img/products/iphone-17e.jpg", 0 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id BETWEEN 24 AND 25;");
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id BETWEEN 24 AND 25;");
            migrationBuilder.Sql("DELETE FROM Products WHERE Id BETWEEN 24 AND 25;");
        }
    }
}

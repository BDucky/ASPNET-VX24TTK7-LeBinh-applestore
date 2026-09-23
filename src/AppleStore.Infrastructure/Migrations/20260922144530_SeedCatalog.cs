using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCatalog : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name", "Slug", "ParentId" },
                values: new object[,]
                {
                    { 1, "iPhone", "iphone", null },
                    { 2, "Mac", "mac", null },
                    { 3, "iPad", "ipad", null },
                    { 4, "Watch", "watch", null },
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "Name", "Slug", "Description", "BasePrice", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 1, "iPhone 17", "iphone-17", "The standard iPhone 17 with the A19 chip and a 6.3-inch display.", 999.00m, true, SeededAt, SeededAt },
                    { 2, 1, "iPhone 17 Pro", "iphone-17-pro", "Titanium design, A19 Pro chip, and a pro camera system.", 1199.00m, true, SeededAt, SeededAt },
                    { 3, 1, "iPhone Air", "iphone-air", "The thinnest iPhone yet, built around the A19 chip.", 1099.00m, true, SeededAt, SeededAt },
                    { 4, 2, "MacBook Air", "macbook-air", "13-inch MacBook Air with the M4 chip, fanless and silent.", 1099.00m, true, SeededAt, SeededAt },
                    { 5, 2, "MacBook Pro", "macbook-pro", "14-inch MacBook Pro with the M4 Pro chip for demanding workflows.", 1999.00m, true, SeededAt, SeededAt },
                    { 6, 2, "Mac mini", "mac-mini", "Compact desktop Mac built around the M4 chip.", 599.00m, true, SeededAt, SeededAt },
                    { 7, 3, "iPad", "ipad", "The everyday iPad with the A16 chip.", 349.00m, true, SeededAt, SeededAt },
                    { 8, 3, "iPad Air", "ipad-air", "Thin, light, and powerful with the M2 chip.", 599.00m, true, SeededAt, SeededAt },
                    { 9, 3, "iPad Pro", "ipad-pro", "The ultimate iPad experience with the M4 chip and Ultra Retina XDR display.", 999.00m, true, SeededAt, SeededAt },
                    { 10, 4, "Apple Watch Series 11", "apple-watch-series-11", "The latest Apple Watch with advanced health features.", 399.00m, true, SeededAt, SeededAt },
                    { 11, 4, "Apple Watch SE", "apple-watch-se", "The essential Apple Watch experience at a lower price.", 249.00m, true, SeededAt, SeededAt },
                    { 12, 4, "Apple Watch Ultra 3", "apple-watch-ultra-3", "The most rugged and capable Apple Watch, built for adventure.", 799.00m, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 1, "IP17-128", 999.00m, 50, true, SeededAt, SeededAt },
                    { 2, 2, "IP17PRO-256", 1199.00m, 30, true, SeededAt, SeededAt },
                    { 3, 3, "IPAIR-256", 1099.00m, 25, true, SeededAt, SeededAt },
                    { 4, 4, "MBA13-M4-256", 1099.00m, 20, true, SeededAt, SeededAt },
                    { 5, 5, "MBP14-M4PRO-512", 1999.00m, 15, true, SeededAt, SeededAt },
                    { 6, 6, "MACMINI-M4-256", 599.00m, 40, true, SeededAt, SeededAt },
                    { 7, 7, "IPAD-A16-64", 349.00m, 60, true, SeededAt, SeededAt },
                    { 8, 8, "IPADAIR-M2-128", 599.00m, 35, true, SeededAt, SeededAt },
                    { 9, 9, "IPADPRO-M4-256", 999.00m, 20, true, SeededAt, SeededAt },
                    { 10, 10, "AWS11-41", 399.00m, 45, true, SeededAt, SeededAt },
                    { 11, 11, "AWSE-40", 249.00m, 50, true, SeededAt, SeededAt },
                    { 12, 12, "AWU3-49", 799.00m, 18, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ProductId", "VariantId", "ImageUrl", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, null, "/img/products/iphone.svg", 0 },
                    { 2, 2, null, "/img/products/iphone.svg", 0 },
                    { 3, 3, null, "/img/products/iphone.svg", 0 },
                    { 4, 4, null, "/img/products/mac.svg", 0 },
                    { 5, 5, null, "/img/products/mac.svg", 0 },
                    { 6, 6, null, "/img/products/mac.svg", 0 },
                    { 7, 7, null, "/img/products/ipad.svg", 0 },
                    { 8, 8, null, "/img/products/ipad.svg", 0 },
                    { 9, 9, null, "/img/products/ipad.svg", 0 },
                    { 10, 10, null, "/img/products/watch.svg", 0 },
                    { 11, 11, null, "/img/products/watch.svg", 0 },
                    { 12, 12, null, "/img/products/watch.svg", 0 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id BETWEEN 1 AND 12;");
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id BETWEEN 1 AND 12;");
            migrationBuilder.Sql("DELETE FROM Products WHERE Id BETWEEN 1 AND 12;");
            migrationBuilder.Sql("DELETE FROM Categories WHERE Id BETWEEN 1 AND 4;");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchSeries12Ultra4AndAirPods : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All 5 of these launched within the last few months, too recent for
            // free-licensed photography of the exact new generation to exist yet.
            // Series 12 / Ultra 4 / AirPods 5 use a disclosed prior-generation
            // photo (same substitution pattern already used for "Apple Watch
            // Series 11" in this catalog, which shows a Series 10). AirPods Pro 3
            // has a genuine current-generation photo, just an unusual color.
            // AirPods Max 2 reuses the same DummyJSON hotlink already used for
            // this catalog's "AirPods Max" (unchanged external shell design).
            // All disclosed on the Credits page, not silently substituted.
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "Name", "Slug", "Description", "BasePrice", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 29, 4, "Apple Watch Series 12", "apple-watch-series-12", "The latest Apple Watch, successor to Series 11, with advanced health features.", 429.00m, true, SeededAt, SeededAt },
                    { 30, 4, "Apple Watch Ultra 4", "apple-watch-ultra-4", "The most rugged and capable Apple Watch, successor to Ultra 3, built for adventure.", 849.00m, true, SeededAt, SeededAt },
                    { 31, 6, "AirPods 5", "airpods-5", "The latest AirPods, successor to the previous generation, with USB-C charging.", 149.99m, true, SeededAt, SeededAt },
                    { 32, 6, "AirPods Pro 3", "airpods-pro-3", "Active noise cancellation earbuds with a redesigned driver and improved seal.", 249.99m, true, SeededAt, SeededAt },
                    { 33, 6, "AirPods Max 2", "airpods-max-2", "Over-ear headphones with high-fidelity audio, adaptive EQ, and active noise cancellation.", 579.99m, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 29, 29, "AWS12-41", 429.00m, 50, true, SeededAt, SeededAt },
                    { 30, 30, "AWU4-49", 849.00m, 15, true, SeededAt, SeededAt },
                    { 31, 31, "AIRPODS5-USBC", 149.99m, 70, true, SeededAt, SeededAt },
                    { 32, 32, "AIRPODSPRO3", 249.99m, 60, true, SeededAt, SeededAt },
                    { 33, 33, "AIRPODSMAX2", 579.99m, 25, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ProductId", "VariantId", "ImageUrl", "SortOrder" },
                values: new object[,]
                {
                    { 29, 29, null, "/img/products/apple-watch-series-12.png", 0 },
                    { 30, 30, null, "/img/products/apple-watch-ultra-4.jpg", 0 },
                    { 31, 31, null, "/img/products/airpods-5.jpg", 0 },
                    { 32, 32, null, "/img/products/airpods-pro-3.jpg", 0 },
                    { 33, 33, null, "https://cdn.dummyjson.com/product-images/mobile-accessories/apple-airpods-max-silver/thumbnail.webp", 0 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id BETWEEN 29 AND 33;");
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id BETWEEN 29 AND 33;");
            migrationBuilder.Sql("DELETE FROM Products WHERE Id BETWEEN 29 AND 33;");
        }
    }
}

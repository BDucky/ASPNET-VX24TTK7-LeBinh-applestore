using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCatalogWithAccessories : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // New category, and older iPhone generations still realistically sold
            // by a reseller alongside the current lineup, plus accessories.
            // Sourced from DummyJSON (a free placeholder/prototyping REST API),
            // images hotlinked from its CDN rather than downloaded into this repo,
            // see docs/submission.md and the site's own Credits page for the
            // licensing caveat: DummyJSON publishes no explicit image license.
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name", "Slug", "ParentId" },
                values: new object[,]
                {
                    { 5, "Accessories", "accessories", null },
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "Name", "Slug", "Description", "BasePrice", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 13, 1, "iPhone 13 Pro", "iphone-13-pro", "The iPhone 13 Pro is a cutting-edge smartphone with a powerful camera system, high-performance chip, and stunning display.", 1099.99m, true, SeededAt, SeededAt },
                    { 14, 1, "iPhone X", "iphone-x", "The iPhone X is a flagship smartphone featuring a bezel-less OLED display, facial recognition technology (Face ID), and impressive performance.", 899.99m, true, SeededAt, SeededAt },
                    { 15, 1, "iPhone 6", "iphone-6", "The iPhone 6 is a stylish and capable smartphone with a larger display and improved performance.", 299.99m, true, SeededAt, SeededAt },
                    { 16, 1, "iPhone 5s", "iphone-5s", "The iPhone 5s is a classic smartphone known for its compact design, still available for budget buyers.", 199.99m, true, SeededAt, SeededAt },
                    { 17, 5, "AirPods", "airpods", "The Apple AirPods offer a seamless wireless audio experience, with easy pairing, high-quality sound, and Siri integration.", 129.99m, true, SeededAt, SeededAt },
                    { 18, 5, "AirPods Max", "airpods-max-silver", "The Apple AirPods Max in Silver are premium over-ear headphones with high-fidelity audio, adaptive EQ, and active noise cancellation.", 549.99m, true, SeededAt, SeededAt },
                    { 19, 5, "HomePod mini", "homepod-mini", "The Apple HomePod mini in Cosmic Grey is a compact smart speaker that integrates seamlessly with the Apple ecosystem.", 99.99m, true, SeededAt, SeededAt },
                    { 20, 5, "MagSafe Battery Pack", "magsafe-battery-pack", "The Apple MagSafe Battery Pack is a portable way to add extra battery life to a MagSafe-compatible iPhone.", 99.99m, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 13, 13, "IP13PRO-128", 1099.99m, 56, true, SeededAt, SeededAt },
                    { 14, 14, "IPX-64", 899.99m, 37, true, SeededAt, SeededAt },
                    { 15, 15, "IP6-64", 299.99m, 60, true, SeededAt, SeededAt },
                    { 16, 16, "IP5S-32", 199.99m, 25, true, SeededAt, SeededAt },
                    { 17, 17, "AIRPODS-GEN3", 129.99m, 67, true, SeededAt, SeededAt },
                    { 18, 18, "AIRPODSMAX-SLV", 549.99m, 59, true, SeededAt, SeededAt },
                    { 19, 19, "HOMEPODMINI-GRY", 99.99m, 27, true, SeededAt, SeededAt },
                    { 20, 20, "MAGSAFEBATT", 99.99m, 1, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ProductId", "VariantId", "ImageUrl", "SortOrder" },
                values: new object[,]
                {
                    { 13, 13, null, "https://cdn.dummyjson.com/product-images/smartphones/iphone-13-pro/thumbnail.webp", 0 },
                    { 14, 14, null, "https://cdn.dummyjson.com/product-images/smartphones/iphone-x/thumbnail.webp", 0 },
                    { 15, 15, null, "https://cdn.dummyjson.com/product-images/smartphones/iphone-6/thumbnail.webp", 0 },
                    { 16, 16, null, "https://cdn.dummyjson.com/product-images/smartphones/iphone-5s/thumbnail.webp", 0 },
                    { 17, 17, null, "https://cdn.dummyjson.com/product-images/mobile-accessories/apple-airpods/thumbnail.webp", 0 },
                    { 18, 18, null, "https://cdn.dummyjson.com/product-images/mobile-accessories/apple-airpods-max-silver/thumbnail.webp", 0 },
                    { 19, 19, null, "https://cdn.dummyjson.com/product-images/mobile-accessories/apple-homepod-mini-cosmic-grey/thumbnail.webp", 0 },
                    { 20, 20, null, "https://cdn.dummyjson.com/product-images/mobile-accessories/apple-magsafe-battery-pack/thumbnail.webp", 0 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id BETWEEN 13 AND 20;");
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id BETWEEN 13 AND 20;");
            migrationBuilder.Sql("DELETE FROM Products WHERE Id BETWEEN 13 AND 20;");
            migrationBuilder.Sql("DELETE FROM Categories WHERE Id = 5;");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIPhone18ProMacStudioAirTag : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // iPhone 18 Pro Max and Mac Pro were deliberately left out here: the
            // only free-licensed photos found were an unusable extreme close-up
            // crop and a pre-2013 cheese-grater tower (the wrong generation
            // entirely, the same mistake that caused the original photo mismatch
            // this session had to fix twice already). Wrong or absent beats
            // wrong and shipped.
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "Name", "Slug", "Description", "BasePrice", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 21, 1, "iPhone 18 Pro", "iphone-18-pro", "Apple's newest flagship, released just days ago, with the latest chip and camera system.", 1299.00m, true, SeededAt, SeededAt },
                    { 22, 2, "Mac Studio", "mac-studio", "A compact desktop workstation built for demanding creative and professional workflows.", 1999.00m, true, SeededAt, SeededAt },
                    { 23, 5, "AirTag", "airtag", "A small, easy-to-attach tracker that helps you keep track of your things.", 29.00m, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 21, 21, "IP18PRO-256", 1299.00m, 40, true, SeededAt, SeededAt },
                    { 22, 22, "MACSTUDIO-M4MAX-512", 1999.00m, 12, true, SeededAt, SeededAt },
                    { 23, 23, "AIRTAG-1PK", 29.00m, 120, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ProductId", "VariantId", "ImageUrl", "SortOrder" },
                values: new object[,]
                {
                    { 21, 21, null, "/img/products/iphone-18-pro.jpg", 0 },
                    { 22, 22, null, "/img/products/mac-studio.jpg", 0 },
                    { 23, 23, null, "/img/products/airtag.jpg", 0 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id BETWEEN 21 AND 23;");
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id BETWEEN 21 AND 23;");
            migrationBuilder.Sql("DELETE FROM Products WHERE Id BETWEEN 21 AND 23;");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAirPodsAndHomeCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Live apple.com/vn/store browsing (2026-09-23, see docs/ux-research.md)
            // shows AirPods and TV & Home as their own top-level categories, not
            // buried in Accessories. Split them out and move the existing
            // AirPods / AirPods Max / HomePod mini products into them.
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name", "Slug", "ParentId" },
                values: new object[,]
                {
                    { 6, "AirPods", "airpods", null },
                    { 7, "Home", "home", null },
                });

            migrationBuilder.UpdateData(table: "Products", keyColumn: "Id", keyValue: 17, column: "CategoryId", value: 6);
            migrationBuilder.UpdateData(table: "Products", keyColumn: "Id", keyValue: 18, column: "CategoryId", value: 6);
            migrationBuilder.UpdateData(table: "Products", keyColumn: "Id", keyValue: 19, column: "CategoryId", value: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(table: "Products", keyColumn: "Id", keyValue: 17, column: "CategoryId", value: 5);
            migrationBuilder.UpdateData(table: "Products", keyColumn: "Id", keyValue: 18, column: "CategoryId", value: 5);
            migrationBuilder.UpdateData(table: "Products", keyColumn: "Id", keyValue: 19, column: "CategoryId", value: 5);

            migrationBuilder.Sql("DELETE FROM Categories WHERE Id IN (6, 7);");
        }
    }
}

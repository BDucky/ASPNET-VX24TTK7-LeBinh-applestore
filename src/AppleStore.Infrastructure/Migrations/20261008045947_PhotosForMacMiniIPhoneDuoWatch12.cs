using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhotosForMacMiniIPhoneDuoWatch12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Licensed photos of these exact models (Wikimedia Commons, credited on
            // /Home/Credits), for products that had none. Found by slug, so the
            // Ids of an earlier seed do not matter.
            migrationBuilder.Sql("INSERT INTO ProductImages (ProductId, ImageUrl, SortOrder) SELECT Id, '/img/products/mac-mini-m4.jpg', 0 FROM Products WHERE Slug = 'mac-mini' AND NOT EXISTS (SELECT 1 FROM ProductImages WHERE ProductId = Products.Id)");
            migrationBuilder.Sql("INSERT INTO ProductImages (ProductId, ImageUrl, SortOrder) SELECT Id, '/img/products/iphone-duo.jpg', 0 FROM Products WHERE Slug = 'iphone-duo' AND NOT EXISTS (SELECT 1 FROM ProductImages WHERE ProductId = Products.Id)");
            migrationBuilder.Sql("INSERT INTO ProductImages (ProductId, ImageUrl, SortOrder) SELECT Id, '/img/products/apple-watch-series-12-commons.jpg', 0 FROM Products WHERE Slug = 'apple-watch-12' AND NOT EXISTS (SELECT 1 FROM ProductImages WHERE ProductId = Products.Id)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE ImageUrl = '/img/products/mac-mini-m4.jpg'");
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE ImageUrl = '/img/products/iphone-duo.jpg'");
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE ImageUrl = '/img/products/apple-watch-series-12-commons.jpg'");
        }
    }
}

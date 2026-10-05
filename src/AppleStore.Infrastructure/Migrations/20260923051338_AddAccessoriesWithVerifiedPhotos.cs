using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessoriesWithVerifiedPhotos : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // apple.com/vn/shop/accessories/all lists a much longer accessory
            // catalog than this (see docs/ux-research.md); these 8 are the ones
            // that survived per-item license research with a genuine, correctly
            // shaped/generation photo. Deliberately NOT added, no honest photo
            // found for the exact product: iPhone 18 Pro Silicone Case, MagSafe
            // Clear Case for iPhone 18 Pro Max, MagSafe Woven Wallet, iPhone Duo
            // Folio/Case (device unreleased), Smart Folio for iPad Pro (only a
            // Smart Keyboard Folio photo exists, a visibly different product),
            // USB-C Power Adapter 30W/35W, USB-C Braided Cable, Anker MagGo 5K
            // Power Bank, Solo Loop Band, Wrist Strap, Cross Strap, Chain Link
            // Magnetic Band (only a Milanese Loop photo exists, a different link
            // pattern), EarPods with USB-C (only Lightning/3.5mm photos exist),
            // AirTag Woven Keychain. Same "wrong or absent beats wrong and
            // shipped" standard as the rest of this catalog.
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "Name", "Slug", "Description", "BasePrice", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 35, 5, "MagSafe Charger", "magsafe-charger", "A magnetic wireless charger that snaps into place on any MagSafe-compatible iPhone.", 39.00m, true, SeededAt, SeededAt },
                    { 36, 5, "Magic Keyboard", "magic-keyboard", "A full-size wireless keyboard with a scissor mechanism and long battery life.", 99.00m, true, SeededAt, SeededAt },
                    { 37, 5, "Magic Mouse", "magic-mouse", "A rechargeable wireless mouse with a Multi-Touch surface for gestures.", 79.00m, true, SeededAt, SeededAt },
                    { 38, 5, "Magic Trackpad", "magic-trackpad", "A rechargeable wireless trackpad with Force Touch and a large glass surface.", 129.00m, true, SeededAt, SeededAt },
                    { 39, 5, "Apple Watch Sport Band", "apple-watch-sport-band", "A soft, durable fluoroelastomer band for Apple Watch, with a pin-and-tuck closure.", 49.00m, true, SeededAt, SeededAt },
                    { 40, 5, "Apple Pencil Pro", "apple-pencil-pro", "An Apple Pencil with a squeeze gesture, barrel roll, and haptic feedback.", 129.00m, true, SeededAt, SeededAt },
                    { 41, 5, "USB-C Digital AV Multiport Adapter", "usb-c-digital-av-multiport-adapter", "Connects a USB-C Mac or iPad to an HDMI display while passing through USB-C power.", 69.00m, true, SeededAt, SeededAt },
                    { 42, 5, "Studio Display", "studio-display", "A 27-inch 5K external display with a 12MP Center Stage camera and studio-quality mics.", 1599.00m, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 35, 35, "MAGSAFECHARGER", 39.00m, 90, true, SeededAt, SeededAt },
                    { 36, 36, "MAGICKEYBOARD", 99.00m, 40, true, SeededAt, SeededAt },
                    { 37, 37, "MAGICMOUSE", 79.00m, 50, true, SeededAt, SeededAt },
                    { 38, 38, "MAGICTRACKPAD", 129.00m, 35, true, SeededAt, SeededAt },
                    { 39, 39, "WATCHSPORTBAND-41", 49.00m, 75, true, SeededAt, SeededAt },
                    { 40, 40, "PENCILPRO-USBC", 129.00m, 55, true, SeededAt, SeededAt },
                    { 41, 41, "USBCAVADAPTER", 69.00m, 60, true, SeededAt, SeededAt },
                    { 42, 42, "STUDIODISPLAY-STD", 1599.00m, 10, true, SeededAt, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ProductId", "VariantId", "ImageUrl", "SortOrder" },
                values: new object[,]
                {
                    { 35, 35, null, "/img/products/magsafe-charger.png", 0 },
                    { 36, 36, null, "/img/products/magic-keyboard.jpg", 0 },
                    { 37, 37, null, "/img/products/magic-mouse.jpg", 0 },
                    { 38, 38, null, "/img/products/magic-trackpad.jpg", 0 },
                    { 39, 39, null, "/img/products/watch-sport-band.jpg", 0 },
                    { 40, 40, null, "/img/products/apple-pencil-pro.png", 0 },
                    { 41, 41, null, "/img/products/usbc-av-adapter.jpg", 0 },
                    { 42, 42, null, "/img/products/studio-display.jpg", 0 },
                });

            // AirTag is genuinely sold individually or as a 4-pack (confirmed
            // live on apple.com/vn/airtag); a second variant on the existing
            // product, not a new product, reusing its existing image.
            migrationBuilder.InsertData(
                table: "ProductVariants",
                columns: new[] { "Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 43, 23, "AIRTAG-4PK", 89.00m, 60, true, SeededAt, SeededAt },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id = 43;");
            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id BETWEEN 35 AND 42;");
            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id BETWEEN 35 AND 42;");
            migrationBuilder.Sql("DELETE FROM Products WHERE Id BETWEEN 35 AND 42;");
        }
    }
}

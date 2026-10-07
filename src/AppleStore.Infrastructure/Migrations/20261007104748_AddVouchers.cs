using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", unicode: false, maxLength: 40, nullable: false),
                    DiscountType = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    MinOrderAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsageLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    UsedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoucherProducts",
                columns: table => new
                {
                    VoucherId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherProducts", x => new { x.VoucherId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_VoucherProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoucherProducts_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Demo vouchers so checkout can be shown before the admin voucher
            // pages exist (task 9). Valid from 2026-10-01 to the end of 2027.
            var created = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "Vouchers",
                columns: new[] { "Id", "Code", "DiscountType", "DiscountValue", "MinOrderAmount", "StartsAt", "EndsAt", "UsageLimit", "UsedCount", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "WELCOME10", 0, 10m, null, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc), 1000, 0, true, created, created },
                    { 2, "GIAM500K", 1, 500000m, 10000000m, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc), 100, 0, true, created, created },
                    { 3, "AIRPODS15", 0, 15m, null, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc), 50, 0, true, created, created },
                });
            // AIRPODS15 applies to AirPods 5 and AirPods Pro 3 only.
            migrationBuilder.InsertData(
                table: "VoucherProducts",
                columns: new[] { "VoucherId", "ProductId" },
                values: new object[,] { { 3, 31 }, { 3, 32 } });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherProducts_ProductId",
                table: "VoucherProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoucherProducts");

            migrationBuilder.DropTable(
                name: "Vouchers");
        }
    }
}

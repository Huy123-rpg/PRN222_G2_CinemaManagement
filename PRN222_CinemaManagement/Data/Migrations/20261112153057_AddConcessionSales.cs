using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222_CinemaManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConcessionSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConcessionSales",
                columns: table => new
                {
                    ConcessionSaleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    SaleDate = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    PaymentMethod = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Cash"),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Completed"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionSales", x => x.ConcessionSaleId);
                    table.CheckConstraint("CK_ConcessionSales_Status", "[Status] IN ('Completed','Cancelled')");
                    table.CheckConstraint("CK_ConcessionSales_Total", "[TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_ConcessionSales_Staffs_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staffs",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConcessionSaleItems",
                columns: table => new
                {
                    ConcessionSaleItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConcessionSaleId = table.Column<int>(type: "int", nullable: false),
                    ConcessionItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, computedColumnSql: "CONVERT(decimal(18,2), [Quantity] * [UnitPrice])", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionSaleItems", x => x.ConcessionSaleItemId);
                    table.CheckConstraint("CK_ConcessionSaleItems_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_ConcessionSaleItems_UnitPrice", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_ConcessionSaleItems_ConcessionItems_ConcessionItemId",
                        column: x => x.ConcessionItemId,
                        principalTable: "ConcessionItems",
                        principalColumn: "ConcessionItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConcessionSaleItems_ConcessionSales_ConcessionSaleId",
                        column: x => x.ConcessionSaleId,
                        principalTable: "ConcessionSales",
                        principalColumn: "ConcessionSaleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionSaleItems_Item",
                table: "ConcessionSaleItems",
                column: "ConcessionItemId");

            migrationBuilder.CreateIndex(
                name: "UQ_ConcessionSaleItems_Item",
                table: "ConcessionSaleItems",
                columns: new[] { "ConcessionSaleId", "ConcessionItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionSales_Date",
                table: "ConcessionSales",
                column: "SaleDate");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionSales_SaleCode",
                table: "ConcessionSales",
                column: "SaleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionSales_Staff",
                table: "ConcessionSales",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConcessionSaleItems");

            migrationBuilder.DropTable(
                name: "ConcessionSales");
        }
    }
}

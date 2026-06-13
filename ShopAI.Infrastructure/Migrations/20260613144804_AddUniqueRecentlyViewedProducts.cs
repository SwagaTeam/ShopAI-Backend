using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueRecentlyViewedProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RecentlyViewedProduct"
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM (
                        SELECT
                            "Id",
                            ROW_NUMBER() OVER (
                                PARTITION BY "UserId", "ProductId"
                                ORDER BY "ViewedAtUtc" DESC, "Id" DESC
                            ) AS row_number
                        FROM "RecentlyViewedProduct"
                    ) duplicates
                    WHERE duplicates.row_number > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RecentlyViewedProduct_UserId_ProductId",
                table: "RecentlyViewedProduct",
                columns: new[] { "UserId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecentlyViewedProduct_UserId_ProductId",
                table: "RecentlyViewedProduct");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_ShopId",
                table: "Categories");

            migrationBuilder.AddColumn<Guid>(
                name: "GlobalCategoryId",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GlobalCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_GlobalCategoryId",
                table: "Categories",
                column: "GlobalCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ShopId_Name_ParentCategoryId",
                table: "Categories",
                columns: new[] { "ShopId", "Name", "ParentCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalCategories_Slug",
                table: "GlobalCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalCategories_SortOrder",
                table: "GlobalCategories",
                column: "SortOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_GlobalCategories_GlobalCategoryId",
                table: "Categories",
                column: "GlobalCategoryId",
                principalTable: "GlobalCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_GlobalCategories_GlobalCategoryId",
                table: "Categories");

            migrationBuilder.DropTable(
                name: "GlobalCategories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_GlobalCategoryId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ShopId_Name_ParentCategoryId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "GlobalCategoryId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ShopId",
                table: "Categories",
                column: "ShopId");
        }
    }
}

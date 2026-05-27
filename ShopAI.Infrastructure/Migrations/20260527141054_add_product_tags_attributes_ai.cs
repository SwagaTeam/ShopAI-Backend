using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class add_product_tags_attributes_ai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                table: "Products",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Products",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttributesJson",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Products");
        }
    }
}

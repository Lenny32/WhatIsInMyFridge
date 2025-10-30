using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatIsInMyFridge.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnitColumnFromGroceryItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "GroceryItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "GroceryItems",
                type: "TEXT",
                nullable: true);
        }
    }
}

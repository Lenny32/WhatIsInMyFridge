using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatIsInMyFridge.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Photos",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Photos",
                table: "Recipes");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "AspNetUsers");
        }
    }
}

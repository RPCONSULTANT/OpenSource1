using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddSoftDelete : Migration
    {
        // NOTA (Task 1.10, corrección respecto al scaffold automático): "xmin" es una columna de
        // sistema de PostgreSQL, no una columna física propia de la aplicación. Cuando la Task 1.9
        // mapeó "xmin" como shadow property (IsRowVersion) en las 4 entidades, EF Core la trató
        // como cualquier otra propiedad nueva del modelo y `dotnet ef migrations add` generó aquí
        // un AddColumn/DropColumn real para ella junto con las columnas legítimas de soft delete.
        // Esa parte del scaffold se eliminó a mano: PostgreSQL rechaza crear una columna llamada
        // "xmin" —verificado contra Postgres real: "ERROR: column name "xmin" conflicts with a
        // system column name"—, así que aplicar el scaffold tal cual habría roto esta migración.
        // "xmin" sigue mapeada en ApplicationDbContext (OnModelCreating) para lectura/concurrencia
        // vía el snapshot del modelo; simplemente no requiere (ni admite) DDL propio.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Productos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Productos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Productos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Entradas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Entradas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Entradas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Clientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Clientes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Clientes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "AppSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AppSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Entradas");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Entradas");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Entradas");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AppSettings");
        }
    }
}
